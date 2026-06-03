using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Partial class for route-corridor scatter placement including road markers and bridge spans during tile generation.</summary>
public static partial class TerrainTileBuilder
{
    private static void AddRouteCorridorScatter(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int cellX,
        int cellZ,
        float localX,
        float localZ,
        float height,
        float slope,
        TerrainWorldField field,
        TerrainRouteCorridorSample corridor,
        List<TerrainScatterInstance> scatter)
    {
        if (!corridor.HasInfluence || corridor.CoreStrength < 0.48f)
        {
            return;
        }

        Vector2 direction = CorridorDirectionOrFallback(corridor, coord, cellX, cellZ, profile);
        float rotation = RouteRotation(direction);

        if (IsBridgeSpanCandidate(corridor, field, height, profile) &&
            slope < 0.48f &&
            Hash01(coord.X, coord.Z, cellX * 5501 + cellZ * 5527, profile.Seed + 229) < BridgeSpanProbability(corridor, field, height, profile))
        {
            float bridgeScale = 2.25f + corridor.CoreStrength * 1.45f;
            Color bridgeTint = RouteBridgeColor(corridor, field);
            scatter.Add(new TerrainScatterInstance(
                TerrainScatterKind.Landmark,
                new Vector3(localX, height + 0.10f, localZ),
                rotation,
                bridgeScale,
                bridgeTint,
                TerrainLandmarkKind.BridgeSpan));
            return;
        }

        if (corridor.CoreStrength < 0.62f || slope > RouteMarkerMaxSlope(corridor.Kind))
        {
            return;
        }

        float markerProbability = RouteMarkerProbability(corridor);
        if (Hash01(coord.X, coord.Z, cellX * 5639 + cellZ * 5657, profile.Seed + 233) > markerProbability)
        {
            return;
        }

        Vector2 side = new(-direction.Y, direction.X);
        float sideRoll = Hash01(coord.X, coord.Z, cellX * 5689 + cellZ * 5711, profile.Seed + 239);
        float sideSign = sideRoll < 0.5f ? -1.0f : 1.0f;
        float shoulderOffset = (2.2f + Hash01(coord.X, coord.Z, cellX * 5737 + cellZ * 5749, profile.Seed + 241) * 2.8f) * sideSign;
        Vector2 local = new(
            Mathf.Clamp(localX + side.X * shoulderOffset, 0.0f, profile.ChunkSize),
            Mathf.Clamp(localZ + side.Y * shoulderOffset, 0.0f, profile.ChunkSize));
        float markerRotation = rotation + (sideSign < 0.0f ? -0.12f : 0.12f);
        float markerScale = 1.05f + corridor.ScenicPotential * 0.36f + Hash01(coord.X, coord.Z, cellX * 5779 + cellZ * 5791, profile.Seed + 251) * 0.32f;
        Color markerTint = RouteMarkerColor(corridor, field);
        scatter.Add(new TerrainScatterInstance(
            TerrainScatterKind.Landmark,
            new Vector3(local.X, height + 0.08f, local.Y),
            markerRotation,
            markerScale,
            markerTint,
            TerrainLandmarkKind.RoadMarker));
    }

