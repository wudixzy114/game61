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
        double exposureSum = 0.0;
        double resourceSum = 0.0;
        double hazardSum = 0.0;
        double encounterSum = 0.0;
        int playableRegionCount = 0;
        int encounterRichCount = 0;
        int resourceRichCount = 0;
        int hazardRichCount = 0;

        foreach (TerrainWorldRegion region in regions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (region.RegionKind == TerrainWorldRegionKind.Ocean)
            {
                continue;
            }

            playableRegionCount++;
            exposureSum += region.Exposure;
            resourceSum += region.ResourcePotential;
            hazardSum += region.HazardPotential;
            encounterSum += region.EncounterPotential;

            if (region.EncounterPotential >= 0.52f)
            {
                encounterRichCount++;
            }

            if (region.ResourcePotential >= 0.50f)
            {
                resourceRichCount++;
            }

            if (region.HazardPotential >= 0.42f)
            {
                hazardRichCount++;
            }
        }

        float invPlayableRegionCount = playableRegionCount == 0 ? 0.0f : 1.0f / playableRegionCount;
        float averageResource = (float)(resourceSum * invPlayableRegionCount);
        float averageHazard = (float)(hazardSum * invPlayableRegionCount);

        return new TerrainExperienceReport(
            playableRegionCount,
            encounterRichCount * invPlayableRegionCount,
            resourceRichCount * invPlayableRegionCount,
            hazardRichCount * invPlayableRegionCount,
            (float)(exposureSum * invPlayableRegionCount),
            averageResource,
            averageHazard,
            (float)(encounterSum * invPlayableRegionCount),
            ComputeRouteRhythmScore(routes, planningReport, cancellationToken),
            ComputePointOfInterestValue(pointsOfInterest, cancellationToken),
            ComputeRiskRewardBalance(averageResource, averageHazard),
            ComputeScenicAnchorRatio(pointsOfInterest, cancellationToken));
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
        var summary = new StringBuilder();
        bool passed = true;

        AppendGate(
            summary,
            "encounter rich regions",
            report.EncounterRichRegionRatio >= thresholds.MinEncounterRichRegionRatio,
            $"{report.EncounterRichRegionRatio:0.000}",
            $">= {thresholds.MinEncounterRichRegionRatio:0.000}",
            ref passed);
        AppendGate(
            summary,
            "resource rich regions",
            report.ResourceRichRegionRatio >= thresholds.MinResourceRichRegionRatio,
            $"{report.ResourceRichRegionRatio:0.000}",
            $">= {thresholds.MinResourceRichRegionRatio:0.000}",
            ref passed);
        AppendGate(
            summary,
            "hazard rich regions",
            report.HazardRichRegionRatio >= thresholds.MinHazardRichRegionRatio,
            $"{report.HazardRichRegionRatio:0.000}",
            $">= {thresholds.MinHazardRichRegionRatio:0.000}",
            ref passed);
        AppendGate(
            summary,
            "average encounter potential",
            report.AverageEncounterPotential >= thresholds.MinAverageEncounterPotential,
            $"{report.AverageEncounterPotential:0.000}",
            $">= {thresholds.MinAverageEncounterPotential:0.000}",
            ref passed);
        AppendGate(
            summary,
            "average resource potential",
            report.AverageResourcePotential >= thresholds.MinAverageResourcePotential,
            $"{report.AverageResourcePotential:0.000}",
            $">= {thresholds.MinAverageResourcePotential:0.000}",
            ref passed);
        AppendGate(
            summary,
            "route rhythm",
            report.RouteRhythmScore >= thresholds.MinRouteRhythmScore,
            $"{report.RouteRhythmScore:0.000}",
            $">= {thresholds.MinRouteRhythmScore:0.000}",
            ref passed);
        AppendGate(
            summary,
            "point of interest value",
            report.PointOfInterestValue >= thresholds.MinPointOfInterestValue,
            $"{report.PointOfInterestValue:0.000}",
            $">= {thresholds.MinPointOfInterestValue:0.000}",
            ref passed);
        AppendGate(
            summary,
            "risk reward balance",
            report.RiskRewardBalance >= thresholds.MinRiskRewardBalance,
            $"{report.RiskRewardBalance:0.000}",
            $">= {thresholds.MinRiskRewardBalance:0.000}",
            ref passed);
        AppendGate(
            summary,
            "scenic anchor ratio",
            report.ScenicAnchorRatio >= thresholds.MinScenicAnchorRatio,
            $"{report.ScenicAnchorRatio:0.000}",
            $">= {thresholds.MinScenicAnchorRatio:0.000}",
            ref passed);

        return new TerrainExperienceGateResult(passed, report, summary.ToString());
    }

    private static float ComputeRouteRhythmScore(
        ReadOnlySpan<TerrainWorldRoute> routes,
        TerrainWorldPlanningReport planningReport,
        CancellationToken cancellationToken)
    {
        if (routes.Length == 0)
        {
            return 0.0f;
        }

        Span<int> kindCounts = stackalloc int[Enum.GetValues<TerrainRouteKind>().Length];
        float scenicSum = 0.0f;
        float traversabilitySum = 0.0f;
        int scenicRouteCount = 0;

        foreach (TerrainWorldRoute route in routes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            scenicSum += route.AverageScenicPotential;
            traversabilitySum += route.AverageTraversability;
            kindCounts[Mathf.Clamp((int)route.Kind, 0, kindCounts.Length - 1)]++;

            if (route.AverageScenicPotential >= 0.34f && route.AverageTraversability >= 0.24f)
            {
                scenicRouteCount++;
            }
        }

        float invRouteCount = 1.0f / routes.Length;
        float kindVariety = CountNonZero(kindCounts) / (float)kindCounts.Length;
        float scenicRouteRatio = scenicRouteCount * invRouteCount;
        float connected = planningReport.ConnectedPointRatio;

        return Mathf.Clamp(
            kindVariety * 0.22f +
            scenicRouteRatio * 0.26f +
            (scenicSum * invRouteCount) * 0.22f +
            (traversabilitySum * invRouteCount) * 0.18f +
            connected * 0.12f,
            0.0f,
            1.0f);
    }

    private static float ComputePointOfInterestValue(
        ReadOnlySpan<TerrainWorldPointOfInterest> pointsOfInterest,
        CancellationToken cancellationToken)
    {
        if (pointsOfInterest.Length == 0)
        {
            return 0.0f;
        }

        float scoreSum = 0.0f;
        float scenicSum = 0.0f;
        float traversalSum = 0.0f;
        float settlementSum = 0.0f;
        foreach (TerrainWorldPointOfInterest point in pointsOfInterest)
        {
            cancellationToken.ThrowIfCancellationRequested();

            scoreSum += point.Score;
            scenicSum += point.ScenicPotential;
            traversalSum += point.Traversability;
            settlementSum += point.SettlementTier switch
            {
                TerrainSettlementTier.Town => 1.0f,
                TerrainSettlementTier.OasisHub => 0.92f,
                TerrainSettlementTier.Village => 0.72f,
                _ => 0.0f
            };
        }

        float invPointCount = 1.0f / pointsOfInterest.Length;
        return Mathf.Clamp(
            (scoreSum * invPointCount) * 0.50f +
            (scenicSum * invPointCount) * 0.24f +
            (traversalSum * invPointCount) * 0.16f +
            (settlementSum * invPointCount) * 0.10f,
            0.0f,
            1.0f);
    }

    private static float ComputeRiskRewardBalance(float averageResource, float averageHazard)
    {
        float hazardShape = 1.0f - Mathf.Clamp(Mathf.Abs(averageHazard - 0.34f) / 0.34f, 0.0f, 1.0f);
        return Mathf.Clamp(averageResource * 0.62f + hazardShape * 0.38f, 0.0f, 1.0f);
    }

    private static float ComputeScenicAnchorRatio(
        ReadOnlySpan<TerrainWorldPointOfInterest> pointsOfInterest,
        CancellationToken cancellationToken)
    {
        if (pointsOfInterest.Length == 0)
        {
            return 0.0f;
        }

        int scenicAnchors = 0;
        foreach (TerrainWorldPointOfInterest point in pointsOfInterest)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (point.Kind is TerrainPointOfInterestKind.Vista or
                    TerrainPointOfInterestKind.MountainPass or
                    TerrainPointOfInterestKind.CanyonOverlook or
                    TerrainPointOfInterestKind.Oasis ||
                point.ScenicPotential >= 0.62f)
            {
                scenicAnchors++;
            }
        }

        return scenicAnchors / (float)pointsOfInterest.Length;
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
}
