# 地形运行时 API 契约

日期：2026-06-04  
适用项目：`d:\game61`  
当前契约范围：`TerrainWorld` 运行时查询 facade、运行时流送诊断快照、开放世界 plan 只读访问、基础地形语义采样、确定性 epsilon 边界、runtime gameplay anchor group/meta 契约、`terrain-plan-v1` JSON 持久化契约。

## 1. 契约目标

地形系统对外承担“世界地表和地形语义的确定性生成与运行时供给”。其他游戏模块应优先依赖本文件列出的稳定入口，而不是直接组合地形内部实现细节。

本契约当前覆盖：

- 任意世界坐标的地形语义查询。
- 表面高度、坡度、颜色和 Godot 3D 坐标转换。
- 当前开放世界 plan 的空态和就绪态访问。
- 当前开放世界 plan 的隔离快照访问。
- 运行时流送状态的只读诊断快照。
- POI 和 route 快照读取、范围查询和近邻查询。
- 静态水体语义和 gameplay tag 采样。
- deterministic、native parity 和快照/序列化比较 epsilon。
- POI 和 route gameplay anchor group/meta descriptor。
- `terrain-plan-v1` plan JSON 导出/读取、profile hash 校验和 roundtrip 验证。
- 基础通行性和水面判断。
- 对应 CLI 验证工具 smoke。

本契约当前不覆盖：

- 最终美术资产选择。
- 任务、AI、资源、天气、存档、导航烘焙逻辑。
- tile 生成内部散布概率和 landmark primitive 表现。
- 存档 delta、玩家改造和跨版本世界迁移策略。

## 2. 稳定入口

一级运行时入口位于：

- `dao/Scripts/Terrain/Streaming/TerrainWorld.cs`
- `dao/Scripts/Terrain/Streaming/TerrainWorldStreamingSnapshot.cs`
- `dao/Scripts/Terrain/TerrainApiVersion.cs`
- `dao/Scripts/Terrain/TerrainDeterminismContract.cs`
- `dao/Scripts/Terrain/Runtime/TerrainWorldAnchorContract.cs`
- `dao/Scripts/Terrain/Runtime/TerrainWorldAnchorBuilder.cs`
- `dao/Scripts/Terrain/Generation/TerrainSemanticQueryData.cs`
- `dao/Scripts/Terrain/Generation/TerrainWorldPlanSerializer.cs`
- `dao/Scripts/Terrain/TerrainProfileHash.cs`

其他模块优先调用：

```csharp
TerrainGenerationProfile profile = terrainWorld.Profile;
TerrainWorldField field = terrainWorld.SampleField(world);
TerrainSample surface = terrainWorld.SampleSurface(world);
Vector3 position = terrainWorld.SurfacePositionAt(world);
bool hasPlan = terrainWorld.TryGetWorldPlan(out TerrainWorldPlan? plan);
TerrainWorldPlanSnapshot snapshot = terrainWorld.GetWorldPlanSnapshot();
bool hasSnapshot = terrainWorld.TryGetWorldPlanSnapshot(out TerrainWorldPlanSnapshot? planSnapshot);
TerrainWorldStreamingSnapshot streaming = terrainWorld.GetStreamingSnapshot();
TerrainWorldPointOfInterest[] points = terrainWorld.GetPointsOfInterest();
TerrainWorldRoute[] routes = terrainWorld.GetRoutes();
bool foundPoint = terrainWorld.TryFindNearestPointOfInterest(world, radius, kind: null, out TerrainWorldPointOfInterest point);
TerrainWorldPointOfInterest[] nearbyPoints = terrainWorld.QueryPointsOfInterest(worldBounds);
TerrainWorldRoute[] nearbyRoutes = terrainWorld.QueryRoutesNear(world, radius);
TerrainWaterState water = terrainWorld.SampleWaterState(world);
TerrainGameplayTags tags = terrainWorld.SampleGameplayTags(world);
bool traversable = terrainWorld.IsTraversable(world);
bool aboveWater = terrainWorld.IsAboveWater(world);
string contract = TerrainApiVersion.Contract;
string version = TerrainApiVersion.Version;
string determinismContract = TerrainDeterminismContract.Contract;
float planPositionEpsilon = TerrainDeterminismContract.PositionEpsilon;
string profileHash = profile.StableHash();
string planJson = TerrainWorldPlanSerializer.ToJson(plan, profile);
bool loaded = TerrainWorldPlanSerializer.TryFromJson(planJson, profile, out TerrainWorldPlan? loadedPlan, out string error);
```

