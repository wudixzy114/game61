using System.Collections.Concurrent;
using Dao.Terrain;

namespace Dao.Terrain.Generation;

internal static class TerrainRouteRuleCatalog
{
    private static readonly ConcurrentDictionary<string, TerrainRouteRuleSetSnapshot> Snapshots = new();

    public static string DefaultHash { get; }
    public static TerrainRouteRuleSetSnapshot Default { get; }

    static TerrainRouteRuleCatalog()
    {
        Default = TerrainWorldPlannerDefaultRules.CreateDefaultRouteRules();
        DefaultHash = Default.StableHash();
        Snapshots[DefaultHash] = Default;
    }

    public static string Register(TerrainRouteRuleSet? ruleSet)
    {
        if (ruleSet is null)
        {
            return DefaultHash;
        }

        TerrainRouteRuleSetSnapshot snapshot = new(
            ruleSet.CreateSecondaryRoutePolicy(),
            ruleSet.CreateSettlementRoutePolicy(),
            ruleSet.CreateSettlementRouteScoring(),
            ruleSet.CreateSecondaryRouteScoring(),
            ruleSet.CreatePathCostPolicy(),
            ruleSet.CreateClassificationPolicy(),
            ruleSet.MinimumSettlementConnectorRoutes);
        string hash = snapshot.StableHash();
        Snapshots.TryAdd(hash, snapshot);
        return hash;
    }

    public static TerrainRouteRuleSetSnapshot Resolve(string? hash)
    {
        string key = string.IsNullOrWhiteSpace(hash) ? DefaultHash : hash;
        return Snapshots.TryGetValue(key, out TerrainRouteRuleSetSnapshot snapshot)
            ? snapshot
            : Default;
    }
}
