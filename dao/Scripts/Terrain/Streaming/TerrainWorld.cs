using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dao.Terrain;
using Dao.Terrain.Generation;
using Dao.Terrain.Rendering;
using Godot;

namespace Dao.Terrain.Streaming;

/// <summary>Streaming terrain world that manages chunk loading/unloading, tile caching, and asynchronous generation job scheduling.</summary>
[GlobalClass]
public partial class TerrainWorld : Node3D, ITerrainQueryService, ITerrainPlanProvider, ITerrainStreamingDiagnostics, ITerrainPlacementService, ITerrainNavigationProvider
{
    private const string PlanReadySignalName = "PlanReady";
    private const string PlanClearedSignalName = "PlanCleared";
    private const string ChunkLoadedSignalName = "ChunkLoaded";
    private const string ChunkUnloadedSignalName = "ChunkUnloaded";
    private const string StreamingSnapshotChangedSignalName = "StreamingSnapshotChanged";

    [Signal] public delegate void PlanReadyEventHandler();
    [Signal] public delegate void PlanClearedEventHandler();
    [Signal] public delegate void ChunkLoadedEventHandler(int x, int z, int lod, bool hasCollision);
    [Signal] public delegate void ChunkUnloadedEventHandler(int x, int z, int lod, bool hadCollision);
    [Signal] public delegate void StreamingSnapshotChangedEventHandler();

    [Export] public TerrainSettings? Settings { get; set; }
    [Export] public NodePath FocusPath { get; set; } = new();
    [Export(PropertyHint.Range, "0.05,2,0.01")] public double StreamingIntervalSeconds { get; set; } = 0.18;
    [Export] public bool CreateWaterPlane { get; set; } = true;
    [ExportGroup("Open World Planning")]
    [Export] public bool GenerateOpenWorldPlanOnReady { get; set; } = true;
    [Export] public bool GenerateOpenWorldPlanAsync { get; set; } = true;
    [Export] public bool StreamTerrainBeforeOpenWorldPlanReady { get; set; } = true;
    [Export] public bool ValidateGeneratedOpenWorldPlan { get; set; } = true;
    [Export] public bool PrintGeneratedOpenWorldPlanSummary { get; set; } = false;
    [Export(PropertyHint.Range, "1024,65536,1")] public float OpenWorldPlanWorldSize { get; set; } = 12_288.0f;

    private readonly Dictionary<TerrainTileCoord, TerrainChunk> _chunks = new();
    private readonly Dictionary<TerrainTileCoord, PendingTileJob> _jobs = new();
    private readonly Dictionary<TerrainTileCacheKey, TerrainTileData> _tileCache = new();
    private readonly Dictionary<TerrainTileCacheKey, LinkedListNode<TerrainTileCacheKey>> _tileCacheNodes = new();
    private readonly LinkedList<TerrainTileCacheKey> _tileCacheLru = new();
    private readonly List<PendingTileJob> _retiredJobs = new();
    private readonly HashSet<TerrainTileCoord> _desiredCoords = new();
    private PendingWorldPlanJob? _worldPlanJob;
    private TerrainGenerationProfile _profile;
    private TerrainWorldPlan? _worldPlan;
    private TerrainRouteCorridorIndex _routeCorridors = TerrainRouteCorridorIndex.Empty;
    private TerrainPointOfInterestIndex _pointOfInterestIndex = TerrainPointOfInterestIndex.Empty;
    private Node3D? _focus;
    private Material _terrainMaterial = null!;
    private Material _waterMaterial = null!;
    private Material _localWaterMaterial = null!;
    private MeshInstance3D? _waterPlane;
    private double _streamTimer;
    private int _worldPlanGenerationVersion;
    private int _streamingStateRevision;
    private int _emittedStreamingStateRevision;
    private bool _hasProfileSnapshot;
    private bool _isReady;

    /// <summary>Current immutable terrain generation profile used by streaming jobs.</summary>
    public TerrainGenerationProfile Profile => CurrentProfile;

    /// <summary>Snapshot copy of the current open-world plan driving route corridors, POI footprints, settlements, and gameplay landmarks.</summary>
    public TerrainWorldPlan? WorldPlan => _worldPlan is null ? null : TerrainWorldPlan.CopyOf(_worldPlan);

    /// <summary>True while the runtime open-world plan is being generated on a worker thread.</summary>
    public bool IsOpenWorldPlanGenerationPending => _worldPlanJob is not null;

    private TerrainGenerationProfile CurrentProfile
    {
        get
        {
            EnsureProfileSnapshot();
            return _profile;
        }
    }

