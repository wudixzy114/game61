using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    private readonly TerrainRouteGraphNode[] _nodes;
    private readonly TerrainRouteGraphEdge[] _edges;
    private readonly Dictionary<int, TerrainRouteGraphNode> _nodeLookup;
    private readonly Dictionary<int, TerrainRouteGraphEdge[]> _outgoingEdges;

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
        _nodes = nodes.Length == 0 ? [] : (TerrainRouteGraphNode[])nodes.Clone();
        _edges = CopyEdges(edges);
        _nodeLookup = BuildNodeLookup(_nodes);
        _outgoingEdges = BuildOutgoingEdges(_edges);
    }

    public Vector2 Center { get; }
    public float WorldSize { get; }
    public TerrainRouteGraphNode[] Nodes => _nodes.Length == 0 ? [] : (TerrainRouteGraphNode[])_nodes.Clone();
    public TerrainRouteGraphEdge[] Edges => CopyEdges(_edges);

    /// <summary>Returns whether the snapshot contains a route-graph node for the requested point of interest.</summary>
    public bool ContainsPoint(int pointId)
    {
        return _nodeLookup.ContainsKey(pointId);
    }

    /// <summary>Returns a copied route-graph node for the requested point of interest when present.</summary>
    public bool TryGetNode(int pointId, out TerrainRouteGraphNode node)
    {
        return _nodeLookup.TryGetValue(pointId, out node);
    }

    /// <summary>Returns edges adjacent to the requested point, oriented away from that point for high-level path traversal.</summary>
    public TerrainRouteGraphEdge[] QueryConnectedEdges(int pointId)
    {
        return _outgoingEdges.TryGetValue(pointId, out TerrainRouteGraphEdge[]? edges)
            ? CopyEdges(edges)
            : [];
    }

    /// <summary>Finds the lowest-cost high-level route path between two planned points of interest.</summary>
    public bool TryFindPath(int fromPointId, int toPointId, [NotNullWhen(true)] out TerrainRouteGraphPath? path)
    {
        path = null;
        if (!_nodeLookup.TryGetValue(fromPointId, out TerrainRouteGraphNode fromNode) ||
            !_nodeLookup.ContainsKey(toPointId))
        {
            return false;
        }

        if (fromPointId == toPointId)
        {
            path = new TerrainRouteGraphPath(
                fromPointId,
                toPointId,
                [fromPointId],
                [],
                [fromNode.WorldPosition],
                totalCost: 0.0f,
                totalDistance: 0.0f);
            return true;
        }

        var frontier = new PriorityQueue<int, float>();
        var bestCosts = new Dictionary<int, float>(_nodeLookup.Count)
        {
            [fromPointId] = 0.0f
        };
        var previousEdges = new Dictionary<int, TerrainRouteGraphEdge>(_nodeLookup.Count);
        frontier.Enqueue(fromPointId, 0.0f);

        while (frontier.Count > 0)
        {
            frontier.TryDequeue(out int currentPointId, out float queuedCost);
            if (!bestCosts.TryGetValue(currentPointId, out float currentBestCost) ||
                queuedCost > currentBestCost + 0.0001f)
            {
                continue;
            }

            if (currentPointId == toPointId)
            {
                break;
            }

            if (!_outgoingEdges.TryGetValue(currentPointId, out TerrainRouteGraphEdge[]? connectedEdges))
            {
                continue;
            }

            foreach (TerrainRouteGraphEdge edge in connectedEdges)
            {
                float edgeCost = SafeEdgeCost(edge);
                if (float.IsPositiveInfinity(edgeCost))
                {
                    continue;
                }

                float nextCost = currentBestCost + edgeCost;
                int nextPointId = edge.ToPointId;
                if (bestCosts.TryGetValue(nextPointId, out float knownCost) &&
                    nextCost + 0.0001f >= knownCost)
                {
                    continue;
                }

                bestCosts[nextPointId] = nextCost;
                previousEdges[nextPointId] = edge;
                frontier.Enqueue(nextPointId, nextCost);
            }
        }

        if (!bestCosts.TryGetValue(toPointId, out float totalCost))
        {
            return false;
        }

        var pathEdges = new List<TerrainRouteGraphEdge>();
        var pointIds = new List<int> { toPointId };
        int cursor = toPointId;
        while (cursor != fromPointId)
        {
            if (!previousEdges.TryGetValue(cursor, out TerrainRouteGraphEdge edge))
            {
                path = null;
                return false;
            }

            pathEdges.Add(edge);
            cursor = edge.FromPointId;
            pointIds.Add(cursor);
        }

        pathEdges.Reverse();
        pointIds.Reverse();

        TerrainRouteGraphEdge[] edgeArray = pathEdges.ToArray();
        path = new TerrainRouteGraphPath(
            fromPointId,
            toPointId,
            pointIds.ToArray(),
            edgeArray,
            BuildPathWaypoints(edgeArray),
            totalCost,
            ComputePathDistance(edgeArray));
        return true;
    }

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

    private static Dictionary<int, TerrainRouteGraphNode> BuildNodeLookup(TerrainRouteGraphNode[] nodes)
    {
        var lookup = new Dictionary<int, TerrainRouteGraphNode>(nodes.Length);
        for (int i = 0; i < nodes.Length; i++)
        {
            lookup[nodes[i].PointId] = nodes[i];
        }

        return lookup;
    }

    private static Dictionary<int, TerrainRouteGraphEdge[]> BuildOutgoingEdges(TerrainRouteGraphEdge[] edges)
    {
        var lists = new Dictionary<int, List<TerrainRouteGraphEdge>>();
        for (int i = 0; i < edges.Length; i++)
        {
            TerrainRouteGraphEdge forward = CreateDirectedEdge(edges[i], reverse: false);
            TerrainRouteGraphEdge reverse = CreateDirectedEdge(edges[i], reverse: true);
            AddDirectedEdge(lists, forward);
            AddDirectedEdge(lists, reverse);
        }

        var frozen = new Dictionary<int, TerrainRouteGraphEdge[]>(lists.Count);
        foreach (KeyValuePair<int, List<TerrainRouteGraphEdge>> pair in lists)
        {
            pair.Value.Sort(CompareConnectedEdges);
            frozen[pair.Key] = pair.Value.ToArray();
        }

        return frozen;
    }

    private static void AddDirectedEdge(
        Dictionary<int, List<TerrainRouteGraphEdge>> lists,
        TerrainRouteGraphEdge edge)
    {
        if (!lists.TryGetValue(edge.FromPointId, out List<TerrainRouteGraphEdge>? connected))
        {
            connected = new List<TerrainRouteGraphEdge>(4);
            lists.Add(edge.FromPointId, connected);
        }

        connected.Add(edge);
    }

    private static int CompareConnectedEdges(TerrainRouteGraphEdge a, TerrainRouteGraphEdge b)
    {
        int cost = a.Cost.CompareTo(b.Cost);
        if (cost != 0)
        {
            return cost;
        }

        int scenic = b.AverageScenicPotential.CompareTo(a.AverageScenicPotential);
        if (scenic != 0)
        {
            return scenic;
        }

        return a.ToPointId.CompareTo(b.ToPointId);
    }

    private static TerrainRouteGraphEdge CreateDirectedEdge(TerrainRouteGraphEdge edge, bool reverse)
    {
        Vector2[] waypoints = reverse
            ? ReverseWaypoints(edge.Waypoints)
            : CopyWaypoints(edge.Waypoints);
        return reverse
            ? new TerrainRouteGraphEdge(
                edge.ToPointId,
                edge.FromPointId,
                edge.Kind,
                edge.Cost,
                edge.AverageScenicPotential,
                edge.AverageTraversability,
                edge.CoreWidth,
                edge.ShoulderWidth,
                waypoints)
            : new TerrainRouteGraphEdge(
                edge.FromPointId,
                edge.ToPointId,
                edge.Kind,
                edge.Cost,
                edge.AverageScenicPotential,
                edge.AverageTraversability,
                edge.CoreWidth,
                edge.ShoulderWidth,
                waypoints);
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

    private static Vector2[] CopyWaypoints(Vector2[] waypoints)
    {
        return waypoints.Length == 0
            ? []
            : (Vector2[])waypoints.Clone();
    }

    private static Vector2[] ReverseWaypoints(Vector2[] waypoints)
    {
        if (waypoints.Length == 0)
        {
            return [];
        }

        Vector2[] reversed = (Vector2[])waypoints.Clone();
        Array.Reverse(reversed);
        return reversed;
    }

    private static float SafeEdgeCost(TerrainRouteGraphEdge edge)
    {
        return float.IsFinite(edge.Cost)
            ? Mathf.Max(0.0f, edge.Cost)
            : float.PositiveInfinity;
    }

    private Vector2[] BuildPathWaypoints(TerrainRouteGraphEdge[] edges)
    {
        if (edges.Length == 0)
        {
            return [];
        }

        var points = new List<Vector2>(edges.Length * 6);
        foreach (TerrainRouteGraphEdge edge in edges)
        {
            if (edge.Waypoints.Length == 0)
            {
                if (_nodeLookup.TryGetValue(edge.FromPointId, out TerrainRouteGraphNode fromNode))
                {
                    AppendWaypoint(points, fromNode.WorldPosition);
                }

                if (_nodeLookup.TryGetValue(edge.ToPointId, out TerrainRouteGraphNode toNode))
                {
                    AppendWaypoint(points, toNode.WorldPosition);
                }

                continue;
            }

            for (int i = 0; i < edge.Waypoints.Length; i++)
            {
                AppendWaypoint(points, edge.Waypoints[i]);
            }
        }

        return points.Count == 0 ? [] : points.ToArray();
    }

    private static void AppendWaypoint(List<Vector2> points, Vector2 waypoint)
    {
        if (points.Count == 0)
        {
            points.Add(waypoint);
            return;
        }

        Vector2 last = points[^1];
        if (last.DistanceSquaredTo(waypoint) > 0.0001f)
        {
            points.Add(waypoint);
        }
    }

    private float ComputePathDistance(TerrainRouteGraphEdge[] edges)
    {
        float total = 0.0f;
        for (int i = 0; i < edges.Length; i++)
        {
            total += ComputeEdgeDistance(edges[i]);
        }

        return total;
    }

    private float ComputeEdgeDistance(TerrainRouteGraphEdge edge)
    {
        Vector2[] waypoints = edge.Waypoints;
        if (waypoints.Length >= 2)
        {
            float total = 0.0f;
            for (int i = 1; i < waypoints.Length; i++)
            {
                total += waypoints[i - 1].DistanceTo(waypoints[i]);
            }

            return total;
        }

        if (_nodeLookup.TryGetValue(edge.FromPointId, out TerrainRouteGraphNode fromNode) &&
            _nodeLookup.TryGetValue(edge.ToPointId, out TerrainRouteGraphNode toNode))
        {
            return fromNode.WorldPosition.DistanceTo(toNode.WorldPosition);
        }

        return 0.0f;
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

/// <summary>Stable high-level route path returned from a route-graph snapshot query.</summary>
public sealed class TerrainRouteGraphPath
{
    private readonly int[] _pointIds;
    private readonly TerrainRouteGraphEdge[] _edges;
    private readonly Vector2[] _waypoints;

    public TerrainRouteGraphPath(
        int startPointId,
        int goalPointId,
        int[] pointIds,
        TerrainRouteGraphEdge[] edges,
        Vector2[] waypoints,
        float totalCost,
        float totalDistance)
    {
        StartPointId = startPointId;
        GoalPointId = goalPointId;
        _pointIds = pointIds.Length == 0 ? [] : (int[])pointIds.Clone();
        _edges = edges.Length == 0 ? [] : CopyEdges(edges);
        _waypoints = waypoints.Length == 0 ? [] : (Vector2[])waypoints.Clone();
        TotalCost = totalCost;
        TotalDistance = totalDistance;
    }

    public int StartPointId { get; }
    public int GoalPointId { get; }
    public int[] PointIds => _pointIds.Length == 0 ? [] : (int[])_pointIds.Clone();
    public TerrainRouteGraphEdge[] Edges => _edges.Length == 0 ? [] : CopyEdges(_edges);
    public Vector2[] Waypoints => _waypoints.Length == 0 ? [] : (Vector2[])_waypoints.Clone();
    public float TotalCost { get; }
    public float TotalDistance { get; }

    private static TerrainRouteGraphEdge[] CopyEdges(TerrainRouteGraphEdge[] edges)
    {
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
}
