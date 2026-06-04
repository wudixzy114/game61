using System;
using System.Text.Json;
using Dao.Terrain;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Serializes open-world terrain plans to a stable JSON schema for audits, persistence handoff, and regression checks.</summary>
public static class TerrainWorldPlanSerializer
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

            if (!string.Equals(dto.ApiVersion, TerrainApiVersion.Version, StringComparison.Ordinal))
            {
                error = $"unsupported terrain API version '{dto.ApiVersion}', expected '{TerrainApiVersion.Version}'";
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

    private static TerrainPlanDto ToDto(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        return new TerrainPlanDto
        {
            Contract = Contract,
            ApiContract = TerrainApiVersion.Contract,
            ApiVersion = TerrainApiVersion.Version,
            GeneratorVersion = GeneratorVersion,
            Seed = profile.Seed,
            ProfileHash = profile.StableHash(),
            Center = ToDto(plan.Center),
            WorldSize = plan.WorldSize,
            GridResolution = plan.GridResolution,
            Regions = ToDtos(plan.Regions),
            PointsOfInterest = ToDtos(plan.PointsOfInterest),
            Routes = ToDtos(plan.Routes),
            Reports = new TerrainPlanReportsDto
            {
                Quality = ToDto(plan.QualityReport),
                Planning = ToDto(plan.PlanningReport),
                Experience = ToDto(plan.ExperienceReport)
            }
        };
    }

    private static TerrainWorldPlan FromDto(TerrainPlanDto dto)
    {
        if (dto.Center is null)
        {
            throw new InvalidOperationException("terrain plan JSON is missing center");
        }

        if (dto.Reports?.Quality is null || dto.Reports.Planning is null || dto.Reports.Experience is null)
        {
            throw new InvalidOperationException("terrain plan JSON is missing reports");
        }

        return new TerrainWorldPlan(
            FromDto(dto.Center),
            dto.WorldSize,
            dto.GridResolution,
            FromDtos(dto.Regions),
            FromDtos(dto.PointsOfInterest),
            FromDtos(dto.Routes),
            FromDto(dto.Reports.Quality),
            FromDto(dto.Reports.Planning),
            FromDto(dto.Reports.Experience));
    }

    private static TerrainRegionDto[] ToDtos(TerrainWorldRegion[] regions)
    {
        var values = new TerrainRegionDto[regions.Length];
        for (int i = 0; i < regions.Length; i++)
        {
            TerrainWorldRegion region = regions[i];
            values[i] = new TerrainRegionDto
            {
                GridX = region.GridX,
                GridY = region.GridY,
                World = ToDto(region.WorldPosition),
                Height = region.Height,
                River = region.River,
                ScenicPotential = region.ScenicPotential,
                Traversability = region.Traversability,
                Exposure = region.Exposure,
                ResourcePotential = region.ResourcePotential,
                HazardPotential = region.HazardPotential,
                EncounterPotential = region.EncounterPotential,
                Biome = ToDto(region.BiomeKind),
                Landscape = ToDto(region.LandscapeKind),
                Region = ToDto(region.RegionKind)
            };
        }

        return values;
    }

    private static TerrainWorldRegion[] FromDtos(TerrainRegionDto[]? regions)
    {
        if (regions is null || regions.Length == 0)
        {
            return [];
        }

        var values = new TerrainWorldRegion[regions.Length];
        for (int i = 0; i < regions.Length; i++)
        {
            TerrainRegionDto region = regions[i];
            values[i] = new TerrainWorldRegion(
                region.GridX,
                region.GridY,
                FromDto(region.World),
                region.Height,
                region.River,
                region.ScenicPotential,
                region.Traversability,
                region.Exposure,
                region.ResourcePotential,
                region.HazardPotential,
                region.EncounterPotential,
                EnumValue<TerrainBiomeKind>(region.Biome),
                EnumValue<TerrainLandscapeKind>(region.Landscape),
                EnumValue<TerrainWorldRegionKind>(region.Region));
        }

        return values;
    }

    private static TerrainPointDto[] ToDtos(TerrainWorldPointOfInterest[] points)
    {
        var values = new TerrainPointDto[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            TerrainWorldPointOfInterest point = points[i];
            values[i] = new TerrainPointDto
            {
                Id = point.Id,
                Kind = ToDto(point.Kind),
                World = ToDto(point.WorldPosition),
                GridX = point.GridX,
                GridY = point.GridY,
                Score = point.Score,
                Height = point.Height,
                ScenicPotential = point.ScenicPotential,
                Traversability = point.Traversability,
                Biome = ToDto(point.BiomeKind),
                Landscape = ToDto(point.LandscapeKind),
                SettlementTier = ToDto(point.SettlementTier),
                DebugName = point.DebugName
            };
        }

        return values;
    }

    private static TerrainWorldPointOfInterest[] FromDtos(TerrainPointDto[]? points)
    {
        if (points is null || points.Length == 0)
        {
            return [];
        }

        var values = new TerrainWorldPointOfInterest[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            TerrainPointDto point = points[i];
            values[i] = new TerrainWorldPointOfInterest(
                point.Id,
                EnumValue<TerrainPointOfInterestKind>(point.Kind),
                FromDto(point.World),
                point.GridX,
                point.GridY,
                point.Score,
                point.Height,
                point.ScenicPotential,
                point.Traversability,
                EnumValue<TerrainBiomeKind>(point.Biome),
                EnumValue<TerrainLandscapeKind>(point.Landscape),
                EnumValue<TerrainSettlementTier>(point.SettlementTier),
                point.DebugName ?? string.Empty);
        }

        return values;
    }

    private static TerrainRouteDto[] ToDtos(TerrainWorldRoute[] routes)
    {
        var values = new TerrainRouteDto[routes.Length];
        for (int i = 0; i < routes.Length; i++)
        {
            TerrainWorldRoute route = routes[i];
            values[i] = new TerrainRouteDto
            {
                FromPointId = route.FromPointId,
                ToPointId = route.ToPointId,
                Kind = ToDto(route.Kind),
                Cost = route.Cost,
                AverageScenicPotential = route.AverageScenicPotential,
                AverageTraversability = route.AverageTraversability,
                Waypoints = ToDtos(route.Waypoints)
            };
        }

        return values;
    }

    private static TerrainWorldRoute[] FromDtos(TerrainRouteDto[]? routes)
    {
        if (routes is null || routes.Length == 0)
        {
            return [];
        }

        var values = new TerrainWorldRoute[routes.Length];
        for (int i = 0; i < routes.Length; i++)
        {
            TerrainRouteDto route = routes[i];
            values[i] = new TerrainWorldRoute(
                route.FromPointId,
                route.ToPointId,
                EnumValue<TerrainRouteKind>(route.Kind),
                route.Cost,
                route.AverageScenicPotential,
                route.AverageTraversability,
                FromDtos(route.Waypoints));
        }

        return values;
    }

    private static TerrainQualityReportDto ToDto(TerrainQualityReport report)
    {
        return new TerrainQualityReportDto
        {
            SampleCount = report.SampleCount,
            WorldSize = report.WorldSize,
            MinHeight = report.MinHeight,
            MaxHeight = report.MaxHeight,
            AverageHeight = report.AverageHeight,
            LandRatio = report.LandRatio,
            OceanRatio = report.OceanRatio,
            CoastRatio = report.CoastRatio,
            RiverRatio = report.RiverRatio,
            ScenicRatio = report.ScenicRatio,
            TraversableLandRatio = report.TraversableLandRatio,
            DistinctLandscapeKinds = report.DistinctLandscapeKinds,
            DistinctBiomeKinds = report.DistinctBiomeKinds,
            OceanCount = report.OceanCount,
            CoastCount = report.CoastCount,
            LowlandCount = report.LowlandCount,
            WetlandCount = report.WetlandCount,
            ForestBasinCount = report.ForestBasinCount,
            RiverValleyCount = report.RiverValleyCount,
            CanyonCount = report.CanyonCount,
            HighlandsCount = report.HighlandsCount,
            MountainMassifCount = report.MountainMassifCount,
            SnowfieldCount = report.SnowfieldCount,
            VistaPlateauCount = report.VistaPlateauCount,
            LakeCount = report.LakeCount,
            BiomeOceanCount = report.BiomeOceanCount,
            BiomeCoastCount = report.BiomeCoastCount,
            IslandCount = report.IslandCount,
            PlainsCount = report.PlainsCount,
            GrasslandCount = report.GrasslandCount,
            DesertCount = report.DesertCount,
            OasisCount = report.OasisCount,
            ForestCount = report.ForestCount,
            BiomeWetlandCount = report.BiomeWetlandCount,
            HillsCount = report.HillsCount,
            MountainsCount = report.MountainsCount,
            BiomeSnowfieldCount = report.BiomeSnowfieldCount,
            BiomeLakeCount = report.BiomeLakeCount
        };
    }

    private static TerrainQualityReport FromDto(TerrainQualityReportDto report)
    {
        return new TerrainQualityReport(
            report.SampleCount,
            report.WorldSize,
            report.MinHeight,
            report.MaxHeight,
            report.AverageHeight,
            report.LandRatio,
            report.OceanRatio,
            report.CoastRatio,
            report.RiverRatio,
            report.ScenicRatio,
            report.TraversableLandRatio,
            report.DistinctLandscapeKinds,
            report.DistinctBiomeKinds,
            report.OceanCount,
            report.CoastCount,
            report.LowlandCount,
            report.WetlandCount,
            report.ForestBasinCount,
            report.RiverValleyCount,
            report.CanyonCount,
            report.HighlandsCount,
            report.MountainMassifCount,
            report.SnowfieldCount,
            report.VistaPlateauCount,
            report.LakeCount,
            report.BiomeOceanCount,
            report.BiomeCoastCount,
            report.IslandCount,
            report.PlainsCount,
            report.GrasslandCount,
            report.DesertCount,
            report.OasisCount,
            report.ForestCount,
            report.BiomeWetlandCount,
            report.HillsCount,
            report.MountainsCount,
            report.BiomeSnowfieldCount,
            report.BiomeLakeCount);
    }

    private static TerrainPlanningReportDto ToDto(TerrainWorldPlanningReport report)
    {
        return new TerrainPlanningReportDto
        {
            PointOfInterestCount = report.PointOfInterestCount,
            DistinctPointOfInterestKinds = report.DistinctPointOfInterestKinds,
            RouteCount = report.RouteCount,
            DistinctRouteKinds = report.DistinctRouteKinds,
            ConnectedPointRatio = report.ConnectedPointRatio,
            ConnectedSettlementRatio = report.ConnectedSettlementRatio,
            SettlementRouteCount = report.SettlementRouteCount,
            PointOfInterestWorldCoverage = report.PointOfInterestWorldCoverage,
            RouteWorldCoverage = report.RouteWorldCoverage,
            AveragePointScore = report.AveragePointScore,
            AverageRouteCost = report.AverageRouteCost,
            AverageRouteScenicPotential = report.AverageRouteScenicPotential,
            AverageRouteTraversability = report.AverageRouteTraversability,
            SettlementCandidateCount = report.SettlementCandidateCount,
            VistaCount = report.VistaCount,
            RiverCrossingCount = report.RiverCrossingCount,
            MountainPassCount = report.MountainPassCount,
            CoastalLandingCount = report.CoastalLandingCount,
            ResourceGroveCount = report.ResourceGroveCount,
            AncientSiteCount = report.AncientSiteCount,
            CanyonOverlookCount = report.CanyonOverlookCount,
            OasisCount = report.OasisCount,
            VillageCount = report.VillageCount,
            TownCount = report.TownCount,
            OasisHubCount = report.OasisHubCount,
            PrimaryTrailCount = report.PrimaryTrailCount,
            RiverRoadCount = report.RiverRoadCount,
            RidgePassCount = report.RidgePassCount,
            CoastalPathCount = report.CoastalPathCount,
            ScenicTrailCount = report.ScenicTrailCount
        };
    }

    private static TerrainWorldPlanningReport FromDto(TerrainPlanningReportDto report)
    {
        return new TerrainWorldPlanningReport(
            report.PointOfInterestCount,
            report.DistinctPointOfInterestKinds,
            report.RouteCount,
            report.DistinctRouteKinds,
            report.ConnectedPointRatio,
            report.ConnectedSettlementRatio,
            report.SettlementRouteCount,
            report.PointOfInterestWorldCoverage,
            report.RouteWorldCoverage,
            report.AveragePointScore,
            report.AverageRouteCost,
            report.AverageRouteScenicPotential,
            report.AverageRouteTraversability,
            report.SettlementCandidateCount,
            report.VistaCount,
            report.RiverCrossingCount,
            report.MountainPassCount,
            report.CoastalLandingCount,
            report.ResourceGroveCount,
            report.AncientSiteCount,
            report.CanyonOverlookCount,
            report.OasisCount,
            report.VillageCount,
            report.TownCount,
            report.OasisHubCount,
            report.PrimaryTrailCount,
            report.RiverRoadCount,
            report.RidgePassCount,
            report.CoastalPathCount,
            report.ScenicTrailCount);
    }

    private static TerrainExperienceReportDto ToDto(TerrainExperienceReport report)
    {
        return new TerrainExperienceReportDto
        {
            RegionCount = report.RegionCount,
            EncounterRichRegionRatio = report.EncounterRichRegionRatio,
            ResourceRichRegionRatio = report.ResourceRichRegionRatio,
            HazardRichRegionRatio = report.HazardRichRegionRatio,
            AverageExposure = report.AverageExposure,
            AverageResourcePotential = report.AverageResourcePotential,
            AverageHazardPotential = report.AverageHazardPotential,
            AverageEncounterPotential = report.AverageEncounterPotential,
            RouteRhythmScore = report.RouteRhythmScore,
            PointOfInterestValue = report.PointOfInterestValue,
            RiskRewardBalance = report.RiskRewardBalance,
            ScenicAnchorRatio = report.ScenicAnchorRatio
        };
    }

    private static TerrainExperienceReport FromDto(TerrainExperienceReportDto report)
    {
        return new TerrainExperienceReport(
            report.RegionCount,
            report.EncounterRichRegionRatio,
            report.ResourceRichRegionRatio,
            report.HazardRichRegionRatio,
            report.AverageExposure,
            report.AverageResourcePotential,
            report.AverageHazardPotential,
            report.AverageEncounterPotential,
            report.RouteRhythmScore,
            report.PointOfInterestValue,
            report.RiskRewardBalance,
            report.ScenicAnchorRatio);
    }

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

    private sealed class TerrainPlanDto
    {
        public string Contract { get; set; } = string.Empty;
        public string ApiContract { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = string.Empty;
        public string GeneratorVersion { get; set; } = string.Empty;
        public int Seed { get; set; }
        public string ProfileHash { get; set; } = string.Empty;
        public TerrainVector2Dto? Center { get; set; }
        public float WorldSize { get; set; }
        public int GridResolution { get; set; }
        public TerrainRegionDto[] Regions { get; set; } = [];
        public TerrainPointDto[] PointsOfInterest { get; set; } = [];
        public TerrainRouteDto[] Routes { get; set; } = [];
        public TerrainPlanReportsDto? Reports { get; set; }
    }

    private sealed class TerrainPlanReportsDto
    {
        public TerrainQualityReportDto? Quality { get; set; }
        public TerrainPlanningReportDto? Planning { get; set; }
        public TerrainExperienceReportDto? Experience { get; set; }
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

    private sealed class TerrainRegionDto
    {
        public int GridX { get; set; }
        public int GridY { get; set; }
        public TerrainVector2Dto? World { get; set; }
        public float Height { get; set; }
        public float River { get; set; }
        public float ScenicPotential { get; set; }
        public float Traversability { get; set; }
        public float Exposure { get; set; }
        public float ResourcePotential { get; set; }
        public float HazardPotential { get; set; }
        public float EncounterPotential { get; set; }
        public TerrainEnumDto? Biome { get; set; }
        public TerrainEnumDto? Landscape { get; set; }
        public TerrainEnumDto? Region { get; set; }
    }

    private sealed class TerrainPointDto
    {
        public int Id { get; set; }
        public TerrainEnumDto? Kind { get; set; }
        public TerrainVector2Dto? World { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public float Score { get; set; }
        public float Height { get; set; }
        public float ScenicPotential { get; set; }
        public float Traversability { get; set; }
        public TerrainEnumDto? Biome { get; set; }
        public TerrainEnumDto? Landscape { get; set; }
        public TerrainEnumDto? SettlementTier { get; set; }
        public string? DebugName { get; set; }
    }

    private sealed class TerrainRouteDto
    {
        public int FromPointId { get; set; }
        public int ToPointId { get; set; }
        public TerrainEnumDto? Kind { get; set; }
        public float Cost { get; set; }
        public float AverageScenicPotential { get; set; }
        public float AverageTraversability { get; set; }
        public TerrainVector2Dto[] Waypoints { get; set; } = [];
    }

    private sealed class TerrainQualityReportDto
    {
        public int SampleCount { get; set; }
        public float WorldSize { get; set; }
        public float MinHeight { get; set; }
        public float MaxHeight { get; set; }
        public float AverageHeight { get; set; }
        public float LandRatio { get; set; }
        public float OceanRatio { get; set; }
        public float CoastRatio { get; set; }
        public float RiverRatio { get; set; }
        public float ScenicRatio { get; set; }
        public float TraversableLandRatio { get; set; }
        public int DistinctLandscapeKinds { get; set; }
        public int DistinctBiomeKinds { get; set; }
        public int OceanCount { get; set; }
        public int CoastCount { get; set; }
        public int LowlandCount { get; set; }
        public int WetlandCount { get; set; }
        public int ForestBasinCount { get; set; }
        public int RiverValleyCount { get; set; }
        public int CanyonCount { get; set; }
        public int HighlandsCount { get; set; }
        public int MountainMassifCount { get; set; }
        public int SnowfieldCount { get; set; }
        public int VistaPlateauCount { get; set; }
        public int LakeCount { get; set; }
        public int BiomeOceanCount { get; set; }
        public int BiomeCoastCount { get; set; }
        public int IslandCount { get; set; }
        public int PlainsCount { get; set; }
        public int GrasslandCount { get; set; }
        public int DesertCount { get; set; }
        public int OasisCount { get; set; }
        public int ForestCount { get; set; }
        public int BiomeWetlandCount { get; set; }
        public int HillsCount { get; set; }
        public int MountainsCount { get; set; }
        public int BiomeSnowfieldCount { get; set; }
        public int BiomeLakeCount { get; set; }
    }

    private sealed class TerrainPlanningReportDto
    {
        public int PointOfInterestCount { get; set; }
        public int DistinctPointOfInterestKinds { get; set; }
        public int RouteCount { get; set; }
        public int DistinctRouteKinds { get; set; }
        public float ConnectedPointRatio { get; set; }
        public float ConnectedSettlementRatio { get; set; }
        public int SettlementRouteCount { get; set; }
        public float PointOfInterestWorldCoverage { get; set; }
        public float RouteWorldCoverage { get; set; }
        public float AveragePointScore { get; set; }
        public float AverageRouteCost { get; set; }
        public float AverageRouteScenicPotential { get; set; }
        public float AverageRouteTraversability { get; set; }
        public int SettlementCandidateCount { get; set; }
        public int VistaCount { get; set; }
        public int RiverCrossingCount { get; set; }
        public int MountainPassCount { get; set; }
        public int CoastalLandingCount { get; set; }
        public int ResourceGroveCount { get; set; }
        public int AncientSiteCount { get; set; }
        public int CanyonOverlookCount { get; set; }
        public int OasisCount { get; set; }
        public int VillageCount { get; set; }
        public int TownCount { get; set; }
        public int OasisHubCount { get; set; }
        public int PrimaryTrailCount { get; set; }
        public int RiverRoadCount { get; set; }
        public int RidgePassCount { get; set; }
        public int CoastalPathCount { get; set; }
        public int ScenicTrailCount { get; set; }
    }

    private sealed class TerrainExperienceReportDto
    {
        public int RegionCount { get; set; }
        public float EncounterRichRegionRatio { get; set; }
        public float ResourceRichRegionRatio { get; set; }
        public float HazardRichRegionRatio { get; set; }
        public float AverageExposure { get; set; }
        public float AverageResourcePotential { get; set; }
        public float AverageHazardPotential { get; set; }
        public float AverageEncounterPotential { get; set; }
        public float RouteRhythmScore { get; set; }
        public float PointOfInterestValue { get; set; }
        public float RiskRewardBalance { get; set; }
        public float ScenicAnchorRatio { get; set; }
    }
}
