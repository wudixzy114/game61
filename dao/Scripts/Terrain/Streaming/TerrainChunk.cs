using System.Collections.Generic;
using Dao.Terrain.Generation;
using Dao.Terrain.Rendering;
using Godot;

namespace Dao.Terrain.Streaming;

public partial class TerrainChunk : Node3D
{
    private MeshInstance3D? _meshInstance;
    private StaticBody3D? _staticBody;
    private readonly Dictionary<TerrainScatterKind, MultiMeshInstance3D> _scatterNodes = new();
    private readonly Dictionary<TerrainLandmarkKind, MultiMeshInstance3D> _landmarkScatter = new();

    private static readonly Dictionary<TerrainScatterKind, Mesh> ScatterMeshes = new();
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
        RebuildSurfaceScatter(data);
        RebuildLandmarkScatter(data);
    }

    private void RebuildSurfaceScatter(TerrainTileData data)
    {
        var activeKinds = new HashSet<TerrainScatterKind>();
        foreach (TerrainScatterInstance instance in data.ScatterInstances)
        {
            if (instance.Kind != TerrainScatterKind.Landmark)
            {
                activeKinds.Add(instance.Kind);
            }
        }

        var staleKinds = new List<TerrainScatterKind>();
        foreach (TerrainScatterKind kind in _scatterNodes.Keys)
        {
            if (!activeKinds.Contains(kind))
            {
                staleKinds.Add(kind);
            }
        }

        foreach (TerrainScatterKind kind in staleKinds)
        {
            _scatterNodes[kind].QueueFree();
            _scatterNodes.Remove(kind);
        }

        foreach (TerrainScatterKind kind in activeKinds)
        {
            _scatterNodes.TryGetValue(kind, out MultiMeshInstance3D? existing);
            MultiMeshInstance3D? rebuilt = RebuildScatterKind(data, kind, existing);
            if (rebuilt is not null)
            {
                _scatterNodes[kind] = rebuilt;
            }
        }
    }

    private MultiMeshInstance3D? RebuildScatterKind(
        TerrainTileData data,
        TerrainScatterKind kind,
        MultiMeshInstance3D? existing)
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

        ScatterVisual visual = VisualForScatter(kind);
        existing ??= CreateScatterNode(visual.NodeName);

        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = GetScatterMesh(kind),
            CustomAabb = new Aabb(
                new Vector3(0.0f, data.MinHeight - 8.0f, 0.0f),
                new Vector3(data.ChunkSize, data.MaxHeight - data.MinHeight + visual.AabbHeightPadding, data.ChunkSize))
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
                .Scaled(visual.AxisScale * instance.UniformScale);
            var transform = new Transform3D(
                basis,
                instance.LocalPosition + Vector3.Up * visual.VerticalOffset * instance.UniformScale);

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

    private static Mesh GetScatterMesh(TerrainScatterKind kind)
    {
        if (ScatterMeshes.TryGetValue(kind, out Mesh? cached))
        {
            return cached;
        }

        Mesh mesh = kind switch
        {
            TerrainScatterKind.Tree => new CylinderMesh
            {
                TopRadius = 0.0f,
                BottomRadius = 0.42f,
                Height = 2.35f,
                RadialSegments = 7,
                Rings = 1,
                CapBottom = true
            },
            TerrainScatterKind.Rock => new SphereMesh
            {
                Radius = 0.72f,
                Height = 0.86f,
                RadialSegments = 8,
                Rings = 4
            },
            TerrainScatterKind.Understory => new CylinderMesh
            {
                TopRadius = 0.22f,
                BottomRadius = 0.46f,
                Height = 0.84f,
                RadialSegments = 6,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainScatterKind.ResourceNode => new SphereMesh
            {
                Radius = 0.54f,
                Height = 0.88f,
                RadialSegments = 7,
                Rings = 4
            },
            TerrainScatterKind.HazardOutcrop => new BoxMesh { Size = new Vector3(0.92f, 0.68f, 0.76f) },
            TerrainScatterKind.GrassTuft => new CylinderMesh
            {
                TopRadius = 0.12f,
                BottomRadius = 0.28f,
                Height = 0.72f,
                RadialSegments = 5,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainScatterKind.DesertShrub => new SphereMesh
            {
                Radius = 0.42f,
                Height = 0.58f,
                RadialSegments = 6,
                Rings = 3
            },
            TerrainScatterKind.CactusCluster => new CylinderMesh
            {
                TopRadius = 0.16f,
                BottomRadius = 0.24f,
                Height = 1.46f,
                RadialSegments = 6,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainScatterKind.ReedCluster => new CylinderMesh
            {
                TopRadius = 0.18f,
                BottomRadius = 0.30f,
                Height = 1.08f,
                RadialSegments = 5,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainScatterKind.SnowClump => new SphereMesh
            {
                Radius = 0.48f,
                Height = 0.52f,
                RadialSegments = 7,
                Rings = 3
            },
            TerrainScatterKind.AlpinePine => new CylinderMesh
            {
                TopRadius = 0.02f,
                BottomRadius = 0.38f,
                Height = 1.74f,
                RadialSegments = 7,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainScatterKind.CoastalPalm => new CylinderMesh
            {
                TopRadius = 0.10f,
                BottomRadius = 0.26f,
                Height = 1.84f,
                RadialSegments = 6,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainScatterKind.Driftwood => new BoxMesh { Size = new Vector3(1.28f, 0.20f, 0.38f) },
            TerrainScatterKind.MangroveRoot => new BoxMesh { Size = new Vector3(1.12f, 0.42f, 0.86f) },
            _ => new SphereMesh
            {
                Radius = 0.5f,
                Height = 0.8f,
                RadialSegments = 6,
                Rings = 3
            }
        };

        if (mesh is PrimitiveMesh primitiveMesh)
        {
            primitiveMesh.Material = TerrainMaterialFactory.CreateScatterMaterial(kind);
        }

        ScatterMeshes[kind] = mesh;
        return mesh;
    }

    private static ScatterVisual VisualForScatter(TerrainScatterKind kind)
    {
        return kind switch
        {
            TerrainScatterKind.Tree => new ScatterVisual("Trees", 1.18f, Vector3.One, 96.0f),
            TerrainScatterKind.Rock => new ScatterVisual("Rocks", 0.38f, Vector3.One, 96.0f),
            TerrainScatterKind.Understory => new ScatterVisual("Understory", 0.32f, new Vector3(0.82f, 0.74f, 0.82f), 48.0f),
            TerrainScatterKind.ResourceNode => new ScatterVisual("ResourceNodes", 0.38f, new Vector3(1.05f, 0.78f, 1.05f), 64.0f),
            TerrainScatterKind.HazardOutcrop => new ScatterVisual("HazardOutcrops", 0.44f, new Vector3(1.20f, 0.82f, 1.05f), 80.0f),
            TerrainScatterKind.GrassTuft => new ScatterVisual("GrassTufts", 0.28f, new Vector3(0.76f, 0.62f, 0.76f), 42.0f),
            TerrainScatterKind.DesertShrub => new ScatterVisual("DesertShrubs", 0.22f, new Vector3(1.12f, 0.64f, 1.12f), 42.0f),
            TerrainScatterKind.CactusCluster => new ScatterVisual("CactusClusters", 0.74f, new Vector3(0.70f, 1.42f, 0.70f), 58.0f),
            TerrainScatterKind.ReedCluster => new ScatterVisual("ReedClusters", 0.42f, new Vector3(0.72f, 0.96f, 0.72f), 52.0f),
            TerrainScatterKind.SnowClump => new ScatterVisual("SnowClumps", 0.18f, new Vector3(1.18f, 0.48f, 1.18f), 42.0f),
            TerrainScatterKind.AlpinePine => new ScatterVisual("AlpinePines", 0.84f, new Vector3(0.82f, 1.36f, 0.82f), 68.0f),
            TerrainScatterKind.CoastalPalm => new ScatterVisual("CoastalPalms", 0.92f, new Vector3(0.82f, 1.42f, 0.82f), 72.0f),
            TerrainScatterKind.Driftwood => new ScatterVisual("Driftwood", 0.12f, new Vector3(1.52f, 0.32f, 0.64f), 36.0f),
            TerrainScatterKind.MangroveRoot => new ScatterVisual("MangroveRoots", 0.24f, new Vector3(1.18f, 0.70f, 0.92f), 42.0f),
            _ => new ScatterVisual(kind.ToString(), 0.35f, Vector3.One, 64.0f)
        };
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
            TerrainLandmarkKind.Oasis => new CylinderMesh
            {
                TopRadius = 0.78f,
                BottomRadius = 0.60f,
                Height = 0.34f,
                RadialSegments = 14,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.Village => new BoxMesh { Size = new Vector3(1.55f, 0.58f, 1.18f) },
            TerrainLandmarkKind.Town => new BoxMesh { Size = new Vector3(2.15f, 0.82f, 1.72f) },
            TerrainLandmarkKind.OasisHub => new CylinderMesh
            {
                TopRadius = 0.96f,
                BottomRadius = 0.70f,
                Height = 0.42f,
                RadialSegments = 14,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.VillageHouse => new BoxMesh { Size = new Vector3(1.05f, 0.78f, 0.86f) },
            TerrainLandmarkKind.TownBlock => new BoxMesh { Size = new Vector3(1.32f, 1.18f, 1.08f) },
            TerrainLandmarkKind.OasisCanopy => new CylinderMesh
            {
                TopRadius = 0.42f,
                BottomRadius = 0.72f,
                Height = 0.88f,
                RadialSegments = 7,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.SettlementPlaza => new BoxMesh { Size = new Vector3(1.55f, 0.14f, 1.55f) },
            TerrainLandmarkKind.OasisPool => new CylinderMesh
            {
                TopRadius = 0.92f,
                BottomRadius = 0.92f,
                Height = 0.08f,
                RadialSegments = 18,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.Waterfall => new BoxMesh { Size = new Vector3(0.72f, 2.65f, 0.34f) },
            TerrainLandmarkKind.RoadMarker => new CylinderMesh
            {
                TopRadius = 0.12f,
                BottomRadius = 0.18f,
                Height = 1.46f,
                RadialSegments = 6,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.BridgeSpan => new BoxMesh { Size = new Vector3(1.68f, 0.18f, 0.78f) },
            TerrainLandmarkKind.DuneCrest => new BoxMesh { Size = new Vector3(2.40f, 0.22f, 0.62f) },
            TerrainLandmarkKind.DesertMonolith => new CylinderMesh
            {
                TopRadius = 0.22f,
                BottomRadius = 0.72f,
                Height = 2.70f,
                RadialSegments = 7,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.CanyonNeedle => new CylinderMesh
            {
                TopRadius = 0.16f,
                BottomRadius = 0.58f,
                Height = 3.10f,
                RadialSegments = 6,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.IceSpire => new CylinderMesh
            {
                TopRadius = 0.08f,
                BottomRadius = 0.46f,
                Height = 2.85f,
                RadialSegments = 7,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.NaturalArch => new BoxMesh { Size = new Vector3(2.25f, 1.18f, 0.42f) },
            TerrainLandmarkKind.GeothermalSpring => new CylinderMesh
            {
                TopRadius = 0.92f,
                BottomRadius = 0.72f,
                Height = 0.16f,
                RadialSegments = 18,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.GlacialRidge => new BoxMesh { Size = new Vector3(2.60f, 0.42f, 0.88f) },
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
            TerrainLandmarkKind.Oasis => new Vector3(scale * 1.42f, scale * 0.42f, scale * 1.42f),
            TerrainLandmarkKind.Village => new Vector3(scale * 1.28f, scale * 0.58f, scale),
            TerrainLandmarkKind.Town => new Vector3(scale * 1.48f, scale * 0.76f, scale * 1.22f),
            TerrainLandmarkKind.OasisHub => new Vector3(scale * 1.58f, scale * 0.48f, scale * 1.58f),
            TerrainLandmarkKind.VillageHouse => new Vector3(scale * 1.08f, scale * 0.82f, scale * 0.92f),
            TerrainLandmarkKind.TownBlock => new Vector3(scale * 1.16f, scale * 1.08f, scale),
            TerrainLandmarkKind.OasisCanopy => new Vector3(scale * 1.10f, scale * 0.74f, scale * 1.10f),
            TerrainLandmarkKind.SettlementPlaza => new Vector3(scale * 1.45f, scale * 0.16f, scale * 1.45f),
            TerrainLandmarkKind.OasisPool => new Vector3(scale * 1.62f, scale * 0.08f, scale * 1.62f),
            TerrainLandmarkKind.Waterfall => new Vector3(scale * 0.58f, scale * 2.10f, scale * 0.38f),
            TerrainLandmarkKind.RoadMarker => new Vector3(scale * 0.38f, scale * 1.18f, scale * 0.38f),
            TerrainLandmarkKind.BridgeSpan => new Vector3(scale * 1.94f, scale * 0.20f, scale * 0.78f),
            TerrainLandmarkKind.DuneCrest => new Vector3(scale * 2.20f, scale * 0.24f, scale * 0.72f),
            TerrainLandmarkKind.DesertMonolith => new Vector3(scale * 0.78f, scale * 1.85f, scale * 0.72f),
            TerrainLandmarkKind.CanyonNeedle => new Vector3(scale * 0.64f, scale * 2.18f, scale * 0.58f),
            TerrainLandmarkKind.IceSpire => new Vector3(scale * 0.52f, scale * 1.92f, scale * 0.52f),
            TerrainLandmarkKind.NaturalArch => new Vector3(scale * 1.88f, scale * 1.08f, scale * 0.44f),
            TerrainLandmarkKind.GeothermalSpring => new Vector3(scale * 1.42f, scale * 0.10f, scale * 1.42f),
            TerrainLandmarkKind.GlacialRidge => new Vector3(scale * 2.18f, scale * 0.36f, scale * 0.88f),
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
            TerrainLandmarkKind.Oasis => 0.24f,
            TerrainLandmarkKind.Village => 0.32f,
            TerrainLandmarkKind.Town => 0.42f,
            TerrainLandmarkKind.OasisHub => 0.28f,
            TerrainLandmarkKind.VillageHouse => 0.38f,
            TerrainLandmarkKind.TownBlock => 0.54f,
            TerrainLandmarkKind.OasisCanopy => 0.34f,
            TerrainLandmarkKind.SettlementPlaza => 0.08f,
            TerrainLandmarkKind.OasisPool => 0.03f,
            TerrainLandmarkKind.Waterfall => 0.84f,
            TerrainLandmarkKind.RoadMarker => 0.58f,
            TerrainLandmarkKind.BridgeSpan => 0.10f,
            TerrainLandmarkKind.DuneCrest => 0.08f,
            TerrainLandmarkKind.DesertMonolith => 0.84f,
            TerrainLandmarkKind.CanyonNeedle => 1.06f,
            TerrainLandmarkKind.IceSpire => 0.96f,
            TerrainLandmarkKind.NaturalArch => 0.46f,
            TerrainLandmarkKind.GeothermalSpring => 0.04f,
            TerrainLandmarkKind.GlacialRidge => 0.18f,
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

    private readonly record struct ScatterVisual(
        string NodeName,
        float VerticalOffset,
        Vector3 AxisScale,
        float AabbHeightPadding);
}
