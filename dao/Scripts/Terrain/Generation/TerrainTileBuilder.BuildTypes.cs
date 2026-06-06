using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private enum TerrainTileSamplingBackendMode
    {
        Adaptive,
        Managed,
        Native
    }

    private enum TerrainWaterSurfaceKind
    {
        None,
        Lake,
        River,
        Oasis
    }

    private readonly record struct TerrainTileSamplingDecisionKey(
        TerrainGenerationProfile Profile,
        int Lod,
        int Resolution);

    private readonly record struct TerrainTileSamplingDecision(
        bool UseNative,
        double ManagedMillisecondsPerTile,
        double NativeMillisecondsPerTile,
        double Speedup,
        int Resolution,
        string Reason)
    {
        public static TerrainTileSamplingDecision Managed(string reason)
        {
            return new TerrainTileSamplingDecision(
                false,
                0.0,
                0.0,
                0.0,
                0,
                reason);
        }
    }

    private readonly record struct TerrainTileNativeSamplingState(
        bool UseNativeFields,
        bool NativeFieldsContainDerivedData,
        float[] NativeFieldSamples,
        bool ReturnNativeFieldSamples,
        bool UseNativeHeights,
        float[] NativeHeights,
        float ManagedLandBalanceOffset);

    private readonly record struct TerrainTileFeaturePreparation(
        TerrainRouteCorridorSegment[] CorridorSegments,
        bool HasCorridors,
        TerrainWorldPointOfInterest[] PointInfluences,
        bool HasPointInfluences,
        TerrainSettlementLayoutDescriptor[] SettlementLayouts,
        bool HasSettlementLayouts);

    private readonly record struct TerrainTileScratchBuffers(
        Vector3[] SurfaceVertices,
        Vector3[] SurfaceNormals,
        Vector2[] SurfaceUvs,
        Color[] SurfaceColors,
        float[] Heights,
        TerrainWorldField[] Fields,
        TerrainRouteCorridorSample[] CorridorSamples,
        TerrainPointFootprintSample[] FootprintSamples,
        TerrainSettlementLayoutSample[] SettlementLayoutSamples);
}
