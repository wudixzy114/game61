using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>High-level region classification used for planning and reporting.</summary>
public enum TerrainWorldRegionKind
{
    Ocean,
    Coast,
    Island,
    Plains,
    Grassland,
    Desert,
    Oasis,
    Lowland,
    Forest,
    Wetland,
    Hills,
    RiverValley,
    Canyon,
    Highlands,
    Mountains,
    Snow,
    ScenicPlateau,
    Lake
}

/// <summary>Gameplay-relevant point of interest types that the planner can place.</summary>
public enum TerrainPointOfInterestKind
{
    SettlementCandidate,
    Vista,
    RiverCrossing,
    MountainPass,
    CoastalLanding,
    ResourceGrove,
    AncientSite,
    CanyonOverlook,
    Oasis
}

/// <summary>Settlement development tiers, from none up to full town or oasis hub.</summary>
public enum TerrainSettlementTier
{
    None,
    Village,
    Town,
    OasisHub
}

/// <summary>Route style classifications that determine corridor width, surface color, and gameplay feel.</summary>
public enum TerrainRouteKind
{
    PrimaryTrail,
    RiverRoad,
    RidgePass,
    CoastalPath,
    ScenicTrail
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
        Regions = regions;
        PointsOfInterest = pointsOfInterest;
        Routes = routes;
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
public static class TerrainWorldPlanner
{
    private const float ImpassableCost = 1000000.0f;

