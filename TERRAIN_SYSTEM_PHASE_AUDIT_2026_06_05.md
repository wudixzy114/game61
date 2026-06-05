# 程序化地形生成系统阶段性审核与开发计划

审核日期：2026-06-05  
项目路径：`d:\game61`  
审核对象：`dao/Scripts/Terrain`、`tools/TerrainValidation`、`gdextension/src`、CI 工作流  
目标标准：面向单人长期维护的商业级 3D 开放世界地形基础设施

## 1. 总结论

当前地形系统已经达到“可运行、可验证、可集成的开放世界地形基础设施原型”水平，不是简单高度图 Demo。它已经具备确定性地形场采样、tile 生成、LOD 流送、开放世界规划、POI/路线/聚落实体化、语义查询、运行时 gameplay anchor、地图/JSON/report 导出、Native sampler 桥接和 CLI 合约验证。

但它还没有达到完整 3A 商业生产级地形系统。主要原因不是功能空白在核心生成层，而是长期生产所需的编辑器工作流、资产管线、导航数据、世界持久化、动态修改、正式性能基线和模块拆分还不足。以一个人长期维护为前提，当前最大风险是 `TerrainTileBuilder`、`TerrainWorldPlanner`、`TerrainWorld`、`TerrainValidation` 这些核心文件已经过大，算法、策略、数据契约和验证逻辑混在同一层，继续加功能会明显提高维护成本。

阶段性评级：

| 维度           | 当前等级 | 结论                                                                                                                   |
| -------------- | -------- | ---------------------------------------------------------------------------------------------------------------------- |
| 功能完整度     | B+       | 作为地形基础设施原型很完整；作为最终 3A 地形生产系统仍缺编辑器、资产、导航、持久化和动态世界层。                       |
| 模块集成能力   | A-       | `TerrainWorld` facade、纯函数 sampler、plan snapshot、anchor group/meta、JSON 和 map exporter 已经能支撑其他模块接入。 |
| API 暴露       | A-       | 暴露面丰富且有 shape smoke 保护，但 public 类型数量已达 63 个，未来要避免把内部实现永久冻结成公共契约。                |
| 职责划分       | B        | 大方向合理，地形系统负责世界地表和地形语义；但 settings、tile builder、planner、streaming world 内部职责仍偏重。       |
| 扩展性         | B        | 能继续添加 biome、POI、路线、scatter、语义层；但目前主要靠大 switch 和硬编码评分扩展，长期应数据驱动。                 |
| 维护性         | B-       | 构建和验证非常好，但大文件和验证工具反射私有字段会拖慢长期迭代。                                                       |
| 解耦性         | B+       | 对外已经有 facade 和 anchor 契约；内部 generation/rendering/runtime/validation 分层仍可继续拆。                        |
| 商业级代码质量 | B        | 质量门禁、确定性契约、API 版本和 CI 说明已经接近商业工程习惯；生产级还需要性能基线、工具链和模块化补强。               |

一句话判断：

当前系统可以作为开放世界玩法、任务、AI、资源、探索、地图工具的上游基础层开始集成；但在继续扩大内容前，应先进行 API 分层和核心大文件拆分，否则单人长期维护风险会快速上升。

## 2. 审核依据

本次审核基于静态代码阅读和本地验证命令。

已执行：

```powershell
dotnet build tools\TerrainValidation\TerrainValidation.csproj --configuration Release -m:1 -p:UseSharedCompilation=false
dotnet run --project tools\TerrainValidation\TerrainValidation.csproj --configuration Release --no-build -- --validation-tier pr
```

验证结果：

- Release build 成功。
- 0 warnings。
- 0 errors。
- PR 级地形验证通过。
- 固定 seed：`613061`。
- 17/17 个辅助检查通过。
- 公共 API shape smoke：63 个导出类型、560 个成员通过。
- enum contract smoke：12 个 enum、153 个值通过。
- plan JSON roundtrip smoke 通过，JSON 约 2317 KB。
- Runtime `TerrainWorld` API smoke 通过。
- Anchor contract smoke 通过。
- Runtime `TerrainWorld` smoke 通过。
- Artifact smoke 通过，导出 open world plan、traversal cost map 和 report。

关键 PR 验证指标：

