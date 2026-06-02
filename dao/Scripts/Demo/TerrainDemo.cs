using Dao.Terrain;
using Dao.Terrain.Generation;
using Dao.Terrain.Streaming;
using Godot;

namespace Dao.Demo;

[GlobalClass]
public partial class TerrainDemo : Node3D
{
    [ExportGroup("Open World Planning")]
    [Export] public bool ValidateOpenWorldPlanOnReady { get; set; } = true;
    [Export] public bool ExportOpenWorldPlanOnReady { get; set; } = false;
    [Export(PropertyHint.Range, "256,2048,1")] public int OpenWorldPlanImageSize { get; set; } = 512;
    [Export(PropertyHint.Range, "1024,65536,1")] public float OpenWorldPlanWorldSize { get; set; } = 12288.0f;
    [Export] public string OpenWorldPlanOutputDirectory { get; set; } = "user://terrain";

    public override void _Ready()
    {
        Name = "ProceduralTerrainDemo";

        DemoFlyCamera camera = CreateCamera();
        AddChild(camera);

        TerrainWorld terrainWorld = CreateTerrainWorld(camera);
        AddChild(terrainWorld);
        terrainWorld.SetFocus(camera);

        AddChild(CreateSun());
        AddChild(CreateWorldEnvironment());

        if (ValidateOpenWorldPlanOnReady || ExportOpenWorldPlanOnReady)
        {
            BuildOpenWorldPlanArtifacts(terrainWorld);
        }
    }

    private static DemoFlyCamera CreateCamera()
    {
        return new DemoFlyCamera { Name = "Camera" };
    }

    private static TerrainWorld CreateTerrainWorld(Node3D focus)
    {
        var settings = new TerrainSettings
        {
            Seed = 613_061,
            ChunkSize = 192.0f,
            BaseResolution = 64,
            StreamRadiusChunks = 6,
            CollisionRadiusChunks = 2,
            MaxLod = 3,
            HeightScale = 820.0f,
            SeaLevel = -22.0f,
            ContinentScale = 5600.0f,
            MountainScale = 1720.0f,
            MountainWeight = 0.82f,
            ValleyWeight = 0.56f,
            DetailWeight = 0.16f,
            VistaFrequency = 0.62f,
            RiverStrength = 0.72f,
            RiverCarveDepth = 130.0f,
            TerraceStrength = 66.0f,
            SkirtDepth = 42.0f,
            MaxCompletedTilesPerFrame = 3,
            MaxQueuedTileJobs = 28,
            GenerateCollision = true,
            UseNativeSamplerWhenAvailable = true
        };

        return new TerrainWorld
        {
            Name = "TerrainWorld",
            Settings = settings,
            CreateWaterPlane = true,
            StreamingIntervalSeconds = 0.12,
            FocusPath = focus.GetPath()
        };
    }

    private void BuildOpenWorldPlanArtifacts(TerrainWorld terrainWorld)
    {
        TerrainGenerationProfile profile = terrainWorld.Settings?.Snapshot() ?? new TerrainSettings().Snapshot();
        TerrainWorldPlan plan = TerrainWorldPlanner.CreateOpenWorldPlan(profile, Vector2.Zero, OpenWorldPlanWorldSize);
        TerrainWorldPlanningGateResult planningGate = TerrainWorldPlanner.ValidateOpenWorldPlanning(plan);
        TerrainQualityGateResult qualityGate = TerrainQualityAnalyzer.ValidateOpenWorldDefault(plan.QualityReport);
        string status = planningGate.Passed && qualityGate.Passed ? "PASS" : "FAIL";

        GD.Print(
            $"Open world terrain plan {status}: " +
            $"{planningGate.Report.PointOfInterestCount} POIs, {planningGate.Report.RouteCount} routes, " +
            $"land {qualityGate.Report.LandRatio:0.000}, scenic {qualityGate.Report.ScenicRatio:0.000}, " +
            $"connected {planningGate.Report.ConnectedPointRatio:0.000}.");

        if (!planningGate.Passed || !qualityGate.Passed)
        {
            GD.PushWarning(
                $"Open world terrain plan validation failed. " +
                $"Planning gate: {planningGate.Passed}, quality gate: {qualityGate.Passed}.");
        }

        if (!ExportOpenWorldPlanOnReady)
        {
            return;
        }

        TerrainWorldPlanArtifactResult export = TerrainWorldPlanExporter.SaveOpenWorldArtifacts(
            plan,
            profile,
            OpenWorldPlanImageSize,
            OpenWorldPlanOutputDirectory);
        string mapPath = ProjectSettings.GlobalizePath(export.MapPath);
        string reportPath = ProjectSettings.GlobalizePath(export.ReportPath);
        GD.Print($"Open world plan artifacts: map '{mapPath}', report '{reportPath}'.");

        if (!export.Passed)
        {
            GD.PushWarning(
                $"Open world terrain artifact export failed. " +
                $"Map save: {export.MapSaveError}, report save: {export.ReportSaveError}.");
        }
    }

    private static DirectionalLight3D CreateSun()
    {
        var sun = new DirectionalLight3D
        {
            Name = "Sun",
            LightEnergy = 3.8f,
            ShadowEnabled = true,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
            DirectionalShadowMaxDistance = 2300.0f,
            DirectionalShadowBlendSplits = true
        };

        sun.RotationDegrees = new Vector3(-47.0f, -32.0f, 0.0f);
        return sun;
    }

    private static WorldEnvironment CreateWorldEnvironment()
    {
        var environment = new Environment
        {
            BackgroundMode = Environment.BGMode.Color,
            BackgroundColor = new Color(0.52f, 0.66f, 0.79f),
            AmbientLightSource = Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.64f, 0.70f, 0.74f),
            AmbientLightEnergy = 0.78f,
            TonemapMode = Environment.ToneMapper.Aces,
            TonemapExposure = 1.06f,
            TonemapWhite = 6.0f,
            FogEnabled = true,
            FogLightColor = new Color(0.62f, 0.72f, 0.78f),
            FogDensity = 0.00042f
        };

        return new WorldEnvironment
        {
            Name = "WorldEnvironment",
            Environment = environment
        };
    }
}
