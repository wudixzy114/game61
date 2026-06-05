using Dao.Terrain.Generation;
using Dao.Terrain.Streaming;
using Godot;

namespace Dao.Terrain.Runtime;

/// <summary>Runtime builder that materializes gameplay anchors from a terrain world plan without requiring debug overlay visuals.</summary>
[GlobalClass]
public partial class TerrainWorldAnchorBuilder : Node3D
{
    [Export] public bool BuildOnReady { get; set; } = false;
    [Export] public NodePath TerrainWorldPath { get; set; } = new();
    [Export(PropertyHint.Range, "0,80,1")] public float AnchorHeightOffset { get; set; } = 3.0f;

    private TerrainWorldPlan? _plan;
    public TerrainWorldPlan? Plan => _plan is null ? null : TerrainWorldPlan.CopyOf(_plan);

    public override void _Ready()
    {
        if (!BuildOnReady)
        {
            return;
        }

        TerrainWorld? terrainWorld = TerrainWorldPath.IsEmpty
            ? GetParentOrNull<TerrainWorld>()
            : GetNodeOrNull<TerrainWorld>(TerrainWorldPath);
        if (terrainWorld is null || !terrainWorld.TryGetWorldPlan(out TerrainWorldPlan? plan))
        {
            return;
        }

        ApplyPlan(plan, terrainWorld.Profile);
    }

    /// <summary>Creates POI and route gameplay anchors for the supplied plan.</summary>
    public void ApplyPlan(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        ClearAnchors();
        _plan = TerrainWorldPlan.CopyOf(plan);
        BuildPointOfInterestAnchors(_plan, profile);
        BuildRouteAnchors(_plan, profile);
    }

    /// <summary>Removes all previously generated gameplay anchors and clears the assigned plan.</summary>
    public void ClearAnchors()
    {
        _plan = null;
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
    }

    private void BuildPointOfInterestAnchors(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        var poiRoot = new Node3D { Name = "PointsOfInterest" };
        AddChild(poiRoot);
        foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
        {
            var anchor = new TerrainWorldPointOfInterestAnchor();
            poiRoot.AddChild(anchor);
            TerrainPointOfInterestArchetype archetype = TerrainPointOfInterestArchetypeCatalog.Get(point.Kind);
            anchor.Configure(point, PositionFor(point.WorldPosition, profile, AnchorHeightOffset + archetype.VerticalOffset));
        }
    }

    private void BuildRouteAnchors(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        var routeRoot = new Node3D { Name = "Routes" };
        AddChild(routeRoot);
        foreach (TerrainWorldRoute route in plan.Routes)
        {
            var anchor = new TerrainWorldRouteAnchor();
            routeRoot.AddChild(anchor);
            anchor.Configure(route, RouteAnchorPosition(route, profile));
        }
    }

    private Vector3 RouteAnchorPosition(TerrainWorldRoute route, TerrainGenerationProfile profile)
    {
        if (route.Waypoints.Length == 0)
        {
            return Vector3.Zero;
        }

        return PositionFor(route.Waypoints[route.Waypoints.Length / 2], profile, AnchorHeightOffset);
    }

    private static Vector3 PositionFor(Vector2 world, TerrainGenerationProfile profile, float heightOffset)
    {
        TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);
        float height = Mathf.Max(field.Height, profile.SeaLevel + 1.5f);
        return new Vector3(world.X, height + heightOffset, world.Y);
    }
}
