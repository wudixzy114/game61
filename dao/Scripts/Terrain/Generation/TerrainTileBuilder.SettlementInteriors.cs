using System;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
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
        TerrainGenerationProfile profile,
        TerrainSettlementVisualRuleSetSnapshot visualRules)
    {
        float quality = Mathf.Lerp(0.90f, 1.18f, Mathf.Clamp(score, 0.0f, 1.0f));
        float jitter = Mathf.Lerp(0.84f, 1.20f, Hash01(coord.X, coord.Z, index * 1399, profile.Seed + 229));
        float baseScale = visualRules.InteriorBaseScale(tier, kind);

        return baseScale * quality * jitter;
    }

    private static Color SettlementInteriorColor(
        TerrainLandmarkKind kind,
        TerrainWorldField field,
        TerrainTileCoord coord,
        int index,
        TerrainGenerationProfile profile,
        TerrainSettlementVisualRuleSetSnapshot visualRules)
    {
        Color baseColor = LandmarkColorFor(kind, field, visualRules);
        Color variation = visualRules.InteriorVariationColor(kind);
        float blend = Mathf.Lerp(0.18f, 0.42f, Hash01(coord.X, coord.Z, index * 1423, profile.Seed + 233));
        return baseColor.Lerp(variation, blend);
    }
}
