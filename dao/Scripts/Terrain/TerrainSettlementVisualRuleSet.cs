using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain;

/// <summary>Optional data-driven settlement and POI visual layout rule set for gateways and interior landmark dressing.</summary>
[GlobalClass]
public partial class TerrainSettlementVisualRuleSet : Resource
{
    [ExportGroup("Interior Counts")]
    [Export(PropertyHint.Range, "0,64,1")] public int VillageInteriorCount { get; set; } = 9;
    [Export(PropertyHint.Range, "0,64,1")] public int TownInteriorCount { get; set; } = 17;
    [Export(PropertyHint.Range, "0,64,1")] public int OasisHubInteriorCount { get; set; } = 13;

    [ExportGroup("POI Landmark Base Scales")]
    [Export(PropertyHint.Range, "0,16,0.01")] public float SettlementLandmarkBaseScale { get; set; } = 7.8f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float VistaLandmarkBaseScale { get; set; } = 6.6f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float RiverCrossingLandmarkBaseScale { get; set; } = 6.2f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float MountainPassLandmarkBaseScale { get; set; } = 7.0f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float CoastalLandingLandmarkBaseScale { get; set; } = 7.4f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float ResourceGroveLandmarkBaseScale { get; set; } = 6.8f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float CanyonOverlookLandmarkBaseScale { get; set; } = 7.2f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float OasisLandmarkBaseScale { get; set; } = 7.6f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float VillageLandmarkBaseScale { get; set; } = 8.4f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float TownLandmarkBaseScale { get; set; } = 10.8f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float OasisHubLandmarkBaseScale { get; set; } = 9.4f;

    [ExportGroup("Gateway Tier Scale")]
    [Export(PropertyHint.Range, "0,8,0.01")] public float DefaultGatewayTierScale { get; set; } = 2.40f;
    [Export(PropertyHint.Range, "0,8,0.01")] public float VillageGatewayTierScale { get; set; } = 2.55f;
    [Export(PropertyHint.Range, "0,8,0.01")] public float TownGatewayTierScale { get; set; } = 3.20f;
    [Export(PropertyHint.Range, "0,8,0.01")] public float OasisHubGatewayTierScale { get; set; } = 2.90f;

    [ExportGroup("Gateway Route Scale")]
    [Export(PropertyHint.Range, "0,4,0.01")] public float DefaultGatewayRouteScale { get; set; } = 1.0f;
    [Export(PropertyHint.Range, "0,4,0.01")] public float PrimaryTrailGatewayRouteScale { get; set; } = 1.08f;
    [Export(PropertyHint.Range, "0,4,0.01")] public float RiverRoadGatewayRouteScale { get; set; } = 1.04f;
    [Export(PropertyHint.Range, "0,4,0.01")] public float CoastalPathGatewayRouteScale { get; set; } = 1.02f;

    [ExportGroup("Gateway Base Colors")]
    [Export] public Color DefaultGatewayBaseColor { get; set; } = new(0.50f, 0.40f, 0.26f);
    [Export] public Color VillageGatewayBaseColor { get; set; } = new(0.62f, 0.48f, 0.28f);
    [Export] public Color TownGatewayBaseColor { get; set; } = new(0.68f, 0.42f, 0.25f);
    [Export] public Color OasisHubGatewayBaseColor { get; set; } = new(0.16f, 0.58f, 0.42f);

    [ExportGroup("Gateway Route Tints")]
    [Export] public Color DefaultGatewayRouteTint { get; set; } = new(0.56f, 0.42f, 0.26f);
    [Export] public Color RiverRoadGatewayRouteTint { get; set; } = new(0.38f, 0.48f, 0.38f);
    [Export] public Color CoastalPathGatewayRouteTint { get; set; } = new(0.58f, 0.56f, 0.40f);
    [Export] public Color RidgePassGatewayRouteTint { get; set; } = new(0.54f, 0.52f, 0.46f);
    [Export] public Color ScenicTrailGatewayRouteTint { get; set; } = new(0.72f, 0.54f, 0.28f);

    [ExportGroup("Interior Base Scales")]
    [Export(PropertyHint.Range, "0,8,0.01")] public float VillageInteriorBaseScale { get; set; } = 2.30f;
    [Export(PropertyHint.Range, "0,8,0.01")] public float TownInteriorBaseScale { get; set; } = 2.95f;
    [Export(PropertyHint.Range, "0,8,0.01")] public float OasisHubInteriorBaseScale { get; set; } = 2.55f;
    [Export(PropertyHint.Range, "0,8,0.01")] public float VillageWellBaseScale { get; set; } = 1.95f;
    [Export(PropertyHint.Range, "0,8,0.01")] public float MarketStallBaseScale { get; set; } = 2.18f;
    [Export(PropertyHint.Range, "0,8,0.01")] public float WatchTowerBaseScale { get; set; } = 3.25f;
    [Export(PropertyHint.Range, "0,8,0.01")] public float OasisGardenBaseScale { get; set; } = 2.45f;

