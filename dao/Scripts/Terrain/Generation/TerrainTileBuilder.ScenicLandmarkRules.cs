using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static TerrainScenicLandmarkRule GetNaturalLandmarkRule(TerrainGenerationProfile profile, TerrainLandmarkKind kind)
    {
        return TerrainScenicLandmarkRuleCatalog.Resolve(profile.ScenicLandmarkRuleSetHash, kind);
    }
}
