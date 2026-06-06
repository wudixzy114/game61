using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanner
{
    private static class PoiSelectionService
    {
        internal static TerrainWorldPointOfInterest[] SelectPointsOfInterest(
            List<PoiCandidate> candidates,
            TerrainGenerationProfile profile,
            TerrainPointOfInterestRuleSetSnapshot poiRules,
            int maxPoints,
            float cellSize,
            float worldSize,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TerrainPoiSelectionPolicy rules = poiRules.Selection;
            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            var selected = new List<TerrainWorldPointOfInterest>(maxPoints);
            var kindCounts = new Dictionary<TerrainPointOfInterestKind, int>();
            int perKindLimit = Mathf.Max(rules.MinPerKindLimit, Mathf.CeilToInt(maxPoints * rules.PerKindLimitRatio));
            float minDistanceSquared = Mathf.Pow(
                Mathf.Max(cellSize * rules.MinDistanceCellMultiplier, profile.ChunkSize * rules.MinDistanceChunkMultiplier),
                2.0f);

            SelectRequiredPointKind(
                candidates,
                selected,
                kindCounts,
                TerrainPointOfInterestKind.Oasis,
                poiRules.SettlementTier,
                maxPoints,
                perKindLimit,
                minDistanceSquared * rules.RequiredKindDistanceFactor,
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
                        poiRules.SettlementTier,
                        maxPoints,
                        perKindLimit,
                        minDistanceSquared * rules.KindSweepDistanceFactor,
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
                rules,
                poiRules.SettlementTier,
                cancellationToken);

            foreach (PoiCandidate candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                TrySelectPoint(
                    selected,
                    kindCounts,
                    candidate,
                    poiRules.SettlementTier,
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
            TerrainSettlementTierScoring settlementTierRules,
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
                    settlementTierRules,
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
            TerrainPoiSelectionPolicy rules,
            TerrainSettlementTierScoring settlementTierRules,
            CancellationToken cancellationToken)
        {
            int targetCount = Mathf.Clamp(Mathf.CeilToInt(maxPoints * rules.CoverageAnchorTargetRatio), selected.Count, maxPoints);
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

                    float score = ScoreCoverageAnchor(candidate, selected, worldSize, currentCoverage, rules);
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
                    settlementTierRules,
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
            float currentCoverage,
            TerrainPoiSelectionPolicy rules)
        {
            if (selected.Count == 0)
            {
                return candidate.Score;
            }

            float coverageGain = ComputeCoverageWithCandidate(selected, candidate.WorldPosition, worldSize) - currentCoverage;
            float distanceNovelty = ComputeNearestPointDistanceRatio(candidate.WorldPosition, selected, worldSize);
            float biomeBonus = candidate.BiomeKind is TerrainBiomeKind.Island or TerrainBiomeKind.Desert or TerrainBiomeKind.Oasis
                ? rules.ExoticBiomeBonus
                : 0.0f;

            return coverageGain * rules.CoverageGainWeight +
                distanceNovelty * rules.DistanceNoveltyWeight +
                candidate.Score * rules.CandidateScoreWeight +
                biomeBonus;
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
            TerrainSettlementTierScoring settlementTierRules,
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
                PoiCandidateScorer.ClassifySettlementTier(candidate, settlementTierRules),
                $"{candidate.Kind}_{candidate.GridX}_{candidate.GridY}_{id}"));
            kindCounts[candidate.Kind] = kindCount + 1;
            return true;
        }
    }
}
