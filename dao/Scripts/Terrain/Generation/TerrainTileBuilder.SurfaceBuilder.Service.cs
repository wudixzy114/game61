using System.Threading.Tasks;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainTileBuilder
{
    private static class TerrainTileSurfaceBuilderService
    {
        internal static TerrainTileSurfaceHeightRange BuildSurfaceGeometry(TerrainTileSurfaceBuildContext context)
        {
            SampleSurfaceVertices(context);
            TerrainTileSurfaceHeightRange heightRange = CalculateSurfaceHeightRange(context.Heights, context.VertexCount);
            ColorSurfaceVertices(context);
            return heightRange;
        }

        private static void SampleSurfaceVertices(TerrainTileSurfaceBuildContext context)
        {
            void SampleVertex(int z, int x)
            {
                int index = Index(x, z, context.VertexCountPerSide);
                float localX = x * context.Step;
                float localZ = z * context.Step;
                Vector2 world = new(context.Origin.X + localX, context.Origin.Y + localZ);
                TerrainWorldField field = context.UseNativeFields
                    ? TerrainWorldFieldSampler.SampleNativeFieldGrid(
                        world,
                        context.Profile,
                        context.NativeFieldSamples,
                        index,
                        context.NativeFieldsContainDerivedData)
                    : context.UseNativeHeights
                    ? TerrainWorldFieldSampler.SampleKnownHeight(world, context.Profile, context.NativeHeights[index])
                    : TerrainWorldFieldSampler.Sample(world, context.Profile, context.ManagedLandBalanceOffset);
                float height = field.Height;
                TerrainRouteCorridorSample corridor = TerrainRouteCorridorSample.None;
                if (context.HasCorridors)
                {
                    corridor = context.RouteCorridors.Sample(world, context.CorridorSegments);
                    context.CorridorSamples[index] = corridor;
                }

                if (corridor.HasInfluence)
                {
                    height = ApplyRouteCorridorHeight(height, corridor);
                    field = field with
                    {
                        Height = height,
                        Traversability = Mathf.Max(field.Traversability, Mathf.Lerp(field.Traversability, 0.86f, corridor.CoreStrength))
                    };
                }

                TerrainPointFootprintSample footprint = TerrainPointFootprintSample.None;
                if (context.HasPointInfluences)
                {
                    footprint = SamplePointFootprint(world, context.PointInfluences, context.Profile);
                    context.FootprintSamples[index] = footprint;
                }

                if (footprint.HasInfluence)
                {
                    height = ApplyPointFootprintHeight(height, footprint);
                    field = field with
                    {
                        Height = height,
                        Traversability = Mathf.Max(field.Traversability, Mathf.Lerp(field.Traversability, 0.92f, footprint.CoreStrength)),
                        EncounterPotential = Mathf.Max(field.EncounterPotential, Mathf.Lerp(field.EncounterPotential, 0.62f, footprint.CoreStrength * 0.60f))
                    };
                }

                TerrainSettlementLayoutSample settlementLayout = TerrainSettlementLayoutSample.None;
                if (context.HasSettlementLayouts)
                {
                    settlementLayout = SampleSettlementLayout(world, context.SettlementLayouts);
                    context.SettlementLayoutSamples[index] = settlementLayout;
                }

                if (settlementLayout.HasInfluence)
                {
                    height = ApplySettlementLayoutHeight(height, settlementLayout);
                    field = field with
                    {
                        Height = height,
                        Traversability = Mathf.Max(field.Traversability, Mathf.Lerp(field.Traversability, 0.95f, settlementLayout.CoreStrength)),
                        EncounterPotential = Mathf.Max(field.EncounterPotential, Mathf.Lerp(field.EncounterPotential, 0.66f, settlementLayout.Influence * 0.45f))
                    };
                }

                context.SurfaceVertices[index] = new Vector3(localX, height, localZ);
                context.SurfaceUvs[index] = new Vector2(
                    world.X / context.Profile.ChunkSize,
                    world.Y / context.Profile.ChunkSize);
                context.Heights[index] = height;
                context.Fields[index] = field;
            }

            if (context.UseParallelSurfaceProcessing)
            {
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = context.CancellationToken,
                    MaxDegreeOfParallelism = SurfaceProcessingMaxDegreeOfParallelism
                };
                Parallel.For(0, context.Resolution + 1, parallelOptions, z =>
                {
                    for (int x = 0; x <= context.Resolution; x++)
                    {
                        SampleVertex(z, x);
                    }
                });
                return;
            }

            for (int z = 0; z <= context.Resolution; z++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x <= context.Resolution; x++)
                {
                    SampleVertex(z, x);
                }
            }
        }

        private static TerrainTileSurfaceHeightRange CalculateSurfaceHeightRange(float[] heights, int vertexCount)
        {
            float minHeight = float.PositiveInfinity;
            float maxHeight = float.NegativeInfinity;
            for (int i = 0; i < vertexCount; i++)
            {
                float height = heights[i];
                minHeight = Mathf.Min(minHeight, height);
                maxHeight = Mathf.Max(maxHeight, height);
            }

            return new TerrainTileSurfaceHeightRange(minHeight, maxHeight);
        }

        private static void ColorSurfaceVertices(TerrainTileSurfaceBuildContext context)
        {
            void ColorVertex(int z, int x)
            {
                int index = Index(x, z, context.VertexCountPerSide);
                Vector3 normal = CalculateGridNormal(
                    x,
                    z,
                    context.Resolution,
                    context.VertexCountPerSide,
                    context.Heights,
                    context.Step);
                float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
                context.SurfaceNormals[index] = normal;
                context.SurfaceColors[index] = TerrainSampler.ColorForSurface(context.Fields[index], context.Profile, slope);

                if (context.Heights[index] < context.Profile.SeaLevel + 3.0f)
                {
                    context.SurfaceColors[index] = context.SurfaceColors[index].Lerp(new Color(0.10f, 0.24f, 0.31f), 0.35f);
                }

                if (context.HasCorridors && context.CorridorSamples[index].HasInfluence)
                {
                    context.SurfaceColors[index] = BlendRouteSurfaceColor(context.SurfaceColors[index], context.CorridorSamples[index]);
                }

                if (context.HasPointInfluences && context.FootprintSamples[index].HasInfluence)
                {
                    context.SurfaceColors[index] = BlendPointFootprintColor(context.SurfaceColors[index], context.FootprintSamples[index]);
                }

                if (context.HasSettlementLayouts && context.SettlementLayoutSamples[index].HasInfluence)
                {
                    context.SurfaceColors[index] = BlendSettlementLayoutColor(context.SurfaceColors[index], context.SettlementLayoutSamples[index]);
                }
            }

            if (context.UseParallelSurfaceProcessing)
            {
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = context.CancellationToken,
                    MaxDegreeOfParallelism = SurfaceProcessingMaxDegreeOfParallelism
                };
                Parallel.For(0, context.Resolution + 1, parallelOptions, z =>
                {
                    for (int x = 0; x <= context.Resolution; x++)
                    {
                        ColorVertex(z, x);
                    }
                });
                return;
            }

            for (int z = 0; z <= context.Resolution; z++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x <= context.Resolution; x++)
                {
                    ColorVertex(z, x);
                }
            }
        }
    }
}
