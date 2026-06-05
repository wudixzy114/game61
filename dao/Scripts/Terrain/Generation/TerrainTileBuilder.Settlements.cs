using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Partial class handling POI settlement placement, interior scatter, footprints, and layout sampling for tile generation.</summary>
public static partial class TerrainTileBuilder
{
    private static void AddPlannedPoiLandmarks(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        float[] heights,
        TerrainWorldField[] fields,
        Vector3[] normals,
        TerrainWorldPointOfInterest[] plannedPoints,
        TerrainRouteCorridorSegment[] corridorSegments,
        List<TerrainScatterInstance> scatter,
        List<TerrainLandmarkData> landmarks)
    {
        if (plannedPoints.Length == 0)
        {
            return;
        }

        Vector2 origin = coord.Origin(profile.ChunkSize);
        foreach (TerrainWorldPointOfInterest point in plannedPoints)
        {
            AddSettlementInteriorScatter(
                coord,
                profile,
                resolution,
                vertexCountPerSide,
                step,
                heights,
                fields,
                point,
                corridorSegments,
                origin,
                scatter);
            AddSettlementGatewayScatter(
                profile,
                resolution,
                vertexCountPerSide,
                step,
                heights,
                fields,
                point,
                corridorSegments,
                origin,
                scatter);

            float localX = point.WorldPosition.X - origin.X;
            float localZ = point.WorldPosition.Y - origin.Y;
            if (localX < 0.0f || localZ < 0.0f || localX > profile.ChunkSize || localZ > profile.ChunkSize)
            {
                continue;
            }

            float height = SampleHeightBilinear(localX, localZ, resolution, step, heights, vertexCountPerSide);
            if (height < profile.SeaLevel - 2.0f)
            {
                continue;
            }

            Vector3 normal = SampleNearestNormal(localX, localZ, resolution, step, normals, vertexCountPerSide);
            float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
            TerrainWorldField field = SampleFieldBilinear(localX, localZ, resolution, step, fields, vertexCountPerSide);
            TerrainLandmarkKind kind = LandmarkKindFor(point);
            float score = Mathf.Clamp(
                point.Score * 0.70f +
                field.ScenicPotential * 0.16f +
                field.Traversability * 0.10f +
                (1.0f - Mathf.Clamp(slope * 1.8f, 0.0f, 1.0f)) * 0.04f,
                0.0f,
                1.0f);
            float rotation = Hash01(coord.X, coord.Z, point.Id * 104_729, profile.Seed + 211) * Mathf.Pi * 2.0f;
            float scale = LandmarkScaleFor(kind, point.Score);
            Color tint = LandmarkColorFor(kind, field);

            var localPosition = new Vector3(localX, height, localZ);
            landmarks.Add(new TerrainLandmarkData(kind, localPosition, score, $"POI_{point.Id:00}_{point.Kind}"));
            scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Landmark, localPosition, rotation, scale, tint, kind));
        }
    }

    private static void AddSettlementInteriorScatter(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        float[] heights,
        TerrainWorldField[] fields,
        TerrainWorldPointOfInterest point,
        TerrainRouteCorridorSegment[] corridorSegments,
        Vector2 origin,
        List<TerrainScatterInstance> scatter)
    {
        if (point.SettlementTier == TerrainSettlementTier.None)
        {
            return;
        }

        float radius = TerrainPointOfInterestIndex.FootprintRadiusFor(point, profile);
        if (!TileIntersectsCircle(origin, profile.ChunkSize, point.WorldPosition, radius))
        {
            return;
        }

        int count = TerrainSettlementRules.InteriorCount(point.SettlementTier);
        Vector2 axis = SettlementLayoutAxis(point, corridorSegments, profile);
        Vector2 side = new(-axis.Y, axis.X);

        for (int i = 0; i < count; i++)
        {
            TerrainLandmarkKind kind = SettlementInteriorKind(point.SettlementTier, i);
            Vector2 offset = SettlementInteriorOffset(point, kind, radius, axis, side, i, count);
            Vector2 world = point.WorldPosition + offset;
            float localX = world.X - origin.X;
            float localZ = world.Y - origin.Y;
            if (localX < 0.0f || localZ < 0.0f || localX > profile.ChunkSize || localZ > profile.ChunkSize)
            {
                continue;
            }

            float height = SampleHeightBilinear(localX, localZ, resolution, step, heights, vertexCountPerSide);
            if (height < profile.SeaLevel - 2.0f)
            {
                continue;
            }

            TerrainWorldField field = SampleFieldBilinear(localX, localZ, resolution, step, fields, vertexCountPerSide);
            float rotation = SettlementInteriorRotation(point, axis, i, profile);
            float scale = SettlementInteriorScale(point.SettlementTier, kind, point.Score, coord, i, profile);
            Color tint = SettlementInteriorColor(kind, field, coord, i, profile);
            scatter.Add(new TerrainScatterInstance(
                TerrainScatterKind.Landmark,
                new Vector3(localX, height, localZ),
                rotation,
                scale,
                tint,
                kind));
        }
    }
}
