using System.Collections.Concurrent;
using Dao.Terrain;

namespace Dao.Terrain.Generation;

internal static class TerrainPointOfInterestRuleCatalog
{
    private static readonly ConcurrentDictionary<string, TerrainPointOfInterestRuleSetSnapshot> Snapshots = new();

    public static string DefaultHash { get; }
    public static TerrainPointOfInterestRuleSetSnapshot Default { get; }

    static TerrainPointOfInterestRuleCatalog()
    {
        Default = TerrainWorldPlannerDefaultRules.CreateDefaultPointOfInterestRules();
        DefaultHash = Default.StableHash();
        Snapshots[DefaultHash] = Default;
    }

    public static string Register(TerrainPointOfInterestRuleSet? ruleSet)
    {
        if (ruleSet is null)
        {
            return DefaultHash;
        }

        TerrainPointOfInterestRuleSetSnapshot snapshot = new(
            ruleSet.CreateThresholds(),
            ruleSet.CreateScoring(),
            ruleSet.CreateSelection(),
            ruleSet.CreateSettlementTierScoring());
        string hash = snapshot.StableHash();
        Snapshots.TryAdd(hash, snapshot);
        return hash;
    }

    public static TerrainPointOfInterestRuleSetSnapshot Resolve(string? hash)
    {
        string key = string.IsNullOrWhiteSpace(hash) ? DefaultHash : hash;
        return Snapshots.TryGetValue(key, out TerrainPointOfInterestRuleSetSnapshot snapshot)
            ? snapshot
            : Default;
    }
}
