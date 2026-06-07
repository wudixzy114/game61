using System;
using System.Collections.Generic;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Waypoint node exported from the high-level route graph for AI and navigation importers.</summary>
public readonly record struct TerrainNavigationWaypointNode(
    int Id,
    Vector2 WorldPosition,
    bool IsPointOfInterest,
    int SourcePointId,
    TerrainPointOfInterestKind PointKind,
    TerrainSettlementTier SettlementTier,
    float Score);

/// <summary>Directed waypoint link exported from planned route geometry for AI and navigation importers.</summary>
public readonly record struct TerrainNavigationWaypointLink(
    int FromNodeId,
    int ToNodeId,
    TerrainRouteKind RouteKind,
    float Distance,
    float Cost,
    float AverageScenicPotential,
    float AverageTraversability,
    float CoreWidth,
    float ShoulderWidth);

/// <summary>Pure data waypoint graph converted from planned POIs and route waypoints.</summary>
public sealed class TerrainNavigationWaypointGraph
{
    private readonly TerrainNavigationWaypointNode[] _nodes;
    private readonly TerrainNavigationWaypointLink[] _links;
    private readonly Dictionary<int, TerrainNavigationWaypointNode> _nodeLookup;
    private readonly Dictionary<int, TerrainNavigationWaypointLink[]> _outgoingLinks;
    private readonly Dictionary<int, int> _pointNodeLookup;

    public static TerrainNavigationWaypointGraph Empty { get; } = new(
        Vector2.Zero,
        0.0f,
        [],
        []);

    public TerrainNavigationWaypointGraph(
        Vector2 center,
        float worldSize,
        TerrainNavigationWaypointNode[] nodes,
        TerrainNavigationWaypointLink[] links)
    {
        Center = center;
        WorldSize = worldSize;
        _nodes = nodes.Length == 0 ? [] : (TerrainNavigationWaypointNode[])nodes.Clone();
        _links = links.Length == 0 ? [] : (TerrainNavigationWaypointLink[])links.Clone();
        _nodeLookup = BuildNodeLookup(_nodes);
        _outgoingLinks = BuildOutgoingLinks(_links);
        _pointNodeLookup = BuildPointNodeLookup(_nodes);
    }

    public Vector2 Center { get; }
    public float WorldSize { get; }
    public TerrainNavigationWaypointNode[] Nodes => _nodes.Length == 0 ? [] : (TerrainNavigationWaypointNode[])_nodes.Clone();
    public TerrainNavigationWaypointLink[] Links => _links.Length == 0 ? [] : (TerrainNavigationWaypointLink[])_links.Clone();

    public bool ContainsNode(int nodeId)
    {
        return _nodeLookup.ContainsKey(nodeId);
    }

    public bool TryGetNode(int nodeId, out TerrainNavigationWaypointNode node)
    {
        return _nodeLookup.TryGetValue(nodeId, out node);
    }

    public bool TryGetPointNodeId(int pointId, out int nodeId)
    {
        return _pointNodeLookup.TryGetValue(pointId, out nodeId);
    }

    public TerrainNavigationWaypointLink[] QueryOutgoingLinks(int nodeId)
    {
        return _outgoingLinks.TryGetValue(nodeId, out TerrainNavigationWaypointLink[]? links)
            ? (TerrainNavigationWaypointLink[])links.Clone()
            : [];
    }

    public static TerrainNavigationWaypointGraph FromRouteGraph(TerrainRouteGraphSnapshot snapshot)
    {
        TerrainRouteGraphNode[] routeNodes = snapshot.Nodes;
        TerrainRouteGraphEdge[] routeEdges = snapshot.Edges;
        if (routeNodes.Length == 0)
        {
            return Empty;
        }

        var nodes = new List<TerrainNavigationWaypointNode>(routeNodes.Length + routeEdges.Length * 4);
        var links = new List<TerrainNavigationWaypointLink>(routeEdges.Length * 8);
        var pointNodeIds = new Dictionary<int, int>(routeNodes.Length);
        for (int i = 0; i < routeNodes.Length; i++)
        {
            TerrainRouteGraphNode routeNode = routeNodes[i];
            int nodeId = nodes.Count;
            nodes.Add(new TerrainNavigationWaypointNode(
                nodeId,
                routeNode.WorldPosition,
                IsPointOfInterest: true,
                routeNode.PointId,
                routeNode.Kind,
                routeNode.SettlementTier,
                routeNode.Score));
            pointNodeIds[routeNode.PointId] = nodeId;
        }

        for (int i = 0; i < routeEdges.Length; i++)
        {
            TerrainRouteGraphEdge routeEdge = routeEdges[i];
            if (!pointNodeIds.TryGetValue(routeEdge.FromPointId, out int fromNodeId) ||
                !pointNodeIds.TryGetValue(routeEdge.ToPointId, out int toNodeId))
            {
                continue;
            }

            AppendRoute(nodes, links, fromNodeId, toNodeId, routeEdge);
        }

        return new TerrainNavigationWaypointGraph(
            snapshot.Center,
            snapshot.WorldSize,
            nodes.ToArray(),
            links.ToArray());
    }

    public static TerrainNavigationWaypointGraph FromPlan(TerrainWorldPlan plan)
    {
        return FromRouteGraph(TerrainRouteGraphSnapshot.FromPlan(plan));
    }

