using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Surface semantic override applied by a mutable terrain modification layer.</summary>
public readonly record struct TerrainSurfaceOverride(
    Vector2 WorldPosition,
    float Radius,
    TerrainBiomeKind BiomeKind,
    TerrainLandscapeKind LandscapeKind,
    TerrainGameplayTag GameplayTags,
    float Traversability,
    float HazardPotential);

/// <summary>Height delta brush applied over deterministic base terrain.</summary>
public readonly record struct TerrainHeightDelta(
    Vector2 WorldPosition,
    float Radius,
    float Delta,
    float InnerRadius);

/// <summary>Persistent scatter mutation for removals or additions inside a world-space radius.</summary>
public readonly record struct TerrainScatterModification(
    Vector2 WorldPosition,
    float Radius,
    TerrainScatterKind Kind,
    bool Remove,
    int StableId,
    string State);

/// <summary>Persistent landmark state override for planned or generated terrain landmarks.</summary>
public readonly record struct TerrainLandmarkModification(
    Vector2 WorldPosition,
    float Radius,
    TerrainLandmarkKind Kind,
    int StableId,
    string State);

/// <summary>Persistent route state override for planned route links.</summary>
public readonly record struct TerrainRouteModification(
    int FromPointId,
    int ToPointId,
    bool Blocked,
    bool Unlocked,
    float CostMultiplier,
    string State);

/// <summary>Deterministic base terrain plus mutable saved overlay data.</summary>
public sealed class TerrainModificationLayer
{
    public const string Contract = "terrain-modification-layer-v1";
    public const int CurrentVersion = 1;

    private readonly TerrainHeightDelta[] _heightDeltas;
    private readonly TerrainSurfaceOverride[] _surfaceOverrides;
    private readonly TerrainScatterModification[] _scatterModifications;
    private readonly TerrainLandmarkModification[] _landmarkModifications;
    private readonly TerrainRouteModification[] _routeModifications;

    public static TerrainModificationLayer Empty { get; } = new(
        [],
        [],
        [],
        [],
        []);

    public TerrainModificationLayer(
        TerrainHeightDelta[] heightDeltas,
        TerrainSurfaceOverride[] surfaceOverrides,
        TerrainScatterModification[] scatterModifications,
        TerrainLandmarkModification[] landmarkModifications,
        TerrainRouteModification[] routeModifications)
    {
        _heightDeltas = heightDeltas.Length == 0 ? [] : (TerrainHeightDelta[])heightDeltas.Clone();
        _surfaceOverrides = surfaceOverrides.Length == 0 ? [] : (TerrainSurfaceOverride[])surfaceOverrides.Clone();
        _scatterModifications = scatterModifications.Length == 0 ? [] : (TerrainScatterModification[])scatterModifications.Clone();
        _landmarkModifications = landmarkModifications.Length == 0 ? [] : (TerrainLandmarkModification[])landmarkModifications.Clone();
        _routeModifications = routeModifications.Length == 0 ? [] : (TerrainRouteModification[])routeModifications.Clone();
    }

    public TerrainHeightDelta[] HeightDeltas => _heightDeltas.Length == 0 ? [] : (TerrainHeightDelta[])_heightDeltas.Clone();
    public TerrainSurfaceOverride[] SurfaceOverrides => _surfaceOverrides.Length == 0 ? [] : (TerrainSurfaceOverride[])_surfaceOverrides.Clone();
    public TerrainScatterModification[] ScatterModifications => _scatterModifications.Length == 0 ? [] : (TerrainScatterModification[])_scatterModifications.Clone();
    public TerrainLandmarkModification[] LandmarkModifications => _landmarkModifications.Length == 0 ? [] : (TerrainLandmarkModification[])_landmarkModifications.Clone();
    public TerrainRouteModification[] RouteModifications => _routeModifications.Length == 0 ? [] : (TerrainRouteModification[])_routeModifications.Clone();
    public bool IsEmpty => _heightDeltas.Length == 0 &&
        _surfaceOverrides.Length == 0 &&
        _scatterModifications.Length == 0 &&
        _landmarkModifications.Length == 0 &&
        _routeModifications.Length == 0;

