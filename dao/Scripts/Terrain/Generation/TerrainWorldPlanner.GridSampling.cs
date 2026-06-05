using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanner
{
    private static TerrainPlanningGridData SamplePlanningGrid(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int resolution,
        CancellationToken cancellationToken)
    {
        float cellSize = worldSize / resolution;
        int cellCount = resolution * resolution;
        var fields = new TerrainWorldField[cellCount];
        var regions = new TerrainWorldRegion[cellCount];
        var candidates = new List<PoiCandidate>(cellCount);

        for (int y = 0; y < resolution; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int x = 0; x < resolution; x++)
            {
                Vector2 world = CellCenter(center, worldSize, resolution, x, y);
                TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);
                int index = Index(x, y, resolution);
                fields[index] = field;
                regions[index] = new TerrainWorldRegion(
                    x,
                    y,
                    world,
                    field.Height,
                    field.River,
                    field.ScenicPotential,
                    field.Traversability,
                    field.Exposure,
                    field.ResourcePotential,
                    field.HazardPotential,
                    field.EncounterPotential,
                    field.BiomeKind,
                    field.LandscapeKind,
                    ClassifyRegion(field));
                AddPoiCandidates(candidates, profile, field, x, y);
            }
        }

        return new TerrainPlanningGridData(fields, regions, candidates, cellSize);
    }

    private static TerrainWorldRegionKind ClassifyRegion(TerrainWorldField field)
    {
        if (field.BiomeKind == TerrainBiomeKind.Lake || field.LandscapeKind == TerrainLandscapeKind.Lake)
        {
            return TerrainWorldRegionKind.Lake;
        }

        if (field.BiomeKind is TerrainBiomeKind.Island or TerrainBiomeKind.Plains or TerrainBiomeKind.Grassland or
            TerrainBiomeKind.Desert or TerrainBiomeKind.Oasis or TerrainBiomeKind.Hills)
        {
            return field.BiomeKind switch
            {
                TerrainBiomeKind.Island => TerrainWorldRegionKind.Island,
                TerrainBiomeKind.Plains => TerrainWorldRegionKind.Plains,
                TerrainBiomeKind.Grassland => TerrainWorldRegionKind.Grassland,
                TerrainBiomeKind.Desert => TerrainWorldRegionKind.Desert,
                TerrainBiomeKind.Oasis => TerrainWorldRegionKind.Oasis,
                TerrainBiomeKind.Hills => TerrainWorldRegionKind.Hills,
                _ => TerrainWorldRegionKind.Lowland
            };
        }

        return field.LandscapeKind switch
        {
            TerrainLandscapeKind.Ocean => TerrainWorldRegionKind.Ocean,
            TerrainLandscapeKind.Coast => TerrainWorldRegionKind.Coast,
            TerrainLandscapeKind.Lowland => TerrainWorldRegionKind.Lowland,
            TerrainLandscapeKind.Wetland => TerrainWorldRegionKind.Wetland,
            TerrainLandscapeKind.ForestBasin => TerrainWorldRegionKind.Forest,
            TerrainLandscapeKind.RiverValley => TerrainWorldRegionKind.RiverValley,
            TerrainLandscapeKind.Canyon => TerrainWorldRegionKind.Canyon,
            TerrainLandscapeKind.Highlands => TerrainWorldRegionKind.Highlands,
            TerrainLandscapeKind.MountainMassif => TerrainWorldRegionKind.Mountains,
            TerrainLandscapeKind.Snowfield => TerrainWorldRegionKind.Snow,
            TerrainLandscapeKind.VistaPlateau => TerrainWorldRegionKind.ScenicPlateau,
            _ => TerrainWorldRegionKind.Lowland
        };
    }

    private static Vector2 CellCenter(Vector2 center, float worldSize, int resolution, int x, int y)
    {
        float invResolution = 1.0f / resolution;
        return new Vector2(
            center.X + ((x + 0.5f) * invResolution - 0.5f) * worldSize,
            center.Y + ((y + 0.5f) * invResolution - 0.5f) * worldSize);
    }

    private static bool InBounds(int x, int y, int resolution)
    {
        return x >= 0 && y >= 0 && x < resolution && y < resolution;
    }

    private static int Index(int x, int y, int resolution)
    {
        return y * resolution + x;
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x * 0x9E3779B9u;
            h = (h << 13) | (h >> 19);
            h ^= (uint)y * 0x85EBCA6Bu;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215.0f;
        }
    }

    private readonly record struct TerrainPlanningGridData(
        TerrainWorldField[] Fields,
        TerrainWorldRegion[] Regions,
        List<PoiCandidate> Candidates,
        float CellSize);
}
