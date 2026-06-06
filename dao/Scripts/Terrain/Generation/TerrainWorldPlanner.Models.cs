using System;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>High-level region classification used for planning and reporting.</summary>
public enum TerrainWorldRegionKind
{
    Ocean = 0,
    Coast = 1,
    Island = 2,
    Plains = 3,
    Grassland = 4,
    Desert = 5,
    Oasis = 6,
    Lowland = 7,
    Forest = 8,
    Wetland = 9,
    Hills = 10,
    RiverValley = 11,
    Canyon = 12,
    Highlands = 13,
    Mountains = 14,
    Snow = 15,
    ScenicPlateau = 16,
    Lake = 17
}

/// <summary>Gameplay-relevant point of interest types that the planner can place.</summary>
public enum TerrainPointOfInterestKind
{
    SettlementCandidate = 0,
    Vista = 1,
    RiverCrossing = 2,
    MountainPass = 3,
    CoastalLanding = 4,
    ResourceGrove = 5,
    AncientSite = 6,
    CanyonOverlook = 7,
    Oasis = 8
}

/// <summary>Settlement development tiers, from none up to full town or oasis hub.</summary>
public enum TerrainSettlementTier
{
    None = 0,
    Village = 1,
    Town = 2,
    OasisHub = 3
}

/// <summary>Route style classifications that determine corridor width, surface color, and gameplay feel.</summary>
public enum TerrainRouteKind
{
    PrimaryTrail = 0,
    RiverRoad = 1,
    RidgePass = 2,
    CoastalPath = 3,
    ScenicTrail = 4
}

/// <summary>A single cell in the planning grid, summarizing terrain attributes and region kind.</summary>
public readonly record struct TerrainWorldRegion(
    int GridX,
    int GridY,
    Vector2 WorldPosition,
    float Height,
    float River,
    float ScenicPotential,
    float Traversability,
    float Exposure,
    float ResourcePotential,
    float HazardPotential,
    float EncounterPotential,
    TerrainBiomeKind BiomeKind,
    TerrainLandscapeKind LandscapeKind,
    TerrainWorldRegionKind RegionKind);

/// <summary>A planned point of interest with its kind, world position, score, and optional settlement tier.</summary>
public readonly record struct TerrainWorldPointOfInterest(
    int Id,
    TerrainPointOfInterestKind Kind,
    Vector2 WorldPosition,
    int GridX,
    int GridY,
    float Score,
    float Height,
    float ScenicPotential,
    float Traversability,
    TerrainBiomeKind BiomeKind,
    TerrainLandscapeKind LandscapeKind,
    TerrainSettlementTier SettlementTier,
    string DebugName);

/// <summary>A planned route connecting two points of interest, with waypoints and averaged attributes.</summary>
public readonly record struct TerrainWorldRoute(
    int FromPointId,
    int ToPointId,
    TerrainRouteKind Kind,
    float Cost,
    float AverageScenicPotential,
    float AverageTraversability,
    Vector2[] Waypoints);

/// <summary>Complete open-world terrain plan containing regions, points of interest, routes, and quality reports.</summary>
public sealed class TerrainWorldPlan
{
    public TerrainWorldPlan(
        Vector2 center,
        float worldSize,
        int gridResolution,
        TerrainWorldRegion[] regions,
        TerrainWorldPointOfInterest[] pointsOfInterest,
        TerrainWorldRoute[] routes,
        TerrainQualityReport qualityReport,
        TerrainWorldPlanningReport planningReport,
        TerrainExperienceReport experienceReport)
    {
        Center = center;
        WorldSize = worldSize;
        GridResolution = gridResolution;
        Regions = TerrainWorldPlanCopy.CopyArray(regions);
        PointsOfInterest = TerrainWorldPlanCopy.CopyArray(pointsOfInterest);
        Routes = TerrainWorldPlanCopy.CopyRoutes(routes);
        QualityReport = qualityReport;
        PlanningReport = planningReport;
        ExperienceReport = experienceReport;
    }

