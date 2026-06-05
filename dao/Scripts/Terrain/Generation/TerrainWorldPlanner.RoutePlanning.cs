using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainWorldPlanner
{
    private static TerrainWorldRoute[] BuildRoutes(
        TerrainWorldPointOfInterest[] points,
        TerrainWorldField[] fields,
        TerrainGenerationProfile profile,
        int resolution,
        int maxRoutes,
        CancellationToken cancellationToken)
    {
        if (points.Length < 2 || maxRoutes == 0)
        {
            return [];
        }

        var routes = new List<TerrainWorldRoute>(maxRoutes);
        var connected = new HashSet<int> { points[0].Id };
        var remaining = new HashSet<int>();
        for (int i = 1; i < points.Length; i++)
        {
            remaining.Add(points[i].Id);
        }

        while (remaining.Count > 0 && routes.Count < maxRoutes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TerrainWorldRoute? bestRoute = null;
            int bestTo = -1;
            float bestScore = float.PositiveInfinity;

            foreach (int from in connected)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (int to in remaining)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    float distance = points[from].WorldPosition.DistanceTo(points[to].WorldPosition);
                    float score = distance * (1.0f - Mathf.Min(points[from].Score, points[to].Score) * 0.25f);
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    TerrainWorldRoute? route = TryBuildRoute(points[from], points[to], fields, profile, resolution, cancellationToken);
                    if (route is null)
                    {
                        continue;
                    }

                    bestRoute = route.Value;
                    bestTo = to;
                    bestScore = score;
                }
            }

            if (bestRoute is null || bestTo < 0)
            {
                break;
            }

            routes.Add(bestRoute.Value);
            connected.Add(bestTo);
            remaining.Remove(bestTo);
        }

        AddSecondaryRoutes(points, fields, profile, resolution, maxRoutes, routes, cancellationToken);
        return routes.ToArray();
    }

    private static void AddSecondaryRoutes(
        TerrainWorldPointOfInterest[] points,
        TerrainWorldField[] fields,
        TerrainGenerationProfile profile,
        int resolution,
        int maxRoutes,
        List<TerrainWorldRoute> routes,
        CancellationToken cancellationToken)
    {
        if (routes.Count >= maxRoutes || points.Length < 3)
        {
            return;
        }

        TerrainSecondaryRoutePolicy rules = TerrainWorldPlannerRules.SecondaryRoutes;
        var existingEdges = new HashSet<long>();
        var routeDegree = new int[points.Length];
        foreach (TerrainWorldRoute route in routes)
        {
            existingEdges.Add(PointPairKey(route.FromPointId, route.ToPointId));
            if ((uint)route.FromPointId < (uint)routeDegree.Length)
            {
                routeDegree[route.FromPointId]++;
            }

            if ((uint)route.ToPointId < (uint)routeDegree.Length)
            {
                routeDegree[route.ToPointId]++;
            }
        }

        AddSettlementConnectorRoutes(points, fields, profile, resolution, maxRoutes, routes, existingEdges, routeDegree, cancellationToken);
        if (routes.Count >= maxRoutes)
        {
            return;
        }

        var candidates = new List<SecondaryRouteCandidate>(points.Length * 4);
        float minDistance = profile.ChunkSize * rules.MinDistanceChunks;
        float idealDistance = profile.ChunkSize * rules.IdealDistanceChunks;
        float maxDistance = profile.ChunkSize * rules.MaxDistanceChunks;

        for (int from = 0; from < points.Length - 1; from++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int to = from + 1; to < points.Length; to++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long key = PointPairKey(points[from].Id, points[to].Id);
                if (existingEdges.Contains(key))
                {
                    continue;
                }

                float distance = points[from].WorldPosition.DistanceTo(points[to].WorldPosition);
                if (distance < minDistance || distance > maxDistance)
                {
                    continue;
                }

                candidates.Add(new SecondaryRouteCandidate(
                    from,
                    to,
                    ScoreSecondaryRouteCandidate(points[from], points[to], routeDegree, distance, idealDistance)));
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

        int testedCandidates = 0;
        int maxCandidateTests = Mathf.Max(rules.MinCandidateTests, (maxRoutes - routes.Count) * rules.CandidateTestMultiplier);
        foreach (SecondaryRouteCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (routes.Count >= maxRoutes || testedCandidates >= maxCandidateTests)
            {
                break;
            }

            TerrainWorldPointOfInterest from = points[candidate.FromIndex];
            TerrainWorldPointOfInterest to = points[candidate.ToIndex];
            long key = PointPairKey(from.Id, to.Id);
            if (existingEdges.Contains(key))
            {
                continue;
            }

            testedCandidates++;
            TerrainWorldRoute? route = TryBuildRoute(from, to, fields, profile, resolution, cancellationToken);
            if (route is null)
            {
                continue;
            }

            routes.Add(route.Value);
            existingEdges.Add(key);
            routeDegree[from.Id]++;
            routeDegree[to.Id]++;
        }
    }

    private static void AddSettlementConnectorRoutes(
        TerrainWorldPointOfInterest[] points,
        TerrainWorldField[] fields,
        TerrainGenerationProfile profile,
        int resolution,
        int maxRoutes,
        List<TerrainWorldRoute> routes,
        HashSet<long> existingEdges,
        int[] routeDegree,
        CancellationToken cancellationToken)
    {
        int settlementCount = CountSettlementHubs(points);
        if (routes.Count >= maxRoutes || settlementCount < 2)
        {
            return;
        }

        TerrainSecondaryRoutePolicy rules = TerrainWorldPlannerRules.SettlementRoutes;
        int settlementRouteCount = CountSettlementRoutes(points, routes);
        int targetSettlementRoutes = Mathf.Min(maxRoutes, Mathf.Max(8, settlementCount - 1));
        if (settlementRouteCount >= targetSettlementRoutes)
        {
            return;
        }

        var candidates = new List<SecondaryRouteCandidate>(settlementCount * settlementCount);
        float minDistance = profile.ChunkSize * rules.MinDistanceChunks;
        float idealDistance = profile.ChunkSize * rules.IdealDistanceChunks;
        float maxDistance = profile.ChunkSize * rules.MaxDistanceChunks;

        for (int from = 0; from < points.Length - 1; from++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsSettlementHub(points[from]))
            {
                continue;
            }

            for (int to = from + 1; to < points.Length; to++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSettlementHub(points[to]))
                {
                    continue;
                }

                long key = PointPairKey(points[from].Id, points[to].Id);
                if (existingEdges.Contains(key))
                {
                    continue;
                }

                float distance = points[from].WorldPosition.DistanceTo(points[to].WorldPosition);
                if (distance < minDistance || distance > maxDistance)
                {
                    continue;
                }

                candidates.Add(new SecondaryRouteCandidate(
                    from,
                    to,
                    ScoreSettlementRouteCandidate(points[from], points[to], routeDegree, distance, idealDistance)));
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

        int testedCandidates = 0;
        int maxCandidateTests = Mathf.Max(rules.MinCandidateTests, (targetSettlementRoutes - settlementRouteCount) * rules.CandidateTestMultiplier);
        foreach (SecondaryRouteCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (routes.Count >= maxRoutes ||
                settlementRouteCount >= targetSettlementRoutes ||
                testedCandidates >= maxCandidateTests)
            {
                return;
            }

            TerrainWorldPointOfInterest from = points[candidate.FromIndex];
            TerrainWorldPointOfInterest to = points[candidate.ToIndex];
            long key = PointPairKey(from.Id, to.Id);
            if (existingEdges.Contains(key))
            {
                continue;
            }

            testedCandidates++;
            TerrainWorldRoute? route = TryBuildRoute(from, to, fields, profile, resolution, cancellationToken);
            if (route is null)
            {
                continue;
            }

            routes.Add(route.Value);
            existingEdges.Add(key);
            routeDegree[from.Id]++;
            routeDegree[to.Id]++;
            settlementRouteCount++;
        }
    }

    private static float ScoreSettlementRouteCandidate(
        TerrainWorldPointOfInterest from,
        TerrainWorldPointOfInterest to,
        int[] routeDegree,
        float distance,
        float idealDistance)
    {
        TerrainRouteScoreWeights weights = TerrainWorldPlannerRules.SettlementRouteScoring;
        float endpointScore = (from.Score + to.Score) * 0.5f;
        float traversalScore = (from.Traversability + to.Traversability) * 0.5f;
        int fromDegree = (uint)from.Id < (uint)routeDegree.Length ? routeDegree[from.Id] : 0;
        int toDegree = (uint)to.Id < (uint)routeDegree.Length ? routeDegree[to.Id] : 0;
        float underConnectedScore = 1.0f / (1.0f + Mathf.Min(fromDegree, toDegree));
        float tierImportance = (SettlementTierRouteWeight(from.SettlementTier) + SettlementTierRouteWeight(to.SettlementTier)) * 0.5f;
        float tierVariety = from.SettlementTier == to.SettlementTier ? 0.45f : 1.0f;
        float distanceScore = 1.0f - Mathf.Clamp(Mathf.Abs(distance - idealDistance) / Mathf.Max(1.0f, idealDistance), 0.0f, 1.0f);

        return
            endpointScore * weights.Endpoint +
            traversalScore * weights.Traversal +
            underConnectedScore * weights.UnderConnected +
            tierImportance * weights.TierImportance +
            tierVariety * weights.TierVariety +
            distanceScore * weights.Distance;
    }

    private static float ScoreSecondaryRouteCandidate(
        TerrainWorldPointOfInterest from,
        TerrainWorldPointOfInterest to,
        int[] routeDegree,
        float distance,
        float idealDistance)
    {
        TerrainRouteScoreWeights weights = TerrainWorldPlannerRules.SecondaryRouteScoring;
        float endpointScore = (from.Score + to.Score) * 0.5f;
        float scenicScore = (from.ScenicPotential + to.ScenicPotential) * 0.5f;
        float traversalScore = (from.Traversability + to.Traversability) * 0.5f;
        int fromDegree = (uint)from.Id < (uint)routeDegree.Length ? routeDegree[from.Id] : 0;
        int toDegree = (uint)to.Id < (uint)routeDegree.Length ? routeDegree[to.Id] : 0;
        float underConnectedScore = 1.0f / (1.0f + Mathf.Min(fromDegree, toDegree));
        float kindVariety = from.Kind == to.Kind ? 0.0f : 1.0f;
        float settlementBonus = IsSettlementHub(from) || IsSettlementHub(to) ? 0.08f : 0.0f;
        float distanceScore = 1.0f - Mathf.Clamp(Mathf.Abs(distance - idealDistance) / Mathf.Max(1.0f, idealDistance), 0.0f, 1.0f);

        return
            endpointScore * weights.Endpoint +
            scenicScore * weights.Scenic +
            traversalScore * weights.Traversal +
            underConnectedScore * weights.UnderConnected +
            kindVariety * weights.KindVariety +
            distanceScore * weights.Distance +
            settlementBonus * weights.SettlementBonus;
    }

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
