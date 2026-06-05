namespace Dao.Terrain;

/// <summary>Stable performance contract for terrain benchmark gates and native parity thresholds.</summary>
public static class TerrainPerformanceContract
{
    public const string Contract = "terrain-performance-v1";

    /// <summary>Shared benchmark hardware baseline label used by validation output and CI gates.</summary>
    public const string TileBenchmarkHardwareBaseline = "dev-linux-x64-provisional";

    /// <summary>Maximum acceptable managed tile build time per tile for the shared benchmark baseline.</summary>
    public const double MaxManagedMillisecondsPerTile = 24.0;

    /// <summary>Maximum acceptable native-enabled tile build time per tile for the shared benchmark baseline.</summary>
    public const double MaxNativeMillisecondsPerTile = 8.0;

    /// <summary>Maximum acceptable managed tile build P50 latency on the shared benchmark baseline.</summary>
    public const double MaxManagedP50Milliseconds = 24.0;

    /// <summary>Maximum acceptable managed tile build P95 latency on the shared benchmark baseline.</summary>
    public const double MaxManagedP95Milliseconds = 48.0;

    /// <summary>Maximum acceptable managed tile build P99 latency on the shared benchmark baseline.</summary>
    public const double MaxManagedP99Milliseconds = 72.0;

    /// <summary>Maximum acceptable native-enabled tile build P50 latency on the shared benchmark baseline.</summary>
    public const double MaxNativeP50Milliseconds = 8.0;

    /// <summary>Maximum acceptable native-enabled tile build P95 latency on the shared benchmark baseline.</summary>
    public const double MaxNativeP95Milliseconds = 16.0;

    /// <summary>Maximum acceptable native-enabled tile build P99 latency on the shared benchmark baseline.</summary>
    public const double MaxNativeP99Milliseconds = 24.0;

    /// <summary>Maximum acceptable allocation cost per tile for the shared benchmark baseline.</summary>
    public const double MaxAllocatedKilobytesPerTile = 2048.0;

    /// <summary>Minimum acceptable native speedup versus managed tile generation on the shared benchmark baseline.</summary>
    public const double MinNativeSpeedup = 1.00;

    /// <summary>Minimum tile count required for native parity validation within benchmark runs.</summary>
    public const int MinParityTileCount = 8;

    /// <summary>Minimum distinct biome kinds required in the benchmark tile set.</summary>
    public const int MinBenchmarkBiomeKinds = 7;

    /// <summary>Minimum distinct landscape kinds required in the benchmark tile set.</summary>
    public const int MinBenchmarkLandscapeKinds = 6;

    /// <summary>Minimum benchmark tiles that must intersect POI footprints or settlements.</summary>
    public const int MinBenchmarkPointOfInterestTiles = 8;

    /// <summary>Minimum benchmark tiles that must intersect route corridors.</summary>
    public const int MinBenchmarkRouteTiles = 8;

    /// <summary>Minimum gameplay-rich benchmark tiles required to keep coverage representative.</summary>
    public const int MinBenchmarkGameplayRichTiles = 12;
}