其中 `world` 是 Godot XZ 平面坐标，类型为 `Vector2(worldX, worldZ)`。

## 3. 方法契约

### `TerrainWorld.Profile`

- 返回当前不可变 `TerrainGenerationProfile`。
- 如果节点尚未进入 `_Ready()`，会从 `Settings` 创建安全快照。
- 读取 profile 不触发 tile 生成，不触发同步 plan 生成。

### `SampleField(Vector2 world)`

- 使用当前 `Profile` 调用 `TerrainWorldFieldSampler.Sample(world, profile)`。
- 返回完整地形语义，包括高度、生物群系、地貌、水系、通行性、资源、危险、遭遇和风景潜力。
- 不要求 `WorldPlan` 已生成。
- 不触发 tile 生成，不触发同步 plan 生成。

### `SampleSurface(Vector2 world, float spacing = 4.0f)`

- 使用当前 `Profile` 调用 `TerrainSampler.SampleWithSlope(world, profile, spacing)`。
- 返回高度、坡度、颜色、生物群系、地貌和通行性。
- `spacing` 会沿用 sampler 内部安全钳制规则。

### `SurfacePositionAt(Vector2 world, float heightOffset = 0.0f)`

- 返回 `Vector3(world.X, sampledHeight + heightOffset, world.Y)`。
- 明确约定：输入 `Vector2.X` 映射 Godot `Vector3.X`，输入 `Vector2.Y` 映射 Godot `Vector3.Z`，采样高度映射 Godot `Vector3.Y`。
- 用于减少任务、特效、AI、音频等模块的 XZ/XY 坐标转换错误。

### `TryGetWorldPlan(out TerrainWorldPlan? plan)`

- 当当前 plan 未生成或已清空时，返回 `false`，输出 `null`。
- 当当前 plan 已生成或通过 `SetWorldPlan` 指定时，返回 `true`，输出当前 plan 的隔离副本。
- 不隐式同步生成 plan。
- `Regions`、`PointsOfInterest`、`Routes` 数组都会复制，route 的 `Waypoints` 数组会深拷贝。
- 调用方修改返回 plan、数组、route 或 waypoint 不会改变 `TerrainWorld` 内部 plan。
- 普通玩法模块仍应优先使用 `GetWorldPlanSnapshot()`、`TryGetWorldPlanSnapshot(...)`、POI/route 查询 facade 或 gameplay anchors，减少对 plan 具体结构的耦合。

### `TerrainWorld.WorldPlan`

- plan 未就绪时返回 `null`。
- plan 就绪时返回当前 plan 的隔离副本。
- 复制规则与 `TryGetWorldPlan(...)` 一致，不暴露内部 region、POI、route 或 waypoint 数组。

### `SetWorldPlan(TerrainWorldPlan? worldPlan)`

- 输入 `null` 时清空当前 plan。
- 输入非空 plan 时，`TerrainWorld` 会先复制 plan，再重建 route corridor 和 POI footprint 索引。
- 调用方在 `SetWorldPlan(...)` 之后继续修改传入 plan、数组、route 或 waypoint，不会改变 `TerrainWorld` 内部 plan。
- 应用或清空 plan 会取消不再匹配的 plan/tile job，清空 tile cache，并在已 ready 时重建 streaming chunks。

### `GetWorldPlanSnapshot()`

- 当 plan 未就绪时，返回空 `TerrainWorldPlanSnapshot`。
- 当 plan 就绪时，返回当前 plan 的隔离快照。
- `Regions`、`PointsOfInterest`、`Routes` 数组都会复制。
- route 的 `Waypoints` 数组会深拷贝。
- 调用方修改返回 snapshot、数组、route 或 waypoint 不会改变 `TerrainWorld` 内部 plan。

### `TryGetWorldPlanSnapshot(out TerrainWorldPlanSnapshot? snapshot)`

- 当当前 plan 未生成或已清空时，返回 `false`，输出 `null`。
- 当当前 plan 已生成或通过 `SetWorldPlan` 指定时，返回 `true`，输出隔离快照。
- 不隐式同步生成 plan。
- 普通任务、AI、资源、导航、音频和 UI 模块应优先使用该入口读取 plan 级数据。

### `GetPointsOfInterest()`

- plan 未就绪时返回空数组。
- plan 就绪时返回 POI 数组快照。
- 调用方修改返回数组不会改变 `TerrainWorld` 内部 plan。

### `GetRoutes()`