    [ExportGroup("POI Landmark Base Colors")]
    [Export] public Color SettlementLandmarkBaseColor { get; set; } = new(0.70f, 0.52f, 0.32f);
    [Export] public Color VistaLandmarkBaseColor { get; set; } = new(0.86f, 0.74f, 0.30f);
    [Export] public Color RiverCrossingLandmarkBaseColor { get; set; } = new(0.42f, 0.48f, 0.45f);
    [Export] public Color MountainPassLandmarkBaseColor { get; set; } = new(0.56f, 0.54f, 0.62f);
    [Export] public Color CoastalLandingLandmarkBaseColor { get; set; } = new(0.46f, 0.58f, 0.64f);
    [Export] public Color ResourceGroveLandmarkBaseColor { get; set; } = new(0.28f, 0.54f, 0.28f);
    [Export] public Color CanyonOverlookLandmarkBaseColor { get; set; } = new(0.66f, 0.38f, 0.24f);
    [Export] public Color OasisLandmarkBaseColor { get; set; } = new(0.18f, 0.58f, 0.42f);
    [Export] public Color VillageLandmarkBaseColor { get; set; } = new(0.74f, 0.56f, 0.30f);
    [Export] public Color TownLandmarkBaseColor { get; set; } = new(0.78f, 0.44f, 0.24f);
    [Export] public Color OasisHubLandmarkBaseColor { get; set; } = new(0.16f, 0.66f, 0.50f);

    [ExportGroup("Interior Variation Colors")]
    [Export] public Color DefaultInteriorVariationColor { get; set; } = new(0.58f, 0.48f, 0.31f);
    [Export] public Color TownBlockVariationColor { get; set; } = new(0.62f, 0.42f, 0.30f);
    [Export] public Color OasisCanopyVariationColor { get; set; } = new(0.12f, 0.58f, 0.44f);
    [Export] public Color SettlementPlazaVariationColor { get; set; } = new(0.58f, 0.50f, 0.38f);
    [Export] public Color OasisPoolVariationColor { get; set; } = new(0.10f, 0.36f, 0.46f);
    [Export] public Color VillageWellVariationColor { get; set; } = new(0.38f, 0.46f, 0.42f);
    [Export] public Color MarketStallVariationColor { get; set; } = new(0.74f, 0.48f, 0.24f);
    [Export] public Color WatchTowerVariationColor { get; set; } = new(0.54f, 0.42f, 0.28f);
    [Export] public Color OasisGardenVariationColor { get; set; } = new(0.12f, 0.62f, 0.34f);

    public string StableHash()
    {
        return ComputeHash(CreateSnapshot());
    }

    internal TerrainSettlementVisualRuleSetSnapshot CreateSnapshot()
    {
        return new TerrainSettlementVisualRuleSetSnapshot(
            VillageInteriorCount,
            TownInteriorCount,
            OasisHubInteriorCount,
            SettlementLandmarkBaseScale,
            VistaLandmarkBaseScale,
            RiverCrossingLandmarkBaseScale,
            MountainPassLandmarkBaseScale,
            CoastalLandingLandmarkBaseScale,
            ResourceGroveLandmarkBaseScale,
            CanyonOverlookLandmarkBaseScale,
            OasisLandmarkBaseScale,
            VillageLandmarkBaseScale,
            TownLandmarkBaseScale,
            OasisHubLandmarkBaseScale,
            DefaultGatewayTierScale,
            VillageGatewayTierScale,
            TownGatewayTierScale,
            OasisHubGatewayTierScale,
            DefaultGatewayRouteScale,
            PrimaryTrailGatewayRouteScale,
            RiverRoadGatewayRouteScale,
            CoastalPathGatewayRouteScale,
            DefaultGatewayBaseColor,
            VillageGatewayBaseColor,
            TownGatewayBaseColor,
            OasisHubGatewayBaseColor,
            DefaultGatewayRouteTint,
            RiverRoadGatewayRouteTint,
            CoastalPathGatewayRouteTint,
            RidgePassGatewayRouteTint,
            ScenicTrailGatewayRouteTint,
            VillageInteriorBaseScale,
            TownInteriorBaseScale,
            OasisHubInteriorBaseScale,
            VillageWellBaseScale,
            MarketStallBaseScale,
            WatchTowerBaseScale,
            OasisGardenBaseScale,
            SettlementLandmarkBaseColor,
            VistaLandmarkBaseColor,
            RiverCrossingLandmarkBaseColor,
            MountainPassLandmarkBaseColor,
            CoastalLandingLandmarkBaseColor,
            ResourceGroveLandmarkBaseColor,
            CanyonOverlookLandmarkBaseColor,
            OasisLandmarkBaseColor,
            VillageLandmarkBaseColor,
            TownLandmarkBaseColor,
            OasisHubLandmarkBaseColor,
            DefaultInteriorVariationColor,
            TownBlockVariationColor,
            OasisCanopyVariationColor,
            SettlementPlazaVariationColor,
            OasisPoolVariationColor,
            VillageWellVariationColor,
            MarketStallVariationColor,
            WatchTowerVariationColor,
            OasisGardenVariationColor);
    }

