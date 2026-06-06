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
        return TerrainWorldPlanQueryService.GetPointsOfInterest(this);
    }

    /// <summary>Returns a snapshot copy of the current plan's routes and waypoint arrays, or an empty array when no plan is ready.</summary>
    public TerrainWorldRoute[] GetRoutes()
    {
        return TerrainWorldPlanQueryService.GetRoutes(this);
    }

    /// <summary>Returns the nearest planned POIs as compact summaries without copying the full world plan payload.</summary>
    public TerrainWorldPointOfInterestSummary[] QueryNearestPointsOfInterest(
        Vector2 world,
        float radius,
        int maxResults,
        TerrainPointOfInterestKind? kind = null)
    {
        return TerrainWorldPlanQueryService.QueryNearestPointsOfInterest(this, world, radius, maxResults, kind);
    }

    /// <summary>Finds the nearest planned POI within a radius, optionally filtering by POI kind. Does not generate a plan.</summary>
    public bool TryFindNearestPointOfInterest(
        Vector2 world,
        float radius,
        TerrainPointOfInterestKind? kind,
        out TerrainWorldPointOfInterest point)
    {
        return TerrainWorldPlanQueryService.TryFindNearestPointOfInterest(this, world, radius, kind, out point);
    }

    /// <summary>Returns planned POIs inside world-space bounds, optionally filtering by POI kind. Does not generate a plan.</summary>
    public TerrainWorldPointOfInterest[] QueryPointsOfInterest(
        Rect2 worldBounds,
        TerrainPointOfInterestKind? kind = null)
    {
        return TerrainWorldPlanQueryService.QueryPointsOfInterest(this, worldBounds, kind);
    }

    /// <summary>Returns bounded gameplay-tag region summaries from the current world plan without exposing full plan-region arrays.</summary>
    public TerrainGameplayTagRegionSummary[] QueryGameplayTagRegions(
        Rect2 worldBounds,
        TerrainGameplayTag requiredTags,
        TerrainGameplayTag excludedTags = TerrainGameplayTag.None,
        int maxResults = 32)
    {
        return TerrainWorldPlanQueryService.QueryGameplayTagRegions(this, worldBounds, requiredTags, excludedTags, maxResults);
    }

    /// <summary>Returns planned routes whose waypoint polyline comes within the requested radius. Route waypoint arrays are copied.</summary>
    public TerrainWorldRoute[] QueryRoutesNear(Vector2 world, float radius)
    {
        return TerrainWorldPlanQueryService.QueryRoutesNear(this, world, radius);
    }

    /// <summary>Returns nearby routes as compact summaries without copying waypoint arrays.</summary>
    public TerrainWorldRouteSummary[] QueryRouteSummariesNear(Vector2 world, float radius, int maxResults)
    {
        return TerrainWorldPlanQueryService.QueryRouteSummariesNear(this, world, radius, maxResults);
    }

    /// <summary>Samples the current plan's route corridor influence at a world XZ position without generating tiles.</summary>
    public TerrainRouteCorridorSample SampleRouteCorridor(Vector2 world)
    {
        return TerrainWorldPlanQueryService.SampleRouteCorridor(this, world);
    }
}
