using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static class TerrainTileFeatureMaterializerService
    {
        internal static void BuildTerrainFeatures(
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
            TerrainScatterRuleSetSnapshot scatterRules = ResolveScatterRules(profile);

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
                        bool isTidalMangroveFlat = IsMangroveTidalFlat(height, slope, field, profile, scatterRules.TidalMangroveFlat);
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
                        TerrainSurfaceNaturalScatterRule treeRule = scatterRules.Tree;
                        if (slope < treeRule.MaxSlope &&
                            field.Moisture > treeRule.MinMoisture &&
                            field.Temperature > treeRule.MinTemperature &&
                            field.River < treeRule.MaxRiver &&
                            field.Traversability > treeRule.MinTraversability &&
                            field.LandscapeKind is TerrainLandscapeKind.ForestBasin or TerrainLandscapeKind.Lowland or TerrainLandscapeKind.RiverValley or TerrainLandscapeKind.Wetland &&
                            roll < treeRule.Probability)
                        {
                            float scale = treeRule.BaseScale + Hash01(coord.X, coord.Z, x * 1237 + z * 2011, profile.Seed + 43) * treeRule.ScaleJitter;
                            float rotation = Hash01(coord.X, coord.Z, x * 719 + z * 911, profile.Seed + 59) * Mathf.Pi * 2.0f;
                            Color tint = treeRule.TintLow.Lerp(treeRule.TintHigh, field.Moisture);
                            scatter.Add(new TerrainScatterInstance(TerrainScatterKind.Tree, new Vector3(localX, height, localZ), rotation, scale, tint));
                            placedNaturalScatter = true;
                        }
                        else if ((slope > scatterRules.Rock.MinSlope ||
                                height > profile.SeaLevel + scatterRules.Rock.MinHeightAboveSea ||
                                field.HazardPotential > scatterRules.Rock.MinHazardPotential ||
                                field.LandscapeKind is TerrainLandscapeKind.Canyon or TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif) &&
                            roll < scatterRules.Rock.Probability)
                        {
                            float scale = scatterRules.Rock.BaseScale + Hash01(coord.X, coord.Z, x * 4567 + z * 3461, profile.Seed + 61) * scatterRules.Rock.ScaleJitter;
                            float rotation = Hash01(coord.X, coord.Z, x * 2467 + z * 6421, profile.Seed + 67) * Mathf.Pi * 2.0f;
                            Color tint = scatterRules.Rock.TintLow.Lerp(scatterRules.Rock.TintHigh, Mathf.Clamp(slope, 0.0f, 1.0f));
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
                            scatterRules,
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
                                scatterRules,
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
    }
}
