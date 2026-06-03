using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace Dao.Terrain.Generation;

public readonly record struct TerrainWorldPlanArtifactResult(
    TerrainWorldPlan Plan,
    TerrainWorldPlanningGateResult PlanningGate,
    TerrainQualityGateResult QualityGate,
    TerrainExperienceGateResult ExperienceGate,
    string MapPath,
    string ReportPath,
    Error MapSaveError,
    Error ReportSaveError)
{
    public bool Passed =>
        PlanningGate.Passed &&
        QualityGate.Passed &&
        ExperienceGate.Passed &&
        MapSaveError == Error.Ok &&
        ReportSaveError == Error.Ok;
}

public static class TerrainWorldPlanExporter
{
    private static readonly Color RouteShadow = new(0.02f, 0.018f, 0.014f, 0.70f);
    private static readonly Color MarkerOutline = new(0.03f, 0.025f, 0.018f, 0.86f);
    private static readonly Color MarkerCore = new(0.96f, 0.92f, 0.78f, 0.78f);

    public static TerrainWorldPlanArtifactResult SaveOpenWorldArtifacts(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int imageSize,
        string outputDirectory,
        TerrainMapLayer baseLayer = TerrainMapLayer.Biome)
    {
        TerrainWorldPlan plan = TerrainWorldPlanner.CreateOpenWorldPlan(profile, center, worldSize);
        return SaveOpenWorldArtifacts(plan, profile, imageSize, outputDirectory, baseLayer);
    }

    public static TerrainWorldPlanArtifactResult SaveOpenWorldArtifacts(
        TerrainWorldPlan plan,
        TerrainGenerationProfile profile,
        int imageSize,
        string outputDirectory,
        TerrainMapLayer baseLayer = TerrainMapLayer.Biome)
    {
        TerrainWorldPlanningGateResult planningGate = TerrainWorldPlanner.ValidateOpenWorldPlanning(plan);
        TerrainQualityGateResult qualityGate = TerrainQualityAnalyzer.ValidateOpenWorldDefault(plan.QualityReport);
        TerrainExperienceGateResult experienceGate = TerrainExperienceAnalyzer.ValidateOpenWorldDefault(plan.ExperienceReport);
        string mapPath = BuildOutputPath(outputDirectory, "open_world_plan.png");
        string reportPath = BuildOutputPath(outputDirectory, "open_world_plan_report.txt");
        Error directoryError = EnsureOutputDirectory(outputDirectory);
        Error mapError = directoryError == Error.Ok
            ? SavePlanMap(plan, profile, imageSize, baseLayer, mapPath)
            : directoryError;
        Error reportError = directoryError == Error.Ok
            ? SaveTextReport(plan, planningGate, qualityGate, experienceGate, mapPath, reportPath)
            : directoryError;

        return new TerrainWorldPlanArtifactResult(
            plan,
            planningGate,
            qualityGate,
            experienceGate,
            mapPath,
            reportPath,
            mapError,
            reportError);
    }

    public static Error SavePlanMap(
        TerrainWorldPlan plan,
        TerrainGenerationProfile profile,
        int imageSize,
        TerrainMapLayer baseLayer,
        string outputPath)
    {
        Image image = CreatePlanMap(plan, profile, imageSize, baseLayer);
        EnsureDirectoryForPath(outputPath);
        return image.SavePng(outputPath);
    }

    public static Image CreatePlanMap(
        TerrainWorldPlan plan,
        TerrainGenerationProfile profile,
        int imageSize,
        TerrainMapLayer baseLayer = TerrainMapLayer.Biome)
    {
        Image image = TerrainMapExporter.CreateMap(profile, plan.Center, plan.WorldSize, imageSize, baseLayer);

        foreach (TerrainWorldRoute route in plan.Routes)
        {
            DrawRoute(image, plan, route);
        }

        foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
        {
            DrawPointOfInterest(image, plan, point);
        }

        return image;
    }