- plan 未就绪时返回空数组。
- plan 就绪时返回 route 数组快照。
- route 的 `Waypoints` 数组也会复制；调用方修改返回 route 或 waypoint 不会改变 `TerrainWorld` 内部 plan。

### `GetStreamingSnapshot()`

- 返回当前 `TerrainWorld` 流送状态的只读诊断快照。
- 包含当前 profile、focus 状态、focus tile、stream radius、desired chunks、loaded chunks、queued tile jobs、retired jobs、tile cache 数量/上限、tile job 队列上限、每帧完成上限、world plan 就绪/pending 状态和 `StreamTerrainBeforeOpenWorldPlanReady` 策略。
- `DesiredChunks`、`LoadedChunks` 和 `QueuedTileJobs` 数组会复制并按 X/Z 稳定排序；调用方修改返回数组不会改变 `TerrainWorld` 内部状态。
- 不触发 tile 生成，不触发同步 plan 生成，不取消/提交 job，不改变缓存 LRU。
- 该入口用于调试、监控、CI smoke 和上层系统做非侵入式 readiness 判断；普通玩法逻辑不应依赖具体 queue 完成顺序。
- `TileCacheWithinLimit` 和 `TileJobQueueWithinLimit` 可用于快速判断流送状态是否超出 profile 契约上限。

### `TryFindNearestPointOfInterest(Vector2 world, float radius, TerrainPointOfInterestKind? kind, out TerrainWorldPointOfInterest point)`

- plan 未就绪时返回 `false`。
- 在指定半径内返回最近 POI，可选按 `TerrainPointOfInterestKind` 过滤。
- 不触发 tile 生成，不触发同步 plan 生成。
- 复杂度为当前 plan POI 数量的线性扫描；返回值是值类型快照，不暴露内部数组。

### `QueryPointsOfInterest(Rect2 worldBounds, TerrainPointOfInterestKind? kind = null)`

- plan 未就绪时返回空数组。
- 返回位于世界 XZ bounds 内的 POI，可选按 kind 过滤。
- 支持负 size 的 `Rect2` bounds，会按 min/max 包围盒处理。
- 不触发 tile 生成，不触发同步 plan 生成。
- 复杂度为当前 plan POI 数量的线性扫描；返回数组可由调用方自由修改。

### `QueryRoutesNear(Vector2 world, float radius)`

- plan 未就绪时返回空数组。
- 返回 waypoint polyline 与查询点距离小于等于半径的 route。
- 不触发 tile 生成，不触发同步 plan 生成。
- 复杂度为 route 数量乘以 route waypoint 段数；返回 route 数组和每条 route 的 waypoint 数组都会复制。

### `SampleWaterState(Vector2 world)`

- 使用当前 `Profile` 和 `TerrainSemanticClassifier.ClassifyWater(...)`。
- 返回静态地形水体语义：`None`、`Ocean`、`Coast`、`Lake`、`River`、`Oasis`。
- 返回水面高度、深度、强度、生物群系和地貌来源。
- 不要求 plan 已生成，不触发 tile 生成，不触发同步 plan 生成。
- 不处理动态水体、玩法水体或运行时特殊水位。

### `SampleGameplayTags(Vector2 world)`

- 使用当前 `Profile` 和 `TerrainSemanticClassifier.ClassifyGameplayTags(...)`。
- 返回 gameplay-facing bit flags，例如 `Traversable`、`Scenic`、`ResourceRich`、`Hazardous`、`EncounterRich`、`WaterAccess`、`Coastal`、`SettlementFriendly`、`HighElevation`、`Cold`、`Arid`。
- 同时返回 tag 来源分数，便于上层模块做二次阈值判断。
- 不要求 plan 已生成，不触发 tile 生成，不触发同步 plan 生成。

### `IsTraversable(Vector2 world, float minTraversability = 0.45f)`

- 使用 `SampleField(world).Traversability`。
- 阈值会钳制到 `0.0f..1.0f`。
- 默认阈值偏保守，适合普通落点/行走候选初筛。

### `IsAboveWater(Vector2 world, float margin = 0.0f)`

- 使用当前 profile 的 `SeaLevel`。
- 判断 `SampleField(world).Height >= Profile.SeaLevel + margin`。
- 不处理动态水体、玩法水体或运行时特殊水位。

### `TerrainApiVersion`

- `TerrainApiVersion.Contract` 当前固定为 `terrain-api-v1`。
- `TerrainApiVersion.Version` 当前固定为 `1.0.0`。
- `Major/Minor/Patch` 当前为 `1/0/0`。
- plan 文本报告必须输出 API contract/version、plan contract、generator version、determinism contract 和 profile hash。
- 破坏性 API 变更必须提升 major 版本，并同步更新本契约和验证工具。

