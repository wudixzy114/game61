using Godot;

namespace Dao.Terrain.Generation;

internal readonly record struct TerrainSettlementVisualRuleSetSnapshot(
    int VillageInteriorCount,
    int TownInteriorCount,
    int OasisHubInteriorCount,
    float SettlementLandmarkBaseScale,
    float VistaLandmarkBaseScale,
    float RiverCrossingLandmarkBaseScale,
    float MountainPassLandmarkBaseScale,
    float CoastalLandingLandmarkBaseScale,
    float ResourceGroveLandmarkBaseScale,
    float CanyonOverlookLandmarkBaseScale,
    float OasisLandmarkBaseScale,
    float VillageLandmarkBaseScale,
    float TownLandmarkBaseScale,
    float OasisHubLandmarkBaseScale,
    float DefaultGatewayTierScale,
    float VillageGatewayTierScale,
    float TownGatewayTierScale,
    float OasisHubGatewayTierScale,
    float DefaultGatewayRouteScale,
    float PrimaryTrailGatewayRouteScale,
    float RiverRoadGatewayRouteScale,
    float CoastalPathGatewayRouteScale,
    Color DefaultGatewayBaseColor,
    Color VillageGatewayBaseColor,
    Color TownGatewayBaseColor,
    Color OasisHubGatewayBaseColor,
    Color DefaultGatewayRouteTint,
    Color RiverRoadGatewayRouteTint,
    Color CoastalPathGatewayRouteTint,
    Color RidgePassGatewayRouteTint,
    Color ScenicTrailGatewayRouteTint,
    float VillageInteriorBaseScale,
    float TownInteriorBaseScale,
    float OasisHubInteriorBaseScale,
    float VillageWellBaseScale,
    float MarketStallBaseScale,
    float WatchTowerBaseScale,
    float OasisGardenBaseScale,
    Color SettlementLandmarkBaseColor,
    Color VistaLandmarkBaseColor,
    Color RiverCrossingLandmarkBaseColor,
    Color MountainPassLandmarkBaseColor,
    Color CoastalLandingLandmarkBaseColor,
    Color ResourceGroveLandmarkBaseColor,
    Color CanyonOverlookLandmarkBaseColor,
    Color OasisLandmarkBaseColor,
    Color VillageLandmarkBaseColor,
    Color TownLandmarkBaseColor,
    Color OasisHubLandmarkBaseColor,
    Color DefaultInteriorVariationColor,
    Color TownBlockVariationColor,
    Color OasisCanopyVariationColor,
    Color SettlementPlazaVariationColor,
    Color OasisPoolVariationColor,
    Color VillageWellVariationColor,
    Color MarketStallVariationColor,
    Color WatchTowerVariationColor,
    Color OasisGardenVariationColor)
{
    public string StableHash()
    {
        return Dao.Terrain.TerrainSettlementVisualRuleSet.ComputeHash(this);
    }

    public int InteriorCount(TerrainSettlementTier tier)
    {
        return tier switch
        {
            TerrainSettlementTier.Town => TownInteriorCount,
            TerrainSettlementTier.OasisHub => OasisHubInteriorCount,
            _ => VillageInteriorCount
        };
    }

    public float GatewayTierScale(TerrainSettlementTier tier)
    {
        return tier switch
        {
            TerrainSettlementTier.Town => TownGatewayTierScale,
            TerrainSettlementTier.OasisHub => OasisHubGatewayTierScale,
            TerrainSettlementTier.Village => VillageGatewayTierScale,
            _ => DefaultGatewayTierScale
        };
    }

    public float GatewayRouteScale(TerrainRouteKind routeKind)
    {
        return routeKind switch
        {
            TerrainRouteKind.PrimaryTrail => PrimaryTrailGatewayRouteScale,
            TerrainRouteKind.RiverRoad => RiverRoadGatewayRouteScale,
            TerrainRouteKind.CoastalPath => CoastalPathGatewayRouteScale,
            _ => DefaultGatewayRouteScale
        };
    }

    public Color GatewayBaseColor(TerrainSettlementTier tier)
    {
        return tier switch
        {
            TerrainSettlementTier.Town => TownGatewayBaseColor,
            TerrainSettlementTier.OasisHub => OasisHubGatewayBaseColor,
            TerrainSettlementTier.Village => VillageGatewayBaseColor,
            _ => DefaultGatewayBaseColor
        };
    }

    public Color GatewayRouteTint(TerrainRouteKind routeKind)
    {
        return routeKind switch
        {
            TerrainRouteKind.RiverRoad => RiverRoadGatewayRouteTint,
            TerrainRouteKind.CoastalPath => CoastalPathGatewayRouteTint,
            TerrainRouteKind.RidgePass => RidgePassGatewayRouteTint,
            TerrainRouteKind.ScenicTrail => ScenicTrailGatewayRouteTint,
            _ => DefaultGatewayRouteTint
        };
    }

    public float InteriorBaseScale(TerrainSettlementTier tier, TerrainLandmarkKind kind)
    {
        return kind switch
        {
            TerrainLandmarkKind.VillageWell => VillageWellBaseScale,
            TerrainLandmarkKind.MarketStall => MarketStallBaseScale,
            TerrainLandmarkKind.WatchTower => WatchTowerBaseScale,
            TerrainLandmarkKind.OasisGarden => OasisGardenBaseScale,
            _ => tier switch
            {
                TerrainSettlementTier.Town => TownInteriorBaseScale,
                TerrainSettlementTier.OasisHub => OasisHubInteriorBaseScale,
                _ => VillageInteriorBaseScale
            }
        };
    }

    public float PointLandmarkBaseScale(TerrainLandmarkKind kind)
    {
        return kind switch
        {
            TerrainLandmarkKind.Settlement => SettlementLandmarkBaseScale,
            TerrainLandmarkKind.Vista => VistaLandmarkBaseScale,
            TerrainLandmarkKind.RiverCrossing => RiverCrossingLandmarkBaseScale,
            TerrainLandmarkKind.MountainPass => MountainPassLandmarkBaseScale,
            TerrainLandmarkKind.CoastalLanding => CoastalLandingLandmarkBaseScale,
            TerrainLandmarkKind.ResourceGrove => ResourceGroveLandmarkBaseScale,
            TerrainLandmarkKind.CanyonOverlook => CanyonOverlookLandmarkBaseScale,
            TerrainLandmarkKind.Oasis => OasisLandmarkBaseScale,
            TerrainLandmarkKind.Village => VillageLandmarkBaseScale,
            TerrainLandmarkKind.Town => TownLandmarkBaseScale,
            TerrainLandmarkKind.OasisHub => OasisHubLandmarkBaseScale,
            _ => 7.0f
        };
    }

    public Color InteriorVariationColor(TerrainLandmarkKind kind)
    {
        return kind switch
        {
            TerrainLandmarkKind.TownBlock => TownBlockVariationColor,
            TerrainLandmarkKind.OasisCanopy => OasisCanopyVariationColor,
            TerrainLandmarkKind.SettlementPlaza => SettlementPlazaVariationColor,
            TerrainLandmarkKind.OasisPool => OasisPoolVariationColor,
            TerrainLandmarkKind.VillageWell => VillageWellVariationColor,
            TerrainLandmarkKind.MarketStall => MarketStallVariationColor,
            TerrainLandmarkKind.WatchTower => WatchTowerVariationColor,
            TerrainLandmarkKind.OasisGarden => OasisGardenVariationColor,
            _ => DefaultInteriorVariationColor
        };
    }

    public Color PointLandmarkBaseColor(TerrainLandmarkKind kind)
    {
        return kind switch
        {
            TerrainLandmarkKind.Settlement => SettlementLandmarkBaseColor,
            TerrainLandmarkKind.Vista => VistaLandmarkBaseColor,
            TerrainLandmarkKind.RiverCrossing => RiverCrossingLandmarkBaseColor,
            TerrainLandmarkKind.MountainPass => MountainPassLandmarkBaseColor,
            TerrainLandmarkKind.CoastalLanding => CoastalLandingLandmarkBaseColor,
            TerrainLandmarkKind.ResourceGrove => ResourceGroveLandmarkBaseColor,
            TerrainLandmarkKind.CanyonOverlook => CanyonOverlookLandmarkBaseColor,
            TerrainLandmarkKind.Oasis => OasisLandmarkBaseColor,
            TerrainLandmarkKind.Village => VillageLandmarkBaseColor,
            TerrainLandmarkKind.Town => TownLandmarkBaseColor,
            TerrainLandmarkKind.OasisHub => OasisHubLandmarkBaseColor,
            _ => DefaultInteriorVariationColor
        };
    }
}

