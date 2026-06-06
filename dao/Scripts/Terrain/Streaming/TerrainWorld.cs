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
    private readonly TerrainTileDataCache _tileCache = new();
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
        TerrainWorldRuntimeLifecycleService.OnReady(this);
    }

    public override void _Process(double delta)
    {
        TerrainWorldRuntimeLifecycleService.OnProcess(this, delta);
    }

    public override void _ExitTree()
    {
        TerrainWorldRuntimeLifecycleService.OnExitTree(this);
    }

    /// <summary>Sets the camera or player focus node and forces a streaming update.</summary>
    public void SetFocus(Node3D focus)
    {
        TerrainWorldRuntimeLifecycleService.SetFocus(this, focus);
    }

    /// <summary>Sets or clears the world plan, rebuilding corridor and POI indices and invalidating the tile cache.</summary>
    public void SetWorldPlan(TerrainWorldPlan? worldPlan)
    {
        TerrainWorldRuntimeLifecycleService.SetWorldPlan(this, worldPlan);
    }

    /// <summary>Regenerates the terrain profile and rebuilds all streaming state.</summary>
    public void Regenerate()
    {
        TerrainWorldRuntimeLifecycleService.Regenerate(this);
    }

    private void InvalidatePlanDependentStreamingState()
    {
        TerrainWorldRuntimeLifecycleService.InvalidatePlanDependentStreamingState(this);
    }

    /// <summary>Builds a new open-world plan for this terrain world and optionally applies it to streaming tiles.</summary>
    public TerrainWorldPlan GenerateOpenWorldPlan(bool apply = true)
    {
        return TerrainWorldRuntimeLifecycleService.GenerateOpenWorldPlan(this, apply);
    }

    private void EnsureGeneratedWorldPlan()
    {
        TerrainWorldRuntimeLifecycleService.EnsureGeneratedWorldPlan(this);
    }

    private void EnsureProfileSnapshot()
    {
        TerrainWorldRuntimeLifecycleService.EnsureProfileSnapshot(this);
    }

    private void PrepareGeneratedWorldPlan()
    {
        TerrainWorldPlanLifecycleService.PrepareGeneratedWorldPlan(this);
    }

    private void StartOpenWorldPlanJob()
    {
        TerrainWorldPlanLifecycleService.StartOpenWorldPlanJob(this);
    }

    private void SubmitCompletedWorldPlanJob()
    {
        TerrainWorldPlanLifecycleService.SubmitCompletedWorldPlanJob(this);
    }

    private void CancelWorldPlanJob()
    {
        TerrainWorldPlanLifecycleService.CancelWorldPlanJob(this);
    }

    private void ApplyPlanIndexChanges(bool hadPlanBeforeChange)
    {
        TerrainWorldPlanLifecycleService.ApplyPlanIndexChanges(this, hadPlanBeforeChange);
    }

    private void ReportGeneratedOpenWorldPlan(TerrainWorldPlan plan)
    {
        TerrainWorldPlanLifecycleService.ReportGeneratedOpenWorldPlan(this, plan);
    }

    private void MarkStreamingSnapshotDirty()
    {
        TerrainWorldSignalDispatchService.MarkStreamingSnapshotDirty(this);
    }

    private void EmitPlanReadySignalIfReady()
    {
        TerrainWorldSignalDispatchService.EmitPlanReadySignalIfReady(this);
    }

    private void EmitPlanClearedSignalIfReady()
    {
        TerrainWorldSignalDispatchService.EmitPlanClearedSignalIfReady(this);
    }

    private void EmitChunkLoadedSignalIfReady(TerrainTileData data)
    {
        TerrainWorldSignalDispatchService.EmitChunkLoadedSignalIfReady(this, data);
    }

    private void EmitChunkUnloadedSignalIfReady(TerrainChunk chunk)
    {
        TerrainWorldSignalDispatchService.EmitChunkUnloadedSignalIfReady(this, chunk);
    }

    private void EmitStreamingSnapshotChangedSignalIfNeeded()
    {
        TerrainWorldSignalDispatchService.EmitStreamingSnapshotChangedSignalIfNeeded(this);
    }

}
