using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
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
        Regions = CopyArray(regions);
        PointsOfInterest = CopyArray(pointsOfInterest);
        Routes = CopyRoutes(routes);
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

    private static T[] CopyArray<T>(T[] values)
    {
        return values.Length == 0
            ? Array.Empty<T>()
            : (T[])values.Clone();
    }

    private static TerrainWorldRoute[] CopyRoutes(TerrainWorldRoute[] routes)
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
        Regions = CopyArray(regions);
        PointsOfInterest = CopyArray(pointsOfInterest);
        Routes = CopyRoutes(routes);
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

    private static T[] CopyArray<T>(T[] values)
    {
        return values.Length == 0
            ? Array.Empty<T>()
            : (T[])values.Clone();
    }

    private static TerrainWorldRoute[] CopyRoutes(TerrainWorldRoute[] routes)
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

/// <summary>Threshold bounds used to validate world planning quality.</summary>
public readonly record struct TerrainWorldPlanningThresholds(
    int MinPointsOfInterest,
    int MinPointOfInterestKinds,
    int MinRoutes,
    int MinRouteKinds,
    float MinConnectedPointRatio,
    float MinConnectedSettlementRatio,
    int MinSettlementRoutes,
    float MinPointOfInterestWorldCoverage,
    float MinRouteWorldCoverage,
    float MinAverageRouteTraversability,
    float MinAverageRouteScenicPotential,
    int MinVillages,
    int MinTowns,
    int MinOasisHubs)
{
    public static TerrainWorldPlanningThresholds OpenWorldDefault { get; } = new(
        MinPointsOfInterest: 18,
        MinPointOfInterestKinds: 5,
        MinRoutes: 48,
        MinRouteKinds: 3,
        MinConnectedPointRatio: 0.95f,
        MinConnectedSettlementRatio: 0.95f,
        MinSettlementRoutes: 8,
        MinPointOfInterestWorldCoverage: 0.70f,
        MinRouteWorldCoverage: 0.70f,
        MinAverageRouteTraversability: 0.34f,
        MinAverageRouteScenicPotential: 0.20f,
        MinVillages: 2,
        MinTowns: 2,
        MinOasisHubs: 1);
}

/// <summary>Detailed statistics from analyzing a world plan's points of interest and routes.</summary>
public readonly record struct TerrainWorldPlanningReport(
    int PointOfInterestCount,
    int DistinctPointOfInterestKinds,
    int RouteCount,
    int DistinctRouteKinds,
    float ConnectedPointRatio,
    float ConnectedSettlementRatio,
    int SettlementRouteCount,
    float PointOfInterestWorldCoverage,
    float RouteWorldCoverage,
    float AveragePointScore,
    float AverageRouteCost,
    float AverageRouteScenicPotential,
    float AverageRouteTraversability,
    int SettlementCandidateCount,
    int VistaCount,
    int RiverCrossingCount,
    int MountainPassCount,
    int CoastalLandingCount,
    int ResourceGroveCount,
    int AncientSiteCount,
    int CanyonOverlookCount,
    int OasisCount,
    int VillageCount,
    int TownCount,
    int OasisHubCount,
    int PrimaryTrailCount,
    int RiverRoadCount,
    int RidgePassCount,
    int CoastalPathCount,
    int ScenicTrailCount);

/// <summary>Result of validating a world plan against planning thresholds.</summary>
public readonly record struct TerrainWorldPlanningGateResult(
    bool Passed,
    TerrainWorldPlanningReport Report,
    string Summary);

/// <summary>Creates open-world terrain plans: samples fields, selects POIs, builds routes, and produces validation reports.</summary>
public static partial class TerrainWorldPlanner
{
    private const float ImpassableCost = 1000000.0f;

