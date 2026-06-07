using System;
using System.Collections.Generic;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Streaming;

public partial class TerrainWorld
{
    /// <summary>Returns the currently applied terrain modification overlay.</summary>
    public TerrainModificationLayer GetModificationLayer()
    {
        return _modificationLayer;
    }

    /// <summary>Applies or replaces the current terrain modification overlay and invalidates affected runtime data.</summary>
    public void SetModificationLayer(TerrainModificationLayer? modificationLayer)
    {
        TerrainWorldRuntimeLifecycleService.SetModificationLayer(this, modificationLayer);
    }

    /// <summary>Clears the current terrain modification overlay.</summary>
    public void ClearModificationLayer()
    {
        TerrainWorldRuntimeLifecycleService.SetModificationLayer(this, null);
    }

    /// <summary>Returns the current terrain modification overlay as JSON for save systems and tooling.</summary>
    public string GetModificationLayerJson()
    {
        return _modificationLayer.ToJson();
    }

    /// <summary>Saves the current terrain modification overlay to a JSON file.</summary>
    public Error SaveModificationLayer(string outputPath)
    {
        return _modificationLayer.SaveJson(outputPath);
    }

    /// <summary>Loads a terrain modification overlay from JSON text and applies it to this world.</summary>
    public bool TrySetModificationLayerFromJson(string json, out string error)
    {
        if (!TerrainModificationLayer.TryFromJson(json, out TerrainModificationLayer? layer, out error) ||
            layer is null)
        {
            return false;
        }

        SetModificationLayer(layer);
        return true;
    }

    /// <summary>Loads a terrain modification overlay from a JSON file and applies it to this world.</summary>
    public bool TryLoadModificationLayer(string path, out string error)
    {
        if (!TerrainModificationLayer.TryLoadJson(path, out TerrainModificationLayer? layer, out error) ||
            layer is null)
        {
            return false;
        }

        SetModificationLayer(layer);
        return true;
    }

    /// <summary>Returns the streaming-tile coordinates affected by the current terrain modification overlay.</summary>
    public TerrainTileCoord[] QueryAffectedModificationTiles()
    {
        return _modificationLayer.QueryAffectedTiles(CurrentProfile.ChunkSize);
    }

    private TerrainWorldField SampleBaseFieldCore(Vector2 world)
    {
        return TerrainWorldFieldSampler.Sample(world, CurrentProfile);
    }

    private TerrainSample SampleBaseSurfaceCore(Vector2 world, float spacing)
    {
        return TerrainSampler.SampleWithSlope(world, CurrentProfile, spacing);
    }

