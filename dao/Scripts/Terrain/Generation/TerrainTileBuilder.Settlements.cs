using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

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
            TerrainSettlementTier.Town => 14,
            TerrainSettlementTier.OasisHub => 10,
            _ => 7
        };
        Vector2 axis = SettlementLayoutAxis(point, corridorSegments, profile);
        Vector2 side = new(-axis.Y, axis.X);

        for (int i = 0; i < count; i++)
        {
            TerrainLandmarkKind kind = SettlementInteriorKind(point.SettlementTier, i);
            Vector2 offset = SettlementInteriorOffset(point, radius, axis, side, i, count);
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
            float scale = SettlementInteriorScale(point.SettlementTier, point.Score, coord, i, profile);
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
            TerrainSettlementTier.Town => index % 7 == 0
                ? TerrainLandmarkKind.SettlementPlaza
                : TerrainLandmarkKind.TownBlock,
            TerrainSettlementTier.OasisHub => index == 0
                ? TerrainLandmarkKind.OasisPool
                : index % 5 == 0
                ? TerrainLandmarkKind.SettlementPlaza
                : TerrainLandmarkKind.OasisCanopy,
            TerrainSettlementTier.Village => index == 0
                ? TerrainLandmarkKind.SettlementPlaza
                : TerrainLandmarkKind.VillageHouse,
            _ => TerrainLandmarkKind.Settlement
        };
    }

    private static Vector2 SettlementInteriorOffset(
        TerrainWorldPointOfInterest point,
        float radius,
        Vector2 axis,
        Vector2 side,
        int index,
        int count)
    {
        if (point.SettlementTier == TerrainSettlementTier.OasisHub && index == 0)
        {
            return Vector2.Zero;
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
        float score,
        TerrainTileCoord coord,
        int index,
        TerrainGenerationProfile profile)
    {
        float quality = Mathf.Lerp(0.90f, 1.18f, Mathf.Clamp(score, 0.0f, 1.0f));
        float jitter = Mathf.Lerp(0.84f, 1.20f, Hash01(coord.X, coord.Z, index * 1399, profile.Seed + 229));
        float baseScale = tier switch
        {
            TerrainSettlementTier.Town => 2.95f,
            TerrainSettlementTier.OasisHub => 2.55f,
            _ => 2.30f
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
            _ => new Color(0.58f, 0.48f, 0.31f)
        };
        float blend = Mathf.Lerp(0.18f, 0.42f, Hash01(coord.X, coord.Z, index * 1423, profile.Seed + 233));
        return baseColor.Lerp(variation, blend);
    }

    private static TerrainSettlementLayoutDescriptor[] BuildSettlementLayoutDescriptors(
        TerrainWorldPointOfInterest[] points,
        TerrainRouteCorridorSegment[] corridorSegments,
        TerrainGenerationProfile profile)
    {
        var layouts = new List<TerrainSettlementLayoutDescriptor>(points.Length);
        foreach (TerrainWorldPointOfInterest point in points)
        {
            if (point.SettlementTier == TerrainSettlementTier.None)
            {
                continue;
            }

            float radius = TerrainPointOfInterestIndex.FootprintRadiusFor(point, profile);
            Vector2 axis = SettlementLayoutAxis(point, corridorSegments, profile);
            Vector2 side = new(-axis.Y, axis.X);
            layouts.Add(new TerrainSettlementLayoutDescriptor(
                point.SettlementTier,
                point.WorldPosition,
                radius,
                axis,
                side,
                TargetHeightForFootprint(point, profile)));
        }

        return layouts.ToArray();
    }

    private static TerrainSettlementLayoutSample SampleSettlementLayout(
        Vector2 world,
        TerrainSettlementLayoutDescriptor[] layouts)
    {
        TerrainSettlementLayoutSample best = TerrainSettlementLayoutSample.None;

        foreach (TerrainSettlementLayoutDescriptor layout in layouts)
        {
            Vector2 local = world - layout.Center;
            float distance = local.Length();
            if (distance > layout.Radius)
            {
                continue;
            }

            float along = local.Dot(layout.Axis);
            float across = local.Dot(layout.Side);
            float plazaStrength = 1.0f - Mathf.SmoothStep(layout.Radius * 0.08f, layout.Radius * 0.18f, distance);
            float streetStrength;
            float oasisGreenStrength = 0.0f;
            float oasisWaterStrength = 0.0f;

            if (layout.Tier == TerrainSettlementTier.Town)
            {
                float mainStreet = LineStrength(across, along, layout.Radius * 0.050f, layout.Radius * 0.68f);
                float crossStreet = LineStrength(along, across, layout.Radius * 0.046f, layout.Radius * 0.54f);
                float marketLane = LineStrength(across - layout.Radius * 0.22f, along, layout.Radius * 0.034f, layout.Radius * 0.48f) * 0.58f;
                streetStrength = Mathf.Max(Mathf.Max(mainStreet, crossStreet), marketLane);
                plazaStrength *= 1.08f;
            }
            else if (layout.Tier == TerrainSettlementTier.OasisHub)
            {
                float ring = 1.0f - Mathf.SmoothStep(layout.Radius * 0.028f, layout.Radius * 0.084f, Mathf.Abs(distance - layout.Radius * 0.42f));
                float entryPath = LineStrength(across, along, layout.Radius * 0.044f, layout.Radius * 0.62f);
                streetStrength = Mathf.Max(ring, entryPath * 0.82f);
                plazaStrength *= 0.72f;
                oasisGreenStrength = (1.0f - Mathf.SmoothStep(layout.Radius * 0.30f, layout.Radius * 0.62f, distance)) * 0.85f;
                oasisWaterStrength = 1.0f - Mathf.SmoothStep(layout.Radius * 0.12f, layout.Radius * 0.25f, distance);
            }
            else
            {
                float mainLane = LineStrength(across, along, layout.Radius * 0.040f, layout.Radius * 0.56f);
                float crossLane = LineStrength(along, across, layout.Radius * 0.032f, layout.Radius * 0.42f) * 0.62f;
                streetStrength = Mathf.Max(mainLane, crossLane);
                plazaStrength *= 0.86f;
            }

            float influence = Mathf.Clamp(Mathf.Max(Mathf.Max(Mathf.Max(streetStrength, plazaStrength), oasisGreenStrength * 0.72f), oasisWaterStrength), 0.0f, 1.0f);
            if (influence <= best.Influence)
            {
                continue;
            }

            best = new TerrainSettlementLayoutSample(
                true,
                layout.Tier,
                influence,
                Mathf.Clamp(Mathf.Max(streetStrength, plazaStrength), 0.0f, 1.0f),
                Mathf.Clamp(streetStrength, 0.0f, 1.0f),
                Mathf.Clamp(plazaStrength, 0.0f, 1.0f),
                Mathf.Clamp(oasisGreenStrength, 0.0f, 1.0f),
                Mathf.Clamp(oasisWaterStrength, 0.0f, 1.0f),
                layout.TargetHeight);
        }

        return best;
    }

    private static float LineStrength(float crossDistance, float alongDistance, float width, float halfLength)
    {
        float along = Mathf.Abs(alongDistance);
        if (along > halfLength)
        {
            return 0.0f;
        }

        float cross = Mathf.Abs(crossDistance);
        float crossStrength = 1.0f - Mathf.SmoothStep(width, width * 2.4f, cross);
        float endFade = 1.0f - Mathf.SmoothStep(halfLength * 0.78f, halfLength, along);
        return Mathf.Clamp(crossStrength * endFade, 0.0f, 1.0f);
    }

    private static bool IsInsidePointFootprint(
        Vector2 world,
        TerrainWorldPointOfInterest[] plannedPoints,
        TerrainGenerationProfile profile,
        float minimumInfluence)
    {
        if (plannedPoints.Length == 0)
        {
            return false;
        }

        foreach (TerrainWorldPointOfInterest point in plannedPoints)
        {
            float radius = TerrainPointOfInterestIndex.FootprintRadiusFor(point, profile);
            float distance = world.DistanceTo(point.WorldPosition);
            if (distance > radius)
            {
                continue;
            }

            float coreRadius = radius * 0.46f;
            float influence = 1.0f - Mathf.SmoothStep(coreRadius, radius, distance);
            if (influence >= minimumInfluence)
            {
                return true;
            }
        }

        return false;
    }

    private static TerrainLandmarkKind LandmarkKindFor(TerrainWorldPointOfInterest point)
    {
        if (point.SettlementTier == TerrainSettlementTier.Town)
        {
            return TerrainLandmarkKind.Town;
        }

        if (point.SettlementTier == TerrainSettlementTier.Village)
        {
            return TerrainLandmarkKind.Village;
        }

        if (point.SettlementTier == TerrainSettlementTier.OasisHub)
        {
            return TerrainLandmarkKind.OasisHub;
        }

        return point.Kind switch
        {
            TerrainPointOfInterestKind.SettlementCandidate => TerrainLandmarkKind.Settlement,
            TerrainPointOfInterestKind.Vista => TerrainLandmarkKind.Vista,
            TerrainPointOfInterestKind.RiverCrossing => TerrainLandmarkKind.RiverCrossing,
            TerrainPointOfInterestKind.MountainPass => TerrainLandmarkKind.MountainPass,
            TerrainPointOfInterestKind.CoastalLanding => TerrainLandmarkKind.CoastalLanding,
            TerrainPointOfInterestKind.ResourceGrove => TerrainLandmarkKind.ResourceGrove,
            TerrainPointOfInterestKind.CanyonOverlook => TerrainLandmarkKind.CanyonOverlook,
            TerrainPointOfInterestKind.Oasis => TerrainLandmarkKind.Oasis,
            _ => TerrainLandmarkKind.AncientStone
        };
    }

    private static float LandmarkScaleFor(TerrainLandmarkKind kind, float score)
    {
        float quality = Mathf.Lerp(0.88f, 1.24f, Mathf.Clamp(score, 0.0f, 1.0f));
        float baseScale = kind switch
        {
            TerrainLandmarkKind.Settlement => 7.8f,
            TerrainLandmarkKind.Vista => 6.6f,
            TerrainLandmarkKind.RiverCrossing => 6.2f,
            TerrainLandmarkKind.MountainPass => 7.0f,
            TerrainLandmarkKind.CoastalLanding => 7.4f,
            TerrainLandmarkKind.ResourceGrove => 6.8f,
            TerrainLandmarkKind.CanyonOverlook => 7.2f,
            TerrainLandmarkKind.Oasis => 7.6f,
            TerrainLandmarkKind.Village => 8.4f,
            TerrainLandmarkKind.Town => 10.8f,
            TerrainLandmarkKind.OasisHub => 9.4f,
            TerrainLandmarkKind.VillageHouse => 2.6f,
            TerrainLandmarkKind.TownBlock => 3.4f,
            TerrainLandmarkKind.OasisCanopy => 3.0f,
            TerrainLandmarkKind.SettlementPlaza => 3.2f,
            TerrainLandmarkKind.OasisPool => 3.4f,
            TerrainLandmarkKind.Waterfall => 7.2f,
            TerrainLandmarkKind.RoadMarker => 2.0f,
            TerrainLandmarkKind.BridgeSpan => 4.4f,
            TerrainLandmarkKind.DuneCrest => 5.4f,
            TerrainLandmarkKind.DesertMonolith => 5.8f,
            TerrainLandmarkKind.CanyonNeedle => 6.2f,
            TerrainLandmarkKind.IceSpire => 5.6f,
            _ => 7.0f
        };

        return baseScale * quality;
    }

    private static Color LandmarkColorFor(TerrainLandmarkKind kind, TerrainWorldField field)
    {
        Color baseColor = kind switch
        {
            TerrainLandmarkKind.Settlement => new Color(0.70f, 0.52f, 0.32f),
            TerrainLandmarkKind.Vista => new Color(0.86f, 0.74f, 0.30f),
            TerrainLandmarkKind.RiverCrossing => new Color(0.42f, 0.48f, 0.45f),
            TerrainLandmarkKind.MountainPass => new Color(0.56f, 0.54f, 0.62f),
            TerrainLandmarkKind.CoastalLanding => new Color(0.46f, 0.58f, 0.64f),
            TerrainLandmarkKind.ResourceGrove => new Color(0.28f, 0.54f, 0.28f),
            TerrainLandmarkKind.CanyonOverlook => new Color(0.66f, 0.38f, 0.24f),
            TerrainLandmarkKind.Oasis => new Color(0.18f, 0.58f, 0.42f),
            TerrainLandmarkKind.Village => new Color(0.74f, 0.56f, 0.30f),
            TerrainLandmarkKind.Town => new Color(0.78f, 0.44f, 0.24f),
            TerrainLandmarkKind.OasisHub => new Color(0.16f, 0.66f, 0.50f),
            TerrainLandmarkKind.VillageHouse => new Color(0.68f, 0.54f, 0.34f),
            TerrainLandmarkKind.TownBlock => new Color(0.72f, 0.46f, 0.31f),
            TerrainLandmarkKind.OasisCanopy => new Color(0.14f, 0.58f, 0.42f),
            TerrainLandmarkKind.SettlementPlaza => new Color(0.62f, 0.54f, 0.40f),
            TerrainLandmarkKind.OasisPool => new Color(0.08f, 0.34f, 0.46f),
            TerrainLandmarkKind.Waterfall => new Color(0.30f, 0.62f, 0.82f),
            TerrainLandmarkKind.RoadMarker => new Color(0.56f, 0.44f, 0.28f),
            TerrainLandmarkKind.BridgeSpan => new Color(0.44f, 0.34f, 0.25f),
            TerrainLandmarkKind.DuneCrest => new Color(0.76f, 0.58f, 0.30f),
            TerrainLandmarkKind.DesertMonolith => new Color(0.62f, 0.42f, 0.24f),
            TerrainLandmarkKind.CanyonNeedle => new Color(0.58f, 0.36f, 0.24f),
            TerrainLandmarkKind.IceSpire => new Color(0.62f, 0.76f, 0.86f),
            _ => new Color(0.52f, 0.50f, 0.44f)
        };

        return baseColor.Lerp(Colors.White, Mathf.Clamp(field.ScenicPotential * 0.12f, 0.0f, 0.12f));
    }

    private static TerrainPointFootprintSample SamplePointFootprint(
        Vector2 world,
        TerrainWorldPointOfInterest[] points,
        TerrainGenerationProfile profile)
    {
        TerrainPointFootprintSample best = TerrainPointFootprintSample.None;

        foreach (TerrainWorldPointOfInterest point in points)
        {
            float radius = TerrainPointOfInterestIndex.FootprintRadiusFor(point, profile);
            float distance = world.DistanceTo(point.WorldPosition);
            if (distance > radius)
            {
                continue;
            }

            float coreRadius = radius * 0.46f;
            float coreStrength = 1.0f - Mathf.SmoothStep(0.0f, coreRadius, distance);
            float influence = 1.0f - Mathf.SmoothStep(coreRadius, radius, distance);
            if (coreStrength > 0.0f)
            {
                influence = Mathf.Max(influence, coreStrength);
            }

            if (influence <= best.Influence)
            {
                continue;
            }

            float targetHeight = TargetHeightForFootprint(point, profile);
            best = new TerrainPointFootprintSample(point.Kind, point.SettlementTier, influence, coreStrength, targetHeight);
        }

        return best;
    }

    private static float TargetHeightForFootprint(TerrainWorldPointOfInterest point, TerrainGenerationProfile profile)
    {
        float landHeight = Mathf.Max(point.Height, profile.SeaLevel + 8.0f);
        return point.SettlementTier switch
        {
            TerrainSettlementTier.Town => landHeight + 1.2f,
            TerrainSettlementTier.Village => landHeight + 0.6f,
            TerrainSettlementTier.OasisHub => Mathf.Max(point.Height - 1.5f, profile.SeaLevel + 4.0f),
            _ => point.Kind == TerrainPointOfInterestKind.Oasis
                ? Mathf.Max(point.Height - 2.0f, profile.SeaLevel + 3.0f)
                : landHeight
        };
    }

    private static float ApplyPointFootprintHeight(float height, TerrainPointFootprintSample footprint)
    {
        float strength = footprint.SettlementTier switch
        {
            TerrainSettlementTier.Town => footprint.CoreStrength * 0.86f + footprint.Influence * 0.28f,
            TerrainSettlementTier.Village => footprint.CoreStrength * 0.76f + footprint.Influence * 0.24f,
            TerrainSettlementTier.OasisHub => footprint.CoreStrength * 0.62f + footprint.Influence * 0.30f,
            _ => footprint.Kind == TerrainPointOfInterestKind.Oasis
                ? footprint.CoreStrength * 0.54f + footprint.Influence * 0.28f
                : footprint.CoreStrength * 0.48f + footprint.Influence * 0.18f
        };

        return Mathf.Lerp(height, footprint.TargetHeight, Mathf.Clamp(strength, 0.0f, 0.88f));
    }

    private static float ApplySettlementLayoutHeight(float height, TerrainSettlementLayoutSample layout)
    {
        float strength = layout.Tier switch
        {
            TerrainSettlementTier.Town => layout.StreetStrength * 0.60f + layout.PlazaStrength * 0.78f,
            TerrainSettlementTier.OasisHub => layout.StreetStrength * 0.52f + layout.PlazaStrength * 0.44f + layout.OasisGreenStrength * 0.18f + layout.OasisWaterStrength * 0.72f,
            _ => layout.StreetStrength * 0.48f + layout.PlazaStrength * 0.58f
        };
        float targetHeight = layout.TargetHeight + layout.PlazaStrength * 0.18f - layout.OasisGreenStrength * 0.08f - layout.OasisWaterStrength * 0.62f;
        return Mathf.Lerp(height, targetHeight, Mathf.Clamp(strength, 0.0f, 0.84f));
    }

    private static Color BlendPointFootprintColor(Color baseColor, TerrainPointFootprintSample footprint)
    {
        Color footprintColor = footprint.SettlementTier switch
        {
            TerrainSettlementTier.Town => new Color(0.54f, 0.40f, 0.27f),
            TerrainSettlementTier.Village => new Color(0.48f, 0.38f, 0.24f),
            TerrainSettlementTier.OasisHub => new Color(0.18f, 0.54f, 0.42f),
            _ => footprint.Kind == TerrainPointOfInterestKind.Oasis
                ? new Color(0.20f, 0.50f, 0.40f)
                : new Color(0.44f, 0.36f, 0.25f)
        };

        float blend = Mathf.Clamp(footprint.CoreStrength * 0.44f + footprint.Influence * 0.26f, 0.0f, 0.66f);
        return baseColor.Lerp(footprintColor, blend);
    }

    private static Color BlendSettlementLayoutColor(Color baseColor, TerrainSettlementLayoutSample layout)
    {
        Color streetColor = layout.Tier switch
        {
            TerrainSettlementTier.Town => new Color(0.46f, 0.38f, 0.30f),
            TerrainSettlementTier.OasisHub => new Color(0.38f, 0.48f, 0.35f),
            _ => new Color(0.42f, 0.34f, 0.22f)
        };
        Color plazaColor = layout.Tier == TerrainSettlementTier.OasisHub
            ? new Color(0.28f, 0.56f, 0.44f)
            : new Color(0.58f, 0.50f, 0.38f);
        Color result = baseColor.Lerp(streetColor, Mathf.Clamp(layout.StreetStrength * 0.64f, 0.0f, 0.68f));
        result = result.Lerp(plazaColor, Mathf.Clamp(layout.PlazaStrength * 0.62f, 0.0f, 0.66f));

        if (layout.OasisGreenStrength > 0.0f)
        {
            result = result.Lerp(new Color(0.12f, 0.54f, 0.34f), Mathf.Clamp(layout.OasisGreenStrength * 0.48f, 0.0f, 0.52f));
        }

        if (layout.OasisWaterStrength > 0.0f)
        {
            result = result.Lerp(new Color(0.05f, 0.30f, 0.42f), Mathf.Clamp(layout.OasisWaterStrength * 0.72f, 0.0f, 0.76f));
        }

        return result;
    }

    private readonly record struct TerrainPointFootprintSample(
        TerrainPointOfInterestKind Kind,
        TerrainSettlementTier SettlementTier,
        float Influence,
        float CoreStrength,
        float TargetHeight)
    {
        public static TerrainPointFootprintSample None { get; } = new(
            TerrainPointOfInterestKind.Vista,
            TerrainSettlementTier.None,
            0.0f,
            0.0f,
            0.0f);

        public bool HasInfluence => Influence > 0.0f;
    }

    private readonly record struct TerrainSettlementLayoutDescriptor(
        TerrainSettlementTier Tier,
        Vector2 Center,
        float Radius,
        Vector2 Axis,
        Vector2 Side,
        float TargetHeight);

    private readonly record struct TerrainSettlementLayoutSample(
        bool HasInfluence,
        TerrainSettlementTier Tier,
        float Influence,
        float CoreStrength,
        float StreetStrength,
        float PlazaStrength,
        float OasisGreenStrength,
        float OasisWaterStrength,
        float TargetHeight)
    {
        public static TerrainSettlementLayoutSample None { get; } = new(
            false,
            TerrainSettlementTier.None,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f);
    }
}
