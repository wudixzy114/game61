# 程序化地形生成系统阶段性审核与开发计划

审核日期：2026-06-06  
项目路径：`d:\game61`  
审核对象：`dao/Scripts/Terrain`、`tools/TerrainValidation`、`gdextension/src`、`.github/workflows`  
目标标准：单人可长期维护的商业级 3D 开放世界程序化地形基础设施

## 1. 总结论

当前地形系统已经不是简单地形 Demo，而是一个可运行、可验证、可被其他模块接入的开放世界地形基础层。它已经实现了确定性地形场采样、世界规划、POI 和路线生成、tile mesh 生成、局部水面、碰撞、生态和玩法 scatter、地标实体化、运行时流送、开放世界 plan 导出、JSON 持久化、runtime anchor、语义查询、导航交接数据、GDExtension native sampler 以及 CI 验证门禁。

但它还没有达到最终 3A 商业生产级地形管线。核心原因不是功能没有落地，而是生产化链路仍缺关键环节：Godot 编辑器插件、预览和批量调参工具、正式资产实例化管线、导航数据烘焙和运行时导航集成、动态地形修改和存档 delta、正式目标硬件性能基线、HLOD/接缝过渡、世界分区持久化，以及更严格的 public API 分层。

阶段性判断：

| 维度 | 当前等级 | 结论 |
| --- | --- | --- |
| 功能完整度 | A- 基础设施 / B 生产管线 | 地形生成、规划、流送、语义、导出、验证已成体系；最终游戏生产还缺资产、编辑器、导航、持久化和动态世界。 |
| 模块集成能力 | A | 五个稳定 runtime interface、Godot signals、anchor contract、plan snapshot、placement candidate、route graph 已能服务任务、AI、资源、地图和音频。 |
| API 暴露 | A- | 暴露丰富且被 shape smoke 锁定；但 public 面已达 99 类型、1014 成员，后续必须区分稳定 API 和实现细节。 |
| 职责边界 | B+ | 地形系统基本只提供地形、语义、世界布局和交接数据，没有直接承担任务或 AI；但 planner、tile builder、runtime world 仍是大静态/partial 门面。 |
| 扩展性 | B+ | 可以添加新 POI、route、scatter、landmark、规则集参数；但仍偏枚举和固定槽位扩展，不是真正数据驱动插件化。 |
| 维护性 | B+ | CI、确定性、hash、JSON、API contract 很强；主要风险是验证工具和 native/C# 双实现体量大，长期演进会有同步成本。 |
| 解耦性 | B+ | 对外解耦较好，内部仍需要继续拆分策略、构建器、可视化和平台后端。 |
| Godot 可配置性 | B+ | 已可通过 `[GlobalClass] Resource` 配置核心参数和规则集；缺编辑器预览、预设库、参数校验 UI 和一键导出工作流。 |
| 商业级代码质量 | B+ | 工程质量明显高于原型；若要按 3A 长期维护标准，需要补齐工具链、资产管线、正式性能和动态世界能力。 |

一句话结论：

当前系统可以作为开放世界玩法系统的上游基础层继续集成，但不能直接宣称地形管线已经商业生产完成。下一阶段应优先把“稳定集成 API、编辑器工具、资产实例化、导航交接、性能基线、动态持久化”做成可维护的生产链路，而不是继续只往生成算法里追加内容。

## 1.1 同日实现更新

在本次审计文档形成后，已补充一批直接面向生产链路的实现：

- 新增初版 Godot `TerrainEditorPlugin` / editor dock：`dao/addons/terrain_editor/*`
- 新增仓库内默认 `TerrainSettings` 资源：`dao/Resources/Terrain/DefaultTerrainSettings.tres`
- `Main.tscn` 与 `TerrainDemo` 已优先消费资源化 `TerrainSettings`，而不是只靠代码内临时 new settings
- editor dock 已支持：
  - 选择或粘贴 `TerrainSettings` 资源路径
  - world plan preview
  - seed override preview
  - 语义采样
  - route graph path preview
  - 一键导出 artifact
  - 一键运行 PR validation
  - `TerrainSettings` preset copy 保存
- `TerrainWorldPlanExporter` 已从“map + report”扩展为统一 artifact bundle，正式导出：
  - plan JSON
  - plan map
  - traversal cost map
  - text report
- `TerrainApiVersion` 已推进到 `terrain-api-v1` / `1.8.0`
- `TraversalCostGrid` handoff 已补齐局部查询能力：
  - `TerrainTraversalCostGrid.WorldBounds`
  - grid index 和 world position 互查
  - nearest sample 查询
  - bounded `QuerySamples(Rect2, maxSamples)`
  - tile-bounded `CreateTraversalCostGridForTile`
  - region-bounded `QueryTraversalCosts`
- `TerrainValidation` 已新增 editor plugin smoke，验证：
  - plugin scaffold 存在
  - 默认 `TerrainSettings` 资源存在
  - `Main.tscn` 资源接线存在
  - editor dock 暴露 preview/export/validation/preset 工作流入口