    public TerrainWorldField ApplyToField(TerrainWorldField field)
    {
        TerrainWorldField modified = field;
        for (int i = 0; i < _heightDeltas.Length; i++)
        {
            TerrainHeightDelta delta = _heightDeltas[i];
            float influence = RadialInfluence(field.WorldPosition, delta.WorldPosition, delta.InnerRadius, delta.Radius);
            if (influence <= 0.0f)
            {
                continue;
            }

            modified = modified with { Height = modified.Height + delta.Delta * influence };
        }

        TerrainSurfaceOverride? bestOverride = null;
        float bestInfluence = 0.0f;
        for (int i = 0; i < _surfaceOverrides.Length; i++)
        {
            TerrainSurfaceOverride surface = _surfaceOverrides[i];
            float influence = RadialInfluence(field.WorldPosition, surface.WorldPosition, 0.0f, surface.Radius);
            if (influence > bestInfluence)
            {
                bestInfluence = influence;
                bestOverride = surface;
            }
        }

        if (bestOverride is TerrainSurfaceOverride surfaceOverride)
        {
            modified = modified with
            {
                BiomeKind = surfaceOverride.BiomeKind,
                LandscapeKind = surfaceOverride.LandscapeKind,
                Traversability = Mathf.Clamp(surfaceOverride.Traversability, 0.0f, 1.0f),
                HazardPotential = Mathf.Clamp(surfaceOverride.HazardPotential, 0.0f, 1.0f)
            };
        }

        return modified;
    }

    public TerrainTileCoord[] QueryAffectedTiles(float chunkSize)
    {
        var tiles = new HashSet<TerrainTileCoord>();
        for (int i = 0; i < _heightDeltas.Length; i++)
        {
            AddTilesForCircle(tiles, _heightDeltas[i].WorldPosition, _heightDeltas[i].Radius, chunkSize);
        }

        for (int i = 0; i < _surfaceOverrides.Length; i++)
        {
            AddTilesForCircle(tiles, _surfaceOverrides[i].WorldPosition, _surfaceOverrides[i].Radius, chunkSize);
        }

        for (int i = 0; i < _scatterModifications.Length; i++)
        {
            AddTilesForCircle(tiles, _scatterModifications[i].WorldPosition, _scatterModifications[i].Radius, chunkSize);
        }

        for (int i = 0; i < _landmarkModifications.Length; i++)
        {
            AddTilesForCircle(tiles, _landmarkModifications[i].WorldPosition, _landmarkModifications[i].Radius, chunkSize);
        }

        TerrainTileCoord[] result = new TerrainTileCoord[tiles.Count];
        tiles.CopyTo(result);
        Array.Sort(result, CompareTiles);
        return result;
    }

    public bool HasRouteState(int fromPointId, int toPointId, out TerrainRouteModification modification)
    {
        for (int i = 0; i < _routeModifications.Length; i++)
        {
            TerrainRouteModification route = _routeModifications[i];
            bool matchesForward = route.FromPointId == fromPointId && route.ToPointId == toPointId;
            bool matchesReverse = route.FromPointId == toPointId && route.ToPointId == fromPointId;
            if (matchesForward || matchesReverse)
            {
                modification = route;
                return true;
            }
        }

        modification = default;
        return false;
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(ToDto(this), JsonOptions);
    }

    public Error SaveJson(string outputPath)
    {
        try
        {
            string filePath = FileSystemPath(outputPath);
            EnsureDirectoryForPath(filePath);
            File.WriteAllText(filePath, ToJson());
            return Error.Ok;
        }
        catch (Exception)
        {
            return Error.FileCantWrite;
        }
    }

