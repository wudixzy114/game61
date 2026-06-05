using System;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Stable classification for static water semantics sampled from terrain fields.</summary>
public enum TerrainWaterKind
{
    None = 0,
    Ocean = 1,
    Coast = 2,
    Lake = 3,
    River = 4,
    Oasis = 5
}

/// <summary>Bit flags for common gameplay-facing terrain semantics.</summary>
[Flags]
public enum TerrainGameplayTag
{
    None = 0,
    Traversable = 1 << 0,
    Scenic = 1 << 1,
    ResourceRich = 1 << 2,
    Hazardous = 1 << 3,
    EncounterRich = 1 << 4,
    WaterAccess = 1 << 5,
    Coastal = 1 << 6,
    SettlementFriendly = 1 << 7,
    HighElevation = 1 << 8,
    Cold = 1 << 9,
    Arid = 1 << 10
}

/// <summary>Static water state at a sampled terrain position.</summary>
public readonly record struct TerrainWaterState(
    Vector2 WorldPosition,
    TerrainWaterKind Kind,
    float SurfaceHeight,
    float Depth,
    float Strength,
    TerrainBiomeKind BiomeKind,
    TerrainLandscapeKind LandscapeKind)
{
    public bool HasWater => Kind != TerrainWaterKind.None;
    public bool IsOceanic => Kind is TerrainWaterKind.Ocean or TerrainWaterKind.Coast;
}

/// <summary>Gameplay-facing terrain tags and source scores at a sampled terrain position.</summary>
public readonly record struct TerrainGameplayTags(
    Vector2 WorldPosition,
    TerrainGameplayTag Flags,
    TerrainBiomeKind BiomeKind,
    TerrainLandscapeKind LandscapeKind,
    TerrainWaterKind WaterKind,
    float Traversability,
    float ScenicPotential,
    float ResourcePotential,
    float HazardPotential,
    float EncounterPotential)
{
    public bool Has(TerrainGameplayTag tag)
    {
        return (Flags & tag) == tag;
    }

    public bool IsTraversable => Has(TerrainGameplayTag.Traversable);
    public bool IsScenic => Has(TerrainGameplayTag.Scenic);
    public bool IsResourceRich => Has(TerrainGameplayTag.ResourceRich);
    public bool IsHazardous => Has(TerrainGameplayTag.Hazardous);
    public bool IsEncounterRich => Has(TerrainGameplayTag.EncounterRich);
    public bool HasWaterAccess => Has(TerrainGameplayTag.WaterAccess);
}

/// <summary>Local terrain traversal semantics for AI, navigation graph weighting, encounters, and placement filters.</summary>
public readonly record struct TerrainTraversalCost(
    Vector2 WorldPosition,
    bool IsBlocked,
    float Cost,
    float Traversability,
    float Slope,
    float HazardPotential,
    TerrainWaterKind WaterKind,
    TerrainBiomeKind BiomeKind,
    TerrainLandscapeKind LandscapeKind)
{
    public bool IsPreferred => !IsBlocked && Cost <= 1.35f;
    public bool IsDifficult => !IsBlocked && Cost >= 2.25f;
}

/// <summary>Pure terrain semantic classifiers shared by runtime facade APIs and validation tools.</summary>
public static class TerrainSemanticClassifier
{
    public static TerrainWaterState ClassifyWater(TerrainWorldField field, TerrainGenerationProfile profile)
    {
        TerrainWaterKind kind = ClassifyWaterKind(field, profile);
        float surfaceHeight = WaterSurfaceHeight(field, profile, kind);
        float depth = kind == TerrainWaterKind.None
            ? 0.0f
            : Mathf.Max(0.0f, surfaceHeight - field.Height);
        float strength = kind switch
        {
            TerrainWaterKind.Ocean => Mathf.Clamp((profile.SeaLevel - field.Height) / Mathf.Max(1.0f, profile.HeightScale * 0.38f), 0.0f, 1.0f),
            TerrainWaterKind.Coast => Mathf.Clamp(1.0f - Mathf.Abs(field.Height - profile.SeaLevel) / 24.0f, 0.0f, 1.0f),
            TerrainWaterKind.Lake => Mathf.Clamp(field.Lake, 0.0f, 1.0f),
            TerrainWaterKind.River => Mathf.Clamp(field.River, 0.0f, 1.0f),
            TerrainWaterKind.Oasis => Mathf.Clamp(field.Moisture * 0.38f + field.ResourcePotential * 0.34f + field.River * 0.28f, 0.0f, 1.0f),
            _ => 0.0f
        };

        return new TerrainWaterState(
            field.WorldPosition,
            kind,
            surfaceHeight,
            depth,
            strength,
            field.BiomeKind,
            field.LandscapeKind);
    }

