using System;
using System.Text;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Threshold bounds for validating player experience metrics against a world plan.</summary>
public readonly record struct TerrainExperienceThresholds(
    float MinEncounterRichRegionRatio,
    float MinResourceRichRegionRatio,
    float MinHazardRichRegionRatio,
    float MinAverageEncounterPotential,
    float MinAverageResourcePotential,
    float MinRouteRhythmScore,
    float MinPointOfInterestValue,
    float MinRiskRewardBalance,
    float MinScenicAnchorRatio)
{
    public static TerrainExperienceThresholds OpenWorldDefault { get; } = new(
        MinEncounterRichRegionRatio: 0.22f,
        MinResourceRichRegionRatio: 0.18f,
        MinHazardRichRegionRatio: 0.12f,
        MinAverageEncounterPotential: 0.34f,
        MinAverageResourcePotential: 0.30f,
        MinRouteRhythmScore: 0.46f,
        MinPointOfInterestValue: 0.58f,
        MinRiskRewardBalance: 0.42f,
        MinScenicAnchorRatio: 0.28f);
}

/// <summary>Player experience metrics derived from region attributes, POI values, and route rhythms.</summary>
public readonly record struct TerrainExperienceReport(
    int RegionCount,
    float EncounterRichRegionRatio,
    float ResourceRichRegionRatio,
    float HazardRichRegionRatio,
    float AverageExposure,
    float AverageResourcePotential,
    float AverageHazardPotential,
    float AverageEncounterPotential,
    float RouteRhythmScore,
    float PointOfInterestValue,
    float RiskRewardBalance,
    float ScenicAnchorRatio);

/// <summary>Result of validating experience metrics against configured thresholds.</summary>
public readonly record struct TerrainExperienceGateResult(
    bool Passed,
    TerrainExperienceReport Report,
    string Summary);

/// <summary>Analyzes world plan regions, POIs, and routes to produce player experience reports and validation gates.</summary>
public static class TerrainExperienceAnalyzer
{
    /// <summary>Analyzes a world plan to produce an experience report.</summary>
    public static TerrainExperienceReport Analyze(
        TerrainWorldPlan plan,
        CancellationToken cancellationToken = default)
    {
        return Analyze(plan.Regions, plan.PointsOfInterest, plan.Routes, plan.PlanningReport, cancellationToken);
    }

    /// <summary>Analyzes raw region, POI, and route data to produce an experience report.</summary>
    public static TerrainExperienceReport Analyze(
        ReadOnlySpan<TerrainWorldRegion> regions,
        ReadOnlySpan<TerrainWorldPointOfInterest> pointsOfInterest,
        ReadOnlySpan<TerrainWorldRoute> routes,
        TerrainWorldPlanningReport planningReport,
        CancellationToken cancellationToken = default)
    {
        return TerrainExperienceAnalysisService.Analyze(
            regions,
            pointsOfInterest,
            routes,
            planningReport,
            cancellationToken);
    }

    /// <summary>Analyzes and validates a world plan against default open-world experience thresholds.</summary>
    public static TerrainExperienceGateResult ValidateOpenWorldDefault(
        TerrainWorldPlan plan,
        CancellationToken cancellationToken = default)
    {
        return Validate(Analyze(plan, cancellationToken), TerrainExperienceThresholds.OpenWorldDefault);
    }

    /// <summary>Validates a pre-computed experience report against default open-world thresholds.</summary>
    public static TerrainExperienceGateResult ValidateOpenWorldDefault(TerrainExperienceReport report)
    {
        return Validate(report, TerrainExperienceThresholds.OpenWorldDefault);
    }

    /// <summary>Validates an experience report against the given thresholds.</summary>
    public static TerrainExperienceGateResult Validate(
        TerrainExperienceReport report,
        TerrainExperienceThresholds thresholds)
    {
        return TerrainExperienceAnalysisService.Validate(report, thresholds);
    }
}