    /// <summary>Creates a full terrain plan by sampling fields, selecting POIs, building routes, and generating quality/experience reports.</summary>
    public static TerrainWorldPlan CreatePlan(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int gridResolution,
        int maxPointsOfInterest = 36,
        int maxRoutes = 18,
        CancellationToken cancellationToken = default)
    {
        int resolution = Mathf.Clamp(gridResolution, 8, 256);
        int safeMaxPoints = Mathf.Clamp(maxPointsOfInterest, 4, 512);
        int safeMaxRoutes = Mathf.Clamp(maxRoutes, 0, 512);
        float safeWorldSize = Mathf.Max(profile.ChunkSize, worldSize);
        TerrainPlanningGridData planningGrid = SamplePlanningGrid(profile, center, safeWorldSize, resolution, cancellationToken);
        TerrainWorldPointOfInterest[] points = SelectPointsOfInterest(planningGrid.Candidates, profile, safeMaxPoints, planningGrid.CellSize, safeWorldSize, cancellationToken);
        TerrainWorldRoute[] routes = BuildRoutes(points, planningGrid.Fields, profile, resolution, safeMaxRoutes, cancellationToken);
        TerrainQualityReport qualityReport = TerrainQualityAnalyzer.Analyze(profile, center, safeWorldSize, resolution, cancellationToken);
        TerrainWorldPlanningReport planningReport = AnalyzePlanning(points, routes, safeWorldSize);
        TerrainExperienceReport experienceReport = TerrainExperienceAnalyzer.Analyze(planningGrid.Regions, points, routes, planningReport, cancellationToken);

        return new TerrainWorldPlan(
            center,
            safeWorldSize,
            resolution,
            planningGrid.Regions,
            points,
            routes,
            qualityReport,
            planningReport,
            experienceReport);
    }

    /// <summary>Convenience method that creates an open-world plan with sensible defaults for planning resolution, POI count, and route count.</summary>
    public static TerrainWorldPlan CreateOpenWorldPlan(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        CancellationToken cancellationToken = default)
    {
        int planningResolution = Mathf.Clamp(profile.StreamRadiusChunks * 10, 48, 128);
        return CreatePlan(
            profile,
            center,
            worldSize,
            planningResolution,
            maxPointsOfInterest: 48,
            maxRoutes: 64,
            cancellationToken: cancellationToken);
    }

    /// <summary>Analyzes a plan's POI and route statistics into a <see cref="TerrainWorldPlanningReport"/>.</summary>
    public static TerrainWorldPlanningReport AnalyzePlanning(TerrainWorldPlan plan)
    {
        return AnalyzePlanning(plan.PointsOfInterest, plan.Routes, plan.WorldSize);
    }

