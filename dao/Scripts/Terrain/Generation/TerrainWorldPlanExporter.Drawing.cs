using System;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanExporter
{
    private static void DrawRoute(TerrainMapRaster raster, TerrainWorldPlan plan, TerrainWorldRoute route)
    {
        if (route.Waypoints.Length < 2)
        {
            return;
        }

        Color routeColor = ColorForRoute(route.Kind);
        DrawPolyline(raster, plan, route.Waypoints, RouteShadow, radius: 4);
        DrawPolyline(raster, plan, route.Waypoints, routeColor, radius: 2);
    }

    private static void DrawPolyline(
        TerrainMapRaster raster,
        TerrainWorldPlan plan,
        Vector2[] waypoints,
        Color color,
        int radius)
    {
        for (int i = 1; i < waypoints.Length; i++)
        {
            if (!TryWorldToPixel(raster, plan, waypoints[i - 1], out Vector2I from) ||
                !TryWorldToPixel(raster, plan, waypoints[i], out Vector2I to))
            {
                continue;
            }

            DrawLine(raster, from, to, color, radius);
        }
    }

    private static void DrawPointOfInterest(
        TerrainMapRaster raster,
        TerrainWorldPlan plan,
        TerrainWorldPointOfInterest point)
    {
        if (!TryWorldToPixel(raster, plan, point.WorldPosition, out Vector2I pixel))
        {
            return;
        }

        int radius = Mathf.Clamp(Mathf.RoundToInt(5.0f + point.Score * 5.0f), 5, 10);
        Color color = ColorForPoint(point);
        DrawDisc(raster, pixel.X, pixel.Y, radius + 2, MarkerOutline);
        DrawDisc(raster, pixel.X, pixel.Y, radius, color);
        DrawDisc(raster, pixel.X, pixel.Y, Mathf.Max(2, radius / 3), MarkerCore);
    }

    private static void DrawLine(TerrainMapRaster raster, Vector2I from, Vector2I to, Color color, int radius)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        if (steps == 0)
        {
            DrawDisc(raster, from.X, from.Y, radius, color);
            return;
        }

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(from.X, to.X, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(from.Y, to.Y, t));
            DrawDisc(raster, x, y, radius, color);
        }
    }

    private static void DrawDisc(TerrainMapRaster raster, int centerX, int centerY, int radius, Color color)
    {
        int radiusSquared = radius * radius;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if ((x * x) + (y * y) > radiusSquared)
                {
                    continue;
                }

                BlendPixel(raster, centerX + x, centerY + y, color);
            }
        }
    }

    private static void BlendPixel(TerrainMapRaster raster, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= raster.Width || y >= raster.Height)
        {
            return;
        }

        Color existing = raster.GetPixel(x, y);
        float alpha = Mathf.Clamp(color.A, 0.0f, 1.0f);
        Color target = new(color.R, color.G, color.B, 1.0f);
        Color blended = existing.Lerp(target, alpha);
        blended.A = 1.0f;
        raster.SetPixel(x, y, blended);
    }

    private static bool TryWorldToPixel(
        TerrainMapRaster raster,
        TerrainWorldPlan plan,
        Vector2 world,
        out Vector2I pixel)
    {
        float tx = ((world.X - plan.Center.X) / plan.WorldSize) + 0.5f;
        float ty = ((world.Y - plan.Center.Y) / plan.WorldSize) + 0.5f;
        if (tx < 0.0f || ty < 0.0f || tx > 1.0f || ty > 1.0f)
        {
            pixel = default;
            return false;
        }

        pixel = new Vector2I(
            Mathf.Clamp(Mathf.RoundToInt(tx * (raster.Width - 1)), 0, raster.Width - 1),
            Mathf.Clamp(Mathf.RoundToInt(ty * (raster.Height - 1)), 0, raster.Height - 1));
        return true;
    }

    private static Color ColorForPoint(TerrainWorldPointOfInterest point)
    {
        return point.SettlementTier switch
        {
            TerrainSettlementTier.Village => new Color(0.86f, 0.58f, 0.26f, 0.94f),
            TerrainSettlementTier.Town => new Color(0.94f, 0.38f, 0.18f, 0.96f),
            TerrainSettlementTier.OasisHub => new Color(0.10f, 0.86f, 0.58f, 0.96f),
            _ => point.Kind switch
            {
                TerrainPointOfInterestKind.SettlementCandidate => new Color(0.95f, 0.70f, 0.25f, 0.92f),
                TerrainPointOfInterestKind.Vista => new Color(0.96f, 0.86f, 0.30f, 0.94f),
                TerrainPointOfInterestKind.RiverCrossing => new Color(0.20f, 0.74f, 0.92f, 0.92f),
                TerrainPointOfInterestKind.MountainPass => new Color(0.70f, 0.62f, 0.96f, 0.92f),
                TerrainPointOfInterestKind.CoastalLanding => new Color(0.24f, 0.56f, 0.92f, 0.92f),
                TerrainPointOfInterestKind.ResourceGrove => new Color(0.30f, 0.78f, 0.36f, 0.92f),
                TerrainPointOfInterestKind.AncientSite => new Color(0.90f, 0.58f, 0.32f, 0.92f),
                TerrainPointOfInterestKind.CanyonOverlook => new Color(0.92f, 0.44f, 0.24f, 0.92f),
                TerrainPointOfInterestKind.Oasis => new Color(0.18f, 0.82f, 0.58f, 0.94f),
                _ => new Color(1.0f, 1.0f, 1.0f, 0.9f)
            }
        };
    }

    private static Color ColorForRoute(TerrainRouteKind kind)
    {
        return kind switch
        {
            TerrainRouteKind.PrimaryTrail => new Color(0.94f, 0.79f, 0.46f, 0.74f),
            TerrainRouteKind.RiverRoad => new Color(0.20f, 0.62f, 0.90f, 0.78f),
            TerrainRouteKind.RidgePass => new Color(0.74f, 0.68f, 0.95f, 0.78f),
            TerrainRouteKind.CoastalPath => new Color(0.34f, 0.74f, 0.82f, 0.78f),
            TerrainRouteKind.ScenicTrail => new Color(0.95f, 0.70f, 0.25f, 0.82f),
            _ => new Color(1.0f, 1.0f, 1.0f, 0.74f)
        };
    }
}
