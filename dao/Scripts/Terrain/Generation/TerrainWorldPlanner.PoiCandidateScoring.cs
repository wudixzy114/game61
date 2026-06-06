using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanner
{
    private static void AddPoiCandidates(
        List<PoiCandidate> candidates,
        TerrainGenerationProfile profile,
        TerrainPointOfInterestRuleSetSnapshot rules,
        TerrainWorldField field,
        int gridX,
        int gridY)
    {
        PoiCandidateScorer.AddCandidates(candidates, profile, rules, field, gridX, gridY);
    }

    private static TerrainSettlementTier ClassifySettlementTier(
        PoiCandidate candidate,
        TerrainSettlementTierScoring rules)
    {
        return PoiCandidateScorer.ClassifySettlementTier(candidate, rules);
    }
}
