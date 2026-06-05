using System;
using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static void AddSettlementGatewayScatter(
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
        if (!TryFindSettlementGatewayApproach(point, corridorSegments, radius, out Vector2 approach, out TerrainRouteKind routeKind))
        {
            approach = SettlementLayoutAxis(point, corridorSegments, profile);
            routeKind = TerrainRouteKind.PrimaryTrail;
        }

        if (!TrySelectSettlementGatewayPlacement(profile, point, corridorSegments, radius, approach, routeKind, out Vector2 world, out Vector2 direction, out TerrainRouteKind placementRouteKind))
        {
            return;
        }

        float localX = world.X - origin.X;
        float localZ = world.Y - origin.Y;
        if (localX < 0.0f || localZ < 0.0f || localX > profile.ChunkSize || localZ > profile.ChunkSize)
        {
            return;
        }

        float height = SampleHeightBilinear(localX, localZ, resolution, step, heights, vertexCountPerSide);
        if (height < profile.SeaLevel - 2.0f)
        {
            return;
        }

        TerrainWorldField field = SampleFieldBilinear(localX, localZ, resolution, step, fields, vertexCountPerSide);
        Vector2 side = new(-direction.Y, direction.X);
        float rotation = Mathf.Atan2(side.Y, side.X);
        float scale = SettlementGatewayScale(point.SettlementTier, point.Score, placementRouteKind);
        Color tint = SettlementGatewayColor(point.SettlementTier, placementRouteKind, field);
        scatter.Add(new TerrainScatterInstance(
            TerrainScatterKind.Landmark,
            new Vector3(localX, height + 0.06f, localZ),
            rotation,
            scale,
            tint,
            TerrainLandmarkKind.SettlementGateway));
    }

    private static bool TrySelectSettlementGatewayPlacement(
        TerrainGenerationProfile profile,
        TerrainWorldPointOfInterest point,
        TerrainRouteCorridorSegment[] corridorSegments,
        float radius,
        Vector2 preferredApproach,
        TerrainRouteKind routeKind,
        out Vector2 world,
        out Vector2 direction,
        out TerrainRouteKind placementRouteKind)
    {
        world = Vector2.Zero;
        direction = Vector2.Zero;
        placementRouteKind = routeKind;

        Vector2 axis = SettlementLayoutAxis(point, corridorSegments, profile);
        Vector2 side = new(-axis.Y, axis.X);
        if (TrySelectSettlementGatewayPlacementForDirection(profile, point, radius, preferredApproach, routeKind, out world, out direction, out placementRouteKind) ||
            TrySelectSettlementGatewayPlacementForDirection(profile, point, radius, axis, TerrainRouteKind.PrimaryTrail, out world, out direction, out placementRouteKind) ||
            TrySelectSettlementGatewayPlacementForDirection(profile, point, radius, -axis, TerrainRouteKind.PrimaryTrail, out world, out direction, out placementRouteKind) ||
            TrySelectSettlementGatewayPlacementForDirection(profile, point, radius, side, TerrainRouteKind.PrimaryTrail, out world, out direction, out placementRouteKind) ||
            TrySelectSettlementGatewayPlacementForDirection(profile, point, radius, -side, TerrainRouteKind.PrimaryTrail, out world, out direction, out placementRouteKind))
        {
            return true;
        }

        return false;
    }

    private static bool TrySelectSettlementGatewayPlacementForDirection(
        TerrainGenerationProfile profile,
        TerrainWorldPointOfInterest point,
        float radius,
        Vector2 candidateDirection,
        TerrainRouteKind routeKind,
        out Vector2 world,
        out Vector2 direction,
        out TerrainRouteKind placementRouteKind)
    {
        world = Vector2.Zero;
        direction = Vector2.Zero;
        placementRouteKind = routeKind;
        if (candidateDirection.LengthSquared() <= 0.0001f)
        {
            return false;
        }

        Vector2 normalized = candidateDirection.Normalized();
        ReadOnlySpan<float> distanceFactors = stackalloc float[] { 0.62f, 0.48f, 0.34f, 0.22f, 0.12f, 0.0f };
        foreach (float factor in distanceFactors)
        {
            Vector2 candidateWorld = point.WorldPosition + normalized * radius * factor;
            TerrainWorldField field = TerrainWorldFieldSampler.Sample(candidateWorld, profile);
            if (field.Height < profile.SeaLevel - 2.0f)
            {
                continue;
            }

            world = candidateWorld;
            direction = normalized;
            placementRouteKind = routeKind;
            return true;
        }

        return false;
    }

    private static bool TryFindSettlementGatewayApproach(
        TerrainWorldPointOfInterest point,
        TerrainRouteCorridorSegment[] corridorSegments,
        float radius,
        out Vector2 approach,
        out TerrainRouteKind routeKind)
    {
        approach = Vector2.Zero;
        routeKind = TerrainRouteKind.PrimaryTrail;
        float maxDistance = radius * 2.35f;
        float maxDistanceSquared = maxDistance * maxDistance;
        float bestScore = float.PositiveInfinity;

        foreach (TerrainRouteCorridorSegment segment in corridorSegments)
        {
            float t = ClosestPointT(point.WorldPosition, segment.From, segment.To);
            Vector2 closest = segment.From.Lerp(segment.To, t);
            float distanceSquared = point.WorldPosition.DistanceSquaredTo(closest);
            if (distanceSquared > maxDistanceSquared)
            {
                continue;
            }

            Vector2 candidateApproach = closest - point.WorldPosition;
            if (candidateApproach.LengthSquared() > radius * radius * 0.025f)
            {
                candidateApproach = candidateApproach.Normalized();
            }
            else if (segment.Direction.LengthSquared() > 0.0001f)
            {
                candidateApproach = t > 0.5f ? -segment.Direction : segment.Direction;
            }
            else
            {
                continue;
            }

            float routePriority = segment.Kind switch
            {
                TerrainRouteKind.PrimaryTrail => 0.12f,
                TerrainRouteKind.RiverRoad => 0.10f,
                TerrainRouteKind.CoastalPath => 0.09f,
                TerrainRouteKind.ScenicTrail => 0.07f,
                TerrainRouteKind.RidgePass => 0.05f,
                _ => 0.0f
            };
            float score = distanceSquared - routePriority * radius * radius;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            approach = candidateApproach;
            routeKind = segment.Kind;
        }

        return approach.LengthSquared() > 0.0001f;
    }

    private static float SettlementGatewayScale(
        TerrainSettlementTier tier,
        float score,
        TerrainRouteKind routeKind)
    {
        float tierScale = TerrainSettlementRules.GatewayTierScale(tier);
        float routeScale = TerrainSettlementRules.GatewayRouteScale(routeKind);

        return tierScale * routeScale * Mathf.Lerp(0.92f, 1.16f, Mathf.Clamp(score, 0.0f, 1.0f));
    }

    private static Color SettlementGatewayColor(
        TerrainSettlementTier tier,
        TerrainRouteKind routeKind,
        TerrainWorldField field)
    {
        Color baseColor = TerrainSettlementRules.GatewayBaseColor(tier);
        Color routeTint = TerrainSettlementRules.GatewayRouteTint(routeKind);

        return baseColor
            .Lerp(routeTint, 0.28f)
            .Lerp(Colors.White, Mathf.Clamp(field.ScenicPotential * 0.10f, 0.0f, 0.10f));
    }
}
