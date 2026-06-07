using System.Collections.Generic;
using Dao.Terrain.Generation;
using Godot;
using Godot.Collections;

namespace Dao.Terrain;

/// <summary>Production-readiness summary for a terrain visual catalog.</summary>
public readonly record struct TerrainVisualCatalogValidationReport(
    bool Passed,
    bool UsePrimitiveFallbacks,
    int ScatterEntryCount,
    int ScatterMeshEntryCount,
    int ScatterSceneEntryCount,
    int ScatterDuplicateEntryCount,
    int ScatterInvalidLodEntryCount,
    int LandmarkEntryCount,
    int LandmarkMeshEntryCount,
    int LandmarkSceneEntryCount,
    int LandmarkDuplicateEntryCount,
    int LandmarkInvalidLodEntryCount,
    TerrainScatterKind[] MissingScatterKinds,
    TerrainLandmarkKind[] MissingLandmarkKinds,
    Resource[] ReferencedResources);

/// <summary>Optional visual asset catalog used by terrain chunks to replace primitive validation meshes.</summary>
[GlobalClass]
public partial class TerrainVisualCatalog : Resource
{
    [Export] public Array<TerrainScatterVisualEntryResource> ScatterEntries { get; set; } = [];
    [Export] public Array<TerrainLandmarkVisualEntryResource> LandmarkEntries { get; set; } = [];
    [Export] public bool UsePrimitiveFallbacks { get; set; } = true;

    /// <summary>Returns the first scatter visual entry configured for the requested kind, if any.</summary>
    public TerrainScatterVisualEntryResource? GetScatterEntry(TerrainScatterKind kind)
    {
        foreach (TerrainScatterVisualEntryResource? entry in ScatterEntries)
        {
            if (entry is not null && entry.Kind == kind)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>Returns the first landmark visual entry configured for the requested kind, if any.</summary>
    public TerrainLandmarkVisualEntryResource? GetLandmarkEntry(TerrainLandmarkKind kind)
    {
        foreach (TerrainLandmarkVisualEntryResource? entry in LandmarkEntries)
        {
            if (entry is not null && entry.Kind == kind)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>Reports scatter kinds missing a mesh or scene entry when primitive fallbacks are disabled.</summary>
    public TerrainScatterKind[] GetMissingScatterMeshKinds()
    {
        var missing = new List<TerrainScatterKind>();
        foreach (TerrainScatterKind kind in System.Enum.GetValues<TerrainScatterKind>())
        {
            if (kind == TerrainScatterKind.Landmark)
            {
                continue;
            }

            TerrainScatterVisualEntryResource? entry = GetScatterEntry(kind);
            if (entry?.Mesh is null && entry?.Scene is null)
            {
                missing.Add(kind);
            }
        }

        return missing.ToArray();
    }

    /// <summary>Reports landmark kinds missing a mesh or scene entry when primitive fallbacks are disabled.</summary>
    public TerrainLandmarkKind[] GetMissingLandmarkMeshKinds()
    {
        var missing = new List<TerrainLandmarkKind>();
        foreach (TerrainLandmarkKind kind in System.Enum.GetValues<TerrainLandmarkKind>())
        {
            TerrainLandmarkVisualEntryResource? entry = GetLandmarkEntry(kind);
            if (entry?.Mesh is null && entry?.Scene is null)
            {
                missing.Add(kind);
            }
        }

        return missing.ToArray();
    }

    /// <summary>Returns every mesh or scene resource referenced by this catalog, de-duplicated by object identity.</summary>
    public Resource[] GetReferencedResources()
    {
        var resources = new List<Resource>();
        var seen = new HashSet<ulong>();

        foreach (TerrainScatterVisualEntryResource? entry in ScatterEntries)
        {
            if (entry is null)
            {
                continue;
            }

            AddReferencedResource(entry.Mesh, resources, seen);
            AddReferencedResource(entry.Scene, resources, seen);
        }

        foreach (TerrainLandmarkVisualEntryResource? entry in LandmarkEntries)
        {
            if (entry is null)
            {
                continue;
            }

            AddReferencedResource(entry.Mesh, resources, seen);
            AddReferencedResource(entry.Scene, resources, seen);
        }

        return resources.ToArray();
    }

    /// <summary>Builds a reusable production-readiness report for editor tooling, validation, and asset pipeline checks.</summary>
    public TerrainVisualCatalogValidationReport ValidateCatalog()
    {
        int scatterEntries = 0;
        int scatterMeshEntries = 0;
        int scatterSceneEntries = 0;
        int scatterInvalidLodEntries = 0;
        int scatterDuplicateEntries = 0;
        var scatterKinds = new HashSet<TerrainScatterKind>();
        foreach (TerrainScatterVisualEntryResource? entry in ScatterEntries)
        {
            if (entry is null)
            {
                continue;
            }

            scatterEntries++;
            if (entry.Mesh is not null)
            {
                scatterMeshEntries++;
            }

            if (entry.Scene is not null)
            {
                scatterSceneEntries++;
            }

            if (entry.MaxLod < entry.MinLod)
            {
                scatterInvalidLodEntries++;
            }

            if (!scatterKinds.Add(entry.Kind))
            {
                scatterDuplicateEntries++;
            }
        }

        int landmarkEntries = 0;
        int landmarkMeshEntries = 0;
        int landmarkSceneEntries = 0;
        int landmarkInvalidLodEntries = 0;
        int landmarkDuplicateEntries = 0;
        var landmarkKinds = new HashSet<TerrainLandmarkKind>();
        foreach (TerrainLandmarkVisualEntryResource? entry in LandmarkEntries)
        {
            if (entry is null)
            {
                continue;
            }

            landmarkEntries++;
            if (entry.Mesh is not null)
            {
                landmarkMeshEntries++;
            }

            if (entry.Scene is not null)
            {
                landmarkSceneEntries++;
            }

            if (entry.MaxLod < entry.MinLod)
            {
                landmarkInvalidLodEntries++;
            }

            if (!landmarkKinds.Add(entry.Kind))
            {
                landmarkDuplicateEntries++;
            }
        }

        TerrainScatterKind[] missingScatter = GetMissingScatterMeshKinds();
        TerrainLandmarkKind[] missingLandmarks = GetMissingLandmarkMeshKinds();
        bool passed =
            scatterInvalidLodEntries == 0 &&
            landmarkInvalidLodEntries == 0 &&
            scatterDuplicateEntries == 0 &&
            landmarkDuplicateEntries == 0 &&
            (UsePrimitiveFallbacks || (missingScatter.Length == 0 && missingLandmarks.Length == 0));

        return new TerrainVisualCatalogValidationReport(
            passed,
            UsePrimitiveFallbacks,
            scatterEntries,
            scatterMeshEntries,
            scatterSceneEntries,
            scatterDuplicateEntries,
            scatterInvalidLodEntries,
            landmarkEntries,
            landmarkMeshEntries,
            landmarkSceneEntries,
            landmarkDuplicateEntries,
            landmarkInvalidLodEntries,
            missingScatter,
            missingLandmarks,
            GetReferencedResources());
    }

    private static void AddReferencedResource(Resource? resource, List<Resource> resources, HashSet<ulong> seen)
    {
        if (resource is null)
        {
            return;
        }

        ulong id = resource.GetInstanceId();
        if (seen.Add(id))
        {
            resources.Add(resource);
        }
    }
}
