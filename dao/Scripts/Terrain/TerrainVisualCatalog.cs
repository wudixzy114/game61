using System.Collections.Generic;
using Dao.Terrain.Generation;
using Godot;
using Godot.Collections;

namespace Dao.Terrain;

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

    /// <summary>Reports scatter kinds missing a mesh entry when primitive fallbacks are disabled.</summary>
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
            if (entry?.Mesh is null)
            {
                missing.Add(kind);
            }
        }

        return missing.ToArray();
    }

    /// <summary>Reports landmark kinds missing a mesh entry when primitive fallbacks are disabled.</summary>
    public TerrainLandmarkKind[] GetMissingLandmarkMeshKinds()
    {
        var missing = new List<TerrainLandmarkKind>();
        foreach (TerrainLandmarkKind kind in System.Enum.GetValues<TerrainLandmarkKind>())
        {
            TerrainLandmarkVisualEntryResource? entry = GetLandmarkEntry(kind);
            if (entry?.Mesh is null)
            {
                missing.Add(kind);
            }
        }

        return missing.ToArray();
    }
}