    public static TerrainGameplayTags ClassifyGameplayTags(TerrainWorldField field, TerrainGenerationProfile profile)
    {
        TerrainWaterState water = ClassifyWater(field, profile);
        TerrainGameplayTag flags = TerrainGameplayTag.None;

        if (field.Traversability >= 0.45f)
        {
            flags |= TerrainGameplayTag.Traversable;
        }

        if (field.ScenicPotential >= 0.62f)
        {
            flags |= TerrainGameplayTag.Scenic;
        }

        if (field.ResourcePotential >= 0.50f)
        {
            flags |= TerrainGameplayTag.ResourceRich;
        }

        if (field.HazardPotential >= 0.42f)
        {
            flags |= TerrainGameplayTag.Hazardous;
        }

        if (field.EncounterPotential >= 0.52f)
        {
            flags |= TerrainGameplayTag.EncounterRich;
        }

        if (water.HasWater || field.River >= 0.34f || field.Lake >= 0.34f)
        {
            flags |= TerrainGameplayTag.WaterAccess;
        }

        if (field.BiomeKind == TerrainBiomeKind.Coast || field.LandscapeKind == TerrainLandscapeKind.Coast)
        {
            flags |= TerrainGameplayTag.Coastal;
        }

        if (field.Traversability >= 0.54f &&
            field.ResourcePotential >= 0.38f &&
            field.HazardPotential < 0.65f &&
            field.Height >= profile.SeaLevel + 8.0f)
        {
            flags |= TerrainGameplayTag.SettlementFriendly;
        }

        if (field.Height > profile.SeaLevel + profile.HeightScale * 0.55f ||
            field.LandscapeKind is TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.Snowfield or TerrainLandscapeKind.VistaPlateau)
        {
            flags |= TerrainGameplayTag.HighElevation;
        }

        if (field.Temperature <= 0.22f || field.BiomeKind == TerrainBiomeKind.Snowfield)
        {
            flags |= TerrainGameplayTag.Cold;
        }

        if (field.BiomeKind == TerrainBiomeKind.Desert ||
            (field.Temperature >= 0.52f && field.Moisture <= 0.34f))
        {
            flags |= TerrainGameplayTag.Arid;
        }

        return new TerrainGameplayTags(
            field.WorldPosition,
            flags,
            field.BiomeKind,
            field.LandscapeKind,
            water.Kind,
            field.Traversability,
            field.ScenicPotential,
            field.ResourcePotential,
            field.HazardPotential,
            field.EncounterPotential);
    }

    public static TerrainTraversalCost ClassifyTraversalCost(TerrainWorldField field, TerrainSample surface, TerrainGenerationProfile profile)
    {
        TerrainWaterState water = ClassifyWater(field, profile);
        float traversability = Mathf.Clamp(field.Traversability, 0.0f, 1.0f);
        float slope = Mathf.Clamp(surface.Slope, 0.0f, 1.0f);
        float hazard = Mathf.Clamp(field.HazardPotential, 0.0f, 1.0f);
        bool blocked =
            traversability < 0.18f ||
            slope > 0.88f ||
            water.Kind == TerrainWaterKind.Ocean ||
            (water.Kind == TerrainWaterKind.Lake && water.Depth > 0.40f);

        float traversabilityPenalty = (1.0f - traversability) * 2.4f;
        float slopePenalty = slope * slope * 2.2f;
        float hazardPenalty = hazard * 0.85f;
        float waterPenalty = water.Kind switch
        {
            TerrainWaterKind.None => 0.0f,
            TerrainWaterKind.Coast => 0.55f,
            TerrainWaterKind.River => 0.72f,
            TerrainWaterKind.Oasis => 0.38f,
            TerrainWaterKind.Lake => 1.15f,
            TerrainWaterKind.Ocean => 4.0f,
            _ => 0.0f
        };
        float cost = blocked
            ? float.PositiveInfinity
            : Mathf.Clamp(1.0f + traversabilityPenalty + slopePenalty + hazardPenalty + waterPenalty, 1.0f, 8.0f);

        return new TerrainTraversalCost(
            field.WorldPosition,
            blocked,
            cost,
            traversability,
            slope,
            hazard,
            water.Kind,
            field.BiomeKind,
            field.LandscapeKind);
    }

    private static TerrainWaterKind ClassifyWaterKind(TerrainWorldField field, TerrainGenerationProfile profile)
    {
        if (field.BiomeKind == TerrainBiomeKind.Ocean ||
            field.LandscapeKind == TerrainLandscapeKind.Ocean ||
            field.Height < profile.SeaLevel - 12.0f)
        {
            return TerrainWaterKind.Ocean;
        }

        if (field.BiomeKind == TerrainBiomeKind.Coast ||
            field.LandscapeKind == TerrainLandscapeKind.Coast ||
            field.Height < profile.SeaLevel + 12.0f)
        {
            return TerrainWaterKind.Coast;
        }

        if (field.BiomeKind == TerrainBiomeKind.Oasis)
        {
            return TerrainWaterKind.Oasis;
        }

        bool inlandEnough = field.Height > profile.SeaLevel + 4.0f;
        bool lake =
            inlandEnough &&
            field.Height < profile.SeaLevel + profile.HeightScale * 0.76f &&
            (field.BiomeKind == TerrainBiomeKind.Lake ||
                field.LandscapeKind == TerrainLandscapeKind.Lake ||
                field.Lake > 0.38f) &&
            field.River < 0.86f;
        if (lake)
        {
            return TerrainWaterKind.Lake;
        }

        bool river =
            inlandEnough &&
            field.River > 0.72f &&
            field.Height < profile.SeaLevel + profile.HeightScale * 0.68f;
        if (river)
        {
            return TerrainWaterKind.River;
        }

        return TerrainWaterKind.None;
    }

    private static float WaterSurfaceHeight(
        TerrainWorldField field,
        TerrainGenerationProfile profile,
        TerrainWaterKind kind)
    {
        return kind switch
        {
            TerrainWaterKind.Ocean or TerrainWaterKind.Coast => profile.SeaLevel,
            TerrainWaterKind.Oasis => Mathf.Max(profile.SeaLevel + 0.08f, field.Height + 0.16f),
            TerrainWaterKind.River => Mathf.Max(profile.SeaLevel + 0.08f, field.Height + 0.11f),
            TerrainWaterKind.Lake => Mathf.Max(profile.SeaLevel + 0.08f, field.Height + 0.13f),
            _ => field.Height
        };
    }
}