- `TerrainValidation` 已新增/扩展 traversal cost handoff smoke，验证：
  - center grid、tile grid、region query 均和 `TerrainMapExporter` / classifier 输出一致
  - grid snapshot 和查询数组隔离
  - world/index helper、bounded query、max sample cap 稳定

基于当前最新本地验证：

- `dotnet build tools\TerrainValidation\TerrainValidation.csproj --configuration Release -m:1 -p:UseSharedCompilation=false -nr:false` 成功
- `dotnet .\tools\TerrainValidation\bin\Release\net8.0\TerrainValidation.dll --validation-tier pr` 成功
- `Open world terrain validation: PASS`
- `Auxiliary checks: PASS (19/19 checks passed)`
- `Terrain editor plugin smoke: PASS`
- `Runtime TerrainWorld API smoke: PASS`
- `Terrain public API shape smoke` 当前为 `99` public types、`1014` members
- `ITerrainQueryService` 已补充显式 base-only 查询入口，runtime query facade 现在同时支持：
  - 当前 overlay world view 查询
  - deterministic base terrain 查询
- `TerrainWorld` 已补充 modification layer runtime/save 便捷入口：
  - `GetModificationLayerJson()`
  - `SaveModificationLayer(...)`
  - `TrySetModificationLayerFromJson(...)`
  - `TryLoadModificationLayer(...)`
  - `QueryAffectedModificationTiles()`
- terrain editor dock 已补充 modification layer 工作流入口：
  - load/save/clear modification JSON
  - affected tile summary
  - base vs overlay semantic sample diff
- `TerrainTileBuilder` 已补充显式 `BuildWithOverlay(...)` 入口，供 validation/tooling 直接生成带 overlay 的 tile artifact

这意味着“编辑器插件完全缺失”已经不再是当前状态，更准确的判断是：编辑器生产工作流已经有初版落地，但仍明显不完整。

## 2. 审核依据

本次审核基于静态代码阅读和本地验证命令。

已执行：

```powershell
dotnet build tools\TerrainValidation\TerrainValidation.csproj --configuration Release -m:1 -p:UseSharedCompilation=false
dotnet run --project tools\TerrainValidation\TerrainValidation.csproj --configuration Release --no-build -- --validation-tier pr
```

构建结果：

- Release build 成功。
- 0 errors。
- 本地出现 4 个 `NU1900` warning，原因是无法访问 `https://api.nuget.org/v3/index.json` 的漏洞数据源。这是本机网络/源可用性问题，不是代码编译错误，但会影响“本地零警告”口径。

PR 级地形验证结果：

- `Overall validation: PASS`
- 1/1 seed passed，固定 seed `613061`。
- 19/19 auxiliary checks passed。
- World size `12288`，planning grid `60 x 60`。
- Public API shape smoke：99 个 public 类型、1014 个成员通过。
- Enum contract smoke：12 个 enum、153 个值通过。
- Plan JSON roundtrip smoke 通过，JSON 约 2317.6 KB。
- Runtime `TerrainWorld` API smoke 通过。
- Runtime anchor contract smoke 通过。
- Runtime `TerrainWorld` smoke 通过。
- Open world artifact smoke 通过，导出 plan JSON、plan map、traversal cost map、report。

关键生成指标：

| 指标 | 当前结果 |
| --- | --- |
| Land ratio | 0.522 |
| Scenic ratio | 0.147 |
| Traversable land | 0.499 |
| POI count | 48 |
| Routes | 64 |
| Villages / towns / oasis hubs | 9 / 5 / 4 |
| Connected point ratio | 1.000 |
| Connected settlement ratio | 1.000 |
| POI / route world coverage | 0.983 / 0.983 |
| Average route scenic / traversability | 0.620 / 0.827 |
| Encounter potential | 0.574 |
| Route rhythm | 0.836 |
| Risk reward balance | 0.791 |

关键 runtime smoke 指标：

- Route corridor tile smoke：PASS，route corridor 对 tile 高度和颜色产生可测影响。
- Route scatter smoke：PASS，road markers 551，bridges 90。
- POI tile landmark smoke：PASS，48/48 POI materialized。
- Gameplay scatter smoke：PASS，understory/resource/hazard 总数 8527。
- Biome scatter smoke：PASS，8/8 required categories materialized。
- Scenic landmark smoke：PASS，8 类自然景观地标 materialized。
- Runtime world smoke：PASS，async plan 约 180.1 ms，cancel 约 12.8 ms，set-plan invalidation pass。

## 3. 当前已实现能力

### 3.1 参数、配置和确定性契约

核心文件：

- `dao/Scripts/Terrain/TerrainSettings.cs`
- `dao/Scripts/Terrain/TerrainWorldSettingsResource.cs`
- `dao/Scripts/Terrain/TerrainShapeSettingsResource.cs`
- `dao/Scripts/Terrain/TerrainGameplaySettingsResource.cs`
- `dao/Scripts/Terrain/TerrainStreamingSettingsResource.cs`
- `dao/Scripts/Terrain/TerrainRenderingSettingsResource.cs`
- `dao/Scripts/Terrain/TerrainProfileHash.cs`
- `dao/Scripts/Terrain/TerrainApiVersion.cs`
- `dao/Scripts/Terrain/TerrainDeterminismContract.cs`
- `dao/Scripts/Terrain/TerrainPerformanceContract.cs`