### `TerrainDeterminismContract`

- `TerrainDeterminismContract.Contract` 当前固定为 `terrain-determinism-v1`。
- `ExactFloatEpsilon` 和 `ExactPositionEpsilon` 用于同进程 facade、快照、anchor descriptor 和 JSON roundtrip 的严格一致性检查。
- `HeightEpsilon`、`FieldEpsilon`、`PositionEpsilon` 用于地形 deterministic contract 的跨实现/跨运行比较边界。
- `NativeHeightMaxEpsilon`、`NativeHeightAverageEpsilon`、`NativeFieldMaxEpsilon`、`NativeFieldAverageEpsilon`、`NativeTileHeightEpsilon` 和 `NativeTileColorEpsilon` 用于 native/managed parity smoke。
- `TileParityHeightEpsilon` 和 `TileParityColorEpsilon` 用于 tile benchmark parity 检查。
- 普通玩法模块可以依赖 deterministic contract 输出；视觉表现、scatter 精确位置、primitive 尺寸和 debug overlay 绘制顺序只按近似或平台相关处理。

### `TerrainGenerationProfile.StableHash()`

- 返回当前 generation profile 的稳定 SHA-256 内容身份。
- hash 使用 invariant culture 和固定字段顺序。
- plan JSON 必须写入 `profileHash`。
- open world plan 文本报告必须写入 `Terrain Profile Hash`。
- 带 `expectedProfile` 的 plan JSON 读取入口必须拒绝 seed 或 profile hash 不匹配的数据。

### `TerrainWorldPlanSerializer`

- `TerrainWorldPlanSerializer.Contract` 当前固定为 `terrain-plan-v1`。
- `TerrainWorldPlanSerializer.GeneratorVersion` 当前固定为 `1.0.0`。
- `ToJson(plan, profile)` 输出当前 plan 的稳定 JSON schema。
- `TryFromJson(json, out plan, out error)` 只接受当前 plan/API/generator 版本。
- `TryFromJson(json, expectedProfile, out plan, out error)` 还会检查 seed 和 profile hash。
- `SaveJson(...)` 和 `TryLoadJson(...)` 使用同一 schema。
- `Vector2` 固定序列化为 `{ "x": number, "z": number }`，避免 XZ/XY 混淆。
- enum 固定序列化为 `{ "name": string, "value": int }`；读取时必须同时校验 name 和 value。
- route waypoint 必须序列化为独立数组，读取后不得共享原 plan 内部数组。

## 4. 兼容性规则

公开契约遵循：

- public 方法只追加，不随意删除或重命名。
- enum 只追加，不重排。
- group 名称只追加，不重命名。
- meta key 只追加，不重命名。
- record 字段谨慎改名；破坏性调整必须写迁移说明。
- facade 查询方法不得引入重型同步 plan 生成。
- 普通模块无法通过 `WorldPlan`、`TryGetWorldPlan` 或 `SetWorldPlan` 输入对象修改 `TerrainWorld` 内部 plan 数组。
- plan 级稳定读取应优先使用 `TerrainWorldPlanSnapshot`。
- POI/routes facade 返回快照，不暴露内部可变数组。
- `TerrainWorldPlan` 构造时必须复制 region、POI、route 数组，并深拷贝 route waypoint 数组。
- `TerrainWorldPlanSnapshot` 必须复制 region、POI、route 数组，并深拷贝 route waypoint 数组。
- `TerrainWorldStreamingSnapshot` 必须复制 chunk/job 坐标数组，不暴露内部 set、dictionary 或 LRU 状态。
- anchor group/meta 名称以 `TerrainWorldAnchorContract` 为准。
- `TerrainWorldPointOfInterestAnchor` 和 `TerrainWorldRouteAnchor` 的公开常量必须与 `TerrainWorldAnchorContract` 保持一致。
- anchor descriptor 不得泄露 route waypoint 内部可变数组。
- plan JSON 必须写入 `contract`、`apiContract`、`apiVersion`、`generatorVersion`、`seed`、`profileHash`。
- plan JSON 当前只读取 `terrain-plan-v1` / `terrain-api-v1` / API `1.0.0` / generator `1.0.0`；不兼容 contract 或 version 必须返回明确错误。
- plan JSON enum name/value 不得漂移。

## 5. 验证命令

PR 默认门槛使用固定 validation tier，运行默认 seed 的 runtime API smoke、plan JSON roundtrip smoke、anchor contract smoke 和运行时世界 smoke：