    public static string CreateTextReport(
        TerrainWorldPlan plan,
        TerrainWorldPlanningGateResult planningGate,
        TerrainQualityGateResult qualityGate,
        TerrainExperienceGateResult experienceGate,
        string? mapPath = null)
    {
        TerrainQualityReport quality = qualityGate.Report;
        TerrainWorldPlanningReport planning = planningGate.Report;
        TerrainExperienceReport experience = experienceGate.Report;
        var builder = new StringBuilder(4096);

        builder.AppendLine("Open World Terrain Plan");
        builder.AppendLine(FormattableString.Invariant($"Center: {plan.Center.X:0.##}, {plan.Center.Y:0.##}"));
        builder.AppendLine(FormattableString.Invariant($"World size: {plan.WorldSize:0.##} meters"));
        builder.AppendLine(FormattableString.Invariant($"Planning grid: {plan.GridResolution} x {plan.GridResolution}"));
        if (!string.IsNullOrWhiteSpace(mapPath))
        {
            builder.AppendLine($"Map: {mapPath}");
        }

        builder.AppendLine();
        builder.AppendLine("Terrain Quality Gate");
        builder.Append(qualityGate.Summary);
        builder.AppendLine(FormattableString.Invariant(
            $"Height range: {quality.MinHeight:0.0} to {quality.MaxHeight:0.0}, average {quality.AverageHeight:0.0}"));
        builder.AppendLine(FormattableString.Invariant(
            $"Land/ocean/coast: {quality.LandRatio:0.000} / {quality.OceanRatio:0.000} / {quality.CoastRatio:0.000}"));
        builder.AppendLine(FormattableString.Invariant(
            $"Rivers/scenic/traversable: {quality.RiverRatio:0.000} / {quality.ScenicRatio:0.000} / {quality.TraversableLandRatio:0.000}"));
        builder.AppendLine(FormattableString.Invariant(
            $"Landscape kinds: {quality.DistinctLandscapeKinds}"));

        builder.AppendLine();
        builder.AppendLine("Open World Planning Gate");
        builder.Append(planningGate.Summary);
        builder.AppendLine(FormattableString.Invariant(
            $"POIs/routes: {planning.PointOfInterestCount} / {planning.RouteCount}"));
        builder.AppendLine(FormattableString.Invariant(
            $"Connected point ratio: {planning.ConnectedPointRatio:0.000}"));
        builder.AppendLine(FormattableString.Invariant(
            $"World coverage POIs/routes: {planning.PointOfInterestWorldCoverage:0.000} / {planning.RouteWorldCoverage:0.000}"));
        builder.AppendLine(FormattableString.Invariant(
            $"Average point score: {planning.AveragePointScore:0.000}"));
        builder.AppendLine(FormattableString.Invariant(
            $"Average route cost/scenic/traversability: {planning.AverageRouteCost:0.0} / {planning.AverageRouteScenicPotential:0.000} / {planning.AverageRouteTraversability:0.000}"));

        builder.AppendLine();
        builder.AppendLine("Open World Experience Gate");
        builder.Append(experienceGate.Summary);
        builder.AppendLine(FormattableString.Invariant(
            $"Encounter/resource/hazard rich regions: {experience.EncounterRichRegionRatio:0.000} / {experience.ResourceRichRegionRatio:0.000} / {experience.HazardRichRegionRatio:0.000}"));
        builder.AppendLine(FormattableString.Invariant(
            $"Average exposure/resource/hazard/encounter: {experience.AverageExposure:0.000} / {experience.AverageResourcePotential:0.000} / {experience.AverageHazardPotential:0.000} / {experience.AverageEncounterPotential:0.000}"));
        builder.AppendLine(FormattableString.Invariant(
            $"Route rhythm / POI value / risk reward / scenic anchors: {experience.RouteRhythmScore:0.000} / {experience.PointOfInterestValue:0.000} / {experience.RiskRewardBalance:0.000} / {experience.ScenicAnchorRatio:0.000}"));

        builder.AppendLine();
        builder.AppendLine("Point Of Interest Counts");
        AppendPoiCount(builder, TerrainPointOfInterestKind.SettlementCandidate, planning.SettlementCandidateCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.Vista, planning.VistaCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.RiverCrossing, planning.RiverCrossingCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.MountainPass, planning.MountainPassCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.CoastalLanding, planning.CoastalLandingCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.ResourceGrove, planning.ResourceGroveCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.AncientSite, planning.AncientSiteCount);
        AppendPoiCount(builder, TerrainPointOfInterestKind.CanyonOverlook, planning.CanyonOverlookCount);

        builder.AppendLine();
        builder.AppendLine("Route Counts");
        AppendRouteCount(builder, TerrainRouteKind.PrimaryTrail, planning.PrimaryTrailCount);
        AppendRouteCount(builder, TerrainRouteKind.RiverRoad, planning.RiverRoadCount);
        AppendRouteCount(builder, TerrainRouteKind.RidgePass, planning.RidgePassCount);
        AppendRouteCount(builder, TerrainRouteKind.CoastalPath, planning.CoastalPathCount);
        AppendRouteCount(builder, TerrainRouteKind.ScenicTrail, planning.ScenicTrailCount);

        builder.AppendLine();
        builder.AppendLine("Top Points Of Interest");
        foreach (TerrainWorldPointOfInterest point in TopPoints(plan.PointsOfInterest, 12))
        {
            builder.AppendLine(FormattableString.Invariant(
                $"{point.Id:00} {point.Kind} score {point.Score:0.000} height {point.Height:0.0} scenic {point.ScenicPotential:0.000} traversable {point.Traversability:0.000} at {point.WorldPosition.X:0.0}, {point.WorldPosition.Y:0.0}"));
        }

        return builder.ToString();
    }

