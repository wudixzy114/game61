using Dao.Terrain;
using Dao.Terrain.Generation;
using Dao.Terrain.Runtime;
using Dao.Terrain.Streaming;
using Godot;

namespace Dao.Demo;

/// <summary>Demo entry point that sets up a procedural terrain world with a fly camera, lighting, and optional open world plan overlay and export.</summary>
[GlobalClass]
public partial class TerrainDemo : Node3D
{
    private const string DefaultTerrainSettingsPath = "res://Resources/Terrain/DefaultTerrainSettings.tres";
    private const string DefaultTerrainVisualCatalogPath = "res://Resources/Terrain/DefaultTerrainVisualCatalog.tres";

    [ExportGroup("Terrain Settings")]
    [Export] public TerrainSettings? TerrainSettingsResource { get; set; }
    [Export] public TerrainVisualCatalog? TerrainVisualCatalogResource { get; set; }

    [ExportGroup("Open World Planning")]
    [Export] public bool ValidateOpenWorldPlanOnReady { get; set; } = true;
    [Export] public bool ShowOpenWorldPlanOverlayOnReady { get; set; } = true;
    [Export] public bool ExportOpenWorldPlanOnReady { get; set; } = false;
    [Export(PropertyHint.Range, "256,2048,1")] public int OpenWorldPlanImageSize { get; set; } = 512;
    [Export(PropertyHint.Range, "1024,65536,1")] public float OpenWorldPlanWorldSize { get; set; } = 12288.0f;
    [Export(PropertyHint.Range, "2,80,1")] public float OpenWorldPlanRouteWidth { get; set; } = 16.0f;
    [Export] public string OpenWorldPlanOutputDirectory { get; set; } = "user://terrain";

    private TerrainWorld? _pendingPlanArtifactWorld;
    private bool _planArtifactsPending;
    private bool _planArtifactsBuilt;

    public override void _Ready()
    {
        Name = "ProceduralTerrainDemo";

        DemoFlyCamera camera = CreateCamera();
        AddChild(camera);

        TerrainSettings terrainSettings = ResolveTerrainSettings();
        TerrainVisualCatalog? visualCatalog = ResolveTerrainVisualCatalog();
        TerrainWorld terrainWorld = CreateTerrainWorld(camera, OpenWorldPlanWorldSize, terrainSettings, visualCatalog);
        AddChild(terrainWorld);
        terrainWorld.SetFocus(camera);

        AddChild(CreateSun());
        AddChild(CreateWorldEnvironment());

        if (ValidateOpenWorldPlanOnReady || ShowOpenWorldPlanOverlayOnReady || ExportOpenWorldPlanOnReady)
        {
            RequestOpenWorldPlanArtifacts(terrainWorld);
        }
    }

    public override void _Process(double delta)
    {
        if (_planArtifactsPending && _pendingPlanArtifactWorld is not null)
        {
            TryBuildOpenWorldPlanArtifacts(_pendingPlanArtifactWorld, allowSynchronousFallback: false);
        }
    }

    private static DemoFlyCamera CreateCamera()
    {
        return new DemoFlyCamera { Name = "Camera" };
    }

    private TerrainSettings ResolveTerrainSettings()
    {
        if (TerrainSettingsResource is TerrainSettings assigned)
        {
            return DuplicateTerrainSettings(assigned);
        }

        Resource? loaded = ResourceLoader.Load(DefaultTerrainSettingsPath);
        if (loaded is TerrainSettings defaultSettings)
        {
            return DuplicateTerrainSettings(defaultSettings);
        }

        return CreateFallbackTerrainSettings();
    }

    private TerrainVisualCatalog? ResolveTerrainVisualCatalog()
    {
        if (TerrainVisualCatalogResource is TerrainVisualCatalog assigned)
        {
            return DuplicateTerrainVisualCatalog(assigned);
        }

        Resource? loaded = ResourceLoader.Load(DefaultTerrainVisualCatalogPath);
        if (loaded is TerrainVisualCatalog defaultCatalog)
        {
            return DuplicateTerrainVisualCatalog(defaultCatalog);
        }

        return null;
    }

    private static TerrainSettings DuplicateTerrainSettings(TerrainSettings source)
    {
        return source.Duplicate(true) as TerrainSettings ?? source;
    }

    private static TerrainVisualCatalog DuplicateTerrainVisualCatalog(TerrainVisualCatalog source)
    {
        return source.Duplicate(true) as TerrainVisualCatalog ?? source;
    }

    private static TerrainSettings CreateFallbackTerrainSettings()
    {
        return new TerrainSettings
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
    }

    private static TerrainWorld CreateTerrainWorld(
        Node3D focus,
        float openWorldPlanWorldSize,
        TerrainSettings settings,
        TerrainVisualCatalog? visualCatalog)
    {
        return new TerrainWorld
        {
            Name = "TerrainWorld",
            Settings = settings,
            VisualCatalog = visualCatalog,
            CreateWaterPlane = true,
            StreamingIntervalSeconds = 0.12,
            FocusPath = focus.GetPath(),
            GenerateOpenWorldPlanOnReady = true,
            ValidateGeneratedOpenWorldPlan = true,
            PrintGeneratedOpenWorldPlanSummary = false,
            OpenWorldPlanWorldSize = openWorldPlanWorldSize
        };
    }

