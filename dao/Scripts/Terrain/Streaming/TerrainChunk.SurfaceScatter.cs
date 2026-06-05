using System.Collections.Generic;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Streaming;

public partial class TerrainChunk
{
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
            TerrainScatterKind.LakeReed => new ScatterVisual("LakeReeds", 0.34f, new Vector3(0.70f, 1.10f, 0.70f), 52.0f),
            TerrainScatterKind.WaterLily => new ScatterVisual("WaterLilies", 0.04f, new Vector3(1.18f, 0.10f, 1.18f), 24.0f),
            _ => new ScatterVisual(kind.ToString(), 0.35f, Vector3.One, 64.0f)
        };
    }
}
