using System;
using System.IO;
using System.Reflection;
using Dao.Terrain;
using Dao.Terrain.Generation;
using Godot;

internal static class TerrainValidationVisualCatalogChecks
{
    internal static TerrainVisualCatalogSmokeReport ValidateTerrainVisualCatalogContract()
    {
        try
        {
            string root = FindRepositoryRoot();
            string catalogSource = ReadRepositoryFile(root, "dao", "Scripts", "Terrain", "TerrainVisualCatalog.cs");
            string scatterEntrySource = ReadRepositoryFile(root, "dao", "Scripts", "Terrain", "TerrainScatterVisualEntryResource.cs");
            string landmarkEntrySource = ReadRepositoryFile(root, "dao", "Scripts", "Terrain", "TerrainLandmarkVisualEntryResource.cs");
            string chunkSource = ReadRepositoryFile(root, "dao", "Scripts", "Terrain", "Streaming", "TerrainChunk.cs");
            string chunkScatterSource = ReadRepositoryFile(root, "dao", "Scripts", "Terrain", "Streaming", "TerrainChunk.SurfaceScatter.cs");
            string chunkLandmarkSource = ReadRepositoryFile(root, "dao", "Scripts", "Terrain", "Streaming", "TerrainChunk.Landmarks.cs");

            bool meshEntryLookupPassed =
                HasPublicProperty<TerrainScatterVisualEntryResource>("Mesh", typeof(Mesh)) &&
                HasPublicProperty<TerrainLandmarkVisualEntryResource>("Mesh", typeof(Mesh)) &&
                HasPublicMethod<TerrainVisualCatalog>("GetScatterEntry", typeof(TerrainScatterVisualEntryResource), typeof(TerrainScatterKind)) &&
                HasPublicMethod<TerrainVisualCatalog>("GetLandmarkEntry", typeof(TerrainLandmarkVisualEntryResource), typeof(TerrainLandmarkKind));
            bool sceneEntryLookupPassed =
                HasPublicProperty<TerrainScatterVisualEntryResource>("Scene", typeof(PackedScene)) &&
                HasPublicProperty<TerrainLandmarkVisualEntryResource>("Scene", typeof(PackedScene));
            bool sceneOnlyEntriesAreAccepted =
                catalogSource.Contains("entry?.Mesh is null && entry?.Scene is null", StringComparison.Ordinal);
            bool missingEntryDetectionPassed =
                catalogSource.Contains("GetMissingScatterMeshKinds", StringComparison.Ordinal) &&
                catalogSource.Contains("GetMissingLandmarkMeshKinds", StringComparison.Ordinal) &&
                catalogSource.Contains("TerrainScatterKind.Landmark", StringComparison.Ordinal);
            bool visualEntryMetadataPassed =
                SourceContainsVisualEntryContract(scatterEntrySource) &&
                SourceContainsVisualEntryContract(landmarkEntrySource);
            bool runtimeScenePathPassed =
                chunkSource.Contains("PackedScene scene", StringComparison.Ordinal) &&
                chunkSource.Contains("scene.Instantiate()", StringComparison.Ordinal) &&
                chunkScatterSource.Contains("RebuildScatterSceneKind", StringComparison.Ordinal) &&
                chunkLandmarkSource.Contains("RebuildLandmarkSceneKind", StringComparison.Ordinal);

            bool passed =
                meshEntryLookupPassed &&
                sceneEntryLookupPassed &&
                sceneOnlyEntriesAreAccepted &&
                missingEntryDetectionPassed &&
                visualEntryMetadataPassed &&
                runtimeScenePathPassed;

            return new TerrainVisualCatalogSmokeReport(
                passed,
                meshEntryLookupPassed,
                sceneEntryLookupPassed,
                sceneOnlyEntriesAreAccepted,
                missingEntryDetectionPassed,
                visualEntryMetadataPassed,
                runtimeScenePathPassed,
                passed
                    ? "visual catalog supports mesh entries, scene entries, production metadata, and runtime scene instancing"
                    : "visual catalog contract did not cover mesh/scene entries, metadata, fallback validation, or runtime scene instancing");
        }
        catch (Exception ex)
        {
            return new TerrainVisualCatalogSmokeReport(
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                $"terrain visual catalog smoke threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool SourceContainsVisualEntryContract(string source)
    {
        return source.Contains("PackedScene? Scene", StringComparison.Ordinal) &&
               source.Contains("PreferSceneInstances", StringComparison.Ordinal) &&
               source.Contains("MinLod", StringComparison.Ordinal) &&
               source.Contains("MaxLod", StringComparison.Ordinal) &&
               source.Contains("CreatesCollision", StringComparison.Ordinal) &&
               source.Contains("CreatesNavigationObstacle", StringComparison.Ordinal) &&
               source.Contains("InteractionTag", StringComparison.Ordinal);
    }

    private static bool HasPublicProperty<T>(string name, Type type)
    {
        PropertyInfo? property = typeof(T).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        return property is not null && property.PropertyType == type;
    }

    private static bool HasPublicMethod<T>(string name, Type returnType, params Type[] parameterTypes)
    {
        MethodInfo? method = typeof(T).GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, parameterTypes);
        return method is not null && method.ReturnType == returnType;
    }

    private static string ReadRepositoryFile(string root, params string[] segments)
    {
        string[] pathSegments = new string[segments.Length + 1];
        pathSegments[0] = root;
        Array.Copy(segments, 0, pathSegments, 1, segments.Length);
        string path = Path.Combine(pathSegments);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
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
}