    private void RequestOpenWorldPlanArtifacts(TerrainWorld terrainWorld)
    {
        _pendingPlanArtifactWorld = terrainWorld;
        _planArtifactsPending = true;
        TryBuildOpenWorldPlanArtifacts(terrainWorld, allowSynchronousFallback: true);
    }

    private bool TryBuildOpenWorldPlanArtifacts(TerrainWorld terrainWorld, bool allowSynchronousFallback)
    {
        if (_planArtifactsBuilt)
        {
            return true;
        }

        terrainWorld.OpenWorldPlanWorldSize = OpenWorldPlanWorldSize;
        TerrainGenerationProfile profile = terrainWorld.Profile;
        TerrainWorldPlan? currentPlan = terrainWorld.WorldPlan;
        TerrainWorldPlan? plan = currentPlan is not null && Mathf.IsEqualApprox(currentPlan.WorldSize, OpenWorldPlanWorldSize)
            ? currentPlan
            : null;
        if (plan is null)
        {
            if (terrainWorld.IsOpenWorldPlanGenerationPending || !allowSynchronousFallback)
            {
                return false;
            }

            plan = terrainWorld.GenerateOpenWorldPlan(apply: true);
        }

        TerrainWorldPlanningGateResult planningGate = TerrainWorldPlanner.ValidateOpenWorldPlanning(plan);
        TerrainQualityGateResult qualityGate = TerrainQualityAnalyzer.ValidateOpenWorldDefault(plan.QualityReport);
        TerrainExperienceGateResult experienceGate = TerrainExperienceAnalyzer.ValidateOpenWorldDefault(plan.ExperienceReport);
        string status = planningGate.Passed && qualityGate.Passed && experienceGate.Passed ? "PASS" : "FAIL";

        GD.Print(
            $"Open world terrain plan {status}: " +
            $"{planningGate.Report.PointOfInterestCount} POIs, {planningGate.Report.RouteCount} routes, " +
            $"land {qualityGate.Report.LandRatio:0.000}, scenic {qualityGate.Report.ScenicRatio:0.000}, " +
            $"encounter {experienceGate.Report.AverageEncounterPotential:0.000}, rhythm {experienceGate.Report.RouteRhythmScore:0.000}, " +
            $"connected {planningGate.Report.ConnectedPointRatio:0.000}, " +
            $"settlement net {planningGate.Report.ConnectedSettlementRatio:0.000}/{planningGate.Report.SettlementRouteCount}, " +
            $"coverage {planningGate.Report.PointOfInterestWorldCoverage:0.000}/{planningGate.Report.RouteWorldCoverage:0.000}.");

        if (!planningGate.Passed || !qualityGate.Passed || !experienceGate.Passed)
        {
            GD.PushWarning(
                $"Open world terrain plan validation failed. " +
                $"Planning gate: {planningGate.Passed}, quality gate: {qualityGate.Passed}, experience gate: {experienceGate.Passed}.");
        }

        if (ShowOpenWorldPlanOverlayOnReady)
        {
            CreateOpenWorldPlanOverlay(plan, profile);
        }

        if (!ExportOpenWorldPlanOnReady)
        {
            _planArtifactsBuilt = true;
            _planArtifactsPending = false;
            _pendingPlanArtifactWorld = null;
            return true;
        }

        TerrainWorldPlanArtifactResult export = TerrainWorldPlanExporter.SaveOpenWorldArtifacts(
            plan,
            profile,
            OpenWorldPlanImageSize,
            OpenWorldPlanOutputDirectory);
        string jsonPath = ProjectSettings.GlobalizePath(export.JsonPath);
        string mapPath = ProjectSettings.GlobalizePath(export.MapPath);
        string traversalCostPath = ProjectSettings.GlobalizePath(export.TraversalCostMapPath);
        string reportPath = ProjectSettings.GlobalizePath(export.ReportPath);
        GD.Print(
            $"Open world plan artifacts: json '{jsonPath}', map '{mapPath}', traversal '{traversalCostPath}', report '{reportPath}'.");

        if (!export.Passed)
        {
            GD.PushWarning(
                $"Open world terrain artifact export failed. " +
                $"JSON save: {export.JsonSaveError}, map save: {export.MapSaveError}, " +
                $"traversal save: {export.TraversalCostMapSaveError}, report save: {export.ReportSaveError}.");
        }

        _planArtifactsBuilt = true;
        _planArtifactsPending = false;
        _pendingPlanArtifactWorld = null;
        return true;
    }

    private void CreateOpenWorldPlanOverlay(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        var overlay = new TerrainWorldPlanOverlay
        {
            Name = "OpenWorldPlanOverlay",
            RouteWidth = OpenWorldPlanRouteWidth,
            VisibleByDefault = true,
            BuildGameplayAnchors = true,
            ShowPointMarkers = true,
            ShowRouteRibbons = true
        };

        AddChild(overlay);
        overlay.ApplyPlan(plan, profile);
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
