using System.Diagnostics.CodeAnalysis;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Streaming;

public partial class TerrainWorld
{
    /// <summary>Queries gameplay-facing placement candidates from terrain semantics without instantiating resources or encounters.</summary>
    public TerrainPlacementCandidate[] QueryPlacementCandidates(
        Rect2 worldBounds,
        TerrainGameplayTag requiredTags,
        TerrainGameplayTag excludedTags = TerrainGameplayTag.None,
        int maxCandidates = 32,
        float sampleSpacing = 32.0f,
        float minTraversability = 0.45f,
        float maxTraversalCost = 2.4f,
        float maxHazardPotential = 1.0f,
        bool requireRouteInfluence = false,
        float minRouteInfluence = 0.0f)
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
        TerrainRouteCorridorIndex corridors = _worldPlan is null ? TerrainRouteCorridorIndex.Empty : _routeCorridors;

        int xSamples = Mathf.Max(1, Mathf.CeilToInt(bounds.Size.X / spacing) + 1);
        int ySamples = Mathf.Max(1, Mathf.CeilToInt(bounds.Size.Y / spacing) + 1);
        var scored = new System.Collections.Generic.List<TerrainPlacementCandidateScore>(xSamples * ySamples);
        TerrainGenerationProfile profile = CurrentProfile;

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
                TerrainWorldField field = SampleFieldWithModification(world);
                TerrainGameplayTags tags = TerrainSemanticClassifier.ClassifyGameplayTags(field, profile);
                if (!MatchesTagFilter(tags.Flags, requiredTags, excludedTags))
                {
                    continue;
                }

                TerrainSample surface = SampleSurfaceWithModification(world, spacing);
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

                float score = ScorePlacementCandidate(requiredTags, tags, traversal, water, routeSample);
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
        var accepted = new System.Collections.Generic.List<TerrainPlacementCandidate>(Mathf.Min(safeMaxCandidates, scored.Count));
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

    /// <summary>Builds a traversal-cost grid handoff for navigation, AI, and gameplay tools without performing pathfinding.</summary>
    public TerrainTraversalCostGrid CreateTraversalCostGrid(
        Vector2 center,
        float worldSize,
        int gridSize,
        float spacing = 24.0f)
    {
        int size = Mathf.Clamp(gridSize, 2, 4096);
        float safeWorldSize = Mathf.Max(1.0f, worldSize);
        var samples = new TerrainTraversalCost[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float tx = x / (float)(size - 1);
                float ty = y / (float)(size - 1);
                Vector2 world = new(
                    center.X + (tx - 0.5f) * safeWorldSize,
                    center.Y + (ty - 0.5f) * safeWorldSize);
                samples[(y * size) + x] = SampleTraversalCostWithModification(world, spacing);
            }
        }

