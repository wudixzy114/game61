using System;
using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

internal static class TerrainPlacementHelper
{
    public static TerrainPlacementCandidate[] QueryCandidates(
        TerrainGenerationProfile profile,
        Rect2 worldBounds,
        TerrainGameplayTag requiredTags,
        TerrainGameplayTag excludedTags,
        int maxCandidates,
        float sampleSpacing,
        float minTraversability,
        float maxTraversalCost,
        float maxHazardPotential,
        bool requireRouteInfluence,
        float minRouteInfluence,
        TerrainRouteCorridorIndex? routeCorridors)
    {
        int safeMaxCandidates = Mathf.Clamp(maxCandidates, 0, 1024);
        if (safeMaxCandidates == 0)
        {
            return [];
        }

        Rect2 bounds = NormalizeRect(worldBounds);
        if (bounds.Size.X <= 0.0f || bounds.Size.Y <= 0.0f)
        {
            return [];
        }

        float spacing = Mathf.Max(4.0f, sampleSpacing);
        float safeMinTraversability = Mathf.Clamp(minTraversability, 0.0f, 1.0f);
        float safeMaxTraversalCost = Mathf.Max(1.0f, maxTraversalCost);
        float safeMaxHazardPotential = Mathf.Clamp(maxHazardPotential, 0.0f, 1.0f);
        float safeMinRouteInfluence = Mathf.Clamp(minRouteInfluence, 0.0f, 1.0f);
        float minSeparation = spacing * 0.75f;
        float minSeparationSquared = minSeparation * minSeparation;
        TerrainRouteCorridorIndex corridors = routeCorridors ?? TerrainRouteCorridorIndex.Empty;

        int xSamples = Mathf.Max(1, Mathf.CeilToInt(bounds.Size.X / spacing) + 1);
        int ySamples = Mathf.Max(1, Mathf.CeilToInt(bounds.Size.Y / spacing) + 1);
        var scored = new List<TerrainPlacementCandidateScore>(xSamples * ySamples);

        for (int y = 0; y < ySamples; y++)
        {
            float worldY = ySamples == 1
                ? bounds.Position.Y + bounds.Size.Y * 0.5f
                : bounds.Position.Y + Mathf.Min(bounds.Size.Y, y * spacing);

            for (int x = 0; x < xSamples; x++)
            {
                float worldX = xSamples == 1
                    ? bounds.Position.X + bounds.Size.X * 0.5f
                    : bounds.Position.X + Mathf.Min(bounds.Size.X, x * spacing);
                Vector2 world = new(worldX, worldY);
                TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);
                TerrainGameplayTags tags = TerrainSemanticClassifier.ClassifyGameplayTags(field, profile);
                if (!MatchesTagFilter(tags.Flags, requiredTags, excludedTags))
                {
                    continue;
                }

                TerrainSample surface = TerrainSampler.SampleWithSlope(world, profile, spacing);
                TerrainTraversalCost traversal = TerrainSemanticClassifier.ClassifyTraversalCost(field, surface, profile);
                if (traversal.IsBlocked ||
                    traversal.Traversability < safeMinTraversability ||
                    (!float.IsPositiveInfinity(traversal.Cost) && traversal.Cost > safeMaxTraversalCost) ||
                    field.HazardPotential > safeMaxHazardPotential)
                {
                    continue;
                }

                TerrainWaterState water = TerrainSemanticClassifier.ClassifyWater(field, profile);
                TerrainRouteCorridorSample routeSample = corridors.HasSegments
                    ? corridors.Sample(world, CoordFromWorld(world, profile.ChunkSize))
                    : TerrainRouteCorridorSample.None;
                if (requireRouteInfluence &&
                    (!routeSample.HasInfluence || routeSample.Influence < safeMinRouteInfluence))
                {
                    continue;
                }

                float score = ScoreCandidate(requiredTags, tags, traversal, water, routeSample);
                scored.Add(new TerrainPlacementCandidateScore(
                    world,
                    field.Height,
                    score,
                    tags,
                    traversal,
                    water,
                    routeSample));
            }
        }