已实现：

- `TerrainSettings` 是 Godot `[GlobalClass] Resource`，可直接挂到 Godot inspector。
- 支持结构化 profile：world、shape、gameplay、streaming、rendering。
- 支持规则集 Resource：scatter、settlement visual、POI、route、scenic landmark。
- `TerrainGenerationProfile` 是不可变快照，适合后台任务和 deterministic generation。
- `TerrainProfileHash` 覆盖 28 个 profile 字段和规则集 hash。
- `TerrainApiVersion` 当前为 `terrain-api-v1` / `1.8.0`，兼容 plan API `1.0.0`、`1.1.0`、`1.2.0`、`1.3.0`、`1.4.0`、`1.5.0`、`1.6.0`、`1.7.0`、`1.8.0`。
- `TerrainDeterminismContract` 集中定义 deterministic、native parity、tile parity 阈值。
- `TerrainPerformanceContract` 定义 tile benchmark 阈值。

评价：

这部分已经达到商业工程基础要求。配置不再集中在单一巨型 settings 中，已经能在 Godot 中组织 profile 和规则集。当前不足是缺少 inspector 校验、preset library、参数依赖提示、profile diff、批量 seed 预览和一键导出工具。

### 3.2 地形场采样和玩法语义

核心文件：

- `dao/Scripts/Terrain/Generation/TerrainWorldField.cs`
- `dao/Scripts/Terrain/Generation/TerrainSampler.cs`
- `dao/Scripts/Terrain/Generation/TerrainSemanticQueryData.cs`
- `dao/Scripts/Terrain/Generation/TerrainMapExporter.cs`

已实现：

- 任意世界坐标采样完整 `TerrainWorldField`。
- 输出高度、大陆性、盆地、陆架、山脉、宽域海拔、河流、湖泊、水分、温度、风景潜力、通行性、暴露度、资源潜力、危险潜力、遭遇潜力、生物群系、地貌。
- `TerrainSampler.SampleWithSlope()` 输出高度、坡度、颜色和语义。
- `TerrainSemanticClassifier` 输出 water state、gameplay tags、traversal cost。
- `TerrainMapExporter` 可输出 biome map、generic map、traversal cost grid、tile traversal grid 和 region traversal cost samples。

评价：

地形系统已经把视觉地表和玩法语义绑定到同一确定性模型。这对开放世界很关键，因为任务、AI、资源、遭遇、音频、天气、地图和导航系统可以直接消费语义数据，而不需要从 mesh 反推。

不足：

- Gameplay tag 阈值仍硬编码在 `TerrainSemanticClassifier`。
- 生物群系、地貌和水体语义还不是完全数据驱动。
- 当前水体是程序化语义和局部水面表现，不是完整水文模拟。

### 3.3 世界规划、POI、路线和体验门禁

核心文件：

- `dao/Scripts/Terrain/Generation/TerrainWorldPlanner*.cs`
- `dao/Scripts/Terrain/Generation/TerrainQualityAnalyzer.cs`
- `dao/Scripts/Terrain/Generation/TerrainExperienceAnalyzer.cs`
- `dao/Scripts/Terrain/Generation/TerrainRouteCorridorIndex.cs`
- `dao/Scripts/Terrain/Generation/TerrainPointOfInterestIndex.cs`
- `dao/Scripts/Terrain/TerrainPointOfInterestRuleSet.cs`
- `dao/Scripts/Terrain/TerrainRouteRuleSet.cs`

已实现：

- `TerrainWorldPlanner.CreateOpenWorldPlan()` 生成 regions、POIs、routes、quality report、planning report、experience report。
- POI 类型覆盖 settlement candidate、vista、river crossing、mountain pass、coastal landing、resource grove、ancient site、canyon overlook、oasis。
- Route 类型覆盖 primary trail、river road、ridge pass、coastal path、scenic trail。
- Settlement tier 覆盖 village、town、oasis hub。
- POI 和 route rule set 已能配置阈值、评分、选择策略、路径成本和路线分类。
- 默认 open-world 门禁被 CLI contract smoke 锁定。

评价：

这已经超过普通地形系统，进入“地形驱动世界布局”的层级。它可以直接支撑探索、任务选点、聚落、路线、资源和遭遇。

不足：

- 扩展方式仍主要围绕 enum 和固定 rule slot。新增一类 POI 或 route 通常要改 enum、评分、选择、序列化、验证、可视化多处代码。
- `TerrainWorldPlanner` 已拆成 partial 和 service，但仍是大静态门面。
- 还没有输出正式 nav mesh 或角色级 AI pathfinding 数据；当前提供 route graph snapshot、center/tile traversal cost grid 和 region traversal cost samples 作为导航交接数据。

