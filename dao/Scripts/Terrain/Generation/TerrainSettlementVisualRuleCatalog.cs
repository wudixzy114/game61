using System.Collections.Concurrent;
using Dao.Terrain;

namespace Dao.Terrain.Generation;

internal static class TerrainSettlementVisualRuleCatalog
{
    private static readonly ConcurrentDictionary<string, TerrainSettlementVisualRuleSetSnapshot> Snapshots = new();

    public static string DefaultHash { get; }
    public static TerrainSettlementVisualRuleSetSnapshot Default { get; }

    static TerrainSettlementVisualRuleCatalog()
    {
        Default = TerrainTileBuilderSettlementDefaults.CreateDefault();
        DefaultHash = Default.StableHash();
        Snapshots[DefaultHash] = Default;
    }

    public static string Register(TerrainSettlementVisualRuleSet? ruleSet)
    {
        if (ruleSet is null)
        {
            return DefaultHash;
        }

        TerrainSettlementVisualRuleSetSnapshot snapshot = ruleSet.CreateSnapshot();
        string hash = snapshot.StableHash();
        Snapshots.TryAdd(hash, snapshot);
        return hash;
    }

    public static TerrainSettlementVisualRuleSetSnapshot Resolve(string? hash)
    {
        string key = string.IsNullOrWhiteSpace(hash) ? DefaultHash : hash;
        return Snapshots.TryGetValue(key, out TerrainSettlementVisualRuleSetSnapshot snapshot)
            ? snapshot
            : Default;
    }
}