    private TerrainWorldField SampleFieldWithModification(Vector2 world)
    {
        TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, CurrentProfile);
        return ApplyModification(field);
    }

    private TerrainWorldField ApplyModification(TerrainWorldField field)
    {
        return _modificationLayer.IsEmpty
            ? field
            : _modificationLayer.ApplyToField(field);
    }

    private TerrainSample SampleSurfaceWithModification(Vector2 world, float spacing)
    {
        TerrainGenerationProfile profile = CurrentProfile;
        TerrainWorldField centerField = SampleFieldWithModification(world);
        float delta = Mathf.Max(1.0f, spacing);
        float left = SampleFieldWithModification(new Vector2(world.X - delta, world.Y)).Height;
        float right = SampleFieldWithModification(new Vector2(world.X + delta, world.Y)).Height;
        float down = SampleFieldWithModification(new Vector2(world.X, world.Y - delta)).Height;
        float up = SampleFieldWithModification(new Vector2(world.X, world.Y + delta)).Height;

        Vector3 normal = new Vector3(left - right, delta * 2.0f, down - up).Normalized();
        float slope = 1.0f - Mathf.Clamp(normal.Y, 0.0f, 1.0f);
        Color color = TerrainSampler.ColorForSurface(centerField, profile, slope);

        return new TerrainSample(
            centerField.Height,
            centerField.Continent,
            centerField.Mountains,
            centerField.River,
            centerField.Lake,
            centerField.Moisture,
            centerField.Temperature,
            centerField.ScenicPotential,
            centerField.Traversability,
            centerField.BiomeKind,
            centerField.LandscapeKind,
            slope,
            color);
    }

    private TerrainTraversalCost SampleBaseTraversalCostCore(Vector2 world, float spacing)
    {
        TerrainGenerationProfile profile = CurrentProfile;
        TerrainWorldField field = SampleBaseFieldCore(world);
        TerrainSample surface = SampleBaseSurfaceCore(world, spacing);
        return TerrainSemanticClassifier.ClassifyTraversalCost(field, surface, profile);
    }

    private TerrainTraversalCost SampleTraversalCostWithModification(Vector2 world, float spacing)
    {
        TerrainGenerationProfile profile = CurrentProfile;
        TerrainWorldField field = SampleFieldWithModification(world);
        TerrainSample surface = SampleSurfaceWithModification(world, spacing);
        return TerrainSemanticClassifier.ClassifyTraversalCost(field, surface, profile);
    }

    private TerrainWorldPlan? CreateEffectiveWorldPlan()
    {
        if (_worldPlan is null)
        {
            return null;
        }

        if (!HasRouteModificationState(_modificationLayer))
        {
            return _worldPlan;
        }

        return new TerrainWorldPlan(
            _worldPlan.Center,
            _worldPlan.WorldSize,
            _worldPlan.GridResolution,
            _worldPlan.Regions,
            _worldPlan.PointsOfInterest,
            BuildEffectiveRoutes(_worldPlan, _modificationLayer),
            _worldPlan.QualityReport,
            _worldPlan.PlanningReport,
            _worldPlan.ExperienceReport);
    }

    private TerrainWorldRoute[] GetEffectiveRoutes()
    {
        if (_worldPlan is null)
        {
            return Array.Empty<TerrainWorldRoute>();
        }

        TerrainWorldRoute[] effective = BuildEffectiveRoutes(_worldPlan, _modificationLayer);
        if (effective.Length == 0)
        {
            return Array.Empty<TerrainWorldRoute>();
        }

        var copy = new TerrainWorldRoute[effective.Length];
        for (int i = 0; i < effective.Length; i++)
        {
            Vector2[] waypoints = effective[i].Waypoints.Length == 0
                ? Array.Empty<Vector2>()
                : (Vector2[])effective[i].Waypoints.Clone();
            copy[i] = effective[i] with { Waypoints = waypoints };
        }

        return copy;
    }

    private static TerrainWorldRoute[] BuildEffectiveRoutes(
        TerrainWorldPlan plan,
        TerrainModificationLayer modificationLayer)
    {
        TerrainWorldRoute[] routes = plan.Routes;
        if (routes.Length == 0 || !HasRouteModificationState(modificationLayer))
        {
            var copy = new TerrainWorldRoute[routes.Length];
            for (int i = 0; i < routes.Length; i++)
            {
                Vector2[] waypoints = routes[i].Waypoints.Length == 0
                    ? Array.Empty<Vector2>()
                    : (Vector2[])routes[i].Waypoints.Clone();
                copy[i] = routes[i] with { Waypoints = waypoints };
            }

            return copy;
        }

        var effective = new List<TerrainWorldRoute>(routes.Length);
        for (int i = 0; i < routes.Length; i++)
        {
            TerrainWorldRoute route = routes[i];
            if (modificationLayer.HasRouteState(route.FromPointId, route.ToPointId, out TerrainRouteModification modification) &&
                modification.Blocked &&
                !modification.Unlocked)
            {
                continue;
            }

            float costMultiplier = modificationLayer.HasRouteState(route.FromPointId, route.ToPointId, out modification)
                ? SafeRouteCostMultiplier(modification)
                : 1.0f;
            Vector2[] waypoints = route.Waypoints.Length == 0
                ? Array.Empty<Vector2>()
                : (Vector2[])route.Waypoints.Clone();
            effective.Add(route with
            {
                Cost = route.Cost * costMultiplier,
                Waypoints = waypoints
            });
        }

        return effective.Count == 0 ? Array.Empty<TerrainWorldRoute>() : effective.ToArray();
    }

    private static bool HasRouteModificationState(TerrainModificationLayer modificationLayer)
    {
        return modificationLayer.RouteModifications.Length > 0;
    }

    private static float SafeRouteCostMultiplier(TerrainRouteModification modification)
    {
        if (!float.IsFinite(modification.CostMultiplier) || modification.CostMultiplier <= 0.0f)
        {
            return 1.0f;
        }

        return modification.CostMultiplier;
    }

    private static int ComputeModificationLayerCacheKey(TerrainModificationLayer modificationLayer)
    {
        unchecked
        {
            int hash = 17;

            foreach (TerrainHeightDelta delta in modificationLayer.HeightDeltas)
            {
                hash = (hash * 397) ^ FloatHash(delta.WorldPosition.X);
                hash = (hash * 397) ^ FloatHash(delta.WorldPosition.Y);
                hash = (hash * 397) ^ FloatHash(delta.Radius);
                hash = (hash * 397) ^ FloatHash(delta.Delta);
                hash = (hash * 397) ^ FloatHash(delta.InnerRadius);
            }

            foreach (TerrainSurfaceOverride surface in modificationLayer.SurfaceOverrides)
            {
                hash = (hash * 397) ^ FloatHash(surface.WorldPosition.X);
                hash = (hash * 397) ^ FloatHash(surface.WorldPosition.Y);
                hash = (hash * 397) ^ FloatHash(surface.Radius);
                hash = (hash * 397) ^ (int)surface.BiomeKind;
                hash = (hash * 397) ^ (int)surface.LandscapeKind;
                hash = (hash * 397) ^ (int)surface.GameplayTags;
                hash = (hash * 397) ^ FloatHash(surface.Traversability);
                hash = (hash * 397) ^ FloatHash(surface.HazardPotential);
            }

            foreach (TerrainScatterModification scatter in modificationLayer.ScatterModifications)
            {
                hash = (hash * 397) ^ FloatHash(scatter.WorldPosition.X);
                hash = (hash * 397) ^ FloatHash(scatter.WorldPosition.Y);
                hash = (hash * 397) ^ FloatHash(scatter.Radius);
                hash = (hash * 397) ^ (int)scatter.Kind;
                hash = (hash * 397) ^ scatter.StableId;
                hash = (hash * 397) ^ (scatter.Remove ? 1 : 0);
                hash = (hash * 397) ^ (scatter.State?.GetHashCode(StringComparison.Ordinal) ?? 0);
            }

            foreach (TerrainLandmarkModification landmark in modificationLayer.LandmarkModifications)
            {
                hash = (hash * 397) ^ FloatHash(landmark.WorldPosition.X);
                hash = (hash * 397) ^ FloatHash(landmark.WorldPosition.Y);
                hash = (hash * 397) ^ FloatHash(landmark.Radius);
                hash = (hash * 397) ^ (int)landmark.Kind;
                hash = (hash * 397) ^ landmark.StableId;
                hash = (hash * 397) ^ (landmark.State?.GetHashCode(StringComparison.Ordinal) ?? 0);
            }

            foreach (TerrainRouteModification route in modificationLayer.RouteModifications)
            {
                hash = (hash * 397) ^ route.FromPointId;
                hash = (hash * 397) ^ route.ToPointId;
                hash = (hash * 397) ^ (route.Blocked ? 1 : 0);
                hash = (hash * 397) ^ (route.Unlocked ? 1 : 0);
                hash = (hash * 397) ^ FloatHash(route.CostMultiplier);
                hash = (hash * 397) ^ (route.State?.GetHashCode(StringComparison.Ordinal) ?? 0);
            }

            return hash;
        }
    }

    private static TerrainTileCoord[] CollectAffectedTiles(
        TerrainModificationLayer previous,
        TerrainModificationLayer current,
        float chunkSize)
    {
        var set = new HashSet<TerrainTileCoord>();
        AddAffectedTiles(set, previous.QueryAffectedTiles(chunkSize));
        AddAffectedTiles(set, current.QueryAffectedTiles(chunkSize));
        if (set.Count == 0)
        {
            return Array.Empty<TerrainTileCoord>();
        }

        TerrainTileCoord[] result = new TerrainTileCoord[set.Count];
        set.CopyTo(result);
        Array.Sort(result, static (a, b) =>
        {
            int x = a.X.CompareTo(b.X);
            return x != 0 ? x : a.Z.CompareTo(b.Z);
        });
        return result;
    }

    private static void AddAffectedTiles(HashSet<TerrainTileCoord> set, TerrainTileCoord[] coords)
    {
        for (int i = 0; i < coords.Length; i++)
        {
            set.Add(coords[i]);
        }
    }

    private static bool RouteModificationSetEquals(
        TerrainModificationLayer previous,
        TerrainModificationLayer current)
    {
        TerrainRouteModification[] previousRoutes = previous.RouteModifications;
        TerrainRouteModification[] currentRoutes = current.RouteModifications;
        if (previousRoutes.Length != currentRoutes.Length)
        {
            return false;
        }

        for (int i = 0; i < previousRoutes.Length; i++)
        {
            if (previousRoutes[i] != currentRoutes[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int FloatHash(float value)
    {
        return unchecked((int)BitConverter.SingleToUInt32Bits(value));
    }
}
