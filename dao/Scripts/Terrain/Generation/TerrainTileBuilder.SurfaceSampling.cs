using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static float SampleHeightBilinear(float localX, float localZ, int resolution, float step, float[] heights, int vertexCountPerSide)
    {
        float gx = Mathf.Clamp(localX / step, 0.0f, resolution);
        float gz = Mathf.Clamp(localZ / step, 0.0f, resolution);
        int x0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, resolution);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(gz), 0, resolution);
        int x1 = Mathf.Min(resolution, x0 + 1);
        int z1 = Mathf.Min(resolution, z0 + 1);
        float tx = gx - x0;
        float tz = gz - z0;

        float a = heights[Index(x0, z0, vertexCountPerSide)];
        float b = heights[Index(x1, z0, vertexCountPerSide)];
        float c = heights[Index(x0, z1, vertexCountPerSide)];
        float d = heights[Index(x1, z1, vertexCountPerSide)];
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
    }

    private static TerrainWorldField SampleFieldBilinear(
        float localX,
        float localZ,
        int resolution,
        float step,
        TerrainWorldField[] fields,
        int vertexCountPerSide)
    {
        float gx = Mathf.Clamp(localX / step, 0.0f, resolution);
        float gz = Mathf.Clamp(localZ / step, 0.0f, resolution);
        int x0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, resolution);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(gz), 0, resolution);
        int x1 = Mathf.Min(resolution, x0 + 1);
        int z1 = Mathf.Min(resolution, z0 + 1);
        float tx = gx - x0;
        float tz = gz - z0;

        TerrainWorldField a = fields[Index(x0, z0, vertexCountPerSide)];
        TerrainWorldField b = fields[Index(x1, z0, vertexCountPerSide)];
        TerrainWorldField c = fields[Index(x0, z1, vertexCountPerSide)];
        TerrainWorldField d = fields[Index(x1, z1, vertexCountPerSide)];

        float height = Bilinear(a.Height, b.Height, c.Height, d.Height, tx, tz);
        float river = Bilinear(a.River, b.River, c.River, d.River, tx, tz);
        float moisture = Bilinear(a.Moisture, b.Moisture, c.Moisture, d.Moisture, tx, tz);
        float temperature = Bilinear(a.Temperature, b.Temperature, c.Temperature, d.Temperature, tx, tz);
        float scenicPotential = Bilinear(a.ScenicPotential, b.ScenicPotential, c.ScenicPotential, d.ScenicPotential, tx, tz);
        float traversability = Bilinear(a.Traversability, b.Traversability, c.Traversability, d.Traversability, tx, tz);
        float exposure = Bilinear(a.Exposure, b.Exposure, c.Exposure, d.Exposure, tx, tz);
        float resourcePotential = Bilinear(a.ResourcePotential, b.ResourcePotential, c.ResourcePotential, d.ResourcePotential, tx, tz);
        float hazardPotential = Bilinear(a.HazardPotential, b.HazardPotential, c.HazardPotential, d.HazardPotential, tx, tz);
        float encounterPotential = Bilinear(a.EncounterPotential, b.EncounterPotential, c.EncounterPotential, d.EncounterPotential, tx, tz);

        TerrainWorldField nearest = fields[Index(
            Mathf.Clamp(Mathf.RoundToInt(gx), 0, resolution),
            Mathf.Clamp(Mathf.RoundToInt(gz), 0, resolution),
            vertexCountPerSide)];

        return nearest with
        {
            Height = height,
            River = river,
            Moisture = moisture,
            Temperature = temperature,
            ScenicPotential = scenicPotential,
            Traversability = traversability,
            Exposure = exposure,
            ResourcePotential = resourcePotential,
            HazardPotential = hazardPotential,
            EncounterPotential = encounterPotential
        };
    }

    private static Vector3 SampleNearestNormal(float localX, float localZ, int resolution, float step, Vector3[] normals, int vertexCountPerSide)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(localX / step), 0, resolution);
        int z = Mathf.Clamp(Mathf.RoundToInt(localZ / step), 0, resolution);
        return normals[Index(x, z, vertexCountPerSide)];
    }

    private static float Bilinear(float a, float b, float c, float d, float tx, float tz)
    {
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
    }

    private static Vector3 CalculateGridNormal(
        int x,
        int z,
        int resolution,
        int vertexCountPerSide,
        float[] heights,
        float step)
    {
        int leftX = Mathf.Max(0, x - 1);
        int rightX = Mathf.Min(resolution, x + 1);
        int downZ = Mathf.Max(0, z - 1);
        int upZ = Mathf.Min(resolution, z + 1);

        float left = heights[Index(leftX, z, vertexCountPerSide)];
        float right = heights[Index(rightX, z, vertexCountPerSide)];
        float down = heights[Index(x, downZ, vertexCountPerSide)];
        float up = heights[Index(x, upZ, vertexCountPerSide)];

        return new Vector3(left - right, step * 2.0f, down - up).Normalized();
    }

    private static int Index(int x, int z, int vertexCountPerSide)
    {
        return z * vertexCountPerSide + x;
    }
}
