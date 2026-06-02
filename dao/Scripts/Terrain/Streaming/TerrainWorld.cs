using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dao.Terrain.Generation;
using Dao.Terrain.Rendering;
using Godot;

namespace Dao.Terrain.Streaming;

[GlobalClass]
public partial class TerrainWorld : Node3D
{
    [Export] public TerrainSettings? Settings { get; set; }
    [Export] public NodePath FocusPath { get; set; } = new();
    [Export(PropertyHint.Range, "0.05,2,0.01")] public double StreamingIntervalSeconds { get; set; } = 0.18;
    [Export] public bool CreateWaterPlane { get; set; } = true;

    private readonly Dictionary<TerrainTileCoord, TerrainChunk> _chunks = new();
    private readonly Dictionary<TerrainTileCoord, PendingTileJob> _jobs = new();
    private readonly HashSet<TerrainTileCoord> _desiredCoords = new();
    private TerrainGenerationProfile _profile;
    private Node3D? _focus;
    private Material _terrainMaterial = null!;
    private Material _waterMaterial = null!;
    private MeshInstance3D? _waterPlane;
    private double _streamTimer;

    public override void _Ready()
    {
        Settings ??= new TerrainSettings();
        _profile = Settings.Snapshot();
        if (_profile.UseNativeSamplerWhenAvailable)
        {
            NativeTerrainBridge.EnsureInitialized();
            if (!NativeTerrainBridge.IsAvailable)
            {
                GD.PushWarning("Native terrain sampler requested but unavailable; using managed C# sampler.");
            }
        }

        _terrainMaterial = TerrainMaterialFactory.CreateTerrainMaterial();
        _waterMaterial = TerrainMaterialFactory.CreateWaterMaterial();

        ResolveFocus();

        if (CreateWaterPlane)
        {
            CreateWater();
        }

        UpdateStreaming(force: true);
    }

    public override void _Process(double delta)
    {
        SubmitCompletedJobs();

        _streamTimer += delta;
        if (_streamTimer >= StreamingIntervalSeconds)
        {
            _streamTimer = 0.0;
            UpdateStreaming(force: false);
        }

        UpdateWaterPlane();
    }

    public void SetFocus(Node3D focus)
    {
        _focus = focus;
        UpdateStreaming(force: true);
    }

    public void Regenerate()
    {
        Settings ??= new TerrainSettings();
        _profile = Settings.Snapshot();
        if (_profile.UseNativeSamplerWhenAvailable)
        {
            NativeTerrainBridge.EnsureInitialized();
        }

        foreach (TerrainChunk chunk in _chunks.Values)
        {
            chunk.QueueFree();
        }

        _chunks.Clear();
        _jobs.Clear();
        UpdateStreaming(force: true);
    }

    private void ResolveFocus()
    {
        if (_focus is not null && IsInstanceValid(_focus))
        {
            return;
        }

        if (!FocusPath.IsEmpty)
        {
            _focus = GetNodeOrNull<Node3D>(FocusPath);
        }

        _focus ??= GetViewport()?.GetCamera3D();
    }

    private void UpdateStreaming(bool force)
    {
        ResolveFocus();
        if (_focus is null)
        {
            return;
        }

        TerrainTileCoord center = TerrainTileCoord.FromWorldPosition(_focus.GlobalPosition, _profile.ChunkSize);
        BuildDesiredSet(center);
        UnloadUndesiredChunks();
        DropStaleJobs(center);
        QueueMissingOrOutdatedChunks(center);
    }

