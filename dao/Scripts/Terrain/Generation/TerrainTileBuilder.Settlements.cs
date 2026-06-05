using System;
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

        int count = point.SettlementTier switch
        {
            TerrainSettlementTier.Town => 17,
            TerrainSettlementTier.OasisHub => 13,
            _ => 9
        };
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


    private static TerrainLandmarkKind SettlementInteriorKind(TerrainSettlementTier tier, int index)
    {
        return tier switch
        {
            TerrainSettlementTier.Town => index == 0
                ? TerrainLandmarkKind.SettlementPlaza
                : index is 1 or 6 or 11
                ? TerrainLandmarkKind.MarketStall
                : index == 2
                ? TerrainLandmarkKind.WatchTower
                : index % 8 == 0
                ? TerrainLandmarkKind.SettlementPlaza
                : TerrainLandmarkKind.TownBlock,
            TerrainSettlementTier.OasisHub => index == 0
                ? TerrainLandmarkKind.OasisPool
                : index is 1 or 6
                ? TerrainLandmarkKind.OasisGarden
                : index == 2
                ? TerrainLandmarkKind.MarketStall
                : index == 3
                ? TerrainLandmarkKind.WatchTower
                : index % 7 == 0
                ? TerrainLandmarkKind.SettlementPlaza
                : TerrainLandmarkKind.OasisCanopy,
            TerrainSettlementTier.Village => index == 0
                ? TerrainLandmarkKind.SettlementPlaza
                : index == 1
                ? TerrainLandmarkKind.VillageWell
                : index == 2
                ? TerrainLandmarkKind.WatchTower
                : TerrainLandmarkKind.VillageHouse,
            _ => TerrainLandmarkKind.Settlement
        };
    }

    private static Vector2 SettlementInteriorOffset(
        TerrainWorldPointOfInterest point,
        TerrainLandmarkKind kind,
        float radius,
        Vector2 axis,
        Vector2 side,
        int index,
        int count)
    {
        if (kind == TerrainLandmarkKind.OasisPool)
        {
            return Vector2.Zero;
        }

        if (kind == TerrainLandmarkKind.SettlementPlaza)
        {
            return point.SettlementTier switch
            {
                TerrainSettlementTier.Town => index == 0
                    ? Vector2.Zero
                    : axis * radius * 0.24f + side * radius * -0.20f,
                TerrainSettlementTier.OasisHub => axis * radius * 0.24f,
                _ => Vector2.Zero
            };
        }

        if (kind == TerrainLandmarkKind.VillageWell)
        {
            return axis * radius * 0.10f + side * radius * -0.10f;
        }

        if (kind == TerrainLandmarkKind.MarketStall)
        {
            float marketAlong = radius * Mathf.Lerp(-0.18f, 0.26f, Hash01(point.Id, index, 1305, 21));
            float acrossSign = Hash01(point.Id, index, 1307, 25) < 0.5f ? -1.0f : 1.0f;
            float marketAcross = acrossSign * radius * Mathf.Lerp(0.13f, 0.23f, Hash01(point.Id, index, 1309, 27));
            return axis * marketAlong + side * marketAcross;
        }

        if (kind == TerrainLandmarkKind.WatchTower)
        {
            float alongSign = Hash01(point.Id, index, 1311, 29) < 0.5f ? -1.0f : 1.0f;
            float acrossSign = Hash01(point.Id, index, 1313, 31) < 0.5f ? -1.0f : 1.0f;
            float towerAlong = alongSign * radius * Mathf.Lerp(0.38f, 0.52f, Hash01(point.Id, index, 1315, 33));
            float towerAcross = acrossSign * radius * Mathf.Lerp(0.22f, 0.40f, Hash01(point.Id, index, 1317, 35));
            return axis * towerAlong + side * towerAcross;
        }

        if (kind == TerrainLandmarkKind.OasisGarden)
        {
            float gardenAngle = (index / (float)Mathf.Max(1, count)) * Mathf.Tau +
                Hash01(point.Id, index, 1318, 36) * 0.54f;
            float gardenRing = radius * Mathf.Lerp(0.20f, 0.34f, Hash01(point.Id, index, 1320, 38));
            return axis * (Mathf.Cos(gardenAngle) * gardenRing) + side * (Mathf.Sin(gardenAngle) * gardenRing);
        }

        if (point.SettlementTier == TerrainSettlementTier.Town)
        {
            const int columns = 4;
            int rows = Mathf.CeilToInt(count / (float)columns);
            int column = index % columns;
            int row = index / columns;
            float blockX = (column - (columns - 1) * 0.5f) * radius * 0.18f;
            float blockZ = (row - (rows - 1) * 0.5f) * radius * 0.20f;
            float jitterX = (Hash01(point.Id, index, 1301, 17) - 0.5f) * radius * 0.055f;
            float jitterZ = (Hash01(point.Id, index, 1303, 19) - 0.5f) * radius * 0.055f;
            return axis * (blockX + jitterX) + side * (blockZ + jitterZ);
        }

        float angle = (index / (float)Mathf.Max(1, count)) * Mathf.Tau +
            Hash01(point.Id, index, 1319, 23) * 0.38f;
        float ring = point.SettlementTier == TerrainSettlementTier.OasisHub
            ? radius * Mathf.Lerp(0.34f, 0.56f, Hash01(point.Id, index, 1321, 29))
            : radius * Mathf.Lerp(0.20f, 0.48f, Hash01(point.Id, index, 1327, 31));
        float along = Mathf.Cos(angle) * ring;
        float across = Mathf.Sin(angle) * ring;
        return axis * along + side * across;
    }

    private static Vector2 SettlementLayoutAxis(
        TerrainWorldPointOfInterest point,
        TerrainRouteCorridorSegment[] corridorSegments,
        TerrainGenerationProfile profile)
    {
        Vector2 best = Vector2.Zero;
        float bestDistanceSquared = float.PositiveInfinity;
        float radius = TerrainPointOfInterestIndex.FootprintRadiusFor(point, profile) * 2.25f;
        float maxDistanceSquared = radius * radius;

        foreach (TerrainRouteCorridorSegment segment in corridorSegments)
        {
            float t = ClosestPointT(point.WorldPosition, segment.From, segment.To);
            Vector2 closest = segment.From.Lerp(segment.To, t);
            float distanceSquared = point.WorldPosition.DistanceSquaredTo(closest);
            if (distanceSquared >= bestDistanceSquared || distanceSquared > maxDistanceSquared)
            {
                continue;
            }

            Vector2 direction = segment.To - segment.From;
            if (direction.LengthSquared() <= 0.001f)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            best = direction.Normalized();
        }

        if (best != Vector2.Zero)
        {
            return best;
        }

        float angle = Hash01(
            Mathf.FloorToInt(point.WorldPosition.X),
            Mathf.FloorToInt(point.WorldPosition.Y),
            point.Id * 7919,
            profile.Seed + 223) * Mathf.Tau;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static float ClosestPointT(Vector2 point, Vector2 from, Vector2 to)
    {
        Vector2 segment = to - from;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return 0.0f;
        }

        return Mathf.Clamp((point - from).Dot(segment) / lengthSquared, 0.0f, 1.0f);
    }

    private static bool TileIntersectsCircle(Vector2 origin, float chunkSize, Vector2 center, float radius)
    {
        float nearestX = Mathf.Clamp(center.X, origin.X, origin.X + chunkSize);
        float nearestZ = Mathf.Clamp(center.Y, origin.Y, origin.Y + chunkSize);
        float dx = center.X - nearestX;
        float dz = center.Y - nearestZ;
        return (dx * dx) + (dz * dz) <= radius * radius;
    }

    private static float SettlementInteriorRotation(
        TerrainWorldPointOfInterest point,
        Vector2 axis,
        int index,
        TerrainGenerationProfile profile)
    {
        float baseRotation = Mathf.Atan2(axis.Y, axis.X);
        float jitter = (Hash01(point.Id, index, 1361, profile.Seed + 227) - 0.5f) * 0.42f;
        return point.SettlementTier == TerrainSettlementTier.OasisHub
            ? baseRotation + Mathf.Pi * 0.5f + jitter
            : baseRotation + jitter;
    }

    private static float SettlementInteriorScale(
        TerrainSettlementTier tier,
        TerrainLandmarkKind kind,
        float score,
        TerrainTileCoord coord,
        int index,
        TerrainGenerationProfile profile)
    {
        float quality = Mathf.Lerp(0.90f, 1.18f, Mathf.Clamp(score, 0.0f, 1.0f));
        float jitter = Mathf.Lerp(0.84f, 1.20f, Hash01(coord.X, coord.Z, index * 1399, profile.Seed + 229));
        float baseScale = kind switch
        {
            TerrainLandmarkKind.VillageWell => 1.95f,
            TerrainLandmarkKind.MarketStall => 2.18f,
            TerrainLandmarkKind.WatchTower => 3.25f,
            TerrainLandmarkKind.OasisGarden => 2.45f,
            _ => tier switch
            {
                TerrainSettlementTier.Town => 2.95f,
                TerrainSettlementTier.OasisHub => 2.55f,
                _ => 2.30f
            }
        };

        return baseScale * quality * jitter;
    }

    private static Color SettlementInteriorColor(
        TerrainLandmarkKind kind,
        TerrainWorldField field,
        TerrainTileCoord coord,
        int index,
        TerrainGenerationProfile profile)
    {
        Color baseColor = LandmarkColorFor(kind, field);
        Color variation = kind switch
        {
            TerrainLandmarkKind.TownBlock => new Color(0.62f, 0.42f, 0.30f),
            TerrainLandmarkKind.OasisCanopy => new Color(0.12f, 0.58f, 0.44f),
            TerrainLandmarkKind.SettlementPlaza => new Color(0.58f, 0.50f, 0.38f),
            TerrainLandmarkKind.OasisPool => new Color(0.10f, 0.36f, 0.46f),
            TerrainLandmarkKind.VillageWell => new Color(0.38f, 0.46f, 0.42f),
            TerrainLandmarkKind.MarketStall => new Color(0.74f, 0.48f, 0.24f),
            TerrainLandmarkKind.WatchTower => new Color(0.54f, 0.42f, 0.28f),
            TerrainLandmarkKind.OasisGarden => new Color(0.12f, 0.62f, 0.34f),
            _ => new Color(0.58f, 0.48f, 0.31f)
        };
        float blend = Mathf.Lerp(0.18f, 0.42f, Hash01(coord.X, coord.Z, index * 1423, profile.Seed + 233));
        return baseColor.Lerp(variation, blend);
    }

}