        return new TerrainTraversalCostGrid(size, size, center, safeWorldSize, samples);
    }

    /// <summary>Builds a traversal-cost grid exactly covering a streaming tile without requiring that tile to be loaded.</summary>
    public TerrainTraversalCostGrid CreateTraversalCostGridForTile(
        TerrainTileCoord coord,
        int gridSize,
        float spacing = 24.0f)
    {
        TerrainGenerationProfile profile = CurrentProfile;
        int size = Mathf.Clamp(gridSize, 2, 4096);
        float safeChunkSize = Mathf.Max(1.0f, profile.ChunkSize);
        float safeSpacing = Mathf.Max(1.0f, spacing);
        Vector2 origin = coord.Origin(safeChunkSize);
        Vector2 center = origin + new Vector2(safeChunkSize * 0.5f, safeChunkSize * 0.5f);
        var samples = new TerrainTraversalCost[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float tx = x / (float)(size - 1);
                float ty = y / (float)(size - 1);
                Vector2 world = new(
                    origin.X + tx * safeChunkSize,
                    origin.Y + ty * safeChunkSize);
                samples[(y * size) + x] = SampleTraversalCostWithModification(world, safeSpacing);
            }
        }

        return new TerrainTraversalCostGrid(size, size, center, safeChunkSize, samples);
    }

    /// <summary>Samples traversal costs inside a bounded world-space region without performing pathfinding.</summary>
    public TerrainTraversalCost[] QueryTraversalCosts(
        Rect2 worldBounds,
        float sampleSpacing = 24.0f,
        int maxSamples = 1024)
    {
        Rect2 bounds = NormalizeRect(worldBounds);
        float safeSpacing = Mathf.Max(1.0f, sampleSpacing);
        int safeMaxSamples = Mathf.Clamp(maxSamples, 0, 262_144);
        if (safeMaxSamples == 0 || bounds.Size.X <= 0.0f || bounds.Size.Y <= 0.0f)
        {
            return [];
        }

        int xCount = Mathf.Max(1, Mathf.FloorToInt(bounds.Size.X / safeSpacing) + 1);
        int yCount = Mathf.Max(1, Mathf.FloorToInt(bounds.Size.Y / safeSpacing) + 1);
        var samples = new System.Collections.Generic.List<TerrainTraversalCost>(Mathf.Min(safeMaxSamples, xCount * yCount));

        for (int y = 0; y < yCount && samples.Count < safeMaxSamples; y++)
        {
            float wy = yCount == 1
                ? bounds.Position.Y + bounds.Size.Y * 0.5f
                : Mathf.Min(bounds.Position.Y + y * safeSpacing, bounds.Position.Y + bounds.Size.Y);
            for (int x = 0; x < xCount && samples.Count < safeMaxSamples; x++)
            {
                float wx = xCount == 1
                    ? bounds.Position.X + bounds.Size.X * 0.5f
                    : Mathf.Min(bounds.Position.X + x * safeSpacing, bounds.Position.X + bounds.Size.X);
                samples.Add(SampleTraversalCostWithModification(new Vector2(wx, wy), safeSpacing));
            }
        }

        return samples.Count == 0 ? [] : samples.ToArray();
    }

    /// <summary>Returns a snapshot copy of the current planned route graph, or an empty snapshot when no plan is ready.</summary>
    public TerrainRouteGraphSnapshot GetRouteGraphSnapshot()
    {
        TerrainWorldPlan? effectivePlan = CreateEffectiveWorldPlan();
        return effectivePlan is null
            ? TerrainRouteGraphSnapshot.Empty
            : TerrainRouteGraphSnapshot.FromPlan(effectivePlan);
    }

    /// <summary>Builds a waypoint graph from the current planned route graph for AI/navigation importers without requiring loaded tiles.</summary>
    public TerrainNavigationWaypointGraph CreateNavigationWaypointGraph()
    {
        TerrainWorldPlan? effectivePlan = CreateEffectiveWorldPlan();
        return effectivePlan is null
            ? TerrainNavigationWaypointGraph.Empty
            : TerrainNavigationWaypointGraph.FromPlan(effectivePlan);
    }

    /// <summary>Returns a snapshot copy of the current planned route graph without exposing internal mutable waypoint arrays.</summary>
    public bool TryGetRouteGraphSnapshot([NotNullWhen(true)] out TerrainRouteGraphSnapshot? snapshot)
    {
        TerrainWorldPlan? effectivePlan = CreateEffectiveWorldPlan();
        if (effectivePlan is null)
        {
            snapshot = null;
            return false;
        }

        snapshot = TerrainRouteGraphSnapshot.FromPlan(effectivePlan);
        return true;
    }

    /// <summary>Finds a high-level planned route path between two POIs without requiring streamed tiles or a rendered nav mesh.</summary>
    public bool TryFindRoutePath(int fromPointId, int toPointId, [NotNullWhen(true)] out TerrainRouteGraphPath? path)
    {
        path = null;
        TerrainWorldPlan? effectivePlan = CreateEffectiveWorldPlan();
        if (effectivePlan is null)
        {
            return false;
        }

        TerrainRouteGraphSnapshot snapshot = TerrainRouteGraphSnapshot.FromPlan(effectivePlan);
        return snapshot.TryFindPath(fromPointId, toPointId, out path);
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

    private static float ScorePlacementCandidate(
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
}
