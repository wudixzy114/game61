using Dao.Terrain;
using Dao.Terrain.Streaming;
using Godot;

namespace Dao.Demo;

[GlobalClass]
public partial class TerrainDemo : Node3D
{
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