        if (scored.Count == 0)
        {
            return [];
        }

        scored.Sort(static (a, b) => b.Score.CompareTo(a.Score));
        var accepted = new List<TerrainPlacementCandidate>(Mathf.Min(safeMaxCandidates, scored.Count));
        foreach (TerrainPlacementCandidateScore candidate in scored)
        {
            bool tooClose = false;
            foreach (TerrainPlacementCandidate existing in accepted)
            {
                if (existing.WorldPosition.DistanceSquaredTo(candidate.WorldPosition) < minSeparationSquared)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
            {
                continue;
            }

            accepted.Add(new TerrainPlacementCandidate(
                candidate.WorldPosition,
                candidate.Height,
                candidate.Score,
                candidate.Tags,
                candidate.Traversal,
                candidate.Water,
                candidate.RouteCorridor));
            if (accepted.Count >= safeMaxCandidates)
            {
                break;
            }
        }

        return accepted.Count == 0 ? [] : accepted.ToArray();
    }

    private static bool MatchesTagFilter(
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

    private static float ScoreCandidate(
        TerrainGameplayTag requiredTags,
        TerrainGameplayTags tags,
        TerrainTraversalCost traversal,
        TerrainWaterState water,
        TerrainRouteCorridorSample routeSample)
    {
        float score =
            tags.Traversability * 0.32f +
            Mathf.Clamp(3.0f - traversal.Cost, 0.0f, 3.0f) / 3.0f * 0.18f +
            (1.0f - tags.HazardPotential) * 0.10f;

        if (requiredTags == TerrainGameplayTag.None)
        {
            score += tags.ScenicPotential * 0.14f +
                tags.ResourcePotential * 0.13f +
                tags.EncounterPotential * 0.13f;
        }
        else
        {
            if ((requiredTags & TerrainGameplayTag.Scenic) != 0)
            {
                score += tags.ScenicPotential * 0.18f;
            }

            if ((requiredTags & TerrainGameplayTag.ResourceRich) != 0)
            {
                score += tags.ResourcePotential * 0.22f;
            }

            if ((requiredTags & TerrainGameplayTag.EncounterRich) != 0)
            {
                score += tags.EncounterPotential * 0.22f;
            }

            if ((requiredTags & TerrainGameplayTag.SettlementFriendly) != 0)
            {
                score += tags.Traversability * 0.10f + (tags.IsTraversable ? 0.06f : 0.0f);
            }

            if ((requiredTags & TerrainGameplayTag.WaterAccess) != 0)
            {
                score += water.Strength * 0.14f;
            }

            if ((requiredTags & TerrainGameplayTag.Coastal) != 0)
            {
                score += water.Kind == TerrainWaterKind.Coast ? 0.16f : 0.0f;
            }

            if ((requiredTags & TerrainGameplayTag.HighElevation) != 0)
            {
                score += tags.Has(TerrainGameplayTag.HighElevation) ? 0.14f + tags.ScenicPotential * 0.06f : 0.0f;
            }

            if ((requiredTags & TerrainGameplayTag.Cold) != 0)
            {
                score += tags.Has(TerrainGameplayTag.Cold) ? 0.12f : 0.0f;
            }

            if ((requiredTags & TerrainGameplayTag.Arid) != 0)
            {
                score += tags.Has(TerrainGameplayTag.Arid) ? 0.12f : 0.0f;
            }

            if ((requiredTags & TerrainGameplayTag.Hazardous) != 0)
            {
                score += tags.HazardPotential * 0.16f;
            }
        }

        if (routeSample.HasInfluence)
        {
            score += routeSample.Influence * 0.12f + routeSample.Traversability * 0.06f;
        }

        return Mathf.Clamp(score, 0.0f, 1.5f);
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

    private static TerrainTileCoord CoordFromWorld(Vector2 world, float chunkSize)
    {
        return new TerrainTileCoord(
            Mathf.FloorToInt(world.X / chunkSize),
            Mathf.FloorToInt(world.Y / chunkSize));
    }
}
