using System;
using Godot;

internal static class TerrainValidationCliHelpers
{
    internal static TerrainValidationTierSpec ParseValidationTier(string[] args, out string error)
    {
        error = string.Empty;
        string? tier = GetArg(args, "--validation-tier");
        if (string.IsNullOrWhiteSpace(tier))
        {
            return TerrainValidationTierSpec.Custom;
        }

        if (HasAnyFlag(
            args,
            "--skip-corridor-smoke",
            "--skip-route-scatter-smoke",
            "--skip-poi-tile-smoke",
            "--skip-gameplay-scatter-smoke",
            "--skip-biome-scatter-smoke",
            "--skip-scenic-landmark-smoke",
            "--skip-artifact-smoke",
            "--skip-plan-json-smoke",
            "--skip-enum-contract-smoke",
            "--skip-runtime-api-smoke",
            "--skip-anchor-smoke",
            "--skip-runtime-world-smoke"))
        {
            error = "--validation-tier cannot be combined with --skip-* flags; tiers are fixed regression gates.";
            return TerrainValidationTierSpec.Custom;
        }

        if (HasAnyOption(
            args,
            "--seed",
            "--seed-count",
            "--seed-step",
            "--world-size",
            "--artifact-image-size",
            "--smoke-all-seeds",
            "--native-smoke",
            "--benchmark-tiles",
            "--benchmark-tile-count"))
        {
            error = "--validation-tier cannot be combined with seed/world/smoke/native/benchmark overrides; choose a tier or custom flags.";
            return TerrainValidationTierSpec.Custom;
        }

        return tier.ToLowerInvariant() switch
        {
            "pr" => TerrainValidationTierSpec.Pr,
            "nightly" => TerrainValidationTierSpec.Nightly,
            "release" => TerrainValidationTierSpec.Release,
            _ => FailUnknownTier(tier, out error)
        };
    }

    internal static TerrainValidationTierSpec FailUnknownTier(string tier, out string error)
    {
        error = $"unknown --validation-tier '{tier}'. Valid tiers: pr, nightly, release.";
        return TerrainValidationTierSpec.Custom;
    }

    internal static void PrintValidationTier(
        TerrainValidationTierSpec tier,
        int seedCount,
        bool smokeAllSeeds,
        bool nativeSmoke,
        bool benchmarkTiles,
        int benchmarkTileCount)
    {
        Console.WriteLine(
            $"Validation tier: {tier.Name} " +
            $"(seeds {seedCount}, smoke-all-seeds {smokeAllSeeds}, native-smoke {nativeSmoke}, " +
            $"benchmark-tiles {benchmarkTiles}, benchmark-tile-count {benchmarkTileCount})");
    }

    internal static int GetIntArg(string[] args, string name, int fallback)
    {
        string? value = GetArg(args, name);
        return int.TryParse(value, out int parsed) ? parsed : fallback;
    }

    internal static float GetFloatArg(string[] args, string name, float fallback)
    {
        string? value = GetArg(args, name);
        return float.TryParse(value, out float parsed) ? parsed : fallback;
    }

    internal static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    internal static bool HasFlag(string[] args, string name)
    {
        foreach (string arg in args)
        {
            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasAnyFlag(string[] args, params string[] names)
    {
        foreach (string name in names)
        {
            if (HasFlag(args, name))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasAnyOption(string[] args, params string[] names)
    {
        foreach (string name in names)
        {
            if (HasFlag(args, name) || GetArg(args, name) is not null)
            {
                return true;
            }
        }

        return false;
    }

    internal static string DefaultArtifactOutputDirectory(int seed)
    {
        return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dao_terrain_validation", $"seed_{seed}");
    }

    internal static string DefaultBatchArtifactOutputDirectory(int seed, int seedCount)
    {
        return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dao_terrain_validation", $"batch_seed_{seed}_count_{seedCount}");
    }

    internal static string ArtifactOutputDirectoryForSeed(
        string baseDirectory,
        int seed,
        bool isolateBySeed)
    {
        return isolateBySeed
            ? System.IO.Path.Combine(baseDirectory, $"seed_{seed}")
            : baseDirectory;
    }

    internal static string FileSystemPath(string path)
    {
        if (path.Contains("://", StringComparison.Ordinal))
        {
            return ProjectSettings.GlobalizePath(path);
        }

        return System.IO.Path.GetFullPath(path);
    }
}
