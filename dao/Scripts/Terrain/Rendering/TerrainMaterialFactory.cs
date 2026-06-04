using Godot;
using Dao.Terrain.Generation;

namespace Dao.Terrain.Rendering;

/// <summary>Factory for creating StandardMaterial3D instances used by terrain, water, scatter, landmark, and plan overlay rendering.</summary>
public static class TerrainMaterialFactory
{
    /// <summary>Creates the main terrain surface material using vertex colors for albedo.</summary>
    public static StandardMaterial3D CreateTerrainMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.92f,
            Metallic = 0.0f
        };
    }

    /// <summary>Creates a semi-transparent water material.</summary>
    public static StandardMaterial3D CreateWaterMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = new Color(0.05f, 0.20f, 0.30f, 0.58f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.18f,
            Metallic = 0.0f
        };
    }

    /// <summary>Creates a vertex-colored transparent material for tile-local rivers, lakes, and oasis pools.</summary>
    public static StandardMaterial3D CreateLocalWaterMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.14f,
            Metallic = 0.0f,
            EmissionEnabled = true,
            Emission = new Color(0.03f, 0.10f, 0.13f),
            EmissionEnergyMultiplier = 0.08f
        };
    }

    /// <summary>Creates a vertex-colored material for tree scatter meshes.</summary>
    public static StandardMaterial3D CreateTreeMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            Roughness = 0.86f,
            Metallic = 0.0f
        };
    }

    /// <summary>Creates a vertex-colored material for rock scatter meshes.</summary>
    public static StandardMaterial3D CreateRockMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            Roughness = 0.96f,
            Metallic = 0.0f
        };
    }

    /// <summary>Creates a material appropriate for the given scatter kind, with per-kind emission and roughness overrides.</summary>
    public static StandardMaterial3D CreateScatterMaterial(TerrainScatterKind kind)
    {
        StandardMaterial3D material = kind switch
        {
            TerrainScatterKind.Tree => CreateTreeMaterial(),
            TerrainScatterKind.Rock => CreateRockMaterial(),
            TerrainScatterKind.Understory => new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
                Roughness = 0.88f,
                Metallic = 0.0f
            },
            TerrainScatterKind.ResourceNode => new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
                Roughness = 0.82f,
                Metallic = 0.0f,
                EmissionEnabled = true,
                Emission = new Color(0.12f, 0.16f, 0.06f),
                EmissionEnergyMultiplier = 0.10f
            },
            TerrainScatterKind.HazardOutcrop => new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
                Roughness = 0.98f,
                Metallic = 0.0f
            },
            TerrainScatterKind.GrassTuft => new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
                Roughness = 0.90f,
                Metallic = 0.0f
            },
            TerrainScatterKind.DesertShrub => new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
                Roughness = 0.94f,
                Metallic = 0.0f
            },
            TerrainScatterKind.CactusCluster => new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
                Roughness = 0.90f,
                Metallic = 0.0f
            },
            TerrainScatterKind.ReedCluster => new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
                Roughness = 0.88f,
                Metallic = 0.0f
            },
            TerrainScatterKind.SnowClump => new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
                Roughness = 0.76f,
                Metallic = 0.0f
            },
            TerrainScatterKind.AlpinePine => new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
                Roughness = 0.88f,
                Metallic = 0.0f
            },
            TerrainScatterKind.CoastalPalm => new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
                Roughness = 0.86f,
                Metallic = 0.0f
            },
            TerrainScatterKind.Driftwood => new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
                Roughness = 0.94f,
                Metallic = 0.0f
            },
            TerrainScatterKind.MangroveRoot => new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
                Roughness = 0.96f,
                Metallic = 0.0f
            },
            TerrainScatterKind.LakeReed => new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
                Roughness = 0.86f,
                Metallic = 0.0f
            },
            TerrainScatterKind.WaterLily => new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
                Roughness = 0.72f,
                Metallic = 0.0f
            },
            _ => CreateRockMaterial()
        };

        return material;
    }

    /// <summary>Creates a vertex-colored material with subtle emission for landmark meshes.</summary>
    public static StandardMaterial3D CreateLandmarkMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            Roughness = 0.9f,
            Metallic = 0.0f,
            EmissionEnabled = true,
            Emission = new Color(0.18f, 0.14f, 0.08f),
            EmissionEnergyMultiplier = 0.25f
        };
    }

    /// <summary>Creates an unshaded, emissive material for plan overlay POI markers.</summary>
    public static StandardMaterial3D CreatePlanMarkerMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.74f,
            Metallic = 0.0f,
            EmissionEnabled = true,
            Emission = Colors.White,
            EmissionEnergyMultiplier = 0.35f
        };
    }

    /// <summary>Creates an unshaded, alpha-transparent material for plan overlay route ribbons.</summary>
    public static StandardMaterial3D CreatePlanRouteMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.8f,
            Metallic = 0.0f
        };
    }
}
