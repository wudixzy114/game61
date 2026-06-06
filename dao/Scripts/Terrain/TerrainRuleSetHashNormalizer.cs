namespace Dao.Terrain;

internal static class TerrainRuleSetHashNormalizer
{
    public static string NormalizeScatterRuleSetHash(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Generation.TerrainScatterRuleCatalog.DefaultHash
            : value;
    }

    public static string NormalizeSettlementVisualRuleSetHash(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Generation.TerrainSettlementVisualRuleCatalog.DefaultHash
            : value;
    }

    public static string NormalizePointOfInterestRuleSetHash(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Generation.TerrainPointOfInterestRuleCatalog.DefaultHash
            : value;
    }

    public static string NormalizeRouteRuleSetHash(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Generation.TerrainRouteRuleCatalog.DefaultHash
            : value;
    }

    public static string NormalizeScenicRuleSetHash(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Generation.TerrainScenicLandmarkRuleCatalog.DefaultHash
            : value;
    }
}
