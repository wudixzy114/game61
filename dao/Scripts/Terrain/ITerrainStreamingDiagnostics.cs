using Dao.Terrain.Streaming;

namespace Dao.Terrain;

/// <summary>Read-only streaming diagnostics facade for runtime observers and tooling.</summary>
public interface ITerrainStreamingDiagnostics
{
    TerrainWorldStreamingSnapshot GetStreamingSnapshot();
}
