using System.Collections.Generic;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Streaming;

public partial class TerrainChunk
{
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

        staleKinds.Clear();
        foreach (TerrainLandmarkKind kind in _landmarkSceneNodes.Keys)
        {
            if (!activeKinds.Contains(kind))
            {
                staleKinds.Add(kind);
            }
        }

        foreach (TerrainLandmarkKind kind in staleKinds)
        {
            _landmarkSceneNodes[kind].QueueFree();
            _landmarkSceneNodes.Remove(kind);
        }

        foreach (TerrainLandmarkKind kind in activeKinds)
        {
            _landmarkScatter.TryGetValue(kind, out MultiMeshInstance3D? existing);
            MultiMeshInstance3D? rebuilt = RebuildLandmarkKind(data, kind, existing);
            if (rebuilt is not null)
            {
                _landmarkScatter[kind] = rebuilt;
            }
            else
            {
                _landmarkScatter.Remove(kind);
            }
        }
    }

    private MultiMeshInstance3D? RebuildLandmarkKind(
        TerrainTileData data,
        TerrainLandmarkKind landmarkKind,
        MultiMeshInstance3D? existing)
    {
        ScatterVisual visual = VisualForLandmark(landmarkKind);
        int count = 0;
        int candidateIndex = 0;
        foreach (TerrainScatterInstance instance in data.ScatterInstances)
        {
            if (instance.Kind == TerrainScatterKind.Landmark && instance.LandmarkKind == landmarkKind)
            {
                if (ShouldRenderVisualInstance(candidateIndex, count, visual))
                {
                    count++;
                }

                candidateIndex++;
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

        if (UsesSceneInstances(visual))
        {
            if (existing is not null)
            {
                existing.QueueFree();
            }

            RebuildLandmarkSceneKind(data, landmarkKind, count, visual);
            return null;
        }

        if (_landmarkSceneNodes.Remove(landmarkKind, out Node3D? sceneContainer))
        {
            sceneContainer.QueueFree();
        }

        Mesh? mesh = visual.Mesh;
        if (mesh is null)
        {
            if (existing is not null)
            {
                existing.QueueFree();
            }

            return null;
        }

        existing ??= CreateScatterNode(visual.NodeName);

        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = mesh,
            CustomAabb = new Aabb(
                new Vector3(0.0f, data.MinHeight - 8.0f, 0.0f),
                new Vector3(data.ChunkSize, data.MaxHeight - data.MinHeight + visual.AabbHeightPadding, data.ChunkSize))
        };

        multimesh.InstanceCount = count;
        multimesh.VisibleInstanceCount = count;

        int index = 0;
        candidateIndex = 0;
        foreach (TerrainScatterInstance instance in data.ScatterInstances)
        {
            if (instance.Kind != TerrainScatterKind.Landmark || instance.LandmarkKind != landmarkKind)
            {
                continue;
            }

            if (!ShouldRenderVisualInstance(candidateIndex, index, visual))
            {
                candidateIndex++;
                continue;
            }

            Basis basis = new Basis(Vector3.Up, instance.RotationY)
                .Scaled(visual.AxisScale * instance.UniformScale);
            var transform = new Transform3D(
                basis,
                instance.LocalPosition + Vector3.Up * visual.VerticalOffset * instance.UniformScale);

            multimesh.SetInstanceTransform(index, transform);
            multimesh.SetInstanceColor(index, instance.Color);
            index++;
            candidateIndex++;
        }

        existing.Multimesh = multimesh;
        return existing;
    }

    private void RebuildLandmarkSceneKind(
        TerrainTileData data,
        TerrainLandmarkKind landmarkKind,
        int count,
        ScatterVisual visual)
    {
        PackedScene? scene = visual.Scene;
        if (scene is null || count == 0)
        {
            if (_landmarkSceneNodes.Remove(landmarkKind, out Node3D? stale))
            {
                stale.QueueFree();
            }

            return;
        }

        if (!_landmarkSceneNodes.TryGetValue(landmarkKind, out Node3D? container))
        {
            container = CreateSceneContainer(visual.NodeName);
            _landmarkSceneNodes[landmarkKind] = container;
        }
        else
        {
            container.Name = $"{visual.NodeName}_Scenes";
            ClearSceneContainer(container);
        }

        int candidateIndex = 0;
        int emittedCount = 0;
        foreach (TerrainScatterInstance instance in data.ScatterInstances)
        {
            if (instance.Kind != TerrainScatterKind.Landmark || instance.LandmarkKind != landmarkKind)
            {
                continue;
            }

            if (!ShouldRenderVisualInstance(candidateIndex, emittedCount, visual))
            {
                candidateIndex++;
                continue;
            }

            AddSceneInstance(container, scene, TransformForInstance(instance, visual), visual, instance.Color);
            emittedCount++;
            candidateIndex++;
        }
    }

    private ScatterVisual VisualForLandmark(TerrainLandmarkKind kind)
    {
        TerrainLandmarkVisualEntryResource? entry = _visualCatalog?.GetLandmarkEntry(kind, Lod);
        ScatterVisual fallback = DefaultVisualForLandmark(kind);
        if (entry is null)
        {
            return _visualCatalog?.UsePrimitiveFallbacks == false
                ? fallback with { Mesh = null }
                : fallback;
        }

        string nodeName = string.IsNullOrWhiteSpace(entry.NodeName)
            ? fallback.NodeName
            : entry.NodeName;
        Mesh? mesh = entry.Mesh ?? (_visualCatalog?.UsePrimitiveFallbacks == false ? null : fallback.Mesh);
        PackedScene? scene = entry.Scene;
        return new ScatterVisual(
            nodeName,
            mesh,
            scene,
            entry.PreferSceneInstances,
            entry.VerticalOffset,
            entry.AxisScale,
            entry.AabbHeightPadding,
            entry.CreatesCollision,
            entry.CreatesNavigationObstacle,
            entry.InteractionTag,
            entry.DensityMultiplier,
            entry.MaxInstancesPerTile);
    }

    private static ScatterVisual DefaultVisualForLandmark(TerrainLandmarkKind kind)
    {
        Vector3 axisScale = kind switch
        {
            TerrainLandmarkKind.Settlement => new Vector3(1.25f, 0.62f, 1.0f),
            TerrainLandmarkKind.RiverCrossing => new Vector3(1.55f, 0.30f, 0.78f),
            TerrainLandmarkKind.ResourceGrove => new Vector3(0.95f, 1.18f, 0.95f),
            TerrainLandmarkKind.CanyonOverlook => new Vector3(1.45f, 0.36f, 1.0f),
            TerrainLandmarkKind.Oasis => new Vector3(1.42f, 0.42f, 1.42f),
            TerrainLandmarkKind.Village => new Vector3(1.28f, 0.58f, 1.0f),
            TerrainLandmarkKind.Town => new Vector3(1.48f, 0.76f, 1.22f),
            TerrainLandmarkKind.OasisHub => new Vector3(1.58f, 0.48f, 1.58f),
            TerrainLandmarkKind.VillageHouse => new Vector3(1.08f, 0.82f, 0.92f),
            TerrainLandmarkKind.TownBlock => new Vector3(1.16f, 1.08f, 1.0f),
            TerrainLandmarkKind.OasisCanopy => new Vector3(1.10f, 0.74f, 1.10f),
            TerrainLandmarkKind.SettlementPlaza => new Vector3(1.45f, 0.16f, 1.45f),
            TerrainLandmarkKind.OasisPool => new Vector3(1.62f, 0.08f, 1.62f),
            TerrainLandmarkKind.VillageWell => new Vector3(0.92f, 0.42f, 0.92f),
            TerrainLandmarkKind.MarketStall => new Vector3(1.18f, 0.58f, 0.82f),
            TerrainLandmarkKind.WatchTower => new Vector3(0.58f, 2.05f, 0.58f),
            TerrainLandmarkKind.OasisGarden => new Vector3(1.32f, 0.22f, 1.32f),
            TerrainLandmarkKind.SettlementGateway => new Vector3(1.75f, 1.02f, 0.32f),
            TerrainLandmarkKind.Waterfall => new Vector3(0.58f, 2.10f, 0.38f),
            TerrainLandmarkKind.RoadMarker => new Vector3(0.38f, 1.18f, 0.38f),
            TerrainLandmarkKind.BridgeSpan => new Vector3(1.94f, 0.20f, 0.78f),
            TerrainLandmarkKind.DuneCrest => new Vector3(2.20f, 0.24f, 0.72f),
            TerrainLandmarkKind.DesertMonolith => new Vector3(0.78f, 1.85f, 0.72f),
            TerrainLandmarkKind.CanyonNeedle => new Vector3(0.64f, 2.18f, 0.58f),
            TerrainLandmarkKind.IceSpire => new Vector3(0.52f, 1.92f, 0.52f),
            TerrainLandmarkKind.NaturalArch => new Vector3(1.88f, 1.08f, 0.44f),
            TerrainLandmarkKind.GeothermalSpring => new Vector3(1.42f, 0.10f, 1.42f),
            TerrainLandmarkKind.GlacialRidge => new Vector3(2.18f, 0.36f, 0.88f),
            TerrainLandmarkKind.Vista => new Vector3(0.86f, 1.42f, 0.86f),
            _ => Vector3.One
        };

        return new ScatterVisual(
            $"Landmarks_{kind}",
            GetLandmarkMesh(kind),
            null,
            false,
            LandmarkVerticalOffset(kind),
            axisScale,
            132.0f,
            false,
            false,
            string.Empty);
    }

    private static float LandmarkVerticalOffset(TerrainLandmarkKind kind)
    {
        return kind switch
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
            TerrainLandmarkKind.VillageWell => 0.20f,
            TerrainLandmarkKind.MarketStall => 0.30f,
            TerrainLandmarkKind.WatchTower => 0.98f,
            TerrainLandmarkKind.OasisGarden => 0.12f,
            TerrainLandmarkKind.SettlementGateway => 0.50f,
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
    }
}
