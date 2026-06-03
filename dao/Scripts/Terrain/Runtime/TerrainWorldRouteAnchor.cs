using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Runtime;

/// <summary>Runtime node for a planned route, exposing cost, scenic potential, and waypoint data via Godot meta properties.</summary>
public partial class TerrainWorldRouteAnchor : Node3D
{
    public int FromPointId { get; private set; }
    public int ToPointId { get; private set; }
    public TerrainRouteKind Kind { get; private set; }
    public float Cost { get; private set; }
    public float AverageScenicPotential { get; private set; }
    public float AverageTraversability { get; private set; }
    public Vector2 WorldMidpoint2D { get; private set; }
    public Vector2[] Waypoints { get; private set; } = [];

    /// <summary>Configures this anchor from route plan data and places it at the midpoint.</summary>
    public void Configure(TerrainWorldRoute route, Vector3 worldPosition)
    {
        FromPointId = route.FromPointId;
        ToPointId = route.ToPointId;
        Kind = route.Kind;
        Cost = route.Cost;
        AverageScenicPotential = route.AverageScenicPotential;
        AverageTraversability = route.AverageTraversability;
        Waypoints = route.Waypoints;
        WorldMidpoint2D = route.Waypoints.Length == 0
            ? Vector2.Zero
            : route.Waypoints[route.Waypoints.Length / 2];

        Name = $"Route_{FromPointId:00}_{ToPointId:00}_{Kind}";
        GlobalPosition = worldPosition;
        AddToGroup("terrain_route");
        SetMeta("terrain_route_kind", Kind.ToString());
        SetMeta("terrain_route_from", FromPointId);
        SetMeta("terrain_route_to", ToPointId);
        SetMeta("terrain_route_cost", Cost);
        SetMeta("terrain_route_scenic", AverageScenicPotential);
        SetMeta("terrain_route_traversability", AverageTraversability);
    }
}