    private static Vector2 CorridorDirectionOrFallback(
        TerrainRouteCorridorSample corridor,
        TerrainTileCoord coord,
        int cellX,
        int cellZ,
        TerrainGenerationProfile profile)
    {
        if (corridor.Direction.LengthSquared() > 0.0001f)
        {
            return corridor.Direction;
        }

        float angle = Hash01(coord.X, coord.Z, cellX * 5813 + cellZ * 5821, profile.Seed + 257) * Mathf.Tau;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static float RouteRotation(Vector2 direction)
    {
        return Mathf.Atan2(direction.Y, direction.X);
    }

    private static bool IsBridgeSpanCandidate(
        TerrainRouteCorridorSample corridor,
        TerrainWorldField field,
        float height,
        TerrainGenerationProfile profile)
    {
        if (corridor.CoreStrength < 0.58f)
        {
            return false;
        }

        bool riverRoad =
            corridor.Kind == TerrainRouteKind.RiverRoad &&
            (field.River > 0.62f ||
                (field.LandscapeKind == TerrainLandscapeKind.RiverValley && field.River > 0.54f) ||
                (field.LandscapeKind == TerrainLandscapeKind.Wetland && field.Moisture > 0.74f));
        bool coastalTrestle =
            corridor.Kind == TerrainRouteKind.CoastalPath &&
            height < profile.SeaLevel + 20.0f &&
            field.Moisture > 0.54f;
        bool wetlandBoardwalk =
            corridor.Kind is TerrainRouteKind.RiverRoad or TerrainRouteKind.CoastalPath &&
            field.LandscapeKind == TerrainLandscapeKind.Wetland &&
            field.Moisture > 0.78f &&
            corridor.Traversability > 0.30f;

        return riverRoad || coastalTrestle || wetlandBoardwalk;
    }

    private static float BridgeSpanProbability(
        TerrainRouteCorridorSample corridor,
        TerrainWorldField field,
        float height,
        TerrainGenerationProfile profile)
    {
        float waterProximity = Mathf.Clamp(
            field.River * 0.68f +
            field.Moisture * 0.18f +
            (1.0f - Mathf.SmoothStep(profile.SeaLevel + 4.0f, profile.SeaLevel + 32.0f, height)) * 0.14f,
            0.0f,
            1.0f);
        float baseProbability = corridor.Kind switch
        {
            TerrainRouteKind.RiverRoad => 0.08f,
            TerrainRouteKind.CoastalPath => 0.07f,
            TerrainRouteKind.ScenicTrail => 0.04f,
            _ => 0.05f
        };

        return Mathf.Clamp(baseProbability + waterProximity * 0.12f + corridor.CoreStrength * 0.04f, 0.04f, 0.24f);
    }

    private static float RouteMarkerProbability(TerrainRouteCorridorSample corridor)
    {
        float baseProbability = corridor.Kind switch
        {
            TerrainRouteKind.RiverRoad => 0.18f,
            TerrainRouteKind.RidgePass => 0.14f,
            TerrainRouteKind.CoastalPath => 0.20f,
            TerrainRouteKind.ScenicTrail => 0.18f,
            _ => 0.17f
        };

        return Mathf.Clamp(
            baseProbability +
            corridor.ScenicPotential * 0.07f +
            corridor.Traversability * 0.04f,
            0.12f,
            0.28f);
    }

    private static float RouteMarkerMaxSlope(TerrainRouteKind kind)
    {
        return kind switch
        {
            TerrainRouteKind.RidgePass => 0.56f,
            TerrainRouteKind.ScenicTrail => 0.44f,
            _ => 0.38f
        };
    }

    private static Color RouteMarkerColor(TerrainRouteCorridorSample corridor, TerrainWorldField field)
    {
        Color baseColor = corridor.Kind switch
        {
            TerrainRouteKind.RiverRoad => new Color(0.36f, 0.44f, 0.36f),
            TerrainRouteKind.RidgePass => new Color(0.50f, 0.48f, 0.42f),
            TerrainRouteKind.CoastalPath => new Color(0.62f, 0.56f, 0.38f),
            TerrainRouteKind.ScenicTrail => new Color(0.64f, 0.48f, 0.25f),
            _ => new Color(0.52f, 0.40f, 0.24f)
        };

        return baseColor.Lerp(Colors.White, Mathf.Clamp(field.ScenicPotential * 0.10f, 0.0f, 0.10f));
    }

    private static Color RouteBridgeColor(TerrainRouteCorridorSample corridor, TerrainWorldField field)
    {
        Color baseColor = corridor.Kind == TerrainRouteKind.CoastalPath
            ? new Color(0.54f, 0.48f, 0.34f)
            : new Color(0.42f, 0.34f, 0.26f);
        return baseColor.Lerp(new Color(0.56f, 0.58f, 0.52f), Mathf.Clamp(field.Moisture * 0.16f, 0.0f, 0.16f));
    }
}