    public override void _Ready()
    {
        Settings ??= new TerrainSettings();
        _profile = Settings.Snapshot();
        _hasProfileSnapshot = true;
        EnsureGeneratedWorldPlan();
        RebuildPlanIndices();
        if (_profile.UseNativeSamplerWhenAvailable)
        {
            NativeTerrainBridge.EnsureInitialized();
            if (!NativeTerrainBridge.IsAvailable)
            {
                GD.PushWarning("Native terrain sampler requested but unavailable; using managed C# sampler.");
            }
        }

        _terrainMaterial = TerrainMaterialFactory.CreateTerrainMaterial();
        _waterMaterial = TerrainMaterialFactory.CreateWaterMaterial();
        _localWaterMaterial = TerrainMaterialFactory.CreateLocalWaterMaterial();

        ResolveFocus();

        if (CreateWaterPlane)
        {
            CreateWater();
        }

        _isReady = true;
        if (_worldPlan is not null)
        {
            EmitPlanReadySignalIfReady();
            MarkStreamingSnapshotDirty();
        }

        UpdateStreaming(force: true);
        EmitStreamingSnapshotChangedSignalIfNeeded();
    }

    public override void _Process(double delta)
    {
        DisposeCompletedRetiredJobs();
        SubmitCompletedWorldPlanJob();
        SubmitCompletedJobs();

        _streamTimer += delta;
        if (_streamTimer >= StreamingIntervalSeconds)
        {
            _streamTimer = 0.0;
            UpdateStreaming(force: false);
        }

        UpdateWaterPlane();
        EmitStreamingSnapshotChangedSignalIfNeeded();
    }

    public override void _ExitTree()
    {
        _worldPlanGenerationVersion++;
        CancelWorldPlanJob();
        CancelAllJobs();
        DisposeCompletedRetiredJobs();
    }

    /// <summary>Sets the camera or player focus node and forces a streaming update.</summary>
    public void SetFocus(Node3D focus)
    {
        _focus = focus;
        MarkStreamingSnapshotDirty();
        UpdateStreaming(force: true);
        EmitStreamingSnapshotChangedSignalIfNeeded();
    }

    /// <summary>Sets or clears the world plan, rebuilding corridor and POI indices and invalidating the tile cache.</summary>
    public void SetWorldPlan(TerrainWorldPlan? worldPlan)
    {
        bool hadPlan = _worldPlan is not null;
        EnsureProfileSnapshot();
        _worldPlanGenerationVersion++;
        CancelWorldPlanJob();
        _worldPlan = worldPlan is null ? null : TerrainWorldPlan.CopyOf(worldPlan);
        ApplyPlanIndexChanges(hadPlan);
        EmitStreamingSnapshotChangedSignalIfNeeded();
    }

    /// <summary>Regenerates the terrain profile and rebuilds all streaming state.</summary>
    public void Regenerate()
    {
        bool hadPlan = _worldPlan is not null;
        Settings ??= new TerrainSettings();
        _profile = Settings.Snapshot();
        _hasProfileSnapshot = true;
        if (GenerateOpenWorldPlanOnReady)
        {
            _worldPlan = null;
            if (hadPlan)
            {
                EmitPlanClearedSignalIfReady();
                MarkStreamingSnapshotDirty();
            }

            PrepareGeneratedWorldPlan();
            RebuildPlanIndices();
            if (!GenerateOpenWorldPlanAsync && _worldPlan is not null)
            {
                EmitPlanReadySignalIfReady();
                MarkStreamingSnapshotDirty();
            }
        }
        else
        {
            RebuildPlanIndices();
        }

        if (_profile.UseNativeSamplerWhenAvailable)
        {
            NativeTerrainBridge.EnsureInitialized();
        }

        InvalidatePlanDependentStreamingState();
        UpdateStreaming(force: true);
        EmitStreamingSnapshotChangedSignalIfNeeded();
    }

    private void InvalidatePlanDependentStreamingState()
    {
        CancelAllJobs();
        ClearTileCache();
        ClearChunks();
    }

    /// <summary>Builds a new open-world plan for this terrain world and optionally applies it to streaming tiles.</summary>
    public TerrainWorldPlan GenerateOpenWorldPlan(bool apply = true)
    {
        Settings ??= new TerrainSettings();
        if (!_isReady)
        {
            _profile = Settings.Snapshot();
            _hasProfileSnapshot = true;
        }

        _worldPlanGenerationVersion++;
        CancelWorldPlanJob();
        float worldSize = Mathf.Max(_profile.ChunkSize, OpenWorldPlanWorldSize);
        TerrainWorldPlan plan = CreateRuntimeOpenWorldPlan(_profile, worldSize);

        if (ValidateGeneratedOpenWorldPlan || PrintGeneratedOpenWorldPlanSummary)
        {
            ReportGeneratedOpenWorldPlan(plan);
        }

        if (apply)
        {
            SetWorldPlan(plan);
        }

        return plan;
    }