    /// <summary>Validates a plan against the given planning thresholds and produces a gate result with summary.</summary>
    public static TerrainWorldPlanningGateResult ValidatePlanning(
        TerrainWorldPlan plan,
        TerrainWorldPlanningThresholds thresholds)
    {
        TerrainWorldPlanningReport report = AnalyzePlanning(plan);
        var summary = new StringBuilder();
        bool passed = true;

        AppendGate(
            summary,
            "points of interest",
            report.PointOfInterestCount >= thresholds.MinPointsOfInterest,
            report.PointOfInterestCount.ToString(),
            $">= {thresholds.MinPointsOfInterest}",
            ref passed);
        AppendGate(
            summary,
            "point kind variety",
            report.DistinctPointOfInterestKinds >= thresholds.MinPointOfInterestKinds,
            report.DistinctPointOfInterestKinds.ToString(),
            $">= {thresholds.MinPointOfInterestKinds}",
            ref passed);
        AppendGate(
            summary,
            "route count",
            report.RouteCount >= thresholds.MinRoutes,
            report.RouteCount.ToString(),
            $">= {thresholds.MinRoutes}",
            ref passed);
        AppendGate(
            summary,
            "route kind variety",
            report.DistinctRouteKinds >= thresholds.MinRouteKinds,
            report.DistinctRouteKinds.ToString(),
            $">= {thresholds.MinRouteKinds}",
            ref passed);
        AppendGate(
            summary,
            "connected point ratio",
            report.ConnectedPointRatio >= thresholds.MinConnectedPointRatio,
            $"{report.ConnectedPointRatio:0.000}",
            $">= {thresholds.MinConnectedPointRatio:0.000}",
            ref passed);
        AppendGate(
            summary,
            "connected settlement ratio",
            report.ConnectedSettlementRatio >= thresholds.MinConnectedSettlementRatio,
            $"{report.ConnectedSettlementRatio:0.000}",
            $">= {thresholds.MinConnectedSettlementRatio:0.000}",
            ref passed);
        AppendGate(
            summary,
            "settlement route count",
            report.SettlementRouteCount >= thresholds.MinSettlementRoutes,
            report.SettlementRouteCount.ToString(),
            $">= {thresholds.MinSettlementRoutes}",
            ref passed);
        AppendGate(
            summary,
            "point world coverage",
            report.PointOfInterestWorldCoverage >= thresholds.MinPointOfInterestWorldCoverage,
            $"{report.PointOfInterestWorldCoverage:0.000}",
            $">= {thresholds.MinPointOfInterestWorldCoverage:0.000}",
            ref passed);
        AppendGate(
            summary,
            "route world coverage",
            report.RouteWorldCoverage >= thresholds.MinRouteWorldCoverage,
            $"{report.RouteWorldCoverage:0.000}",
            $">= {thresholds.MinRouteWorldCoverage:0.000}",
            ref passed);
        AppendGate(
            summary,
            "route traversability",
            report.AverageRouteTraversability >= thresholds.MinAverageRouteTraversability,
            $"{report.AverageRouteTraversability:0.000}",
            $">= {thresholds.MinAverageRouteTraversability:0.000}",
            ref passed);
        AppendGate(
            summary,
            "route scenic value",
            report.AverageRouteScenicPotential >= thresholds.MinAverageRouteScenicPotential,
            $"{report.AverageRouteScenicPotential:0.000}",
            $">= {thresholds.MinAverageRouteScenicPotential:0.000}",
            ref passed);
        AppendGate(
            summary,
            "villages",
            report.VillageCount >= thresholds.MinVillages,
            report.VillageCount.ToString(),
            $">= {thresholds.MinVillages}",
            ref passed);
        AppendGate(
            summary,
            "towns",
            report.TownCount >= thresholds.MinTowns,
            report.TownCount.ToString(),
            $">= {thresholds.MinTowns}",
            ref passed);
        AppendGate(
            summary,
            "oasis hubs",
            report.OasisHubCount >= thresholds.MinOasisHubs,
            report.OasisHubCount.ToString(),
            $">= {thresholds.MinOasisHubs}",
            ref passed);

        return new TerrainWorldPlanningGateResult(passed, report, summary.ToString());
    }

    /// <summary>Validates a plan against the default open-world thresholds.</summary>
    public static TerrainWorldPlanningGateResult ValidateOpenWorldPlanning(TerrainWorldPlan plan)
    {
        return ValidatePlanning(plan, TerrainWorldPlanningThresholds.OpenWorldDefault);
    }

    /// <summary>Creates an open-world plan and validates it against default thresholds.</summary>
    public static TerrainWorldPlanningGateResult ValidateOpenWorldPlanning(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        CancellationToken cancellationToken = default)
    {
        TerrainWorldPlan plan = CreateOpenWorldPlan(profile, center, worldSize, cancellationToken);
        return ValidateOpenWorldPlanning(plan);
    }

    private static void AppendGate(
        StringBuilder summary,
        string name,
        bool passed,
        string actual,
        string expected,
        ref bool allPassed)
    {
        if (!passed)
        {
            allPassed = false;
        }

        summary
            .Append(passed ? "PASS" : "FAIL")
            .Append(": ")
            .Append(name)
            .Append(" actual ")
            .Append(actual)
            .Append(" expected ")
            .Append(expected)
            .AppendLine();
    }

    private readonly record struct SecondaryRouteCandidate(
        int FromIndex,
        int ToIndex,
        float Score);

    private readonly record struct PoiCandidate(
        TerrainPointOfInterestKind Kind,
        Vector2 WorldPosition,
        int GridX,
        int GridY,
        float Score,
        float Height,
        float ScenicPotential,
        float Traversability,
        float ResourcePotential,
        float River,
        TerrainBiomeKind BiomeKind,
        TerrainLandscapeKind LandscapeKind);
}
