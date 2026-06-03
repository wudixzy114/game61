using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Runtime;

/// <summary>Runtime node for a planned POI, exposing archetype-driven gameplay metadata via Godot groups and meta properties.</summary>
public partial class TerrainWorldPointOfInterestAnchor : Marker3D
{
    public int Id { get; private set; }
    public TerrainPointOfInterestKind Kind { get; private set; }
    public Vector2 WorldPosition2D { get; private set; }
    public float Score { get; private set; }
    public float Height { get; private set; }
    public float ScenicPotential { get; private set; }
    public float Traversability { get; private set; }
    public TerrainSettlementTier SettlementTier { get; private set; }
    public TerrainLandscapeKind LandscapeKind { get; private set; }
    public TerrainPointOfInterestVisualKind VisualKind { get; private set; }
    public string GameplayTag { get; private set; } = string.Empty;
    public float InteractionRadius { get; private set; }
    public int EncounterBudget { get; private set; }

    /// <summary>Sets up this anchor from plan data and places it at the computed world position.</summary>
    public void Configure(TerrainWorldPointOfInterest point, Vector3 worldPosition)
    {
        TerrainPointOfInterestArchetype archetype = TerrainPointOfInterestArchetypeCatalog.Get(point.Kind);

        Id = point.Id;
        Kind = point.Kind;
        WorldPosition2D = point.WorldPosition;
        Score = point.Score;
        Height = point.Height;
        ScenicPotential = point.ScenicPotential;
        Traversability = point.Traversability;
        SettlementTier = point.SettlementTier;
        LandscapeKind = point.LandscapeKind;
        VisualKind = TerrainPointOfInterestArchetypeCatalog.VisualKindFor(point);
        GameplayTag = archetype.GameplayTag;
        InteractionRadius = archetype.InteractionRadius;
        EncounterBudget = archetype.EncounterBudget;

        Name = $"POI_{Id:00}_{Kind}";
        GlobalPosition = worldPosition;
        AddToGroup("terrain_poi");
        AddToGroup(GameplayTag);
        SetMeta("terrain_poi_id", Id);
        SetMeta("terrain_poi_kind", Kind.ToString());
        SetMeta("terrain_poi_visual", VisualKind.ToString());
        SetMeta("terrain_poi_gameplay_tag", GameplayTag);
        SetMeta("terrain_poi_score", Score);
        SetMeta("terrain_poi_scenic", ScenicPotential);
        SetMeta("terrain_poi_traversability", Traversability);
        SetMeta("terrain_poi_settlement_tier", SettlementTier.ToString());
        SetMeta("terrain_poi_landscape", LandscapeKind.ToString());
        SetMeta("terrain_poi_interaction_radius", InteractionRadius);
        SetMeta("terrain_poi_encounter_budget", EncounterBudget);
    }
}
