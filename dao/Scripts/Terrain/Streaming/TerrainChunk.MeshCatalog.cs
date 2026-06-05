using Dao.Terrain.Generation;
using Dao.Terrain.Rendering;
using Godot;

namespace Dao.Terrain.Streaming;

public partial class TerrainChunk
{
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
            TerrainScatterKind.LakeReed => new CylinderMesh
            {
                TopRadius = 0.10f,
                BottomRadius = 0.22f,
                Height = 1.18f,
                RadialSegments = 5,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainScatterKind.WaterLily => new CylinderMesh
            {
                TopRadius = 0.42f,
                BottomRadius = 0.46f,
                Height = 0.06f,
                RadialSegments = 12,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
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
            TerrainLandmarkKind.VillageWell => new CylinderMesh
            {
                TopRadius = 0.48f,
                BottomRadius = 0.54f,
                Height = 0.46f,
                RadialSegments = 12,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.MarketStall => new BoxMesh { Size = new Vector3(1.20f, 0.54f, 0.84f) },
            TerrainLandmarkKind.WatchTower => new CylinderMesh
            {
                TopRadius = 0.24f,
                BottomRadius = 0.46f,
                Height = 2.35f,
                RadialSegments = 6,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.OasisGarden => new CylinderMesh
            {
                TopRadius = 0.74f,
                BottomRadius = 0.82f,
                Height = 0.32f,
                RadialSegments = 10,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainLandmarkKind.SettlementGateway => new BoxMesh { Size = new Vector3(2.25f, 1.12f, 0.34f) },
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
}