    private static void AppendRoute(
        List<TerrainNavigationWaypointNode> nodes,
        List<TerrainNavigationWaypointLink> links,
        int fromNodeId,
        int toNodeId,
        TerrainRouteGraphEdge routeEdge)
    {
        int previousNodeId = fromNodeId;
        Vector2 previousPosition = nodes[fromNodeId].WorldPosition;
        Vector2[] waypoints = routeEdge.Waypoints;
        for (int i = 0; i < waypoints.Length; i++)
        {
            Vector2 waypoint = waypoints[i];
            if (previousPosition.DistanceSquaredTo(waypoint) <= 0.0001f ||
                nodes[toNodeId].WorldPosition.DistanceSquaredTo(waypoint) <= 0.0001f)
            {
                continue;
            }

            int waypointNodeId = nodes.Count;
            nodes.Add(new TerrainNavigationWaypointNode(
                waypointNodeId,
                waypoint,
                IsPointOfInterest: false,
                SourcePointId: -1,
                PointKind: TerrainPointOfInterestKind.SettlementCandidate,
                SettlementTier: TerrainSettlementTier.None,
                Score: 0.0f));
            AddBidirectionalLink(links, previousNodeId, waypointNodeId, previousPosition, waypoint, routeEdge);
            previousNodeId = waypointNodeId;
            previousPosition = waypoint;
        }

        AddBidirectionalLink(links, previousNodeId, toNodeId, previousPosition, nodes[toNodeId].WorldPosition, routeEdge);
    }

    private static void AddBidirectionalLink(
        List<TerrainNavigationWaypointLink> links,
        int fromNodeId,
        int toNodeId,
        Vector2 fromPosition,
        Vector2 toPosition,
        TerrainRouteGraphEdge routeEdge)
    {
        float distance = fromPosition.DistanceTo(toPosition);
        if (distance <= 0.0001f)
        {
            return;
        }

        float segmentCost = distance * RouteCostPerMeter(routeEdge);
        links.Add(CreateLink(fromNodeId, toNodeId, routeEdge, distance, segmentCost));
        links.Add(CreateLink(toNodeId, fromNodeId, routeEdge, distance, segmentCost));
    }

    private static TerrainNavigationWaypointLink CreateLink(
        int fromNodeId,
        int toNodeId,
        TerrainRouteGraphEdge routeEdge,
        float distance,
        float cost)
    {
        return new TerrainNavigationWaypointLink(
            fromNodeId,
            toNodeId,
            routeEdge.Kind,
            distance,
            cost,
            routeEdge.AverageScenicPotential,
            routeEdge.AverageTraversability,
            routeEdge.CoreWidth,
            routeEdge.ShoulderWidth);
    }

    private static float RouteCostPerMeter(TerrainRouteGraphEdge routeEdge)
    {
        float routeDistance = 0.0f;
        Vector2[] waypoints = routeEdge.Waypoints;
        for (int i = 1; i < waypoints.Length; i++)
        {
            routeDistance += waypoints[i - 1].DistanceTo(waypoints[i]);
        }

        if (routeDistance <= 0.0001f || !float.IsFinite(routeEdge.Cost))
        {
            return 1.0f;
        }

        return Mathf.Max(0.0001f, routeEdge.Cost / routeDistance);
    }

    private static Dictionary<int, TerrainNavigationWaypointNode> BuildNodeLookup(TerrainNavigationWaypointNode[] nodes)
    {
        var lookup = new Dictionary<int, TerrainNavigationWaypointNode>(nodes.Length);
        for (int i = 0; i < nodes.Length; i++)
        {
            lookup[nodes[i].Id] = nodes[i];
        }

        return lookup;
    }

    private static Dictionary<int, TerrainNavigationWaypointLink[]> BuildOutgoingLinks(TerrainNavigationWaypointLink[] links)
    {
        var lists = new Dictionary<int, List<TerrainNavigationWaypointLink>>();
        for (int i = 0; i < links.Length; i++)
        {
            TerrainNavigationWaypointLink link = links[i];
            if (!lists.TryGetValue(link.FromNodeId, out List<TerrainNavigationWaypointLink>? nodeLinks))
            {
                nodeLinks = new List<TerrainNavigationWaypointLink>(4);
                lists[link.FromNodeId] = nodeLinks;
            }

            nodeLinks.Add(link);
        }

        var frozen = new Dictionary<int, TerrainNavigationWaypointLink[]>(lists.Count);
        foreach (KeyValuePair<int, List<TerrainNavigationWaypointLink>> pair in lists)
        {
            pair.Value.Sort(CompareLinks);
            frozen[pair.Key] = pair.Value.ToArray();
        }

        return frozen;
    }

    private static int CompareLinks(TerrainNavigationWaypointLink a, TerrainNavigationWaypointLink b)
    {
        int cost = a.Cost.CompareTo(b.Cost);
        if (cost != 0)
        {
            return cost;
        }

        return a.ToNodeId.CompareTo(b.ToNodeId);
    }

    private static Dictionary<int, int> BuildPointNodeLookup(TerrainNavigationWaypointNode[] nodes)
    {
        var lookup = new Dictionary<int, int>();
        for (int i = 0; i < nodes.Length; i++)
        {
            TerrainNavigationWaypointNode node = nodes[i];
            if (node.IsPointOfInterest)
            {
                lookup[node.SourcePointId] = node.Id;
            }
        }

        return lookup;
    }
}
