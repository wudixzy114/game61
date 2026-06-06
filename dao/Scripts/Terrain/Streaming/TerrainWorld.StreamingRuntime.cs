using System;
using System.Collections.Generic;
using System.Linq;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Streaming;

public partial class TerrainWorld
{
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
        _ = force;
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
        if (TerrainStreamingSetBuilder.RebuildDesiredSet(_desiredCoords, center, _profile.StreamRadiusChunks))
        {
            MarkStreamingSnapshotDirty();
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
            TerrainTileRequest request = TerrainStreamingSetBuilder.GetDesiredRequest(coord, center, _profile);
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

            var cancellation = new System.Threading.CancellationTokenSource();
            TerrainGenerationProfile jobProfile = _profile;
            _jobs[coord] = new PendingTileJob(
                coord,
                request.Lod,
                request.IncludeCollision,
                jobProfile,
                terrainFeatureKey,
                cancellation,
                System.Threading.Tasks.Task.Run(
                    () => TerrainTileBuilder.Build(coord, request.Lod, jobProfile, request.IncludeCollision, routeCorridors, pointOfInterestIndex, cancellation.Token),
                    cancellation.Token));
            MarkStreamingSnapshotDirty();
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
            MarkStreamingSnapshotDirty();

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
        MarkStreamingSnapshotDirty();
        EmitChunkLoadedSignalIfReady(data);
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
        MarkStreamingSnapshotDirty();
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
            MarkStreamingSnapshotDirty();
            EmitChunkUnloadedSignalIfReady(chunk);
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
                MarkStreamingSnapshotDirty();
                RetireJob(job);
                continue;
            }

            TerrainTileRequest request = TerrainStreamingSetBuilder.GetDesiredRequest(job.Coord, center, _profile);
            if (request.Lod != job.Lod ||
                request.IncludeCollision != job.IncludeCollision ||
                !job.Profile.Equals(_profile) ||
                job.TerrainFeatureKey != TerrainFeatureKey)
            {
                _jobs.Remove(job.Coord);
                MarkStreamingSnapshotDirty();
                RetireJob(job);
            }
        }
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
        return _tileCache.TryGet(key);
    }

    private void StoreCachedTile(
        TerrainTileData data,
        TerrainGenerationProfile profile,
        bool includeCollision,
        int terrainFeatureKey)
    {
        TerrainTileCacheKey key = new(data.Coord, data.Lod, includeCollision, profile, terrainFeatureKey);
        if (_tileCache.Store(data, key, _profile.MaxCachedTileData))
        {
            MarkStreamingSnapshotDirty();
        }
    }

    private void ClearTileCache()
    {
        if (_tileCache.Clear())
        {
            MarkStreamingSnapshotDirty();
        }
    }

    private void ClearChunks()
    {
        if (_chunks.Count == 0)
        {
            return;
        }

        foreach (TerrainChunk? chunk in _chunks.Values)
        {
            if (chunk is null)
            {
                continue;
            }

            EmitChunkUnloadedSignalIfReady(chunk);
            chunk.QueueFree();
        }

        _chunks.Clear();
        MarkStreamingSnapshotDirty();
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
        if (_jobs.Count == 0)
        {
            return;
        }

        foreach (PendingTileJob job in _jobs.Values)
        {
            RetireJob(job);
        }

        _jobs.Clear();
        MarkStreamingSnapshotDirty();
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
        MarkStreamingSnapshotDirty();
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
            MarkStreamingSnapshotDirty();
        }
    }
}
