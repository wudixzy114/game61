using System;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanSerializer
{
    private static TerrainVector2Dto[] ToDtos(Vector2[] values)
    {
        if (values.Length == 0)
        {
            return [];
        }

        var result = new TerrainVector2Dto[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = ToDto(values[i]);
        }

        return result;
    }

    private static Vector2[] FromDtos(TerrainVector2Dto[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return [];
        }

        var result = new Vector2[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = FromDto(values[i]);
        }

        return result;
    }

    private static TerrainVector2Dto ToDto(Vector2 value)
    {
        return new TerrainVector2Dto { X = value.X, Z = value.Y };
    }

    private static Vector2 FromDto(TerrainVector2Dto? value)
    {
        if (value is null)
        {
            throw new InvalidOperationException("terrain plan JSON is missing a vector value");
        }

        return new Vector2(value.X, value.Z);
    }

    private static TerrainEnumDto ToDto<T>(T value)
        where T : struct, Enum
    {
        return new TerrainEnumDto { Name = value.ToString(), Value = Convert.ToInt32(value) };
    }

    private static T EnumValue<T>(TerrainEnumDto? value)
        where T : struct, Enum
    {
        if (value is null)
        {
            throw new InvalidOperationException($"terrain plan JSON is missing enum {typeof(T).Name}");
        }

        if (!Enum.IsDefined(typeof(T), value.Value))
        {
            throw new InvalidOperationException($"terrain plan JSON has unsupported {typeof(T).Name} value {value.Value}");
        }

        T parsed = (T)Enum.ToObject(typeof(T), value.Value);
        if (!string.Equals(parsed.ToString(), value.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"terrain plan JSON enum mismatch for {typeof(T).Name}: {value.Name}/{value.Value}");
        }

        return parsed;
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

        string directory = path[..slash];
        string normalized = directory.Replace('\\', '/');
        if (normalized.Contains("://", StringComparison.Ordinal))
        {
            _ = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(normalized));
            return;
        }

        System.IO.Directory.CreateDirectory(normalized);
    }
}
