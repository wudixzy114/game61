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

}
