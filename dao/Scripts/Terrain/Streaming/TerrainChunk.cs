using System.Collections.Generic;
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
    private readonly Dictionary<TerrainLandmarkKind, MultiMeshInstance3D> _landmarkScatter = new();

    private static Mesh? _treeMesh;
    private static Mesh? _rockMesh;
    private static readonly Dictionary<TerrainLandmarkKind, Mesh> LandmarkMeshes = new();

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
        RebuildLandmarkScatter(data);
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

    private void RebuildLandmarkScatter(TerrainTileData data)
    {
        var activeKinds = new HashSet<TerrainLandmarkKind>();
        foreach (TerrainScatterInstance instance in data.ScatterInstances)
        {
            if (instance.Kind == TerrainScatterKind.Landmark)
            {
                activeKinds.Add(instance.LandmarkKind);
            }
        }

        var staleKinds = new List<TerrainLandmarkKind>();
        foreach (TerrainLandmarkKind kind in _landmarkScatter.Keys)
        {
            if (!activeKinds.Contains(kind))
            {
                staleKinds.Add(kind);
            }
        }

        foreach (TerrainLandmarkKind kind in staleKinds)
        {
            _landmarkScatter[kind].QueueFree();
            _landmarkScatter.Remove(kind);
        }

        foreach (TerrainLandmarkKind kind in activeKinds)
        {
            _landmarkScatter.TryGetValue(kind, out MultiMeshInstance3D? existing);
            MultiMeshInstance3D? rebuilt = RebuildLandmarkKind(data, kind, existing);
            if (rebuilt is not null)
            {
                _landmarkScatter[kind] = rebuilt;
            }
        }
    }

    private MultiMeshInstance3D? RebuildLandmarkKind(
        TerrainTileData data,
        TerrainLandmarkKind landmarkKind,
        MultiMeshInstance3D? existing)
    {
        int count = 0;
        foreach (TerrainScatterInstance instance in data.ScatterInstances)
        {
            if (instance.Kind == TerrainScatterKind.Landmark && instance.LandmarkKind == landmarkKind)
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

        existing ??= CreateScatterNode($"Landmarks_{landmarkKind}");

        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = GetLandmarkMesh(landmarkKind),
            CustomAabb = new Aabb(
                new Vector3(0.0f, data.MinHeight - 8.0f, 0.0f),
                new Vector3(data.ChunkSize, data.MaxHeight - data.MinHeight + 132.0f, data.ChunkSize))
        };

        multimesh.InstanceCount = count;
        multimesh.VisibleInstanceCount = count;

        int index = 0;
        foreach (TerrainScatterInstance instance in data.ScatterInstances)
        {
            if (instance.Kind != TerrainScatterKind.Landmark || instance.LandmarkKind != landmarkKind)
            {
                continue;
            }

            Basis basis = BasisForLandmark(landmarkKind, instance.RotationY, instance.UniformScale);
            var transform = new Transform3D(
                basis,
                instance.LocalPosition + Vector3.Up * LandmarkVerticalOffset(landmarkKind, instance.UniformScale));

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

    private static Mesh GetLandmarkMesh(TerrainLandmarkKind kind)
    {
        if (LandmarkMeshes.TryGetValue(kind, out Mesh? cached))
        {
            return cached;
        }

        Mesh mesh = kind switch
        {
            TerrainLandmarkKind.Settlement => new BoxMesh { Size = new Vector3(1.75f, 0.72f, 1.45f) },
            TerrainLandmarkKind.Vista => new CylinderMesh
            {
                TopRadius = 0.12f,
                BottomRadius = 0.48f,
                Height = 2.85f,
                RadialSegments = 8,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.RiverCrossing => new BoxMesh { Size = new Vector3(2.45f, 0.22f, 0.72f) },
            TerrainLandmarkKind.MountainPass => new CylinderMesh
            {
                TopRadius = 0.32f,
                BottomRadius = 0.82f,
                Height = 1.65f,
                RadialSegments = 7,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.CoastalLanding => new CylinderMesh
            {
                TopRadius = 0.62f,
                BottomRadius = 0.82f,
                Height = 0.48f,
                RadialSegments = 10,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.ResourceGrove => new SphereMesh
            {
                Radius = 0.88f,
                Height = 1.35f,
                RadialSegments = 9,
                Rings = 5
            },
            TerrainLandmarkKind.CanyonOverlook => new BoxMesh { Size = new Vector3(1.85f, 0.38f, 1.15f) },
            _ => new BoxMesh { Size = new Vector3(0.78f, 2.6f, 0.78f) }
        };

        if (mesh is PrimitiveMesh primitiveMesh)
        {
            primitiveMesh.Material = TerrainMaterialFactory.CreateLandmarkMaterial();
        }

        LandmarkMeshes[kind] = mesh;
        return mesh;
    }

    private static Basis BasisForLandmark(TerrainLandmarkKind kind, float rotationY, float scale)
    {
        Vector3 axisScale = kind switch
        {
            TerrainLandmarkKind.Settlement => new Vector3(scale * 1.25f, scale * 0.62f, scale),
            TerrainLandmarkKind.RiverCrossing => new Vector3(scale * 1.55f, scale * 0.30f, scale * 0.78f),
            TerrainLandmarkKind.ResourceGrove => new Vector3(scale * 0.95f, scale * 1.18f, scale * 0.95f),
            TerrainLandmarkKind.CanyonOverlook => new Vector3(scale * 1.45f, scale * 0.36f, scale),
            TerrainLandmarkKind.Vista => new Vector3(scale * 0.86f, scale * 1.42f, scale * 0.86f),
            _ => Vector3.One * scale
        };

        return new Basis(Vector3.Up, rotationY).Scaled(axisScale);
    }

    private static float LandmarkVerticalOffset(TerrainLandmarkKind kind, float scale)
    {
        float multiplier = kind switch
        {
            TerrainLandmarkKind.RiverCrossing => 0.18f,
            TerrainLandmarkKind.CoastalLanding => 0.22f,
            TerrainLandmarkKind.CanyonOverlook => 0.28f,
            TerrainLandmarkKind.Settlement => 0.34f,
            TerrainLandmarkKind.ResourceGrove => 0.72f,
            _ => 0.64f
        };

        return multiplier * scale;
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