| 指标                                | 结果        |
| ----------------------------------- | ----------- |
| Land ratio                          | 0.522       |
| Scenic ratio                        | 0.147       |
| Traversable land                    | 0.499       |
| POI                                 | 48          |
| Routes                              | 64          |
| Villages/Towns/Oasis hubs           | 9/5/4       |
| Connected point ratio               | 1.000       |
| Connected settlement ratio          | 1.000       |
| POI/route world coverage            | 0.983/0.983 |
| Average route scenic/traversability | 0.620/0.827 |
| Route rhythm                        | 0.836       |
| Risk reward balance                 | 0.791       |

## 3. 当前已实现能力

### 3.1 地形参数和确定性配置

核心文件：

- `dao/Scripts/Terrain/TerrainSettings.cs`
- `dao/Scripts/Terrain/TerrainProfileHash.cs`
- `dao/Scripts/Terrain/TerrainApiVersion.cs`
- `dao/Scripts/Terrain/TerrainDeterminismContract.cs`
- `dao/Scripts/Terrain/TerrainPerformanceContract.cs`

已实现：

- `TerrainSettings` 是 Godot `Resource`，可在编辑器中配置 seed、chunk、分辨率、流送半径、LOD、海平面、山脉、水系、碰撞、cache 和 Native sampler。
- `TerrainGenerationProfile` 是不可变快照，适合传递给后台任务，避免运行时配置半更新问题。
- `StableHash()` 覆盖 23 个 profile 字段，并通过 hash smoke 验证。
- `TerrainApiVersion` 当前为 `terrain-api-v1` / `1.2.0`。
- `TerrainDeterminismContract` 集中定义跨实现、Native parity、tile parity 的 epsilon。
- `TerrainPerformanceContract` 集中定义 tile benchmark 阈值，但硬件基线仍是 `dev-linux-x64-provisional`，还不是正式目标平台基线。

评价：

这部分已经具备商业工程意识。后续重点不是增加更多字段，而是把 generation、streaming、planning、rendering/performance 配置拆成多个 profile，避免 `TerrainSettings` 继续膨胀。

### 3.2 地形场采样和语义数据

核心文件：

- `dao/Scripts/Terrain/Generation/TerrainWorldField.cs`
- `dao/Scripts/Terrain/Generation/TerrainSampler.cs`
- `dao/Scripts/Terrain/Generation/TerrainSemanticQueryData.cs`
- `dao/Scripts/Terrain/Generation/ProceduralNoise.cs`

已实现：

- 任意世界 XZ 坐标采样完整 `TerrainWorldField`。
- 输出高度、大陆性、盆地、陆架、山脉、宽域海拔、河流、湖泊、水分、温度、风景潜力、通行性、暴露度、资源潜力、危险潜力、遭遇潜力、生物群系和地貌。
- `TerrainSampler.SampleWithSlope()` 提供高度、坡度、表面颜色。
- `TerrainSemanticClassifier` 提供水体、gameplay tags、traversal cost 分类。
- `TerrainMapExporter` 可导出 traversal cost raster 和机器可读 grid。

评价：

地形系统已经把视觉地表和玩法语义绑定到同一套确定性模型，这对开放世界模块非常重要。任务、AI、资源、遭遇、音频、天气和导航系统都可以直接消费语义场，不需要从 mesh 反推信息。

不足：

- 语义阈值仍大量硬编码。
- 生物群系和地貌规则不是数据驱动。
- 水系更接近程序化语义和视觉水面，不是完整水文模拟。
- 暂无地形修改、玩家建造、破坏、存档 delta 的长期模型。

### 3.3 Tile 生成、mesh、水面、碰撞和散布

核心文件：

- `dao/Scripts/Terrain/Generation/TerrainTileBuilder.cs`
- `dao/Scripts/Terrain/Generation/TerrainTileBuilder.RouteScatter.cs`
- `dao/Scripts/Terrain/Generation/TerrainTileBuilder.Settlements.cs`
- `dao/Scripts/Terrain/Generation/TerrainTileBuilder.SurfaceScatter.cs`
- `dao/Scripts/Terrain/Generation/TerrainTileBuilder.ScenicLandmarks.cs`
- `dao/Scripts/Terrain/Generation/TerrainTileData.cs`
- `dao/Scripts/Terrain/Rendering/TerrainMeshBuilder.cs`
- `dao/Scripts/Terrain/Streaming/TerrainChunk.cs`

已实现：

