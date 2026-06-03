using System.Collections.Generic;
using Dao.Terrain.Generation;
using Dao.Terrain.Rendering;
using Godot;

namespace Dao.Terrain.Runtime;

[GlobalClass]
public partial class TerrainWorldPlanOverlay : Node3D
{
    [Export] public bool VisibleByDefault { get; set; } = true;
    [Export] public bool BuildGameplayAnchors { get; set; } = true;
    [Export] public bool ShowPointMarkers { get; set; } = true;
    [Export] public bool ShowRouteRibbons { get; set; } = true;
    [Export(PropertyHint.Range, "2,80,1")] public float RouteWidth { get; set; } = 16.0f;
    [Export(PropertyHint.Range, "0,80,1")] public float RouteHeightOffset { get; set; } = 6.0f;
    [Export(PropertyHint.Range, "2,120,1")] public float MarkerBaseScale { get; set; } = 18.0f;
    [Export(PropertyHint.Range, "0,120,1")] public float MarkerHeightOffset { get; set; } = 16.0f;
    [Export(PropertyHint.Range, "0,80,1")] public float AnchorHeightOffset { get; set; } = 3.0f;

    private readonly Dictionary<TerrainPointOfInterestKind, MultiMeshInstance3D> _pointMarkers = new();
    private MeshInstance3D? _routeRibbons;
    private Node3D? _anchorRoot;
    private TerrainWorldPlan? _plan;

    public TerrainWorldPlan? Plan => _plan;

    public override void _Ready()
    {
        Visible = VisibleByDefault;
    }

    public void ApplyPlan(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        _plan = plan;

        ClearRuntimeObjects();

        if (ShowRouteRibbons)
        {
            BuildRouteRibbons(plan, profile);
        }

        if (ShowPointMarkers)
        {
            BuildPointMarkers(plan, profile);
        }

        if (BuildGameplayAnchors)
        {
            BuildAnchors(plan, profile);
        }
    }

    public void ClearPlan()
    {
        _plan = null;
        ClearRuntimeObjects();
    }

    private void ClearRuntimeObjects()
    {
        foreach (MultiMeshInstance3D marker in _pointMarkers.Values)
        {
            marker.QueueFree();
        }

        _pointMarkers.Clear();

        if (_routeRibbons is not null)
        {
            _routeRibbons.QueueFree();
            _routeRibbons = null;
        }

        if (_anchorRoot is not null)
        {
            _anchorRoot.QueueFree();
            _anchorRoot = null;
        }
    }