    private void EnsureGeneratedWorldPlan()
    {
        if (!GenerateOpenWorldPlanOnReady || _worldPlan is not null)
        {
            return;
        }

        PrepareGeneratedWorldPlan();
    }

    private void EnsureProfileSnapshot()
    {
        if (_hasProfileSnapshot)
        {
            return;
        }

        Settings ??= new TerrainSettings();
        _profile = Settings.Snapshot();
        _hasProfileSnapshot = true;
    }

    private void PrepareGeneratedWorldPlan()
    {
        if (GenerateOpenWorldPlanAsync)
        {
            StartOpenWorldPlanJob();
            return;
        }

        _worldPlan = GenerateOpenWorldPlan(apply: false);
    }

    private void StartOpenWorldPlanJob()
    {
        CancelWorldPlanJob();
        _worldPlanGenerationVersion++;
        TerrainGenerationProfile planProfile = _profile;
        float worldSize = Mathf.Max(planProfile.ChunkSize, OpenWorldPlanWorldSize);
        var cancellation = new CancellationTokenSource();
        Task<TerrainWorldPlan> task = CreateRuntimeOpenWorldPlanAsync(planProfile, worldSize, cancellation.Token);
        ObserveWorldPlanTaskCompletion(task);
        _worldPlanJob = new PendingWorldPlanJob(_worldPlanGenerationVersion, planProfile, worldSize, cancellation, task);
        MarkStreamingSnapshotDirty();
    }

    private void SubmitCompletedWorldPlanJob()
    {
        if (_worldPlanJob is not { } job || !job.Task.IsCompleted)
        {
            return;
        }

        bool hadPlan = _worldPlan is not null;
        _worldPlanJob = null;
        MarkStreamingSnapshotDirty();
        if (job.Task.IsCanceled)
        {
            job.Cancellation.Dispose();
            return;
        }

        if (job.Task.IsFaulted)
        {
            job.Cancellation.Dispose();
            GD.PushError($"Open world terrain plan generation failed: {job.Task.Exception?.GetBaseException().Message}");
            return;
        }

        if (job.Version != _worldPlanGenerationVersion ||
            !job.Profile.Equals(_profile) ||
            !Mathf.IsEqualApprox(job.WorldSize, Mathf.Max(_profile.ChunkSize, OpenWorldPlanWorldSize)))
        {
            job.Cancellation.Dispose();
            return;
        }

        TerrainWorldPlan plan = job.Task.Result;
        job.Cancellation.Dispose();
        if (ValidateGeneratedOpenWorldPlan || PrintGeneratedOpenWorldPlanSummary)
        {
            ReportGeneratedOpenWorldPlan(plan);
        }

        _worldPlan = plan;
        ApplyPlanIndexChanges(hadPlan);
    }

    private void CancelWorldPlanJob()
    {
        if (_worldPlanJob is not { } job)
        {
            return;
        }

        _worldPlanJob = null;
        MarkStreamingSnapshotDirty();
        if (job.Task.IsCompleted)
        {
            job.Cancellation.Dispose();
            return;
        }

        job.Cancellation.Cancel();
        ObserveRetiredWorldPlanTaskCompletion(job.Task, job.Cancellation);
    }

    private void ApplyPlanIndexChanges(bool hadPlanBeforeChange)
    {
        int previousKey = TerrainFeatureKey;
        bool hasPlanNow = _worldPlan is not null;
        RebuildPlanIndices();
        MarkStreamingSnapshotDirty();

        if (!_isReady)
        {
            return;
        }

        if (hadPlanBeforeChange && !hasPlanNow)
        {
            EmitPlanClearedSignalIfReady();
        }
        else if (hasPlanNow)
        {
            EmitPlanReadySignalIfReady();
        }

        if (previousKey == TerrainFeatureKey)
        {
            return;
        }

        InvalidatePlanDependentStreamingState();
        UpdateStreaming(force: true);
    }