- 按 tile coord、LOD、profile 生成完整 `TerrainTileData`。
- 生成顶点、法线、UV、顶点色、索引、skirt、碰撞三角面。
- 生成局部水面，覆盖 lake、river、oasis。
- 根据 route corridor 修改高度和颜色，并生成道路标记、桥段。
- 根据 POI footprint 修改地表并生成 settlement、vista、crossing、pass、ancient site、oasis 等地标。
- 生成生态散布、资源点、危险 outcrop 和自然景观地标。
- `TerrainChunk` 将数据转换成 Godot `ArrayMesh`、`MultiMeshInstance3D` 和 `ConcavePolygonShape3D`。

评价：

这部分已经形成“规划数据影响地形表现”的闭环，价值很高。PR smoke 中 route scatter、POI landmark、gameplay scatter、biome scatter、scenic landmark 都通过，说明实现不是空 API。

高风险：

- `TerrainTileBuilder.cs` 约 63 KB，`TerrainTileBuilder.Settlements.cs` 约 43 KB，`TerrainTileBuilder.ScenicLandmarks.cs` 约 27 KB。复杂度已经高于长期单人维护的舒适区。
- Tile 构建同时负责 surface sampling、route deformation、POI footprint、settlement layout、water surface、scatter、landmark、Native backend 选择、array pooling、parallel policy。
- 扩展新 biome、新资产、新 POI 时容易继续往大文件追加规则。
- 当前 scatter 是 placeholder primitive 表现，尚未接入最终资产/材质/实例化策略。

结论：

功能阶段性完成度高，但下一阶段必须拆出可测试的小模块，否则继续加内容会降低商业级维护性。

### 3.4 Runtime streaming world

核心文件：

- `dao/Scripts/Terrain/Streaming/TerrainWorld.cs`
- `dao/Scripts/Terrain/Streaming/TerrainWorldStreamingSnapshot.cs`
- `dao/Scripts/Terrain/Streaming/TerrainChunk.cs`

已实现：

- `TerrainWorld` 是 Godot `Node3D` 主入口。
- 根据 focus 节点位置构建 desired chunk set。
- 根据 Chebyshev 距离计算 LOD 和碰撞半径。
- 后台 `Task.Run` 生成 tile。
- 每帧限制完成 tile 应用数量。
- 支持 job cancel、retire 和 stale job drop。
- 支持 tile cache 和 LRU。
- 支持 plan 变化后重建 route/POI index、清缓存、取消 job、重建 chunk。
- 提供 `GetStreamingSnapshot()` 诊断快照。

评价：

已经满足一个开放世界原型的 runtime streaming 基础需求，并且 API smoke 验证了快照隔离、计划状态、cache/job 上限等。

不足：

- `TerrainWorld.cs` 约 40 KB，同时负责 Godot 生命周期、plan job、tile job、cache、streaming policy、water plane、runtime facade。
- 没有正式的 streaming service/job scheduler/cache 子模块。
- 没有跨帧预算的更细粒度 profiler 输出。
- LOD 目前依赖 skirt 缓解接缝，尚未实现 geomorph、chunk 边界一致性策略或 HLOD。
- 没有世界分区存档和异步资源加载队列。

### 3.5 开放世界规划、POI、路线和体验门禁

核心文件：

- `dao/Scripts/Terrain/Generation/TerrainWorldPlanner.cs`
- `dao/Scripts/Terrain/Generation/TerrainQualityAnalyzer.cs`
- `dao/Scripts/Terrain/Generation/TerrainExperienceAnalyzer.cs`
- `dao/Scripts/Terrain/Generation/TerrainRouteCorridorIndex.cs`
- `dao/Scripts/Terrain/Generation/TerrainPointOfInterestIndex.cs`

已实现：

- `TerrainWorldPlanner.CreateOpenWorldPlan()` 按地形场生成 region、POI、route、quality report、planning report、experience report。
- POI 类型包括 settlement candidate、vista、river crossing、mountain pass、coastal landing、resource grove、ancient site、canyon overlook、oasis。
- route 类型包括 primary trail、river road、ridge pass、coastal path、scenic trail。
- settlement tier 包括 village、town、oasis hub。
- 默认 planning、quality、experience threshold 已被 CLI contract smoke 锁定。
- PR 验证固定 seed 生成 48 POI、64 routes，连通率和覆盖率均通过。

评价：

这已经超过普通地形系统范围，进入“地形驱动世界布局”的层级。它能直接服务任务、探索、聚落、路线、资源和遭遇系统。

