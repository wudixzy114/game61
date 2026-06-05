using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanner
{
    private static void AddPoiCandidates(
        List<PoiCandidate> candidates,
        TerrainGenerationProfile profile,
        TerrainWorldField field,
        int gridX,
        int gridY)
    {
        if (field.Height < profile.SeaLevel - 4.0f)
        {
            return;
        }

        float land = Mathf.SmoothStep(profile.SeaLevel + 2.0f, profile.SeaLevel + 48.0f, field.Height);
        float elevation = Mathf.SmoothStep(profile.SeaLevel + 80.0f, profile.SeaLevel + profile.HeightScale * 0.72f, field.Height);
        float rarity = Hash01(gridX, gridY, profile.Seed + 911);
        float stableFlatLand = field.Traversability * land;

        float settlementBiomeBonus = field.BiomeKind is TerrainBiomeKind.Plains or TerrainBiomeKind.Grassland
            ? 0.10f
            : field.BiomeKind == TerrainBiomeKind.Oasis ? 0.16f : 0.0f;
        float settlementScore =
            stableFlatLand * 0.55f +
            Mathf.Clamp(1.0f - Mathf.Abs(field.Moisture - 0.55f) * 2.0f, 0.0f, 1.0f) * 0.12f +
            Mathf.Clamp(1.0f - Mathf.Abs(field.Temperature - 0.56f) * 2.1f, 0.0f, 1.0f) * 0.12f +
            Mathf.SmoothStep(0.18f, 0.62f, field.River) * 0.09f +
            field.ScenicPotential * 0.12f +
            settlementBiomeBonus;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.SettlementCandidate, settlementScore, 0.58f, field, gridX, gridY, rarity);

        float vistaScore = field.ScenicPotential * 0.82f + elevation * 0.14f + rarity * 0.04f;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.Vista, vistaScore, 0.64f, field, gridX, gridY, rarity);

        float crossingScore =
            Mathf.SmoothStep(0.50f, 0.82f, field.River) * 0.55f +
            field.Traversability * 0.30f +
            land * 0.15f;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.RiverCrossing, crossingScore, 0.62f, field, gridX, gridY, rarity);

        float passScore = 0.0f;
        if (field.LandscapeKind is TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.VistaPlateau)
        {
            passScore = elevation * 0.30f + field.Traversability * 0.36f + field.ScenicPotential * 0.28f + rarity * 0.06f;
        }

        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.MountainPass, passScore, 0.54f, field, gridX, gridY, rarity);

        float coastScore = 0.0f;
        if (field.LandscapeKind == TerrainLandscapeKind.Coast || Mathf.Abs(field.Height - profile.SeaLevel) < 30.0f)
        {
            coastScore = land * 0.30f + field.Traversability * 0.30f + field.ScenicPotential * 0.28f + rarity * 0.12f;
        }

        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.CoastalLanding, coastScore, 0.50f, field, gridX, gridY, rarity);

        float resourceScore = 0.0f;
        if (field.LandscapeKind is TerrainLandscapeKind.ForestBasin or TerrainLandscapeKind.Wetland or TerrainLandscapeKind.RiverValley ||
            field.BiomeKind == TerrainBiomeKind.Oasis)
        {
            resourceScore = field.Moisture * 0.34f + field.Traversability * 0.24f + (1.0f - elevation) * 0.16f + field.River * 0.12f + rarity * 0.14f;
        }

        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.ResourceGrove, resourceScore, 0.58f, field, gridX, gridY, rarity);

        float ancientScore = field.ScenicPotential * 0.50f + elevation * 0.18f + stableFlatLand * 0.16f + rarity * 0.16f;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.AncientSite, ancientScore, 0.70f, field, gridX, gridY, rarity);

        float canyonScore = field.LandscapeKind == TerrainLandscapeKind.Canyon
            ? field.ScenicPotential * 0.50f + field.River * 0.26f + elevation * 0.12f + rarity * 0.12f
            : 0.0f;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.CanyonOverlook, canyonScore, 0.58f, field, gridX, gridY, rarity);

        bool naturalOasis = field.BiomeKind == TerrainBiomeKind.Oasis;
        float warmDryWaterAccess =
            Mathf.SmoothStep(0.46f, 0.76f, field.Temperature) *
            (1.0f - Mathf.SmoothStep(0.50f, 0.76f, field.Moisture)) *
            Mathf.Max(
                Mathf.SmoothStep(0.22f, 0.62f, field.River),
                Mathf.SmoothStep(0.44f, 0.72f, field.ResourcePotential));
        bool strategicOasisSite =
            !naturalOasis &&
            field.Height > profile.SeaLevel + 8.0f &&
            field.Traversability > 0.28f &&
            field.BiomeKind is TerrainBiomeKind.Desert or TerrainBiomeKind.Plains or TerrainBiomeKind.Grassland or TerrainBiomeKind.Hills &&
            warmDryWaterAccess > 0.18f;
        float oasisScore = naturalOasis
            ? field.ResourcePotential * 0.38f + field.Traversability * 0.20f + field.River * 0.18f + field.ScenicPotential * 0.14f + rarity * 0.10f
            : strategicOasisSite
            ? warmDryWaterAccess * 0.30f + field.ResourcePotential * 0.26f + field.Traversability * 0.18f + field.ScenicPotential * 0.12f + rarity * 0.14f
            : 0.0f;
        AddCandidateIfStrong(candidates, TerrainPointOfInterestKind.Oasis, oasisScore, 0.54f, field, gridX, gridY, rarity);
    }

    private static void AddCandidateIfStrong(
        List<PoiCandidate> candidates,
        TerrainPointOfInterestKind kind,
        float score,
        float threshold,
        TerrainWorldField field,
        int gridX,
        int gridY,
        float rarity)
    {
        if (score < threshold)
        {
            return;
        }

        candidates.Add(new PoiCandidate(
            kind,
            field.WorldPosition,
            gridX,
            gridY,
            Mathf.Clamp(score + rarity * 0.025f, 0.0f, 1.0f),
            field.Height,
            field.ScenicPotential,
            field.Traversability,
            field.ResourcePotential,
            field.River,
            field.BiomeKind,
            field.LandscapeKind));
    }

    private static TerrainWorldPointOfInterest[] SelectPointsOfInterest(
        List<PoiCandidate> candidates,
        TerrainGenerationProfile profile,
        int maxPoints,
        float cellSize,
        float worldSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var selected = new List<TerrainWorldPointOfInterest>(maxPoints);
        var kindCounts = new Dictionary<TerrainPointOfInterestKind, int>();
        int perKindLimit = Mathf.Max(3, Mathf.CeilToInt(maxPoints * 0.28f));
        float minDistanceSquared = Mathf.Pow(Mathf.Max(cellSize * 2.2f, profile.ChunkSize * 0.70f), 2.0f);

        SelectRequiredPointKind(
            candidates,
            selected,
            kindCounts,
            TerrainPointOfInterestKind.Oasis,
            maxPoints,
            perKindLimit,
            minDistanceSquared * 0.36f,
            cancellationToken);

        foreach (TerrainPointOfInterestKind kind in Enum.GetValues<TerrainPointOfInterestKind>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (PoiCandidate candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (candidate.Kind != kind)
                {
                    continue;
                }

                if (TrySelectPoint(
                    selected,
                    kindCounts,
                    candidate,
                    maxPoints,
                    perKindLimit,
                    minDistanceSquared * 0.48f,
                    enforcePerKindLimit: false))
                {
                    break;
                }
            }
        }

        SelectCoverageAnchors(
            candidates,
            selected,
            kindCounts,
            maxPoints,
            perKindLimit,
            minDistanceSquared,
            worldSize,
            cancellationToken);

        foreach (PoiCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TrySelectPoint(
                selected,
                kindCounts,
                candidate,
                maxPoints,
                perKindLimit,
                minDistanceSquared,
                enforcePerKindLimit: true);
        }

        return selected.ToArray();
    }

    private static void SelectRequiredPointKind(
        List<PoiCandidate> candidates,
        List<TerrainWorldPointOfInterest> selected,
        Dictionary<TerrainPointOfInterestKind, int> kindCounts,
        TerrainPointOfInterestKind requiredKind,
        int maxPoints,
        int perKindLimit,
        float minDistanceSquared,
        CancellationToken cancellationToken)
    {
        kindCounts.TryGetValue(requiredKind, out int existingCount);
        if (existingCount > 0)
        {
            return;
        }

        foreach (PoiCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (candidate.Kind != requiredKind)
            {
                continue;
            }

            if (TrySelectPoint(
                selected,
                kindCounts,
                candidate,
                maxPoints,
                perKindLimit,
                minDistanceSquared,
                enforcePerKindLimit: false))
            {
                return;
            }
        }
    }

    private static void SelectCoverageAnchors(
        List<PoiCandidate> candidates,
        List<TerrainWorldPointOfInterest> selected,
        Dictionary<TerrainPointOfInterestKind, int> kindCounts,
        int maxPoints,
        int perKindLimit,
        float minDistanceSquared,
        float worldSize,
        CancellationToken cancellationToken)
    {
        int targetCount = Mathf.Clamp(Mathf.CeilToInt(maxPoints * 0.44f), selected.Count, maxPoints);
        while (selected.Count < targetCount)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int bestIndex = -1;
            float bestScore = float.NegativeInfinity;
            float currentCoverage = ComputePointCoverage(selected, worldSize);

            for (int i = 0; i < candidates.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                PoiCandidate candidate = candidates[i];
                if (!CanSelectPoint(
                    selected,
                    kindCounts,
                    candidate,
                    maxPoints,
                    perKindLimit,
                    minDistanceSquared,
                    enforcePerKindLimit: true))
                {
                    continue;
                }

                float score = ScoreCoverageAnchor(candidate, selected, worldSize, currentCoverage);
                if (score <= bestScore)
                {
                    continue;
                }

                bestIndex = i;
                bestScore = score;
            }

            if (bestIndex < 0)
            {
                return;
            }

            TrySelectPoint(
                selected,
                kindCounts,
                candidates[bestIndex],
                maxPoints,
                perKindLimit,
                minDistanceSquared,
                enforcePerKindLimit: true);
        }
    }

    private static float ScoreCoverageAnchor(
        PoiCandidate candidate,
        List<TerrainWorldPointOfInterest> selected,
        float worldSize,
        float currentCoverage)
    {
        if (selected.Count == 0)
        {
            return candidate.Score;
        }

        float coverageGain = ComputeCoverageWithCandidate(selected, candidate.WorldPosition, worldSize) - currentCoverage;
        float distanceNovelty = ComputeNearestPointDistanceRatio(candidate.WorldPosition, selected, worldSize);
        float biomeBonus = candidate.BiomeKind is TerrainBiomeKind.Island or TerrainBiomeKind.Desert or TerrainBiomeKind.Oasis
            ? 0.08f
            : 0.0f;

        return coverageGain * 12.0f + distanceNovelty * 0.42f + candidate.Score * 0.30f + biomeBonus;
    }

    private static bool CanSelectPoint(
        List<TerrainWorldPointOfInterest> selected,
        Dictionary<TerrainPointOfInterestKind, int> kindCounts,
        PoiCandidate candidate,
        int maxPoints,
        int perKindLimit,
        float minDistanceSquared,
        bool enforcePerKindLimit)
    {
        if (selected.Count >= maxPoints)
        {
            return false;
        }

        kindCounts.TryGetValue(candidate.Kind, out int kindCount);
        if (enforcePerKindLimit && kindCount >= perKindLimit)
        {
            return false;
        }

        foreach (TerrainWorldPointOfInterest existing in selected)
        {
            if (existing.WorldPosition.DistanceSquaredTo(candidate.WorldPosition) < minDistanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySelectPoint(
        List<TerrainWorldPointOfInterest> selected,
        Dictionary<TerrainPointOfInterestKind, int> kindCounts,
        PoiCandidate candidate,
        int maxPoints,
        int perKindLimit,
        float minDistanceSquared,
        bool enforcePerKindLimit)
    {
        if (!CanSelectPoint(
            selected,
            kindCounts,
            candidate,
            maxPoints,
            perKindLimit,
            minDistanceSquared,
            enforcePerKindLimit))
        {
            return false;
        }

        kindCounts.TryGetValue(candidate.Kind, out int kindCount);
        int id = selected.Count;
        selected.Add(new TerrainWorldPointOfInterest(
            id,
            candidate.Kind,
            candidate.WorldPosition,
            candidate.GridX,
            candidate.GridY,
            candidate.Score,
            candidate.Height,
            candidate.ScenicPotential,
            candidate.Traversability,
            candidate.BiomeKind,
            candidate.LandscapeKind,
            ClassifySettlementTier(candidate),
            $"{candidate.Kind}_{candidate.GridX}_{candidate.GridY}_{id}"));
        kindCounts[candidate.Kind] = kindCount + 1;
        return true;
    }

    private static TerrainSettlementTier ClassifySettlementTier(PoiCandidate candidate)
    {
        if (candidate.Kind == TerrainPointOfInterestKind.Oasis)
        {
            return TerrainSettlementTier.OasisHub;
        }

        if (candidate.Kind != TerrainPointOfInterestKind.SettlementCandidate)
        {
            return TerrainSettlementTier.None;
        }

        float biomeScore = candidate.BiomeKind switch
        {
            TerrainBiomeKind.Plains => 1.0f,
            TerrainBiomeKind.Grassland => 0.92f,
            TerrainBiomeKind.Oasis => 0.88f,
            TerrainBiomeKind.Forest => 0.68f,
            TerrainBiomeKind.Coast => 0.58f,
            _ => 0.36f
        };
        float townScore =
            candidate.Score * 0.40f +
            candidate.Traversability * 0.22f +
            candidate.ResourcePotential * 0.20f +
            candidate.ScenicPotential * 0.08f +
            biomeScore * 0.10f;

        return townScore >= 0.84f
            ? TerrainSettlementTier.Town
            : TerrainSettlementTier.Village;
    }
}
