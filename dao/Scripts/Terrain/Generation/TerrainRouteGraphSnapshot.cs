using System;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>A route-graph node derived from a planned point of interest for navigation and world-logic handoff.</summary>
public readonly record struct TerrainRouteGraphNode(
    int PointId,
    Vector2 WorldPosition,
    TerrainPointOfInterestKind Kind,
    TerrainSettlementTier SettlementTier,
    float Score);

/// <summary>A route-graph edge with route metadata and copied waypoint geometry for navigation handoff.</summary>
public readonly record struct TerrainRouteGraphEdge(
    int FromPointId,
    int ToPointId,
    TerrainRouteKind Kind,
    float Cost,
    float AverageScenicPotential,
    float AverageTraversability,
    float CoreWidth,
    float ShoulderWidth,
    Vector2[] Waypoints);

/// <summary>Read-only snapshot of the plan's route graph suitable for AI, map tools, and navigation graph importers.</summary>
public sealed class TerrainRouteGraphSnapshot
{
    public static TerrainRouteGraphSnapshot Empty { get; } = new(
        Vector2.Zero,
        0.0f,
        [],
        []);

    public TerrainRouteGraphSnapshot(
        Vector2 center,
        float worldSize,
        TerrainRouteGraphNode[] nodes,
        TerrainRouteGraphEdge[] edges)
    {
        Center = center;
        WorldSize = worldSize;
        Nodes = nodes.Length == 0 ? [] : (TerrainRouteGraphNode[])nodes.Clone();
        Edges = CopyEdges(edges);
    }

    public Vector2 Center { get; }
    public float WorldSize { get; }
    public TerrainRouteGraphNode[] Nodes { get; }
    public TerrainRouteGraphEdge[] Edges { get; }

    public static TerrainRouteGraphSnapshot FromPlan(TerrainWorldPlan plan)
    {
        TerrainRouteGraphNode[] nodes = new TerrainRouteGraphNode[plan.PointsOfInterest.Length];
        for (int i = 0; i < plan.PointsOfInterest.Length; i++)
        {
            TerrainWorldPointOfInterest point = plan.PointsOfInterest[i];
            nodes[i] = new TerrainRouteGraphNode(
                point.Id,
                point.WorldPosition,
                point.Kind,
                point.SettlementTier,
                point.Score);
        }

        TerrainRouteGraphEdge[] edges = new TerrainRouteGraphEdge[plan.Routes.Length];
        for (int i = 0; i < plan.Routes.Length; i++)
        {
            TerrainWorldRoute route = plan.Routes[i];
            GetCorridorWidths(route.Kind, out float coreWidth, out float shoulderWidth);
            edges[i] = new TerrainRouteGraphEdge(
                route.FromPointId,
                route.ToPointId,
                route.Kind,
                route.Cost,
                route.AverageScenicPotential,
                route.AverageTraversability,
                coreWidth,
                shoulderWidth,
                route.Waypoints.Length == 0 ? [] : (Vector2[])route.Waypoints.Clone());
        }

        return new TerrainRouteGraphSnapshot(plan.Center, plan.WorldSize, nodes, edges);
    }

    private static TerrainRouteGraphEdge[] CopyEdges(TerrainRouteGraphEdge[] edges)
    {
        if (edges.Length == 0)
        {
            return [];
        }

        var copy = new TerrainRouteGraphEdge[edges.Length];
        for (int i = 0; i < edges.Length; i++)
        {
            Vector2[] waypoints = edges[i].Waypoints.Length == 0
                ? []
                : (Vector2[])edges[i].Waypoints.Clone();
            copy[i] = edges[i] with { Waypoints = waypoints };
        }

        return copy;
    }

    private static void GetCorridorWidths(TerrainRouteKind kind, out float coreWidth, out float shoulderWidth)
    {
        switch (kind)
        {
            case TerrainRouteKind.RiverRoad:
                coreWidth = 18.0f;
                shoulderWidth = 62.0f;
                return;
            case TerrainRouteKind.RidgePass:
                coreWidth = 11.0f;
                shoulderWidth = 42.0f;
                return;
            case TerrainRouteKind.CoastalPath:
                coreWidth = 20.0f;
                shoulderWidth = 72.0f;
                return;
            case TerrainRouteKind.ScenicTrail:
                coreWidth = 13.0f;
                shoulderWidth = 50.0f;
                return;
            default:
                coreWidth = 14.0f;
                shoulderWidth = 54.0f;
                return;
        }
    }
}
