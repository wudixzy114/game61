using System.Collections.Concurrent;
using Dao.Terrain;

namespace Dao.Terrain.Generation;

internal static class TerrainScatterRuleCatalog
{
    private static readonly ConcurrentDictionary<string, TerrainScatterRuleSetSnapshot> Snapshots = new();

    public static string DefaultHash { get; }
    public static TerrainScatterRuleSetSnapshot Default { get; }

    static TerrainScatterRuleCatalog()
    {
        Default = TerrainTileBuilderSurfaceScatterDefaults.CreateDefault();
        DefaultHash = Default.StableHash();
        Snapshots[DefaultHash] = Default;
    }

    public static string Register(TerrainScatterRuleSet? ruleSet)
    {
        if (ruleSet is null)
        {
            return DefaultHash;
        }

        TerrainScatterRuleSetSnapshot snapshot = ruleSet.CreateSnapshot();
        string hash = snapshot.StableHash();
        Snapshots.TryAdd(hash, snapshot);
        return hash;
    }

    public static TerrainScatterRuleSetSnapshot Resolve(string? hash)
    {
        string key = string.IsNullOrWhiteSpace(hash) ? DefaultHash : hash;
        return Snapshots.TryGetValue(key, out TerrainScatterRuleSetSnapshot snapshot)
            ? snapshot
            : Default;
    }
}
