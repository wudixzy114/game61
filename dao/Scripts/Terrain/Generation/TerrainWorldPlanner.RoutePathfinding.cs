using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanner
{
    private static TerrainWorldRoute? TryBuildRoute(
        TerrainWorldPointOfInterest from,
        TerrainWorldPointOfInterest to,
        TerrainWorldField[] fields,
        TerrainGenerationProfile profile,
        int resolution,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int start = Index(from.GridX, from.GridY, resolution);
        int goal = Index(to.GridX, to.GridY, resolution);
        int count = fields.Length;
        var frontier = new PriorityQueue<int, float>();
        var cameFrom = new int[count];
        var costSoFar = new float[count];

        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            cameFrom[i] = -1;
            costSoFar[i] = float.PositiveInfinity;
        }

        frontier.Enqueue(start, 0.0f);
        cameFrom[start] = start;
        costSoFar[start] = 0.0f;

        while (frontier.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int current = frontier.Dequeue();
            if (current == goal)
            {
                break;
            }

            int currentX = current % resolution;
            int currentY = current / resolution;
            TerrainWorldField currentField = fields[current];

            for (int oy = -1; oy <= 1; oy++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0)
                    {
                        continue;
                    }

                    int nx = currentX + ox;
                    int ny = currentY + oy;
                    if (!InBounds(nx, ny, resolution))
                    {
                        continue;
                    }

                    int next = Index(nx, ny, resolution);
                    TerrainWorldField nextField = fields[next];
                    float moveCost = StepCost(currentField, nextField, profile, ox != 0 && oy != 0);
                    if (moveCost >= ImpassableCost)
                    {
                        continue;
                    }

                    float newCost = costSoFar[current] + moveCost;
                    if (newCost >= costSoFar[next])
                    {
                        continue;
                    }

                    costSoFar[next] = newCost;
                    cameFrom[next] = current;
                    float heuristic = Mathf.Abs(nx - to.GridX) + Mathf.Abs(ny - to.GridY);
                    frontier.Enqueue(next, newCost + heuristic);
                }
            }
        }

        if (cameFrom[goal] < 0)
        {
            return null;
        }

        List<int> path = ReconstructPath(goal, cameFrom);
        if (path.Count < 2)
        {
            return null;
        }

        Vector2[] waypoints = BuildWaypoints(path, fields);
        float scenic = 0.0f;
        float traversability = 0.0f;
        float river = 0.0f;
        float highland = 0.0f;
        float coast = 0.0f;
        float water = 0.0f;

        foreach (int index in path)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TerrainWorldField field = fields[index];
            scenic += field.ScenicPotential;
            traversability += field.Traversability;
            river += field.River;
            highland += field.LandscapeKind is TerrainLandscapeKind.Highlands or TerrainLandscapeKind.MountainMassif or TerrainLandscapeKind.VistaPlateau ? 1.0f : 0.0f;
            coast += field.LandscapeKind == TerrainLandscapeKind.Coast ? 1.0f : 0.0f;
            water += field.Height < profile.SeaLevel - 4.0f ? 1.0f : 0.0f;
        }

        float invCount = 1.0f / path.Count;
        scenic *= invCount;
        traversability *= invCount;
        river *= invCount;
        highland *= invCount;
        coast *= invCount;
        water *= invCount;

        return new TerrainWorldRoute(
            from.Id,
            to.Id,
            ClassifyRoute(from, to, scenic, river, highland, coast, water),
            costSoFar[goal],
            scenic,
            traversability,
            waypoints);
    }

    private static float StepCost(
        TerrainWorldField current,
        TerrainWorldField next,
        TerrainGenerationProfile profile,
        bool diagonal)
    {
        float waterDepth = profile.SeaLevel - next.Height;
        if (waterDepth > profile.HeightScale * 0.62f)
        {
            return ImpassableCost;
        }

        float baseCost = diagonal ? 1.4142f : 1.0f;
        float traversabilityPenalty = (1.0f - next.Traversability) * 4.5f;
        float heightDeltaPenalty = Mathf.Clamp(Mathf.Abs(next.Height - current.Height) / Mathf.Max(1.0f, profile.HeightScale * 0.18f), 0.0f, 4.0f);
        float riverPenalty = next.River > 0.72f ? 1.4f : next.River * 0.38f;
        float waterPenalty = waterDepth > 4.0f ? 5.8f + Mathf.Clamp(waterDepth / 90.0f, 0.0f, 5.5f) : 0.0f;
        float scenicBonus = next.ScenicPotential * 0.18f;

        return baseCost * Mathf.Max(0.35f, 1.0f + traversabilityPenalty + heightDeltaPenalty + riverPenalty + waterPenalty - scenicBonus);
    }

    private static List<int> ReconstructPath(int goal, int[] cameFrom)
    {
        var path = new List<int>();
        int current = goal;
        while (cameFrom[current] != current)
        {
            path.Add(current);
            current = cameFrom[current];
            if (current < 0)
            {
                break;
            }
        }

        if (current >= 0)
        {
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static Vector2[] BuildWaypoints(List<int> path, TerrainWorldField[] fields)
    {
        int stride = Mathf.Max(1, path.Count / 48);
        var waypoints = new List<Vector2>(Mathf.Min(path.Count, 64));

        for (int i = 0; i < path.Count; i += stride)
        {
            waypoints.Add(fields[path[i]].WorldPosition);
        }

        Vector2 last = fields[path[^1]].WorldPosition;
        if (waypoints.Count == 0 || waypoints[^1] != last)
        {
            waypoints.Add(last);
        }

        return waypoints.ToArray();
    }

    private static TerrainRouteKind ClassifyRoute(
        TerrainWorldPointOfInterest from,
        TerrainWorldPointOfInterest to,
        float scenic,
        float river,
        float highland,
        float coast,
        float water)
    {
        if (water > 0.12f || coast > 0.32f || from.Kind == TerrainPointOfInterestKind.CoastalLanding || to.Kind == TerrainPointOfInterestKind.CoastalLanding)
        {
            return TerrainRouteKind.CoastalPath;
        }

        if (river > 0.55f)
        {
            return TerrainRouteKind.RiverRoad;
        }

        if (highland > 0.55f)
        {
            return TerrainRouteKind.RidgePass;
        }

        if (scenic > 0.62f || from.Kind == TerrainPointOfInterestKind.Vista || to.Kind == TerrainPointOfInterestKind.Vista)
        {
            return TerrainRouteKind.ScenicTrail;
        }

        if (river > 0.34f || from.Kind == TerrainPointOfInterestKind.RiverCrossing || to.Kind == TerrainPointOfInterestKind.RiverCrossing)
        {
            return TerrainRouteKind.RiverRoad;
        }

        if (highland > 0.34f || from.Kind == TerrainPointOfInterestKind.MountainPass || to.Kind == TerrainPointOfInterestKind.MountainPass)
        {
            return TerrainRouteKind.RidgePass;
        }

        return TerrainRouteKind.PrimaryTrail;
    }

    private static long PointPairKey(int a, int b)
    {
        int min = Math.Min(a, b);
        int max = Math.Max(a, b);
        return ((long)min << 32) | (uint)max;
    }

    private static bool IsSettlementHub(TerrainWorldPointOfInterest point)
    {
        return point.SettlementTier is TerrainSettlementTier.Village or TerrainSettlementTier.Town or TerrainSettlementTier.OasisHub;
    }

    private static bool IsSettlementHub(TerrainWorldPointOfInterest[] points, int pointId)
    {
        return (uint)pointId < (uint)points.Length && IsSettlementHub(points[pointId]);
    }

    private static int CountSettlementHubs(TerrainWorldPointOfInterest[] points)
    {
        int count = 0;
        foreach (TerrainWorldPointOfInterest point in points)
        {
            if (IsSettlementHub(point))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountSettlementRoutes(
        TerrainWorldPointOfInterest[] points,
        List<TerrainWorldRoute> routes)
    {
        int count = 0;
        foreach (TerrainWorldRoute route in routes)
        {
            if (IsSettlementHub(points, route.FromPointId) && IsSettlementHub(points, route.ToPointId))
            {
                count++;
            }
        }

        return count;
    }

    private static float SettlementTierRouteWeight(TerrainSettlementTier tier)
    {
        return tier switch
        {
            TerrainSettlementTier.Town => 1.0f,
            TerrainSettlementTier.OasisHub => 0.94f,
            TerrainSettlementTier.Village => 0.76f,
            _ => 0.0f
        };
    }
}
