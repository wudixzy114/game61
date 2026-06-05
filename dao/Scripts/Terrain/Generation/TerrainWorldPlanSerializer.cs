using System;
using System.Text.Json;
using Dao.Terrain;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Serializes open-world terrain plans to a stable JSON schema for audits, persistence handoff, and regression checks.</summary>
public static partial class TerrainWorldPlanSerializer
{
    public const string Contract = "terrain-plan-v1";
    public const string GeneratorVersion = "1.0.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string ToJson(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        TerrainPlanDto dto = ToDto(plan, profile);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static bool TryFromJson(string json, out TerrainWorldPlan? plan, out string error)
    {
        plan = null;
        error = string.Empty;

        try
        {
            TerrainPlanDto? dto = JsonSerializer.Deserialize<TerrainPlanDto>(json, JsonOptions);
            if (dto is null)
            {
                error = "terrain plan JSON was empty or invalid";
                return false;
            }

            if (!string.Equals(dto.Contract, Contract, StringComparison.Ordinal))
            {
                error = $"unsupported terrain plan contract '{dto.Contract}', expected '{Contract}'";
                return false;
            }

            if (!string.Equals(dto.ApiContract, TerrainApiVersion.Contract, StringComparison.Ordinal))
            {
                error = $"unsupported terrain API contract '{dto.ApiContract}', expected '{TerrainApiVersion.Contract}'";
                return false;
            }

            if (!TerrainApiVersion.IsSupportedPlanApiVersion(dto.ApiVersion))
            {
                error = $"unsupported terrain API version '{dto.ApiVersion}', expected '{TerrainApiVersion.Version}' or compatible terrain-api-v1 plan version";
                return false;
            }

            if (!string.Equals(dto.GeneratorVersion, GeneratorVersion, StringComparison.Ordinal))
            {
                error = $"unsupported terrain generator version '{dto.GeneratorVersion}', expected '{GeneratorVersion}'";
                return false;
            }

            plan = FromDto(dto);
            return true;
        }
        catch (Exception exception)
        {
            error = $"failed to parse terrain plan JSON: {exception.Message}";
            return false;
        }
    }

    public static bool TryFromJson(
        string json,
        TerrainGenerationProfile expectedProfile,
        out TerrainWorldPlan? plan,
        out string error)
    {
        if (!TryFromJson(json, out plan, out error))
        {
            return false;
        }

        try
        {
            TerrainPlanDto? dto = JsonSerializer.Deserialize<TerrainPlanDto>(json, JsonOptions);
            if (dto is null)
            {
                plan = null;
                error = "terrain plan JSON was empty or invalid";
                return false;
            }

            if (dto.Seed != expectedProfile.Seed)
            {
                plan = null;
                error = $"terrain plan seed '{dto.Seed}' did not match expected seed '{expectedProfile.Seed}'";
                return false;
            }

            string expectedProfileHash = expectedProfile.StableHash();
            if (!string.Equals(dto.ProfileHash, expectedProfileHash, StringComparison.Ordinal))
            {
                plan = null;
                error = $"terrain plan profile hash '{dto.ProfileHash}' did not match expected hash '{expectedProfileHash}'";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            plan = null;
            error = $"failed to validate terrain plan profile metadata: {exception.Message}";
            return false;
        }
    }

    public static Error SaveJson(TerrainWorldPlan plan, TerrainGenerationProfile profile, string outputPath)
    {
        try
        {
            EnsureDirectoryForPath(outputPath);
            string path = FileSystemPath(outputPath);
            System.IO.File.WriteAllText(path, ToJson(plan, profile));
            return Error.Ok;
        }
        catch (Exception exception)
        {
            GD.PushError($"Failed to save terrain plan JSON '{outputPath}': {exception.Message}");
            return Error.FileCantWrite;
        }
    }

    public static bool TryLoadJson(string inputPath, out TerrainWorldPlan? plan, out string error)
    {
        plan = null;
        error = string.Empty;

        try
        {
            string path = FileSystemPath(inputPath);
            if (!System.IO.File.Exists(path))
            {
                error = $"terrain plan JSON file does not exist: {inputPath}";
                return false;
            }

            return TryFromJson(System.IO.File.ReadAllText(path), out plan, out error);
        }
        catch (Exception exception)
        {
            error = $"failed to load terrain plan JSON '{inputPath}': {exception.Message}";
            return false;
        }
    }

    public static bool TryLoadJson(
        string inputPath,
        TerrainGenerationProfile expectedProfile,
        out TerrainWorldPlan? plan,
        out string error)
    {
        plan = null;
        error = string.Empty;

        try
        {
            string path = FileSystemPath(inputPath);
            if (!System.IO.File.Exists(path))
            {
                error = $"terrain plan JSON file does not exist: {inputPath}";
                return false;
            }

            return TryFromJson(System.IO.File.ReadAllText(path), expectedProfile, out plan, out error);
        }
        catch (Exception exception)
        {
            error = $"failed to load terrain plan JSON '{inputPath}': {exception.Message}";
            return false;
        }
    }
}