不足：

- `TerrainWorldPlanner.cs` 约 69 KB，是当前最大维护风险之一。
- POI 评分、路线候选、覆盖率、settlement tier、route kind 选择大量硬编码。
- `maxRoutes` 默认值和 open-world 默认值存在调用层策略差异，需要继续明确哪些是小图默认，哪些是开放世界默认。
- 规划器目前没有分层输出 nav graph、region graph、resource layer 或 quest hooks。
- 缺少编辑器可视化调参工作流。

### 3.6 Runtime gameplay anchor 和集成契约

核心文件：

- `dao/Scripts/Terrain/Runtime/TerrainWorldAnchorContract.cs`
- `dao/Scripts/Terrain/Runtime/TerrainWorldAnchorBuilder.cs`
- `dao/Scripts/Terrain/Runtime/TerrainWorldPointOfInterestAnchor.cs`
- `dao/Scripts/Terrain/Runtime/TerrainWorldRouteAnchor.cs`
- `dao/Scripts/Terrain/Runtime/TerrainWorldPlanOverlay.cs`
- `dao/Scripts/Terrain/Runtime/TerrainPointOfInterestArchetypeCatalog.cs`

已实现：

- POI anchor group：`terrain_poi`。
- Route anchor group：`terrain_route`。
- POI meta 覆盖 id、kind、visual、gameplay tag、score、scenic、traversability、settlement tier、landscape、interaction radius、encounter budget。
- Route meta 覆盖 kind、from、to、cost、scenic、traversability。
- Anchor descriptor 可以不依赖 debug overlay 直接生成。
- Anchor contract smoke 已验证 group/meta/constants/snapshot isolation。

评价：

这是当前系统最适合被其他 gameplay 模块使用的集成面。任务、AI、资源、遭遇、UI、地图和音频模块可以扫描 group/meta，而不必绑定到 terrain planner 内部结构。

建议：

- 继续把 anchor contract 视为一级稳定 API。
- 不要让任务、AI 或资源系统直接依赖 `TerrainTileBuilder` 内部散布逻辑。
- 增加可选 Godot signal，例如 plan ready、streaming snapshot changed、chunk loaded/unloaded，降低轮询成本。

### 3.7 导出、持久化和验证

核心文件：

- `dao/Scripts/Terrain/Generation/TerrainMapExporter.cs`
- `dao/Scripts/Terrain/Generation/TerrainWorldPlanExporter.cs`
- `dao/Scripts/Terrain/Generation/TerrainWorldPlanSerializer.cs`
- `tools/TerrainValidation/Program.cs`
- `.github/workflows/dotnet.yml`
- `.github/workflows/gdextension.yml`

已实现：

- 地图导出支持 biome、高度、河流、水分、温度、风景、通行性、暴露度、资源、危险、遭遇、route influence、traversal cost。
- Plan JSON 使用 `terrain-plan-v1`，包含 API contract/version、generator version、seed、profile hash。
- JSON roundtrip 验证覆盖 string/file、版本兼容、seed/hash mismatch、enum name/value drift、snapshot isolation。
- CLI 验证覆盖 terrain quality、planning、experience、archetype、route corridor、route scatter、POI tile、gameplay scatter、biome scatter、scenic landmark、artifact、JSON、enum、public API shape、profile hash、CLI tier、threshold、default state、runtime API、anchor、runtime world、native parity、benchmark。
- CI 包含 .NET PR/nightly 验证和 GDExtension native 验证。

评价：

验证体系是当前项目最强的工程资产之一。它已经不是“靠肉眼看地形”的开发方式，而是有可重复 gate。

高风险：

- `tools/TerrainValidation/Program.cs` 已经约 8700 行。
- 验证工具通过 `RuntimeHelpers.GetUninitializedObject` 和反射访问 `TerrainWorld` 私有字段来构造 probe，这对内部重构很敏感。
- 目前验证工具是 console monolith，不利于按模块定位失败。
- 文档此前存在编码损坏文件，本次已删除旧 `TERRAIN_API_CONTRACT.md`，以本文件作为新的阶段性结论和计划来源。

## 4. 对用户关键问题的直接回答

### 4.1 有没有实现完整功能

回答：对“地形基础设施原型”来说，功能相当完整；对“最终 3A 商业地形系统”来说，还不完整。

已完整或接近完整：

