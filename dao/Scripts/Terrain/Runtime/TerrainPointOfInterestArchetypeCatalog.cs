using System;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Runtime;

public enum TerrainPointOfInterestVisualKind
{
    Settlement,
    VistaSpire,
    RiverCrossing,
    MountainPass,
    CoastalLanding,
    ResourceGrove,
    AncientSite,
    CanyonOverlook,
    Oasis,
    Village,
    Town,
    OasisHub
}

public readonly record struct TerrainPointOfInterestArchetype(
    TerrainPointOfInterestKind Kind,
    TerrainPointOfInterestVisualKind VisualKind,
    string GameplayTag,
    string DisplayName,
    float VisualScale,
    float VerticalOffset,
    float InteractionRadius,
    int EncounterBudget,
    Color Color);

public readonly record struct TerrainPointOfInterestArchetypeValidationReport(
    bool Passed,
    int DefinedArchetypeCount,
    int ExpectedArchetypeCount,
    int MissingArchetypeCount,
    int PlanPointCount,
    int PlanPointsWithArchetypes,
    string Summary);

public static class TerrainPointOfInterestArchetypeCatalog
{
    private static readonly TerrainPointOfInterestArchetype[] Archetypes =
    [
        new(
            TerrainPointOfInterestKind.SettlementCandidate,
            TerrainPointOfInterestVisualKind.Settlement,
            "poi.settlement_candidate",
            "Settlement Candidate",
            18.0f,
            5.0f,
            64.0f,
            6,
            new Color(0.92f, 0.64f, 0.24f)),
        new(
            TerrainPointOfInterestKind.Vista,
            TerrainPointOfInterestVisualKind.VistaSpire,
            "poi.vista",
            "Vista",
            22.0f,
            9.0f,
            48.0f,
            3,
            new Color(1.00f, 0.88f, 0.24f)),
        new(
            TerrainPointOfInterestKind.RiverCrossing,
            TerrainPointOfInterestVisualKind.RiverCrossing,
            "poi.river_crossing",
            "River Crossing",
            20.0f,
            4.0f,
            56.0f,
            5,
            new Color(0.22f, 0.70f, 0.92f)),
        new(
            TerrainPointOfInterestKind.MountainPass,
            TerrainPointOfInterestVisualKind.MountainPass,
            "poi.mountain_pass",
            "Mountain Pass",
            20.0f,
            7.0f,
            58.0f,
            5,
            new Color(0.70f, 0.64f, 0.96f)),
        new(
            TerrainPointOfInterestKind.CoastalLanding,
            TerrainPointOfInterestVisualKind.CoastalLanding,
            "poi.coastal_landing",
            "Coastal Landing",
            22.0f,
            4.0f,
            72.0f,
            5,
            new Color(0.28f, 0.56f, 0.92f)),
        new(
            TerrainPointOfInterestKind.ResourceGrove,
            TerrainPointOfInterestVisualKind.ResourceGrove,
            "poi.resource_grove",
            "Resource Grove",
            19.0f,
            5.0f,
            50.0f,
            4,
            new Color(0.30f, 0.76f, 0.34f)),
        new(
            TerrainPointOfInterestKind.AncientSite,
            TerrainPointOfInterestVisualKind.AncientSite,
            "poi.ancient_site",
            "Ancient Site",
            24.0f,
            8.0f,
            66.0f,
            7,
            new Color(0.88f, 0.52f, 0.30f)),
        new(
            TerrainPointOfInterestKind.CanyonOverlook,
            TerrainPointOfInterestVisualKind.CanyonOverlook,
            "poi.canyon_overlook",
            "Canyon Overlook",
            20.0f,
            8.0f,
            52.0f,
            4,
            new Color(0.92f, 0.40f, 0.20f)),
        new(
            TerrainPointOfInterestKind.Oasis,
            TerrainPointOfInterestVisualKind.Oasis,
            "poi.oasis",
            "Oasis",
            21.0f,
            5.0f,
            72.0f,
            6,
            new Color(0.20f, 0.70f, 0.48f))
    ];

    public static ReadOnlySpan<TerrainPointOfInterestArchetype> All => Archetypes;

    public static TerrainPointOfInterestArchetype Get(TerrainPointOfInterestKind kind)
    {
        if (TryGet(kind, out TerrainPointOfInterestArchetype archetype))
        {
            return archetype;
        }

        throw new ArgumentOutOfRangeException(nameof(kind), kind, "No terrain point of interest archetype is defined.");
    }

    public static bool TryGet(TerrainPointOfInterestKind kind, out TerrainPointOfInterestArchetype archetype)
    {
        for (int i = 0; i < Archetypes.Length; i++)
        {
            if (Archetypes[i].Kind == kind)
            {
                archetype = Archetypes[i];
                return true;
            }
        }

        archetype = default;
        return false;
    }

    public static TerrainPointOfInterestVisualKind VisualKindFor(TerrainWorldPointOfInterest point)
    {
        return point.SettlementTier switch
        {
            TerrainSettlementTier.Village => TerrainPointOfInterestVisualKind.Village,
            TerrainSettlementTier.Town => TerrainPointOfInterestVisualKind.Town,
            TerrainSettlementTier.OasisHub => TerrainPointOfInterestVisualKind.OasisHub,
            _ => Get(point.Kind).VisualKind
        };
    }

    public static TerrainPointOfInterestArchetypeValidationReport ValidatePlanReadiness(TerrainWorldPlan plan)
    {
        int expected = Enum.GetValues<TerrainPointOfInterestKind>().Length;
        int missing = 0;
        foreach (TerrainPointOfInterestKind kind in Enum.GetValues<TerrainPointOfInterestKind>())
        {
            if (!TryGet(kind, out _))
            {
                missing++;
            }
        }

        int pointsWithArchetypes = 0;
        foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
        {
            if (TryGet(point.Kind, out _))
            {
                pointsWithArchetypes++;
            }
        }

        bool passed = missing == 0 && pointsWithArchetypes == plan.PointsOfInterest.Length;
        string summary = passed
            ? $"PASS: POI runtime archetypes {Archetypes.Length}/{expected}; plan points covered {pointsWithArchetypes}/{plan.PointsOfInterest.Length}"
            : $"FAIL: POI runtime archetypes {Archetypes.Length}/{expected}, missing {missing}; plan points covered {pointsWithArchetypes}/{plan.PointsOfInterest.Length}";

        return new TerrainPointOfInterestArchetypeValidationReport(
            passed,
            Archetypes.Length,
            expected,
            missing,
            plan.PointsOfInterest.Length,
            pointsWithArchetypes,
            summary);
    }
}
