using System.Collections.Generic;
using Dao.Terrain.Generation;
using Dao.Terrain.Rendering;
using Godot;

namespace Dao.Terrain.Streaming;

/// <summary>A single terrain tile in the streaming world, holding render mesh, scatter multimeshes, and collision.</summary>
public partial class TerrainChunk : Node3D
{
    private MeshInstance3D? _meshInstance;
    private MeshInstance3D? _waterInstance;
    private StaticBody3D? _staticBody;
    private TerrainVisualCatalog? _visualCatalog;
    private readonly Dictionary<TerrainScatterKind, MultiMeshInstance3D> _scatterNodes = new();
    private readonly Dictionary<TerrainScatterKind, Node3D> _scatterSceneNodes = new();
    private readonly Dictionary<TerrainLandmarkKind, MultiMeshInstance3D> _landmarkScatter = new();
    private readonly Dictionary<TerrainLandmarkKind, Node3D> _landmarkSceneNodes = new();

    private static readonly Dictionary<TerrainScatterKind, Mesh> ScatterMeshes = new();
    private static readonly Dictionary<TerrainLandmarkKind, Mesh> LandmarkMeshes = new();

    public TerrainTileCoord Coord { get; private set; }
    public int Lod { get; private set; }
    public bool HasCollision { get; private set; }

    /// <summary>Applies terrain tile data, rebuilding the render mesh, scatter instances, and collision geometry.</summary>
    public void Apply(TerrainTileData data, Material terrainMaterial, Material waterMaterial, TerrainVisualCatalog? visualCatalog = null)
    {
        Coord = data.Coord;
        Lod = data.Lod;
        HasCollision = data.CollisionFaces.Length > 0;
        _visualCatalog = visualCatalog;
        Name = $"Terrain_{data.Coord.X}_{data.Coord.Z}_L{data.Lod}";
        Position = new Vector3(data.Origin.X, 0.0f, data.Origin.Y);

        _meshInstance ??= CreateMeshInstance();
        _meshInstance.Mesh = TerrainMeshBuilder.CreateMesh(data);
        _meshInstance.SetSurfaceOverrideMaterial(0, terrainMaterial);

        RebuildLocalWater(data, waterMaterial);
        RebuildScatter(data);
        RebuildCollision(data);
    }

    private MeshInstance3D CreateMeshInstance()
    {
        var meshInstance = new MeshInstance3D { Name = "Mesh" };
        AddChild(meshInstance);
        return meshInstance;
    }

    private MeshInstance3D CreateWaterInstance()
    {
        var waterInstance = new MeshInstance3D { Name = "LocalWater" };
        AddChild(waterInstance);
        return waterInstance;
    }

    private void RebuildLocalWater(TerrainTileData data, Material waterMaterial)
    {
        if (!data.WaterSurface.HasSurface)
        {
            if (_waterInstance is not null)
            {
                _waterInstance.QueueFree();
                _waterInstance = null;
            }

            return;
        }

        _waterInstance ??= CreateWaterInstance();
        _waterInstance.Mesh = TerrainMeshBuilder.CreateWaterMesh(data);
        _waterInstance.SetSurfaceOverrideMaterial(0, waterMaterial);
    }

    private void RebuildScatter(TerrainTileData data)
    {
        RebuildSurfaceScatter(data);
        RebuildLandmarkScatter(data);
    }

    private MultiMeshInstance3D CreateScatterNode(string nodeName)
    {
        var node = new MultiMeshInstance3D { Name = nodeName };
        AddChild(node);
        return node;
    }

    private Node3D CreateSceneContainer(string nodeName)
    {
        var node = new Node3D { Name = $"{nodeName}_Scenes" };
        AddChild(node);
        return node;
    }

    private static bool UsesSceneInstances(ScatterVisual visual)
    {
        return visual.Scene is not null && (visual.PreferSceneInstances || visual.Mesh is null);
    }

    private void ClearSceneContainer(Node3D container)
    {
        foreach (Node child in container.GetChildren())
        {
            child.QueueFree();
        }
    }

    private static Transform3D TransformForInstance(TerrainScatterInstance instance, ScatterVisual visual)
    {
        var basis = new Basis(Vector3.Up, instance.RotationY)
            .Scaled(visual.AxisScale * instance.UniformScale);
        return new Transform3D(
            basis,
            instance.LocalPosition + Vector3.Up * visual.VerticalOffset * instance.UniformScale);
    }

    private void AddSceneInstance(Node3D container, PackedScene scene, Transform3D transform, ScatterVisual visual, Color tint)
    {
        Node? sceneRoot = scene.Instantiate();
        if (sceneRoot is null)
        {
            return;
        }

        if (sceneRoot is Node3D sceneRoot3D)
        {
            sceneRoot3D.Transform = transform;
            ApplyVisualMetadata(sceneRoot3D, tint, visual);
            container.AddChild(sceneRoot3D);
            return;
        }

        var wrapper = new Node3D { Transform = transform };
        ApplyVisualMetadata(wrapper, tint, visual);
        wrapper.AddChild(sceneRoot);
        container.AddChild(wrapper);
    }

    private static void ApplyVisualMetadata(Node node, Color tint, ScatterVisual visual)
    {
        node.SetMeta("terrain_tint", tint);
        node.SetMeta("terrain_creates_collision", visual.CreatesCollision);
        node.SetMeta("terrain_creates_navigation_obstacle", visual.CreatesNavigationObstacle);
        if (!string.IsNullOrWhiteSpace(visual.InteractionTag))
        {
            node.SetMeta("terrain_interaction_tag", visual.InteractionTag);
        }
    }

    private readonly record struct ScatterVisual(
        string NodeName,
        Mesh? Mesh,
        PackedScene? Scene,
        bool PreferSceneInstances,
        float VerticalOffset,
        Vector3 AxisScale,
        float AabbHeightPadding,
        bool CreatesCollision,
        bool CreatesNavigationObstacle,
        string InteractionTag);
}
