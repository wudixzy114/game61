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
    private readonly Dictionary<TerrainScatterKind, MultiMeshInstance3D> _scatterNodes = new();
    private readonly Dictionary<TerrainLandmarkKind, MultiMeshInstance3D> _landmarkScatter = new();

    private static readonly Dictionary<TerrainScatterKind, Mesh> ScatterMeshes = new();
    private static readonly Dictionary<TerrainLandmarkKind, Mesh> LandmarkMeshes = new();

    public TerrainTileCoord Coord { get; private set; }
    public int Lod { get; private set; }
    public bool HasCollision { get; private set; }

    /// <summary>Applies terrain tile data, rebuilding the render mesh, scatter instances, and collision geometry.</summary>
    public void Apply(TerrainTileData data, Material terrainMaterial, Material waterMaterial)
    {
        Coord = data.Coord;
        Lod = data.Lod;
        HasCollision = data.CollisionFaces.Length > 0;
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

    private readonly record struct ScatterVisual(
        string NodeName,
        float VerticalOffset,
        Vector3 AxisScale,
        float AabbHeightPadding);
}
