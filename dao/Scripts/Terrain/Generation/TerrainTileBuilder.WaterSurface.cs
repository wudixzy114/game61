using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static TerrainWaterSurfaceData BuildWaterSurface(
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        float[] heights,
        TerrainWorldField[] fields,
        TerrainPointFootprintSample[] footprintSamples,
        TerrainSettlementLayoutSample[] settlementLayoutSamples,
        CancellationToken cancellationToken)
    {
        var vertices = new List<Vector3>(resolution * 8);
        var normals = new List<Vector3>(resolution * 8);
        var uvs = new List<Vector2>(resolution * 8);
        var colors = new List<Color>(resolution * 8);
        var indices = new List<int>(resolution * 12);
        int lakeCellCount = 0;
        int riverCellCount = 0;
        int oasisCellCount = 0;
        float minHeight = float.PositiveInfinity;
        float maxHeight = float.NegativeInfinity;

        for (int z = 0; z < resolution; z++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int x = 0; x < resolution; x++)
            {
                int i00 = Index(x, z, vertexCountPerSide);
                int i10 = Index(x + 1, z, vertexCountPerSide);
                int i01 = Index(x, z + 1, vertexCountPerSide);
                int i11 = Index(x + 1, z + 1, vertexCountPerSide);
                TerrainWaterSurfaceKind kind = ClassifyWaterSurfaceCell(
                    profile,
                    fields,
                    heights,
                    footprintSamples,
                    settlementLayoutSamples,
                    i00,
                    i10,
                    i01,
                    i11,
                    out float averageHeight,
                    out float maxTerrainHeight);

                if (kind == TerrainWaterSurfaceKind.None)
                {
                    continue;
                }

                switch (kind)
                {
                    case TerrainWaterSurfaceKind.Lake:
                        lakeCellCount++;
                        break;
                    case TerrainWaterSurfaceKind.River:
                        riverCellCount++;
                        break;
                    case TerrainWaterSurfaceKind.Oasis:
                        oasisCellCount++;
                        break;
                }

                int vertexStart = vertices.Count;
                float localX0 = x * step;
                float localX1 = (x + 1) * step;
                float localZ0 = z * step;
                float localZ1 = (z + 1) * step;
                float waterHeight = WaterSurfaceHeight(profile, kind, averageHeight, maxTerrainHeight);
                Color color = WaterSurfaceColor(kind);

                vertices.Add(new Vector3(localX0, waterHeight, localZ0));
                vertices.Add(new Vector3(localX1, waterHeight, localZ0));
                vertices.Add(new Vector3(localX0, waterHeight, localZ1));
                vertices.Add(new Vector3(localX1, waterHeight, localZ1));
                normals.Add(Vector3.Up);
                normals.Add(Vector3.Up);
                normals.Add(Vector3.Up);
                normals.Add(Vector3.Up);
                uvs.Add(new Vector2(localX0 / profile.ChunkSize, localZ0 / profile.ChunkSize));
                uvs.Add(new Vector2(localX1 / profile.ChunkSize, localZ0 / profile.ChunkSize));
                uvs.Add(new Vector2(localX0 / profile.ChunkSize, localZ1 / profile.ChunkSize));
                uvs.Add(new Vector2(localX1 / profile.ChunkSize, localZ1 / profile.ChunkSize));
                colors.Add(color);
                colors.Add(color);
                colors.Add(color);
                colors.Add(color);
                indices.Add(vertexStart);
                indices.Add(vertexStart + 2);
                indices.Add(vertexStart + 1);
                indices.Add(vertexStart + 1);
                indices.Add(vertexStart + 2);
                indices.Add(vertexStart + 3);

                minHeight = Mathf.Min(minHeight, waterHeight);
                maxHeight = Mathf.Max(maxHeight, waterHeight);
            }
        }

        if (vertices.Count == 0)
        {
            return TerrainWaterSurfaceData.Empty;
        }

        return new TerrainWaterSurfaceData(
            vertices.ToArray(),
            normals.ToArray(),
            uvs.ToArray(),
            colors.ToArray(),
            indices.ToArray(),
            lakeCellCount,
            riverCellCount,
            oasisCellCount,
            minHeight,
            maxHeight);
    }

    private static TerrainWaterSurfaceKind ClassifyWaterSurfaceCell(
        TerrainGenerationProfile profile,
        TerrainWorldField[] fields,
        float[] heights,
        TerrainPointFootprintSample[] footprintSamples,
        TerrainSettlementLayoutSample[] settlementLayoutSamples,
        int i00,
        int i10,
        int i01,
        int i11,
        out float averageHeight,
        out float maxTerrainHeight)
    {
        TerrainWorldField f00 = fields[i00];
        TerrainWorldField f10 = fields[i10];
        TerrainWorldField f01 = fields[i01];
        TerrainWorldField f11 = fields[i11];
        float h00 = heights[i00];
        float h10 = heights[i10];
        float h01 = heights[i01];
        float h11 = heights[i11];
        averageHeight = (h00 + h10 + h01 + h11) * 0.25f;
        maxTerrainHeight = Mathf.Max(Mathf.Max(h00, h10), Mathf.Max(h01, h11));
        float lake = (f00.Lake + f10.Lake + f01.Lake + f11.Lake) * 0.25f;
        float river = (f00.River + f10.River + f01.River + f11.River) * 0.25f;
        float resource = (f00.ResourcePotential + f10.ResourcePotential + f01.ResourcePotential + f11.ResourcePotential) * 0.25f;
        float moisture = (f00.Moisture + f10.Moisture + f01.Moisture + f11.Moisture) * 0.25f;
        float temperature = (f00.Temperature + f10.Temperature + f01.Temperature + f11.Temperature) * 0.25f;
        int lakeKindCount = CountWaterKinds(f00, f10, f01, f11, TerrainBiomeKind.Lake, TerrainLandscapeKind.Lake);
        int oceanOrCoastCount = CountOceanOrCoast(f00, f10, f01, f11);
        float oasisFootprint = AverageOasisFootprint(footprintSamples, i00, i10, i01, i11);
        float oasisLayoutWater = AverageOasisLayoutWater(settlementLayoutSamples, i00, i10, i01, i11);
        bool inlandEnough = oceanOrCoastCount < 3 && averageHeight > profile.SeaLevel + 4.0f;
        bool lakeCell = inlandEnough &&
            averageHeight < profile.SeaLevel + profile.HeightScale * 0.76f &&
            (lakeKindCount > 0 || lake > 0.38f) &&
            river < 0.86f;
        bool oasisCell =
            averageHeight > profile.SeaLevel + 3.0f &&
            (oasisLayoutWater > 0.08f ||
                oasisFootprint > 0.16f ||
                (IsOasisLike(f00) || IsOasisLike(f10) || IsOasisLike(f01) || IsOasisLike(f11)) &&
                resource > 0.34f &&
                moisture > 0.34f &&
                temperature > 0.32f);
        bool riverCell = inlandEnough &&
            !lakeCell &&
            river > 0.72f &&
            averageHeight < profile.SeaLevel + profile.HeightScale * 0.68f;

        if (oasisCell)
        {
            return TerrainWaterSurfaceKind.Oasis;
        }

        if (lakeCell)
        {
            return TerrainWaterSurfaceKind.Lake;
        }

        return riverCell ? TerrainWaterSurfaceKind.River : TerrainWaterSurfaceKind.None;
    }

    private static int CountWaterKinds(
        TerrainWorldField a,
        TerrainWorldField b,
        TerrainWorldField c,
        TerrainWorldField d,
        TerrainBiomeKind biome,
        TerrainLandscapeKind landscape)
    {
        int count = 0;
        count += a.BiomeKind == biome || a.LandscapeKind == landscape ? 1 : 0;
        count += b.BiomeKind == biome || b.LandscapeKind == landscape ? 1 : 0;
        count += c.BiomeKind == biome || c.LandscapeKind == landscape ? 1 : 0;
        count += d.BiomeKind == biome || d.LandscapeKind == landscape ? 1 : 0;
        return count;
    }

    private static int CountOceanOrCoast(
        TerrainWorldField a,
        TerrainWorldField b,
        TerrainWorldField c,
        TerrainWorldField d)
    {
        int count = 0;
        count += IsOceanOrCoast(a) ? 1 : 0;
        count += IsOceanOrCoast(b) ? 1 : 0;
        count += IsOceanOrCoast(c) ? 1 : 0;
        count += IsOceanOrCoast(d) ? 1 : 0;
        return count;
    }

    private static bool IsOceanOrCoast(TerrainWorldField field)
    {
        return field.BiomeKind is TerrainBiomeKind.Ocean or TerrainBiomeKind.Coast ||
            field.LandscapeKind is TerrainLandscapeKind.Ocean or TerrainLandscapeKind.Coast;
    }

    private static bool IsOasisLike(TerrainWorldField field)
    {
        return field.BiomeKind == TerrainBiomeKind.Oasis;
    }

    private static float AverageOasisFootprint(
        TerrainPointFootprintSample[] footprintSamples,
        int i00,
        int i10,
        int i01,
        int i11)
    {
        if (footprintSamples.Length == 0)
        {
            return 0.0f;
        }

        return (OasisFootprintStrength(footprintSamples[i00]) +
            OasisFootprintStrength(footprintSamples[i10]) +
            OasisFootprintStrength(footprintSamples[i01]) +
            OasisFootprintStrength(footprintSamples[i11])) * 0.25f;
    }

    private static float OasisFootprintStrength(TerrainPointFootprintSample footprint)
    {
        return footprint.Kind == TerrainPointOfInterestKind.Oasis || footprint.SettlementTier == TerrainSettlementTier.OasisHub
            ? footprint.Influence
            : 0.0f;
    }

    private static float AverageOasisLayoutWater(
        TerrainSettlementLayoutSample[] settlementLayoutSamples,
        int i00,
        int i10,
        int i01,
        int i11)
    {
        if (settlementLayoutSamples.Length == 0)
        {
            return 0.0f;
        }

        return (settlementLayoutSamples[i00].OasisWaterStrength +
            settlementLayoutSamples[i10].OasisWaterStrength +
            settlementLayoutSamples[i01].OasisWaterStrength +
            settlementLayoutSamples[i11].OasisWaterStrength) * 0.25f;
    }

    private static float WaterSurfaceHeight(
        TerrainGenerationProfile profile,
        TerrainWaterSurfaceKind kind,
        float averageTerrainHeight,
        float maxTerrainHeight)
    {
        float lift = kind switch
        {
            TerrainWaterSurfaceKind.Oasis => 0.16f,
            TerrainWaterSurfaceKind.River => 0.11f,
            _ => 0.13f
        };
        float terrainAnchored = kind == TerrainWaterSurfaceKind.River
            ? Mathf.Lerp(averageTerrainHeight, maxTerrainHeight, 0.42f)
            : maxTerrainHeight;
        return Mathf.Max(profile.SeaLevel + 0.08f, terrainAnchored + lift);
    }

    private static Color WaterSurfaceColor(TerrainWaterSurfaceKind kind)
    {
        return kind switch
        {
            TerrainWaterSurfaceKind.Oasis => new Color(0.05f, 0.44f, 0.36f, 0.58f),
            TerrainWaterSurfaceKind.River => new Color(0.05f, 0.27f, 0.46f, 0.48f),
            TerrainWaterSurfaceKind.Lake => new Color(0.06f, 0.34f, 0.44f, 0.52f),
            _ => new Color(0.05f, 0.20f, 0.30f, 0.50f)
        };
    }
}
