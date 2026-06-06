using System;
using System.Collections.Generic;
using System.Text;
using Dao.Terrain;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanExporter
{
    /// <summary>Creates a detailed text report describing a world plan's quality, planning, and experience metrics.</summary>
    public static string CreateTextReport(
        TerrainWorldPlan plan,
        TerrainWorldPlanningGateResult planningGate,
        TerrainQualityGateResult qualityGate,
        TerrainExperienceGateResult experienceGate,
        string? mapPath = null)
    {
        return CreateTextReport(plan, profile: null, planningGate, qualityGate, experienceGate, mapPath);
    }

    /// <summary>Creates a detailed text report with profile identity metadata for audits and cache validation.</summary>
    public static string CreateTextReport(
        TerrainWorldPlan plan,
        TerrainGenerationProfile profile,
        TerrainWorldPlanningGateResult planningGate,
        TerrainQualityGateResult qualityGate,
        TerrainExperienceGateResult experienceGate,
        string? mapPath = null)
    {
        return CreateTextReport(plan, (TerrainGenerationProfile?)profile, planningGate, qualityGate, experienceGate, mapPath);
    }

    private static string CreateTextReport(
        TerrainWorldPlan plan,
        TerrainGenerationProfile? profile,
        TerrainWorldPlanningGateResult planningGate,
        TerrainQualityGateResult qualityGate,
        TerrainExperienceGateResult experienceGate,
        string? mapPath)
    {
        TerrainQualityReport quality = qualityGate.Report;
        TerrainWorldPlanningReport planning = planningGate.Report;
        TerrainExperienceReport experience = experienceGate.Report;
        var builder = new StringBuilder(4096);

        builder.AppendLine("Open World Terrain Plan");
        builder.AppendLine($"Terrain API Contract: {TerrainApiVersion.Contract}");
        builder.AppendLine($"Terrain API Version: {TerrainApiVersion.Version}");
        builder.AppendLine($"Terrain Plan Contract: {TerrainWorldPlanSerializer.Contract}");
        builder.AppendLine($"Terrain Generator Version: {TerrainWorldPlanSerializer.GeneratorVersion}");
        builder.AppendLine($"Terrain Determinism Contract: {TerrainDeterminismContract.Contract}");
        if (profile is TerrainGenerationProfile value)
        {
            builder.AppendLine($"Terrain Profile Hash: {value.StableHash()}");
            builder.AppendLine($"Terrain Scatter Rule Set Hash: {TerrainRuleSetHashNormalizer.NormalizeScatterRuleSetHash(value.ScatterRuleSetHash)}");
            builder.AppendLine($"Terrain Settlement Visual Rule Set Hash: {TerrainRuleSetHashNormalizer.NormalizeSettlementVisualRuleSetHash(value.SettlementVisualRuleSetHash)}");
            builder.AppendLine($"Terrain POI Rule Set Hash: {TerrainRuleSetHashNormalizer.NormalizePointOfInterestRuleSetHash(value.PointOfInterestRuleSetHash)}");
            builder.AppendLine($"Terrain Route Rule Set Hash: {TerrainRuleSetHashNormalizer.NormalizeRouteRuleSetHash(value.RouteRuleSetHash)}");
            builder.AppendLine($"Terrain Scenic Landmark Rule Set Hash: {TerrainRuleSetHashNormalizer.NormalizeScenicRuleSetHash(value.ScenicLandmarkRuleSetHash)}");
        }

        builder.AppendLine(FormattableString.Invariant($"Center: {plan.Center.X:0.##}, {plan.Center.Y:0.##}"));
        builder.AppendLine(FormattableString.Invariant($"World size: {plan.WorldSize:0.##} meters"));
        builder.AppendLine(FormattableString.Invariant($"Planning grid: {plan.GridResolution} x {plan.GridResolution}"));
        if (!string.IsNullOrWhiteSpace(mapPath))
        {
            builder.AppendLine($"Map: {mapPath}");
        }

        builder.AppendLine();
        builder.AppendLine("Terrain Quality Gate");
        builder.Append(qualityGate.Summary);
        builder.AppendLine(FormattableString.Invariant(
            $"Height range: {quality.MinHeight:0.0} to {quality.MaxHeight:0.0}, average {quality.AverageHeight:0.0}"));
        builder.AppendLine(FormattableString.Invariant(
            $"Land/ocean/coast: {quality.LandRatio:0.000} / {quality.OceanRatio:0.000} / {quality.CoastRatio:0.000}"));
        builder.AppendLine(FormattableString.Invariant(
            $"Rivers/scenic/traversable: {quality.RiverRatio:0.000} / {quality.ScenicRatio:0.000} / {quality.TraversableLandRatio:0.000}"));
        builder.AppendLine(FormattableString.Invariant($"Landscape kinds: {quality.DistinctLandscapeKinds}"));
        builder.AppendLine(FormattableString.Invariant($"Biome kinds: {quality.DistinctBiomeKinds}"));

        builder.AppendLine();
        builder.AppendLine("Open World Planning Gate");
        builder.Append(planningGate.Summary);
        builder.AppendLine(FormattableString.Invariant($"POIs/routes: {planning.PointOfInterestCount} / {planning.RouteCount}"));
        builder.AppendLine(FormattableString.Invariant($"Connected point ratio: {planning.ConnectedPointRatio:0.000}"));
        builder.AppendLine(FormattableString.Invariant($"Connected settlement ratio: {planning.ConnectedSettlementRatio:0.000}"));
        builder.AppendLine(FormattableString.Invariant(
            $"World coverage POIs/routes: {planning.PointOfInterestWorldCoverage:0.000} / {planning.RouteWorldCoverage:0.000}"));
        builder.AppendLine(FormattableString.Invariant($"Average point score: {planning.AveragePointScore:0.000}"));
        builder.AppendLine(FormattableString.Invariant(
            $"Average route cost/scenic/traversability: {planning.AverageRouteCost:0.0} / {planning.AverageRouteScenicPotential:0.000} / {planning.AverageRouteTraversability:0.000}"));

        builder.AppendLine();
        builder.AppendLine("Open World Experience Gate");
        builder.Append(experienceGate.Summary);
        builder.AppendLine(FormattableString.Invariant(
            $"Encounter/resource/hazard rich regions: {experience.EncounterRichRegionRatio:0.000} / {experience.ResourceRichRegionRatio:0.000} / {experience.HazardRichRegionRatio:0.000}"));
        builder.AppendLine(FormattableString.Invariant(
            $"Average exposure/resource/hazard/encounter: {experience.AverageExposure:0.000} / {experience.AverageResourcePotential:0.000} / {experience.AverageHazardPotential:0.000} / {experience.AverageEncounterPotential:0.000}"));
        builder.AppendLine(FormattableString.Invariant(
            $"Route rhythm / POI value / risk reward / scenic anchors: {experience.RouteRhythmScore:0.000} / {experience.PointOfInterestValue:0.000} / {experience.RiskRewardBalance:0.000} / {experience.ScenicAnchorRatio:0.000}"));

        builder.AppendLine();
        builder.AppendLine("Point Of Interest Counts");
        AppendPoiCount(builder, TerrainPointOfInterestKind.SettlementCandidate, planning.SettlementCandidateCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.Vista, planning.VistaCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.RiverCrossing, planning.RiverCrossingCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.MountainPass, planning.MountainPassCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.CoastalLanding, planning.CoastalLandingCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.ResourceGrove, planning.ResourceGroveCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.AncientSite, planning.AncientSiteCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.CanyonOverlook, planning.CanyonOverlookCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.Oasis, planning.OasisCount);

        builder.AppendLine();
        builder.AppendLine("Settlement Development");
        AppendSettlementTierCount(builder, TerrainSettlementTier.Village, planning.VillageCount);
        AppendSettlementTierCount(builder, TerrainSettlementTier.Town, planning.TownCount);
        AppendSettlementTierCount(builder, TerrainSettlementTier.OasisHub, planning.OasisHubCount);

        builder.AppendLine();
        builder.AppendLine("Settlement Network");
        builder.AppendLine(FormattableString.Invariant($"Connected settlement ratio: {planning.ConnectedSettlementRatio:0.000}"));
        builder.AppendLine(FormattableString.Invariant($"Direct settlement routes: {planning.SettlementRouteCount}"));

        builder.AppendLine();
        builder.AppendLine("Biome Counts");
        AppendBiomeCount(builder, TerrainBiomeKind.Ocean, quality.BiomeOceanCount);
        AppendBiomeCount(builder, TerrainBiomeKind.Coast, quality.BiomeCoastCount);
        AppendBiomeCount(builder, TerrainBiomeKind.Island, quality.IslandCount);
        AppendBiomeCount(builder, TerrainBiomeKind.Plains, quality.PlainsCount);
        AppendBiomeCount(builder, TerrainBiomeKind.Grassland, quality.GrasslandCount);
        AppendBiomeCount(builder, TerrainBiomeKind.Desert, quality.DesertCount);
        AppendBiomeCount(builder, TerrainBiomeKind.Oasis, quality.OasisCount);
        AppendBiomeCount(builder, TerrainBiomeKind.Forest, quality.ForestCount);
        AppendBiomeCount(builder, TerrainBiomeKind.Wetland, quality.BiomeWetlandCount);
        AppendBiomeCount(builder, TerrainBiomeKind.Hills, quality.HillsCount);
        AppendBiomeCount(builder, TerrainBiomeKind.Mountains, quality.MountainsCount);
        AppendBiomeCount(builder, TerrainBiomeKind.Snowfield, quality.BiomeSnowfieldCount);
        AppendBiomeCount(builder, TerrainBiomeKind.Lake, quality.BiomeLakeCount);

        builder.AppendLine();
        builder.AppendLine("Route Counts");
        AppendRouteCount(builder, TerrainRouteKind.PrimaryTrail, planning.PrimaryTrailCount);
        AppendRouteCount(builder, TerrainRouteKind.RiverRoad, planning.RiverRoadCount);
        AppendRouteCount(builder, TerrainRouteKind.RidgePass, planning.RidgePassCount);
        AppendRouteCount(builder, TerrainRouteKind.CoastalPath, planning.CoastalPathCount);
        AppendRouteCount(builder, TerrainRouteKind.ScenicTrail, planning.ScenicTrailCount);

        builder.AppendLine();
        builder.AppendLine("Top Points Of Interest");
        foreach (TerrainWorldPointOfInterest point in TopPoints(plan.PointsOfInterest, 12))
        {
            builder.AppendLine(FormattableString.Invariant(
                $"{point.Id:00} {point.Kind} {SettlementTierLabel(point.SettlementTier)}score {point.Score:0.000} height {point.Height:0.0} scenic {point.ScenicPotential:0.000} traversable {point.Traversability:0.000} at {point.WorldPosition.X:0.0}, {point.WorldPosition.Y:0.0}"));
        }

        return builder.ToString();
    }

    /// <summary>Saves a text report to a file.</summary>
    public static Error SaveTextReport(
        TerrainWorldPlan plan,
        TerrainWorldPlanningGateResult planningGate,
        TerrainQualityGateResult qualityGate,
        TerrainExperienceGateResult experienceGate,
        string? mapPath,
        string outputPath)
    {
        return SaveTextReport(plan, profile: null, planningGate, qualityGate, experienceGate, mapPath, outputPath);
    }

    /// <summary>Saves a text report with profile identity metadata to a file.</summary>
    public static Error SaveTextReport(
        TerrainWorldPlan plan,
        TerrainGenerationProfile profile,
        TerrainWorldPlanningGateResult planningGate,
        TerrainQualityGateResult qualityGate,
        TerrainExperienceGateResult experienceGate,
        string? mapPath,
        string outputPath)
    {
        return SaveTextReport(plan, (TerrainGenerationProfile?)profile, planningGate, qualityGate, experienceGate, mapPath, outputPath);
    }

    private static Error SaveTextReport(
        TerrainWorldPlan plan,
        TerrainGenerationProfile? profile,
        TerrainWorldPlanningGateResult planningGate,
        TerrainQualityGateResult qualityGate,
        TerrainExperienceGateResult experienceGate,
        string? mapPath,
        string outputPath)
    {
        try
        {
            EnsureDirectoryForPath(outputPath);
            string path = FileSystemPath(outputPath);
            System.IO.File.WriteAllText(
                path,
                CreateTextReport(plan, profile, planningGate, qualityGate, experienceGate, mapPath));
            return Error.Ok;
        }
        catch (Exception exception)
        {
            GD.PushError($"Failed to save terrain text report '{outputPath}': {exception.Message}");
            return Error.FileCantWrite;
        }
    }

    private static IEnumerable<TerrainWorldPointOfInterest> TopPoints(
        TerrainWorldPointOfInterest[] points,
        int maxCount)
    {
        var copy = new TerrainWorldPointOfInterest[points.Length];
        points.CopyTo(copy, 0);
        Array.Sort(copy, (a, b) => b.Score.CompareTo(a.Score));

        int count = Math.Min(maxCount, copy.Length);
        for (int i = 0; i < count; i++)
        {
            yield return copy[i];
        }
    }

    private static string SettlementTierLabel(TerrainSettlementTier tier)
    {
        return tier == TerrainSettlementTier.None
            ? string.Empty
            : $"{tier} ";
    }

    private static void AppendSettlementTierCount(StringBuilder builder, TerrainSettlementTier tier, int count)
    {
        builder.AppendLine(FormattableString.Invariant($"{tier}: {count}"));
    }

    private static void AppendPoiCount(StringBuilder builder, TerrainPointOfInterestKind kind, int count)
    {
        builder.AppendLine(FormattableString.Invariant($"{kind}: {count}"));
    }

    private static void AppendRouteCount(StringBuilder builder, TerrainRouteKind kind, int count)
    {
        builder.AppendLine(FormattableString.Invariant($"{kind}: {count}"));
    }

    private static void AppendBiomeCount(StringBuilder builder, TerrainBiomeKind kind, int count)
    {
        builder.AppendLine(FormattableString.Invariant($"{kind}: {count}"));
    }
}
