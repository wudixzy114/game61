using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static TerrainLandmarkKind LandmarkKindFor(TerrainWorldPointOfInterest point)
    {
        if (point.SettlementTier == TerrainSettlementTier.Town)
        {
            return TerrainLandmarkKind.Town;
        }

        if (point.SettlementTier == TerrainSettlementTier.Village)
        {
            return TerrainLandmarkKind.Village;
        }

        if (point.SettlementTier == TerrainSettlementTier.OasisHub)
        {
            return TerrainLandmarkKind.OasisHub;
        }

        return point.Kind switch
        {
            TerrainPointOfInterestKind.SettlementCandidate => TerrainLandmarkKind.Settlement,
            TerrainPointOfInterestKind.Vista => TerrainLandmarkKind.Vista,
            TerrainPointOfInterestKind.RiverCrossing => TerrainLandmarkKind.RiverCrossing,
            TerrainPointOfInterestKind.MountainPass => TerrainLandmarkKind.MountainPass,
            TerrainPointOfInterestKind.CoastalLanding => TerrainLandmarkKind.CoastalLanding,
            TerrainPointOfInterestKind.ResourceGrove => TerrainLandmarkKind.ResourceGrove,
            TerrainPointOfInterestKind.CanyonOverlook => TerrainLandmarkKind.CanyonOverlook,
            TerrainPointOfInterestKind.Oasis => TerrainLandmarkKind.Oasis,
            _ => TerrainLandmarkKind.AncientStone
        };
    }

    private static float LandmarkScaleFor(
        TerrainLandmarkKind kind,
        float score,
        TerrainSettlementVisualRuleSetSnapshot visualRules)
    {
        float quality = Mathf.Lerp(0.88f, 1.24f, Mathf.Clamp(score, 0.0f, 1.0f));
        float baseScale = kind switch
        {
            TerrainLandmarkKind.Settlement => visualRules.SettlementLandmarkBaseScale,
            TerrainLandmarkKind.Vista => visualRules.VistaLandmarkBaseScale,
            TerrainLandmarkKind.RiverCrossing => visualRules.RiverCrossingLandmarkBaseScale,
            TerrainLandmarkKind.MountainPass => visualRules.MountainPassLandmarkBaseScale,
            TerrainLandmarkKind.CoastalLanding => visualRules.CoastalLandingLandmarkBaseScale,
            TerrainLandmarkKind.ResourceGrove => visualRules.ResourceGroveLandmarkBaseScale,
            TerrainLandmarkKind.CanyonOverlook => visualRules.CanyonOverlookLandmarkBaseScale,
            TerrainLandmarkKind.Oasis => visualRules.OasisLandmarkBaseScale,
            TerrainLandmarkKind.Village => visualRules.VillageLandmarkBaseScale,
            TerrainLandmarkKind.Town => visualRules.TownLandmarkBaseScale,
            TerrainLandmarkKind.OasisHub => visualRules.OasisHubLandmarkBaseScale,
            TerrainLandmarkKind.VillageWell => visualRules.VillageWellBaseScale,
            TerrainLandmarkKind.MarketStall => visualRules.MarketStallBaseScale,
            TerrainLandmarkKind.WatchTower => visualRules.WatchTowerBaseScale,
            TerrainLandmarkKind.OasisGarden => visualRules.OasisGardenBaseScale,
            TerrainLandmarkKind.VillageHouse => 2.6f,
            TerrainLandmarkKind.TownBlock => 3.4f,
            TerrainLandmarkKind.OasisCanopy => 3.0f,
            TerrainLandmarkKind.SettlementPlaza => 3.2f,
            TerrainLandmarkKind.OasisPool => 3.4f,
            TerrainLandmarkKind.SettlementGateway => 3.0f,
            TerrainLandmarkKind.Waterfall => 7.2f,
            TerrainLandmarkKind.RoadMarker => 2.0f,
            TerrainLandmarkKind.BridgeSpan => 4.4f,
            TerrainLandmarkKind.DuneCrest => 5.4f,
            TerrainLandmarkKind.DesertMonolith => 5.8f,
            TerrainLandmarkKind.CanyonNeedle => 6.2f,
            TerrainLandmarkKind.IceSpire => 5.6f,
            TerrainLandmarkKind.NaturalArch => 6.0f,
            TerrainLandmarkKind.GeothermalSpring => 5.2f,
            TerrainLandmarkKind.GlacialRidge => 5.8f,
            _ => 7.0f
        };

        return baseScale * quality;
    }

    private static Color LandmarkColorFor(
        TerrainLandmarkKind kind,
        TerrainWorldField field,
        TerrainSettlementVisualRuleSetSnapshot visualRules)
    {
        Color baseColor = kind switch
        {
            TerrainLandmarkKind.Settlement => visualRules.SettlementLandmarkBaseColor,
            TerrainLandmarkKind.Vista => visualRules.VistaLandmarkBaseColor,
            TerrainLandmarkKind.RiverCrossing => visualRules.RiverCrossingLandmarkBaseColor,
            TerrainLandmarkKind.MountainPass => visualRules.MountainPassLandmarkBaseColor,
            TerrainLandmarkKind.CoastalLanding => visualRules.CoastalLandingLandmarkBaseColor,
            TerrainLandmarkKind.ResourceGrove => visualRules.ResourceGroveLandmarkBaseColor,
            TerrainLandmarkKind.CanyonOverlook => visualRules.CanyonOverlookLandmarkBaseColor,
            TerrainLandmarkKind.Oasis => visualRules.OasisLandmarkBaseColor,
            TerrainLandmarkKind.Village => visualRules.VillageLandmarkBaseColor,
            TerrainLandmarkKind.Town => visualRules.TownLandmarkBaseColor,
            TerrainLandmarkKind.OasisHub => visualRules.OasisHubLandmarkBaseColor,
            TerrainLandmarkKind.VillageHouse => new Color(0.68f, 0.54f, 0.34f),
            TerrainLandmarkKind.TownBlock => new Color(0.72f, 0.46f, 0.31f),
            TerrainLandmarkKind.OasisCanopy => new Color(0.14f, 0.58f, 0.42f),
            TerrainLandmarkKind.SettlementPlaza => new Color(0.62f, 0.54f, 0.40f),
            TerrainLandmarkKind.OasisPool => new Color(0.08f, 0.34f, 0.46f),
            TerrainLandmarkKind.VillageWell => new Color(0.34f, 0.42f, 0.40f),
            TerrainLandmarkKind.MarketStall => new Color(0.76f, 0.45f, 0.22f),
            TerrainLandmarkKind.WatchTower => new Color(0.58f, 0.44f, 0.28f),
            TerrainLandmarkKind.OasisGarden => new Color(0.12f, 0.58f, 0.32f),
            TerrainLandmarkKind.SettlementGateway => new Color(0.64f, 0.46f, 0.28f),
            TerrainLandmarkKind.Waterfall => new Color(0.30f, 0.62f, 0.82f),
            TerrainLandmarkKind.RoadMarker => new Color(0.56f, 0.44f, 0.28f),
            TerrainLandmarkKind.BridgeSpan => new Color(0.44f, 0.34f, 0.25f),
            TerrainLandmarkKind.DuneCrest => new Color(0.76f, 0.58f, 0.30f),
            TerrainLandmarkKind.DesertMonolith => new Color(0.62f, 0.42f, 0.24f),
            TerrainLandmarkKind.CanyonNeedle => new Color(0.58f, 0.36f, 0.24f),
            TerrainLandmarkKind.IceSpire => new Color(0.62f, 0.76f, 0.86f),
            TerrainLandmarkKind.NaturalArch => new Color(0.66f, 0.44f, 0.28f),
            TerrainLandmarkKind.GeothermalSpring => new Color(0.24f, 0.58f, 0.62f),
            TerrainLandmarkKind.GlacialRidge => new Color(0.70f, 0.82f, 0.88f),
            _ => new Color(0.52f, 0.50f, 0.44f)
        };

        baseColor = kind switch
        {
            TerrainLandmarkKind.VillageWell => visualRules.VillageWellVariationColor,
            TerrainLandmarkKind.MarketStall => visualRules.MarketStallVariationColor,
            TerrainLandmarkKind.WatchTower => visualRules.WatchTowerVariationColor,
            TerrainLandmarkKind.OasisGarden => visualRules.OasisGardenVariationColor,
            TerrainLandmarkKind.TownBlock => visualRules.TownBlockVariationColor,
            TerrainLandmarkKind.OasisCanopy => visualRules.OasisCanopyVariationColor,
            TerrainLandmarkKind.SettlementPlaza => visualRules.SettlementPlazaVariationColor,
            TerrainLandmarkKind.OasisPool => visualRules.OasisPoolVariationColor,
            _ => baseColor
        };

        return baseColor.Lerp(Colors.White, Mathf.Clamp(field.ScenicPotential * 0.12f, 0.0f, 0.12f));
    }
}
