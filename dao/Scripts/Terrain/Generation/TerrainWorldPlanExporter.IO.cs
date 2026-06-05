using System;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanExporter
{
    private static string BuildOutputPath(string outputDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return fileName;
        }

        return $"{outputDirectory.Replace('\\', '/').TrimEnd('/')}/{fileName}";
    }

    private static string FileSystemPath(string path)
    {
        return path.Contains("://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(path)
            : System.IO.Path.GetFullPath(path);
    }

    private static void EnsureDirectoryForPath(string path)
    {
        int slash = path.Replace('\\', '/').LastIndexOf('/');
        if (slash <= 0)
        {
            return;
        }

        EnsureOutputDirectory(path[..slash]);
    }

    private static Error EnsureOutputDirectory(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return Error.Ok;
        }

        try
        {
            string normalized = outputDirectory.Replace('\\', '/');
            if (normalized.Contains("://", StringComparison.Ordinal))
            {
                return DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(normalized));
            }

            System.IO.Directory.CreateDirectory(normalized);
            return Error.Ok;
        }
        catch (Exception exception)
        {
            GD.PushError($"Failed to create terrain output directory '{outputDirectory}': {exception.Message}");
            return Error.FileCantWrite;
        }
    }
}
