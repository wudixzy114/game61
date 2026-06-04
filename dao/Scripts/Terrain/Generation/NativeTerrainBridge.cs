using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Godot;

namespace Dao.Terrain.Generation;

/// <summary>Provides interop with a native (C++) terrain sampling library for accelerated height and field grid sampling.</summary>
public static class NativeTerrainBridge
{
    private static readonly object Lock = new();
    private static bool _initialized;
    private static bool _available;
    private static IntPtr _libraryHandle;
    private static SampleFieldGridV2Delegate? _sampleFieldGridV2;
    private static SampleFieldGridV1Delegate? _sampleFieldGridV1;
    private static SampleHeightGridV2Delegate? _sampleHeightGridV2;
    private static SampleHeightGridDelegate? _sampleHeightGrid;

    /// <summary>True if the native library was successfully loaded and at least one export is available.</summary>
    public static bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _available;
        }
    }

    /// <summary>True if the native library can sample full terrain field grids for tile generation.</summary>
    public static bool SupportsFieldGridSampler
    {
        get
        {
            EnsureInitialized();
            return _sampleFieldGridV2 is not null || _sampleFieldGridV1 is not null;
        }
    }

    /// <summary>True if the native library can sample already-derived gameplay fields and classifications.</summary>
    public static bool SupportsDerivedFieldGridSampler
    {
        get
        {
            EnsureInitialized();
            return _sampleFieldGridV2 is not null;
        }
    }

    /// <summary>True if the native library can sample height grids.</summary>
    public static bool SupportsHeightGridSampler
    {
        get
        {
            EnsureInitialized();
            return _sampleHeightGridV2 is not null || _sampleHeightGrid is not null;
        }
    }

    /// <summary>Attempts to fill a height grid using the native sampler. Returns false if the native library is unavailable.</summary>
    public static bool TrySampleHeightGrid(
        TerrainTileCoord coord,
        int resolution,
        TerrainGenerationProfile profile,
        out float[] heights)
    {
        EnsureInitialized();
        heights = [];

        if (!_initialized || !_available || (_sampleHeightGridV2 is null && _sampleHeightGrid is null))
        {
            return false;
        }

        int width = resolution + 1;
        int expectedCount = width * width;
        heights = new float[expectedCount];

        Vector2 origin = coord.Origin(profile.ChunkSize);
        GCHandle handle = GCHandle.Alloc(heights, GCHandleType.Pinned);
        try
        {
            int written = _sampleHeightGridV2 is not null
                ? _sampleHeightGridV2(
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
                    TerrainWorldFieldSampler.LandBalanceOffsetFor(profile),
                    handle.AddrOfPinnedObject(),
                    expectedCount)
                : _sampleHeightGrid!(
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

            bool passed = written == expectedCount;
            if (!passed)
            {
                heights = [];
            }

            return passed;
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>Attempts to fill a full field grid (height + shape terms + climate) using the native sampler.</summary>
    public static bool TrySampleFieldGrid(
        TerrainTileCoord coord,
        int resolution,
        TerrainGenerationProfile profile,
        out float[] samples)
    {
        EnsureInitialized();
        samples = [];

        if (!_initialized || !_available || (_sampleFieldGridV2 is null && _sampleFieldGridV1 is null))
        {
            return false;
        }

        int width = resolution + 1;
        int expectedCount = width * width * TerrainWorldFieldSampler.NativeFieldGridStride;
        samples = new float[expectedCount];

        bool passed = TrySampleFieldGrid(coord, resolution, profile, samples, expectedCount, out _);
        if (!passed)
        {
            samples = [];
        }

        return passed;
    }

    /// <summary>Attempts to write native field samples into a caller-provided buffer.</summary>
    public static bool TrySampleFieldGrid(
        TerrainTileCoord coord,
        int resolution,
        TerrainGenerationProfile profile,
        float[] samples,
        int expectedCount)
    {
        return TrySampleFieldGrid(coord, resolution, profile, samples, expectedCount, out _);
    }

    /// <summary>Attempts to write native field samples into a caller-provided buffer and reports whether the buffer already contains derived gameplay fields.</summary>
    public static bool TrySampleFieldGrid(
        TerrainTileCoord coord,
        int resolution,
        TerrainGenerationProfile profile,
        float[] samples,
        int expectedCount,
        out bool containsDerivedFields)
    {
        EnsureInitialized();
        containsDerivedFields = false;

        if (!_initialized || !_available || (_sampleFieldGridV2 is null && _sampleFieldGridV1 is null))
        {
            return false;
        }

        if (samples.Length < expectedCount)
        {
            return false;
        }

        Vector2 origin = coord.Origin(profile.ChunkSize);
        GCHandle handle = GCHandle.Alloc(samples, GCHandleType.Pinned);
        try
        {
            containsDerivedFields = _sampleFieldGridV2 is not null;
            int written = containsDerivedFields
                ? _sampleFieldGridV2!(
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
                    TerrainWorldFieldSampler.LandBalanceOffsetFor(profile),
                    handle.AddrOfPinnedObject(),
                    expectedCount)
                : _sampleFieldGridV1!(
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
                    TerrainWorldFieldSampler.LandBalanceOffsetFor(profile),
                    handle.AddrOfPinnedObject(),
                    expectedCount);

            bool passed = written == expectedCount;
            if (!passed)
            {
                containsDerivedFields = false;
            }

            return passed;
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>Attempts to load the native library if not already initialized. Safe to call multiple times.</summary>
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
            "dao.windows.template_release.x86_64.dll",
            "dao.windows.template_debug.x86_64.dll",
            "dao.linux.template_release.x86_64.so",
            "dao.linux.template_debug.x86_64.so"
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

                if (NativeLibrary.TryGetExport(handle, "dao_native_sample_field_grid_v2", out IntPtr fieldGridV2Export))
                {
                    _sampleFieldGridV2 = Marshal.GetDelegateForFunctionPointer<SampleFieldGridV2Delegate>(fieldGridV2Export);
                }

                if (NativeLibrary.TryGetExport(handle, "dao_native_sample_field_grid_v1", out IntPtr fieldGridExport))
                {
                    _sampleFieldGridV1 = Marshal.GetDelegateForFunctionPointer<SampleFieldGridV1Delegate>(fieldGridExport);
                }

                if (NativeLibrary.TryGetExport(handle, "dao_native_sample_height_grid_v2", out IntPtr gridV2Export))
                {
                    _sampleHeightGridV2 = Marshal.GetDelegateForFunctionPointer<SampleHeightGridV2Delegate>(gridV2Export);
                }
                else if (NativeLibrary.TryGetExport(handle, "dao_native_sample_height_grid", out IntPtr gridExport))
                {
                    _sampleHeightGrid = Marshal.GetDelegateForFunctionPointer<SampleHeightGridDelegate>(gridExport);
                }

                if (_sampleFieldGridV2 is null && _sampleFieldGridV1 is null && _sampleHeightGridV2 is null && _sampleHeightGrid is null)
                {
                    NativeLibrary.Free(handle);
                    continue;
                }

                _libraryHandle = handle;
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

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SampleHeightGridV2Delegate(
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
        double landBalanceOffset,
        IntPtr outputHeights,
        int outputHeightCount);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SampleFieldGridV2Delegate(
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
        double landBalanceOffset,
        IntPtr outputSamples,
        int outputSampleFloatCount);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SampleFieldGridV1Delegate(
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
        double landBalanceOffset,
        IntPtr outputSamples,
        int outputSampleFloatCount);
}
