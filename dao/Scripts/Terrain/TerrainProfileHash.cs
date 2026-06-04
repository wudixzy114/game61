using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Dao.Terrain;

/// <summary>Computes stable content identity hashes for terrain generation profiles.</summary>
public static class TerrainProfileHash
{
    public static string Compute(TerrainGenerationProfile profile)
    {
        var builder = new StringBuilder(512);
        Append(builder, nameof(profile.Seed), profile.Seed);
        Append(builder, nameof(profile.ChunkSize), profile.ChunkSize);
        Append(builder, nameof(profile.BaseResolution), profile.BaseResolution);
        Append(builder, nameof(profile.StreamRadiusChunks), profile.StreamRadiusChunks);
        Append(builder, nameof(profile.CollisionRadiusChunks), profile.CollisionRadiusChunks);
        Append(builder, nameof(profile.MaxLod), profile.MaxLod);
        Append(builder, nameof(profile.HeightScale), profile.HeightScale);
        Append(builder, nameof(profile.SeaLevel), profile.SeaLevel);
        Append(builder, nameof(profile.ContinentScale), profile.ContinentScale);
        Append(builder, nameof(profile.MountainScale), profile.MountainScale);
        Append(builder, nameof(profile.MountainWeight), profile.MountainWeight);
        Append(builder, nameof(profile.ValleyWeight), profile.ValleyWeight);
        Append(builder, nameof(profile.DetailWeight), profile.DetailWeight);
        Append(builder, nameof(profile.VistaFrequency), profile.VistaFrequency);
        Append(builder, nameof(profile.RiverStrength), profile.RiverStrength);
        Append(builder, nameof(profile.RiverCarveDepth), profile.RiverCarveDepth);
        Append(builder, nameof(profile.TerraceStrength), profile.TerraceStrength);
        Append(builder, nameof(profile.SkirtDepth), profile.SkirtDepth);
        Append(builder, nameof(profile.MaxCompletedTilesPerFrame), profile.MaxCompletedTilesPerFrame);
        Append(builder, nameof(profile.MaxQueuedTileJobs), profile.MaxQueuedTileJobs);
        Append(builder, nameof(profile.MaxCachedTileData), profile.MaxCachedTileData);
        Append(builder, nameof(profile.GenerateCollision), profile.GenerateCollision);
        Append(builder, nameof(profile.UseNativeSamplerWhenAvailable), profile.UseNativeSamplerWhenAvailable);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string name, int value)
    {
        builder.Append(name).Append('=').Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');
    }

    private static void Append(StringBuilder builder, string name, float value)
    {
        builder.Append(name).Append('=').Append(value.ToString("G9", CultureInfo.InvariantCulture)).Append(';');
    }

    private static void Append(StringBuilder builder, string name, bool value)
    {
        builder.Append(name).Append('=').Append(value ? "true" : "false").Append(';');
    }
}
