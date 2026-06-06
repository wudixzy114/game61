using System;
using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

internal static class TerrainGameplayTagRegionQuery
{
    public static TerrainGameplayTagRegionSummary[] QueryRegions(
        TerrainWorldPlan plan,
        TerrainGenerationProfile profile,
        Rect2 worldBounds,
        TerrainGameplayTag requiredTags,
        TerrainGameplayTag excludedTags,
        int maxResults)
    {
        int safeMaxResults = Mathf.Clamp(maxResults, 0, 1024);
        if (safeMaxResults == 0 || plan.GridResolution <= 0 || plan.Regions.Length == 0)
        {
            return [];
        }

        Rect2 bounds = NormalizeRect(worldBounds);
        if (bounds.Size.X < 0.0f || bounds.Size.Y < 0.0f)
        {
            return [];
        }

        var matches = new List<RegionCandidate>(Mathf.Min(safeMaxResults, plan.Regions.Length));
        foreach (TerrainWorldRegion region in plan.Regions)
        {
            if (!ContainsPoint(bounds, region.WorldPosition))
            {
                continue;
            }

            TerrainWorldField field = TerrainWorldFieldSampler.Sample(region.WorldPosition, profile);
            TerrainGameplayTag flags = TerrainSemanticClassifier.ClassifyGameplayTags(field, profile).Flags;
            if (!MatchesFilter(flags, requiredTags, excludedTags))
            {
                continue;
            }

            matches.Add(new RegionCandidate(
                CreateSummary(plan, region, flags),
                ScoreRegion(region, flags, requiredTags)));
        }

        if (matches.Count == 0)
        {
            return [];
        }

        matches.Sort(static (a, b) =>
        {
            int score = b.Score.CompareTo(a.Score);
            if (score != 0)
            {
                return score;
            }

            int y = a.Summary.GridY.CompareTo(b.Summary.GridY);
            return y != 0 ? y : a.Summary.GridX.CompareTo(b.Summary.GridX);
        });

        int count = Mathf.Min(safeMaxResults, matches.Count);
        var result = new TerrainGameplayTagRegionSummary[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = matches[i].Summary;
        }

        return result;
    }

    private static TerrainGameplayTagRegionSummary CreateSummary(
        TerrainWorldPlan plan,
        TerrainWorldRegion region,
        TerrainGameplayTag flags)
    {
        return new TerrainGameplayTagRegionSummary(
            region.GridX,
            region.GridY,
            region.WorldPosition,
            ComputeRegionBounds(plan, region.GridX, region.GridY),
            flags,
            region.BiomeKind,
            region.LandscapeKind,
            region.RegionKind,
            region.Traversability,
            region.ScenicPotential,
            region.ResourcePotential,
            region.HazardPotential,
            region.EncounterPotential);
    }

    private static Rect2 ComputeRegionBounds(TerrainWorldPlan plan, int gridX, int gridY)
    {
        float cellSize = plan.WorldSize / plan.GridResolution;
        Vector2 min = new(
            plan.Center.X - plan.WorldSize * 0.5f + gridX * cellSize,
            plan.Center.Y - plan.WorldSize * 0.5f + gridY * cellSize);
        return new Rect2(min, new Vector2(cellSize, cellSize));
    }

    private static bool MatchesFilter(
        TerrainGameplayTag actual,
        TerrainGameplayTag required,
        TerrainGameplayTag excluded)
    {
        if (required != TerrainGameplayTag.None && (actual & required) != required)
        {
            return false;
        }

        return excluded == TerrainGameplayTag.None || (actual & excluded) == TerrainGameplayTag.None;
    }

    private static float ScoreRegion(
        TerrainWorldRegion region,
        TerrainGameplayTag flags,
        TerrainGameplayTag requiredTags)
    {
        if (requiredTags == TerrainGameplayTag.None)
        {
            return
                region.ScenicPotential * 0.24f +
                region.ResourcePotential * 0.22f +
                region.EncounterPotential * 0.18f +
                region.Traversability * 0.22f +
                (1.0f - region.HazardPotential) * 0.14f;
        }

        float score = 0.0f;
        if ((requiredTags & TerrainGameplayTag.Traversable) != 0)
        {
            score += region.Traversability * 0.26f;
        }

        if ((requiredTags & TerrainGameplayTag.Scenic) != 0)
        {
            score += region.ScenicPotential * 0.24f;
        }

        if ((requiredTags & TerrainGameplayTag.ResourceRich) != 0)
        {
            score += region.ResourcePotential * 0.24f;
        }

        if ((requiredTags & TerrainGameplayTag.Hazardous) != 0)
        {
            score += region.HazardPotential * 0.22f;
        }

        if ((requiredTags & TerrainGameplayTag.EncounterRich) != 0)
        {
            score += region.EncounterPotential * 0.22f;
        }

        score += FlagBonus(flags, requiredTags, TerrainGameplayTag.WaterAccess, 0.18f);
        score += FlagBonus(flags, requiredTags, TerrainGameplayTag.Coastal, 0.16f);
        score += FlagBonus(flags, requiredTags, TerrainGameplayTag.SettlementFriendly, 0.20f);
        score += FlagBonus(flags, requiredTags, TerrainGameplayTag.HighElevation, 0.18f);
        score += FlagBonus(flags, requiredTags, TerrainGameplayTag.Cold, 0.14f);
        score += FlagBonus(flags, requiredTags, TerrainGameplayTag.Arid, 0.14f);
        score += region.Traversability * 0.08f + (1.0f - region.HazardPotential) * 0.06f;
        return score;
    }

    private static float FlagBonus(
        TerrainGameplayTag flags,
        TerrainGameplayTag required,
        TerrainGameplayTag tag,
        float bonus)
    {
        return (required & tag) != 0 && (flags & tag) == tag ? bonus : 0.0f;
    }

    private static Rect2 NormalizeRect(Rect2 rect)
    {
        float x0 = rect.Position.X;
        float y0 = rect.Position.Y;
        float x1 = rect.Position.X + rect.Size.X;
        float y1 = rect.Position.Y + rect.Size.Y;
        float minX = Mathf.Min(x0, x1);
        float maxX = Mathf.Max(x0, x1);
        float minY = Mathf.Min(y0, y1);
        float maxY = Mathf.Max(y0, y1);
        return new Rect2(new Vector2(minX, minY), new Vector2(maxX - minX, maxY - minY));
    }

    private static bool ContainsPoint(Rect2 bounds, Vector2 point)
    {
        float x0 = bounds.Position.X;
        float y0 = bounds.Position.Y;
        float x1 = bounds.Position.X + bounds.Size.X;
        float y1 = bounds.Position.Y + bounds.Size.Y;
        float minX = Mathf.Min(x0, x1);
        float maxX = Mathf.Max(x0, x1);
        float minY = Mathf.Min(y0, y1);
        float maxY = Mathf.Max(y0, y1);
        return point.X >= minX &&
            point.X <= maxX &&
            point.Y >= minY &&
            point.Y <= maxY;
    }

    private readonly record struct RegionCandidate(
        TerrainGameplayTagRegionSummary Summary,
        float Score);
}
