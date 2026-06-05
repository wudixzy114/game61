using Dao.Terrain;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Carries the outcome of a world plan export operation, including gate validation results and file paths.</summary>
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

/// <summary>Exports open-world terrain plans as PNG maps and text reports, drawing routes and POI markers on the map.</summary>
public static partial class TerrainWorldPlanExporter
{
    private static readonly Color RouteShadow = new(0.02f, 0.018f, 0.014f, 0.70f);
    private static readonly Color MarkerOutline = new(0.03f, 0.025f, 0.018f, 0.86f);
    private static readonly Color MarkerCore = new(0.96f, 0.92f, 0.78f, 0.78f);

    /// <summary>Creates a plan, validates it, and saves map + report artifacts.</summary>
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

    /// <summary>Validates a plan and saves map + report artifacts to disk.</summary>
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
            ? SaveTextReport(plan, profile, planningGate, qualityGate, experienceGate, mapPath, reportPath)
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

    /// <summary>Saves a plan map (base layer + routes + POIs) as a PNG image.</summary>
    public static Error SavePlanMap(
        TerrainWorldPlan plan,
        TerrainGenerationProfile profile,
        int imageSize,
        TerrainMapLayer baseLayer,
        string outputPath)
    {
        EnsureDirectoryForPath(outputPath);
        return TerrainMapExporter.SaveRasterPng(CreatePlanRaster(plan, profile, imageSize, baseLayer), outputPath);
    }

    /// <summary>Creates a plan map image with base layer terrain, route lines, and POI markers.</summary>
    public static Image CreatePlanMap(
        TerrainWorldPlan plan,
        TerrainGenerationProfile profile,
        int imageSize,
        TerrainMapLayer baseLayer = TerrainMapLayer.Biome)
    {
        return TerrainMapExporter.CreateImage(CreatePlanRaster(plan, profile, imageSize, baseLayer));
    }

    /// <summary>Creates a managed plan map raster with base terrain, route lines, and POI markers.</summary>
    public static TerrainMapRaster CreatePlanRaster(
        TerrainWorldPlan plan,
        TerrainGenerationProfile profile,
        int imageSize,
        TerrainMapLayer baseLayer = TerrainMapLayer.Biome)
    {
        TerrainMapRaster raster = TerrainMapExporter.CreateRaster(profile, plan.Center, plan.WorldSize, imageSize, baseLayer);

        foreach (TerrainWorldRoute route in plan.Routes)
        {
            DrawRoute(raster, plan, route);
        }

        foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
        {
            DrawPointOfInterest(raster, plan, point);
        }

        return raster;
    }
}
