using System;
using System.Reflection;
using System.Text.Json.Nodes;
using Dao.Terrain;
using Dao.Terrain.Generation;
using Dao.Terrain.Streaming;
using Godot;
using static TerrainValidationRuntimeProbeHelpers;

internal static class TerrainValidationPlanJsonChecks
{
    internal static TerrainPlanJsonSmokeReport ValidateTerrainPlanJsonRoundtrip(
        TerrainGenerationProfile profile,
        TerrainWorldPlan plan)
    {
        try
        {
            string json = TerrainWorldPlanSerializer.ToJson(plan, profile);
            JsonObject? root = JsonNode.Parse(json) as JsonObject;
            bool metadataPassed = root is not null && PlanJsonMetadataMatches(root, profile, plan);
            bool schemaShapePassed = root is not null && PlanJsonSchemaShapeMatches(root, plan);

            bool stringLoadPassed = TerrainWorldPlanSerializer.TryFromJson(
                json,
                profile,
                out TerrainWorldPlan? loadedPlan,
                out string stringLoadError);
            bool stringRoundtripMatches = stringLoadPassed &&
                loadedPlan is not null &&
                TerrainPlansMatchForJson(plan, loadedPlan);
            bool roundtripIsolationPassed = loadedPlan is not null && RoundtripPlanIsolated(plan, loadedPlan);
            bool setWorldPlanPassed = loadedPlan is not null && RoundtripPlanCanBeAssignedToRuntimeWorld(profile, loadedPlan);

            string outputPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "dao_terrain_validation",
                $"seed_{profile.Seed}",
                "terrain_plan_roundtrip.json");
            Error saveError = TerrainWorldPlanSerializer.SaveJson(plan, profile, outputPath);
            string filePath = TerrainValidationCliHelpers.FileSystemPath(outputPath);
            bool fileExists = System.IO.File.Exists(filePath);
            long fileBytes = fileExists ? new System.IO.FileInfo(filePath).Length : 0L;
            TerrainWorldPlan? filePlan = null;
            string fileLoadError = string.Empty;
            bool fileLoadPassed =
                saveError == Error.Ok &&
                fileExists &&
                fileBytes >= json.Length;
            if (fileLoadPassed)
            {
                fileLoadPassed = TerrainWorldPlanSerializer.TryLoadJson(
                    outputPath,
                    profile,
                    out filePlan,
                    out fileLoadError);
            }

            bool fileRoundtripMatches = fileLoadPassed &&
                filePlan is not null &&
                TerrainPlansMatchForJson(plan, filePlan);

            bool seedMismatchRejected = !TerrainWorldPlanSerializer.TryFromJson(
                json,
                profile with { Seed = profile.Seed + 1 },
                out _,
                out _);
            bool profileHashMismatchRejected = !TerrainWorldPlanSerializer.TryFromJson(
                json,
                profile with { ChunkSize = profile.ChunkSize + 1.0f },
                out _,
                out _);
            bool legacyApiVersionAccepted = AcceptsCompatibleApiVersion(json, profile, "1.0.0");
            bool previousApiVersionAccepted = AcceptsCompatibleApiVersion(json, profile, "1.1.0");
            bool currentApiMinusThreeVersionAccepted = AcceptsCompatibleApiVersion(json, profile, "1.2.0");
            bool currentApiMinusTwoVersionAccepted = AcceptsCompatibleApiVersion(json, profile, "1.3.0");
            bool currentApiMinusOneVersionAccepted = AcceptsCompatibleApiVersion(json, profile, "1.4.0");
            bool versionDriftRejected = RejectsVersionDrift(json, profile);
            bool enumNameDriftRejected = RejectsEnumNameDrift(json, profile);
            bool enumValueDriftRejected = RejectsEnumValueDrift(json, profile);

            bool passed =
                metadataPassed &&
                schemaShapePassed &&
                stringLoadPassed &&
                stringRoundtripMatches &&
                roundtripIsolationPassed &&
                setWorldPlanPassed &&
                fileLoadPassed &&
                fileRoundtripMatches &&
                seedMismatchRejected &&
                profileHashMismatchRejected &&
                legacyApiVersionAccepted &&
                previousApiVersionAccepted &&
                currentApiMinusThreeVersionAccepted &&
                currentApiMinusTwoVersionAccepted &&
                currentApiMinusOneVersionAccepted &&
                versionDriftRejected &&
                enumNameDriftRejected &&
                enumValueDriftRejected;
            string reason = passed
                ? "plan JSON schema roundtrips through string and file persistence with version/profile/enum drift checks"
                : PlanJsonFailureReason(
                    metadataPassed,
                    schemaShapePassed,
                    stringLoadPassed,
                    stringRoundtripMatches,
                    roundtripIsolationPassed,
                    setWorldPlanPassed,
                    fileLoadPassed,
                    fileRoundtripMatches,
                    seedMismatchRejected,
                    profileHashMismatchRejected,
                    legacyApiVersionAccepted,
                    previousApiVersionAccepted,
                    currentApiMinusThreeVersionAccepted,
                    currentApiMinusTwoVersionAccepted,
                    currentApiMinusOneVersionAccepted,
                    versionDriftRejected,
                    enumNameDriftRejected,
                    enumValueDriftRejected,
                    saveError,
                    stringLoadError,
                    fileLoadError);

            return new TerrainPlanJsonSmokeReport(
                passed,
                metadataPassed,
                schemaShapePassed,
                stringLoadPassed,
                stringRoundtripMatches,
                fileLoadPassed,
                fileRoundtripMatches,
                seedMismatchRejected,
                profileHashMismatchRejected,
                legacyApiVersionAccepted,
                previousApiVersionAccepted,
                currentApiMinusThreeVersionAccepted,
                currentApiMinusTwoVersionAccepted,
                currentApiMinusOneVersionAccepted,
                versionDriftRejected,
                enumNameDriftRejected,
                enumValueDriftRejected,
                roundtripIsolationPassed,
                setWorldPlanPassed,
                json.Length,
                fileBytes,
                reason);
        }
        catch (Exception ex)
        {
            return new TerrainPlanJsonSmokeReport(
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                0,
                0,
                $"plan JSON smoke threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool PlanJsonMetadataMatches(JsonObject root, TerrainGenerationProfile profile, TerrainWorldPlan plan)
    {
        return JsonStringEquals(root, "contract", TerrainWorldPlanSerializer.Contract) &&
            JsonStringEquals(root, "apiContract", TerrainApiVersion.Contract) &&
            JsonStringEquals(root, "apiVersion", TerrainApiVersion.Version) &&
            JsonStringEquals(root, "generatorVersion", TerrainWorldPlanSerializer.GeneratorVersion) &&
            JsonIntEquals(root, "seed", profile.Seed) &&
            JsonStringEquals(root, "profileHash", profile.StableHash()) &&
            JsonStringEquals(root, "scatterRuleSetHash", NormalizeScatterRuleSetHash(profile.ScatterRuleSetHash)) &&
            JsonStringEquals(root, "settlementVisualRuleSetHash", NormalizeSettlementVisualRuleSetHash(profile.SettlementVisualRuleSetHash)) &&
            JsonStringEquals(root, "pointOfInterestRuleSetHash", NormalizePointOfInterestRuleSetHash(profile.PointOfInterestRuleSetHash)) &&
            JsonStringEquals(root, "routeRuleSetHash", NormalizeRouteRuleSetHash(profile.RouteRuleSetHash)) &&
            JsonStringEquals(root, "scenicLandmarkRuleSetHash", NormalizeScenicRuleSetHash(profile.ScenicLandmarkRuleSetHash)) &&
            JsonArrayCount(root, "regions") == plan.Regions.Length &&
            JsonArrayCount(root, "pointsOfInterest") == plan.PointsOfInterest.Length &&
            JsonArrayCount(root, "routes") == plan.Routes.Length &&
            root["center"] is JsonObject &&
            root["reports"]?["quality"] is JsonObject &&
            root["reports"]?["planning"] is JsonObject &&
            root["reports"]?["experience"] is JsonObject &&
            HasEnumNode(root, "regions", "biome") &&
            HasEnumNode(root, "regions", "landscape") &&
            HasEnumNode(root, "regions", "region") &&
            HasEnumNode(root, "pointsOfInterest", "kind") &&
            HasEnumNode(root, "routes", "kind");
    }

    private static bool PlanJsonSchemaShapeMatches(JsonObject root, TerrainWorldPlan plan)
    {
        if (!VectorNodeUsesXz(root["center"] as JsonObject))
        {
            return false;
        }

        JsonObject? firstRegionWorld = FirstObjectProperty(root, "regions", "world");
        if (plan.Regions.Length > 0 && !VectorNodeUsesXz(firstRegionWorld))
        {
            return false;
        }

        JsonObject? firstPointWorld = FirstObjectProperty(root, "pointsOfInterest", "world");
        if (plan.PointsOfInterest.Length > 0 && !VectorNodeUsesXz(firstPointWorld))
        {
            return false;
        }

        JsonObject? firstRoute = FirstArrayObject(root, "routes");
        if (plan.Routes.Length == 0)
        {
            return firstRoute is null;
        }

        if (firstRoute is null || firstRoute["waypoints"] is not JsonArray waypoints)
        {
            return false;
        }

        if (plan.Routes[0].Waypoints.Length == 0)
        {
            return waypoints.Count == 0;
        }

        return waypoints.Count == plan.Routes[0].Waypoints.Length &&
            VectorNodeUsesXz(waypoints[0] as JsonObject);
    }

    private static bool VectorNodeUsesXz(JsonObject? node)
    {
        return node is not null &&
            node["x"] is not null &&
            node["z"] is not null &&
            node["y"] is null;
    }

    private static bool JsonStringEquals(JsonObject root, string propertyName, string expected)
    {
        return root.TryGetPropertyValue(propertyName, out JsonNode? node) &&
            node is not null &&
            string.Equals(node.GetValue<string>(), expected, StringComparison.Ordinal);
    }

    private static bool JsonIntEquals(JsonObject root, string propertyName, int expected)
    {
        return root.TryGetPropertyValue(propertyName, out JsonNode? node) &&
            node is not null &&
            node.GetValue<int>() == expected;
    }

    private static int JsonArrayCount(JsonObject root, string propertyName)
    {
        return root.TryGetPropertyValue(propertyName, out JsonNode? node) && node is JsonArray array
            ? array.Count
            : -1;
    }

    private static bool HasEnumNode(JsonObject root, string arrayName, string propertyName)
    {
        JsonObject? enumNode = FirstObjectProperty(root, arrayName, propertyName);
        return enumNode is not null &&
            enumNode["name"] is not null &&
            enumNode["value"] is not null;
    }

    private static JsonObject? FirstObjectProperty(JsonObject root, string arrayName, string propertyName)
    {
        JsonObject? firstObject = FirstArrayObject(root, arrayName);
        return firstObject?[propertyName] as JsonObject;
    }

    private static JsonObject? FirstArrayObject(JsonObject root, string arrayName)
    {
        if (root[arrayName] is not JsonArray array || array.Count == 0)
        {
            return null;
        }

        return array[0] as JsonObject;
    }

    private static bool RejectsEnumNameDrift(string json, TerrainGenerationProfile profile)
    {
        JsonObject? root = JsonNode.Parse(json) as JsonObject;
        JsonObject? enumNode = root is null ? null : FirstObjectProperty(root, "regions", "biome");
        if (root is null || enumNode is null)
        {
            return false;
        }

        enumNode["name"] = "__invalid_enum_name__";
        return !TerrainWorldPlanSerializer.TryFromJson(root.ToJsonString(), profile, out _, out _);
    }

    private static bool RejectsVersionDrift(string json, TerrainGenerationProfile profile)
    {
        return RejectsStringPropertyDrift(json, profile, "contract", "__terrain_plan_v2__") &&
            RejectsStringPropertyDrift(json, profile, "apiContract", "__terrain_api_v2__") &&
            RejectsStringPropertyDrift(json, profile, "apiVersion", "99.0.0") &&
            RejectsStringPropertyDrift(json, profile, "generatorVersion", "99.0.0");
    }

    private static bool AcceptsCompatibleApiVersion(
        string json,
        TerrainGenerationProfile profile,
        string apiVersion)
    {
        JsonObject? root = JsonNode.Parse(json) as JsonObject;
        if (root is null || root["apiVersion"] is null)
        {
            return false;
        }

        root["apiVersion"] = apiVersion;
        return TerrainWorldPlanSerializer.TryFromJson(root.ToJsonString(), profile, out TerrainWorldPlan? plan, out _) &&
            plan is not null;
    }

    private static bool RejectsStringPropertyDrift(
        string json,
        TerrainGenerationProfile profile,
        string propertyName,
        string invalidValue)
    {
        JsonObject? root = JsonNode.Parse(json) as JsonObject;
        if (root is null || root[propertyName] is null)
        {
            return false;
        }

        root[propertyName] = invalidValue;
        return !TerrainWorldPlanSerializer.TryFromJson(root.ToJsonString(), profile, out _, out _);
    }

    private static bool RejectsEnumValueDrift(string json, TerrainGenerationProfile profile)
    {
        JsonObject? root = JsonNode.Parse(json) as JsonObject;
        JsonObject? enumNode = root is null ? null : FirstObjectProperty(root, "pointsOfInterest", "kind");
        if (root is null || enumNode is null)
        {
            return false;
        }

        enumNode["value"] = 9999;
        return !TerrainWorldPlanSerializer.TryFromJson(root.ToJsonString(), profile, out _, out _);
    }

    private static bool TerrainPlansMatchForJson(TerrainWorldPlan expected, TerrainWorldPlan actual)
    {
        if (!ExactPositionEquals(expected.Center, actual.Center) ||
            !PlanFloatEquals(expected.WorldSize, actual.WorldSize) ||
            expected.GridResolution != actual.GridResolution ||
            expected.Regions.Length != actual.Regions.Length ||
            expected.PointsOfInterest.Length != actual.PointsOfInterest.Length ||
            expected.Routes.Length != actual.Routes.Length ||
            !PublicValuePropertiesMatch(expected.QualityReport, actual.QualityReport) ||
            !PublicValuePropertiesMatch(expected.PlanningReport, actual.PlanningReport) ||
            !PublicValuePropertiesMatch(expected.ExperienceReport, actual.ExperienceReport))
        {
            return false;
        }

        for (int i = 0; i < expected.Regions.Length; i++)
        {
            if (!RegionsMatchForJson(expected.Regions[i], actual.Regions[i]))
            {
                return false;
            }
        }

        for (int i = 0; i < expected.PointsOfInterest.Length; i++)
        {
            if (!PointsMatchForJson(expected.PointsOfInterest[i], actual.PointsOfInterest[i]))
            {
                return false;
            }
        }

        for (int i = 0; i < expected.Routes.Length; i++)
        {
            if (!RoutesMatchForJson(expected.Routes[i], actual.Routes[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RegionsMatchForJson(TerrainWorldRegion expected, TerrainWorldRegion actual)
    {
        return expected.GridX == actual.GridX &&
            expected.GridY == actual.GridY &&
            ExactPositionEquals(expected.WorldPosition, actual.WorldPosition) &&
            PlanFloatEquals(expected.Height, actual.Height) &&
            PlanFloatEquals(expected.River, actual.River) &&
            PlanFloatEquals(expected.ScenicPotential, actual.ScenicPotential) &&
            PlanFloatEquals(expected.Traversability, actual.Traversability) &&
            PlanFloatEquals(expected.Exposure, actual.Exposure) &&
            PlanFloatEquals(expected.ResourcePotential, actual.ResourcePotential) &&
            PlanFloatEquals(expected.HazardPotential, actual.HazardPotential) &&
            PlanFloatEquals(expected.EncounterPotential, actual.EncounterPotential) &&
            expected.BiomeKind == actual.BiomeKind &&
            expected.LandscapeKind == actual.LandscapeKind &&
            expected.RegionKind == actual.RegionKind;
    }

    private static bool PointsMatchForJson(TerrainWorldPointOfInterest expected, TerrainWorldPointOfInterest actual)
    {
        return expected.Id == actual.Id &&
            expected.Kind == actual.Kind &&
            ExactPositionEquals(expected.WorldPosition, actual.WorldPosition) &&
            expected.GridX == actual.GridX &&
            expected.GridY == actual.GridY &&
            PlanFloatEquals(expected.Score, actual.Score) &&
            PlanFloatEquals(expected.Height, actual.Height) &&
            PlanFloatEquals(expected.ScenicPotential, actual.ScenicPotential) &&
            PlanFloatEquals(expected.Traversability, actual.Traversability) &&
            expected.BiomeKind == actual.BiomeKind &&
            expected.LandscapeKind == actual.LandscapeKind &&
            expected.SettlementTier == actual.SettlementTier &&
            string.Equals(expected.DebugName, actual.DebugName, StringComparison.Ordinal);
    }

    private static bool RoutesMatchForJson(TerrainWorldRoute expected, TerrainWorldRoute actual)
    {
        if (expected.FromPointId != actual.FromPointId ||
            expected.ToPointId != actual.ToPointId ||
            expected.Kind != actual.Kind ||
            !PlanFloatEquals(expected.Cost, actual.Cost) ||
            !PlanFloatEquals(expected.AverageScenicPotential, actual.AverageScenicPotential) ||
            !PlanFloatEquals(expected.AverageTraversability, actual.AverageTraversability) ||
            expected.Waypoints.Length != actual.Waypoints.Length)
        {
            return false;
        }

        for (int i = 0; i < expected.Waypoints.Length; i++)
        {
            if (!ExactPositionEquals(expected.Waypoints[i], actual.Waypoints[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PublicValuePropertiesMatch<T>(T expected, T actual)
    {
        foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            object? expectedValue = property.GetValue(expected);
            object? actualValue = property.GetValue(actual);
            if (expectedValue is float expectedFloat && actualValue is float actualFloat)
            {
                if (!PlanFloatEquals(expectedFloat, actualFloat))
                {
                    return false;
                }
            }
            else if (!Equals(expectedValue, actualValue))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RoundtripPlanIsolated(TerrainWorldPlan original, TerrainWorldPlan roundtrip)
    {
        bool isolated = true;
        if (original.Regions.Length > 0 && roundtrip.Regions.Length > 0)
        {
            TerrainWorldRegion originalRegion = original.Regions[0];
            roundtrip.Regions[0] = originalRegion with { Height = originalRegion.Height + 1000.0f };
            isolated = PlanFloatEquals(original.Regions[0].Height, originalRegion.Height);
        }

        if (isolated && original.PointsOfInterest.Length > 0 && roundtrip.PointsOfInterest.Length > 0)
        {
            TerrainWorldPointOfInterest originalPoint = original.PointsOfInterest[0];
            roundtrip.PointsOfInterest[0] = originalPoint with { Id = originalPoint.Id + 1000 };
            isolated = original.PointsOfInterest[0].Id == originalPoint.Id;
        }

        if (isolated && original.Routes.Length > 0 && roundtrip.Routes.Length > 0)
        {
            TerrainWorldRoute originalRoute = original.Routes[0];
            TerrainWorldRoute roundtripRoute = roundtrip.Routes[0];
            roundtrip.Routes[0] = roundtripRoute with { FromPointId = roundtripRoute.FromPointId + 1000 };
            isolated = original.Routes[0].FromPointId == originalRoute.FromPointId;

            if (isolated && originalRoute.Waypoints.Length > 0 && roundtrip.Routes[0].Waypoints.Length > 0)
            {
                Vector2 originalWaypoint = originalRoute.Waypoints[0];
                roundtrip.Routes[0].Waypoints[0] = originalWaypoint + new Vector2(1000.0f, -1000.0f);
                isolated = ExactPositionEquals(original.Routes[0].Waypoints[0], originalWaypoint);
            }
        }

        return isolated;
    }

    private static bool RoundtripPlanCanBeAssignedToRuntimeWorld(
        TerrainGenerationProfile profile,
        TerrainWorldPlan roundtrip)
    {
        TerrainWorld world = CreateTerrainWorldFacadeProbe(profile, worldPlan: null);
        world.SetWorldPlan(roundtrip);
        bool assignedPlanPassed =
            world.TryGetWorldPlan(out TerrainWorldPlan? assignedPlan) &&
            assignedPlan is not null &&
            !ReferenceEquals(assignedPlan, roundtrip) &&
            TerrainPlansMatchForJson(roundtrip, assignedPlan) &&
            RuntimeWorldPlanFacadeIsolated(world, assignedPlan, roundtrip);

        return assignedPlanPassed &&
            world.GetPointsOfInterest().Length == roundtrip.PointsOfInterest.Length &&
            world.GetRoutes().Length == roundtrip.Routes.Length &&
            world.TryGetWorldPlanSnapshot(out TerrainWorldPlanSnapshot? snapshot) &&
            snapshot is not null &&
            snapshot.PointsOfInterest.Length == roundtrip.PointsOfInterest.Length &&
            snapshot.Routes.Length == roundtrip.Routes.Length &&
            snapshot.Regions.Length == roundtrip.Regions.Length;
    }

    private static bool RuntimeWorldPlanFacadeIsolated(
        TerrainWorld world,
        TerrainWorldPlan returnedPlan,
        TerrainWorldPlan expectedPlan)
    {
        if (returnedPlan.Regions.Length > 0)
        {
            TerrainWorldRegion originalRegion = expectedPlan.Regions[0];
            returnedPlan.Regions[0] = originalRegion with { Height = originalRegion.Height + 9999.0f };
            if (!world.TryGetWorldPlan(out TerrainWorldPlan? secondPlan) ||
                secondPlan is null ||
                secondPlan.Regions.Length != expectedPlan.Regions.Length ||
                !RegionsMatchForJson(originalRegion, secondPlan.Regions[0]))
            {
                return false;
            }
        }

        if (returnedPlan.PointsOfInterest.Length > 0)
        {
            TerrainWorldPointOfInterest originalPoint = expectedPlan.PointsOfInterest[0];
            returnedPlan.PointsOfInterest[0] = originalPoint with { Id = originalPoint.Id + 9999 };
            if (!world.TryGetWorldPlan(out TerrainWorldPlan? secondPlan) ||
                secondPlan is null ||
                secondPlan.PointsOfInterest.Length != expectedPlan.PointsOfInterest.Length ||
                !PointsMatchForJson(originalPoint, secondPlan.PointsOfInterest[0]))
            {
                return false;
            }
        }

        if (returnedPlan.Routes.Length > 0)
        {
            TerrainWorldRoute originalRoute = expectedPlan.Routes[0];
            returnedPlan.Routes[0] = returnedPlan.Routes[0] with { FromPointId = originalRoute.FromPointId + 9999 };
            if (!world.TryGetWorldPlan(out TerrainWorldPlan? secondPlan) ||
                secondPlan is null ||
                secondPlan.Routes.Length != expectedPlan.Routes.Length ||
                !RoutesMatchForJson(originalRoute, secondPlan.Routes[0]))
            {
                return false;
            }

            if (originalRoute.Waypoints.Length > 0 && returnedPlan.Routes[0].Waypoints.Length > 0)
            {
                returnedPlan.Routes[0].Waypoints[0] = originalRoute.Waypoints[0] + new Vector2(9999.0f, -9999.0f);
                if (!world.TryGetWorldPlan(out TerrainWorldPlan? waypointPlan) ||
                    waypointPlan is null ||
                    waypointPlan.Routes.Length != expectedPlan.Routes.Length ||
                    !RoutesMatchForJson(originalRoute, waypointPlan.Routes[0]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string NormalizeScatterRuleSetHash(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ResolveInternalDefaultRuleHash("Dao.Terrain.Generation.TerrainScatterRuleCatalog")
            : value;
    }

    private static string NormalizeSettlementVisualRuleSetHash(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ResolveInternalDefaultRuleHash("Dao.Terrain.Generation.TerrainSettlementVisualRuleCatalog")
            : value;
    }

    private static string NormalizePointOfInterestRuleSetHash(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ResolveInternalDefaultRuleHash("Dao.Terrain.Generation.TerrainPointOfInterestRuleCatalog")
            : value;
    }

    private static string NormalizeRouteRuleSetHash(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ResolveInternalDefaultRuleHash("Dao.Terrain.Generation.TerrainRouteRuleCatalog")
            : value;
    }

    private static string NormalizeScenicRuleSetHash(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ResolveInternalDefaultRuleHash("Dao.Terrain.Generation.TerrainScenicLandmarkRuleCatalog")
            : value;
    }

    private static string ResolveInternalDefaultRuleHash(string fullTypeName)
    {
        Type assemblyType = typeof(TerrainWorld);
        Type? type = assemblyType.Assembly.GetType(fullTypeName, throwOnError: false);
        if (type is null)
        {
            throw new InvalidOperationException($"Unable to resolve terrain rule catalog type '{fullTypeName}'.");
        }

        PropertyInfo? property = type.GetProperty("DefaultHash", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (property?.GetValue(null) is string value && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Terrain rule catalog '{fullTypeName}' did not expose a usable DefaultHash.");
    }

    private static bool PlanFloatEquals(float expected, float actual)
    {
        return ExactFloatEquals(expected, actual);
    }

    private static bool ExactFloatEquals(float expected, float actual)
    {
        return Math.Abs(expected - actual) <= TerrainDeterminismContract.ExactFloatEpsilon;
    }

    private static bool ExactPositionEquals(Vector2 expected, Vector2 actual)
    {
        return expected.DistanceSquaredTo(actual) <= TerrainDeterminismContract.Squared(TerrainDeterminismContract.ExactPositionEpsilon);
    }

    private static string PlanJsonFailureReason(
        bool metadataPassed,
        bool schemaShapePassed,
        bool stringLoadPassed,
        bool stringRoundtripMatches,
        bool roundtripIsolationPassed,
        bool setWorldPlanPassed,
        bool fileLoadPassed,
        bool fileRoundtripMatches,
        bool seedMismatchRejected,
        bool profileHashMismatchRejected,
        bool legacyApiVersionAccepted,
        bool previousApiVersionAccepted,
        bool currentApiMinusThreeVersionAccepted,
        bool currentApiMinusTwoVersionAccepted,
        bool currentApiMinusOneVersionAccepted,
        bool versionDriftRejected,
        bool enumNameDriftRejected,
        bool enumValueDriftRejected,
        Error saveError,
        string stringLoadError,
        string fileLoadError)
    {
        if (!metadataPassed)
        {
            return "plan JSON metadata or required schema nodes did not match the contract";
        }

        if (!schemaShapePassed)
        {
            return "plan JSON vector schema did not use stable x/z coordinate nodes or route waypoint arrays";
        }

        if (!stringLoadPassed)
        {
            return $"plan JSON string load failed: {stringLoadError}";
        }

        if (!stringRoundtripMatches)
        {
            return "plan JSON string roundtrip changed plan data";
        }

        if (!roundtripIsolationPassed)
        {
            return "plan JSON roundtrip reused mutable plan array state";
        }

        if (!setWorldPlanPassed)
        {
            return "plan JSON roundtrip could not be assigned through TerrainWorld.SetWorldPlan";
        }

        if (!fileLoadPassed)
        {
            return $"plan JSON file save/load failed ({saveError}): {fileLoadError}";
        }

        if (!fileRoundtripMatches)
        {
            return "plan JSON file roundtrip changed plan data";
        }

        if (!seedMismatchRejected)
        {
            return "plan JSON accepted a mismatched seed";
        }

        if (!profileHashMismatchRejected)
        {
            return "plan JSON accepted a mismatched profile hash";
        }

        if (!legacyApiVersionAccepted)
        {
            return "plan JSON rejected a compatible terrain-api-v1 1.0.0 plan";
        }

        if (!previousApiVersionAccepted)
        {
            return "plan JSON rejected a compatible terrain-api-v1 1.1.0 plan";
        }

        if (!currentApiMinusThreeVersionAccepted)
        {
            return "plan JSON rejected a compatible terrain-api-v1 1.2.0 plan";
        }

        if (!currentApiMinusTwoVersionAccepted)
        {
            return "plan JSON rejected a compatible terrain-api-v1 1.3.0 plan";
        }

        if (!currentApiMinusOneVersionAccepted)
        {
            return "plan JSON rejected a compatible terrain-api-v1 1.4.0 plan";
        }

        if (!versionDriftRejected)
        {
            return "plan JSON accepted an incompatible contract or version drift";
        }

        if (!enumNameDriftRejected)
        {
            return "plan JSON accepted an enum name drift";
        }

        if (!enumValueDriftRejected)
        {
            return "plan JSON accepted an enum value drift";
        }

        return "plan JSON roundtrip smoke failed";
    }
}
