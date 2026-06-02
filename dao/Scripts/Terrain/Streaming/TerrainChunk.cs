using Dao.Terrain.Generation;
using Dao.Terrain.Rendering;
using Godot;

namespace Dao.Terrain.Streaming;

public partial class TerrainChunk : Node3D
{
    private MeshInstance3D? _meshInstance;
    private StaticBody3D? _staticBody;
    private MultiMeshInstance3D? _treeScatter;
    private MultiMeshInstance3D? _rockScatter;
    private MultiMeshInstance3D? _landmarkScatter;

    private static Mesh? _treeMesh;
    private static Mesh? _rockMesh;
    private static Mesh? _landmarkMesh;

    public TerrainTileCoord Coord { get; private set; }
    public int Lod { get; private set; }
    public bool HasCollision { get; private set; }

    public void Apply(TerrainTileData data, Material terrainMaterial)
    {
        Coord = data.Coord;
        Lod = data.Lod;
        HasCollision = data.CollisionFaces.Length > 0;
        Name = $"Terrain_{data.Coord.X}_{data.Coord.Z}_L{data.Lod}";
        Position = new Vector3(data.Origin.X, 0.0f, data.Origin.Y);

        _meshInstance ??= CreateMeshInstance();
        _meshInstance.Mesh = TerrainMeshBuilder.CreateMesh(data);
        _meshInstance.SetSurfaceOverrideMaterial(0, terrainMaterial);

        RebuildScatter(data);
        RebuildCollision(data);
    }

    private MeshInstance3D CreateMeshInstance()
    {
        var meshInstance = new MeshInstance3D { Name = "Mesh" };
        AddChild(meshInstance);
        return meshInstance;
    }

    private void RebuildScatter(TerrainTileData data)
    {
        _treeScatter = RebuildScatterKind(
            data,
            TerrainScatterKind.Tree,
            _treeScatter,
            "Trees",
            GetTreeMesh(),
            1.18f);
        _rockScatter = RebuildScatterKind(
            data,
            TerrainScatterKind.Rock,
            _rockScatter,
            "Rocks",
            GetRockMesh(),
            0.38f);
        _landmarkScatter = RebuildScatterKind(
            data,
            TerrainScatterKind.Landmark,
            _landmarkScatter,
            "Landmarks",
            GetLandmarkMesh(),
            1.30f);
    }

    private MultiMeshInstance3D? RebuildScatterKind(
        TerrainTileData data,
        TerrainScatterKind kind,
        MultiMeshInstance3D? existing,
        string nodeName,
        Mesh mesh,
        float verticalOffset)
    {
        int count = 0;
        foreach (TerrainScatterInstance instance in data.ScatterInstances)
        {
            if (instance.Kind == kind)
            {
                count++;
            }
        }

        if (count == 0)
        {
            if (existing is not null)
            {
                existing.QueueFree();
            }

            return null;
        }

        existing ??= CreateScatterNode(nodeName);

        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = mesh,
            CustomAabb = new Aabb(
                new Vector3(0.0f, data.MinHeight - 8.0f, 0.0f),
                new Vector3(data.ChunkSize, data.MaxHeight - data.MinHeight + 96.0f, data.ChunkSize))
        };

        multimesh.InstanceCount = count;
        multimesh.VisibleInstanceCount = count;

        int index = 0;
        foreach (TerrainScatterInstance instance in data.ScatterInstances)
        {
            if (instance.Kind != kind)
            {
                continue;
            }

            var basis = new Basis(Vector3.Up, instance.RotationY)
                .Scaled(Vector3.One * instance.UniformScale);
            var transform = new Transform3D(
                basis,
                instance.LocalPosition + Vector3.Up * verticalOffset * instance.UniformScale);

            multimesh.SetInstanceTransform(index, transform);
            multimesh.SetInstanceColor(index, instance.Color);
            index++;
        }

        existing.Multimesh = multimesh;
        return existing;
    }

    private MultiMeshInstance3D CreateScatterNode(string nodeName)
    {
        var node = new MultiMeshInstance3D { Name = nodeName };
        AddChild(node);
        return node;
    }

    private static Mesh GetTreeMesh()
    {
        if (_treeMesh is not null)
        {
            return _treeMesh;
        }

        var mesh = new CylinderMesh
        {
            TopRadius = 0.0f,
            BottomRadius = 0.42f,
            Height = 2.35f,
            RadialSegments = 7,
            Rings = 1,
            CapBottom = true,
            Material = TerrainMaterialFactory.CreateTreeMaterial()
        };

        _treeMesh = mesh;
        return mesh;
    }

    private static Mesh GetRockMesh()
    {
        if (_rockMesh is not null)
        {
            return _rockMesh;
        }

        var mesh = new SphereMesh
        {
            Radius = 0.72f,
            Height = 0.86f,
            RadialSegments = 8,
            Rings = 4,
            Material = TerrainMaterialFactory.CreateRockMaterial()
        };

        _rockMesh = mesh;
        return mesh;
    }

    private static Mesh GetLandmarkMesh()
    {
        if (_landmarkMesh is not null)
        {
            return _landmarkMesh;
        }

        var mesh = new BoxMesh
        {
            Size = new Vector3(0.78f, 2.6f, 0.78f),
            Material = TerrainMaterialFactory.CreateLandmarkMaterial()
        };

        _landmarkMesh = mesh;
        return mesh;
    }

    private void RebuildCollision(TerrainTileData data)
    {
        if (_staticBody is not null)
        {
            _staticBody.QueueFree();
            _staticBody = null;
        }

        if (data.CollisionFaces.Length == 0)
        {
            return;
        }

        var shape = new ConcavePolygonShape3D();
        shape.SetFaces(data.CollisionFaces);
        shape.BackfaceCollision = false;

        var collisionShape = new CollisionShape3D
        {
            Name = "CollisionShape",
            Shape = shape
        };

        _staticBody = new StaticBody3D { Name = "Collision" };
        _staticBody.AddChild(collisionShape);
        AddChild(_staticBody);
    }
}
