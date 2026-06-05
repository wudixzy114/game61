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

    private static float LandmarkScaleFor(TerrainLandmarkKind kind, float score)
    {
        float quality = Mathf.Lerp(0.88f, 1.24f, Mathf.Clamp(score, 0.0f, 1.0f));
        float baseScale = kind switch
        {
            TerrainLandmarkKind.Settlement => 7.8f,
            TerrainLandmarkKind.Vista => 6.6f,
            TerrainLandmarkKind.RiverCrossing => 6.2f,
            TerrainLandmarkKind.MountainPass => 7.0f,
            TerrainLandmarkKind.CoastalLanding => 7.4f,
            TerrainLandmarkKind.ResourceGrove => 6.8f,
            TerrainLandmarkKind.CanyonOverlook => 7.2f,
            TerrainLandmarkKind.Oasis => 7.6f,
            TerrainLandmarkKind.Village => 8.4f,
            TerrainLandmarkKind.Town => 10.8f,
            TerrainLandmarkKind.OasisHub => 9.4f,
            TerrainLandmarkKind.VillageHouse => 2.6f,
            TerrainLandmarkKind.TownBlock => 3.4f,
            TerrainLandmarkKind.OasisCanopy => 3.0f,
            TerrainLandmarkKind.SettlementPlaza => 3.2f,
            TerrainLandmarkKind.OasisPool => 3.4f,
            TerrainLandmarkKind.VillageWell => 2.4f,
            TerrainLandmarkKind.MarketStall => 2.7f,
            TerrainLandmarkKind.WatchTower => 4.2f,
            TerrainLandmarkKind.OasisGarden => 3.0f,
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

    private static Color LandmarkColorFor(TerrainLandmarkKind kind, TerrainWorldField field)
    {
        Color baseColor = kind switch
        {
            TerrainLandmarkKind.Settlement => new Color(0.70f, 0.52f, 0.32f),
            TerrainLandmarkKind.Vista => new Color(0.86f, 0.74f, 0.30f),
            TerrainLandmarkKind.RiverCrossing => new Color(0.42f, 0.48f, 0.45f),
            TerrainLandmarkKind.MountainPass => new Color(0.56f, 0.54f, 0.62f),
            TerrainLandmarkKind.CoastalLanding => new Color(0.46f, 0.58f, 0.64f),
            TerrainLandmarkKind.ResourceGrove => new Color(0.28f, 0.54f, 0.28f),
            TerrainLandmarkKind.CanyonOverlook => new Color(0.66f, 0.38f, 0.24f),
            TerrainLandmarkKind.Oasis => new Color(0.18f, 0.58f, 0.42f),
            TerrainLandmarkKind.Village => new Color(0.74f, 0.56f, 0.30f),
            TerrainLandmarkKind.Town => new Color(0.78f, 0.44f, 0.24f),
            TerrainLandmarkKind.OasisHub => new Color(0.16f, 0.66f, 0.50f),
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

        return baseColor.Lerp(Colors.White, Mathf.Clamp(field.ScenicPotential * 0.12f, 0.0f, 0.12f));
    }
}
