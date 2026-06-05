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
            TerrainLandmarkKind.VillageWell => new Vector3(scale * 0.92f, scale * 0.42f, scale * 0.92f),
            TerrainLandmarkKind.MarketStall => new Vector3(scale * 1.18f, scale * 0.58f, scale * 0.82f),
            TerrainLandmarkKind.WatchTower => new Vector3(scale * 0.58f, scale * 2.05f, scale * 0.58f),
            TerrainLandmarkKind.OasisGarden => new Vector3(scale * 1.32f, scale * 0.22f, scale * 1.32f),
            TerrainLandmarkKind.SettlementGateway => new Vector3(scale * 1.75f, scale * 1.02f, scale * 0.32f),
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

        return multiplier * scale;
    }
}
