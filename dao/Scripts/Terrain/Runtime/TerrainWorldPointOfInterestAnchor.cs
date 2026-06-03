using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Runtime;

public partial class TerrainWorldPointOfInterestAnchor : Marker3D
{
    public int Id { get; private set; }
    public TerrainPointOfInterestKind Kind { get; private set; }
    public Vector2 WorldPosition2D { get; private set; }
    public float Score { get; private set; }
    public float Height { get; private set; }
    public float ScenicPotential { get; private set; }
    public float Traversability { get; private set; }
    public TerrainLandscapeKind LandscapeKind { get; private set; }

    public void Configure(TerrainWorldPointOfInterest point, Vector3 worldPosition)
    {
        Id = point.Id;
        Kind = point.Kind;
        WorldPosition2D = point.WorldPosition;
        Score = point.Score;
        Height = point.Height;
        ScenicPotential = point.ScenicPotential;
        Traversability = point.Traversability;
        LandscapeKind = point.LandscapeKind;

        Name = $"POI_{Id:00}_{Kind}";
        GlobalPosition = worldPosition;
        AddToGroup("terrain_poi");
        SetMeta("terrain_poi_id", Id);
        SetMeta("terrain_poi_kind", Kind.ToString());
        SetMeta("terrain_poi_score", Score);
        SetMeta("terrain_poi_scenic", ScenicPotential);
        SetMeta("terrain_poi_traversability", Traversability);
        SetMeta("terrain_poi_landscape", LandscapeKind.ToString());
    }
}
