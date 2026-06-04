using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Runtime;

/// <summary>Runtime node for a planned POI, exposing archetype-driven gameplay metadata via Godot groups and meta properties.</summary>
public partial class TerrainWorldPointOfInterestAnchor : Marker3D
{
    public const string GroupName = TerrainWorldAnchorContract.PointOfInterestGroup;
    public const string MetaKeyId = TerrainWorldAnchorContract.PointOfInterestMetaKeyId;
    public const string MetaKeyKind = TerrainWorldAnchorContract.PointOfInterestMetaKeyKind;
    public const string MetaKeyVisual = TerrainWorldAnchorContract.PointOfInterestMetaKeyVisual;
    public const string MetaKeyGameplayTag = TerrainWorldAnchorContract.PointOfInterestMetaKeyGameplayTag;
    public const string MetaKeyScore = TerrainWorldAnchorContract.PointOfInterestMetaKeyScore;
    public const string MetaKeyScenic = TerrainWorldAnchorContract.PointOfInterestMetaKeyScenic;
    public const string MetaKeyTraversability = TerrainWorldAnchorContract.PointOfInterestMetaKeyTraversability;
    public const string MetaKeySettlementTier = TerrainWorldAnchorContract.PointOfInterestMetaKeySettlementTier;
    public const string MetaKeyLandscape = TerrainWorldAnchorContract.PointOfInterestMetaKeyLandscape;
    public const string MetaKeyInteractionRadius = TerrainWorldAnchorContract.PointOfInterestMetaKeyInteractionRadius;
    public const string MetaKeyEncounterBudget = TerrainWorldAnchorContract.PointOfInterestMetaKeyEncounterBudget;

    public static string[] RequiredMetaKeys => TerrainWorldAnchorContract.GetPointOfInterestRequiredMetaKeys();

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
        TerrainWorldPointOfInterestAnchorDescriptor descriptor =
            TerrainWorldAnchorContract.CreatePointOfInterestDescriptor(point);
        Configure(descriptor, worldPosition);
    }

    /// <summary>Sets up this anchor from a stable gameplay descriptor and places it at the computed world position.</summary>
    public void Configure(TerrainWorldPointOfInterestAnchorDescriptor descriptor, Vector3 worldPosition)
    {
        Id = descriptor.Id;
        Kind = descriptor.Kind;
        WorldPosition2D = descriptor.WorldPosition2D;
        Score = descriptor.Score;
        Height = descriptor.Height;
        ScenicPotential = descriptor.ScenicPotential;
        Traversability = descriptor.Traversability;
        SettlementTier = descriptor.SettlementTier;
        LandscapeKind = descriptor.LandscapeKind;
        VisualKind = descriptor.VisualKind;
        GameplayTag = descriptor.GameplayTag;
        InteractionRadius = descriptor.InteractionRadius;
        EncounterBudget = descriptor.EncounterBudget;

        Name = descriptor.Name;
        GlobalPosition = worldPosition;
        AddToGroup(descriptor.GroupName);
        AddToGroup(descriptor.GameplayTagGroup);
        SetMeta(MetaKeyId, Id);
        SetMeta(MetaKeyKind, Kind.ToString());
        SetMeta(MetaKeyVisual, VisualKind.ToString());
        SetMeta(MetaKeyGameplayTag, GameplayTag);
        SetMeta(MetaKeyScore, Score);
        SetMeta(MetaKeyScenic, ScenicPotential);
        SetMeta(MetaKeyTraversability, Traversability);
        SetMeta(MetaKeySettlementTier, SettlementTier.ToString());
        SetMeta(MetaKeyLandscape, LandscapeKind.ToString());
        SetMeta(MetaKeyInteractionRadius, InteractionRadius);
        SetMeta(MetaKeyEncounterBudget, EncounterBudget);
    }
}