- 确定性地形生成。
- 地形语义采样。
- Tile mesh/collision/water/scatter/landmark 生成。
- Runtime chunk streaming。
- 开放世界 plan 生成。
- POI、路线、聚落、route corridor、POI footprint。
- Runtime 查询 facade。
- Gameplay anchor。
- Map/report/JSON artifact。
- CLI 和 CI 验证。
- Native sampler fallback 架构。

尚不完整：

- 最终美术资产和材质管线。
- 数据驱动 biome/POI/scatter/landmark 规则。
- 正式导航数据生成，包含 navmesh、navigation graph、AI cost map handoff。
- 世界持久化、玩家改造、破坏、建造、delta save。
- 编辑器可视化调参和审核工具。
- 目标平台性能基线和内存预算。
- LOD morph、HLOD、远景地形、streaming profiler。
- 大规模 QA seed farm 和 artifact regression diff。

### 4.2 能不能和其他模块轻松集成

回答：可以开始集成，但建议其他模块只依赖一级 facade 和 anchor contract，不要依赖 tile builder/planner 内部实现。

推荐集成入口：

- `TerrainWorld.Profile`
- `TerrainWorld.SampleField(...)`
- `TerrainWorld.SampleSurface(...)`
- `TerrainWorld.SurfacePositionAt(...)`
- `TerrainWorld.TryGetWorldPlan(...)`
- `TerrainWorld.GetWorldPlanSnapshot()`
- `TerrainWorld.GetPointsOfInterest()`
- `TerrainWorld.GetRoutes()`
- `TerrainWorld.QueryPointsOfInterest(...)`
- `TerrainWorld.QueryRoutesNear(...)`
- `TerrainWorld.SampleRouteCorridor(...)`
- `TerrainWorld.SampleWaterState(...)`
- `TerrainWorld.SampleGameplayTags(...)`
- `TerrainWorld.SampleTraversalCost(...)`
- `TerrainWorld.GetStreamingSnapshot()`
- `TerrainWorldAnchorContract`
- `TerrainWorldPlanSerializer`
- `TerrainMapExporter.CreateTraversalCostGrid(...)`

建议新增：

- `ITerrainQueryService` 或等价接口，让任务、AI、资源、音频、UI 模块依赖接口而不是 Godot 节点类型。
- `ITerrainPlanProvider`，只暴露 plan snapshot、POI query、route query。
- `ITerrainStreamingDiagnostics`，只暴露 streaming snapshot。
- Godot signal：`PlanReady`、`PlanCleared`、`ChunkLoaded`、`ChunkUnloaded`、`StreamingSnapshotChanged`。

### 4.3 接口丰不丰富

回答：接口已经很丰富，甚至已经接近“过宽”。当前 public API shape smoke 锁定 63 个导出类型和 560 个成员。

优点：

- Facade、sampler、serializer、exporter、anchor contract 都存在。
- 数据载体大多是不可变 record struct 或复制数组。
- API version、enum contract、profile hash、threshold contract 都有验证。

风险：

- public 类型过多会冻结实现细节。
- `TerrainWorldPlan`、`TerrainTileData`、`TerrainMapRaster` 等数据结构虽然复制输入，但 public array 属性仍是可变数组。当前通过 snapshot/copy 规避内部泄漏，但长远最好为 gameplay-facing API 提供只读视图。
- 二级工具 API 和一级 gameplay API 的边界还需要文档和命名约束强化。

建议：

- 保留当前 facade。
- 明确一级稳定 API、二级工具 API、内部 API。
- 新功能优先追加到 facade/interface，而不是让其他模块引用底层 builder。
- 对 public 类型新增必须经过 API review，并更新 public API shape smoke。

### 4.4 担负的责任合不合理

回答：总体合理，但内部职责偏重。

合理边界：

- 地形系统负责地表、地貌、水系、语义、tile、plan、POI/route anchor 和 artifact。
- 地形系统不应负责最终任务逻辑、NPC 刷新、资源掉落、商店、阵营、天气本体、存档系统本体。

当前偏重位置：

- `TerrainTileBuilder` 同时承担 mesh、水面、路线、POI、聚落、散布、Native backend 策略。
- `TerrainWorldPlanner` 同时承担采样、候选评分、POI 选择、路线生成、规划统计。
- `TerrainWorld` 同时承担 Godot 节点、streaming、jobs、cache、plan async、facade、water plane。
- `TerrainValidation` 同时承担 CLI、测试框架、smoke、benchmark、反射 probe、报告输出。

