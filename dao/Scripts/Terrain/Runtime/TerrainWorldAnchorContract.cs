using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Runtime;

/// <summary>Stable gameplay anchor group, meta, and descriptor contract shared by runtime nodes and validation tools.</summary>
public static class TerrainWorldAnchorContract
{
    public const string PointOfInterestGroup = "terrain_poi";
    public const string PointOfInterestMetaKeyId = "terrain_poi_id";
    public const string PointOfInterestMetaKeyKind = "terrain_poi_kind";
    public const string PointOfInterestMetaKeyVisual = "terrain_poi_visual";
    public const string PointOfInterestMetaKeyGameplayTag = "terrain_poi_gameplay_tag";
    public const string PointOfInterestMetaKeyScore = "terrain_poi_score";
    public const string PointOfInterestMetaKeyScenic = "terrain_poi_scenic";
    public const string PointOfInterestMetaKeyTraversability = "terrain_poi_traversability";
    public const string PointOfInterestMetaKeySettlementTier = "terrain_poi_settlement_tier";
    public const string PointOfInterestMetaKeyLandscape = "terrain_poi_landscape";
    public const string PointOfInterestMetaKeyInteractionRadius = "terrain_poi_interaction_radius";
    public const string PointOfInterestMetaKeyEncounterBudget = "terrain_poi_encounter_budget";

    public const string RouteGroup = "terrain_route";
    public const string RouteMetaKeyKind = "terrain_route_kind";
    public const string RouteMetaKeyFrom = "terrain_route_from";
    public const string RouteMetaKeyTo = "terrain_route_to";
    public const string RouteMetaKeyCost = "terrain_route_cost";
    public const string RouteMetaKeyScenic = "terrain_route_scenic";
    public const string RouteMetaKeyTraversability = "terrain_route_traversability";

    private static readonly string[] PointOfInterestMetaKeys =
    [
        PointOfInterestMetaKeyId,
        PointOfInterestMetaKeyKind,
        PointOfInterestMetaKeyVisual,
        PointOfInterestMetaKeyGameplayTag,
        PointOfInterestMetaKeyScore,
        PointOfInterestMetaKeyScenic,
        PointOfInterestMetaKeyTraversability,
        PointOfInterestMetaKeySettlementTier,
        PointOfInterestMetaKeyLandscape,
        PointOfInterestMetaKeyInteractionRadius,
        PointOfInterestMetaKeyEncounterBudget
    ];

    private static readonly string[] RouteMetaKeys =
    [
        RouteMetaKeyKind,
        RouteMetaKeyFrom,
        RouteMetaKeyTo,
        RouteMetaKeyCost,
        RouteMetaKeyScenic,
        RouteMetaKeyTraversability
    ];

    public static string[] GetPointOfInterestRequiredMetaKeys()
    {
        return (string[])PointOfInterestMetaKeys.Clone();
    }

    public static string[] GetRouteRequiredMetaKeys()
    {
        return (string[])RouteMetaKeys.Clone();
    }

    public static TerrainWorldPointOfInterestAnchorDescriptor CreatePointOfInterestDescriptor(
        TerrainWorldPointOfInterest point)
    {
        TerrainPointOfInterestArchetype archetype = TerrainPointOfInterestArchetypeCatalog.Get(point.Kind);
        TerrainPointOfInterestVisualKind visualKind = TerrainPointOfInterestArchetypeCatalog.VisualKindFor(point);
        return new TerrainWorldPointOfInterestAnchorDescriptor(
            Name: $"POI_{point.Id:00}_{point.Kind}",
            GroupName: PointOfInterestGroup,
            GameplayTagGroup: archetype.GameplayTag,
            Id: point.Id,
            Kind: point.Kind,
            WorldPosition2D: point.WorldPosition,
            Score: point.Score,
            Height: point.Height,
            ScenicPotential: point.ScenicPotential,
            Traversability: point.Traversability,
            SettlementTier: point.SettlementTier,
            LandscapeKind: point.LandscapeKind,
            VisualKind: visualKind,
            GameplayTag: archetype.GameplayTag,
            InteractionRadius: archetype.InteractionRadius,
            EncounterBudget: archetype.EncounterBudget);
    }

    public static TerrainWorldPointOfInterestAnchorDescriptor[] CreatePointOfInterestDescriptors(TerrainWorldPlan plan)
    {
        if (plan.PointsOfInterest.Length == 0)
        {
            return [];
        }

        var descriptors = new TerrainWorldPointOfInterestAnchorDescriptor[plan.PointsOfInterest.Length];
        for (int i = 0; i < plan.PointsOfInterest.Length; i++)
        {
            descriptors[i] = CreatePointOfInterestDescriptor(plan.PointsOfInterest[i]);
        }

        return descriptors;
    }

    public static TerrainWorldRouteAnchorDescriptor CreateRouteDescriptor(TerrainWorldRoute route)
    {
        Vector2[] waypoints = route.Waypoints.Length == 0
            ? []
            : (Vector2[])route.Waypoints.Clone();
        Vector2 midpoint = route.Waypoints.Length == 0
            ? Vector2.Zero
            : route.Waypoints[route.Waypoints.Length / 2];
        return new TerrainWorldRouteAnchorDescriptor(
            Name: $"Route_{route.FromPointId:00}_{route.ToPointId:00}_{route.Kind}",
            GroupName: RouteGroup,
            FromPointId: route.FromPointId,
            ToPointId: route.ToPointId,
            Kind: route.Kind,
            Cost: route.Cost,
            AverageScenicPotential: route.AverageScenicPotential,
            AverageTraversability: route.AverageTraversability,
            WorldMidpoint2D: midpoint,
            Waypoints: waypoints);
    }

    public static TerrainWorldRouteAnchorDescriptor[] CreateRouteDescriptors(TerrainWorldPlan plan)
    {
        if (plan.Routes.Length == 0)
        {
            return [];
        }

        var descriptors = new TerrainWorldRouteAnchorDescriptor[plan.Routes.Length];
        for (int i = 0; i < plan.Routes.Length; i++)
        {
            descriptors[i] = CreateRouteDescriptor(plan.Routes[i]);
        }

        return descriptors;
    }
}

public readonly record struct TerrainWorldPointOfInterestAnchorDescriptor(
    string Name,
    string GroupName,
    string GameplayTagGroup,
    int Id,
    TerrainPointOfInterestKind Kind,
    Vector2 WorldPosition2D,
    float Score,
    float Height,
    float ScenicPotential,
    float Traversability,
    TerrainSettlementTier SettlementTier,
    TerrainLandscapeKind LandscapeKind,
    TerrainPointOfInterestVisualKind VisualKind,
    string GameplayTag,
    float InteractionRadius,
    int EncounterBudget);

public readonly record struct TerrainWorldRouteAnchorDescriptor(
    string Name,
    string GroupName,
    int FromPointId,
    int ToPointId,
    TerrainRouteKind Kind,
    float Cost,
    float AverageScenicPotential,
    float AverageTraversability,
    Vector2 WorldMidpoint2D,
    Vector2[] Waypoints);
