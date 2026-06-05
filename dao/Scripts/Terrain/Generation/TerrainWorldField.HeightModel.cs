using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldFieldSampler
{
    private static float BuildHeight(TerrainShapeTerms terms, TerrainGenerationProfile profile)
    {
        return BuildHeight(terms, profile, GetLandBalanceOffset(profile));
    }

    private static float BuildHeight(TerrainShapeTerms terms, TerrainGenerationProfile profile, float landBalanceOffset)
    {
        float height = BuildUnbalancedHeight(terms, profile);
        height -= landBalanceOffset;

        float terraceMask = Mathf.SmoothStep(0.52f, 0.86f, terms.Mountains) *
            profile.VistaFrequency *
            Mathf.Lerp(0.55f, 1.0f, terms.Alpine);
        return ProceduralNoise.Terrace(height, Mathf.Max(12.0f, profile.TerraceStrength), terraceMask * 0.38f);
    }

    private static float BuildUnbalancedHeight(TerrainShapeTerms terms, TerrainGenerationProfile profile)
    {
        float lowlandFlatness = Mathf.Clamp(
            Mathf.Max(terms.Plains * 0.80f, Mathf.Max(terms.Aridity * 0.72f, terms.Wetland * 0.68f)) *
            (1.0f - Mathf.SmoothStep(0.32f, 0.64f, terms.Mountains)),
            0.0f,
            1.0f);
        float mountainFactor = Mathf.Lerp(0.48f, 1.14f, Mathf.Clamp(terms.Alpine + terms.Hills * 0.24f, 0.0f, 1.0f));
        float shelfFactor = Mathf.Lerp(0.20f, 0.34f, 1.0f - lowlandFlatness);
        float detailFactor = profile.DetailWeight *
            Mathf.Lerp(0.42f, 1.16f, Mathf.Clamp(terms.Alpine + terms.Hills * 0.45f, 0.0f, 1.0f)) *
            Mathf.Lerp(1.0f, 0.62f, lowlandFlatness);

        float height =
            ((terms.Basin - 0.44f) * profile.HeightScale * 0.72f) +
            (terms.Shelf * terms.BroadElevation * profile.HeightScale * shelfFactor) +
            (terms.Mountains * profile.HeightScale * mountainFactor) +
            (terms.MicroDetail * profile.HeightScale * detailFactor) +
            (terms.Island * profile.HeightScale * 0.36f);

        float lowlandTarget =
            ((terms.Basin - 0.46f) * profile.HeightScale * 0.44f) +
            ((terms.BroadElevation - 0.50f) * profile.HeightScale * 0.10f) +
            (terms.Island * profile.HeightScale * 0.25f);
        height = Mathf.Lerp(height, lowlandTarget, lowlandFlatness * 0.62f);
        height += terms.Aridity * (terms.DuneDetail - 0.40f) * profile.HeightScale * 0.075f;
        height -= terms.Lake *
            Mathf.Lerp(profile.HeightScale * 0.030f, profile.HeightScale * 0.070f, 1.0f - Mathf.Clamp(terms.Mountains, 0.0f, 1.0f)) *
            (0.72f + terms.Wetland * 0.28f);
        height -= terms.Wetland *
            Mathf.SmoothStep(0.26f, 0.72f, terms.Continent + terms.Island * 0.20f) *
            profile.HeightScale *
            0.045f;

        float shallowShelf = terms.Shelf * (1.0f - Mathf.SmoothStep(0.14f, 0.46f, terms.Mountains));
        float waterlineProximity = 1.0f - Mathf.SmoothStep(profile.SeaLevel + 52.0f, profile.SeaLevel + 220.0f, height);
        height -= shallowShelf * waterlineProximity * profile.HeightScale * 0.035f;

        float valleyCarve = terms.River * profile.RiverCarveDepth * (0.35f + terms.Mountains * 0.85f);
        height -= valleyCarve * profile.ValleyWeight;

        return height;
    }

    private static float GetLandBalanceOffset(TerrainGenerationProfile profile)
    {
        return LandBalanceOffsets.GetOrAdd(profile, ComputeLandBalanceOffset);
    }

    private static float ComputeLandBalanceOffset(TerrainGenerationProfile profile)
    {
        const int resolution = 33;
        const float targetLandRatio = 0.58f;
        const float correctionStrength = 0.48f;
        float extent = Mathf.Max(profile.ChunkSize * 48.0f, profile.ContinentScale * 2.2f);
        int landCount = 0;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float tx = x / (float)(resolution - 1);
                float ty = y / (float)(resolution - 1);
                Vector2 world = new((tx - 0.5f) * extent, (ty - 0.5f) * extent);
                TerrainShapeTerms terms = SampleShapeTerms(world, profile, includeMicroDetail: false);
                float height = BuildUnbalancedHeight(terms, profile);
                if (height >= profile.SeaLevel + 3.0f)
                {
                    landCount++;
                }
            }
        }

        float landRatio = landCount / (float)(resolution * resolution);
        float offset = (landRatio - targetLandRatio) * profile.HeightScale * correctionStrength;
        return Mathf.Clamp(offset, profile.HeightScale * -0.16f, profile.HeightScale * 0.16f);
    }
}
