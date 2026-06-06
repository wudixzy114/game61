using System;
using System.Collections.Generic;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Streaming;

public partial class TerrainWorld
{
    /// <summary>Returns a snapshot copy of the current plan's points of interest, or an empty array when no plan is ready.</summary>
    public TerrainWorldPointOfInterest[] GetPointsOfInterest()
    {
        if (_worldPlan is null)
        {
            return Array.Empty<TerrainWorldPointOfInterest>();
        }

        TerrainWorldPointOfInterest[] points = _worldPlan.PointsOfInterest;
        return points.Length == 0
            ? Array.Empty<TerrainWorldPointOfInterest>()
            : (TerrainWorldPointOfInterest[])points.Clone();
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
            copy[i] = CopyRoute(routes[i]);
        }

        return copy;
    }

    /// <summary>Returns the nearest planned POIs as compact summaries without copying the full world plan payload.</summary>
    public TerrainWorldPointOfInterestSummary[] QueryNearestPointsOfInterest(
        Vector2 world,
        float radius,
        int maxResults,
        TerrainPointOfInterestKind? kind = null)
    {
        if (_worldPlan is null || maxResults <= 0)
        {
            return Array.Empty<TerrainWorldPointOfInterestSummary>();
        }

        float safeRadius = Mathf.Max(0.0f, radius);
        float radiusSquared = safeRadius * safeRadius;
        var summaries = new List<TerrainWorldPointOfInterestSummary>();
        foreach (TerrainWorldPointOfInterest point in _worldPlan.PointsOfInterest)
        {
            if (kind.HasValue && point.Kind != kind.Value)
            {
                continue;
            }

            float distanceSquared = point.WorldPosition.DistanceSquaredTo(world);
            if (distanceSquared > radiusSquared)
            {
                continue;
            }

            summaries.Add(CreatePointSummary(point, distanceSquared));
        }

        if (summaries.Count == 0)
        {
            return Array.Empty<TerrainWorldPointOfInterestSummary>();
        }

        summaries.Sort(ComparePointSummaries);
        if (summaries.Count <= maxResults)
        {
            return summaries.ToArray();
        }

        var limited = new TerrainWorldPointOfInterestSummary[maxResults];
        summaries.CopyTo(0, limited, 0, maxResults);
        return limited;
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

    /// <summary>Returns bounded gameplay-tag region summaries from the current world plan without exposing full plan-region arrays.</summary>
    public TerrainGameplayTagRegionSummary[] QueryGameplayTagRegions(
        Rect2 worldBounds,
        TerrainGameplayTag requiredTags,
        TerrainGameplayTag excludedTags = TerrainGameplayTag.None,
        int maxResults = 32)
    {
        if (_worldPlan is null)
        {
            return Array.Empty<TerrainGameplayTagRegionSummary>();
        }

        return TerrainGameplayTagRegionQuery.QueryRegions(
            _worldPlan,
            CurrentProfile,
            worldBounds,
            requiredTags,
            excludedTags,
            maxResults);
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

    /// <summary>Returns nearby routes as compact summaries without copying waypoint arrays.</summary>
    public TerrainWorldRouteSummary[] QueryRouteSummariesNear(Vector2 world, float radius, int maxResults)
    {
        if (_worldPlan is null || maxResults <= 0)
        {
            return Array.Empty<TerrainWorldRouteSummary>();
        }

        float safeRadius = Mathf.Max(0.0f, radius);
        float radiusSquared = safeRadius * safeRadius;
        var summaries = new List<TerrainWorldRouteSummary>();
        foreach (TerrainWorldRoute route in _worldPlan.Routes)
        {
            float distanceSquared = DistanceSquaredToRoute(world, route);
            if (distanceSquared > radiusSquared)
            {
                continue;
            }

            summaries.Add(CreateRouteSummary(route, distanceSquared));
        }

        if (summaries.Count == 0)
        {
            return Array.Empty<TerrainWorldRouteSummary>();
        }

        summaries.Sort(CompareRouteSummaries);
        if (summaries.Count <= maxResults)
        {
            return summaries.ToArray();
        }

        var limited = new TerrainWorldRouteSummary[maxResults];
        summaries.CopyTo(0, limited, 0, maxResults);
        return limited;
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

    private static TerrainWorldPointOfInterestSummary CreatePointSummary(
        TerrainWorldPointOfInterest point,
        float distanceSquared)
    {
        return new TerrainWorldPointOfInterestSummary(
            point.Id,
            point.Kind,
            point.WorldPosition,
            Mathf.Sqrt(distanceSquared),
            point.Score,
            point.ScenicPotential,
            point.Traversability,
            point.SettlementTier,
            point.BiomeKind,
            point.LandscapeKind);
    }

    private static TerrainWorldRouteSummary CreateRouteSummary(TerrainWorldRoute route, float distanceSquared)
    {
        return new TerrainWorldRouteSummary(
            route.FromPointId,
            route.ToPointId,
            route.Kind,
            Mathf.Sqrt(distanceSquared),
            route.Cost,
            route.AverageScenicPotential,
            route.AverageTraversability,
            route.Waypoints.Length);
    }

    private static int ComparePointSummaries(
        TerrainWorldPointOfInterestSummary a,
        TerrainWorldPointOfInterestSummary b)
    {
        int distance = a.Distance.CompareTo(b.Distance);
        if (distance != 0)
        {
            return distance;
        }

        int score = b.Score.CompareTo(a.Score);
        return score != 0 ? score : a.Id.CompareTo(b.Id);
    }

    private static int CompareRouteSummaries(TerrainWorldRouteSummary a, TerrainWorldRouteSummary b)
    {
        int distance = a.Distance.CompareTo(b.Distance);
        if (distance != 0)
        {
            return distance;
        }

        int scenic = b.AverageScenicPotential.CompareTo(a.AverageScenicPotential);
        if (scenic != 0)
        {
            return scenic;
        }

        int from = a.FromPointId.CompareTo(b.FromPointId);
        return from != 0 ? from : a.ToPointId.CompareTo(b.ToPointId);
    }

    private static TerrainWorldRoute CopyRoute(TerrainWorldRoute route)
    {
        Vector2[] waypoints = route.Waypoints.Length == 0
            ? Array.Empty<Vector2>()
            : (Vector2[])route.Waypoints.Clone();
        return route with { Waypoints = waypoints };
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