    /// <summary>Creates a full terrain plan by sampling fields, selecting POIs, building routes, and generating quality/experience reports.</summary>
    public static TerrainWorldPlan CreatePlan(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int gridResolution,
        int maxPointsOfInterest = 36,
        int maxRoutes = 18)
    {
        int resolution = Mathf.Clamp(gridResolution, 8, 256);
        int safeMaxPoints = Mathf.Clamp(maxPointsOfInterest, 4, 512);
        int safeMaxRoutes = Mathf.Clamp(maxRoutes, 0, 512);
        float safeWorldSize = Mathf.Max(profile.ChunkSize, worldSize);
        float cellSize = safeWorldSize / resolution;
        int cellCount = resolution * resolution;
        var fields = new TerrainWorldField[cellCount];
        var regions = new TerrainWorldRegion[cellCount];
        var candidates = new List<PoiCandidate>(cellCount);

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                Vector2 world = CellCenter(center, safeWorldSize, resolution, x, y);
                TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);
                int index = Index(x, y, resolution);
                fields[index] = field;
                regions[index] = new TerrainWorldRegion(
                    x,
                    y,
                    world,
                    field.Height,
                    field.River,
                    field.ScenicPotential,
                    field.Traversability,
                    field.Exposure,
                    field.ResourcePotential,
                    field.HazardPotential,
                    field.EncounterPotential,
                    field.BiomeKind,
                    field.LandscapeKind,
                    ClassifyRegion(field));
                AddPoiCandidates(candidates, profile, field, x, y);
            }
        }

        TerrainWorldPointOfInterest[] points = SelectPointsOfInterest(candidates, profile, safeMaxPoints, cellSize, safeWorldSize);
        TerrainWorldRoute[] routes = BuildRoutes(points, fields, profile, resolution, safeMaxRoutes);
        TerrainQualityReport qualityReport = TerrainQualityAnalyzer.Analyze(profile, center, safeWorldSize, resolution);
        TerrainWorldPlanningReport planningReport = AnalyzePlanning(points, routes, safeWorldSize);
        TerrainExperienceReport experienceReport = TerrainExperienceAnalyzer.Analyze(regions, points, routes, planningReport);

        return new TerrainWorldPlan(
            center,
            safeWorldSize,
            resolution,
            regions,
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
        float worldSize)
    {
        int planningResolution = Mathf.Clamp(profile.StreamRadiusChunks * 10, 48, 128);
        return CreatePlan(
            profile,
            center,
            worldSize,
            planningResolution,
            maxPointsOfInterest: 48,
            maxRoutes: 64);
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
        float worldSize)
    {
        TerrainWorldPlan plan = CreateOpenWorldPlan(profile, center, worldSize);
        return ValidateOpenWorldPlanning(plan);
    }

    private static void AddPoiCandidates(
        List<PoiCandidate> candidates,
        TerrainGenerationProfile profile,
        TerrainWorldField field,
        int gridX,
        int gridY)
    {
        if (field.Height < profile.SeaLevel - 4.0f)
        {
            return;
        }

        float land = Mathf.SmoothStep(profile.SeaLevel + 2.0f, profile.SeaLevel + 48.0f, field.Height);
        float elevation = Mathf.SmoothStep(profile.SeaLevel + 80.0f, profile.SeaLevel + profile.HeightScale * 0.72f, field.Height);
        float rarity = Hash01(gridX, gridY, profile.Seed + 911);
        float stableFlatLand = field.Traversability * land;

        float settlementBiomeBonus = field.BiomeKind is TerrainBiomeKind.Plains or TerrainBiomeKind.Grassland
            ? 0.10f
            : field.BiomeKind == TerrainBiomeKind.Oasis ? 0.16f : 0.0f;
        float settlementScore =
            stableFlatLand * 0.55f +
            Mathf.Clamp(1.0f - Mathf.Abs(field.Moisture - 0.55f) * 2.0f, 0.0f, 1.0f) * 0.12f +
            Mathf.Clamp(1.0f - Mathf.Abs(field.Temperature - 0.56f) * 2.1f, 0.0f, 1.0f) * 0.12f +
            Mathf.SmoothStep(0.18f, 0.62f, field.River) * 0.09f +
            field.ScenicPotential * 0.12f +
            settlementBiomeBonus;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.SettlementCandidate, settlementScore, 0.58f, field, gridX, gridY, rarity);

        float vistaScore = field.ScenicPotential * 0.82f + elevation * 0.14f + rarity * 0.04f;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.Vista, vistaScore, 0.64f, field, gridX, gridY, rarity);

        float crossingScore =
            Mathf.SmoothStep(0.50f, 0.82f, field.River) * 0.55f +
            field.Traversability * 0.30f +
            land * 0.15f;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.RiverCrossing, crossingScore, 0.62f, field, gridX, gridY, rarity);

        float passScore = 0.0f;
        if (field.LandscapeKind is TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.VistaPlateau)
        {
            passScore = elevation * 0.30f + field.Traversability * 0.36f + field.ScenicPotential * 0.28f + rarity * 0.06f;
        }

        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.MountainPass, passScore, 0.54f, field, gridX, gridY, rarity);

        float coastScore = 0.0f;
        if (field.LandscapeKind == TerrainLandscapeKind.Coast || Mathf.Abs(field.Height - profile.SeaLevel) < 30.0f)
        {
            coastScore = land * 0.30f + field.Traversability * 0.30f + field.ScenicPotential * 0.28f + rarity * 0.12f;
        }

        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.CoastalLanding, coastScore, 0.50f, field, gridX, gridY, rarity);

        float resourceScore = 0.0f;
        if (field.LandscapeKind is TerrainLandscapeKind.ForestBasin or TerrainLandscapeKind.Wetland or TerrainLandscapeKind.RiverValley ||
            field.BiomeKind == TerrainBiomeKind.Oasis)
        {
            resourceScore = field.Moisture * 0.34f + field.Traversability * 0.24f + (1.0f - elevation) * 0.16f + field.River * 0.12f + rarity * 0.14f;
        }

        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.ResourceGrove, resourceScore, 0.58f, field, gridX, gridY, rarity);

        float ancientScore = field.ScenicPotential * 0.50f + elevation * 0.18f + stableFlatLand * 0.16f + rarity * 0.16f;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.AncientSite, ancientScore, 0.70f, field, gridX, gridY, rarity);

        float canyonScore = field.LandscapeKind == TerrainLandscapeKind.Canyon
            ? field.ScenicPotential * 0.50f + field.River * 0.26f + elevation * 0.12f + rarity * 0.12f
            : 0.0f;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.CanyonOverlook, canyonScore, 0.58f, field, gridX, gridY, rarity);

        bool naturalOasis = field.BiomeKind == TerrainBiomeKind.Oasis;
        float warmDryWaterAccess =
            Mathf.SmoothStep(0.46f, 0.76f, field.Temperature) *
            (1.0f - Mathf.SmoothStep(0.50f, 0.76f, field.Moisture)) *
            Mathf.Max(
                Mathf.SmoothStep(0.22f, 0.62f, field.River),
                Mathf.SmoothStep(0.44f, 0.72f, field.ResourcePotential));
        bool strategicOasisSite =
            !naturalOasis &&
            field.Height > profile.SeaLevel + 8.0f &&
            field.Traversability > 0.28f &&
            field.BiomeKind is TerrainBiomeKind.Desert or TerrainBiomeKind.Plains or TerrainBiomeKind.Grassland or TerrainBiomeKind.Hills &&
            warmDryWaterAccess > 0.18f;
        float oasisScore = naturalOasis
            ? field.ResourcePotential * 0.38f + field.Traversability * 0.20f + field.River * 0.18f + field.ScenicPotential * 0.14f + rarity * 0.10f
            : strategicOasisSite
            ? warmDryWaterAccess * 0.30f + field.ResourcePotential * 0.26f + field.Traversability * 0.18f + field.ScenicPotential * 0.12f + rarity * 0.14f
            : 0.0f;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.Oasis, oasisScore, 0.54f, field, gridX, gridY, rarity);
    }

    private static void AddCandidateIfStrong(
        List<PoiCandidate> candidates,
        TerrainPointOfInterestKind kind,
        float score,
        float threshold,
        TerrainWorldField field,
        int gridX,
        int gridY,
        float rarity)
    {
        if (score < threshold)
        {
            return;
        }

        candidates.Add(new PoiCandidate(
            kind,
            field.WorldPosition,
            gridX,
            gridY,
            Mathf.Clamp(score + rarity * 0.025f, 0.0f, 1.0f),
            field.Height,
            field.ScenicPotential,
            field.Traversability,
            field.ResourcePotential,
            field.River,
            field.BiomeKind,
            field.LandscapeKind));
    }

    private static TerrainWorldPointOfInterest[] SelectPointsOfInterest(
        List<PoiCandidate> candidates,
        TerrainGenerationProfile profile,
        int maxPoints,
        float cellSize,
        float worldSize)
    {
        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var selected = new List<TerrainWorldPointOfInterest>(maxPoints);
        var kindCounts = new Dictionary<TerrainPointOfInterestKind, int>();
        int perKindLimit = Mathf.Max(3, Mathf.CeilToInt(maxPoints * 0.28f));
        float minDistanceSquared = Mathf.Pow(Mathf.Max(cellSize * 2.2f, profile.ChunkSize * 0.70f), 2.0f);

        SelectRequiredPointKind(
            candidates,
            selected,
            kindCounts,
            TerrainPointOfInterestKind.Oasis,
            maxPoints,
            perKindLimit,
            minDistanceSquared * 0.36f);

        foreach (TerrainPointOfInterestKind kind in Enum.GetValues<TerrainPointOfInterestKind>())
        {
            foreach (PoiCandidate candidate in candidates)
            {
                if (candidate.Kind != kind)
                {
                    continue;
                }

                if (TrySelectPoint(
                    selected,
                    kindCounts,
                    candidate,
                    maxPoints,
                    perKindLimit,
                    minDistanceSquared * 0.48f,
                    enforcePerKindLimit: false))
                {
                    break;
                }
            }
        }

        SelectCoverageAnchors(
            candidates,
            selected,
            kindCounts,
            maxPoints,
            perKindLimit,
            minDistanceSquared,
            worldSize);

        foreach (PoiCandidate candidate in candidates)
        {
            TrySelectPoint(
                selected,
                kindCounts,
                candidate,
                maxPoints,
                perKindLimit,
                minDistanceSquared,
                enforcePerKindLimit: true);
        }

        return selected.ToArray();
    }

    private static void SelectRequiredPointKind(
        List<PoiCandidate> candidates,
        List<TerrainWorldPointOfInterest> selected,
        Dictionary<TerrainPointOfInterestKind, int> kindCounts,
        TerrainPointOfInterestKind requiredKind,
        int maxPoints,
        int perKindLimit,
        float minDistanceSquared)
    {
        kindCounts.TryGetValue(requiredKind, out int existingCount);
        if (existingCount > 0)
        {
            return;
        }

        foreach (PoiCandidate candidate in candidates)
        {
            if (candidate.Kind != requiredKind)
            {
                continue;
            }

            if (TrySelectPoint(
                selected,
                kindCounts,
                candidate,
                maxPoints,
                perKindLimit,
                minDistanceSquared,
                enforcePerKindLimit: false))
            {
                return;
            }
        }
    }

    private static void SelectCoverageAnchors(
        List<PoiCandidate> candidates,
        List<TerrainWorldPointOfInterest> selected,
        Dictionary<TerrainPointOfInterestKind, int> kindCounts,
        int maxPoints,
        int perKindLimit,
        float minDistanceSquared,
        float worldSize)
    {
        int targetCount = Mathf.Clamp(Mathf.CeilToInt(maxPoints * 0.44f), selected.Count, maxPoints);
        while (selected.Count < targetCount)
        {
            int bestIndex = -1;
            float bestScore = float.NegativeInfinity;
            float currentCoverage = ComputePointCoverage(selected, worldSize);

            for (int i = 0; i < candidates.Count; i++)
            {
                PoiCandidate candidate = candidates[i];
                if (!CanSelectPoint(
                    selected,
                    kindCounts,
                    candidate,
                    maxPoints,
                    perKindLimit,
                    minDistanceSquared,
                    enforcePerKindLimit: true))
                {
                    continue;
                }

                float score = ScoreCoverageAnchor(candidate, selected, worldSize, currentCoverage);
                if (score <= bestScore)
                {
                    continue;
                }

                bestIndex = i;
                bestScore = score;
            }

            if (bestIndex < 0)
            {
                return;
            }

            TrySelectPoint(
                selected,
                kindCounts,
                candidates[bestIndex],
                maxPoints,
                perKindLimit,
                minDistanceSquared,
                enforcePerKindLimit: true);
        }
    }

    private static float ScoreCoverageAnchor(
        PoiCandidate candidate,
        List<TerrainWorldPointOfInterest> selected,
        float worldSize,
        float currentCoverage)
    {
        if (selected.Count == 0)
        {
            return candidate.Score;
        }

        float coverageGain = ComputeCoverageWithCandidate(selected, candidate.WorldPosition, worldSize) - currentCoverage;
        float distanceNovelty = ComputeNearestPointDistanceRatio(candidate.WorldPosition, selected, worldSize);
        float biomeBonus = candidate.BiomeKind is TerrainBiomeKind.Island or TerrainBiomeKind.Desert or TerrainBiomeKind.Oasis
            ? 0.08f
            : 0.0f;

        return coverageGain * 12.0f + distanceNovelty * 0.42f + candidate.Score * 0.30f + biomeBonus;
    }

    private static bool CanSelectPoint(
        List<TerrainWorldPointOfInterest> selected,
        Dictionary<TerrainPointOfInterestKind, int> kindCounts,
        PoiCandidate candidate,
        int maxPoints,
        int perKindLimit,
        float minDistanceSquared,
        bool enforcePerKindLimit)
    {
        if (selected.Count >= maxPoints)
        {
            return false;
        }

        kindCounts.TryGetValue(candidate.Kind, out int kindCount);
        if (enforcePerKindLimit && kindCount >= perKindLimit)
        {
            return false;
        }

        foreach (TerrainWorldPointOfInterest existing in selected)
        {
            if (existing.WorldPosition.DistanceSquaredTo(candidate.WorldPosition) < minDistanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySelectPoint(
        List<TerrainWorldPointOfInterest> selected,
        Dictionary<TerrainPointOfInterestKind, int> kindCounts,
        PoiCandidate candidate,
        int maxPoints,
        int perKindLimit,
        float minDistanceSquared,
        bool enforcePerKindLimit)
    {
        if (!CanSelectPoint(
            selected,
            kindCounts,
            candidate,
            maxPoints,
            perKindLimit,
            minDistanceSquared,
            enforcePerKindLimit))
        {
            return false;
        }

        kindCounts.TryGetValue(candidate.Kind, out int kindCount);
        int id = selected.Count;
        selected.Add(new TerrainWorldPointOfInterest(
            id,
            candidate.Kind,
            candidate.WorldPosition,
            candidate.GridX,
            candidate.GridY,
            candidate.Score,
            candidate.Height,
            candidate.ScenicPotential,
            candidate.Traversability,
            candidate.BiomeKind,
            candidate.LandscapeKind,
            ClassifySettlementTier(candidate),
            $"{candidate.Kind}_{candidate.GridX}_{candidate.GridY}_{id}"));
        kindCounts[candidate.Kind] = kindCount + 1;
        return true;
    }

    private static TerrainSettlementTier ClassifySettlementTier(PoiCandidate candidate)
    {
        if (candidate.Kind == TerrainPointOfInterestKind.Oasis)
        {
            return TerrainSettlementTier.OasisHub;
        }

        if (candidate.Kind != TerrainPointOfInterestKind.SettlementCandidate)
        {
            return TerrainSettlementTier.None;
        }

        float biomeScore = candidate.BiomeKind switch
        {
            TerrainBiomeKind.Plains => 1.0f,
            TerrainBiomeKind.Grassland => 0.92f,
            TerrainBiomeKind.Oasis => 0.88f,
            TerrainBiomeKind.Forest => 0.68f,
            TerrainBiomeKind.Coast => 0.58f,
            _ => 0.36f
        };
        float townScore =
            candidate.Score * 0.40f +
            candidate.Traversability * 0.22f +
            candidate.ResourcePotential * 0.20f +
            candidate.ScenicPotential * 0.08f +
            biomeScore * 0.10f;

        return townScore >= 0.84f
            ? TerrainSettlementTier.Town
            : TerrainSettlementTier.Village;
    }

    private static TerrainWorldRoute[] BuildRoutes(
        TerrainWorldPointOfInterest[] points,
        TerrainWorldField[] fields,
        TerrainGenerationProfile profile,
        int resolution,
        int maxRoutes)
    {
        if (points.Length < 2 || maxRoutes == 0)
        {
            return [];
        }

        var routes = new List<TerrainWorldRoute>(maxRoutes);
        var connected = new HashSet<int> { points[0].Id };
        var remaining = new HashSet<int>();
        for (int i = 1; i < points.Length; i++)
        {
            remaining.Add(points[i].Id);
        }

        while (remaining.Count > 0 && routes.Count < maxRoutes)
        {
            TerrainWorldRoute? bestRoute = null;
            int bestTo = -1;
            float bestScore = float.PositiveInfinity;

            foreach (int from in connected)
            {
                foreach (int to in remaining)
                {
                    float distance = points[from].WorldPosition.DistanceTo(points[to].WorldPosition);
                    float score = distance * (1.0f - Mathf.Min(points[from].Score, points[to].Score) * 0.25f);
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    TerrainWorldRoute? route = TryBuildRoute(points[from], points[to], fields, profile, resolution);
                    if (route is null)
                    {
                        continue;
                    }

                    bestRoute = route.Value;
                    bestTo = to;
                    bestScore = score;
                }
            }

            if (bestRoute is null || bestTo < 0)
            {
                break;
            }

            routes.Add(bestRoute.Value);
            connected.Add(bestTo);
            remaining.Remove(bestTo);
        }

        AddSecondaryRoutes(points, fields, profile, resolution, maxRoutes, routes);
        return routes.ToArray();
    }

    private static void AddSecondaryRoutes(
        TerrainWorldPointOfInterest[] points,
        TerrainWorldField[] fields,
        TerrainGenerationProfile profile,
        int resolution,
        int maxRoutes,
        List<TerrainWorldRoute> routes)
    {
        if (routes.Count >= maxRoutes || points.Length < 3)
        {
            return;
        }

        var existingEdges = new HashSet<long>();
        var routeDegree = new int[points.Length];
        foreach (TerrainWorldRoute route in routes)
        {
            existingEdges.Add(PointPairKey(route.FromPointId, route.ToPointId));
            if ((uint)route.FromPointId < (uint)routeDegree.Length)
            {
                routeDegree[route.FromPointId]++;
            }

            if ((uint)route.ToPointId < (uint)routeDegree.Length)
            {
                routeDegree[route.ToPointId]++;
            }
        }

        AddSettlementConnectorRoutes(points, fields, profile, resolution, maxRoutes, routes, existingEdges, routeDegree);
        if (routes.Count >= maxRoutes)
        {
            return;
        }

        var candidates = new List<SecondaryRouteCandidate>(points.Length * 4);
        float minDistance = profile.ChunkSize * 2.0f;
        float idealDistance = profile.ChunkSize * 18.0f;
        float maxDistance = profile.ChunkSize * 42.0f;

        for (int from = 0; from < points.Length - 1; from++)
        {
            for (int to = from + 1; to < points.Length; to++)
            {
                long key = PointPairKey(points[from].Id, points[to].Id);
                if (existingEdges.Contains(key))
                {
                    continue;
                }

                float distance = points[from].WorldPosition.DistanceTo(points[to].WorldPosition);
                if (distance < minDistance || distance > maxDistance)
                {
                    continue;
                }

                candidates.Add(new SecondaryRouteCandidate(
                    from,
                    to,
                    ScoreSecondaryRouteCandidate(points[from], points[to], routeDegree, distance, idealDistance)));
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

        int testedCandidates = 0;
        int maxCandidateTests = Mathf.Max(64, (maxRoutes - routes.Count) * 10);
        foreach (SecondaryRouteCandidate candidate in candidates)
        {
            if (routes.Count >= maxRoutes || testedCandidates >= maxCandidateTests)
            {
                break;
            }

            TerrainWorldPointOfInterest from = points[candidate.FromIndex];
            TerrainWorldPointOfInterest to = points[candidate.ToIndex];
            long key = PointPairKey(from.Id, to.Id);
            if (existingEdges.Contains(key))
            {
                continue;
            }

            testedCandidates++;
            TerrainWorldRoute? route = TryBuildRoute(from, to, fields, profile, resolution);
            if (route is null)
            {
                continue;
            }

            routes.Add(route.Value);
            existingEdges.Add(key);
            routeDegree[from.Id]++;
            routeDegree[to.Id]++;
        }
    }

    private static void AddSettlementConnectorRoutes(
        TerrainWorldPointOfInterest[] points,
        TerrainWorldField[] fields,
        TerrainGenerationProfile profile,
        int resolution,
        int maxRoutes,
        List<TerrainWorldRoute> routes,
        HashSet<long> existingEdges,
        int[] routeDegree)
    {
        int settlementCount = CountSettlementHubs(points);
        if (routes.Count >= maxRoutes || settlementCount < 2)
        {
            return;
        }

        int settlementRouteCount = CountSettlementRoutes(points, routes);
        int targetSettlementRoutes = Mathf.Min(maxRoutes, Mathf.Max(8, settlementCount - 1));
        if (settlementRouteCount >= targetSettlementRoutes)
        {
            return;
        }

        var candidates = new List<SecondaryRouteCandidate>(settlementCount * settlementCount);
        float minDistance = profile.ChunkSize * 1.5f;
        float idealDistance = profile.ChunkSize * 14.0f;
        float maxDistance = profile.ChunkSize * 38.0f;

        for (int from = 0; from < points.Length - 1; from++)
        {
            if (!IsSettlementHub(points[from]))
            {
                continue;
            }

            for (int to = from + 1; to < points.Length; to++)
            {
                if (!IsSettlementHub(points[to]))
                {
                    continue;
                }

                long key = PointPairKey(points[from].Id, points[to].Id);
                if (existingEdges.Contains(key))
                {
                    continue;
                }

                float distance = points[from].WorldPosition.DistanceTo(points[to].WorldPosition);
                if (distance < minDistance || distance > maxDistance)
                {
                    continue;
                }

                candidates.Add(new SecondaryRouteCandidate(
                    from,
                    to,
                    ScoreSettlementRouteCandidate(points[from], points[to], routeDegree, distance, idealDistance)));
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

        int testedCandidates = 0;
        int maxCandidateTests = Mathf.Max(32, (targetSettlementRoutes - settlementRouteCount) * 8);
        foreach (SecondaryRouteCandidate candidate in candidates)
        {
            if (routes.Count >= maxRoutes ||
                settlementRouteCount >= targetSettlementRoutes ||
                testedCandidates >= maxCandidateTests)
            {
                return;
            }

            TerrainWorldPointOfInterest from = points[candidate.FromIndex];
            TerrainWorldPointOfInterest to = points[candidate.ToIndex];
            long key = PointPairKey(from.Id, to.Id);
            if (existingEdges.Contains(key))
            {
                continue;
            }

            testedCandidates++;
            TerrainWorldRoute? route = TryBuildRoute(from, to, fields, profile, resolution);
            if (route is null)
            {
                continue;
            }

            routes.Add(route.Value);
            existingEdges.Add(key);
            routeDegree[from.Id]++;
            routeDegree[to.Id]++;
            settlementRouteCount++;
        }
    }

    private static float ScoreSettlementRouteCandidate(
        TerrainWorldPointOfInterest from,
        TerrainWorldPointOfInterest to,
        int[] routeDegree,
        float distance,
        float idealDistance)
    {
        float endpointScore = (from.Score + to.Score) * 0.5f;
        float traversalScore = (from.Traversability + to.Traversability) * 0.5f;
        int fromDegree = (uint)from.Id < (uint)routeDegree.Length ? routeDegree[from.Id] : 0;
        int toDegree = (uint)to.Id < (uint)routeDegree.Length ? routeDegree[to.Id] : 0;
        float underConnectedScore = 1.0f / (1.0f + Mathf.Min(fromDegree, toDegree));
        float tierImportance = (SettlementTierRouteWeight(from.SettlementTier) + SettlementTierRouteWeight(to.SettlementTier)) * 0.5f;
        float tierVariety = from.SettlementTier == to.SettlementTier ? 0.45f : 1.0f;
        float distanceScore = 1.0f - Mathf.Clamp(Mathf.Abs(distance - idealDistance) / Mathf.Max(1.0f, idealDistance), 0.0f, 1.0f);

        return
            endpointScore * 0.22f +
            traversalScore * 0.18f +
            underConnectedScore * 0.24f +
            tierImportance * 0.20f +
            tierVariety * 0.08f +
            distanceScore * 0.08f;
    }

    private static float ScoreSecondaryRouteCandidate(
        TerrainWorldPointOfInterest from,
        TerrainWorldPointOfInterest to,
        int[] routeDegree,
        float distance,
        float idealDistance)
    {
        float endpointScore = (from.Score + to.Score) * 0.5f;
        float scenicScore = (from.ScenicPotential + to.ScenicPotential) * 0.5f;
        float traversalScore = (from.Traversability + to.Traversability) * 0.5f;
        int fromDegree = (uint)from.Id < (uint)routeDegree.Length ? routeDegree[from.Id] : 0;
        int toDegree = (uint)to.Id < (uint)routeDegree.Length ? routeDegree[to.Id] : 0;
        float underConnectedScore = 1.0f / (1.0f + Mathf.Min(fromDegree, toDegree));
        float kindVariety = from.Kind == to.Kind ? 0.0f : 1.0f;
        float settlementBonus = IsSettlementHub(from) || IsSettlementHub(to) ? 0.08f : 0.0f;
        float distanceScore = 1.0f - Mathf.Clamp(Mathf.Abs(distance - idealDistance) / Mathf.Max(1.0f, idealDistance), 0.0f, 1.0f);

        return
            endpointScore * 0.28f +
            scenicScore * 0.26f +
            traversalScore * 0.16f +
            underConnectedScore * 0.18f +
            kindVariety * 0.06f +
            distanceScore * 0.06f +
            settlementBonus;
    }

    private static TerrainWorldPlanningReport AnalyzePlanning(
        TerrainWorldPointOfInterest[] points,
        TerrainWorldRoute[] routes,
        float worldSize)
    {
        Span<int> poiCounts = stackalloc int[Enum.GetValues<TerrainPointOfInterestKind>().Length];
        Span<int> routeCounts = stackalloc int[Enum.GetValues<TerrainRouteKind>().Length];
        Span<int> settlementTierCounts = stackalloc int[Enum.GetValues<TerrainSettlementTier>().Length];
        float scoreSum = 0.0f;

        foreach (TerrainWorldPointOfInterest point in points)
        {
            poiCounts[Mathf.Clamp((int)point.Kind, 0, poiCounts.Length - 1)]++;
            settlementTierCounts[Mathf.Clamp((int)point.SettlementTier, 0, settlementTierCounts.Length - 1)]++;
            scoreSum += point.Score;
        }

        float routeCostSum = 0.0f;
        float routeScenicSum = 0.0f;
        float routeTraversabilitySum = 0.0f;
        var connected = new HashSet<int>();
        var connectedSettlements = new HashSet<int>();
        int settlementPointCount = 0;
        int settlementRouteCount = 0;

        foreach (TerrainWorldPointOfInterest point in points)
        {
            if (IsSettlementHub(point))
            {
                settlementPointCount++;
            }
        }

        foreach (TerrainWorldRoute route in routes)
        {
            routeCounts[Mathf.Clamp((int)route.Kind, 0, routeCounts.Length - 1)]++;
            routeCostSum += route.Cost;
            routeScenicSum += route.AverageScenicPotential;
            routeTraversabilitySum += route.AverageTraversability;
            connected.Add(route.FromPointId);
            connected.Add(route.ToPointId);

            bool fromSettlement = IsSettlementHub(points, route.FromPointId);
            bool toSettlement = IsSettlementHub(points, route.ToPointId);
            if (fromSettlement)
            {
                connectedSettlements.Add(route.FromPointId);
            }

            if (toSettlement)
            {
                connectedSettlements.Add(route.ToPointId);
            }

            if (fromSettlement && toSettlement)
            {
                settlementRouteCount++;
            }
        }

        int distinctPoiKinds = CountNonZero(poiCounts);
        int distinctRouteKinds = CountNonZero(routeCounts);
        float invPoiCount = points.Length == 0 ? 0.0f : 1.0f / points.Length;
        float invRouteCount = routes.Length == 0 ? 0.0f : 1.0f / routes.Length;

        return new TerrainWorldPlanningReport(
            points.Length,
            distinctPoiKinds,
            routes.Length,
            distinctRouteKinds,
            points.Length == 0 ? 0.0f : connected.Count / (float)points.Length,
            settlementPointCount == 0 ? 0.0f : connectedSettlements.Count / (float)settlementPointCount,
            settlementRouteCount,
            ComputePointCoverage(points, worldSize),
            ComputeRouteCoverage(routes, worldSize),
            scoreSum * invPoiCount,
            routeCostSum * invRouteCount,
            routeScenicSum * invRouteCount,
            routeTraversabilitySum * invRouteCount,
            poiCounts[(int)TerrainPointOfInterestKind.SettlementCandidate],
            poiCounts[(int)TerrainPointOfInterestKind.Vista],
            poiCounts[(int)TerrainPointOfInterestKind.RiverCrossing],
            poiCounts[(int)TerrainPointOfInterestKind.MountainPass],
            poiCounts[(int)TerrainPointOfInterestKind.CoastalLanding],
            poiCounts[(int)TerrainPointOfInterestKind.ResourceGrove],
            poiCounts[(int)TerrainPointOfInterestKind.AncientSite],
            poiCounts[(int)TerrainPointOfInterestKind.CanyonOverlook],
            poiCounts[(int)TerrainPointOfInterestKind.Oasis],
            settlementTierCounts[(int)TerrainSettlementTier.Village],
            settlementTierCounts[(int)TerrainSettlementTier.Town],
            settlementTierCounts[(int)TerrainSettlementTier.OasisHub],
            routeCounts[(int)TerrainRouteKind.PrimaryTrail],
            routeCounts[(int)TerrainRouteKind.RiverRoad],
            routeCounts[(int)TerrainRouteKind.RidgePass],
            routeCounts[(int)TerrainRouteKind.CoastalPath],
            routeCounts[(int)TerrainRouteKind.ScenicTrail]);
    }

    private static float ComputePointCoverage(
        TerrainWorldPointOfInterest[] points,
        float worldSize)
    {
        if (points.Length == 0)
        {
            return 0.0f;
        }

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        foreach (TerrainWorldPointOfInterest point in points)
        {
            minX = Mathf.Min(minX, point.WorldPosition.X);
            maxX = Mathf.Max(maxX, point.WorldPosition.X);
            minY = Mathf.Min(minY, point.WorldPosition.Y);
            maxY = Mathf.Max(maxY, point.WorldPosition.Y);
        }

        return ComputeNormalizedCoverage(minX, maxX, minY, maxY, worldSize);
    }

    private static float ComputePointCoverage(
        List<TerrainWorldPointOfInterest> points,
        float worldSize)
    {
        if (points.Count == 0)
        {
            return 0.0f;
        }

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        foreach (TerrainWorldPointOfInterest point in points)
        {
            minX = Mathf.Min(minX, point.WorldPosition.X);
            maxX = Mathf.Max(maxX, point.WorldPosition.X);
            minY = Mathf.Min(minY, point.WorldPosition.Y);
            maxY = Mathf.Max(maxY, point.WorldPosition.Y);
        }

        return ComputeNormalizedCoverage(minX, maxX, minY, maxY, worldSize);
    }

    private static float ComputeCoverageWithCandidate(
        List<TerrainWorldPointOfInterest> points,
        Vector2 candidateWorldPosition,
        float worldSize)
    {
        if (points.Count == 0)
        {
            return 0.0f;
        }

        float minX = candidateWorldPosition.X;
        float maxX = candidateWorldPosition.X;
        float minY = candidateWorldPosition.Y;
        float maxY = candidateWorldPosition.Y;

        foreach (TerrainWorldPointOfInterest point in points)
        {
            minX = Mathf.Min(minX, point.WorldPosition.X);
            maxX = Mathf.Max(maxX, point.WorldPosition.X);
            minY = Mathf.Min(minY, point.WorldPosition.Y);
            maxY = Mathf.Max(maxY, point.WorldPosition.Y);
        }

        return ComputeNormalizedCoverage(minX, maxX, minY, maxY, worldSize);
    }

    private static float ComputeNearestPointDistanceRatio(
        Vector2 worldPosition,
        List<TerrainWorldPointOfInterest> points,
        float worldSize)
    {
        float minDistanceSquared = float.PositiveInfinity;
        foreach (TerrainWorldPointOfInterest point in points)
        {
            minDistanceSquared = Mathf.Min(minDistanceSquared, worldPosition.DistanceSquaredTo(point.WorldPosition));
        }

        float distance = Mathf.Sqrt(minDistanceSquared);
        return Mathf.Clamp(distance / Mathf.Max(1.0f, worldSize * 0.32f), 0.0f, 1.0f);
    }

    private static float ComputeRouteCoverage(
        TerrainWorldRoute[] routes,
        float worldSize)
    {
        bool hasWaypoint = false;
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        foreach (TerrainWorldRoute route in routes)
        {
            foreach (Vector2 waypoint in route.Waypoints)
            {
                hasWaypoint = true;
                minX = Mathf.Min(minX, waypoint.X);
                maxX = Mathf.Max(maxX, waypoint.X);
                minY = Mathf.Min(minY, waypoint.Y);
                maxY = Mathf.Max(maxY, waypoint.Y);
            }
        }

        return hasWaypoint
            ? ComputeNormalizedCoverage(minX, maxX, minY, maxY, worldSize)
            : 0.0f;
    }

    private static float ComputeNormalizedCoverage(
        float minX,
        float maxX,
        float minY,
        float maxY,
        float worldSize)
    {
        float safeWorldSize = Mathf.Max(1.0f, worldSize);
        float coverageX = Mathf.Clamp((maxX - minX) / safeWorldSize, 0.0f, 1.0f);
        float coverageY = Mathf.Clamp((maxY - minY) / safeWorldSize, 0.0f, 1.0f);
        return Mathf.Clamp(Mathf.Sqrt((coverageX * coverageX) + (coverageY * coverageY)) / 1.4142135f, 0.0f, 1.0f);
    }

    private static TerrainWorldRoute? TryBuildRoute(
        TerrainWorldPointOfInterest from,
        TerrainWorldPointOfInterest to,
        TerrainWorldField[] fields,
        TerrainGenerationProfile profile,
        int resolution)
    {
        int start = Index(from.GridX, from.GridY, resolution);
        int goal = Index(to.GridX, to.GridY, resolution);
        int count = fields.Length;
        var frontier = new PriorityQueue<int, float>();
        var cameFrom = new int[count];
        var costSoFar = new float[count];

        for (int i = 0; i < count; i++)
        {
            cameFrom[i] = -1;
            costSoFar[i] = float.PositiveInfinity;
        }

        frontier.Enqueue(start, 0.0f);
        cameFrom[start] = start;
        costSoFar[start] = 0.0f;

        while (frontier.Count > 0)
        {
            int current = frontier.Dequeue();
            if (current == goal)
            {
                break;
            }

            int currentX = current % resolution;
            int currentY = current / resolution;
            TerrainWorldField currentField = fields[current];

            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0)
                    {
                        continue;
                    }

                    int nx = currentX + ox;
                    int ny = currentY + oy;
                    if (!InBounds(nx, ny, resolution))
                    {
                        continue;
                    }

                    int next = Index(nx, ny, resolution);
                    TerrainWorldField nextField = fields[next];
                    float moveCost = StepCost(currentField, nextField, profile, ox != 0 && oy != 0);
                    if (moveCost >= ImpassableCost)
                    {
                        continue;
                    }

                    float newCost = costSoFar[current] + moveCost;
                    if (newCost >= costSoFar[next])
                    {
                        continue;
                    }

                    costSoFar[next] = newCost;
                    cameFrom[next] = current;
                    float heuristic = Mathf.Abs(nx - to.GridX) + Mathf.Abs(ny - to.GridY);
                    frontier.Enqueue(next, newCost + heuristic);
                }
            }
        }

        if (cameFrom[goal] < 0)
        {
            return null;
        }

        List<int> path = ReconstructPath(goal, cameFrom);
        if (path.Count < 2)
        {
            return null;
        }

        Vector2[] waypoints = BuildWaypoints(path, fields);
        float scenic = 0.0f;
        float traversability = 0.0f;
        float river = 0.0f;
        float highland = 0.0f;
        float coast = 0.0f;
        float water = 0.0f;

        foreach (int index in path)
        {
            TerrainWorldField field = fields[index];
            scenic += field.ScenicPotential;
            traversability += field.Traversability;
            river += field.River;
            highland += field.LandscapeKind is TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.VistaPlateau ? 1.0f : 0.0f;
            coast += field.LandscapeKind == TerrainLandscapeKind.Coast ? 1.0f : 0.0f;
            water += field.Height < profile.SeaLevel - 4.0f ? 1.0f : 0.0f;
        }

        float invCount = 1.0f / path.Count;
        scenic *= invCount;
        traversability *= invCount;
        river *= invCount;
        highland *= invCount;
        coast *= invCount;
        water *= invCount;

        return new TerrainWorldRoute(
            from.Id,
            to.Id,
            ClassifyRoute(from, to, scenic, river, highland, coast, water),
            costSoFar[goal],
            scenic,
            traversability,
            waypoints);
    }

    private static float StepCost(
        TerrainWorldField current,
        TerrainWorldField next,
        TerrainGenerationProfile profile,
        bool diagonal)
    {
        float waterDepth = profile.SeaLevel - next.Height;
        if (waterDepth > profile.HeightScale * 0.62f)
        {
            return ImpassableCost;
        }

        float baseCost = diagonal ? 1.4142f : 1.0f;
        float traversabilityPenalty = (1.0f - next.Traversability) * 4.5f;
        float heightDeltaPenalty = Mathf.Clamp(Mathf.Abs(next.Height - current.Height) / Mathf.Max(1.0f, profile.HeightScale * 0.18f), 0.0f, 4.0f);
        float riverPenalty = next.River > 0.72f ? 1.4f : next.River * 0.38f;
        float waterPenalty = waterDepth > 4.0f ? 5.8f + Mathf.Clamp(waterDepth / 90.0f, 0.0f, 5.5f) : 0.0f;
        float scenicBonus = next.ScenicPotential * 0.18f;

        return baseCost * Mathf.Max(0.35f, 1.0f + traversabilityPenalty + heightDeltaPenalty + riverPenalty + waterPenalty - scenicBonus);
    }

    private static List<int> ReconstructPath(int goal, int[] cameFrom)
    {
        var path = new List<int>();
        int current = goal;
        while (cameFrom[current] != current)
        {
            path.Add(current);
            current = cameFrom[current];
            if (current < 0)
            {
                break;
            }
        }

        if (current >= 0)
        {
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static Vector2[] BuildWaypoints(List<int> path, TerrainWorldField[] fields)
    {
        int stride = Mathf.Max(1, path.Count / 48);
        var waypoints = new List<Vector2>(Mathf.Min(path.Count, 64));

        for (int i = 0; i < path.Count; i += stride)
        {
            waypoints.Add(fields[path[i]].WorldPosition);
        }

        Vector2 last = fields[path[^1]].WorldPosition;
        if (waypoints.Count == 0 || waypoints[^1] != last)
        {
            waypoints.Add(last);
        }

        return waypoints.ToArray();
    }

    private static TerrainRouteKind ClassifyRoute(
        TerrainWorldPointOfInterest from,
        TerrainWorldPointOfInterest to,
        float scenic,
        float river,
        float highland,
        float coast,
        float water)
    {
        if (water > 0.12f || coast > 0.32f || from.Kind == TerrainPointOfInterestKind.CoastalLanding || to.Kind == TerrainPointOfInterestKind.CoastalLanding)
        {
            return TerrainRouteKind.CoastalPath;
        }

        if (river > 0.55f)
        {
            return TerrainRouteKind.RiverRoad;
        }

        if (highland > 0.55f)
        {
            return TerrainRouteKind.RidgePass;
        }

        if (scenic > 0.62f || from.Kind == TerrainPointOfInterestKind.Vista || to.Kind == TerrainPointOfInterestKind.Vista)
        {
            return TerrainRouteKind.ScenicTrail;
        }

        if (river > 0.34f || from.Kind == TerrainPointOfInterestKind.RiverCrossing || to.Kind == TerrainPointOfInterestKind.RiverCrossing)
        {
            return TerrainRouteKind.RiverRoad;
        }

        if (highland > 0.34f || from.Kind == TerrainPointOfInterestKind.MountainPass || to.Kind == TerrainPointOfInterestKind.MountainPass)
        {
            return TerrainRouteKind.RidgePass;
        }

        return TerrainRouteKind.PrimaryTrail;
    }

    private static TerrainWorldRegionKind ClassifyRegion(TerrainWorldField field)
    {
        if (field.BiomeKind == TerrainBiomeKind.Lake || field.LandscapeKind == TerrainLandscapeKind.Lake)
        {
            return TerrainWorldRegionKind.Lake;
        }

        if (field.BiomeKind is TerrainBiomeKind.Island or TerrainBiomeKind.Plains or TerrainBiomeKind.Grassland or
            TerrainBiomeKind.Desert or TerrainBiomeKind.Oasis or TerrainBiomeKind.Hills)
        {
            return field.BiomeKind switch
            {
                TerrainBiomeKind.Island => TerrainWorldRegionKind.Island,
                TerrainBiomeKind.Plains => TerrainWorldRegionKind.Plains,
                TerrainBiomeKind.Grassland => TerrainWorldRegionKind.Grassland,
                TerrainBiomeKind.Desert => TerrainWorldRegionKind.Desert,
                TerrainBiomeKind.Oasis => TerrainWorldRegionKind.Oasis,
                TerrainBiomeKind.Hills => TerrainWorldRegionKind.Hills,
                _ => TerrainWorldRegionKind.Lowland
            };
        }

        return field.LandscapeKind switch
        {
            TerrainLandscapeKind.Ocean => TerrainWorldRegionKind.Ocean,
            TerrainLandscapeKind.Coast => TerrainWorldRegionKind.Coast,
            TerrainLandscapeKind.Lowland => TerrainWorldRegionKind.Lowland,
            TerrainLandscapeKind.Wetland => TerrainWorldRegionKind.Wetland,
            TerrainLandscapeKind.ForestBasin => TerrainWorldRegionKind.Forest,
            TerrainLandscapeKind.RiverValley => TerrainWorldRegionKind.RiverValley,
            TerrainLandscapeKind.Canyon => TerrainWorldRegionKind.Canyon,
            TerrainLandscapeKind.Highlands => TerrainWorldRegionKind.Highlands,
            TerrainLandscapeKind.MountainMassif => TerrainWorldRegionKind.Mountains,
            TerrainLandscapeKind.Snowfield => TerrainWorldRegionKind.Snow,
            TerrainLandscapeKind.VistaPlateau => TerrainWorldRegionKind.ScenicPlateau,
            _ => TerrainWorldRegionKind.Lowland
        };
    }

    private static Vector2 CellCenter(Vector2 center, float worldSize, int resolution, int x, int y)
    {
        float invResolution = 1.0f / resolution;
        return new Vector2(
            center.X + ((x + 0.5f) * invResolution - 0.5f) * worldSize,
            center.Y + ((y + 0.5f) * invResolution - 0.5f) * worldSize);
    }

    private static bool InBounds(int x, int y, int resolution)
    {
        return x >= 0 && y >= 0 && x < resolution && y < resolution;
    }

    private static int Index(int x, int y, int resolution)
    {
        return y * resolution + x;
    }

    private static long PointPairKey(int a, int b)
    {
        int min = Math.Min(a, b);
        int max = Math.Max(a, b);
        return ((long)min << 32) | (uint)max;
    }

    private static bool IsSettlementHub(TerrainWorldPointOfInterest point)
    {
        return point.SettlementTier is TerrainSettlementTier.Village or TerrainSettlementTier.Town or TerrainSettlementTier.OasisHub;
    }

    private static bool IsSettlementHub(TerrainWorldPointOfInterest[] points, int pointId)
    {
        return (uint)pointId < (uint)points.Length && IsSettlementHub(points[pointId]);
    }

    private static int CountSettlementHubs(TerrainWorldPointOfInterest[] points)
    {
        int count = 0;
        foreach (TerrainWorldPointOfInterest point in points)
        {
            if (IsSettlementHub(point))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountSettlementRoutes(
        TerrainWorldPointOfInterest[] points,
        List<TerrainWorldRoute> routes)
    {
        int count = 0;
        foreach (TerrainWorldRoute route in routes)
        {
            if (IsSettlementHub(points, route.FromPointId) && IsSettlementHub(points, route.ToPointId))
            {
                count++;
            }
        }

        return count;
    }

    private static float SettlementTierRouteWeight(TerrainSettlementTier tier)
    {
        return tier switch
        {
            TerrainSettlementTier.Town => 1.0f,
            TerrainSettlementTier.OasisHub => 0.94f,
            TerrainSettlementTier.Village => 0.76f,
            _ => 0.0f
        };
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x * 0x9E3779B9u;
            h = (h << 13) | (h >> 19);
            h ^= (uint)y * 0x85EBCA6Bu;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215.0f;
        }
    }

    private static int CountNonZero(ReadOnlySpan<int> values)
    {
        int count = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] > 0)
            {
                count++;
            }
        }

        return count;
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