    public static Error SaveTextReport(
        TerrainWorldPlan plan,
        TerrainWorldPlanningGateResult planningGate,
        TerrainQualityGateResult qualityGate,
        TerrainExperienceGateResult experienceGate,
        string? mapPath,
        string outputPath)
    {
        EnsureDirectoryForPath(outputPath);
        FileAccess? file = FileAccess.Open(outputPath, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            return FileAccess.GetOpenError();
        }

        using (file)
        {
            file.StoreString(CreateTextReport(plan, planningGate, qualityGate, experienceGate, mapPath));
        }

        return Error.Ok;
    }

    private static IEnumerable<TerrainWorldPointOfInterest> TopPoints(
        TerrainWorldPointOfInterest[] points,
        int maxCount)
    {
        var copy = new TerrainWorldPointOfInterest[points.Length];
        points.CopyTo(copy, 0);
        Array.Sort(copy, (a, b) => b.Score.CompareTo(a.Score));

        int count = Math.Min(maxCount, copy.Length);
        for (int i = 0; i < count; i++)
        {
            yield return copy[i];
        }
    }

    private static void DrawRoute(Image image, TerrainWorldPlan plan, TerrainWorldRoute route)
    {
        if (route.Waypoints.Length < 2)
        {
            return;
        }

        Color routeColor = ColorForRoute(route.Kind);
        DrawPolyline(image, plan, route.Waypoints, RouteShadow, radius: 4);
        DrawPolyline(image, plan, route.Waypoints, routeColor, radius: 2);
    }

    private static void DrawPolyline(
        Image image,
        TerrainWorldPlan plan,
        Vector2[] waypoints,
        Color color,
        int radius)
    {
        for (int i = 1; i < waypoints.Length; i++)
        {
            if (!TryWorldToPixel(image, plan, waypoints[i - 1], out Vector2I from) ||
                !TryWorldToPixel(image, plan, waypoints[i], out Vector2I to))
            {
                continue;
            }

            DrawLine(image, from, to, color, radius);
        }
    }

    private static void DrawPointOfInterest(
        Image image,
        TerrainWorldPlan plan,
        TerrainWorldPointOfInterest point)
    {
        if (!TryWorldToPixel(image, plan, point.WorldPosition, out Vector2I pixel))
        {
            return;
        }

        int radius = Mathf.Clamp(Mathf.RoundToInt(5.0f + point.Score * 5.0f), 5, 10);
        Color color = ColorForPoint(point.Kind);
        DrawDisc(image, pixel.X, pixel.Y, radius + 2, MarkerOutline);
        DrawDisc(image, pixel.X, pixel.Y, radius, color);
        DrawDisc(image, pixel.X, pixel.Y, Mathf.Max(2, radius / 3), MarkerCore);
    }

