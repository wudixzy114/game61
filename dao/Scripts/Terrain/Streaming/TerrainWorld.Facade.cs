using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Streaming;

public partial class TerrainWorld
{
    /// <summary>Samples the complete terrain semantic field at a world XZ position using this world's current profile.</summary>
    public TerrainWorldField SampleField(Vector2 world)
    {
        return SampleFieldWithModification(world);
    }

    /// <summary>Samples height, slope, biome, landscape, traversability, and surface color at a world XZ position.</summary>
    public TerrainSample SampleSurface(Vector2 world, float spacing = 4.0f)
    {
        return SampleSurfaceWithModification(world, spacing);
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
        if (_worldPlan is null)
        {
            plan = null;
            return false;
        }

        plan = TerrainWorldPlan.CopyOf(_worldPlan);
        return true;
    }

    /// <summary>Returns a snapshot copy of the current open-world plan, or an empty snapshot when no plan is ready.</summary>
    public TerrainWorldPlanSnapshot GetWorldPlanSnapshot()
    {
        return _worldPlan is null
            ? TerrainWorldPlanSnapshot.Empty
            : TerrainWorldPlanSnapshot.FromPlan(_worldPlan);
    }

    /// <summary>Returns a snapshot copy of the current open-world plan without exposing internal mutable arrays.</summary>
    public bool TryGetWorldPlanSnapshot([NotNullWhen(true)] out TerrainWorldPlanSnapshot? snapshot)
    {
        if (_worldPlan is null)
        {
            snapshot = null;
            return false;
        }

        snapshot = TerrainWorldPlanSnapshot.FromPlan(_worldPlan);
        return true;
    }

    /// <summary>Returns an isolated diagnostics snapshot of the current streaming queues, chunks, cache, and plan state.</summary>
    public TerrainWorldStreamingSnapshot GetStreamingSnapshot()
    {
        TerrainGenerationProfile profile = CurrentProfile;
        bool hasFocus = _focus is not null && IsInstanceValid(_focus);
        Vector3 focusPosition = hasFocus ? _focus!.GlobalPosition : Vector3.Zero;
        TerrainTileCoord focusCoord = hasFocus
            ? TerrainTileCoord.FromWorldPosition(focusPosition, profile.ChunkSize)
            : default;

        TerrainTileCoord[] desiredChunks = _desiredCoords is null
            ? Array.Empty<TerrainTileCoord>()
            : CopySortedCoords(_desiredCoords);
        TerrainTileCoord[] loadedChunks = _chunks is null
            ? Array.Empty<TerrainTileCoord>()
            : CopySortedCoords(_chunks.Keys);
        TerrainTileCoord[] queuedJobs = _jobs is null
            ? Array.Empty<TerrainTileCoord>()
            : CopySortedCoords(_jobs.Keys);

        return new TerrainWorldStreamingSnapshot(
            profile,
            hasFocus,
            focusPosition,
            focusCoord,
            profile.StreamRadiusChunks,
            desiredChunks.Length,
            desiredChunks,
            loadedChunks.Length,
            loadedChunks,
            queuedJobs.Length,
            queuedJobs,
            _retiredJobs?.Count ?? 0,
            _tileCache?.Count ?? 0,
            Mathf.Max(0, profile.MaxCachedTileData),
            profile.MaxQueuedTileJobs,
            profile.MaxCompletedTilesPerFrame,
            _worldPlan is not null,
            _worldPlanJob is not null,
            StreamTerrainBeforeOpenWorldPlanReady);
    }

    /// <summary>Samples static terrain water semantics at a world XZ position without touching streaming tiles.</summary>
    public TerrainWaterState SampleWaterState(Vector2 world)
    {
        TerrainGenerationProfile profile = CurrentProfile;
        TerrainWorldField field = SampleFieldWithModification(world);
        return TerrainSemanticClassifier.ClassifyWater(field, profile);
    }

    /// <summary>Samples gameplay-facing terrain tags at a world XZ position without touching streaming tiles.</summary>
    public TerrainGameplayTags SampleGameplayTags(Vector2 world)
    {
        TerrainGenerationProfile profile = CurrentProfile;
        TerrainWorldField field = SampleFieldWithModification(world);
        return TerrainSemanticClassifier.ClassifyGameplayTags(field, profile);
    }

    /// <summary>Samples local traversal cost semantics for navigation, AI, encounters, and placement filters without pathfinding.</summary>
    public TerrainTraversalCost SampleTraversalCost(Vector2 world, float spacing = 4.0f)
    {
        return SampleTraversalCostWithModification(world, spacing);
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
        return SampleFieldWithModification(world).Height >= profile.SeaLevel + margin;
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

    private static TerrainTileCoord[] CopySortedCoords(IEnumerable<TerrainTileCoord> coords)
    {
        TerrainTileCoord[] copy = coords.ToArray();
        Array.Sort(copy, CompareTileCoords);
        return copy;
    }

    private static int CompareTileCoords(TerrainTileCoord a, TerrainTileCoord b)
    {
        int x = a.X.CompareTo(b.X);
        return x != 0 ? x : a.Z.CompareTo(b.Z);
    }
}
