using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static void BuildTerrainFeatures(
        TerrainTileCoord coord,
        int lod,
        TerrainGenerationProfile profile,
        int resolution,
        int vertexCountPerSide,
        float step,
        float[] heights,
        TerrainWorldField[] fields,
        Vector3[] normals,
        TerrainRouteCorridorIndex routeCorridors,
        TerrainRouteCorridorSegment[] corridorSegments,
        TerrainPointOfInterestIndex pointOfInterestIndex,
        CancellationToken cancellationToken,
        out TerrainScatterInstance[] scatterInstances,
        out TerrainLandmarkData[] landmarks)
    {
        var scatter = new List<TerrainScatterInstance>(160);
        var landmarkList = new List<TerrainLandmarkData>(4);
        Vector2 origin = coord.Origin(profile.ChunkSize);
        bool hasCorridors = corridorSegments.Length > 0;
        TerrainWorldPointOfInterest[] plannedPoints = pointOfInterestIndex.GetPointsUnsafe(coord);

        if (lod <= 2)
        {
            int cells = lod == 0 ? 14 : lod == 1 ? 9 : 5;
            for (int z = 0; z < cells; z++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (int x = 0; x < cells; x++)
                {
                    float jx = Hash01(coord.X, coord.Z, x * 193 + z * 389, profile.Seed);
                    float jz = Hash01(coord.X, coord.Z, x * 557 + z * 263, profile.Seed + 17);
                    float localX = (x + 0.18f + jx * 0.64f) / cells * profile.ChunkSize;
                    float localZ = (z + 0.18f + jz * 0.64f) / cells * profile.ChunkSize;
                    float height = SampleHeightBilinear(localX, localZ, resolution, step, heights, vertexCountPerSide);
                    Vector2 world = new(origin.X + localX, origin.Y + localZ);
                    TerrainRouteCorridorSample corridor = hasCorridors
                        ? routeCorridors.Sample(world, corridorSegments)
                        : TerrainRouteCorridorSample.None;

                    Vector3 normal = SampleNearestNormal(localX, localZ, resolution, step, normals, vertexCountPerSide);
                    float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
                    TerrainWorldField field = SampleFieldBilinear(localX, localZ, resolution, step, fields, vertexCountPerSide);
                    bool isTidalMangroveFlat = IsMangroveTidalFlat(height, slope, field, profile);
                    if (height < profile.SeaLevel + 6.0f &&
                        !isTidalMangroveFlat &&
                        (!corridor.HasInfluence || corridor.CoreStrength < 0.32f))
                    {
                        continue;
                    }

                    float roll = Hash01(coord.X, coord.Z, x * 881 + z * 977, profile.Seed + 31);

                    if (IsInsidePointFootprint(world, plannedPoints, profile, minimumInfluence: 0.08f))
                    {
                        continue;
                    }

                    if (lod <= 1 && corridor.HasInfluence)
                    {
                        AddRouteCorridorScatter(
                            coord,
                            profile,
                            x,
                            z,
                            localX,
                            localZ,
                            height,
                            slope,
                            field,
                            corridor,
                            scatter);
                    }

                    if (corridor.HasInfluence && (corridor.CoreStrength > 0.04f || corridor.Influence > 0.58f))
                    {
                        continue;
                    }

                    bool placedNaturalScatter = false;
                    if (slope < 0.30f &&
                        field.Moisture > 0.47f &&
                        field.Temperature > 0.24f &&
                        field.River < 0.78f &&
                        field.Traversability > 0.35f &&
                        field.LandscapeKind is TerrainLandscapeKind.ForestBasin or TerrainLandscapeKind.Lowland or TerrainLandscapeKind.RiverValley or TerrainLandscapeKind.Wetland &&
                        roll < 0.44f)
                    {
                        float scale = 2.2f + Hash01(coord.X, coord.Z, x * 1237 + z * 2011, profile.Seed + 43) * 3.4f;
                        float rotation = Hash01(coord.X, coord.Z, x * 719 + z * 911, profile.Seed + 59) * Mathf.Pi * 2.0f;
                        Color tint = new Color(0.22f, 0.44f, 0.19f).Lerp(new Color(0.08f, 0.25f, 0.12f), field.Moisture);
                        scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Tree, new Vector3(localX, height, localZ), rotation, scale, tint));
                        placedNaturalScatter = true;
                    }
                    else if ((slope > 0.35f ||
                            height > profile.SeaLevel + 360.0f ||
                            field.HazardPotential > 0.56f ||
                            field.LandscapeKind is TerrainLandscapeKind.Canyon or TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif) &&
                        roll < 0.38f)
                    {
                        float scale = 1.3f + Hash01(coord.X, coord.Z, x * 4567 + z * 3461, profile.Seed + 61) * 3.1f;
                        float rotation = Hash01(coord.X, coord.Z, x * 2467 + z * 6421, profile.Seed + 67) * Mathf.Pi * 2.0f;
                        Color tint = new Color(0.36f, 0.35f, 0.32f).Lerp(new Color(0.55f, 0.54f, 0.49f), Mathf.Clamp(slope, 0.0f, 1.0f));
                        scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Rock, new Vector3(localX, height, localZ), rotation, scale, tint));
                        placedNaturalScatter = true;
                    }

                    AddBiomeSurfaceScatter(
                        coord,
                        profile,
                        x,
                        z,
                        localX,
                        localZ,
                        height,
                        slope,
                        field,
                        placedNaturalScatter,
                        scatter);

                    if (lod <= 1)
                    {
                        AddGameplayScatter(
                            coord,
                            profile,
                            x,
                            z,
                            localX,
                            localZ,
                            height,
                            slope,
                            field,
                            scatter);
                    }
                }
            }
        }

        if (lod <= 1)
        {
            AddPlannedPoiLandmarks(coord, profile, resolution, vertexCountPerSide, step, heights, fields, normals, plannedPoints, corridorSegments, scatter, landmarkList);
            if (landmarkList.Count == 0)
            {
                AddBestLandmark(coord, profile, resolution, vertexCountPerSide, step, heights, fields, normals, cancellationToken, scatter, landmarkList);
                AddScenicNaturalLandmarks(coord, profile, resolution, vertexCountPerSide, step, heights, fields, normals, cancellationToken, scatter, landmarkList);
            }
        }

        scatterInstances = scatter.ToArray();
        landmarks = landmarkList.ToArray();
    }

    private static float Hash01(int x, int z, int salt, int seed)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x * 0x9E3779B9u;
            h = (h << 13) | (h >> 19);
            h ^= (uint)z * 0x85EBCA6Bu;
            h = (h << 17) | (h >> 15);
            h ^= (uint)salt * 0xC2B2AE35u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215.0f;
        }
    }

    private static float ApplyRouteCorridorHeight(float height, TerrainRouteCorridorSample corridor)
    {
        float strength = corridor.Kind switch
        {
            TerrainRouteKind.RidgePass => corridor.CoreStrength * 0.52f + corridor.Influence * 0.18f,
            TerrainRouteKind.ScenicTrail => corridor.CoreStrength * 0.58f + corridor.Influence * 0.20f,
            TerrainRouteKind.CoastalPath => corridor.CoreStrength * 0.70f + corridor.Influence * 0.24f,
            _ => corridor.CoreStrength * 0.74f + corridor.Influence * 0.26f
        };

        strength = Mathf.Clamp(strength, 0.0f, 0.82f);
        return Mathf.Lerp(height, corridor.TargetHeight, strength);
    }

    private static Color BlendRouteSurfaceColor(Color baseColor, TerrainRouteCorridorSample corridor)
    {
        Color routeColor = corridor.Kind switch
        {
            TerrainRouteKind.RiverRoad => new Color(0.35f, 0.45f, 0.38f),
            TerrainRouteKind.RidgePass => new Color(0.44f, 0.43f, 0.39f),
            TerrainRouteKind.CoastalPath => new Color(0.55f, 0.50f, 0.36f),
            TerrainRouteKind.ScenicTrail => new Color(0.50f, 0.42f, 0.25f),
            _ => new Color(0.45f, 0.36f, 0.23f)
        };

        float blend = Mathf.Clamp(corridor.CoreStrength * 0.52f + corridor.Influence * 0.20f, 0.0f, 0.62f);
        return baseColor.Lerp(routeColor, blend);
    }
}