结论：

系统的外部职责边界基本正确，内部需要拆分。

## 5. 商业级质量差距

### 高优先级差距

1. 大文件和大类过载  
   当前最大文件包括 `TerrainWorldPlanner.cs` 约 69 KB、`TerrainTileBuilder.cs` 约 63 KB、`TerrainTileBuilder.Settlements.cs` 约 43 KB、`TerrainWorld.cs` 约 40 KB、`TerrainWorldField.cs` 约 36 KB、`TerrainWorldPlanSerializer.cs` 约 31 KB、`TerrainChunk.cs` 约 29 KB。继续堆功能会损害单人长期维护。

2. 规则硬编码过多  
   Biome、POI、route、settlement、scatter、landmark 的评分和阈值大量写在代码里。商业项目后期会频繁调数，应该迁移到 profile/table/catalog。

3. API 面积过宽  
   63 个 public 类型已经被 shape smoke 锁定。稳定性是优点，但如果未分层，后续重构会被 public contract 绑住。

4. 缺少正式目标硬件性能基线  
   `TerrainPerformanceContract.TileBenchmarkHardwareBaseline` 仍为 `dev-linux-x64-provisional`。这说明性能 gate 是有的，但还不是正式生产目标。

5. 缺少导航和世界状态持久化  
   当前有 traversal cost，不等于 navmesh、navigation graph 或 AI pathfinding。当前有 plan JSON，不等于玩家修改后的世界存档。

6. 缺少编辑器工作流  
   商业级开放世界地形需要可视化 seed 比较、POI/route 审核、局部 override、artifact diff、手动锁点、导出检查。

### 中优先级差距

1. Native ABI 契约不足  
   Native bridge 支持 v1/v2 export，但建议增加 ABI version/capability query，避免 DLL/SO 漂移时只靠函数存在判断。

2. 验证工具单体化  
   当前验证覆盖强，但 console monolith 过大。应拆分 smoke modules 和 shared assertion helpers。

3. Chunk 表现仍是 primitive placeholder  
   当前散布和地标证明了数据链路，但不是最终资产表现。

4. 运行时事件缺失  
   目前其他模块可能需要轮询 snapshot。增加 signal 或事件可以降低耦合和成本。

5. 只读数据视图不足  
   通过 copy 保证隔离是可接受的，但频繁查询 POI/routes 时会分配数组。后续需要 non-alloc query 或 read-only view。

## 6. 开发计划

### P0：文档和契约收敛，立即完成

目标：

- 删除过时或损坏的地形分析/计划文档。
- 以本文件作为当前阶段唯一审核结论和开发计划。
- 保持 `dotnet build` 和 `--validation-tier pr` 通过。

验收：

- 旧 `TERRAIN_API_CONTRACT.md` 删除。
- 旧 `TERRAIN_SYSTEM_API_STABILIZATION_PLAN.md` 删除。
- 旧 `TERRAIN_SYSTEM_PHASE_AUDIT.md` 删除。
- 新 `TERRAIN_SYSTEM_PHASE_AUDIT_2026_06_05.md` 存在。
- Release build 通过。
- PR validation 通过。

### P1：API 分层和集成接口，建议 2 到 4 天

目标：

- 明确一级 gameplay-facing API、二级工具 API、内部 API。
- 让其他模块依赖小接口，而不是依赖 `TerrainWorld` 或生成内部类。

任务：

- 新增 `ITerrainQueryService`：
  - `SampleField`
  - `SampleSurface`
  - `SurfacePositionAt`
  - `SampleWaterState`
  - `SampleGameplayTags`
  - `SampleTraversalCost`
  - `IsTraversable`
  - `IsAboveWater`
- 新增 `ITerrainPlanProvider`：
  - `TryGetWorldPlanSnapshot`
  - `GetPointsOfInterest`
  - `GetRoutes`
  - `TryFindNearestPointOfInterest`
  - `QueryPointsOfInterest`
  - `QueryRoutesNear`
  - `SampleRouteCorridor`
- 新增 `ITerrainStreamingDiagnostics`：
  - `GetStreamingSnapshot`
- 让 `TerrainWorld` 实现这些接口。
- 更新 public API shape smoke。
- 新增一页轻量 API contract 文档，注意使用 UTF-8。

验收：

