using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Runtime;

/// <summary>Runtime node for a planned route, exposing cost, scenic potential, and waypoint data via Godot meta properties.</summary>
public partial class TerrainWorldRouteAnchor : Node3D
{
    public const string GroupName = TerrainWorldAnchorContract.RouteGroup;
    public const string MetaKeyKind = TerrainWorldAnchorContract.RouteMetaKeyKind;
    public const string MetaKeyFrom = TerrainWorldAnchorContract.RouteMetaKeyFrom;
    public const string MetaKeyTo = TerrainWorldAnchorContract.RouteMetaKeyTo;
    public const string MetaKeyCost = TerrainWorldAnchorContract.RouteMetaKeyCost;
    public const string MetaKeyScenic = TerrainWorldAnchorContract.RouteMetaKeyScenic;
    public const string MetaKeyTraversability = TerrainWorldAnchorContract.RouteMetaKeyTraversability;

    public static string[] RequiredMetaKeys => TerrainWorldAnchorContract.GetRouteRequiredMetaKeys();

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
        TerrainWorldRouteAnchorDescriptor descriptor = TerrainWorldAnchorContract.CreateRouteDescriptor(route);
        Configure(descriptor, worldPosition);
    }

    /// <summary>Configures this anchor from a stable gameplay descriptor and places it at the midpoint.</summary>
    public void Configure(TerrainWorldRouteAnchorDescriptor descriptor, Vector3 worldPosition)
    {
        FromPointId = descriptor.FromPointId;
        ToPointId = descriptor.ToPointId;
        Kind = descriptor.Kind;
        Cost = descriptor.Cost;
        AverageScenicPotential = descriptor.AverageScenicPotential;
        AverageTraversability = descriptor.AverageTraversability;
        Waypoints = descriptor.Waypoints.Length == 0
            ? []
            : (Vector2[])descriptor.Waypoints.Clone();
        WorldMidpoint2D = descriptor.WorldMidpoint2D;

        Name = descriptor.Name;
        GlobalPosition = worldPosition;
        AddToGroup(descriptor.GroupName);
        SetMeta(MetaKeyKind, Kind.ToString());
        SetMeta(MetaKeyFrom, FromPointId);
        SetMeta(MetaKeyTo, ToPointId);
        SetMeta(MetaKeyCost, Cost);
        SetMeta(MetaKeyScenic, AverageScenicPotential);
        SetMeta(MetaKeyTraversability, AverageTraversability);
    }
}