    private static void DrawLine(Image image, Vector2I from, Vector2I to, Color color, int radius)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        if (steps == 0)
        {
            DrawDisc(image, from.X, from.Y, radius, color);
            return;
        }

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(from.X, to.X, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(from.Y, to.Y, t));
            DrawDisc(image, x, y, radius, color);
        }
    }

    private static void DrawDisc(Image image, int centerX, int centerY, int radius, Color color)
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

                BlendPixel(image, centerX + x, centerY + y, color);
            }
        }
    }

    private static void BlendPixel(Image image, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight())
        {
            return;
        }

        Color existing = image.GetPixel(x, y);
        float alpha = Mathf.Clamp(color.A, 0.0f, 1.0f);
        Color target = new(color.R, color.G, color.B, 1.0f);
        Color blended = existing.Lerp(target, alpha);
        blended.A = 1.0f;
        image.SetPixel(x, y, blended);
    }

    private static bool TryWorldToPixel(
        Image image,
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
            Mathf.Clamp(Mathf.RoundToInt(tx * (image.GetWidth() - 1)), 0, image.GetWidth() - 1),
            Mathf.Clamp(Mathf.RoundToInt(ty * (image.GetHeight() - 1)), 0, image.GetHeight() - 1));
        return true;
    }

    private static Color ColorForPoint(TerrainPointOfInterestKind kind)
    {
        return kind switch
        {
            TerrainPointOfInterestKind.SettlementCandidate => new Color(0.95f, 0.70f, 0.25f, 0.92f),
            TerrainPointOfInterestKind.Vista => new Color(0.96f, 0.86f, 0.30f, 0.94f),
            TerrainPointOfInterestKind.RiverCrossing => new Color(0.20f, 0.74f, 0.92f, 0.92f),
            TerrainPointOfInterestKind.MountainPass => new Color(0.70f, 0.62f, 0.96f, 0.92f),
            TerrainPointOfInterestKind.CoastalLanding => new Color(0.24f, 0.56f, 0.92f, 0.92f),
            TerrainPointOfInterestKind.ResourceGrove => new Color(0.30f, 0.78f, 0.36f, 0.92f),
            TerrainPointOfInterestKind.AncientSite => new Color(0.90f, 0.58f, 0.32f, 0.92f),
            TerrainPointOfInterestKind.CanyonOverlook => new Color(0.92f, 0.44f, 0.24f, 0.92f),
            _ => new Color(1.0f, 1.0f, 1.0f, 0.9f)
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

    private static void AppendPoiCount(StringBuilder builder, TerrainPointOfInterestKind kind, int count)
    {
        builder.AppendLine(FormattableString.Invariant($"{kind}: {count}"));
    }

    private static void AppendRouteCount(StringBuilder builder, TerrainRouteKind kind, int count)
    {
        builder.AppendLine(FormattableString.Invariant($"{kind}: {count}"));
    }

    private static string BuildOutputPath(string outputDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return fileName;
        }

        return $"{outputDirectory.Replace('\\', '/').TrimEnd('/')}/{fileName}";
    }

    private static void EnsureDirectoryForPath(string path)
    {
        int slash = path.Replace('\\', '/').LastIndexOf('/');
        if (slash <= 0)
        {
            return;
        }

        EnsureOutputDirectory(path[..slash]);
    }

    private static Error EnsureOutputDirectory(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return Error.Ok;
        }

        try
        {
            string normalized = outputDirectory.Replace('\\', '/');
            if (normalized.Contains("://", StringComparison.Ordinal))
            {
                return DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(normalized));
            }

            System.IO.Directory.CreateDirectory(normalized);
            return Error.Ok;
        }
        catch (Exception exception)
        {
            GD.PushError($"Failed to create terrain output directory '{outputDirectory}': {exception.Message}");
            return Error.FileCantWrite;
        }
    }
}
