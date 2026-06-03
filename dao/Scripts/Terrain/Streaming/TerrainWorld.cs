using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dao.Terrain.Generation;
using Dao.Terrain.Rendering;
using Godot;

namespace Dao.Terrain.Streaming;

[GlobalClass]
public partial class TerrainWorld : Node3D
{
    [Export] public TerrainSettings? Settings { get; set; }
    [Export] public NodePath FocusPath { get; set; } = new();
    [Export(PropertyHint.Range, "0.05,2,0.01")] public double StreamingIntervalSeconds { get; set; } = 0.18;
    [Export] public bool CreateWaterPlane { get; set; } = true;

    private readonly Dictionary<TerrainTileCoord, TerrainChunk> _chunks = new();
    private readonly Dictionary<TerrainTileCoord, PendingTileJob> _jobs = new();
    private readonly Dictionary<TerrainTileCacheKey, TerrainTileData> _tileCache = new();
    private readonly Dictionary<TerrainTileCacheKey, LinkedListNode<TerrainTileCacheKey>> _tileCacheNodes = new();
    private readonly LinkedList<TerrainTileCacheKey> _tileCacheLru = new();
    private readonly List<PendingTileJob> _retiredJobs = new();
    private readonly HashSet<TerrainTileCoord> _desiredCoords = new();
    private TerrainGenerationProfile _profile;
    private TerrainWorldPlan? _worldPlan;
    private TerrainRouteCorridorIndex _routeCorridors = TerrainRouteCorridorIndex.Empty;
    private TerrainPointOfInterestIndex _pointOfInterestIndex = TerrainPointOfInterestIndex.Empty;
    private Node3D? _focus;
    private Material _terrainMaterial = null!;
    private Material _waterMaterial = null!;
    private MeshInstance3D? _waterPlane;
    private double _streamTimer;
    private bool _isReady;

    public override void _Ready()
    {
        Settings ??= new TerrainSettings();
        _profile = Settings.Snapshot();
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
        CancelAllJobs();
        DisposeCompletedRetiredJobs();
    }

    public void SetFocus(Node3D focus)
    {
        _focus = focus;
        UpdateStreaming(force: true);
    }

    public void SetWorldPlan(TerrainWorldPlan? worldPlan)
    {
        _worldPlan = worldPlan;
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

    public void Regenerate()
    {
        Settings ??= new TerrainSettings();
        _profile = Settings.Snapshot();
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
        chunk.Apply(data, _terrainMaterial);
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

    private sealed record PendingTileJob(
        TerrainTileCoord Coord,
        int Lod,
        bool IncludeCollision,
        TerrainGenerationProfile Profile,
        int TerrainFeatureKey,
        CancellationTokenSource Cancellation,
        Task<TerrainTileData> Task);

    private readonly record struct DesiredTileRequest(int Lod, bool IncludeCollision);
    private readonly record struct TerrainTileCacheKey(
        TerrainTileCoord Coord,
        int Lod,
        bool IncludeCollision,
        TerrainGenerationProfile Profile,
        int TerrainFeatureKey);
}
