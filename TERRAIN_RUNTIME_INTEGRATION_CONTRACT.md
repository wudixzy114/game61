# Terrain Runtime Integration Contract

Updated: 2026-06-05

This document defines the gameplay-facing integration surface for the terrain runtime.
It is intentionally narrower than the full public terrain API and should be the default
dependency surface for quests, AI, resources, audio, map UI, and similar systems.

## Stable Runtime Interfaces

### `ITerrainQueryService`

Use for point sampling and semantic terrain queries.

- `SampleField(Vector2 world)`
- `SampleSurface(Vector2 world, float spacing = 4.0f)`
- `SurfacePositionAt(Vector2 world, float heightOffset = 0.0f)`
- `SampleWaterState(Vector2 world)`
- `SampleGameplayTags(Vector2 world)`
- `SampleTraversalCost(Vector2 world, float spacing = 4.0f)`
- `IsTraversable(Vector2 world, float minTraversability = 0.45f)`
- `IsAboveWater(Vector2 world, float margin = 0.0f)`

### `ITerrainPlanProvider`

Use for open-world plan snapshots and semantic world-layout queries.

- `GetWorldPlanSnapshot()`
- `TryGetWorldPlanSnapshot(out TerrainWorldPlanSnapshot? snapshot)`
- `GetPointsOfInterest()`
- `GetRoutes()`
- `TryFindNearestPointOfInterest(Vector2 world, float radius, TerrainPointOfInterestKind? kind, out TerrainWorldPointOfInterest point)`
- `QueryPointsOfInterest(Rect2 worldBounds, TerrainPointOfInterestKind? kind = null)`
- `QueryRoutesNear(Vector2 world, float radius)`
- `SampleRouteCorridor(Vector2 world)`

### `ITerrainStreamingDiagnostics`

Use for read-only runtime streaming diagnostics.

- `GetStreamingSnapshot()`

## Runtime Provider

`Dao.Terrain.Streaming.TerrainWorld` implements all three interfaces above.

Gameplay systems should prefer depending on one or more of these interfaces instead of:

- `TerrainWorld` concrete type
- `TerrainWorldPlanner`
- `TerrainTileBuilder`
- internal tile, cache, or streaming job structures

## Runtime Signals

`TerrainWorld` exposes optional Godot signals for push-based integration:

- `PlanReady`
- `PlanCleared`
- `ChunkLoaded(int x, int z, int lod, bool hasCollision)`
- `ChunkUnloaded(int x, int z, int lod, bool hadCollision)`
- `StreamingSnapshotChanged`

Recommended usage:

- quests, encounters, and map overlays react to `PlanReady`
- streaming observers and debug tools react to `StreamingSnapshotChanged`
- local runtime systems that care about tile residency react to `ChunkLoaded` and `ChunkUnloaded`

## API Layering

Preferred dependency order:

1. stable runtime interfaces in this document
2. `TerrainWorld` facade methods when Node access is required
3. serializer/exporter/tooling APIs
4. internal generation and streaming implementation details

New gameplay systems should not read `TerrainTileBuilder` internals directly.

## Validation

The PR terrain validation tier now verifies:

- `TerrainWorld` implements the stable runtime interfaces
- runtime signal delegates exist with the expected signatures
- existing runtime facade behavior remains unchanged
- open-world generation, artifacts, JSON roundtrip, anchor contract, and runtime materialization still pass
