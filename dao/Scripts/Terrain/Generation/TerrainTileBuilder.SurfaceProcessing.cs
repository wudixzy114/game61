using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private readonly record struct TerrainTileSurfaceBuildContext(
        TerrainGenerationProfile Profile,
        TerrainModificationLayer ModificationLayer,
        int Resolution,
        int VertexCountPerSide,
        int VertexCount,
        float Step,
        Vector2 Origin,
        bool UseNativeFields,
        bool NativeFieldsContainDerivedData,
        float[] NativeFieldSamples,
        bool UseNativeHeights,
        float[] NativeHeights,
        float ManagedLandBalanceOffset,
        bool HasCorridors,
        TerrainRouteCorridorIndex RouteCorridors,
        TerrainRouteCorridorSegment[] CorridorSegments,
        bool HasPointInfluences,
        TerrainWorldPointOfInterest[] PointInfluences,
        bool HasSettlementLayouts,
        TerrainSettlementLayoutDescriptor[] SettlementLayouts,
        bool UseParallelSurfaceProcessing,
        Vector3[] SurfaceVertices,
        Vector3[] SurfaceNormals,
        Vector2[] SurfaceUvs,
        Color[] SurfaceColors,
        float[] Heights,
        TerrainWorldField[] Fields,
        TerrainRouteCorridorSample[] CorridorSamples,
        TerrainPointFootprintSample[] FootprintSamples,
        TerrainSettlementLayoutSample[] SettlementLayoutSamples,
        CancellationToken CancellationToken);

    private readonly record struct TerrainTileSurfaceHeightRange(
        float MinHeight,
        float MaxHeight);

    private static TerrainTileSurfaceHeightRange BuildSurfaceGeometry(TerrainTileSurfaceBuildContext context)
    {
        return TerrainTileSurfaceBuilderService.BuildSurfaceGeometry(context);
    }

    private static void SampleSurfaceVertices(TerrainTileSurfaceBuildContext context)
    {
        _ = context;
    }

    private static TerrainTileSurfaceHeightRange CalculateSurfaceHeightRange(float[] heights, int vertexCount)
    {
        _ = heights;
        _ = vertexCount;
        return default;
    }

    private static void ColorSurfaceVertices(TerrainTileSurfaceBuildContext context)
    {
        _ = context;
    }
}