### 3.4 Tile 生成、mesh、水面、碰撞和 scatter

核心文件：

- `dao/Scripts/Terrain/Generation/TerrainTileBuilder*.cs`
- `dao/Scripts/Terrain/Generation/TerrainTileData.cs`
- `dao/Scripts/Terrain/Rendering/TerrainMeshBuilder.cs`
- `dao/Scripts/Terrain/Streaming/TerrainChunk*.cs`
- `dao/Scripts/Terrain/TerrainScatterRuleSet.cs`
- `dao/Scripts/Terrain/TerrainSettlementVisualRuleSet.cs`
- `dao/Scripts/Terrain/TerrainScenicLandmarkRuleSet.cs`

已实现：

- 按 tile coord、LOD、profile 生成完整 `TerrainTileData`。
- 生成 render vertices、normals、UV、colors、indices、skirt、collision faces。
- 生成局部 lake、river、oasis water surface。
- 根据 route corridor 修改地形高度和颜色，并生成 road marker、bridge span。
- 根据 POI footprint 修改地表并生成 settlement、vista、crossing、pass、ancient site、oasis 等地标。
- 生成自然 scatter、biome scatter、gameplay scatter、settlement interior scatter、scenic landmarks。
- `TerrainChunk` 把 tile data 转换成 Godot `ArrayMesh`、`MultiMeshInstance3D`、`ConcavePolygonShape3D`。

评价：

功能完整度很高，并且 PR smoke 已证明 route、POI、gameplay、biome、scenic landmark 都不是空 API。当前 tile 生成已经形成“规划数据影响地形表现”的闭环。

不足：

- `TerrainChunk.MeshCatalog` 使用 Godot primitive mesh 和默认 material 表现 scatter/landmark。这适合验证和原型，不是最终资产管线。
- 缺少按 biome/LOD/距离切换真实 asset 的实例化策略。
- 缺少材质层、贴图、shader、地表混合、植被风、地形 decal、物理材质等最终表现链路。
- `TerrainTileBuilder` 虽然已拆成多文件和内部 service，但仍承担 surface sampling、native backend、route deformation、POI footprint、settlement layout、water、scatter、landmark、mesh assembly 多个职责。

### 3.5 Runtime streaming world

核心文件：

- `dao/Scripts/Terrain/Streaming/TerrainWorld*.cs`
- `dao/Scripts/Terrain/Streaming/TerrainStreamingSetBuilder.cs`
- `dao/Scripts/Terrain/Streaming/TerrainTileDataCache.cs`
- `dao/Scripts/Terrain/Streaming/TerrainWorldStreamingSnapshot.cs`

已实现：

- `TerrainWorld` 是 Godot `[GlobalClass] Node3D` 主入口。
- 根据 focus node 位置构建 desired chunk set。
- 根据距离计算 LOD 和碰撞半径。
- 后台 `Task.Run` 生成 tile 和 open-world plan。
- 每帧限制完成 tile 应用数量。
- 支持 job cancel、retire、stale job drop。
- 支持 tile data cache 和 LRU。
- 支持 plan 变化后重建 route/POI index、清缓存、取消 job、重建 chunk。
- 提供 `GetStreamingSnapshot()` 诊断快照。
- 暴露 `PlanReady`、`PlanCleared`、`ChunkLoaded`、`ChunkUnloaded`、`StreamingSnapshotChanged` signals。

评价：

已经满足开放世界原型和早期商业项目的 runtime streaming 基础需求。`TerrainWorld` 当前作为 facade 是合理的，但内部仍需要进一步把 scheduler、cache、streaming policy、chunk materialization、plan lifecycle 分成更独立的可测试服务。

不足：

- LOD 主要依赖 skirt 缓解接缝，缺 geomorph 和更强的边界一致性策略。
- 没有 HLOD、远景 impostor 或 world partition 持久化。
- 没有正式跨帧 profiling 输出和 budget telemetry。
- 没有异步资源加载队列，因为目前还没有真实资产管线。

### 3.6 运行时集成 API 和 gameplay anchor

核心文件：

- `dao/Scripts/Terrain/ITerrainQueryService.cs`
- `dao/Scripts/Terrain/ITerrainPlanProvider.cs`
- `dao/Scripts/Terrain/ITerrainPlacementService.cs`
- `dao/Scripts/Terrain/ITerrainNavigationProvider.cs`
- `dao/Scripts/Terrain/ITerrainStreamingDiagnostics.cs`
- `dao/Scripts/Terrain/Runtime/TerrainWorldAnchorContract.cs`
- `dao/Scripts/Terrain/Runtime/TerrainWorldAnchorBuilder.cs`
- `TERRAIN_RUNTIME_INTEGRATION_CONTRACT.md`

已实现稳定接口：

- `ITerrainQueryService`：地形采样、surface position、water、tags、traversal。
- `ITerrainPlanProvider`：plan snapshot、POI、route、region tag、corridor 查询。
- `ITerrainPlacementService`：资源、遭遇、音频、本地互动的候选点查询。
- `ITerrainNavigationProvider`：traversal cost grid、tile/region traversal cost 查询、route graph snapshot。
- `ITerrainStreamingDiagnostics`：streaming snapshot。