    private void BuildDesiredSet(TerrainTileCoord center)
    {
        _desiredCoords.Clear();
        int radius = _profile.StreamRadiusChunks;
        int radiusSquared = radius * radius;

        for (int z = -radius; z <= radius; z++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if ((x * x) + (z * z) > radiusSquared)
                {
                    continue;
                }

                _desiredCoords.Add(new TerrainTileCoord(center.X + x, center.Z + z));
            }
        }
    }

    private void QueueMissingOrOutdatedChunks(TerrainTileCoord center)
    {
        if (_focus is null)
        {
            return;
        }

        Vector3 focusPosition = _focus.GlobalPosition;
        List<TerrainTileCoord> sorted = _desiredCoords
            .OrderBy(coord => coord.CenterDistanceSquaredTo(focusPosition, _profile.ChunkSize))
            .ToList();

        foreach (TerrainTileCoord coord in sorted)
        {
            if (_jobs.Count >= _profile.MaxQueuedTileJobs)
            {
                return;
            }

            DesiredTileRequest request = GetDesiredRequest(coord, center);
            if (_chunks.TryGetValue(coord, out TerrainChunk? chunk) &&
                chunk.Lod == request.Lod &&
                chunk.HasCollision == request.IncludeCollision)
            {
                continue;
            }

            if (_jobs.TryGetValue(coord, out PendingTileJob? existing) &&
                existing.Lod == request.Lod &&
                existing.IncludeCollision == request.IncludeCollision)
            {
                continue;
            }

            _jobs[coord] = new PendingTileJob(
                coord,
                request.Lod,
                request.IncludeCollision,
                Task.Run(() => TerrainTileBuilder.Build(coord, request.Lod, _profile, request.IncludeCollision)));
        }
    }

    private void SubmitCompletedJobs()
    {
        int submitted = 0;
        var completed = _jobs.Values.Where(job => job.Task.IsCompleted).ToList();

        foreach (PendingTileJob job in completed)
        {
            if (submitted >= _profile.MaxCompletedTilesPerFrame)
            {
                break;
            }

            if (!_jobs.TryGetValue(job.Coord, out PendingTileJob? current) || current != job)
            {
                continue;
            }

            _jobs.Remove(job.Coord);

            if (!_desiredCoords.Contains(job.Coord))
            {
                continue;
            }

            if (job.Task.IsFaulted)
            {
                GD.PushError($"Terrain tile {job.Coord} failed: {job.Task.Exception?.GetBaseException().Message}");
                continue;
            }

            TerrainTileData data = job.Task.Result;
            TerrainChunk chunk = GetOrCreateChunk(job.Coord);
            chunk.Apply(data, _terrainMaterial);
            submitted++;
        }
    }

    private TerrainChunk GetOrCreateChunk(TerrainTileCoord coord)
    {
        if (_chunks.TryGetValue(coord, out TerrainChunk? existing))
        {
            return existing;
        }

        var chunk = new TerrainChunk();
        _chunks.Add(coord, chunk);
        AddChild(chunk);
        return chunk;
    }

    private void UnloadUndesiredChunks()
    {
        foreach (TerrainTileCoord coord in _chunks.Keys.ToList())
        {
            if (_desiredCoords.Contains(coord))
            {
                continue;
            }

            TerrainChunk chunk = _chunks[coord];
            _chunks.Remove(coord);
            chunk.QueueFree();
        }
    }

    private void DropStaleJobs(TerrainTileCoord center)
    {
        foreach (PendingTileJob job in _jobs.Values.ToList())
        {
            if (!_desiredCoords.Contains(job.Coord))
            {
                _jobs.Remove(job.Coord);
                continue;
            }

            DesiredTileRequest request = GetDesiredRequest(job.Coord, center);
            if (request.Lod != job.Lod || request.IncludeCollision != job.IncludeCollision)
            {
                _jobs.Remove(job.Coord);
            }
        }
    }

    private DesiredTileRequest GetDesiredRequest(TerrainTileCoord coord, TerrainTileCoord center)
    {
        int distance = coord.ChebyshevDistanceTo(center);
        bool includeCollision = _profile.GenerateCollision && distance <= _profile.CollisionRadiusChunks;
        int lod = includeCollision ? 0 : Mathf.Clamp((distance - 1) / 2, 0, _profile.MaxLod);
        return new DesiredTileRequest(lod, includeCollision);
    }

    private void CreateWater()
    {
        float waterSize = _profile.ChunkSize * (_profile.StreamRadiusChunks * 2 + 6);
        var planeMesh = new PlaneMesh
        {
            Size = new Vector2(waterSize, waterSize),
            SubdivideWidth = 8,
            SubdivideDepth = 8
        };

        _waterPlane = new MeshInstance3D
        {
            Name = "Water",
            Mesh = planeMesh,
            MaterialOverride = _waterMaterial
        };

        AddChild(_waterPlane);
        UpdateWaterPlane();
    }

    private void UpdateWaterPlane()
    {
        if (_waterPlane is null || _focus is null)
        {
            return;
        }

        Vector3 focusPosition = _focus.GlobalPosition;
        float grid = _profile.ChunkSize;
        _waterPlane.Position = new Vector3(
            Mathf.Round(focusPosition.X / grid) * grid,
            _profile.SeaLevel,
            Mathf.Round(focusPosition.Z / grid) * grid);
    }

    private sealed record PendingTileJob(
        TerrainTileCoord Coord,
        int Lod,
        bool IncludeCollision,
        Task<TerrainTileData> Task);

    private readonly record struct DesiredTileRequest(int Lod, bool IncludeCollision);
}