```powershell
dotnet run --project tools\TerrainValidation\TerrainValidation.csproj -- --validation-tier pr
```

分层验证入口：

```powershell
dotnet run --project tools\TerrainValidation\TerrainValidation.csproj -- --validation-tier pr
dotnet run --project tools\TerrainValidation\TerrainValidation.csproj -- --validation-tier nightly
dotnet run --project tools\TerrainValidation\TerrainValidation.csproj -- --validation-tier release
```

- `pr`：1 个固定默认 seed，默认 smoke 全部开启，不跑 native/benchmark。
- `nightly`：10 个固定 seed，对每个 seed 运行全部 smoke。
- `release`：25 个固定 seed，对每个 seed 运行全部 smoke，并启用 native parity 和 tile benchmark。
- 显式 tier 不能与 `--skip-*`、`--seed*`、`--world-size`、`--smoke-all-seeds`、`--native-smoke`、`--benchmark-tiles` 等覆盖参数混用，避免 CI 门槛被意外削弱。

本次契约对应的输出项：

```text
Runtime TerrainWorld API smoke: PASS
Plan JSON roundtrip smoke: PASS
Terrain anchor contract smoke: PASS
```

该 smoke 覆盖：

- `SampleField` 与底层 sampler 一致。
- `SampleSurface` 与底层 sampler 一致。
- `SurfacePositionAt` 坐标轴正确。
- `TerrainApiVersion` 为 `terrain-api-v1` / `1.0.0`。
- `TerrainDeterminismContract` 为 `terrain-determinism-v1`，关键 epsilon 未漂移。
- plan 空态返回 false 和空集合。
- plan 就绪态返回 POI/route 数量正确。
- `WorldPlan`、`TryGetWorldPlan`、`SetWorldPlan` 输入和 plan facade 返回值不会泄露内部可变状态。
- `IsTraversable` 和 `IsAboveWater` 与采样字段一致。
- `TryFindNearestPointOfInterest`、`QueryPointsOfInterest` 和 `QueryRoutesNear` 返回正确结果且不泄露 route waypoint 内部数组。
- `SampleWaterState` 与共享地形语义分类器一致。
- `SampleGameplayTags` 与共享地形语义分类器一致。
- `GetStreamingSnapshot` 返回稳定、隔离的流送诊断快照，且 cache/job 上限判断未漂移。
- POI 数组、route 数组和 route waypoint 数组不会泄露内部可变状态。
- `TerrainWorldPlanSnapshot` 的 region、POI、route 和 route waypoint 数组不会泄露内部可变状态。
- 导出的 open world plan report 包含 API contract/version、plan contract、generator version、determinism contract 和 profile hash。

plan JSON roundtrip smoke 覆盖：

- JSON 顶层 metadata 包含 plan/API/generator version、seed 和 profile hash。
- plan string roundtrip 后 region、POI、route、waypoint 和 report 关键数据一致。
- plan file save/load roundtrip 成功。
- seed mismatch 和 profile hash mismatch 会被拒绝。
- plan/API/generator contract 或 version drift 会被拒绝。
- enum name/value drift 会被拒绝。
- roundtrip plan 的数组和 route waypoint 不共享原 plan 内部可变状态。

anchor contract smoke 覆盖：

- `terrain_poi` 和 `terrain_route` group 名称未漂移。
- POI 和 route 必需 meta key 未漂移。
- anchor 节点公开常量与 `TerrainWorldAnchorContract` 一致。
- POI/route descriptor 数量与 plan 一致。
- POI/route descriptor 的 group、name、archetype、route 字段与 plan 一致。
- route descriptor 的 waypoint 数组不会泄露 plan 内部可变状态。
- descriptor 可重复构建且结果稳定。

如需要临时跳过该检查，可使用：

```powershell
dotnet run --project tools\TerrainValidation\TerrainValidation.csproj -- --skip-runtime-api-smoke
```

anchor contract smoke 可用以下参数临时跳过：

```powershell
dotnet run --project tools\TerrainValidation\TerrainValidation.csproj -- --skip-anchor-smoke
```

plan JSON roundtrip smoke 可用以下参数临时跳过：

```powershell
dotnet run --project tools\TerrainValidation\TerrainValidation.csproj -- --skip-plan-json-smoke
```

## 6. 后续契约缺口

下一批应补：

- 将 `--validation-tier pr/nightly/release` 接入外部 CI runner。
- Native parity 和 tile benchmark 的目标硬件基线。
