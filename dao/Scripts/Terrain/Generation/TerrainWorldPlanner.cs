using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

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
        TerrainPointOfInterestRuleSetSnapshot poiRules = ResolvePointOfInterestRules(profile);
        TerrainRouteRuleSetSnapshot routeRules = ResolveRouteRules(profile);
        TerrainPlanningGridData planningGrid = SamplePlanningGrid(profile, center, safeWorldSize, resolution, poiRules, cancellationToken);
        TerrainWorldPointOfInterest[] points = SelectPointsOfInterest(
            planningGrid.Candidates,
            profile,
            poiRules,
            safeMaxPoints,
            planningGrid.CellSize,
            safeWorldSize,
            cancellationToken);
        TerrainWorldRoute[] routes = BuildRoutes(
            points,
            planningGrid.Fields,
            profile,
            routeRules,
            resolution,
            safeMaxRoutes,
            cancellationToken);
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

    private static TerrainPointOfInterestRuleSetSnapshot ResolvePointOfInterestRules(TerrainGenerationProfile profile)
    {
        return TerrainPointOfInterestRuleCatalog.Resolve(profile.PointOfInterestRuleSetHash);
    }

    private static TerrainRouteRuleSetSnapshot ResolveRouteRules(TerrainGenerationProfile profile)
    {
        return TerrainRouteRuleCatalog.Resolve(profile.RouteRuleSetHash);
    }
}
