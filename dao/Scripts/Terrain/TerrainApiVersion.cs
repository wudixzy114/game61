namespace Dao.Terrain;

/// <summary>Stable terrain runtime API contract version used by reports and validation tools.</summary>
public static class TerrainApiVersion
{
    public const int Major = 1;
    public const int Minor = 7;
    public const int Patch = 0;
    public const string Contract = "terrain-api-v1";
    public const string Version = "1.7.0";

    public static bool IsSupportedPlanApiVersion(string? version)
    {
        return string.Equals(version, "1.0.0", System.StringComparison.Ordinal) ||
            string.Equals(version, "1.1.0", System.StringComparison.Ordinal) ||
            string.Equals(version, "1.2.0", System.StringComparison.Ordinal) ||
            string.Equals(version, "1.3.0", System.StringComparison.Ordinal) ||
            string.Equals(version, "1.4.0", System.StringComparison.Ordinal) ||
            string.Equals(version, "1.5.0", System.StringComparison.Ordinal) ||
            string.Equals(version, "1.6.0", System.StringComparison.Ordinal) ||
            string.Equals(version, Version, System.StringComparison.Ordinal);
    }
}