internal static class TerrainTileBuilderSettlementDefaults
{
    public static TerrainSettlementVisualRuleSetSnapshot CreateDefault()
    {
        return new TerrainSettlementVisualRuleSetSnapshot(
            VillageInteriorCount: 9,
            TownInteriorCount: 17,
            OasisHubInteriorCount: 13,
            SettlementLandmarkBaseScale: 7.8f,
            VistaLandmarkBaseScale: 6.6f,
            RiverCrossingLandmarkBaseScale: 6.2f,
            MountainPassLandmarkBaseScale: 7.0f,
            CoastalLandingLandmarkBaseScale: 7.4f,
            ResourceGroveLandmarkBaseScale: 6.8f,
            CanyonOverlookLandmarkBaseScale: 7.2f,
            OasisLandmarkBaseScale: 7.6f,
            VillageLandmarkBaseScale: 8.4f,
            TownLandmarkBaseScale: 10.8f,
            OasisHubLandmarkBaseScale: 9.4f,
            DefaultGatewayTierScale: 2.40f,
            VillageGatewayTierScale: 2.55f,
            TownGatewayTierScale: 3.20f,
            OasisHubGatewayTierScale: 2.90f,
            DefaultGatewayRouteScale: 1.0f,
            PrimaryTrailGatewayRouteScale: 1.08f,
            RiverRoadGatewayRouteScale: 1.04f,
            CoastalPathGatewayRouteScale: 1.02f,
            DefaultGatewayBaseColor: new Color(0.50f, 0.40f, 0.26f),
            VillageGatewayBaseColor: new Color(0.62f, 0.48f, 0.28f),
            TownGatewayBaseColor: new Color(0.68f, 0.42f, 0.25f),
            OasisHubGatewayBaseColor: new Color(0.16f, 0.58f, 0.42f),
            DefaultGatewayRouteTint: new Color(0.56f, 0.42f, 0.26f),
            RiverRoadGatewayRouteTint: new Color(0.38f, 0.48f, 0.38f),
            CoastalPathGatewayRouteTint: new Color(0.58f, 0.56f, 0.40f),
            RidgePassGatewayRouteTint: new Color(0.54f, 0.52f, 0.46f),
            ScenicTrailGatewayRouteTint: new Color(0.72f, 0.54f, 0.28f),
            VillageInteriorBaseScale: 2.30f,
            TownInteriorBaseScale: 2.95f,
            OasisHubInteriorBaseScale: 2.55f,
            VillageWellBaseScale: 1.95f,
            MarketStallBaseScale: 2.18f,
            WatchTowerBaseScale: 3.25f,
            OasisGardenBaseScale: 2.45f,
            SettlementLandmarkBaseColor: new Color(0.70f, 0.52f, 0.32f),
            VistaLandmarkBaseColor: new Color(0.86f, 0.74f, 0.30f),
            RiverCrossingLandmarkBaseColor: new Color(0.42f, 0.48f, 0.45f),
            MountainPassLandmarkBaseColor: new Color(0.56f, 0.54f, 0.62f),
            CoastalLandingLandmarkBaseColor: new Color(0.46f, 0.58f, 0.64f),
            ResourceGroveLandmarkBaseColor: new Color(0.28f, 0.54f, 0.28f),
            CanyonOverlookLandmarkBaseColor: new Color(0.66f, 0.38f, 0.24f),
            OasisLandmarkBaseColor: new Color(0.18f, 0.58f, 0.42f),
            VillageLandmarkBaseColor: new Color(0.74f, 0.56f, 0.30f),
            TownLandmarkBaseColor: new Color(0.78f, 0.44f, 0.24f),
            OasisHubLandmarkBaseColor: new Color(0.16f, 0.66f, 0.50f),
            DefaultInteriorVariationColor: new Color(0.58f, 0.48f, 0.31f),
            TownBlockVariationColor: new Color(0.62f, 0.42f, 0.30f),
            OasisCanopyVariationColor: new Color(0.12f, 0.58f, 0.44f),
            SettlementPlazaVariationColor: new Color(0.58f, 0.50f, 0.38f),
            OasisPoolVariationColor: new Color(0.10f, 0.36f, 0.46f),
            VillageWellVariationColor: new Color(0.38f, 0.46f, 0.42f),
            MarketStallVariationColor: new Color(0.74f, 0.48f, 0.24f),
            WatchTowerVariationColor: new Color(0.54f, 0.42f, 0.28f),
            OasisGardenVariationColor: new Color(0.12f, 0.62f, 0.34f));
    }
}

public static partial class TerrainTileBuilder
{
    private static TerrainSettlementVisualRuleSetSnapshot ResolveSettlementVisualRules(TerrainGenerationProfile profile)
    {
        return TerrainSettlementVisualRuleCatalog.Resolve(profile.SettlementVisualRuleSetHash);
    }
}