    public static bool TryFromJson(string json, out TerrainModificationLayer? layer, out string error)
    {
        layer = null;
        error = string.Empty;
        try
        {
            TerrainModificationLayerDto? dto = JsonSerializer.Deserialize<TerrainModificationLayerDto>(json, JsonOptions);
            if (dto is null)
            {
                error = "terrain modification JSON was empty or invalid";
                return false;
            }

            if (!string.Equals(dto.Contract, Contract, StringComparison.Ordinal))
            {
                error = $"unsupported terrain modification contract '{dto.Contract}', expected '{Contract}'";
                return false;
            }

            if (dto.Version != CurrentVersion)
            {
                error = $"unsupported terrain modification version '{dto.Version}', expected '{CurrentVersion}'";
                return false;
            }

            layer = FromDto(dto);
            return true;
        }
        catch (Exception exception)
        {
            error = $"failed to parse terrain modification JSON: {exception.Message}";
            return false;
        }
    }

    public static bool TryLoadJson(string path, out TerrainModificationLayer? layer, out string error)
    {
        layer = null;
        error = string.Empty;
        try
        {
            string filePath = FileSystemPath(path);
            if (!File.Exists(filePath))
            {
                error = $"terrain modification JSON file '{filePath}' was not found";
                return false;
            }

            return TryFromJson(File.ReadAllText(filePath), out layer, out error);
        }
        catch (Exception exception)
        {
            error = $"failed to load terrain modification JSON: {exception.Message}";
            return false;
        }
    }

    private static float RadialInfluence(Vector2 world, Vector2 center, float innerRadius, float outerRadius)
    {
        float radius = Mathf.Max(0.001f, outerRadius);
        float inner = Mathf.Clamp(innerRadius, 0.0f, radius);
        float distance = world.DistanceTo(center);
        if (distance > radius)
        {
            return 0.0f;
        }

        if (distance <= inner)
        {
            return 1.0f;
        }

        float t = Mathf.Clamp((distance - inner) / Mathf.Max(0.001f, radius - inner), 0.0f, 1.0f);
        return 1.0f - (t * t * (3.0f - 2.0f * t));
    }

