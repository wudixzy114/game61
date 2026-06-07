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
            TerrainModificationLayer modificationLayer,
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

            ApplyTerrainFeatureModifications(coord, profile, modificationLayer, scatter, landmarkList);
            scatterInstances = scatter.ToArray();
            landmarks = landmarkList.ToArray();
        }

        private static void ApplyTerrainFeatureModifications(
            TerrainTileCoord coord,
            TerrainGenerationProfile profile,
            TerrainModificationLayer modificationLayer,
            List<TerrainScatterInstance> scatter,
            List<TerrainLandmarkData> landmarks)
        {
            if (modificationLayer.IsEmpty)
            {
                return;
            }

            Vector2 origin = coord.Origin(profile.ChunkSize);
            ApplyScatterModifications(coord, profile, origin, modificationLayer.ScatterModifications, scatter, landmarks);
            ApplyLandmarkModifications(origin, modificationLayer.LandmarkModifications, scatter, landmarks);
        }

        private static void ApplyScatterModifications(
            TerrainTileCoord coord,
            TerrainGenerationProfile profile,
            Vector2 origin,
            TerrainScatterModification[] modifications,
            List<TerrainScatterInstance> scatter,
            List<TerrainLandmarkData> landmarks)
        {
            if (modifications.Length == 0)
            {
                return;
            }

            for (int i = 0; i < modifications.Length; i++)
            {
                TerrainScatterModification modification = modifications[i];
                if (!CircleTouchesTile(modification.WorldPosition, modification.Radius, origin, profile.ChunkSize))
                {
                    continue;
                }

                if (modification.Remove)
                {
                    RemoveMatchingScatter(origin, modification, scatter, landmarks);
                    continue;
                }

                if (TryCreateScatterInstanceForModification(coord, profile, origin, modification, out TerrainScatterInstance instance))
                {
                    scatter.Add(instance);
                }
            }
        }

        private static void ApplyLandmarkModifications(
            Vector2 origin,
            TerrainLandmarkModification[] modifications,
            List<TerrainScatterInstance> scatter,
            List<TerrainLandmarkData> landmarks)
        {
            if (modifications.Length == 0 || landmarks.Count == 0)
            {
                return;
            }

            for (int i = 0; i < modifications.Length; i++)
            {
                TerrainLandmarkModification modification = modifications[i];
                if (!CircleTouchesTile(modification.WorldPosition, modification.Radius, origin, float.PositiveInfinity))
                {
                    continue;
                }

                bool remove = LandmarkStateRemovesVisual(modification.State);
                for (int landmarkIndex = landmarks.Count - 1; landmarkIndex >= 0; landmarkIndex--)
                {
                    TerrainLandmarkData landmark = landmarks[landmarkIndex];
                    if (landmark.Kind != modification.Kind ||
                        !IsWithinRadius(origin + new Vector2(landmark.LocalPosition.X, landmark.LocalPosition.Z), modification.WorldPosition, modification.Radius))
                    {
                        continue;
                    }

                    if (remove)
                    {
                        landmarks.RemoveAt(landmarkIndex);
                        RemoveMatchingLandmarkScatter(origin, modification, scatter);
                        continue;
                    }

                    landmarks[landmarkIndex] = landmark with
                    {
                        DebugName = string.IsNullOrWhiteSpace(modification.State)
                            ? landmark.DebugName
                            : $"{landmark.DebugName}|state={modification.State}"
                    };
                }
            }
        }

        private static void RemoveMatchingScatter(
            Vector2 origin,
            TerrainScatterModification modification,
            List<TerrainScatterInstance> scatter,
            List<TerrainLandmarkData> landmarks)
        {
            for (int scatterIndex = scatter.Count - 1; scatterIndex >= 0; scatterIndex--)
            {
                TerrainScatterInstance instance = scatter[scatterIndex];
                if (instance.Kind != modification.Kind ||
                    !IsWithinRadius(origin + new Vector2(instance.LocalPosition.X, instance.LocalPosition.Z), modification.WorldPosition, modification.Radius))
                {
                    continue;
                }

                if (instance.Kind == TerrainScatterKind.Landmark)
                {
                    RemoveLandmarkMetadataNear(origin, instance.LandmarkKind, modification.WorldPosition, modification.Radius, landmarks);
                }

                scatter.RemoveAt(scatterIndex);
            }
        }

        private static void RemoveMatchingLandmarkScatter(
            Vector2 origin,
            TerrainLandmarkModification modification,
            List<TerrainScatterInstance> scatter)
        {
            for (int scatterIndex = scatter.Count - 1; scatterIndex >= 0; scatterIndex--)
            {
                TerrainScatterInstance instance = scatter[scatterIndex];
                if (instance.Kind != TerrainScatterKind.Landmark ||
                    instance.LandmarkKind != modification.Kind ||
                    !IsWithinRadius(origin + new Vector2(instance.LocalPosition.X, instance.LocalPosition.Z), modification.WorldPosition, modification.Radius))
                {
                    continue;
                }

                scatter.RemoveAt(scatterIndex);
            }
        }

        private static void RemoveLandmarkMetadataNear(
            Vector2 origin,
            TerrainLandmarkKind kind,
            Vector2 worldPosition,
            float radius,
            List<TerrainLandmarkData> landmarks)
        {
            for (int landmarkIndex = landmarks.Count - 1; landmarkIndex >= 0; landmarkIndex--)
            {
                TerrainLandmarkData landmark = landmarks[landmarkIndex];
                if (landmark.Kind != kind ||
                    !IsWithinRadius(origin + new Vector2(landmark.LocalPosition.X, landmark.LocalPosition.Z), worldPosition, radius))
                {
                    continue;
                }

                landmarks.RemoveAt(landmarkIndex);
            }
        }

        private static bool TryCreateScatterInstanceForModification(
            TerrainTileCoord coord,
            TerrainGenerationProfile profile,
            Vector2 origin,
            TerrainScatterModification modification,
            out TerrainScatterInstance instance)
        {
            instance = default;
            if (modification.Kind == TerrainScatterKind.Landmark)
            {
                return false;
            }

            Vector2 local = modification.WorldPosition - origin;
            if (local.X < 0.0f || local.Y < 0.0f || local.X > profile.ChunkSize || local.Y > profile.ChunkSize)
            {
                return false;
            }

            TerrainWorldField field = TerrainWorldFieldSampler.Sample(modification.WorldPosition, profile);
            float height = field.Height;
            float rotation = StableHash01(modification.StableId, 17) * Mathf.Pi * 2.0f;
            float scale = ScatterModificationScale(modification.Kind);
            Color color = ScatterModificationColor(modification.Kind, field, modification.State);
            instance = new TerrainScatterInstance(
                modification.Kind,
                new Vector3(local.X, height, local.Y),
                rotation,
                scale,
                color);
            return true;
        }

        private static bool CircleTouchesTile(Vector2 center, float radius, Vector2 tileOrigin, float chunkSize)
        {
            float safeRadius = Mathf.Max(0.0f, radius);
            float tileMaxX = tileOrigin.X + chunkSize;
            float tileMaxY = tileOrigin.Y + chunkSize;
            float closestX = Mathf.Clamp(center.X, tileOrigin.X, tileMaxX);
            float closestY = Mathf.Clamp(center.Y, tileOrigin.Y, tileMaxY);
            float dx = center.X - closestX;
            float dy = center.Y - closestY;
            return (dx * dx) + (dy * dy) <= safeRadius * safeRadius;
        }

        private static bool IsWithinRadius(Vector2 a, Vector2 b, float radius)
        {
            float safeRadius = Mathf.Max(0.0f, radius);
            return a.DistanceSquaredTo(b) <= safeRadius * safeRadius;
        }

        private static bool LandmarkStateRemovesVisual(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return false;
            }

            return state.Contains("remove", System.StringComparison.OrdinalIgnoreCase) ||
                state.Contains("hidden", System.StringComparison.OrdinalIgnoreCase) ||
                state.Contains("disabled", System.StringComparison.OrdinalIgnoreCase) ||
                state.Contains("destroyed", System.StringComparison.OrdinalIgnoreCase) ||
                state.Contains("consumed", System.StringComparison.OrdinalIgnoreCase);
        }

        private static float ScatterModificationScale(TerrainScatterKind kind)
        {
            return kind switch
            {
                TerrainScatterKind.Tree => 3.6f,
                TerrainScatterKind.Rock => 2.8f,
                TerrainScatterKind.ResourceNode => 1.8f,
                TerrainScatterKind.HazardOutcrop => 2.2f,
                TerrainScatterKind.GrassTuft => 0.9f,
                TerrainScatterKind.DesertShrub => 1.0f,
                TerrainScatterKind.CactusCluster => 1.4f,
                TerrainScatterKind.ReedCluster => 1.1f,
                TerrainScatterKind.SnowClump => 1.0f,
                TerrainScatterKind.AlpinePine => 2.4f,
                TerrainScatterKind.CoastalPalm => 2.8f,
                TerrainScatterKind.Driftwood => 1.6f,
                TerrainScatterKind.MangroveRoot => 2.2f,
                TerrainScatterKind.LakeReed => 1.0f,
                TerrainScatterKind.WaterLily => 0.8f,
                TerrainScatterKind.Understory => 0.8f,
                _ => 1.2f
            };
        }

        private static Color ScatterModificationColor(
            TerrainScatterKind kind,
            TerrainWorldField field,
            string state)
        {
            Color baseColor = kind switch
            {
                TerrainScatterKind.Tree => new Color(0.20f, 0.48f, 0.22f),
                TerrainScatterKind.Rock => new Color(0.48f, 0.44f, 0.40f),
                TerrainScatterKind.Understory => new Color(0.18f, 0.58f, 0.24f),
                TerrainScatterKind.ResourceNode => new Color(0.72f, 0.50f, 0.20f),
                TerrainScatterKind.HazardOutcrop => new Color(0.70f, 0.28f, 0.22f),
                TerrainScatterKind.GrassTuft => new Color(0.34f, 0.64f, 0.26f),
                TerrainScatterKind.DesertShrub => new Color(0.56f, 0.46f, 0.28f),
                TerrainScatterKind.CactusCluster => new Color(0.18f, 0.52f, 0.32f),
                TerrainScatterKind.ReedCluster => new Color(0.42f, 0.62f, 0.26f),
                TerrainScatterKind.SnowClump => new Color(0.84f, 0.88f, 0.92f),
                TerrainScatterKind.AlpinePine => new Color(0.18f, 0.40f, 0.26f),
                TerrainScatterKind.CoastalPalm => new Color(0.24f, 0.58f, 0.34f),
                TerrainScatterKind.Driftwood => new Color(0.60f, 0.50f, 0.34f),
                TerrainScatterKind.MangroveRoot => new Color(0.36f, 0.24f, 0.18f),
                TerrainScatterKind.LakeReed => new Color(0.38f, 0.58f, 0.24f),
                TerrainScatterKind.WaterLily => new Color(0.32f, 0.62f, 0.38f),
                _ => new Color(0.60f, 0.60f, 0.60f)
            };
            if (!string.IsNullOrWhiteSpace(state) &&
                state.Contains("harvest", System.StringComparison.OrdinalIgnoreCase))
            {
                return baseColor.Lerp(new Color(0.36f, 0.26f, 0.18f), 0.48f);
            }

            return baseColor.Lerp(new Color(0.82f, 0.80f, 0.72f), Mathf.Clamp(field.Exposure * 0.18f, 0.0f, 0.18f));
        }

        private static float StableHash01(int value, int salt)
        {
            unchecked
            {
                uint hash = (uint)(value * 747796405) + (uint)(salt * 2891336453);
                hash ^= hash >> 16;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                hash *= 3266489917u;
                hash ^= hash >> 16;
                return (hash & 0x00FFFFFF) / 16777215.0f;
            }
        }
    }
}
