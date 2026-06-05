using System.Collections.Concurrent;
using Dao.Terrain;
using Godot;

namespace Dao.Terrain.Generation;

internal static class TerrainScenicLandmarkRuleCatalog
{
    private static readonly ConcurrentDictionary<string, TerrainScenicLandmarkRuleSetSnapshot> Snapshots = new();

    public static string DefaultHash { get; }
    public static TerrainScenicLandmarkRuleSetSnapshot Default { get; }

    static TerrainScenicLandmarkRuleCatalog()
    {
        Default = TerrainScenicLandmarkRuleSetSnapshot.CreateDefault();
        DefaultHash = Default.StableHash();
        Snapshots[DefaultHash] = Default;
    }

    public static string Register(TerrainScenicLandmarkRuleSet? ruleSet)
    {
        if (ruleSet is null)
        {
            return DefaultHash;
        }

        string hash = ruleSet.StableHash();
        Snapshots.TryAdd(hash, TerrainScenicLandmarkRuleSetSnapshot.FromResource(ruleSet));
        return hash;
    }

    public static TerrainScenicLandmarkRule Resolve(string? hash, TerrainLandmarkKind kind)
    {
        string key = string.IsNullOrWhiteSpace(hash) ? DefaultHash : hash;
        if (!Snapshots.TryGetValue(key, out TerrainScenicLandmarkRuleSetSnapshot? snapshot))
        {
            snapshot = Default;
        }

        return snapshot.Get(kind);
    }
}

internal sealed class TerrainScenicLandmarkRuleSetSnapshot
{
    private readonly TerrainScenicLandmarkRule _waterfall;
    private readonly TerrainScenicLandmarkRule _duneCrest;
    private readonly TerrainScenicLandmarkRule _desertMonolith;
    private readonly TerrainScenicLandmarkRule _canyonNeedle;
    private readonly TerrainScenicLandmarkRule _iceSpire;
    private readonly TerrainScenicLandmarkRule _naturalArch;
    private readonly TerrainScenicLandmarkRule _geothermalSpring;
    private readonly TerrainScenicLandmarkRule _glacialRidge;

    private TerrainScenicLandmarkRuleSetSnapshot(
        TerrainScenicLandmarkRule waterfall,
        TerrainScenicLandmarkRule duneCrest,
        TerrainScenicLandmarkRule desertMonolith,
        TerrainScenicLandmarkRule canyonNeedle,
        TerrainScenicLandmarkRule iceSpire,
        TerrainScenicLandmarkRule naturalArch,
        TerrainScenicLandmarkRule geothermalSpring,
        TerrainScenicLandmarkRule glacialRidge)
    {
        _waterfall = waterfall;
        _duneCrest = duneCrest;
        _desertMonolith = desertMonolith;
        _canyonNeedle = canyonNeedle;
        _iceSpire = iceSpire;
        _naturalArch = naturalArch;
        _geothermalSpring = geothermalSpring;
        _glacialRidge = glacialRidge;
    }

    public static TerrainScenicLandmarkRuleSetSnapshot CreateDefault()
    {
        return new TerrainScenicLandmarkRuleSetSnapshot(
            new TerrainScenicLandmarkRule(0.74f, 4.8f, 3.2f, new Color(0.30f, 0.62f, 0.82f)),
            new TerrainScenicLandmarkRule(0.68f, 4.4f, 2.6f, new Color(0.76f, 0.58f, 0.30f)),
            new TerrainScenicLandmarkRule(0.66f, 3.6f, 2.8f, new Color(0.62f, 0.42f, 0.24f)),
            new TerrainScenicLandmarkRule(0.68f, 4.2f, 3.0f, new Color(0.58f, 0.36f, 0.24f)),
            new TerrainScenicLandmarkRule(0.66f, 3.6f, 2.4f, new Color(0.62f, 0.76f, 0.86f)),
            new TerrainScenicLandmarkRule(0.64f, 4.2f, 2.8f, new Color(0.66f, 0.44f, 0.28f)),
            new TerrainScenicLandmarkRule(0.64f, 3.8f, 2.2f, new Color(0.24f, 0.58f, 0.62f)),
            new TerrainScenicLandmarkRule(0.64f, 4.4f, 2.6f, new Color(0.70f, 0.82f, 0.88f)));
    }

    public static TerrainScenicLandmarkRuleSetSnapshot FromResource(TerrainScenicLandmarkRuleSet ruleSet)
    {
        TerrainScenicLandmarkRuleSetSnapshot defaults = CreateDefault();
        return new TerrainScenicLandmarkRuleSetSnapshot(
            FromRule(ruleSet.Waterfall, defaults._waterfall),
            FromRule(ruleSet.DuneCrest, defaults._duneCrest),
            FromRule(ruleSet.DesertMonolith, defaults._desertMonolith),
            FromRule(ruleSet.CanyonNeedle, defaults._canyonNeedle),
            FromRule(ruleSet.IceSpire, defaults._iceSpire),
            FromRule(ruleSet.NaturalArch, defaults._naturalArch),
            FromRule(ruleSet.GeothermalSpring, defaults._geothermalSpring),
            FromRule(ruleSet.GlacialRidge, defaults._glacialRidge));
    }

    public string StableHash()
    {
        return TerrainScenicLandmarkRuleSet.ComputeHash(
            _waterfall,
            _duneCrest,
            _desertMonolith,
            _canyonNeedle,
            _iceSpire,
            _naturalArch,
            _geothermalSpring,
            _glacialRidge);
    }

    public TerrainScenicLandmarkRule Get(TerrainLandmarkKind kind)
    {
        return kind switch
        {
            TerrainLandmarkKind.Waterfall => _waterfall,
            TerrainLandmarkKind.DuneCrest => _duneCrest,
            TerrainLandmarkKind.DesertMonolith => _desertMonolith,
            TerrainLandmarkKind.CanyonNeedle => _canyonNeedle,
            TerrainLandmarkKind.IceSpire => _iceSpire,
            TerrainLandmarkKind.NaturalArch => _naturalArch,
            TerrainLandmarkKind.GeothermalSpring => _geothermalSpring,
            TerrainLandmarkKind.GlacialRidge => _glacialRidge,
            _ => _waterfall
        };
    }

    private static TerrainScenicLandmarkRule FromRule(TerrainNaturalLandmarkRuleResource? resource, TerrainScenicLandmarkRule fallback)
    {
        if (resource is null)
        {
            return fallback;
        }

        return new TerrainScenicLandmarkRule(
            resource.Threshold,
            resource.BaseScale,
            resource.ScoreScale,
            resource.BaseColor);
    }
}

internal readonly record struct TerrainScenicLandmarkRule(
    float Threshold,
    float BaseScale,
    float ScoreScale,
    Color BaseColor);
