using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Dao.Terrain;
using Dao.Terrain.Generation;
using Dao.Terrain.Streaming;
using Godot;

internal static class TerrainValidationRuntimeProbeHelpers
{
    internal static TerrainWorld CreateTerrainWorldFacadeProbe(
        TerrainGenerationProfile profile,
        TerrainWorldPlan? worldPlan)
    {
        var world = (TerrainWorld)RuntimeHelpers.GetUninitializedObject(typeof(TerrainWorld));
        SetPrivateField(world, "_profile", profile);
        SetPrivateField(world, "_hasProfileSnapshot", true);
        SetPrivateField(world, "_worldPlan", worldPlan);
        SetPrivateField(world, "_modificationLayer", TerrainModificationLayer.Empty);
        SetPrivateField(world, "_modificationLayerCacheKey", 0);
        SetPrivateField(
            world,
            "_routeCorridors",
            worldPlan is null ? TerrainRouteCorridorIndex.Empty : TerrainRouteCorridorIndex.FromPlan(worldPlan, profile));
        SetPrivateField(
            world,
            "_pointOfInterestIndex",
            worldPlan is null ? TerrainPointOfInterestIndex.Empty : TerrainPointOfInterestIndex.FromPlan(worldPlan, profile));
        world.StreamTerrainBeforeOpenWorldPlanReady = true;
        return world;
    }

    internal static TerrainChunk CreateTerrainChunkProbe(
        TerrainTileCoord coord,
        int lod,
        bool hasCollision)
    {
        var chunk = (TerrainChunk)RuntimeHelpers.GetUninitializedObject(typeof(TerrainChunk));
        SetCompilerGeneratedAutoPropertyField(chunk, nameof(TerrainChunk.Coord), coord);
        SetCompilerGeneratedAutoPropertyField(chunk, nameof(TerrainChunk.Lod), lod);
        SetCompilerGeneratedAutoPropertyField(chunk, nameof(TerrainChunk.HasCollision), hasCollision);
        return chunk;
    }

