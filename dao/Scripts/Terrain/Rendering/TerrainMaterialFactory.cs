using Godot;

namespace Dao.Terrain.Rendering;

public static class TerrainMaterialFactory
{
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
