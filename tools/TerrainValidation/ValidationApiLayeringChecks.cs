using System;
using System.Collections.Generic;
using System.IO;

internal static class TerrainValidationApiLayeringChecks
{
    private static readonly string[] IgnoredRelativePathPrefixes =
    [
        "dao/Scripts/Terrain/",
        "dao/Scripts/Demo/"
    ];

    private static readonly string[] ForbiddenGameplayDependencyTokens =
    [
        "TerrainTileBuilder",
        "TerrainWorldPlanner",
        "TerrainChunk",
        "TerrainTileDataCache",
        "TerrainStreamingSetBuilder",
        "NativeTerrainBridge"
    ];

    internal static TerrainApiLayeringSmokeReport ValidateTerrainApiLayeringContract()
    {
        try
        {
            string root = FindRepositoryRoot();
            string scriptsDirectory = Path.Combine(root, "dao", "Scripts");
            if (!Directory.Exists(scriptsDirectory))
            {
                return new TerrainApiLayeringSmokeReport(
                    false,
                    0,
                    0,
                    [],
                    $"dao/Scripts was not found under '{root}'");
            }

            int scannedFileCount = 0;
            var violations = new List<string>();
            foreach (string file in Directory.EnumerateFiles(scriptsDirectory, "*.cs", SearchOption.AllDirectories))
            {
                string relativePath = NormalizeRelativePath(root, file);
                if (ShouldIgnore(relativePath))
                {
                    continue;
                }

                scannedFileCount++;
                string source = File.ReadAllText(file);
                foreach (string token in ForbiddenGameplayDependencyTokens)
                {
                    if (source.Contains(token, StringComparison.Ordinal))
                    {
                        violations.Add($"{relativePath}: references internal terrain implementation token '{token}'");
                    }
                }
            }

            bool passed = violations.Count == 0;
            string reason = passed
                ? "gameplay-facing scripts only depend on the stable terrain runtime API or no gameplay scripts exist yet"
                : "gameplay-facing scripts referenced terrain generation/streaming implementation details";

            return new TerrainApiLayeringSmokeReport(
                passed,
                scannedFileCount,
                violations.Count,
                violations.ToArray(),
                reason);
        }
        catch (Exception ex)
        {
            return new TerrainApiLayeringSmokeReport(
                false,
                0,
                0,
                [],
                $"terrain API layering smoke threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool ShouldIgnore(string relativePath)
    {
        foreach (string prefix in IgnoredRelativePathPrefixes)
        {
            if (relativePath.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string FindRepositoryRoot()
    {
        string? directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "global.json")) &&
                Directory.Exists(Path.Combine(directory, "dao")) &&
                Directory.Exists(Path.Combine(directory, "tools")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string NormalizeRelativePath(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }
}