- 任务、AI、资源等未来模块可以只依赖接口。
- 当前 public facade 行为不破坏。
- PR validation 通过。

### P2：核心大文件拆分，建议 1 到 2 周

目标：

- 降低长期维护风险。
- 保持外部 API 不变，优先内部重组。

任务：

- 拆 `TerrainTileBuilder`：
  - `TerrainTileSurfaceSampler`
  - `TerrainTileMeshDataBuilder`
  - `TerrainTileWaterSurfaceBuilder`
  - `TerrainRouteTerrainModifier`
  - `TerrainPoiFootprintModifier`
  - `TerrainSettlementTileMaterializer`
  - `TerrainSurfaceScatterPlacer`
  - `TerrainScenicLandmarkPlacer`
  - `TerrainTileSamplingBackendSelector`
- 拆 `TerrainWorldPlanner`：
  - `TerrainPlanningGridSampler`
  - `TerrainPoiCandidateScorer`
  - `TerrainPoiSelector`
  - `TerrainRoutePlanner`
  - `TerrainPlanningReportBuilder`
  - `TerrainOpenWorldPlanFactory`
- 拆 `TerrainWorld`：
  - `TerrainStreamingSetBuilder`
  - `TerrainTileJobScheduler`
  - `TerrainTileCache`
  - `TerrainWorldPlanJobRunner`
  - `TerrainRuntimeQueryFacade`
- 拆 `TerrainValidation`：
  - `ValidationCli`
  - `TerrainQualitySmokes`
  - `TerrainRuntimeSmokes`
  - `TerrainArtifactSmokes`
  - `TerrainApiContractSmokes`
  - `TerrainBenchmarkSmokes`

验收：

- 外部 public API shape 不变，除非明确升级 version。
- 每次拆分后跑 `--validation-tier pr`。
- 拆分后没有任何单个 terrain 生产代码文件超过 30 KB，验证文件可暂时放宽到 40 KB。

### P3：数据驱动规则和调参工作流，建议 1 到 2 周

目标：

- 让 biome、POI、route、scatter、landmark 规则可调，不需要频繁改 C# 代码。

任务：

- 新增 `TerrainBiomeRuleSet` Resource。
- 新增 `TerrainPointOfInterestRuleSet` Resource。
- 新增 `TerrainRouteRuleSet` Resource。
- 新增 `TerrainScatterRuleSet` Resource。
- 新增 `TerrainLandmarkRuleSet` Resource。
- 把关键阈值从硬编码迁移到默认 rule set。
- 保留当前默认规则，确保同 seed 可控地迁移。
- 为 rule set 增加 stable hash，写入 plan report/JSON metadata。

验收：

- 不改代码即可调整主要 biome/POI/route/scatter 阈值。
- 默认规则生成结果通过当前 quality/planning/experience gate。
- Plan JSON 能记录 rule set identity。

### P4：模块对接能力，建议 1 到 2 周

目标：

- 让任务、AI、资源、地图 UI、音频、天气系统能低耦合集成。

任务：

- 增加 Godot signal：
  - `PlanReady`
  - `PlanCleared`
  - `ChunkLoaded`
  - `ChunkUnloaded`
  - `StreamingStateChanged`
- 增加 non-alloc 或 limited query：
  - 查询最近 N 个 POI。
  - 查询半径内 routes，不强制复制所有 waypoint。
  - 查询指定 gameplay tag 区域。
- 增加 resource/encounter placement helper：
  - 基于 `TerrainGameplayTags` 和 `TerrainTraversalCost` 返回候选点。
  - 不实例化资源或敌人，只输出候选。
- 增加 navigation handoff：
  - 导出 traversal cost grid。
  - 导出 route corridor graph。
  - 明确不在地形系统内执行角色级寻路。

验收：

- 上层模块不需要引用 `TerrainTileBuilder`。
- 上层模块不需要读取 `TerrainWorldPlan` 内部数组即可完成常见查询。
- Runtime API smoke 扩展覆盖 signal/readiness/non-alloc query。

### P5：性能和规模化，建议 2 到 4 周

目标：

- 从“能跑”升级到“可按目标硬件预算持续跑”。

任务：

- 确认目标平台：
  - 开发机 baseline。
  - 最低 PC baseline。
  - 目标主机或 Steam Deck baseline，如适用。