    internal static string ComputeHash(TerrainSettlementVisualRuleSetSnapshot snapshot)
    {
        var builder = new StringBuilder(2048);
        Append(builder, nameof(snapshot.VillageInteriorCount), snapshot.VillageInteriorCount);
        Append(builder, nameof(snapshot.TownInteriorCount), snapshot.TownInteriorCount);
        Append(builder, nameof(snapshot.OasisHubInteriorCount), snapshot.OasisHubInteriorCount);
        Append(builder, nameof(snapshot.SettlementLandmarkBaseScale), snapshot.SettlementLandmarkBaseScale);
        Append(builder, nameof(snapshot.VistaLandmarkBaseScale), snapshot.VistaLandmarkBaseScale);
        Append(builder, nameof(snapshot.RiverCrossingLandmarkBaseScale), snapshot.RiverCrossingLandmarkBaseScale);
        Append(builder, nameof(snapshot.MountainPassLandmarkBaseScale), snapshot.MountainPassLandmarkBaseScale);
        Append(builder, nameof(snapshot.CoastalLandingLandmarkBaseScale), snapshot.CoastalLandingLandmarkBaseScale);
        Append(builder, nameof(snapshot.ResourceGroveLandmarkBaseScale), snapshot.ResourceGroveLandmarkBaseScale);
        Append(builder, nameof(snapshot.CanyonOverlookLandmarkBaseScale), snapshot.CanyonOverlookLandmarkBaseScale);
        Append(builder, nameof(snapshot.OasisLandmarkBaseScale), snapshot.OasisLandmarkBaseScale);
        Append(builder, nameof(snapshot.VillageLandmarkBaseScale), snapshot.VillageLandmarkBaseScale);
        Append(builder, nameof(snapshot.TownLandmarkBaseScale), snapshot.TownLandmarkBaseScale);
        Append(builder, nameof(snapshot.OasisHubLandmarkBaseScale), snapshot.OasisHubLandmarkBaseScale);
        Append(builder, nameof(snapshot.DefaultGatewayTierScale), snapshot.DefaultGatewayTierScale);
        Append(builder, nameof(snapshot.VillageGatewayTierScale), snapshot.VillageGatewayTierScale);
        Append(builder, nameof(snapshot.TownGatewayTierScale), snapshot.TownGatewayTierScale);
        Append(builder, nameof(snapshot.OasisHubGatewayTierScale), snapshot.OasisHubGatewayTierScale);
        Append(builder, nameof(snapshot.DefaultGatewayRouteScale), snapshot.DefaultGatewayRouteScale);
        Append(builder, nameof(snapshot.PrimaryTrailGatewayRouteScale), snapshot.PrimaryTrailGatewayRouteScale);
        Append(builder, nameof(snapshot.RiverRoadGatewayRouteScale), snapshot.RiverRoadGatewayRouteScale);
        Append(builder, nameof(snapshot.CoastalPathGatewayRouteScale), snapshot.CoastalPathGatewayRouteScale);
        Append(builder, nameof(snapshot.DefaultGatewayBaseColor), snapshot.DefaultGatewayBaseColor);
        Append(builder, nameof(snapshot.VillageGatewayBaseColor), snapshot.VillageGatewayBaseColor);
        Append(builder, nameof(snapshot.TownGatewayBaseColor), snapshot.TownGatewayBaseColor);
        Append(builder, nameof(snapshot.OasisHubGatewayBaseColor), snapshot.OasisHubGatewayBaseColor);
        Append(builder, nameof(snapshot.DefaultGatewayRouteTint), snapshot.DefaultGatewayRouteTint);
        Append(builder, nameof(snapshot.RiverRoadGatewayRouteTint), snapshot.RiverRoadGatewayRouteTint);
        Append(builder, nameof(snapshot.CoastalPathGatewayRouteTint), snapshot.CoastalPathGatewayRouteTint);
        Append(builder, nameof(snapshot.RidgePassGatewayRouteTint), snapshot.RidgePassGatewayRouteTint);
        Append(builder, nameof(snapshot.ScenicTrailGatewayRouteTint), snapshot.ScenicTrailGatewayRouteTint);
        Append(builder, nameof(snapshot.VillageInteriorBaseScale), snapshot.VillageInteriorBaseScale);
        Append(builder, nameof(snapshot.TownInteriorBaseScale), snapshot.TownInteriorBaseScale);
        Append(builder, nameof(snapshot.OasisHubInteriorBaseScale), snapshot.OasisHubInteriorBaseScale);
        Append(builder, nameof(snapshot.VillageWellBaseScale), snapshot.VillageWellBaseScale);
        Append(builder, nameof(snapshot.MarketStallBaseScale), snapshot.MarketStallBaseScale);
        Append(builder, nameof(snapshot.WatchTowerBaseScale), snapshot.WatchTowerBaseScale);
        Append(builder, nameof(snapshot.OasisGardenBaseScale), snapshot.OasisGardenBaseScale);
        Append(builder, nameof(snapshot.SettlementLandmarkBaseColor), snapshot.SettlementLandmarkBaseColor);
        Append(builder, nameof(snapshot.VistaLandmarkBaseColor), snapshot.VistaLandmarkBaseColor);
        Append(builder, nameof(snapshot.RiverCrossingLandmarkBaseColor), snapshot.RiverCrossingLandmarkBaseColor);
        Append(builder, nameof(snapshot.MountainPassLandmarkBaseColor), snapshot.MountainPassLandmarkBaseColor);
        Append(builder, nameof(snapshot.CoastalLandingLandmarkBaseColor), snapshot.CoastalLandingLandmarkBaseColor);
        Append(builder, nameof(snapshot.ResourceGroveLandmarkBaseColor), snapshot.ResourceGroveLandmarkBaseColor);
        Append(builder, nameof(snapshot.CanyonOverlookLandmarkBaseColor), snapshot.CanyonOverlookLandmarkBaseColor);
        Append(builder, nameof(snapshot.OasisLandmarkBaseColor), snapshot.OasisLandmarkBaseColor);
        Append(builder, nameof(snapshot.VillageLandmarkBaseColor), snapshot.VillageLandmarkBaseColor);
        Append(builder, nameof(snapshot.TownLandmarkBaseColor), snapshot.TownLandmarkBaseColor);
        Append(builder, nameof(snapshot.OasisHubLandmarkBaseColor), snapshot.OasisHubLandmarkBaseColor);
        Append(builder, nameof(snapshot.DefaultInteriorVariationColor), snapshot.DefaultInteriorVariationColor);
        Append(builder, nameof(snapshot.TownBlockVariationColor), snapshot.TownBlockVariationColor);
        Append(builder, nameof(snapshot.OasisCanopyVariationColor), snapshot.OasisCanopyVariationColor);
        Append(builder, nameof(snapshot.SettlementPlazaVariationColor), snapshot.SettlementPlazaVariationColor);
        Append(builder, nameof(snapshot.OasisPoolVariationColor), snapshot.OasisPoolVariationColor);
        Append(builder, nameof(snapshot.VillageWellVariationColor), snapshot.VillageWellVariationColor);
        Append(builder, nameof(snapshot.MarketStallVariationColor), snapshot.MarketStallVariationColor);
        Append(builder, nameof(snapshot.WatchTowerVariationColor), snapshot.WatchTowerVariationColor);
        Append(builder, nameof(snapshot.OasisGardenVariationColor), snapshot.OasisGardenVariationColor);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string name, int value)
    {
        builder.Append(name).Append('=').Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');
    }

    private static void Append(StringBuilder builder, string name, float value)
    {
        builder.Append(name).Append('=').Append(value.ToString("G9", CultureInfo.InvariantCulture)).Append(';');
    }

    private static void Append(StringBuilder builder, string name, Color value)
    {
        builder.Append(name).Append('=').Append(value.ToHtml()).Append(';');
    }
}