    private void ReportGeneratedOpenWorldPlan(TerrainWorldPlan plan)
    {
        TerrainWorldPlanningGateResult planningGate = TerrainWorldPlanner.ValidateOpenWorldPlanning(plan);
        TerrainQualityGateResult qualityGate = TerrainQualityAnalyzer.ValidateOpenWorldDefault(plan.QualityReport);
        TerrainExperienceGateResult experienceGate = TerrainExperienceAnalyzer.ValidateOpenWorldDefault(plan.ExperienceReport);
        bool passed = planningGate.Passed && qualityGate.Passed && experienceGate.Passed;

        if (PrintGeneratedOpenWorldPlanSummary)
        {
            GD.Print(
                $"Open world terrain plan {(passed ? "PASS" : "FAIL")}: " +
                $"{planningGate.Report.PointOfInterestCount} POIs, {planningGate.Report.RouteCount} routes, " +
                $"settlements V/T/O {planningGate.Report.VillageCount}/{planningGate.Report.TownCount}/{planningGate.Report.OasisHubCount}, " +
                $"land {qualityGate.Report.LandRatio:0.000}, scenic {qualityGate.Report.ScenicRatio:0.000}, " +
                $"encounter {experienceGate.Report.AverageEncounterPotential:0.000}, rhythm {experienceGate.Report.RouteRhythmScore:0.000}, " +
                $"connected {planningGate.Report.ConnectedPointRatio:0.000}, " +
                $"settlement net {planningGate.Report.ConnectedSettlementRatio:0.000}/{planningGate.Report.SettlementRouteCount}, " +
                $"coverage {planningGate.Report.PointOfInterestWorldCoverage:0.000}/{planningGate.Report.RouteWorldCoverage:0.000}.");
        }

        if (ValidateGeneratedOpenWorldPlan && !passed)
        {
            GD.PushWarning(
                $"Generated open world terrain plan failed readiness gates. " +
                $"Planning gate: {planningGate.Passed}, quality gate: {qualityGate.Passed}, experience gate: {experienceGate.Passed}.");
        }
    }

    private void MarkStreamingSnapshotDirty()
    {
        _streamingStateRevision++;
    }

    private void EmitPlanReadySignalIfReady()
    {
        if (!_isReady)
        {
            return;
        }

        EmitSignal(PlanReadySignalName);
    }

    private void EmitPlanClearedSignalIfReady()
    {
        if (!_isReady)
        {
            return;
        }

        EmitSignal(PlanClearedSignalName);
    }

    private void EmitChunkLoadedSignalIfReady(TerrainTileData data)
    {
        if (!_isReady)
        {
            return;
        }

        EmitSignal(ChunkLoadedSignalName, data.Coord.X, data.Coord.Z, data.Lod, data.CollisionFaces.Length > 0);
    }

    private void EmitChunkUnloadedSignalIfReady(TerrainChunk chunk)
    {
        if (!_isReady)
        {
            return;
        }

        EmitSignal(ChunkUnloadedSignalName, chunk.Coord.X, chunk.Coord.Z, chunk.Lod, chunk.HasCollision);
    }

    private void EmitStreamingSnapshotChangedSignalIfNeeded()
    {
        if (!_isReady || _emittedStreamingStateRevision == _streamingStateRevision)
        {
            return;
        }

        _emittedStreamingStateRevision = _streamingStateRevision;
        EmitSignal(StreamingSnapshotChangedSignalName);
    }

    private static void ObserveWorldPlanTaskCompletion(Task<TerrainWorldPlan> task)
    {
        _ = task.ContinueWith(
            static completed =>
            {
                if (completed.IsFaulted)
                {
                    _ = completed.Exception;
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void ObserveRetiredWorldPlanTaskCompletion(
        Task<TerrainWorldPlan> task,
        CancellationTokenSource cancellation)
    {
        _ = task.ContinueWith(
            static (completed, state) =>
            {
                if (completed.IsFaulted)
                {
                    _ = completed.Exception;
                }

                ((CancellationTokenSource)state!).Dispose();
            },
            cancellation,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private sealed record PendingTileJob(
        TerrainTileCoord Coord,
        int Lod,
        bool IncludeCollision,
        TerrainGenerationProfile Profile,
        int TerrainFeatureKey,
        CancellationTokenSource Cancellation,
        Task<TerrainTileData> Task);

    private sealed record PendingWorldPlanJob(
        int Version,
        TerrainGenerationProfile Profile,
        float WorldSize,
        CancellationTokenSource Cancellation,
        Task<TerrainWorldPlan> Task);

    private readonly record struct DesiredTileRequest(int Lod, bool IncludeCollision);
    private readonly record struct TerrainTileCacheKey(
        TerrainTileCoord Coord,
        int Lod,
        bool IncludeCollision,
        TerrainGenerationProfile Profile,
        int TerrainFeatureKey);
}
