using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static class TerrainSettlementRules
    {
        public static int InteriorCount(TerrainSettlementTier tier)
        {
            return tier switch
            {
                TerrainSettlementTier.Town => 17,
                TerrainSettlementTier.OasisHub => 13,
                _ => 9
            };
        }

        public static float GatewayTierScale(TerrainSettlementTier tier)
        {
            return tier switch
            {
                TerrainSettlementTier.Town => 3.20f,
                TerrainSettlementTier.OasisHub => 2.90f,
                TerrainSettlementTier.Village => 2.55f,
                _ => 2.40f
            };
        }

        public static float GatewayRouteScale(TerrainRouteKind routeKind)
        {
            return routeKind switch
            {
                TerrainRouteKind.PrimaryTrail => 1.08f,
                TerrainRouteKind.RiverRoad => 1.04f,
                TerrainRouteKind.CoastalPath => 1.02f,
                _ => 1.0f
            };
        }

        public static Color GatewayBaseColor(TerrainSettlementTier tier)
        {
            return tier switch
            {
                TerrainSettlementTier.Town => new Color(0.68f, 0.42f, 0.25f),
                TerrainSettlementTier.OasisHub => new Color(0.16f, 0.58f, 0.42f),
                TerrainSettlementTier.Village => new Color(0.62f, 0.48f, 0.28f),
                _ => new Color(0.50f, 0.40f, 0.26f)
            };
        }

        public static Color GatewayRouteTint(TerrainRouteKind routeKind)
        {
            return routeKind switch
            {
                TerrainRouteKind.RiverRoad => new Color(0.38f, 0.48f, 0.38f),
                TerrainRouteKind.CoastalPath => new Color(0.58f, 0.56f, 0.40f),
                TerrainRouteKind.RidgePass => new Color(0.54f, 0.52f, 0.46f),
                TerrainRouteKind.ScenicTrail => new Color(0.72f, 0.54f, 0.28f),
                _ => new Color(0.56f, 0.42f, 0.26f)
            };
        }

        public static float InteriorBaseScale(TerrainSettlementTier tier, TerrainLandmarkKind kind)
        {
            return kind switch
            {
                TerrainLandmarkKind.VillageWell => 1.95f,
                TerrainLandmarkKind.MarketStall => 2.18f,
                TerrainLandmarkKind.WatchTower => 3.25f,
                TerrainLandmarkKind.OasisGarden => 2.45f,
                _ => tier switch
                {
                    TerrainSettlementTier.Town => 2.95f,
                    TerrainSettlementTier.OasisHub => 2.55f,
                    _ => 2.30f
                }
            };
        }

        public static Color InteriorVariationColor(TerrainLandmarkKind kind)
        {
            return kind switch
            {
                TerrainLandmarkKind.TownBlock => new Color(0.62f, 0.42f, 0.30f),
                TerrainLandmarkKind.OasisCanopy => new Color(0.12f, 0.58f, 0.44f),
                TerrainLandmarkKind.SettlementPlaza => new Color(0.58f, 0.50f, 0.38f),
                TerrainLandmarkKind.OasisPool => new Color(0.10f, 0.36f, 0.46f),
                TerrainLandmarkKind.VillageWell => new Color(0.38f, 0.46f, 0.42f),
                TerrainLandmarkKind.MarketStall => new Color(0.74f, 0.48f, 0.24f),
                TerrainLandmarkKind.WatchTower => new Color(0.54f, 0.42f, 0.28f),
                TerrainLandmarkKind.OasisGarden => new Color(0.12f, 0.62f, 0.34f),
                _ => new Color(0.58f, 0.48f, 0.31f)
            };
        }
    }
}