- 替换 `dev-linux-x64-provisional` 为正式 baseline。
- 扩展 benchmark：
  - managed/native tile P50/P95/P99。
  - allocation per tile。
  - streaming apply time。
  - chunk load/unload spike。
  - plan generation sync/async time。
  - memory cache pressure。
- Native bridge 增加 ABI version/capability query。
- 增加 LOD 接缝策略：
  - geomorph 或边界一致采样。
  - 远景 HLOD 或 impostor。
- 增加 artifact regression：
  - 多 seed map diff。
  - route/POI count drift。
  - biome coverage drift。

验收：

- Release tier 在正式目标硬件或固定 CI runner 上稳定通过。
- benchmark 输出进入 CI artifact。
- Native/managed parity 不依赖人工判断。

### P6：商业生产缺口，持续推进

目标：

- 补齐 3A 开放世界最终生产需要的地形生态。

任务：

- 最终资产管线：
  - biome 到材质/mesh/foliage asset 的映射。
  - scatter LOD。
  - instance culling。
  - impostor 或 HLOD。
- 编辑器工具：
  - seed browser。
  - plan overlay inspector。
  - POI lock/manual override。
  - route edit/lock。
  - local terrain override。
  - artifact export panel。
- 世界持久化：
  - plan persistence。
  - player modification delta。
  - chunk state save/load。
  - version migration。
- 导航：
  - route graph export。
  - traversal cost to nav layer handoff。
  - dynamic blockers handoff。
- QA：
  - 多 seed nightly/release farm。
  - artifact diff。
  - gameplay reachability checks。
  - disconnected island/settlement regression checks。

验收：

- 可以锁定某个 seed 作为 production world。
- 可以在编辑器中审查和调整关键 POI/route。
- 可以保存和迁移玩家影响。
- AI/任务/资源系统可以稳定使用地形查询和 anchor，而不绑定生成内部实现。

## 7. 近期优先级建议

按单人开发效率排序：

1. 先做 P1 API 接口分层。  
   原因：这是后续任务、AI、资源系统接入前的最低成本保险。

2. 再拆 `TerrainTileBuilder` 和 `TerrainWorldPlanner`。  
   原因：这两个文件是未来每次添加内容都会碰到的核心复杂区。

3. 再把规则数据驱动。  
   原因：开放世界调参会非常频繁，硬编码会吞掉大量维护时间。

4. 然后补 signal 和 non-alloc query。  
   原因：其他模块接入后，数组复制和轮询会变成实际成本。

5. 最后校准正式性能基线和 Native ABI。  
   原因：当前性能 gate 已存在，但还缺目标硬件定义。

## 8. 当前可保留的架构决策

这些设计方向是正确的，应继续保留：

- 地形系统输出语义，不直接生成任务/NPC/资源最终逻辑。
- `TerrainGenerationProfile` 作为异步任务不可变输入。
- `TerrainWorld` 作为运行时 facade。
- 纯函数 sampler 作为无场景依赖查询入口。
- `TerrainWorldPlan` 和 snapshot copy 避免内部状态泄漏。
- Anchor group/meta 作为 Godot 场景树解耦集成点。
- JSON 写入 contract/version/profile hash。
- enum/API/threshold/default state smoke 防止隐式漂移。
- Native sampler 可选，managed fallback 必须可用。

## 9. 当前不建议做的事

- 不建议立刻扩大 POI/biome/scatter 数量。先拆分和数据驱动。
- 不建议让任务系统直接读取 `TerrainWorldPlanner` 或 `TerrainTileBuilder`。
- 不建议把最终资源、NPC、商店、阵营逻辑放进地形系统。
- 不建议把当前 primitive scatter 当作最终美术管线。
- 不建议在性能基线未正式确定前宣称已达到最终 3A runtime 性能。
- 不建议继续维护多个地形审核/计划文档，避免结论漂移和编码损坏。

## 10. 最终判断

当前程序化地形系统的方向是正确的，且已经具备很强的可验证工程基础。它可以开始承担开放世界项目的地形基础层职责，也可以为其他模块提供稳定查询和 gameplay anchor。

它尚未达到完整商业级 3A 地形系统，核心差距集中在长期维护和生产管线，而不是单点算法能力。下一阶段的正确策略不是继续堆更多地貌和 POI，而是先收敛 API、拆分大模块、迁移硬编码规则、补齐编辑器/导航/持久化/性能基线。这样才能让一个独立开发者长期维护、扩展和调试这个系统。
