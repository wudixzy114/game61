using System;
using System.Text;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

internal static class TerrainQualityAnalysisService
{
    internal static TerrainQualityReport Analyze(
        TerrainGenerationProfile profile,
        Vector2 center,
        float worldSize,
        int sampleResolution,
        CancellationToken cancellationToken)
    {
        int resolution = Mathf.Clamp(sampleResolution, 8, 1024);
        int sampleCount = resolution * resolution;
        float safeWorldSize = Mathf.Max(profile.ChunkSize, worldSize);
        float minHeight = float.PositiveInfinity;
        float maxHeight = float.NegativeInfinity;
        double heightSum = 0.0;
        int landCount = 0;
        int coastCount = 0;
        int riverCount = 0;
        int scenicCount = 0;
        int traversableLandCount = 0;
        Span<int> landscapeCounts = stackalloc int[Enum.GetValues<TerrainLandscapeKind>().Length];
        Span<int> biomeCounts = stackalloc int[Enum.GetValues<TerrainBiomeKind>().Length];

        for (int y = 0; y < resolution; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int x = 0; x < resolution; x++)
            {
                float tx = resolution == 1 ? 0.0f : x / (float)(resolution - 1);
                float ty = resolution == 1 ? 0.0f : y / (float)(resolution - 1);
                Vector2 world = new(
                    center.X + (tx - 0.5f) * safeWorldSize,
                    center.Y + (ty - 0.5f) * safeWorldSize);
                TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);

                minHeight = Mathf.Min(minHeight, field.Height);
                maxHeight = Mathf.Max(maxHeight, field.Height);
                heightSum += field.Height;

                if (field.Height >= profile.SeaLevel + 3.0f)
                {
                    landCount++;
                }

                if (field.Height >= profile.SeaLevel - 12.0f && field.Height <= profile.SeaLevel + 18.0f)
                {
                    coastCount++;
                }

                if (field.River > 0.62f && field.Height > profile.SeaLevel - 6.0f)
                {
                    riverCount++;
                }

                if (field.ScenicPotential > 0.66f && field.Height > profile.SeaLevel + 18.0f)
                {
                    scenicCount++;
                }

                if (field.Height > profile.SeaLevel + 8.0f && field.Traversability > 0.42f)
                {
                    traversableLandCount++;
                }

                int landscapeIndex = Mathf.Clamp((int)field.LandscapeKind, 0, landscapeCounts.Length - 1);
                landscapeCounts[landscapeIndex]++;
                int biomeIndex = Mathf.Clamp((int)field.BiomeKind, 0, biomeCounts.Length - 1);
                biomeCounts[biomeIndex]++;
            }
        }

        int distinctLandscapeKinds = 0;
        for (int i = 0; i < landscapeCounts.Length; i++)
        {
            if (landscapeCounts[i] > 0)
            {
                distinctLandscapeKinds++;
            }
        }

        int distinctBiomeKinds = 0;
        for (int i = 0; i < biomeCounts.Length; i++)
        {
            if (biomeCounts[i] > 0)
            {
                distinctBiomeKinds++;
            }
        }

        float invSampleCount = sampleCount <= 0 ? 0.0f : 1.0f / sampleCount;
        return new TerrainQualityReport(
            sampleCount,
            safeWorldSize,
            minHeight,
            maxHeight,
            (float)(heightSum * invSampleCount),
            landCount * invSampleCount,
            landscapeCounts[(int)TerrainLandscapeKind.Ocean] * invSampleCount,
            coastCount * invSampleCount,
            riverCount * invSampleCount,
            scenicCount * invSampleCount,
            traversableLandCount * invSampleCount,
            distinctLandscapeKinds,
            distinctBiomeKinds,
            landscapeCounts[(int)TerrainLandscapeKind.Ocean],
            landscapeCounts[(int)TerrainLandscapeKind.Coast],
            landscapeCounts[(int)TerrainLandscapeKind.Lowland],
            landscapeCounts[(int)TerrainLandscapeKind.Wetland],
            landscapeCounts[(int)TerrainLandscapeKind.ForestBasin],
            landscapeCounts[(int)TerrainLandscapeKind.RiverValley],
            landscapeCounts[(int)TerrainLandscapeKind.Canyon],
            landscapeCounts[(int)TerrainLandscapeKind.Highlands],
            landscapeCounts[(int)TerrainLandscapeKind.MountainMassif],
            landscapeCounts[(int)TerrainLandscapeKind.Snowfield],
            landscapeCounts[(int)TerrainLandscapeKind.VistaPlateau],
            landscapeCounts[(int)TerrainLandscapeKind.Lake],
            biomeCounts[(int)TerrainBiomeKind.Ocean],
            biomeCounts[(int)TerrainBiomeKind.Coast],
            biomeCounts[(int)TerrainBiomeKind.Island],
            biomeCounts[(int)TerrainBiomeKind.Plains],
            biomeCounts[(int)TerrainBiomeKind.Grassland],
            biomeCounts[(int)TerrainBiomeKind.Desert],
            biomeCounts[(int)TerrainBiomeKind.Oasis],
            biomeCounts[(int)TerrainBiomeKind.Forest],
            biomeCounts[(int)TerrainBiomeKind.Wetland],
            biomeCounts[(int)TerrainBiomeKind.Hills],
            biomeCounts[(int)TerrainBiomeKind.Mountains],
            biomeCounts[(int)TerrainBiomeKind.Snowfield],
            biomeCounts[(int)TerrainBiomeKind.Lake]);
    }

    internal static TerrainQualityGateResult ValidateReport(
        TerrainQualityReport report,
        TerrainQualityThresholds thresholds)
    {
        var summary = new StringBuilder();
        bool passed = true;

        AppendGate(
            summary,
            "land ratio",
            report.LandRatio >= thresholds.MinLandRatio && report.LandRatio <= thresholds.MaxLandRatio,
            $"{report.LandRatio:0.000}",
            $"{thresholds.MinLandRatio:0.000}-{thresholds.MaxLandRatio:0.000}",
            ref passed);
        AppendGate(
            summary,
            "river coverage",
            report.RiverRatio >= thresholds.MinRiverRatio,
            $"{report.RiverRatio:0.000}",
            $">= {thresholds.MinRiverRatio:0.000}",
            ref passed);
        AppendGate(
            summary,
            "scenic coverage",
            report.ScenicRatio >= thresholds.MinScenicRatio,
            $"{report.ScenicRatio:0.000}",
            $">= {thresholds.MinScenicRatio:0.000}",
            ref passed);
        AppendGate(
            summary,
            "traversable land",
            report.TraversableLandRatio >= thresholds.MinTraversableLandRatio,
            $"{report.TraversableLandRatio:0.000}",
            $">= {thresholds.MinTraversableLandRatio:0.000}",
            ref passed);
        AppendGate(
            summary,
            "landscape variety",
            report.DistinctLandscapeKinds >= thresholds.MinDistinctLandscapeKinds,
            report.DistinctLandscapeKinds.ToString(),
            $">= {thresholds.MinDistinctLandscapeKinds}",
            ref passed);
        AppendGate(
            summary,
            "biome variety",
            report.DistinctBiomeKinds >= thresholds.MinDistinctBiomeKinds,
            report.DistinctBiomeKinds.ToString(),
            $">= {thresholds.MinDistinctBiomeKinds}",
            ref passed);
        AppendGate(
            summary,
            "plains/grassland coverage",
            report.PlainsGrasslandRatio >= thresholds.MinPlainsGrasslandRatio,
            $"{report.PlainsGrasslandRatio:0.000}",
            $">= {thresholds.MinPlainsGrasslandRatio:0.000}",
            ref passed);
        AppendGate(
            summary,
            "desert/oasis coverage",
            report.DesertOasisRatio >= thresholds.MinDesertOasisRatio,
            $"{report.DesertOasisRatio:0.000}",
            $">= {thresholds.MinDesertOasisRatio:0.000}",
            ref passed);
        AppendGate(
            summary,
            "island/coast coverage",
            report.IslandCoastRatio >= thresholds.MinIslandCoastRatio,
            $"{report.IslandCoastRatio:0.000}",
            $">= {thresholds.MinIslandCoastRatio:0.000}",
            ref passed);
        AppendGate(
            summary,
            "hill/mountain coverage",
            report.HillMountainRatio >= thresholds.MinHillMountainRatio,
            $"{report.HillMountainRatio:0.000}",
            $">= {thresholds.MinHillMountainRatio:0.000}",
            ref passed);
        AppendGate(
            summary,
            "snow coverage",
            report.SnowRatio >= thresholds.MinSnowRatio,
            $"{report.SnowRatio:0.000}",
            $">= {thresholds.MinSnowRatio:0.000}",
            ref passed);
        AppendGate(
            summary,
            "lake coverage",
            report.LakeRatio >= thresholds.MinLakeRatio,
            $"{report.LakeRatio:0.000}",
            $">= {thresholds.MinLakeRatio:0.000}",
            ref passed);

        return new TerrainQualityGateResult(passed, report, summary.ToString());
    }

    private static void AppendGate(
        StringBuilder summary,
        string name,
        bool passed,
        string actual,
        string expected,
        ref bool allPassed)
    {
        if (!passed)
        {
            allPassed = false;
        }

        summary
            .Append(passed ? "PASS" : "FAIL")
            .Append(": ")
            .Append(name)
            .Append(" actual ")
            .Append(actual)
            .Append(" expected ")
            .Append(expected)
            .AppendLine();
    }
}
