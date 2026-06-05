using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static class TerrainSurfaceScatterRules
    {
        public static readonly GameplayScatterRule Understory = new(
            MaxSlope: 0.22f,
            MinPrimary: 0.42f,
            MinSecondary: 0.50f,
            MinTemperature: 0.24f,
            ProbabilityLow: 0.08f,
            ProbabilityHigh: 0.46f,
            BaseScale: 0.55f,
            ScaleJitter: 0.95f,
            TintLow: new Color(0.18f, 0.36f, 0.16f),
            TintHigh: new Color(0.34f, 0.50f, 0.22f));

        public static readonly GameplayScatterRule ResourceNode = new(
            MaxSlope: 0.30f,
            MinPrimary: 0.62f,
            MinSecondary: 0.34f,
            MinTemperature: 0.0f,
            ProbabilityLow: 0.04f,
            ProbabilityHigh: 0.24f,
            BaseScale: 0.95f,
            ScaleJitter: 1.45f,
            TintLow: new Color(0.28f, 0.48f, 0.22f),
            TintHigh: new Color(0.62f, 0.54f, 0.30f));

        public static readonly GameplayScatterRule HazardOutcrop = new(
            MaxSlope: float.PositiveInfinity,
            MinPrimary: 0.48f,
            MinSecondary: 0.40f,
            MinTemperature: 0.0f,
            ProbabilityLow: 0.05f,
            ProbabilityHigh: 0.30f,
            BaseScale: 0.85f,
            ScaleJitter: 1.80f,
            TintLow: new Color(0.38f, 0.30f, 0.27f),
            TintHigh: new Color(0.64f, 0.58f, 0.50f));

        public static readonly WaterZoneRule TidalMangroveFlat = new(
            MaxSlope: 0.24f,
            MinHeightOffset: -8.0f,
            MaxHeightOffset: 34.0f,
            MinPrimary: 0.50f,
            MinSecondary: 0.28f,
            RiverThreshold: 0.26f,
            ShorelineHeightOffset: 12.0f);

        public static readonly WaterZoneRule LakeScatterZone = new(
            MaxSlope: 0.22f,
            MinHeightOffset: 6.0f,
            MaxHeightOffsetFactor: 0.72f,
            MinPrimary: 0.30f,
            MinSecondary: 0.58f,
            ResourceThreshold: 0.34f);

        public const float NaturalDensityPenalty = 0.42f;
        public const float BaseDensityPenalty = 1.0f;
    }

    private readonly record struct GameplayScatterRule(
        float MaxSlope,
        float MinPrimary,
        float MinSecondary,
        float MinTemperature,
        float ProbabilityLow,
        float ProbabilityHigh,
        float BaseScale,
        float ScaleJitter,
        Color TintLow,
        Color TintHigh);

    private readonly record struct WaterZoneRule(
        float MaxSlope,
        float MinHeightOffset,
        float MaxHeightOffset,
        float MinPrimary,
        float MinSecondary,
        float RiverThreshold,
        float ShorelineHeightOffset)
    {
        public WaterZoneRule(
            float MaxSlope,
            float MinHeightOffset,
            float MaxHeightOffsetFactor,
            float MinPrimary,
            float MinSecondary,
            float ResourceThreshold)
            : this(
                MaxSlope,
                MinHeightOffset,
                MaxHeightOffsetFactor,
                MinPrimary,
                MinSecondary,
                ResourceThreshold,
                0.0f)
        {
        }
    }
}