    internal static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        field.SetValue(instance, value);
    }

    internal static void SetCompilerGeneratedAutoPropertyField<T>(object instance, string propertyName, T value)
    {
        string backingFieldName = $"<{propertyName}>k__BackingField";
        FieldInfo? field = instance.GetType().GetField(backingFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null)
        {
            throw new MissingFieldException(instance.GetType().FullName, backingFieldName);
        }

        field.SetValue(instance, value);
    }

    internal static void InvokePrivateMethod(object instance, string methodName)
    {
        MethodInfo? method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null)
        {
            throw new MissingMethodException(instance.GetType().FullName, methodName);
        }

        method.Invoke(instance, null);
    }

    internal static object? InvokePrivateMethod(object instance, string methodName, params object?[]? args)
    {
        MethodInfo? method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null)
        {
            throw new MissingMethodException(instance.GetType().FullName, methodName);
        }

        return method.Invoke(instance, args);
    }

    internal static object CreatePendingTileJobKeyDictionary(TerrainTileCoord[] coords)
    {
        Type pendingJobType = ResolveRuntimeType("PendingTileJob");
        Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(TerrainTileCoord), pendingJobType);
        var dictionary = (System.Collections.IDictionary)(Activator.CreateInstance(dictionaryType)
            ?? throw new InvalidOperationException("Failed to create pending tile job dictionary."));

        foreach (TerrainTileCoord coord in coords)
        {
            dictionary.Add(coord, null);
        }

        return dictionary;
    }

    internal static object CreatePendingTileJobStateDictionary(
        TerrainTileCoord[] coords,
        TerrainGenerationProfile profile,
        int terrainFeatureKey)
    {
        Type pendingJobType = ResolveRuntimeType("PendingTileJob");
        Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(TerrainTileCoord), pendingJobType);
        var dictionary = (System.Collections.IDictionary)(Activator.CreateInstance(dictionaryType)
            ?? throw new InvalidOperationException("Failed to create pending tile job dictionary."));

        foreach (TerrainTileCoord coord in coords)
        {
            dictionary.Add(coord, CreatePendingTileJob(pendingJobType, coord, profile, terrainFeatureKey));
        }

        return dictionary;
    }

    internal static object CreatePendingTileJobList()
    {
        Type pendingJobType = ResolveRuntimeType("PendingTileJob");
        Type listType = typeof(List<>).MakeGenericType(pendingJobType);
        return Activator.CreateInstance(listType)
            ?? throw new InvalidOperationException("Failed to create retired pending tile job list.");
    }

    internal static object CreateTileCacheState(
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int terrainFeatureKey)
    {
        return CreateTileCacheState([coord], profile, terrainFeatureKey);
    }

    internal static object CreateTileCacheState(
        TerrainTileCoord[] coords,
        TerrainGenerationProfile profile,
        int terrainFeatureKey)
    {
        Type cacheType = ResolveRuntimeType("TerrainTileDataCache");
        object cache = Activator.CreateInstance(cacheType, nonPublic: true)
            ?? throw new InvalidOperationException("Failed to create terrain tile cache state.");
        Type cacheKeyType = ResolveRuntimeType("TerrainTileCacheKey");
        Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(cacheKeyType, typeof(TerrainTileData));
        var dictionary = (System.Collections.IDictionary)(Activator.CreateInstance(dictionaryType)
            ?? throw new InvalidOperationException("Failed to create tile cache dictionary."));
        foreach (TerrainTileCoord coord in coords)
        {
            object key = Activator.CreateInstance(cacheKeyType, coord, 0, false, profile, terrainFeatureKey)
                ?? throw new InvalidOperationException("Failed to create tile cache key.");
            dictionary.Add(key, null);
        }
        SetPrivateField(cache, "_tileCache", dictionary);
        Type nodeType = typeof(LinkedListNode<>).MakeGenericType(cacheKeyType);
        Type nodeDictionaryType = typeof(Dictionary<,>).MakeGenericType(cacheKeyType, nodeType);
        object nodeDictionary = Activator.CreateInstance(nodeDictionaryType)
            ?? throw new InvalidOperationException("Failed to create tile cache node dictionary.");
        SetPrivateField(cache, "_tileCacheNodes", nodeDictionary);
        Type listType = typeof(LinkedList<>).MakeGenericType(cacheKeyType);
        object list = Activator.CreateInstance(listType)
            ?? throw new InvalidOperationException("Failed to create tile cache LRU list.");
        SetPrivateField(cache, "_tileCacheLru", list);
        return cache;
    }

    internal static int GetNestedPrivateCollectionCount(object instance, string fieldName, string nestedFieldName)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        object value = field.GetValue(instance)
            ?? throw new InvalidOperationException($"Private field {fieldName} was null.");
        FieldInfo? nestedField = value.GetType().GetField(nestedFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (nestedField is null)
        {
            throw new MissingFieldException(value.GetType().FullName, nestedFieldName);
        }

        object nestedValue = nestedField.GetValue(value)
            ?? throw new InvalidOperationException($"Nested private field {nestedFieldName} was null.");
        PropertyInfo? count = nestedValue.GetType().GetProperty("Count", BindingFlags.Instance | BindingFlags.Public);
        if (count?.GetValue(nestedValue) is int result)
        {
            return result;
        }

        throw new InvalidOperationException($"Nested private field {nestedFieldName} does not expose a Count property.");
    }

    private static Type ResolveRuntimeType(string typeName)
    {
        Type worldType = typeof(TerrainWorld);
        return worldType.GetNestedType(typeName, BindingFlags.NonPublic)
            ?? worldType.Assembly.GetType($"Dao.Terrain.Streaming.{typeName}", throwOnError: false)
            ?? throw new MissingMemberException(worldType.FullName, typeName);
    }

    internal static object CreateTileCacheNodeDictionary()
    {
        Type cacheKeyType = ResolveRuntimeType("TerrainTileCacheKey");
        Type nodeType = typeof(LinkedListNode<>).MakeGenericType(cacheKeyType);
        Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(cacheKeyType, nodeType);
        return Activator.CreateInstance(dictionaryType)
            ?? throw new InvalidOperationException("Failed to create tile cache node dictionary.");
    }

    internal static object CreateTileCacheLinkedList()
    {
        Type cacheKeyType = ResolveRuntimeType("TerrainTileCacheKey");
        Type listType = typeof(LinkedList<>).MakeGenericType(cacheKeyType);
        return Activator.CreateInstance(listType)
            ?? throw new InvalidOperationException("Failed to create tile cache LRU list.");
    }

    internal static int GetPrivateCollectionCount(object instance, string fieldName)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        object value = field.GetValue(instance)
            ?? throw new InvalidOperationException($"Private field {fieldName} was null.");
        PropertyInfo? count = value.GetType().GetProperty("Count", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (count?.GetValue(value) is int result)
        {
            return result;
        }

        throw new InvalidOperationException($"Private field {fieldName} does not expose a Count property.");
    }

    internal static bool StreamingSnapshotMatchesFacadeContract(
        TerrainWorld world,
        TerrainGenerationProfile profile,
        bool hasWorldPlan,
        TerrainTileCoord[] expectedDesiredCoords,
        TerrainTileCoord[] expectedLoadedCoords,
        TerrainTileCoord[] expectedQueuedCoords)
    {
        TerrainWorldStreamingSnapshot snapshot = world.GetStreamingSnapshot();
        if (!StreamingSnapshotValuesMatch(
                snapshot,
                profile,
                hasWorldPlan,
                expectedDesiredCoords,
                expectedLoadedCoords,
                expectedQueuedCoords))
        {
            return false;
        }

        if (snapshot.DesiredChunks.Length > 0)
        {
            snapshot.DesiredChunks[0] = new TerrainTileCoord(999, 999);
        }

        if (snapshot.LoadedChunks.Length > 0)
        {
            snapshot.LoadedChunks[0] = new TerrainTileCoord(888, 888);
        }

        if (snapshot.QueuedTileJobs.Length > 0)
        {
            snapshot.QueuedTileJobs[0] = new TerrainTileCoord(777, 777);
        }

        TerrainWorldStreamingSnapshot secondSnapshot = world.GetStreamingSnapshot();
        return StreamingSnapshotValuesMatch(
            secondSnapshot,
            profile,
            hasWorldPlan,
            expectedDesiredCoords,
            expectedLoadedCoords,
            expectedQueuedCoords);
    }

    private static object CreatePendingTileJob(
        Type pendingJobType,
        TerrainTileCoord coord,
        TerrainGenerationProfile profile,
        int terrainFeatureKey)
    {
        var completion = new TaskCompletionSource<TerrainTileData>();
        return Activator.CreateInstance(
                pendingJobType,
                coord,
                0,
                false,
                profile,
                terrainFeatureKey,
                new CancellationTokenSource(),
                completion.Task)
            ?? throw new InvalidOperationException("Failed to create pending tile job.");
    }

    private static bool StreamingSnapshotValuesMatch(
        TerrainWorldStreamingSnapshot snapshot,
        TerrainGenerationProfile profile,
        bool hasWorldPlan,
        TerrainTileCoord[] expectedDesiredCoords,
        TerrainTileCoord[] expectedLoadedCoords,
        TerrainTileCoord[] expectedQueuedCoords)
    {
        return snapshot.Profile.Equals(profile) &&
            !snapshot.HasFocus &&
            snapshot.FocusPosition == Vector3.Zero &&
            snapshot.FocusCoord == default &&
            snapshot.StreamRadiusChunks == profile.StreamRadiusChunks &&
            snapshot.DesiredChunkCount == snapshot.DesiredChunks.Length &&
            snapshot.LoadedChunkCount == snapshot.LoadedChunks.Length &&
            snapshot.QueuedTileJobCount == snapshot.QueuedTileJobs.Length &&
            snapshot.RetiredTileJobCount == 0 &&
            snapshot.TileCacheLimit == Mathf.Max(0, profile.MaxCachedTileData) &&
            snapshot.MaxQueuedTileJobs == profile.MaxQueuedTileJobs &&
            snapshot.MaxCompletedTilesPerFrame == profile.MaxCompletedTilesPerFrame &&
            snapshot.HasWorldPlan == hasWorldPlan &&
            !snapshot.IsWorldPlanGenerationPending &&
            snapshot.StreamTerrainBeforeOpenWorldPlanReady &&
            snapshot.TileCacheWithinLimit &&
            snapshot.TileJobQueueWithinLimit &&
            !snapshot.CanStreamTerrain &&
            !snapshot.FocusTileLoaded &&
            !snapshot.DesiredChunksLoaded &&
            !snapshot.FocusAreaReady &&
            StreamingReadinessContractMatches(profile) &&
            TileCoordsMatch(snapshot.DesiredChunks, expectedDesiredCoords) &&
            TileCoordsMatch(snapshot.LoadedChunks, expectedLoadedCoords) &&
            TileCoordsMatch(snapshot.QueuedTileJobs, expectedQueuedCoords);
    }

    private static bool StreamingReadinessContractMatches(TerrainGenerationProfile profile)
    {
        TerrainTileCoord focusCoord = new(4, -2);
        TerrainTileCoord neighborCoord = new(5, -2);
        Vector3 focusPosition = new(
            focusCoord.X * profile.ChunkSize + profile.ChunkSize * 0.5f,
            0.0f,
            focusCoord.Z * profile.ChunkSize + profile.ChunkSize * 0.5f);
        TerrainTileCoord[] desired = [focusCoord, neighborCoord];
        TerrainTileCoord[] loaded = [focusCoord, neighborCoord];

        TerrainWorldStreamingSnapshot waitingForPlan = CreateStreamingReadinessSnapshot(
            profile,
            hasFocus: true,
            focusPosition,
            focusCoord,
            desired,
            loaded,
            queuedJobs: [],
            retiredJobCount: 0,
            tileCacheCount: 0,
            hasWorldPlan: false,
            isWorldPlanGenerationPending: true,
            streamTerrainBeforeOpenWorldPlanReady: false);
        if (waitingForPlan.CanStreamTerrain || waitingForPlan.FocusAreaReady)
        {
            return false;
        }

        TerrainWorldStreamingSnapshot queued = CreateStreamingReadinessSnapshot(
            profile,
            hasFocus: true,
            focusPosition,
            focusCoord,
            desired,
            loaded,
            queuedJobs: [neighborCoord],
            retiredJobCount: 0,
            tileCacheCount: 1,
            hasWorldPlan: true,
            isWorldPlanGenerationPending: false,
            streamTerrainBeforeOpenWorldPlanReady: false);
        if (!queued.CanStreamTerrain ||
            !queued.FocusTileLoaded ||
            !queued.DesiredChunksLoaded ||
            queued.FocusAreaReady)
        {
            return false;
        }

        TerrainWorldStreamingSnapshot ready = CreateStreamingReadinessSnapshot(
            profile,
            hasFocus: true,
            focusPosition,
            focusCoord,
            desired,
            loaded,
            queuedJobs: [],
            retiredJobCount: 0,
            tileCacheCount: 1,
            hasWorldPlan: true,
            isWorldPlanGenerationPending: false,
            streamTerrainBeforeOpenWorldPlanReady: false);
        if (!ready.CanStreamTerrain ||
            !ready.FocusTileLoaded ||
            !ready.DesiredChunksLoaded ||
            !ready.FocusAreaReady)
        {
            return false;
        }

        TerrainWorldStreamingSnapshot missingFocusTile = CreateStreamingReadinessSnapshot(
            profile,
            hasFocus: true,
            focusPosition,
            focusCoord,
            desired,
            loaded: [neighborCoord],
            queuedJobs: [],
            retiredJobCount: 0,
            tileCacheCount: 1,
            hasWorldPlan: true,
            isWorldPlanGenerationPending: false,
            streamTerrainBeforeOpenWorldPlanReady: false);
        if (missingFocusTile.FocusTileLoaded ||
            missingFocusTile.DesiredChunksLoaded ||
            missingFocusTile.FocusAreaReady)
        {
            return false;
        }

        TerrainWorldStreamingSnapshot overBudget = CreateStreamingReadinessSnapshot(
            profile,
            hasFocus: true,
            focusPosition,
            focusCoord,
            desired,
            loaded,
            queuedJobs: CreateTileCoordArray(Mathf.Max(0, profile.MaxQueuedTileJobs) + 1, focusCoord),
            retiredJobCount: 0,
            tileCacheCount: Mathf.Max(0, profile.MaxCachedTileData) + 1,
            hasWorldPlan: true,
            isWorldPlanGenerationPending: false,
            streamTerrainBeforeOpenWorldPlanReady: false);
        return !overBudget.TileJobQueueWithinLimit &&
            !overBudget.CanStreamTerrain &&
            !overBudget.FocusAreaReady;
    }

    private static TerrainTileCoord[] CreateTileCoordArray(int count, TerrainTileCoord start)
    {
        var coords = new TerrainTileCoord[count];
        for (int i = 0; i < coords.Length; i++)
        {
            coords[i] = new TerrainTileCoord(start.X + i, start.Z);
        }

        return coords;
    }

    private static TerrainWorldStreamingSnapshot CreateStreamingReadinessSnapshot(
        TerrainGenerationProfile profile,
        bool hasFocus,
        Vector3 focusPosition,
        TerrainTileCoord focusCoord,
        TerrainTileCoord[] desired,
        TerrainTileCoord[] loaded,
        TerrainTileCoord[] queuedJobs,
        int retiredJobCount,
        int tileCacheCount,
        bool hasWorldPlan,
        bool isWorldPlanGenerationPending,
        bool streamTerrainBeforeOpenWorldPlanReady)
    {
        return new TerrainWorldStreamingSnapshot(
            profile,
            hasFocus,
            focusPosition,
            focusCoord,
            profile.StreamRadiusChunks,
            desired.Length,
            desired,
            loaded.Length,
            loaded,
            queuedJobs.Length,
            queuedJobs,
            retiredJobCount,
            tileCacheCount,
            Mathf.Max(0, profile.MaxCachedTileData),
            profile.MaxQueuedTileJobs,
            profile.MaxCompletedTilesPerFrame,
            hasWorldPlan,
            isWorldPlanGenerationPending,
            streamTerrainBeforeOpenWorldPlanReady);
    }

    private static bool TileCoordsMatch(TerrainTileCoord[] actual, TerrainTileCoord[] expected)
    {
        if (actual.Length != expected.Length)
        {
            return false;
        }

        for (int i = 0; i < actual.Length; i++)
        {
            if (actual[i] != expected[i])
            {
                return false;
            }
        }

        return true;
    }
}