已实现 anchor contract：

- POI group：`terrain_poi`
- Route group：`terrain_route`
- 统一 meta key contract。
- `TerrainWorldAnchorBuilder` 可从 plan 生成 POI 和 route anchors。

评价：

这是当前系统最接近商业级的部分。其他 gameplay 模块不应该依赖 `TerrainTileBuilder`、`TerrainWorldPlanner`、chunk/cache/job 内部类型，而应依赖上述接口。`TERRAIN_RUNTIME_INTEGRATION_CONTRACT.md` 应继续作为默认集成契约。

风险：

- Public API shape 已经有 99 类型、1014 成员。不是所有 public 类型都应该被 gameplay 模块直接依赖。
- 需要给 public API 分层：Stable Runtime API、Tooling API、Data Contract API、Internal Implementation API。后续新增 public 类型前必须先决定属于哪一层。

### 3.7 序列化、导出和工具验证

核心文件：

- `dao/Scripts/Terrain/Generation/TerrainWorldPlanSerializer*.cs`
- `dao/Scripts/Terrain/Generation/TerrainWorldPlanExporter*.cs`
- `tools/TerrainValidation/*.cs`
- `.github/workflows/dotnet.yml`
- `.github/workflows/gdextension.yml`

已实现：

- Plan JSON schema，包含 contract、api version、generator version、seed、profile hash、rule set hash、regions、POIs、routes、reports。
- JSON roundtrip、file roundtrip、seed/profile/version/enum drift rejection。
- Open world plan PNG、traversal cost PNG、text report 导出。
- PR / nightly / release validation tiers。
- Native sampler parity 和 tile benchmark 流程。
- Public API shape、enum contract、profile hash、threshold、default state、runtime API、anchor contract smoke。

评价：

验证体系非常强，已经接近商业工程习惯。它显著降低了单人维护风险。

不足：

- `tools/TerrainValidation/Program.cs` 体量过大，仍有大量 smoke 逻辑集中在主程序。
- Runtime probe 通过反射设置私有字段，这能覆盖内部行为，但会让重构成本变高。
- 需要把 validation 拆成更小的 test suite 或 helper modules，并增加 Godot headless 场景级测试。

### 3.8 GDExtension native sampler

核心文件：

- `gdextension/src/dao_extension.cpp`
- `dao/Scripts/Terrain/Generation/NativeTerrainBridge.cs`
- `.github/workflows/gdextension.yml`

已实现：

- Native height grid sampler。
- Native field grid sampler v1/v2。
- C# fallback 和 adaptive sampler selection。
- Native/managed parity checks。
- Linux x86_64 CI 构建和 native validation。

评价：

Native acceleration 已有实际工程基础，不是空壳。

风险：

- Native C++ 和 C# terrain field 逻辑存在双实现同步成本。
- 当前 CI 重点是 Linux，尚未覆盖 Windows/macOS export target 的 native build matrix。
- `TerrainPerformanceContract.TileBenchmarkHardwareBaseline` 仍是 `dev-linux-x64-provisional`，不是最终目标平台基线。

## 4. 对用户关注点的直接回答

### 4.1 有没有实现完整功能

如果“完整功能”指可运行的开放世界程序化地形基础设施，答案是基本实现。系统可以生成地形、规划世界内容、流送 tile、产生碰撞、水面、scatter、POI、路线、地标、语义查询、运行时 anchor、导出地图和报告，并有自动验证。

如果“完整功能”指最终 3A 商业游戏可直接生产上百小时内容的地形管线，答案是还没有。缺口集中在编辑器、资产、导航、动态修改、存档、性能基线、最终视觉材质和生产工作流。

### 4.2 能不能和其他游戏模块轻松集成

可以。当前集成方式是合理的：

- 任务系统可以通过 `ITerrainPlanProvider` 获取 POI、routes、regions、anchors。
- AI 和导航系统可以通过 `ITerrainNavigationProvider` 获取 route graph、center/tile traversal cost grid 和 region traversal cost samples。
- 资源、遭遇、音频可以通过 `ITerrainPlacementService` 查询符合 gameplay tags 的候选点。
- UI 和地图系统可以用 plan snapshot、route summary、POI summary、exporter。
- 调试和流送监控可以用 `ITerrainStreamingDiagnostics`。
- 需要 Godot scene 节点的系统可以监听 `TerrainWorld` signals。

建议所有 gameplay 模块默认依赖接口，不直接依赖 `TerrainWorld` concrete type，除非确实需要 Node lifecycle 或 signals。

### 4.3 接口是否丰富，API 是否充分暴露

接口已经丰富，甚至需要开始收敛。当前 public API 覆盖非常广，PR smoke 锁定了 99 个 public 类型和 1014 个成员。对外能力足够，但必须避免把内部实现继续暴露成永久 contract。