    private static void AddTilesForCircle(HashSet<TerrainTileCoord> tiles, Vector2 center, float radius, float chunkSize)
    {
        float safeChunkSize = Mathf.Max(1.0f, chunkSize);
        float safeRadius = Mathf.Max(0.0f, radius);
        int minX = Mathf.FloorToInt((center.X - safeRadius) / safeChunkSize);
        int maxX = Mathf.FloorToInt((center.X + safeRadius) / safeChunkSize);
        int minZ = Mathf.FloorToInt((center.Y - safeRadius) / safeChunkSize);
        int maxZ = Mathf.FloorToInt((center.Y + safeRadius) / safeChunkSize);
        for (int z = minZ; z <= maxZ; z++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                tiles.Add(new TerrainTileCoord(x, z));
            }
        }
    }

    private static int CompareTiles(TerrainTileCoord a, TerrainTileCoord b)
    {
        int z = a.Z.CompareTo(b.Z);
        return z != 0 ? z : a.X.CompareTo(b.X);
    }

    private static TerrainModificationLayerDto ToDto(TerrainModificationLayer layer)
    {
        return new TerrainModificationLayerDto
        {
            Contract = Contract,
            Version = CurrentVersion,
            HeightDeltas = ToDtos(layer._heightDeltas),
            SurfaceOverrides = ToDtos(layer._surfaceOverrides),
            ScatterModifications = ToDtos(layer._scatterModifications),
            LandmarkModifications = ToDtos(layer._landmarkModifications),
            RouteModifications = ToDtos(layer._routeModifications)
        };
    }

    private static TerrainModificationLayer FromDto(TerrainModificationLayerDto dto)
    {
        return new TerrainModificationLayer(
            FromDtos(dto.HeightDeltas),
            FromDtos(dto.SurfaceOverrides),
            FromDtos(dto.ScatterModifications),
            FromDtos(dto.LandmarkModifications),
            FromDtos(dto.RouteModifications));
    }

    private static TerrainHeightDeltaDto[] ToDtos(TerrainHeightDelta[] values)
    {
        var result = new TerrainHeightDeltaDto[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = new TerrainHeightDeltaDto
            {
                World = ToDto(values[i].WorldPosition),
                Radius = values[i].Radius,
                Delta = values[i].Delta,
                InnerRadius = values[i].InnerRadius
            };
        }

        return result;
    }

    private static TerrainSurfaceOverrideDto[] ToDtos(TerrainSurfaceOverride[] values)
    {
        var result = new TerrainSurfaceOverrideDto[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = new TerrainSurfaceOverrideDto
            {
                World = ToDto(values[i].WorldPosition),
                Radius = values[i].Radius,
                Biome = ToDto(values[i].BiomeKind),
                Landscape = ToDto(values[i].LandscapeKind),
                GameplayTags = ToDto(values[i].GameplayTags),
                Traversability = values[i].Traversability,
                HazardPotential = values[i].HazardPotential
            };
        }

        return result;
    }

    private static TerrainScatterModificationDto[] ToDtos(TerrainScatterModification[] values)
    {
        var result = new TerrainScatterModificationDto[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = new TerrainScatterModificationDto
            {
                World = ToDto(values[i].WorldPosition),
                Radius = values[i].Radius,
                Kind = ToDto(values[i].Kind),
                Remove = values[i].Remove,
                StableId = values[i].StableId,
                State = values[i].State
            };
        }

        return result;
    }

    private static TerrainLandmarkModificationDto[] ToDtos(TerrainLandmarkModification[] values)
    {
        var result = new TerrainLandmarkModificationDto[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = new TerrainLandmarkModificationDto
            {
                World = ToDto(values[i].WorldPosition),
                Radius = values[i].Radius,
                Kind = ToDto(values[i].Kind),
                StableId = values[i].StableId,
                State = values[i].State
            };
        }

        return result;
    }

    private static TerrainRouteModificationDto[] ToDtos(TerrainRouteModification[] values)
    {
        var result = new TerrainRouteModificationDto[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = new TerrainRouteModificationDto
            {
                FromPointId = values[i].FromPointId,
                ToPointId = values[i].ToPointId,
                Blocked = values[i].Blocked,
                Unlocked = values[i].Unlocked,
                CostMultiplier = values[i].CostMultiplier,
                State = values[i].State
            };
        }

        return result;
    }

    private static TerrainHeightDelta[] FromDtos(TerrainHeightDeltaDto[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return [];
        }

        var result = new TerrainHeightDelta[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            TerrainHeightDeltaDto value = values[i];
            result[i] = new TerrainHeightDelta(FromDto(value.World), value.Radius, value.Delta, value.InnerRadius);
        }

        return result;
    }

    private static TerrainSurfaceOverride[] FromDtos(TerrainSurfaceOverrideDto[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return [];
        }

        var result = new TerrainSurfaceOverride[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            TerrainSurfaceOverrideDto value = values[i];
            result[i] = new TerrainSurfaceOverride(
                FromDto(value.World),
                value.Radius,
                EnumValue<TerrainBiomeKind>(value.Biome),
                EnumValue<TerrainLandscapeKind>(value.Landscape),
                EnumValue<TerrainGameplayTag>(value.GameplayTags),
                value.Traversability,
                value.HazardPotential);
        }

        return result;
    }

    private static TerrainScatterModification[] FromDtos(TerrainScatterModificationDto[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return [];
        }

        var result = new TerrainScatterModification[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            TerrainScatterModificationDto value = values[i];
            result[i] = new TerrainScatterModification(
                FromDto(value.World),
                value.Radius,
                EnumValue<TerrainScatterKind>(value.Kind),
                value.Remove,
                value.StableId,
                value.State ?? string.Empty);
        }

        return result;
    }

    private static TerrainLandmarkModification[] FromDtos(TerrainLandmarkModificationDto[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return [];
        }

        var result = new TerrainLandmarkModification[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            TerrainLandmarkModificationDto value = values[i];
            result[i] = new TerrainLandmarkModification(
                FromDto(value.World),
                value.Radius,
                EnumValue<TerrainLandmarkKind>(value.Kind),
                value.StableId,
                value.State ?? string.Empty);
        }

        return result;
    }

    private static TerrainRouteModification[] FromDtos(TerrainRouteModificationDto[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return [];
        }

        var result = new TerrainRouteModification[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            TerrainRouteModificationDto value = values[i];
            result[i] = new TerrainRouteModification(
                value.FromPointId,
                value.ToPointId,
                value.Blocked,
                value.Unlocked,
                value.CostMultiplier,
                value.State ?? string.Empty);
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
            throw new InvalidOperationException("terrain modification JSON is missing a vector value");
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
            throw new InvalidOperationException($"terrain modification JSON is missing enum {typeof(T).Name}");
        }

        bool isFlagsEnum = Attribute.IsDefined(typeof(T), typeof(FlagsAttribute));
        if (!isFlagsEnum && !Enum.IsDefined(typeof(T), value.Value))
        {
            throw new InvalidOperationException($"terrain modification JSON has unsupported {typeof(T).Name} value {value.Value}");
        }

        T parsed = (T)Enum.ToObject(typeof(T), value.Value);
        if (!string.Equals(parsed.ToString(), value.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"terrain modification JSON enum mismatch for {typeof(T).Name}: {value.Name}/{value.Value}");
        }

        return parsed;
    }

    private static string FileSystemPath(string path)
    {
        return path.Contains("://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(path)
            : Path.GetFullPath(path);
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

        Directory.CreateDirectory(normalized);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private sealed class TerrainModificationLayerDto
    {
        public string Contract { get; set; } = string.Empty;
        public int Version { get; set; }
        public TerrainHeightDeltaDto[] HeightDeltas { get; set; } = [];
        public TerrainSurfaceOverrideDto[] SurfaceOverrides { get; set; } = [];
        public TerrainScatterModificationDto[] ScatterModifications { get; set; } = [];
        public TerrainLandmarkModificationDto[] LandmarkModifications { get; set; } = [];
        public TerrainRouteModificationDto[] RouteModifications { get; set; } = [];
    }

    private sealed class TerrainHeightDeltaDto
    {
        public TerrainVector2Dto? World { get; set; }
        public float Radius { get; set; }
        public float Delta { get; set; }
        public float InnerRadius { get; set; }
    }

    private sealed class TerrainSurfaceOverrideDto
    {
        public TerrainVector2Dto? World { get; set; }
        public float Radius { get; set; }
        public TerrainEnumDto? Biome { get; set; }
        public TerrainEnumDto? Landscape { get; set; }
        public TerrainEnumDto? GameplayTags { get; set; }
        public float Traversability { get; set; }
        public float HazardPotential { get; set; }
    }

    private sealed class TerrainScatterModificationDto
    {
        public TerrainVector2Dto? World { get; set; }
        public float Radius { get; set; }
        public TerrainEnumDto? Kind { get; set; }
        public bool Remove { get; set; }
        public int StableId { get; set; }
        public string? State { get; set; }
    }

    private sealed class TerrainLandmarkModificationDto
    {
        public TerrainVector2Dto? World { get; set; }
        public float Radius { get; set; }
        public TerrainEnumDto? Kind { get; set; }
        public int StableId { get; set; }
        public string? State { get; set; }
    }

    private sealed class TerrainRouteModificationDto
    {
        public int FromPointId { get; set; }
        public int ToPointId { get; set; }
        public bool Blocked { get; set; }
        public bool Unlocked { get; set; }
        public float CostMultiplier { get; set; }
        public string? State { get; set; }
    }

    private sealed class TerrainVector2Dto
    {
        public float X { get; set; }
        public float Z { get; set; }
    }

    private sealed class TerrainEnumDto
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
