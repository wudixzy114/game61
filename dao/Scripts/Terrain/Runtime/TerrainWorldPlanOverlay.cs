using System.Collections.Generic;
using Dao.Terrain.Generation;
using Dao.Terrain.Rendering;
using Godot;

namespace Dao.Terrain.Runtime;

/// <summary>Godot node that visualizes a world plan as an in-editor overlay with POI markers and route ribbons.</summary>
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

    private readonly Dictionary<TerrainPointOfInterestVisualKind, MultiMeshInstance3D> _pointMarkers = new();
    private MeshInstance3D? _routeRibbons;
    private TerrainWorldAnchorBuilder? _anchorBuilder;
    private TerrainWorldPlan? _plan;

    public TerrainWorldPlan? Plan => _plan;

    public override void _Ready()
    {
        Visible = VisibleByDefault;
    }

    /// <summary>Applies a world plan, rebuilding the overlay markers and route ribbons.</summary>
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

    /// <summary>Clears the current plan and removes all overlay objects.</summary>
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

        if (_anchorBuilder is not null)
        {
            _anchorBuilder.QueueFree();
            _anchorBuilder = null;
        }
    }

    private void BuildPointMarkers(TerrainWorldPlan plan, TerrainGenerationProfile profile)
    {
        var byVisualKind = new Dictionary<TerrainPointOfInterestVisualKind, List<TerrainWorldPointOfInterest>>();
        foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
        {
            TerrainPointOfInterestVisualKind visualKind = TerrainPointOfInterestArchetypeCatalog.VisualKindFor(point);
            if (!byVisualKind.TryGetValue(visualKind, out List<TerrainWorldPointOfInterest>? points))
            {
                points = new List<TerrainWorldPointOfInterest>();
                byVisualKind.Add(visualKind, points);
            }

            points.Add(point);
        }

        Material material = TerrainMaterialFactory.CreatePlanMarkerMaterial();

        foreach ((TerrainPointOfInterestVisualKind visualKind, List<TerrainWorldPointOfInterest> points) in byVisualKind)
        {
            Mesh markerMesh = CreateMarkerMesh(visualKind);
            var multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                Mesh = markerMesh,
                CustomAabb = PlanAabb(plan, profile, MarkerBaseScale * 10.0f + MarkerHeightOffset)
            };

            multimesh.InstanceCount = points.Count;
            multimesh.VisibleInstanceCount = points.Count;

            for (int i = 0; i < points.Count; i++)
            {
                TerrainWorldPointOfInterest point = points[i];
                TerrainPointOfInterestArchetype archetype = TerrainPointOfInterestArchetypeCatalog.Get(point.Kind);
                Vector3 position = PositionFor(point.WorldPosition, profile, MarkerHeightOffset + archetype.VerticalOffset);
                float uniformScale = archetype.VisualScale * Mathf.Lerp(0.86f, 1.22f, point.Score);
                TerrainPointOfInterestVisualKind visualKindForPoint = TerrainPointOfInterestArchetypeCatalog.VisualKindFor(point);
                Basis basis = BasisForVisual(visualKindForPoint, uniformScale);
                multimesh.SetInstanceTransform(i, new Transform3D(basis, position));
                multimesh.SetInstanceColor(i, ColorForPoint(archetype, point));
            }

            var instance = new MultiMeshInstance3D
            {
                Name = $"POI_{visualKind}",
                Multimesh = multimesh,
                MaterialOverride = material
            };

            AddChild(instance);
            _pointMarkers.Add(visualKind, instance);
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
        _anchorBuilder = new TerrainWorldAnchorBuilder
        {
            Name = "GameplayAnchors",
            AnchorHeightOffset = AnchorHeightOffset
        };
        AddChild(_anchorBuilder);
        _anchorBuilder.ApplyPlan(plan, profile);
    }

    private Vector3 PositionFor(Vector2 world, TerrainGenerationProfile profile, float heightOffset)
    {
        TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);
        float height = Mathf.Max(field.Height, profile.SeaLevel + 1.5f);
        return new Vector3(world.X, height + heightOffset, world.Y);
    }

    private static Mesh CreateMarkerMesh(TerrainPointOfInterestVisualKind visualKind)
    {
        return visualKind switch
        {
            TerrainPointOfInterestVisualKind.Settlement => new BoxMesh { Size = new Vector3(1.3f, 0.55f, 1.3f) },
            TerrainPointOfInterestVisualKind.Village => new BoxMesh { Size = new Vector3(1.45f, 0.56f, 1.08f) },
            TerrainPointOfInterestVisualKind.Town => new BoxMesh { Size = new Vector3(1.92f, 0.78f, 1.52f) },
            TerrainPointOfInterestVisualKind.RiverCrossing => new BoxMesh { Size = new Vector3(1.65f, 0.16f, 0.62f) },
            TerrainPointOfInterestVisualKind.CoastalLanding => new CylinderMesh
            {
                TopRadius = 0.34f,
                BottomRadius = 0.58f,
                Height = 0.44f,
                RadialSegments = 9,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainPointOfInterestVisualKind.ResourceGrove => new SphereMesh
            {
                Radius = 0.62f,
                Height = 1.05f,
                RadialSegments = 8,
                Rings = 4
            },
            TerrainPointOfInterestVisualKind.AncientSite => new BoxMesh { Size = new Vector3(0.72f, 1.65f, 0.72f) },
            TerrainPointOfInterestVisualKind.MountainPass => new CylinderMesh
            {
                TopRadius = 0.28f,
                BottomRadius = 0.72f,
                Height = 1.15f,
                RadialSegments = 8,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainPointOfInterestVisualKind.CanyonOverlook => new BoxMesh { Size = new Vector3(1.2f, 0.35f, 0.92f) },
            TerrainPointOfInterestVisualKind.Oasis => new CylinderMesh
            {
                TopRadius = 0.72f,
                BottomRadius = 0.54f,
                Height = 0.36f,
                RadialSegments = 12,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            TerrainPointOfInterestVisualKind.OasisHub => new CylinderMesh
            {
                TopRadius = 0.90f,
                BottomRadius = 0.62f,
                Height = 0.42f,
                RadialSegments = 14,
                Rings = 1,
                CapTop = true,
                CapBottom = true
            },
            _ => new CylinderMesh
            {
                TopRadius = 0.0f,
                BottomRadius = 0.42f,
                Height = 1.0f,
                RadialSegments = 8,
                Rings = 1,
                CapBottom = true
            }
        };
    }

    private static Basis BasisForVisual(TerrainPointOfInterestVisualKind visualKind, float scale)
    {
        return visualKind switch
        {
            TerrainPointOfInterestVisualKind.Settlement => Basis.Identity.Scaled(new Vector3(scale * 1.2f, scale * 0.62f, scale * 1.2f)),
            TerrainPointOfInterestVisualKind.Village => Basis.Identity.Scaled(new Vector3(scale * 1.26f, scale * 0.60f, scale)),
            TerrainPointOfInterestVisualKind.Town => Basis.Identity.Scaled(new Vector3(scale * 1.46f, scale * 0.80f, scale * 1.20f)),
            TerrainPointOfInterestVisualKind.RiverCrossing => Basis.Identity.Scaled(new Vector3(scale * 1.45f, scale * 0.28f, scale * 0.62f)),
            TerrainPointOfInterestVisualKind.ResourceGrove => Basis.Identity.Scaled(new Vector3(scale * 0.92f, scale * 1.30f, scale * 0.92f)),
            TerrainPointOfInterestVisualKind.AncientSite => Basis.Identity.Scaled(new Vector3(scale * 0.72f, scale * 1.72f, scale * 0.72f)),
            TerrainPointOfInterestVisualKind.CanyonOverlook => Basis.Identity.Scaled(new Vector3(scale * 1.35f, scale * 0.46f, scale * 0.92f)),
            TerrainPointOfInterestVisualKind.Oasis => Basis.Identity.Scaled(new Vector3(scale * 1.36f, scale * 0.38f, scale * 1.36f)),
            TerrainPointOfInterestVisualKind.OasisHub => Basis.Identity.Scaled(new Vector3(scale * 1.58f, scale * 0.48f, scale * 1.58f)),
            TerrainPointOfInterestVisualKind.VistaSpire => Basis.Identity.Scaled(new Vector3(scale, scale * 2.65f, scale)),
            _ => Basis.Identity.Scaled(new Vector3(scale, scale * 1.4f, scale))
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

    private static Color ColorForPoint(TerrainPointOfInterestArchetype archetype, TerrainWorldPointOfInterest point)
    {
        Color baseColor = point.SettlementTier switch
        {
            TerrainSettlementTier.Village => new Color(0.82f, 0.58f, 0.28f),
            TerrainSettlementTier.Town => new Color(0.88f, 0.42f, 0.22f),
            TerrainSettlementTier.OasisHub => new Color(0.14f, 0.82f, 0.58f),
            _ => archetype.Color
        };

        return baseColor.Lerp(Colors.White, Mathf.Clamp((point.Score - 0.5f) * 0.32f, 0.0f, 0.16f));
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
