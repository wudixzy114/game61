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

    /// <summary>Finds the nearest planned POI within a radius, optionally filtering by POI kind. Does not generate a plan.</summary>
    public bool TryFindNearestPointOfInterest(
        Vector2 world,
        float radius,
        TerrainPointOfInterestKind? kind,
        out TerrainWorldPointOfInterest point)
    {
        point = default;
        if (_worldPlan is null)
        {
            return false;
        }

        float safeRadius = Mathf.Max(0.0f, radius);
        float radiusSquared = safeRadius * safeRadius;
        float bestDistanceSquared = float.PositiveInfinity;
        bool found = false;

        foreach (TerrainWorldPointOfInterest candidate in _worldPlan.PointsOfInterest)
        {
            if (kind.HasValue && candidate.Kind != kind.Value)
            {
                continue;
            }

            float distanceSquared = candidate.WorldPosition.DistanceSquaredTo(world);
            if (distanceSquared <= radiusSquared && distanceSquared < bestDistanceSquared)
            {
                point = candidate;
                bestDistanceSquared = distanceSquared;
                found = true;
            }
        }

        return found;
    }

    /// <summary>Returns planned POIs inside world-space bounds, optionally filtering by POI kind. Does not generate a plan.</summary>
    public TerrainWorldPointOfInterest[] QueryPointsOfInterest(
        Rect2 worldBounds,
        TerrainPointOfInterestKind? kind = null)
    {
        if (_worldPlan is null)
        {
            return Array.Empty<TerrainWorldPointOfInterest>();
        }

        var points = new List<TerrainWorldPointOfInterest>();
        foreach (TerrainWorldPointOfInterest point in _worldPlan.PointsOfInterest)
        {
            if (kind.HasValue && point.Kind != kind.Value)
            {
                continue;
            }

            if (ContainsPoint(worldBounds, point.WorldPosition))
            {
                points.Add(point);
            }
        }

        return points.Count == 0 ? Array.Empty<TerrainWorldPointOfInterest>() : points.ToArray();
    }

    /// <summary>Returns planned routes whose waypoint polyline comes within the requested radius. Route waypoint arrays are copied.</summary>
    public TerrainWorldRoute[] QueryRoutesNear(Vector2 world, float radius)
    {
        if (_worldPlan is null)
        {
            return Array.Empty<TerrainWorldRoute>();
        }

        float safeRadius = Mathf.Max(0.0f, radius);
        float radiusSquared = safeRadius * safeRadius;
        var routes = new List<TerrainWorldRoute>();
        foreach (TerrainWorldRoute route in _worldPlan.Routes)
        {
            if (DistanceSquaredToRoute(world, route) <= radiusSquared)
            {
                routes.Add(CopyRoute(route));
            }
        }

        return routes.Count == 0 ? Array.Empty<TerrainWorldRoute>() : routes.ToArray();
    }

    /// <summary>Samples the current plan's route corridor influence at a world XZ position without generating tiles.</summary>
    public TerrainRouteCorridorSample SampleRouteCorridor(Vector2 world)
    {
        TerrainRouteCorridorIndex corridors = _routeCorridors ?? TerrainRouteCorridorIndex.Empty;
        if (_worldPlan is null || !corridors.HasSegments)
        {
            return TerrainRouteCorridorSample.None;
        }

        TerrainGenerationProfile profile = CurrentProfile;
        return corridors.Sample(world, CoordFromWorld(world, profile.ChunkSize));
    }

    /// <summary>Samples static terrain water semantics at a world XZ position without touching streaming tiles.</summary>
    public TerrainWaterState SampleWaterState(Vector2 world)
    {
        TerrainGenerationProfile profile = CurrentProfile;
        TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);
        return TerrainSemanticClassifier.ClassifyWater(field, profile);
    }

    /// <summary>Samples gameplay-facing terrain tags at a world XZ position without touching streaming tiles.</summary>
    public TerrainGameplayTags SampleGameplayTags(Vector2 world)
    {
        TerrainGenerationProfile profile = CurrentProfile;
        TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);
        return TerrainSemanticClassifier.ClassifyGameplayTags(field, profile);
    }

    /// <summary>Samples local traversal cost semantics for navigation, AI, encounters, and placement filters without pathfinding.</summary>
    public TerrainTraversalCost SampleTraversalCost(Vector2 world, float spacing = 4.0f)
    {
        TerrainGenerationProfile profile = CurrentProfile;
        TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);
        TerrainSample surface = TerrainSampler.SampleWithSlope(world, profile, spacing);
        return TerrainSemanticClassifier.ClassifyTraversalCost(field, surface, profile);
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

    private static TerrainWorldRoute CopyRoute(TerrainWorldRoute route)
    {
        Vector2[] waypoints = route.Waypoints.Length == 0
            ? Array.Empty<Vector2>()
            : (Vector2[])route.Waypoints.Clone();
        return route with { Waypoints = waypoints };
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

    private static bool ContainsPoint(Rect2 bounds, Vector2 point)
    {
        float x0 = bounds.Position.X;
        float y0 = bounds.Position.Y;
        float x1 = bounds.Position.X + bounds.Size.X;
        float y1 = bounds.Position.Y + bounds.Size.Y;
        float minX = Mathf.Min(x0, x1);
        float maxX = Mathf.Max(x0, x1);
        float minY = Mathf.Min(y0, y1);
        float maxY = Mathf.Max(y0, y1);
        return point.X >= minX &&
            point.X <= maxX &&
            point.Y >= minY &&
            point.Y <= maxY;
    }

    private static TerrainTileCoord CoordFromWorld(Vector2 world, float chunkSize)
    {
        return new TerrainTileCoord(
            Mathf.FloorToInt(world.X / chunkSize),
            Mathf.FloorToInt(world.Y / chunkSize));
    }

    private static float DistanceSquaredToRoute(Vector2 world, TerrainWorldRoute route)
    {
        Vector2[] waypoints = route.Waypoints;
        if (waypoints.Length == 0)
        {
            return float.PositiveInfinity;
        }

        if (waypoints.Length == 1)
        {
            return world.DistanceSquaredTo(waypoints[0]);
        }

        float best = float.PositiveInfinity;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            best = Mathf.Min(best, DistanceSquaredToSegment(world, waypoints[i], waypoints[i + 1]));
        }

        return best;
    }

    private static float DistanceSquaredToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 segment = b - a;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return point.DistanceSquaredTo(a);
        }

        float t = Mathf.Clamp((point - a).Dot(segment) / lengthSquared, 0.0f, 1.0f);
        Vector2 closest = a + segment * t;
        return point.DistanceSquaredTo(closest);
    }
}
