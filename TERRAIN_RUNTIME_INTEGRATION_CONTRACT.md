# Terrain Runtime Integration Contract

Updated: 2026-06-06

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
- `QueryNearestPointsOfInterest(Vector2 world, float radius, int maxResults, TerrainPointOfInterestKind? kind = null)`
- `TryFindNearestPointOfInterest(Vector2 world, float radius, TerrainPointOfInterestKind? kind, out TerrainWorldPointOfInterest point)`
- `QueryPointsOfInterest(Rect2 worldBounds, TerrainPointOfInterestKind? kind = null)`
- `QueryGameplayTagRegions(Rect2 worldBounds, TerrainGameplayTag requiredTags, TerrainGameplayTag excludedTags = TerrainGameplayTag.None, int maxResults = 32)`
- `QueryRoutesNear(Vector2 world, float radius)`
- `QueryRouteSummariesNear(Vector2 world, float radius, int maxResults)`
- `SampleRouteCorridor(Vector2 world)`

`QueryNearestPointsOfInterest(...)` returns compact `TerrainWorldPointOfInterestSummary` rows
for low-allocation nearest-POI lookups when full point payload copies are unnecessary.

`QueryRouteSummariesNear(...)` returns compact `TerrainWorldRouteSummary` rows for nearby-route
queries without copying waypoint arrays.

`QueryGameplayTagRegions(...)` returns compact `TerrainGameplayTagRegionSummary` rows for bounded
region-level gameplay-tag lookups without exposing the full planning-grid array.

### `ITerrainStreamingDiagnostics`

Use for read-only runtime streaming diagnostics.

- `GetStreamingSnapshot()`

### `ITerrainPlacementService`

Use for terrain-driven resource, encounter, audio, and local interaction placement candidates.

- `QueryPlacementCandidates(Rect2 worldBounds, TerrainGameplayTag requiredTags, TerrainGameplayTag excludedTags = TerrainGameplayTag.None, int maxCandidates = 32, float sampleSpacing = 32.0f, float minTraversability = 0.45f, float maxTraversalCost = 2.4f, float maxHazardPotential = 1.0f, bool requireRouteInfluence = false, float minRouteInfluence = 0.0f)`

### `ITerrainNavigationProvider`

Use for navigation and map-graph handoff without embedding character pathfinding in the terrain system.

- `CreateTraversalCostGrid(Vector2 center, float worldSize, int gridSize, float spacing = 24.0f)`
- `GetRouteGraphSnapshot()`
- `TryGetRouteGraphSnapshot(out TerrainRouteGraphSnapshot? snapshot)`

`TerrainRouteGraphSnapshot` now provides stable high-level graph queries for importer and AI layers
without exposing planner internals:

- `ContainsPoint(int pointId)`
- `TryGetNode(int pointId, out TerrainRouteGraphNode node)`
- `QueryConnectedEdges(int pointId)`
- `TryFindPath(int fromPointId, int toPointId, out TerrainRouteGraphPath? path)`

`TryFindPath(...)` returns a `TerrainRouteGraphPath` with ordered point ids, directed edge copies,
collapsed waypoint geometry, total route cost, and total world distance. This remains a high-level
route handoff, not character pathfinding or navmesh ownership.

## Runtime Provider

`Dao.Terrain.Streaming.TerrainWorld` implements all five interfaces above.

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

The terrain module exposes more public C# types than gameplay systems should normally
consume. Treat the public surface as four layers:

- Stable Runtime API: the five interfaces in this document plus the value types returned
  by those interfaces. Quests, AI, resources, audio, map UI, encounter systems, and similar
  modules should depend on this layer by default.
- Tooling API: serializer, exporter, analyzer, map exporter, and validation helpers used by
  editor tools, CLI tools, and offline pipelines.
- Rendering Configuration API: `TerrainVisualCatalog` and its visual entry resources, used by
  `TerrainWorld`/`TerrainChunk` to replace primitive validation meshes with project assets.
- Data Contract API: plan snapshots, summaries, enums, reports, terrain samples, route graph
  snapshots, traversal grids, and anchor descriptors that may cross module or persistence
  boundaries.
- Internal Implementation API: tile builders, planners, streaming chunks, caches, native
  bridges, indices, scheduler/job structures, and service partials. These remain public where
  Godot, tooling, or validation needs direct access, but gameplay modules should not take
  dependencies on them.

Preferred dependency order:

1. stable runtime interfaces in this document
2. `TerrainWorld` facade methods when Node lifecycle, exported properties, or signals are required
3. Data Contract API values returned by the stable interfaces
4. Rendering Configuration API from terrain world setup and editor tooling
5. Tooling API from editor/CLI/offline code only
6. Internal Implementation API only from terrain internals, validation, demos, or explicitly reviewed tooling

New gameplay systems should not read `TerrainTileBuilder` internals directly.

Do not add a new public terrain type without first deciding which layer owns it. Stable runtime
interfaces require contract documentation and smoke coverage. Data contracts require shape or
serialization coverage when they cross persistence/module boundaries. Internal implementation
types should not be used as a shortcut from gameplay code.

### Stable Gameplay Integration Examples

- Quest systems should depend on `ITerrainPlanProvider` for POIs, routes, region tags, and plan
  snapshots.
- AI and high-level navigation should depend on `ITerrainNavigationProvider` for route graph
  and traversal cost handoff.
- Resource, encounter, audio, and local interaction systems should depend on
  `ITerrainPlacementService` for filtered placement candidates.
- Map UI should depend on `ITerrainPlanProvider` and serialized/exported plan artifacts, not on
  tile generation.
- Debug streaming panels should depend on `ITerrainStreamingDiagnostics`.

## Validation

The PR terrain validation tier now verifies:

- `TerrainWorld` implements the stable runtime interfaces
- limited-result POI, gameplay-tag region, and route summary queries return stable isolated results
- placement candidates respect requested tags, traversal filters, and route-influence requirements
- route-graph snapshots and traversal-cost grid handoff are stable and isolated
- route-graph node lookup, connected-edge lookup, and high-level path queries are stable and isolated
- runtime signal delegates exist with the expected signatures
- gameplay-facing scripts outside `dao/Scripts/Terrain` and `dao/Scripts/Demo` do not directly
  reference internal terrain implementation tokens such as `TerrainTileBuilder`,
  `TerrainWorldPlanner`, `TerrainChunk`, `TerrainTileDataCache`, `TerrainStreamingSetBuilder`,
  or `NativeTerrainBridge`
- existing runtime facade behavior remains unchanged
- open-world generation, artifacts, JSON roundtrip, anchor contract, and runtime materialization still pass
