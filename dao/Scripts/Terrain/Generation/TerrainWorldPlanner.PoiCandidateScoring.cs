using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanner
{
    private static void AddPoiCandidates(
        List<PoiCandidate> candidates,
        TerrainGenerationProfile profile,
        TerrainWorldField field,
        int gridX,
        int gridY)
    {
        TerrainPoiThresholds thresholds = TerrainWorldPlannerRules.PoiThresholds;
        TerrainPoiScoringWeights weights = TerrainWorldPlannerRules.PoiScoring;
        if (field.Height < profile.SeaLevel - 4.0f)
        {
            return;
        }

        float land = Mathf.SmoothStep(profile.SeaLevel + 2.0f, profile.SeaLevel + 48.0f, field.Height);
        float elevation = Mathf.SmoothStep(profile.SeaLevel + 80.0f, profile.SeaLevel + profile.HeightScale * 0.72f, field.Height);
        float rarity = Hash01(gridX, gridY, profile.Seed + 911);
        float stableFlatLand = field.Traversability * land;

        float settlementBiomeBonus = field.BiomeKind is TerrainBiomeKind.Plains or TerrainBiomeKind.Grassland
            ? weights.SettlementPlainsGrassBonus
            : field.BiomeKind == TerrainBiomeKind.Oasis ? weights.SettlementOasisBonus : 0.0f;
        float settlementScore =
            stableFlatLand * weights.SettlementStableFlatLand +
            Mathf.Clamp(1.0f - Mathf.Abs(field.Moisture - 0.55f) * 2.0f, 0.0f, 1.0f) * weights.SettlementMoisture +
            Mathf.Clamp(1.0f - Mathf.Abs(field.Temperature - 0.56f) * 2.1f, 0.0f, 1.0f) * weights.SettlementTemperature +
            Mathf.SmoothStep(0.18f, 0.62f, field.River) * weights.SettlementRiver +
            field.ScenicPotential * weights.SettlementScenic +
            settlementBiomeBonus;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.SettlementCandidate, settlementScore, thresholds.SettlementCandidate, field, gridX, gridY, rarity);

        float vistaScore = field.ScenicPotential * weights.VistaScenic + elevation * weights.VistaElevation + rarity * weights.VistaRarity;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.Vista, vistaScore, thresholds.Vista, field, gridX, gridY, rarity);

        float crossingScore =
            Mathf.SmoothStep(0.50f, 0.82f, field.River) * weights.CrossingRiver +
            field.Traversability * weights.CrossingTraversability +
            land * weights.CrossingLand;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.RiverCrossing, crossingScore, thresholds.RiverCrossing, field, gridX, gridY, rarity);

        float passScore = 0.0f;
        if (field.LandscapeKind is TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.VistaPlateau)
        {
            passScore = elevation * weights.PassElevation +
                field.Traversability * weights.PassTraversability +
                field.ScenicPotential * weights.PassScenic +
                rarity * weights.PassRarity;
        }

        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.MountainPass, passScore, thresholds.MountainPass, field, gridX, gridY, rarity);

        float coastScore = 0.0f;
        if (field.LandscapeKind == TerrainLandscapeKind.Coast || Mathf.Abs(field.Height - profile.SeaLevel) < 30.0f)
        {
            coastScore = land * weights.CoastLand +
                field.Traversability * weights.CoastTraversability +
                field.ScenicPotential * weights.CoastScenic +
                rarity * weights.CoastRarity;
        }

        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.CoastalLanding, coastScore, thresholds.CoastalLanding, field, gridX, gridY, rarity);

        float resourceScore = 0.0f;
        if (field.LandscapeKind is TerrainLandscapeKind.ForestBasin or TerrainLandscapeKind.Wetland or TerrainLandscapeKind.RiverValley ||
            field.BiomeKind == TerrainBiomeKind.Oasis)
        {
            resourceScore = field.Moisture * weights.ResourceMoisture +
                field.Traversability * weights.ResourceTraversability +
                (1.0f - elevation) * weights.ResourceLowElevation +
                field.River * weights.ResourceRiver +
                rarity * weights.ResourceRarity;
        }

        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.ResourceGrove, resourceScore, thresholds.ResourceGrove, field, gridX, gridY, rarity);

        float ancientScore = field.ScenicPotential * weights.AncientScenic +
            elevation * weights.AncientElevation +
            stableFlatLand * weights.AncientStableFlatLand +
            rarity * weights.AncientRarity;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.AncientSite, ancientScore, thresholds.AncientSite, field, gridX, gridY, rarity);

        float canyonScore = field.LandscapeKind == TerrainLandscapeKind.Canyon
            ? field.ScenicPotential * weights.CanyonScenic +
                field.River * weights.CanyonRiver +
                elevation * weights.CanyonElevation +
                rarity * weights.CanyonRarity
            : 0.0f;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.CanyonOverlook, canyonScore, thresholds.CanyonOverlook, field, gridX, gridY, rarity);

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
            ? field.ResourcePotential * weights.OasisNaturalResource +
                field.Traversability * weights.OasisNaturalTraversability +
                field.River * weights.OasisNaturalRiver +
                field.ScenicPotential * weights.OasisNaturalScenic +
                rarity * weights.OasisNaturalRarity
            : strategicOasisSite
            ? warmDryWaterAccess * weights.OasisStrategicWaterAccess +
                field.ResourcePotential * weights.OasisStrategicResource +
                field.Traversability * weights.OasisStrategicTraversability +
                field.ScenicPotential * weights.OasisStrategicScenic +
                rarity * weights.OasisStrategicRarity
            : 0.0f;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.Oasis, oasisScore, thresholds.Oasis, field, gridX, gridY, rarity);
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
            Mathf.Clamp(score + rarity * TerrainWorldPlannerRules.PoiScoring.CandidateRarityLift, 0.0f, 1.0f),
            field.Height,
            field.ScenicPotential,
            field.Traversability,
            field.ResourcePotential,
            field.River,
            field.BiomeKind,
            field.LandscapeKind));
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
}