    public Vector2 Center { get; }
    public float WorldSize { get; }
    public int GridResolution { get; }
    public TerrainWorldRegion[] Regions { get; }
    public TerrainWorldPointOfInterest[] PointsOfInterest { get; }
    public TerrainWorldRoute[] Routes { get; }
    public TerrainQualityReport QualityReport { get; }
    public TerrainWorldPlanningReport PlanningReport { get; }
    public TerrainExperienceReport ExperienceReport { get; }

    public static TerrainWorldPlan CopyOf(TerrainWorldPlan plan)
    {
        return new TerrainWorldPlan(
            plan.Center,
            plan.WorldSize,
            plan.GridResolution,
            plan.Regions,
            plan.PointsOfInterest,
            plan.Routes,
            plan.QualityReport,
            plan.PlanningReport,
            plan.ExperienceReport);
    }
}

/// <summary>Snapshot copy of an open-world terrain plan intended for stable runtime API consumers.</summary>
public sealed class TerrainWorldPlanSnapshot
{
    public static TerrainWorldPlanSnapshot Empty { get; } = new(
        Vector2.Zero,
        worldSize: 0.0f,
        gridResolution: 0,
        [],
        [],
        [],
        default,
        default,
        default);

    public TerrainWorldPlanSnapshot(
        Vector2 center,
        float worldSize,
        int gridResolution,
        TerrainWorldRegion[] regions,
        TerrainWorldPointOfInterest[] pointsOfInterest,
        TerrainWorldRoute[] routes,
        TerrainQualityReport qualityReport,
        TerrainWorldPlanningReport planningReport,
        TerrainExperienceReport experienceReport)
    {
        Center = center;
        WorldSize = worldSize;
        GridResolution = gridResolution;
        Regions = TerrainWorldPlanCopy.CopyArray(regions);
        PointsOfInterest = TerrainWorldPlanCopy.CopyArray(pointsOfInterest);
        Routes = TerrainWorldPlanCopy.CopyRoutes(routes);
        QualityReport = qualityReport;
        PlanningReport = planningReport;
        ExperienceReport = experienceReport;
    }

    public Vector2 Center { get; }
    public float WorldSize { get; }
    public int GridResolution { get; }
    public TerrainWorldRegion[] Regions { get; }
    public TerrainWorldPointOfInterest[] PointsOfInterest { get; }
    public TerrainWorldRoute[] Routes { get; }
    public TerrainQualityReport QualityReport { get; }
    public TerrainWorldPlanningReport PlanningReport { get; }
    public TerrainExperienceReport ExperienceReport { get; }

    public static TerrainWorldPlanSnapshot FromPlan(TerrainWorldPlan plan)
    {
        return new TerrainWorldPlanSnapshot(
            plan.Center,
            plan.WorldSize,
            plan.GridResolution,
            plan.Regions,
            plan.PointsOfInterest,
            plan.Routes,
            plan.QualityReport,
            plan.PlanningReport,
            plan.ExperienceReport);
    }
}

internal static class TerrainWorldPlanCopy
{
    public static T[] CopyArray<T>(T[] values)
    {
        return values.Length == 0
            ? Array.Empty<T>()
            : (T[])values.Clone();
    }

    public static TerrainWorldRoute[] CopyRoutes(TerrainWorldRoute[] routes)
    {
        if (routes.Length == 0)
        {
            return Array.Empty<TerrainWorldRoute>();
        }

        var copy = new TerrainWorldRoute[routes.Length];
        for (int i = 0; i < routes.Length; i++)
        {
            Vector2[] waypoints = routes[i].Waypoints.Length == 0
                ? Array.Empty<Vector2>()
                : (Vector2[])routes[i].Waypoints.Clone();
            copy[i] = routes[i] with { Waypoints = waypoints };
        }

        return copy;
    }
}
