namespace Dao.Terrain;

/// <summary>Stable tolerance contract for deterministic terrain data and native/managed parity checks.</summary>
public static class TerrainDeterminismContract
{
    public const string Contract = "terrain-determinism-v1";

    /// <summary>Strict tolerance for same-process facade, serialization, and snapshot equality checks.</summary>
    public const float ExactFloatEpsilon = 0.0001f;

    /// <summary>Strict world-space position tolerance for exact snapshot and JSON roundtrip checks.</summary>
    public const float ExactPositionEpsilon = 0.01f;

    /// <summary>Stable height tolerance for deterministic plan topology and cross-runtime comparisons.</summary>
    public const float HeightEpsilon = 0.05f;

    /// <summary>Stable normalized field tolerance for gameplay-facing terrain semantic values.</summary>
    public const float FieldEpsilon = 0.001f;

    /// <summary>Stable world-space position tolerance for generated plan topology.</summary>
    public const float PositionEpsilon = 0.10f;

    /// <summary>Maximum acceptable native height grid delta against the managed sampler.</summary>
    public const float NativeHeightMaxEpsilon = 1.5f;

    /// <summary>Average acceptable native height grid delta against the managed sampler.</summary>
    public const float NativeHeightAverageEpsilon = 0.25f;

    /// <summary>Maximum acceptable native derived field delta against the managed sampler.</summary>
    public const float NativeFieldMaxEpsilon = 0.015f;

    /// <summary>Average acceptable native derived field delta against the managed sampler.</summary>
    public const float NativeFieldAverageEpsilon = 0.0025f;

    /// <summary>Maximum acceptable native tile vertex height delta against the managed tile path.</summary>
    public const float NativeTileHeightEpsilon = 1.5f;

    /// <summary>Maximum acceptable native tile color delta against the managed tile path.</summary>
    public const float NativeTileColorEpsilon = 0.03f;

    /// <summary>Maximum acceptable benchmark tile parity height delta.</summary>
    public const float TileParityHeightEpsilon = 0.05f;

    /// <summary>Maximum acceptable benchmark tile parity color delta.</summary>
    public const float TileParityColorEpsilon = 0.03f;

    public static float Squared(float epsilon)
    {
        return epsilon * epsilon;
    }
}