建议：

- `TERRAIN_RUNTIME_INTEGRATION_CONTRACT.md` 中列出的五个接口作为稳定 gameplay API。
- Serializer/exporter/analyzer 作为 tooling API。
- Plan、summary、snapshot、enum、record struct 作为 data contract API。
- Builder、planner service、chunk、cache、native bridge 细节尽量不要作为 gameplay 依赖。

### 4.4 担负的责任是否合理

总体合理。地形系统目前负责：

- 地形场和语义。
- 世界规划数据。
- Tile mesh、水面、碰撞、scatter、地标。
- 地形驱动的 placement candidate。
- 导航和 AI 的成本图/route graph 交接。
- 导出、验证和可视化辅助。

它没有直接负责：

- 任务脚本。
- AI 决策。
- 资源实际生成。
- 角色 pathfinding。
- 战斗遭遇逻辑。
- 存档系统。

这个边界是正确的。后续应保持“地形给数据和候选，不拥有 gameplay 行为”。

### 4.5 拓展性、维护性、解耦性如何

结论：可继续开发，但需要有计划地控制复杂度。

已具备的维护优势：

- Deterministic profile hash。
- API version。
- JSON contract。
- CI validation tiers。
- Runtime interface facade。
- Plan snapshot isolation。
- RuleSet Resource。
- Native parity smoke。

当前主要维护风险：

- Public API 面过宽。
- Planner、tile builder、validation、native bridge 虽已拆文件但仍是大逻辑中心。
- 新增 enum 型内容需要多处同步。
- C# 和 C++ sampler 双实现增加漂移风险。
- 编辑器工具缺失会导致调参靠代码和 CLI，长期效率低。

### 4.6 能否直接在 Godot 中配置参数

可以配置核心参数和大量规则。`TerrainSettings`、structured profile Resource、scatter/POI/route/settlement/scenic rule set 都是 `[GlobalClass] Resource`，可以在 Godot inspector 中配置。

但这还不是完整编辑器工作流。缺少：

- 地形 profile preset 库。
- Inspector 参数校验和警告。
- Seed 批量预览。
- 地图/规划/route/POI overlay 的 editor dock。
- 一键导出 plan JSON、PNG、report。
- 运行 PR/nightly/release validation 的 Godot 内按钮。
- 对规则变更的 profile hash diff 和影响报告。

### 4.7 如果需要单独开发编辑器，当前能不能支持

能支持。现有基础很好：

- `TerrainWorldPlanExporter` 已可统一导出 plan JSON、plan map、traversal cost map 和 report。
- `TerrainMapExporter` 可生成 raster 和 traversal cost map。
- `TerrainWorldPlanOverlay` 可显示 plan。
- `TerrainWorldAnchorBuilder` 可生成 anchors。
- `TerrainWorldPlanSerializer` 可保存/加载 JSON。
- `TerrainValidation` 可作为编辑器按钮背后的 CLI。

当前已经有初版 `TerrainEditorPlugin` / editor dock 脚手架、默认 `TerrainSettings` 资源工作流、plan preview、route graph path preview、语义采样、artifact 导出和 PR validation 触发入口。缺的是把这些能力继续打磨成完整生产工作流，而不是再从零开始搭插件。

## 5. 当前不满足商业级要求的具体问题

### P0：没有最终资产实例化管线

当前 `TerrainChunk.MeshCatalog` 使用 primitive mesh 表现树、石头、聚落、道路标记、桥、自然地标。这对验证非常有价值，但不能作为最终视觉生产管线。

需要实现：

- Scatter/landmark visual catalog Resource。
- 每个 `TerrainScatterKind` 和 `TerrainLandmarkKind` 映射到真实 `PackedScene`、`Mesh` 或 `MultiMesh` asset。
- LOD asset、impostor、远景替代策略。
- 资产异步加载和缓存。
- 按 biome/region/seed 的变体选择。
- 碰撞、导航阻挡、交互 metadata 绑定。

### P0：Godot 编辑器插件和生产工作流仍不完整

虽然可配置 Resource 已经存在，并且当前已新增初版 `TerrainEditorPlugin` / editor dock、默认 `TerrainSettings` 资源、plan preview、artifact export、语义采样、route graph path preview 和 PR validation 触发，但这还不是完整生产工作流。对单人开发者来说，后续仍会直接影响长期效率。

需要实现：

- Terrain editor dock。
- Profile preset 管理。
- Seed sweep preview。
- Plan preview、POI/route/filter overlay。
- 参数校验和一键修复建议。
- 更完整的统一 artifact 浏览和导出 UX。
- 运行 validation tier。
- 选择 tile/region 查看语义字段。

### P0：导航交接还不是完整导航系统

当前已提供 route graph snapshot、center/tile traversal cost grid、region traversal cost samples 和 grid 局部查询 helper，这是正确边界。但对 3D 开放世界游戏，还需要接入 Godot Navigation 或自研导航层。

需要实现：