    private void BuildPointMarkers(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        var byKind = new Dictionary<TerrainPointOfInterestKind, List<TerrainWorldPointOfInterest>>();
        foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
        {
            if (!byKind.TryGetValue(point.Kind, out List<TerrainWorldPointOfInterest>? points))
            {
                points = new List<TerrainWorldPointOfInterest>();
                byKind.Add(point.Kind, points);
            }

            points.Add(point);
        }

        Mesh markerMesh = CreateMarkerMesh();
        Material material = TerrainMaterialFactory.CreatePlanMarkerMaterial();

        foreach ((TerrainPointOfInterestKind kind, List<TerrainWorldPointOfInterest> points) in byKind)
        {
            var multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                Mesh = markerMesh,
                CustomAabb = PlanAabb(plan, profile, MarkerBaseScale * 8.0f + MarkerHeightOffset)
            };

            multimesh.InstanceCount = points.Count;
            multimesh.VisibleInstanceCount = points.Count;

            for (int i = 0; i < points.Count; i++)
            {
                TerrainWorldPointOfInterest point = points[i];
                Vector3 position = PositionFor(point.WorldPosition, profile, MarkerHeightOffset);
                float scale = MarkerBaseScale * Mathf.Lerp(0.72f, 1.34f, point.Score);
                var basis = Basis.Identity.Scaled(new Vector3(scale, scale * 2.4f, scale));
                multimesh.SetInstanceTransform(i, new Transform3D(basis, position));
                multimesh.SetInstanceColor(i, ColorForPoint(point.Kind, point.Score));
            }

            var instance = new MultiMeshInstance3D
            {
                Name = $"POI_{kind}",
                Multimesh = multimesh,
                MaterialOverride = material
            };

            AddChild(instance);
            _pointMarkers.Add(kind, instance);
        }
    }

    private void BuildRouteRibbons(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        var vertices = new List<Vector3>(plan.Routes.Length * 64 * 4);
        var colors = new List<Color>(plan.Routes.Length * 64 * 4);
        var indices = new List<int>(plan.Routes.Length * 64 * 6);

        foreach (TerrainWorldRoute route in plan.Routes)
        {
            AddRouteRibbon(route, profile, vertices, colors, indices);
        }

        if (vertices.Count == 0)
        {
            return;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.CustomAabb = PlanAabb(plan, profile, RouteWidth + RouteHeightOffset + 8.0f);

        _routeRibbons = new MeshInstance3D
        {
            Name = "RouteRibbons",
            Mesh = mesh,
            MaterialOverride = TerrainMaterialFactory.CreatePlanRouteMaterial()
        };

        AddChild(_routeRibbons);
    }

    private void AddRouteRibbon(
        TerrainWorldRoute route,
        TerrainGenerationProfile profile,
        List<Vector3> vertices,
        List<Color> colors,
        List<int> indices)
    {
        if (route.Waypoints.Length < 2)
        {
            return;
        }

        Color color = ColorForRoute(route.Kind, route.AverageScenicPotential);
        float halfWidth = Mathf.Max(1.0f, RouteWidth) * 0.5f;

        for (int i = 1; i < route.Waypoints.Length; i++)
        {
            Vector3 from = PositionFor(route.Waypoints[i - 1], profile, RouteHeightOffset);
            Vector3 to = PositionFor(route.Waypoints[i], profile, RouteHeightOffset);
            Vector3 direction = to - from;
            direction.Y = 0.0f;

            if (direction.LengthSquared() <= 0.001f)
            {
                continue;
            }

            Vector3 side = direction.Normalized().Cross(Vector3.Up).Normalized() * halfWidth;
            int start = vertices.Count;
            vertices.Add(from - side);
            vertices.Add(from + side);
            vertices.Add(to - side);
            vertices.Add(to + side);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            indices.Add(start);
            indices.Add(start + 2);
            indices.Add(start + 1);
            indices.Add(start + 1);
            indices.Add(start + 2);
            indices.Add(start + 3);
        }
    }

    private void BuildAnchors(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        _anchorRoot = new Node3D { Name = "GameplayAnchors" };
        AddChild(_anchorRoot);

        var poiRoot = new Node3D { Name = "PointsOfInterest" };
        _anchorRoot.AddChild(poiRoot);
        foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
        {
            var anchor = new TerrainWorldPointOfInterestAnchor();
            poiRoot.AddChild(anchor);
            anchor.Configure(point, PositionFor(point.WorldPosition, profile, AnchorHeightOffset));
        }

        var routeRoot = new Node3D { Name = "Routes" };
        _anchorRoot.AddChild(routeRoot);
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

    private Vector3 PositionFor(Vector2 world, TerrainGenerationProfile profile, float heightOffset)
    {
        TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);
        float height = Mathf.Max(field.Height, profile.SeaLevel + 1.5f);
        return new Vector3(world.X, height + heightOffset, world.Y);
    }

    private static Mesh CreateMarkerMesh()
    {
        return new CylinderMesh
        {
            TopRadius = 0.0f,
            BottomRadius = 0.42f,
            Height = 1.0f,
            RadialSegments = 8,
            Rings = 1,
            CapBottom = true
        };
    }

    private static Aabb PlanAabb(TerrainWorldPlan plan, TerrainGenerationProfile profile, float padding)
    {
        float halfSize = plan.WorldSize * 0.5f;
        float minX = plan.Center.X - halfSize - padding;
        float minZ = plan.Center.Y - halfSize - padding;
        float minY = Mathf.Min(plan.QualityReport.MinHeight, profile.SeaLevel) - padding;
        float maxX = plan.Center.X + halfSize + padding;
        float maxZ = plan.Center.Y + halfSize + padding;
        float maxY = Mathf.Max(plan.QualityReport.MaxHeight, profile.SeaLevel) + padding + 128.0f;

        return new Aabb(
            new Vector3(minX, minY, minZ),
            new Vector3(maxX - minX, maxY - minY, maxZ - minZ));
    }

    private static Color ColorForPoint(TerrainPointOfInterestKind kind, float score)
    {
        Color baseColor = kind switch
        {
            TerrainPointOfInterestKind.SettlementCandidate => new Color(0.95f, 0.68f, 0.22f),
            TerrainPointOfInterestKind.Vista => new Color(1.0f, 0.9f, 0.26f),
            TerrainPointOfInterestKind.RiverCrossing => new Color(0.22f, 0.74f, 0.96f),
            TerrainPointOfInterestKind.MountainPass => new Color(0.70f, 0.62f, 1.0f),
            TerrainPointOfInterestKind.CoastalLanding => new Color(0.26f, 0.58f, 0.95f),
            TerrainPointOfInterestKind.ResourceGrove => new Color(0.32f, 0.78f, 0.36f),
            TerrainPointOfInterestKind.AncientSite => new Color(0.94f, 0.56f, 0.30f),
            TerrainPointOfInterestKind.CanyonOverlook => new Color(0.96f, 0.42f, 0.22f),
            _ => Colors.White
        };

        return baseColor.Lerp(Colors.White, Mathf.Clamp((score - 0.5f) * 0.32f, 0.0f, 0.16f));
    }

    private static Color ColorForRoute(TerrainRouteKind kind, float scenic)
    {
        Color baseColor = kind switch
        {
            TerrainRouteKind.PrimaryTrail => new Color(0.92f, 0.75f, 0.42f, 0.62f),
            TerrainRouteKind.RiverRoad => new Color(0.20f, 0.62f, 0.92f, 0.68f),
            TerrainRouteKind.RidgePass => new Color(0.74f, 0.68f, 1.0f, 0.68f),
            TerrainRouteKind.CoastalPath => new Color(0.32f, 0.72f, 0.82f, 0.66f),
            TerrainRouteKind.ScenicTrail => new Color(1.0f, 0.66f, 0.24f, 0.74f),
            _ => new Color(1.0f, 1.0f, 1.0f, 0.62f)
        };

        return baseColor.Lerp(Colors.White, Mathf.Clamp(scenic * 0.12f, 0.0f, 0.12f));
    }
}
