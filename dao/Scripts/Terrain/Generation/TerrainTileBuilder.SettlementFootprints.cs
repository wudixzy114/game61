using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
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
