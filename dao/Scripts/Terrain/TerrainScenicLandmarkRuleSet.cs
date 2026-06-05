using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain;

/// <summary>Optional scenic landmark rule set resource for data-driven natural landmark tuning.</summary>
[GlobalClass]
public partial class TerrainScenicLandmarkRuleSet : Resource
{
    [Export] public TerrainNaturalLandmarkRuleResource? Waterfall { get; set; }
    [Export] public TerrainNaturalLandmarkRuleResource? DuneCrest { get; set; }
    [Export] public TerrainNaturalLandmarkRuleResource? DesertMonolith { get; set; }
    [Export] public TerrainNaturalLandmarkRuleResource? CanyonNeedle { get; set; }
    [Export] public TerrainNaturalLandmarkRuleResource? IceSpire { get; set; }
    [Export] public TerrainNaturalLandmarkRuleResource? NaturalArch { get; set; }
    [Export] public TerrainNaturalLandmarkRuleResource? GeothermalSpring { get; set; }
    [Export] public TerrainNaturalLandmarkRuleResource? GlacialRidge { get; set; }

    /// <summary>Computes a stable content hash for this scenic landmark rule set.</summary>
    public string StableHash()
    {
        return ComputeHash(
            ToRule(Waterfall),
            ToRule(DuneCrest),
            ToRule(DesertMonolith),
            ToRule(CanyonNeedle),
            ToRule(IceSpire),
            ToRule(NaturalArch),
            ToRule(GeothermalSpring),
            ToRule(GlacialRidge));
    }

    internal static string ComputeHash(
        TerrainScenicLandmarkRule waterfall,
        TerrainScenicLandmarkRule duneCrest,
        TerrainScenicLandmarkRule desertMonolith,
        TerrainScenicLandmarkRule canyonNeedle,
        TerrainScenicLandmarkRule iceSpire,
        TerrainScenicLandmarkRule naturalArch,
        TerrainScenicLandmarkRule geothermalSpring,
        TerrainScenicLandmarkRule glacialRidge)
    {
        var builder = new StringBuilder(512);
        Append(builder, nameof(Waterfall), waterfall);
        Append(builder, nameof(DuneCrest), duneCrest);
        Append(builder, nameof(DesertMonolith), desertMonolith);
        Append(builder, nameof(CanyonNeedle), canyonNeedle);
        Append(builder, nameof(IceSpire), iceSpire);
        Append(builder, nameof(NaturalArch), naturalArch);
        Append(builder, nameof(GeothermalSpring), geothermalSpring);
        Append(builder, nameof(GlacialRidge), glacialRidge);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static TerrainScenicLandmarkRule ToRule(TerrainNaturalLandmarkRuleResource? rule)
    {
        return rule is null
            ? default
            : new TerrainScenicLandmarkRule(rule.Threshold, rule.BaseScale, rule.ScoreScale, rule.BaseColor);
    }

    private static void Append(StringBuilder builder, string name, TerrainScenicLandmarkRule rule)
    {
        builder.Append(name).Append('=');
        builder
            .Append(rule.Threshold.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.BaseScale.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.ScoreScale.ToString("G9", CultureInfo.InvariantCulture)).Append('|')
            .Append(rule.BaseColor.ToHtml()).Append(';');
    }
}
