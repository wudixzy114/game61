using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Godot;

namespace Dao.Terrain.Generation;

public static class NativeTerrainBridge
{
    private static readonly object Lock = new();
    private static bool _initialized;
    private static bool _available;
    private static IntPtr _libraryHandle;
    private static SampleHeightGridDelegate? _sampleHeightGrid;

    public static bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _available;
        }
    }

    public static bool TrySampleHeightGrid(
        TerrainTileCoord coord,
        int resolution,
        TerrainGenerationProfile profile,
        out float[] heights)
    {
        EnsureInitialized();

        int width = resolution + 1;
        int expectedCount = width * width;
        heights = new float[expectedCount];

        if (!_initialized || !_available || _sampleHeightGrid is null)
        {
            return false;
        }

        Vector2 origin = coord.Origin(profile.ChunkSize);
        GCHandle handle = GCHandle.Alloc(heights, GCHandleType.Pinned);
        try
        {
            int written = _sampleHeightGrid(
                profile.Seed,
                origin.X,
                origin.Y,
                resolution,
                profile.ChunkSize,
                profile.HeightScale,
                profile.SeaLevel,
                profile.ContinentScale,
                profile.MountainScale,
                profile.MountainWeight,
                profile.ValleyWeight,
                profile.DetailWeight,
                profile.VistaFrequency,
                profile.RiverStrength,
                profile.RiverCarveDepth,
                profile.TerraceStrength,
                handle.AddrOfPinnedObject(),
                expectedCount);

            return written == expectedCount;
        }
        finally
        {
            handle.Free();
        }
    }

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (Lock)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _available = TryLoadNativeLibrary();
        }
    }

    private static bool TryLoadNativeLibrary()
    {
        string[] candidates =
        [
            "dao.windows.template_debug.x86_64.dll",
            "dao.windows.template_release.x86_64.dll",
            "dao.linux.template_debug.x86_64.so",
            "dao.linux.template_release.x86_64.so"
        ];

        foreach (string candidate in candidates)
        {
            foreach (string path in CandidateLibraryPaths(candidate))
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                if (!NativeLibrary.TryLoad(path, out IntPtr handle))
                {
                    continue;
                }

                if (!NativeLibrary.TryGetExport(handle, "dao_native_sample_height_grid", out IntPtr gridExport))
                {
                    NativeLibrary.Free(handle);
                    continue;
                }

                _libraryHandle = handle;
                _sampleHeightGrid = Marshal.GetDelegateForFunctionPointer<SampleHeightGridDelegate>(gridExport);
                return true;
            }
        }

        return false;
    }

    private static string[] CandidateLibraryPaths(string fileName)
    {
        var paths = new List<string>(16);
        AddCandidate(paths, Path.Combine(System.Environment.CurrentDirectory, "bin", fileName));
        AddCandidate(paths, Path.Combine(System.Environment.CurrentDirectory, "dao", "bin", fileName));
        AddCandidate(paths, Path.Combine(AppContext.BaseDirectory, "bin", fileName));
        AddCandidate(paths, Path.Combine(AppContext.BaseDirectory, "dao", "bin", fileName));

        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && directory is not null; i++)
        {
            AddCandidate(paths, Path.Combine(directory.FullName, "bin", fileName));
            AddCandidate(paths, Path.Combine(directory.FullName, "dao", "bin", fileName));
            directory = directory.Parent;
        }

        return paths.ToArray();
    }

    private static void AddCandidate(List<string> paths, string path)
    {
        string fullPath = Path.GetFullPath(path);
        foreach (string existing in paths)
        {
            if (string.Equals(existing, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        if (fullPath.Length > 0)
        {
            paths.Add(fullPath);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SampleHeightGridDelegate(
        int seed,
        double originX,
        double originZ,
        int resolution,
        double chunkSize,
        double heightScale,
        double seaLevel,
        double continentScale,
        double mountainScale,
        double mountainWeight,
        double valleyWeight,
        double detailWeight,
        double vistaFrequency,
        double riverStrength,
        double riverCarveDepth,
        double terraceStrength,
        IntPtr outputHeights,
        int outputHeightCount);
}
