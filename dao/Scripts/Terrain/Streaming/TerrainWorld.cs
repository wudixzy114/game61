using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dao.Terrain.Generation;
using Dao.Terrain.Rendering;
using Godot;

namespace Dao.Terrain.Streaming;

/// <summary>Streaming terrain world that manages chunk loading/unloading, tile caching, and asynchronous generation job scheduling.</summary>
[GlobalClass]
public partial class TerrainWorld : Node3D
{
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
    private bool _hasProfileSnapshot;
    private bool _isReady;

    /// <summary>Current immutable terrain generation profile used by streaming jobs.</summary>
    public TerrainGenerationProfile Profile => CurrentProfile;

    /// <summary>Current open-world plan driving route corridors, POI footprints, settlements, and gameplay landmarks.</summary>
    public TerrainWorldPlan? WorldPlan => _worldPlan;
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

        UpdateStreaming(force: true);
        _isReady = true;
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
        UpdateStreaming(force: true);
    }

    /// <summary>Sets or clears the world plan, rebuilding corridor and POI indices and invalidating the tile cache.</summary>
    public void SetWorldPlan(TerrainWorldPlan? worldPlan)
    {
        EnsureProfileSnapshot();
        _worldPlanGenerationVersion++;
        CancelWorldPlanJob();
        _worldPlan = worldPlan;
        ApplyPlanIndexChanges();
    }

    /// <summary>Regenerates the terrain profile and rebuilds all streaming state.</summary>
    public void Regenerate()
    {
        Settings ??= new TerrainSettings();
        _profile = Settings.Snapshot();
        _hasProfileSnapshot = true;
        if (GenerateOpenWorldPlanOnReady)
        {
            _worldPlan = null;
            PrepareGeneratedWorldPlan();
        }

        RebuildPlanIndices();

        if (_profile.UseNativeSamplerWhenAvailable)
        {
            NativeTerrainBridge.EnsureInitialized();
        }

        CancelAllJobs();
        ClearTileCache();
        ClearChunks();
        UpdateStreaming(force: true);
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

    /// <summary>Samples the complete terrain semantic field at a world XZ position using this world's current profile.</summary>
    public TerrainWorldField SampleField(Vector2 world)
    {
        return TerrainWorldFieldSampler.Sample(world, CurrentProfile);
    }

    /// <summary>Samples height, slope, biome, landscape, traversability, and surface color at a world XZ position.</summary>
    public TerrainSample SampleSurface(Vector2 world, float spacing = 4.0f)
    {
        return TerrainSampler.SampleWithSlope(world, CurrentProfile, spacing);
    }

    /// <summary>Returns a Godot 3D surface position for a world XZ query, using X/Z as horizontal axes and Y as height.</summary>
    public Vector3 SurfacePositionAt(Vector2 world, float heightOffset = 0.0f)
    {
        TerrainWorldField field = SampleField(world);
        return new Vector3(world.X, field.Height + heightOffset, world.Y);
    }

    /// <summary>Returns the current open-world plan if one has been generated or assigned, without generating one synchronously.</summary>
    public bool TryGetWorldPlan([NotNullWhen(true)] out TerrainWorldPlan? plan)
    {
        plan = _worldPlan;
        return plan is not null;
    }

    /// <summary>Returns a snapshot copy of the current plan's points of interest, or an empty array when no plan is ready.</summary>
    public TerrainWorldPointOfInterest[] GetPointsOfInterest()
    {
        return _worldPlan is null
            ? Array.Empty<TerrainWorldPointOfInterest>()
            : _worldPlan.PointsOfInterest.ToArray();
    }

    /// <summary>Returns a snapshot copy of the current plan's routes and waypoint arrays, or an empty array when no plan is ready.</summary>
    public TerrainWorldRoute[] GetRoutes()
    {
        if (_worldPlan is null)
        {
            return Array.Empty<TerrainWorldRoute>();
        }

        TerrainWorldRoute[] routes = _worldPlan.Routes;
        var copy = new TerrainWorldRoute[routes.Length];
        for (int i = 0; i < routes.Length; i++)
        {
            copy[i] = routes[i] with { Waypoints = routes[i].Waypoints.ToArray() };
        }

        return copy;
    }

    /// <summary>Returns whether the sampled terrain field meets the requested traversability threshold.</summary>
    public bool IsTraversable(Vector2 world, float minTraversability = 0.45f)
    {
        float threshold = Mathf.Clamp(minTraversability, 0.0f, 1.0f);
        return SampleField(world).Traversability >= threshold;
    }

    /// <summary>Returns whether sampled terrain height is above this world's sea level plus an optional margin.</summary>
    public bool IsAboveWater(Vector2 world, float margin = 0.0f)
    {
        TerrainGenerationProfile profile = CurrentProfile;
        return TerrainWorldFieldSampler.Sample(world, profile).Height >= profile.SeaLevel + margin;
    }

    /// <summary>Creates the open-world plan used by TerrainWorld runtime streaming for a profile and world size.</summary>
    public static TerrainWorldPlan CreateRuntimeOpenWorldPlan(
        TerrainGenerationProfile profile,
        float worldSize,
        CancellationToken cancellationToken = default)
    {
        return CreateRuntimeOpenWorldPlan(profile, Vector2.Zero, worldSize, cancellationToken);
    }

    /// <summary>Creates the open-world plan used by TerrainWorld runtime streaming on a background worker.</summary>
    public static Task<TerrainWorldPlan> CreateRuntimeOpenWorldPlanAsync(
        TerrainGenerationProfile profile,
        float worldSize,
        CancellationToken cancellationToken = default)
    {
        return CreateRuntimeOpenWorldPlanAsync(profile, Vector2.Zero, worldSize, cancellationToken);
    }

    /// <summary>Creates the open-world plan used by TerrainWorld runtime streaming on a background worker.</summary>
    public static Task<TerrainWorldPlan> CreateRuntimeOpenWorldPlanAsync(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        CancellationToken cancellationToken = default)
    {
        float safeWorldSize = Mathf.Max(profile.ChunkSize, worldSize);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return CreateRuntimeOpenWorldPlan(profile, center, safeWorldSize, cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>Creates the open-world plan used by TerrainWorld runtime streaming for a profile, center, and world size.</summary>
    public static TerrainWorldPlan CreateRuntimeOpenWorldPlan(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        CancellationToken cancellationToken = default)
    {
        return TerrainWorldPlanner.CreateOpenWorldPlan(
            profile,
            center,
            Mathf.Max(profile.ChunkSize, worldSize),
            cancellationToken);
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
    }

    private void SubmitCompletedWorldPlanJob()
    {
        if (_worldPlanJob is not { } job || !job.Task.IsCompleted)
        {
            return;
        }

        _worldPlanJob = null;
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
        ApplyPlanIndexChanges();
    }

    private void CancelWorldPlanJob()
    {
        if (_worldPlanJob is not { } job)
        {
            return;
        }

        _worldPlanJob = null;
        if (job.Task.IsCompleted)
        {
            job.Cancellation.Dispose();
            return;
        }

        job.Cancellation.Cancel();
        ObserveRetiredWorldPlanTaskCompletion(job.Task, job.Cancellation);
    }

    private void ApplyPlanIndexChanges()
    {
        int previousKey = TerrainFeatureKey;
        RebuildPlanIndices();

        if (!_isReady || previousKey == TerrainFeatureKey)
        {
            return;
        }

        CancelAllJobs();
        ClearTileCache();
        ClearChunks();
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

    private void ResolveFocus()
    {
        if (_focus is not null && IsInstanceValid(_focus))
        {
            return;
        }

        if (!FocusPath.IsEmpty)
        {
            _focus = GetNodeOrNull<Node3D>(FocusPath);
        }

        _focus ??= GetViewport()?.GetCamera3D();
    }

    private void UpdateStreaming(bool force)
    {
        if (_worldPlanJob is not null && !StreamTerrainBeforeOpenWorldPlanReady)
        {
            return;
        }

        ResolveFocus();
        if (_focus is null)
        {
            return;
        }

        TerrainTileCoord center = TerrainTileCoord.FromWorldPosition(_focus.GlobalPosition, _profile.ChunkSize);
        BuildDesiredSet(center);
        UnloadUndesiredChunks();
        DropStaleJobs(center);
        QueueMissingOrOutdatedChunks(center);
    }

    private void BuildDesiredSet(TerrainTileCoord center)
    {
        _desiredCoords.Clear();
        int radius = _profile.StreamRadiusChunks;
        int radiusSquared = radius * radius;

        for (int z = -radius; z <= radius; z++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if ((x * x) + (z * z) > radiusSquared)
                {
                    continue;
                }

                _desiredCoords.Add(new TerrainTileCoord(center.X + x, center.Z + z));
            }
        }
    }

    private void QueueMissingOrOutdatedChunks(TerrainTileCoord center)
    {
        if (_focus is null)
        {
            return;
        }

        Vector3 focusPosition = _focus.GlobalPosition;
        List<TerrainTileCoord> sorted = _desiredCoords
            .OrderBy(coord => coord.CenterDistanceSquaredTo(focusPosition, _profile.ChunkSize))
            .ToList();
        int cachedApplied = 0;
        TerrainRouteCorridorIndex routeCorridors = _routeCorridors;
        TerrainPointOfInterestIndex pointOfInterestIndex = _pointOfInterestIndex;
        int terrainFeatureKey = TerrainFeatureKey;

        foreach (TerrainTileCoord coord in sorted)
        {
            DesiredTileRequest request = GetDesiredRequest(coord, center);
            TerrainTileCacheKey cacheKey = new(coord, request.Lod, request.IncludeCollision, _profile, terrainFeatureKey);

            if (_chunks.TryGetValue(coord, out TerrainChunk? chunk) &&
                chunk.Lod == request.Lod &&
                chunk.HasCollision == request.IncludeCollision)
            {
                continue;
            }

            TerrainTileData? cachedTile = GetCachedTile(cacheKey);
            if (cachedTile is not null)
            {
                if (cachedApplied >= _profile.MaxCompletedTilesPerFrame)
                {
                    continue;
                }

                ApplyTileData(cachedTile);
                cachedApplied++;
                continue;
            }

            if (_jobs.TryGetValue(coord, out PendingTileJob? existing) &&
                existing.Lod == request.Lod &&
                existing.IncludeCollision == request.IncludeCollision &&
                existing.Profile.Equals(_profile) &&
                existing.TerrainFeatureKey == terrainFeatureKey)
            {
                continue;
            }

            if (_jobs.Count >= _profile.MaxQueuedTileJobs)
            {
                return;
            }

            var cancellation = new CancellationTokenSource();
            TerrainGenerationProfile jobProfile = _profile;
            _jobs[coord] = new PendingTileJob(
                coord,
                request.Lod,
                request.IncludeCollision,
                jobProfile,
                terrainFeatureKey,
                cancellation,
                Task.Run(
                    () => TerrainTileBuilder.Build(coord, request.Lod, jobProfile, request.IncludeCollision, routeCorridors, pointOfInterestIndex, cancellation.Token),
                    cancellation.Token));
        }
    }

    private void SubmitCompletedJobs()
    {
        int submitted = 0;
        var completed = _jobs.Values.Where(job => job.Task.IsCompleted).ToList();

        foreach (PendingTileJob job in completed)
        {
            if (submitted >= _profile.MaxCompletedTilesPerFrame)
            {
                break;
            }

            if (!_jobs.TryGetValue(job.Coord, out PendingTileJob? current) || current != job)
            {
                continue;
            }

            _jobs.Remove(job.Coord);

            if (job.Task.IsCanceled)
            {
                job.Cancellation.Dispose();
                continue;
            }

            if (job.Task.IsFaulted)
            {
                job.Cancellation.Dispose();
                GD.PushError($"Terrain tile {job.Coord} failed: {job.Task.Exception?.GetBaseException().Message}");
                continue;
            }

            TerrainTileData data = job.Task.Result;
            job.Cancellation.Dispose();

            if (job.Profile.Equals(_profile) && job.TerrainFeatureKey == TerrainFeatureKey)
            {
                StoreCachedTile(data, job.Profile, job.IncludeCollision, job.TerrainFeatureKey);
            }

            if (!_desiredCoords.Contains(job.Coord) ||
                !job.Profile.Equals(_profile) ||
                job.TerrainFeatureKey != TerrainFeatureKey)
            {
                continue;
            }

            ApplyTileData(data);
            submitted++;
        }
    }

    private void ApplyTileData(TerrainTileData data)
    {
        TerrainChunk chunk = GetOrCreateChunk(data.Coord);
        chunk.Apply(data, _terrainMaterial, _localWaterMaterial);
    }

    private TerrainChunk GetOrCreateChunk(TerrainTileCoord coord)
    {
        if (_chunks.TryGetValue(coord, out TerrainChunk? existing))
        {
            return existing;
        }

        var chunk = new TerrainChunk();
        _chunks.Add(coord, chunk);
        AddChild(chunk);
        return chunk;
    }

    private void UnloadUndesiredChunks()
    {
        foreach (TerrainTileCoord coord in _chunks.Keys.ToList())
        {
            if (_desiredCoords.Contains(coord))
            {
                continue;
            }

            TerrainChunk chunk = _chunks[coord];
            _chunks.Remove(coord);
            chunk.QueueFree();
        }
    }

    private void DropStaleJobs(TerrainTileCoord center)
    {
        foreach (PendingTileJob job in _jobs.Values.ToList())
        {
            if (!_desiredCoords.Contains(job.Coord))
            {
                _jobs.Remove(job.Coord);
                RetireJob(job);
                continue;
            }

            DesiredTileRequest request = GetDesiredRequest(job.Coord, center);
            if (request.Lod != job.Lod ||
                request.IncludeCollision != job.IncludeCollision ||
                !job.Profile.Equals(_profile) ||
                job.TerrainFeatureKey != TerrainFeatureKey)
            {
                _jobs.Remove(job.Coord);
                RetireJob(job);
            }
        }
    }

    private DesiredTileRequest GetDesiredRequest(TerrainTileCoord coord, TerrainTileCoord center)
    {
        int distance = coord.ChebyshevDistanceTo(center);
        bool includeCollision = _profile.GenerateCollision && distance <= _profile.CollisionRadiusChunks;
        int lod = includeCollision ? 0 : Mathf.Clamp((distance - 1) / 2, 0, _profile.MaxLod);
        return new DesiredTileRequest(lod, includeCollision);
    }

    private void CreateWater()
    {
        float waterSize = _profile.ChunkSize * (_profile.StreamRadiusChunks * 2 + 6);
        var planeMesh = new PlaneMesh
        {
            Size = new Vector2(waterSize, waterSize),
            SubdivideWidth = 8,
            SubdivideDepth = 8
        };

        _waterPlane = new MeshInstance3D
        {
            Name = "Water",
            Mesh = planeMesh,
            MaterialOverride = _waterMaterial
        };

        AddChild(_waterPlane);
        UpdateWaterPlane();
    }

    private void UpdateWaterPlane()
    {
        if (_waterPlane is null || _focus is null)
        {
            return;
        }

        Vector3 focusPosition = _focus.GlobalPosition;
        float grid = _profile.ChunkSize;
        _waterPlane.Position = new Vector3(
            Mathf.Round(focusPosition.X / grid) * grid,
            _profile.SeaLevel,
            Mathf.Round(focusPosition.Z / grid) * grid);
    }

    private TerrainTileData? GetCachedTile(TerrainTileCacheKey key)
    {
        if (!_tileCache.TryGetValue(key, out TerrainTileData? tileData))
        {
            return null;
        }

        TouchCacheKey(key);
        return tileData;
    }

    private void StoreCachedTile(
        TerrainTileData data,
        TerrainGenerationProfile profile,
        bool includeCollision,
        int terrainFeatureKey)
    {
        int limit = Mathf.Max(0, _profile.MaxCachedTileData);
        if (limit == 0)
        {
            return;
        }

        TerrainTileCacheKey key = new(data.Coord, data.Lod, includeCollision, profile, terrainFeatureKey);
        if (_tileCache.ContainsKey(key))
        {
            _tileCache[key] = data;
            TouchCacheKey(key);
            return;
        }

        _tileCache[key] = data;
        _tileCacheNodes[key] = _tileCacheLru.AddLast(key);
        TrimTileCache(limit);
    }

    private void TouchCacheKey(TerrainTileCacheKey key)
    {
        if (!_tileCacheNodes.TryGetValue(key, out LinkedListNode<TerrainTileCacheKey>? node))
        {
            return;
        }

        _tileCacheLru.Remove(node);
        _tileCacheLru.AddLast(node);
    }

    private void TrimTileCache(int limit)
    {
        while (_tileCache.Count > limit && _tileCacheLru.First is not null)
        {
            TerrainTileCacheKey oldest = _tileCacheLru.First.Value;
            _tileCacheLru.RemoveFirst();
            _tileCacheNodes.Remove(oldest);
            _tileCache.Remove(oldest);
        }
    }

    private void ClearTileCache()
    {
        _tileCache.Clear();
        _tileCacheNodes.Clear();
        _tileCacheLru.Clear();
    }

    private void ClearChunks()
    {
        foreach (TerrainChunk chunk in _chunks.Values)
        {
            chunk.QueueFree();
        }

        _chunks.Clear();
    }

    private void RebuildPlanIndices()
    {
        _routeCorridors = _worldPlan is null
            ? TerrainRouteCorridorIndex.Empty
            : TerrainRouteCorridorIndex.FromPlan(_worldPlan, _profile);
        _pointOfInterestIndex = _worldPlan is null
            ? TerrainPointOfInterestIndex.Empty
            : TerrainPointOfInterestIndex.FromPlan(_worldPlan, _profile);
    }

    private int TerrainFeatureKey => HashCode.Combine(_routeCorridors.CacheKey, _pointOfInterestIndex.CacheKey);

    private void CancelAllJobs()
    {
        foreach (PendingTileJob job in _jobs.Values)
        {
            RetireJob(job);
        }

        _jobs.Clear();
    }

    private void RetireJob(PendingTileJob job)
    {
        if (job.Task.IsCompleted)
        {
            job.Cancellation.Dispose();
            return;
        }

        job.Cancellation.Cancel();
        _retiredJobs.Add(job);
    }

    private void DisposeCompletedRetiredJobs()
    {
        for (int i = _retiredJobs.Count - 1; i >= 0; i--)
        {
            PendingTileJob job = _retiredJobs[i];
            if (!job.Task.IsCompleted)
            {
                continue;
            }

            if (job.Task.IsFaulted && job.Task.Exception?.GetBaseException() is not OperationCanceledException)
            {
                GD.PushError($"Retired terrain tile {job.Coord} failed: {job.Task.Exception?.GetBaseException().Message}");
            }

            job.Cancellation.Dispose();
            _retiredJobs.RemoveAt(i);
        }
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