- Terrain route graph 到 AI waypoint/nav graph 的 importer。
- Traversal cost grid / region samples 到局部 pathfinding 或 nav mesh 权重的转换。
- Tile 流送时导航区域更新策略。
- 水、坡度、危险、道路对导航成本的统一配置。
- 验证 AI 查询不会依赖未加载 tile。

### P1：动态地形和存档 delta 缺失

当前系统是确定性静态生成。若游戏需要建造、破坏、道路修正、采集后状态、任务永久影响，就需要 delta layer。

需要实现：

- Terrain modification data model。
- Tile delta patch 应用。
- 保存/加载 delta。
- Delta 对 mesh、collision、scatter、navigation、queries 的影响。
- Deterministic base + mutable overlay 的版本策略。

### P1：正式性能基线未完成

当前有 benchmark contract，但 baseline 仍是 `dev-linux-x64-provisional`。这说明性能门禁还没有绑定最终目标硬件。

需要实现：

- Windows、Linux、目标发布平台的 benchmark matrix。
- Managed/native tile p50/p95/p99 基线。
- Streaming frame budget telemetry。
- GC allocation budget。
- Tile apply main-thread cost。
- Asset streaming cost。
- CI artifact 保存 benchmark trend。

### P1：LOD 和远景策略还不完整

当前有 LOD 和 skirt，但没有完整的生产级地形过渡。

需要实现：

- Chunk 边界一致性策略。
- Geomorph 或 cross-fade。
- HLOD/远景地形 mesh。
- 远景水体和远景地标。
- 大尺度地图下的 streaming horizon。

### P1：Public API 需要分层治理

当前 public surface 已经很大。对商业级长期维护来说，API 过宽会锁死内部重构。

需要实现：

- API 分层文档。
- Stable gameplay API 白名单。
- Tooling API 白名单。
- Data contract API 白名单。
- Implementation API 限制新增 public。
- API deprecation policy。

### P2：Validation 工具需要拆分

当前验证能力强，但 `Program.cs` 过大，且 probe helper 反射私有字段。

需要实现：

- 把 smoke checks 迁出主程序。
- 引入更明确的 test fixture。
- 为 runtime service 提供测试 seam，减少反射私有字段。
- 增加 Godot headless scene smoke。

### P2：Native/C# 双实现同步风险

Native sampler 是性能基础，但 C++ 和 C# terrain logic 双实现会带来漂移。

需要实现：

- Native parity release gate 覆盖更多 seeds/profile。
- 生成共享测试样本。
- C++/C# 参数结构版本化。
- 如果算法继续复杂化，评估代码生成或单源算法描述。

## 6. 详细开发计划

### 阶段 1：锁定稳定 API 和生产集成边界

目标：防止 gameplay 模块依赖内部实现，保护后续重构空间。

任务：

- 更新 `TERRAIN_RUNTIME_INTEGRATION_CONTRACT.md`，明确 Stable Runtime API、Tooling API、Data Contract API、Internal API。
- 给新增 public 类型制定规则：没有进入 contract 文档的 public 类型不允许被 gameplay 直接依赖。
- 给 `TerrainWorld` facade 增加最小示例：任务系统、资源系统、AI、地图 UI 分别依赖哪个接口。
- 增加 validation：稳定接口方法签名白名单和禁止 gameplay 依赖 builder/planner/chunk 的静态检查。
- 标记未来可能收敛的 public 类型，制定 deprecation policy。

验收标准：

- 所有 gameplay-facing 接口都有文档和 smoke。
- 新增 API 能明确归属层级。
- 没有新模块直接依赖 `TerrainTileBuilder`、`TerrainWorldPlanner`、`TerrainChunk` 内部。

### 阶段 2：开发 Godot Terrain Editor Plugin

目标：让单人开发者可以在 Godot 内完成调参、预览、导出和验证。

任务：

- 在现有 `TerrainEditorPlugin` / editor dock 基础上继续补完整生产工作流。
- 支持选择或创建 `TerrainSettings` 和规则集 Resource。
- 显示 profile hash、API version、rule set hash。
- 支持 seed preview：生成 plan map、traversal map、POI/route summary。
- 支持 region inspector：点击地图采样 field、tags、water、traversal。
- 支持一键导出 JSON、PNG、report。
- 支持一键运行 `TerrainValidation` 的 `pr` 或 custom seed smoke。
- 提供 profile preset 保存和加载。

验收标准：

- 不运行游戏也能预览一个 world plan。
- 参数修改后能看到 hash 和地图变化。
- 编辑器内能导出 artifact。
- 编辑器内能运行 PR 级核心验证或至少运行 seed smoke。

### 阶段 3：资产实例化管线

目标：把 primitive 验证表现替换成可扩展的真实游戏资产管线。

任务：

- 新增 `TerrainVisualCatalog` Resource。
- 为 scatter/landmark 定义 visual entry：kind、asset、LOD、density multiplier、collision policy、nav obstacle policy、interaction tag。
- 支持 `PackedScene`、`Mesh`、`MultiMesh` 三种实例化路径。
- `TerrainChunk` 从 catalog 创建 visuals，不再硬编码 primitive mesh。
- 增加异步资源加载和缓存。
- 增加 fallback primitive，仅用于缺失资产或验证模式。
- 增加 validation：所有 enum kind 有 visual entry 或明确 fallback。

验收标准：

- 不改 C# 代码即可替换树、石头、道路标记、桥、聚落、自然地标资产。
- Tile streaming 不同步加载大型资源。
- 缺失资产会警告但不崩溃。
- PR smoke 仍可在 fallback 模式下运行。

### 阶段 4：导航和 AI 交接

目标：把地形语义转换成 AI 和导航系统可以长期消费的数据。

任务：

- 已完成：为 traversal cost grid 增加 tile/region 局部查询，并纳入 PR runtime smoke。
- 为 `TerrainRouteGraphSnapshot` 增加 importer 示例。
- 输出 route graph node/edge 到 Godot navigation 或自研 graph。
- 设计 streaming tile 加载/卸载时的 navigation update policy。
- 定义水体、坡度、危险、道路、聚落对导航成本的配置。
- 继续增加 validation：route graph importer、navigation update policy、route-connected settlement navigation。

验收标准：

- AI 可以从一个 POI 找到另一个 POI 的高层路线。
- 局部导航能避开 ocean/deep lake/极端坡度。
- 导航查询不要求目标 tile 已经渲染加载。

### 阶段 5：动态地形和持久化 overlay

目标：支持玩家或任务对世界产生长期影响，同时保持 deterministic base generation。

任务：

- 定义 `TerrainModificationLayer`。
- 支持 height delta、surface override、scatter removal/addition、landmark state、route unlock/block。
- Tile builder 应用 base terrain + modification overlay。
- Serializer 保存 modification delta，不保存完整 generated mesh。
- Query API 已具备显式 base-only 与 overlay-aware 双路径。
- Runtime/save/editor 已具备 modification layer JSON 载入、保存、应用与受影响 tile 查询入口。
- Tile feature materialization 已开始消费 modification delta，当前至少覆盖：
  - scatter removal/addition
  - landmark remove/state tagging
- Navigation、collision、scatter 随 delta invalidation 更新。

验收标准：

- 修改一个 tile 后只重建受影响 tile。
- 存档只保存 delta。
- 重新加载后地形、scatter、collision、query 结果一致。

### 阶段 6：正式性能和平台基线

目标：把性能从 provisional 变成可追踪的发布门禁。

任务：

- 定义目标硬件 baseline：开发机、最低配置 PC、目标平台。
- 扩展 CI 或本地 benchmark 输出 JSON。
- 记录 managed/native tile build p50/p95/p99。
- 记录 main-thread tile apply cost。
- 记录 streaming queue latency。
- 记录 GC allocation per tile。
- 记录 asset catalog 加载和实例化成本。
- 把 `TerrainPerformanceContract.TileBenchmarkHardwareBaseline` 从 provisional 改为正式 label。

验收标准：

- Release validation 能输出并保存 benchmark artifact。
- 性能回退超过阈值时 CI fail。
- Native sampler 是否启用由目标平台数据决定，而不是只靠临时测量。

### 阶段 7：LOD、HLOD 和远景

目标：支撑大型开放世界视距和稳定帧率。

任务：

- 设计 chunk border matching 或 geomorph。
- 增加远景 low-poly terrain mesh。
- 增加 water horizon 表现。
- 为 landmarks 增加远景 impostor 或 simplified mesh。
- 让 streaming snapshot 输出 LOD 分布和远景状态。

验收标准：

- 移动 focus 时无明显裂缝和突变。
- 大视距下 tile 数、draw calls、instance 数可控。
- 远景与近景语义一致。

## 7. 建议优先级

近期不要优先继续增加更多 biome 或 POI 类型。当前更重要的是把系统变成可以长期生产内容的工具链。

推荐顺序：

1. API 分层和 contract 治理。
2. Godot editor plugin。
3. Visual asset catalog。
4. Navigation handoff。
5. 正式性能 baseline。
6. Dynamic modification overlay。
7. HLOD 和远景。

原因：

- API 分层防止现在的 public surface 继续失控。
- 编辑器工具会立刻提升单人调参和内容生产效率。
- 资产 catalog 是从验证表现走向最终画面的必要步骤。
- 导航是开放世界 gameplay 集成的硬依赖。
- 性能 baseline 必须在资产管线接入后重新确认。
- 动态地形和 HLOD 属于中后期系统，但需要提前设计接口。

## 8. 当前文件清理结果

本次审核将旧阶段文件替换为当前文件：

- 删除：`TERRAIN_SYSTEM_PHASE_AUDIT_2026_06_05.md`
- 新增：`TERRAIN_SYSTEM_PHASE_AUDIT_2026_06_06.md`

保留：

- `TERRAIN_RUNTIME_INTEGRATION_CONTRACT.md`

保留原因：该文件是 2026-06-06 更新的运行时集成契约，内容与当前五个稳定 runtime interface 一致，仍然有效。
