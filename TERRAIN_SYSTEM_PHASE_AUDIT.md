# 程序化地形系统阶段性审核报告

审核日期：2026-06-04  
项目路径：`d:\game61`  
目标定位：完全由 AI 生成的程序化地形系统，用于支撑 3A 大型开放世界游戏的地形、规划、流送、玩法锚点与验证流程。

## 1. 审核结论

当前系统已经不是单纯的噪声高度图 Demo，而是一个可运行的开放世界地形原型框架。它包含：

- 参数化地形生成配置。
- 任意世界坐标的地形场采样。
- 分块 tile 网格生成。
- LOD、异步任务、缓存、碰撞和水面流送。
- 开放世界规划器，生成区域、POI、聚落、路线网络。
- POI 和路线对地形 tile 的实体化影响，包括道路走廊、聚落足迹、服务设施、路线标记、桥段。
- 生态散布、资源点、危险地貌、自然景观地标。
- 可被玩法系统扫描的 Godot anchor/group/meta。
- PNG 地图和文本报告导出。
- CLI 校验工具与多类 smoke test。
- 可选 C++ GDExtension 原生采样加速桥接。

阶段性判断：系统具备较高工程价值，已经能作为开放世界玩法原型、内容布局验证和运行时地形流送的基础层使用。但距离 3A 生产级地形系统仍有缺口，主要在美术资产管线、长期世界持久化、导航数据、物理/植被规模优化、多人/存档确定性契约、编辑器工作流和目标硬件性能基线方面。

## 2. 已完成的有价值工作

### 2.1 地形基础场

核心位置：

- `dao/Scripts/Terrain/TerrainSettings.cs`
- `dao/Scripts/Terrain/Generation/TerrainWorldField.cs`
- `dao/Scripts/Terrain/Generation/TerrainSampler.cs`
- `dao/Scripts/Terrain/Generation/ProceduralNoise.cs`

已完成能力：

- `TerrainSettings` 作为 Godot `Resource` 暴露种子、chunk 尺寸、分辨率、LOD、海平面、山脉、河流、视野点、水蚀、碰撞和 Native 采样开关。
- `TerrainGenerationProfile` 提供不可变配置快照，避免运行中设置被异步任务读到半更新状态。
- `TerrainWorldFieldSampler.Sample()` 可在任意 `Vector2` 世界坐标生成完整地形场：
  - 高度。
  - 大陆性、盆地、陆架、山脉、宽域海拔。
  - 河流、湖泊、水分、温度。
  - 风景潜力、可通行性、暴露度、资源潜力、危险潜力、遭遇潜力。
  - 生物群系 `TerrainBiomeKind`。
  - 地貌类型 `TerrainLandscapeKind`。
- `TerrainSampler.SampleWithSlope()` 和 `NormalAt()` 提供坡度、法线、表面颜色，可直接给移动、落点、特效、AI 评估使用。

价值：系统已经把“地形视觉生成”和“玩法语义场”合并在同一套确定性采样模型中，玩法模块不需要反向从网格推断地形属性。

### 2.2 Tile 生成与运行时流送

核心位置：

- `dao/Scripts/Terrain/Generation/TerrainTileBuilder.cs`
- `dao/Scripts/Terrain/Generation/TerrainTileData.cs`
- `dao/Scripts/Terrain/Rendering/TerrainMeshBuilder.cs`
- `dao/Scripts/Terrain/Streaming/TerrainWorld.cs`
- `dao/Scripts/Terrain/Streaming/TerrainChunk.cs`

已完成能力：

- `TerrainTileBuilder.Build()` 生成单块 tile 的完整数据：
  - 顶点、法线、UV、顶点色、索引。
  - skirt，降低 chunk 接缝可见性。
  - 可选碰撞三角面。
  - 局部河流、湖泊、绿洲水面。
  - surface scatter。
  - planned landmark 数据。
- `TerrainWorld` 运行时按 focus 节点位置流送 terrain chunk：
  - 环形加载区域。
  - Chebyshev 距离和 LOD。
  - 近处生成碰撞，远处降低 LOD。
  - 后台 `Task.Run` 生成 tile。
  - 每帧限制完成 tile 应用数量。
  - tile LRU 缓存。
  - world plan 变化后清缓存、取消旧任务、重建 chunk。
- `TerrainChunk.Apply()` 将 tile 数据变成 Godot 节点：
  - `ArrayMesh` 地形。
  - `ArrayMesh` 局部水面。
  - `MultiMeshInstance3D` 散布物。
  - `ConcavePolygonShape3D` 碰撞。

价值：这部分已经达到“可运行开放世界地形流送原型”的程度，而不是离线生成静态网格。

### 2.3 开放世界规划

核心位置：

- `dao/Scripts/Terrain/Generation/TerrainWorldPlanner.cs`
- `dao/Scripts/Terrain/Generation/TerrainQualityAnalyzer.cs`
- `dao/Scripts/Terrain/Generation/TerrainExperienceAnalyzer.cs`
- `dao/Scripts/Terrain/Generation/TerrainRouteCorridorIndex.cs`
- `dao/Scripts/Terrain/Generation/TerrainPointOfInterestIndex.cs`

已完成能力：

- `TerrainWorldPlanner.CreateOpenWorldPlan()` 在地形场上采样规划网格，并生成：
  - `TerrainWorldRegion[]` 区域语义。
  - `TerrainWorldPointOfInterest[]` POI。
  - `TerrainWorldRoute[]` 路线网络。
  - `TerrainQualityReport`。
  - `TerrainWorldPlanningReport`。
  - `TerrainExperienceReport`。
- POI 类型包括：
  - 聚落候选。
  - 观景点。
  - 河流渡口。
  - 山口。
  - 海岸登陆点。
  - 资源林地。
  - 古代遗址。
  - 峡谷眺望点。
  - 绿洲。
- 聚落层级包括：
  - `None`
  - `Village`
  - `Town`
  - `OasisHub`
- 路线类型包括：
  - `PrimaryTrail`
  - `RiverRoad`
  - `RidgePass`
  - `CoastalPath`
  - `ScenicTrail`
- 默认开放世界规划门槛包括：
  - 至少 18 个 POI。
  - 至少 48 条路线。
  - 至少 3 类路线。
  - POI 连接率不低于 0.95。
  - 聚落连接率不低于 0.95。
  - 至少 2 个村庄、2 个城镇、1 个绿洲枢纽。

价值：系统已经有“地形驱动的内容布局层”，可作为任务、探索、聚落、路线和遭遇系统的上游。

### 2.4 路线、POI 与地表实体化

核心位置：

- `dao/Scripts/Terrain/Generation/TerrainTileBuilder.RouteScatter.cs`
- `dao/Scripts/Terrain/Generation/TerrainTileBuilder.Settlements.cs`
- `dao/Scripts/Terrain/Generation/TerrainTileBuilder.SurfaceScatter.cs`
- `dao/Scripts/Terrain/Generation/TerrainTileBuilder.ScenicLandmarks.cs`

已完成能力：

- 路线 corridor 会影响 tile 高度和颜色，形成可见路径。
- route scatter 会生成道路标记和桥段。
- POI footprint 会影响地表形态、颜色和 landmark。
- 聚落可实体化为村庄、城镇、绿洲枢纽及内部服务设施：
  - 房屋、街区、绿洲棚、广场、绿洲池。
  - 井、市集摊位、瞭望塔、绿洲花园、聚落入口。
- 普通生态散布包括树、岩石、草、灌木、仙人掌、芦苇、雪堆、高山松、海岸棕榈、漂木、红树林根、湖芦苇、睡莲。
- 玩法散布包括：
  - `Understory`
  - `ResourceNode`
  - `HazardOutcrop`
- 景观自然地标包括：
  - 瀑布、沙丘脊、沙漠巨石、峡谷针岩、冰刺、天然拱、地热泉、冰川脊。

价值：规划结果已经不是只停留在数据层，而是能反馈到地形和可见内容，形成“规划 - 生成 - 运行时表现”的闭环。

### 2.5 运行时玩法锚点

核心位置：

- `dao/Scripts/Terrain/Runtime/TerrainWorldPlanOverlay.cs`
- `dao/Scripts/Terrain/Runtime/TerrainWorldPointOfInterestAnchor.cs`
- `dao/Scripts/Terrain/Runtime/TerrainWorldRouteAnchor.cs`
- `dao/Scripts/Terrain/Runtime/TerrainPointOfInterestArchetypeCatalog.cs`

已完成能力：

- `TerrainWorldPlanOverlay.ApplyPlan()` 可把 plan 转成可视化路线 ribbon、POI marker 和 gameplay anchor。
- POI anchor 会加入 Godot group：
  - `terrain_poi`
  - 对应玩法 tag，例如 `poi.vista`、`poi.resource_grove`、`poi.oasis`。
- POI anchor 暴露 meta：
  - `terrain_poi_id`
  - `terrain_poi_kind`
  - `terrain_poi_visual`
  - `terrain_poi_gameplay_tag`
  - `terrain_poi_score`
  - `terrain_poi_scenic`
  - `terrain_poi_traversability`
  - `terrain_poi_settlement_tier`
  - `terrain_poi_landscape`
  - `terrain_poi_interaction_radius`
  - `terrain_poi_encounter_budget`
- Route anchor 会加入 Godot group：
  - `terrain_route`
- Route anchor 暴露 meta：
  - `terrain_route_kind`
  - `terrain_route_from`
  - `terrain_route_to`
  - `terrain_route_cost`
  - `terrain_route_scenic`
  - `terrain_route_traversability`

价值：任务、遭遇、AI、导航、资源、快速旅行、探索奖励等模块可以不直接解析 plan，而是通过 Godot 场景树 group/meta 扫描锚点。

### 2.6 导出、验证和工具链

核心位置：

- `dao/Scripts/Terrain/Generation/TerrainMapExporter.cs`
- `dao/Scripts/Terrain/Generation/TerrainWorldPlanExporter.cs`
- `tools/TerrainValidation/Program.cs`

已完成能力：

- 地图导出支持层：
  - 生物群系。
  - 高度。
  - 河流。
  - 湿度。
  - 温度。
  - 风景潜力。
  - 可通行性。
  - 暴露度。
  - 资源潜力。
  - 危险潜力。
  - 遭遇潜力。
  - 地貌。
- `TerrainWorldPlanExporter` 可导出开放世界 plan PNG 和文本报告。
- `tools/TerrainValidation` 覆盖：
  - 地形质量门槛。
  - 开放世界规划门槛。
  - 体验门槛。
  - POI archetype 覆盖。
  - route corridor tile 影响。
  - route scatter 实体化。
  - POI tile landmark 实体化。
  - gameplay scatter 实体化。
  - biome scatter 与局部水面实体化。
  - scenic landmark 实体化。
  - map/report artifact 导出。
  - runtime `TerrainWorld` 生成与实体化。
  - Native sampler parity。
  - tile benchmark。

价值：这是当前系统最重要的工程资产之一。它让 AI 生成系统具备可重复验收的质量门槛，而不是只靠截图判断。

### 2.7 Native 采样桥接

核心位置：

- `dao/Scripts/Terrain/Generation/NativeTerrainBridge.cs`
- `gdextension/src/dao_extension.cpp`
- `gdextension/src/dao_extension.h`
- `dao/bin/dao.gdextension`

已完成能力：

- C# 侧会查找 `dao.windows.template_release.x86_64.dll`、`dao.windows.template_debug.x86_64.dll` 等库。
- 支持原生导出：
  - `dao_native_sample_height_grid`
  - `dao_native_sample_height_grid_v2`
  - `dao_native_sample_field_grid_v1`
  - `dao_native_sample_field_grid_v2`
- `TerrainTileBuilder.ShouldUseNativeSamplerForTileGeneration()` 会基于配置、LOD、分辨率和校准结果决定是否使用 Native。
- Native 不可用时自动回退 C# managed sampler。

价值：已经为高频 tile 采样建立性能扩展路径，同时保留 managed fallback。

## 3. 当前可支撑的游戏性

### 3.1 探索与地标发现

系统可支撑：

- 观景点发现。
- 峡谷眺望点。
- 山口探索。
- 海岸登陆点。
- 古代遗址。
- 自然奇观发现，例如瀑布、天然拱、冰刺、沙漠巨石。

可用数据：

- `TerrainWorldPointOfInterest.Kind`
- `Score`
- `ScenicPotential`
- `LandscapeKind`
- `TerrainWorldPointOfInterestAnchor` group/meta

适合模块：

- 探索奖励。
- 地图揭示。
- 成就系统。
- 摄影/观景任务。
- 开放世界兴趣点发现。

### 3.2 道路、路径与旅行节奏

系统可支撑：

- 主路、河路、山脊通道、海岸路线、风景路线。
- 道路沿线 marker。
- 桥段。
- 聚落之间的连接网络。
- 路线 scenic/traversability 成本评估。

可用数据：

- `TerrainWorldRoute`
- `TerrainRouteKind`
- `Waypoints`
- `AverageScenicPotential`
- `AverageTraversability`
- `Cost`
- `TerrainWorldRouteAnchor`
- `TerrainRouteCorridorIndex.Sample()`

适合模块：

- AI 巡逻路径。
- 商队/交通路线。
- 快速旅行解锁。
- 主线任务路径建议。
- 动态遭遇沿路生成。

注意：当前系统提供路线语义和 waypoint，不等价于完整 navmesh 或角色级寻路系统。

### 3.3 聚落、据点与服务设施

系统可支撑：

- 村庄、城镇、绿洲枢纽。
- 聚落入口、广场、井、市集、瞭望塔、花园。
- 聚落内部散布。
- 聚落之间的连通性检查。

可用数据：

- `TerrainSettlementTier`
- `TerrainPointOfInterestKind.SettlementCandidate`
- `TerrainPointOfInterestKind.Oasis`
- `TerrainPointOfInterestAnchor.InteractionRadius`
- `EncounterBudget`
- group `terrain_poi`
- gameplay tag `poi.settlement_candidate` / `poi.oasis`

适合模块：

- NPC 聚落生成。
- 商店/服务点。
- 安全区。
- 阵营据点。
- 任务 hub。

### 3.4 资源采集、风险和遭遇

系统可支撑：

- 资源节点投放。
- 危险地貌/危险物投放。
- 草丛/灌木/生态物件分布。
- 根据资源、危险、暴露、可通行性综合生成遭遇潜力。

可用数据：

- `TerrainWorldField.ResourcePotential`
- `TerrainWorldField.HazardPotential`
- `TerrainWorldField.EncounterPotential`
- `TerrainScatterKind.ResourceNode`
- `TerrainScatterKind.HazardOutcrop`
- `TerrainExperienceReport`

适合模块：

- 采集系统。
- 怪物刷新。
- 动态遭遇。
- 风险/收益区域生成。
- 生存玩法资源分布。

### 3.5 生物群系和环境表现

系统可支撑：

- 海洋、海岸、岛屿、平原、草地、沙漠、绿洲、森林、湿地、丘陵、山地、雪原、湖泊。
- 对应生态散布。
- 水体、河流、湖泊、绿洲局部水面。

可用数据：

- `TerrainBiomeKind`
- `TerrainLandscapeKind`
- `TerrainMapLayer`
- `TerrainWaterSurfaceData`
- biome scatter instances。

适合模块：

- 环境音频。
- 天气/气候系统。
- 生物刷新表。
- 材质/植被替换。
- 视觉后处理区域。

### 3.6 QA、策划评审和内容验收

系统可支撑：

- 自动生成开放世界地图。
- 自动输出 plan 文本报告。
- 多 seed 批量验收。
- 质量门槛回归测试。
- 规划、体验、实体化 smoke test。

可用入口：

- `TerrainWorldPlanExporter.SaveOpenWorldArtifacts()`
- `TerrainMapExporter.SaveMap()`
- `dotnet run --project tools/TerrainValidation/TerrainValidation.csproj`

适合模块：

- 技术美术评审。
- 关卡设计 review。
- CI 回归。
- 种子筛选。
- 内容密度调参。

## 4. 其他模块如何集成使用

### 4.1 最小运行时集成：在场景中挂 TerrainWorld

推荐用于游戏运行时。

步骤：

1. 在 Godot 场景中添加 `TerrainWorld` 节点，或从 C# 创建。
2. 创建并赋值 `TerrainSettings`。
3. 设置 `FocusPath` 或调用 `SetFocus(playerOrCamera)`。
4. 开启 `GenerateOpenWorldPlanOnReady`。
5. 运行后读取 `terrainWorld.WorldPlan` 或通过 overlay 生成 anchor。

示例：

```csharp
var settings = new TerrainSettings
{
    Seed = 613061,
    ChunkSize = 192.0f,
    BaseResolution = 64,
    StreamRadiusChunks = 6,
    CollisionRadiusChunks = 2,
    MaxLod = 3,
    GenerateCollision = true,
    UseNativeSamplerWhenAvailable = true
};

var terrainWorld = new TerrainWorld
{
    Settings = settings,
    GenerateOpenWorldPlanOnReady = true,
    GenerateOpenWorldPlanAsync = true,
    OpenWorldPlanWorldSize = 12288.0f,
    CreateWaterPlane = true
};

AddChild(terrainWorld);
terrainWorld.SetFocus(playerNode);
```

参考实现：`dao/Scripts/Demo/TerrainDemo.cs`。

### 4.2 查询任意位置地形语义

推荐给移动、AI、资源、音频、天气、任务落点系统。

```csharp
TerrainGenerationProfile profile = terrainWorld.Profile;
Vector2 world2D = new(player.GlobalPosition.X, player.GlobalPosition.Z);

TerrainWorldField field = TerrainWorldFieldSampler.Sample(world2D, profile);

if (field.Traversability > 0.6f && field.HazardPotential < 0.4f)
{
    // 适合作为普通 NPC 行走/任务落点候选。
}
```

如需坡度和颜色：

```csharp
TerrainSample sample = TerrainSampler.SampleWithSlope(world2D, profile, spacing: 4.0f);
Vector3 normal = TerrainSampler.NormalAt(world2D, profile, spacing: 4.0f);
```

### 4.3 使用开放世界计划数据

推荐给任务、探索、聚落、路线、AI director。

```csharp
TerrainWorldPlan? plan = terrainWorld.WorldPlan;
if (plan is null)
{
    return;
}

foreach (TerrainWorldPointOfInterest point in plan.PointsOfInterest)
{
    if (point.Kind == TerrainPointOfInterestKind.ResourceGrove)
    {
        Vector3 position = new(point.WorldPosition.X, point.Height, point.WorldPosition.Y);
        // 在资源林地附近挂任务、采集点或遭遇。
    }
}

foreach (TerrainWorldRoute route in plan.Routes)
{
    Vector2[] waypoints = route.Waypoints;
    // 交给巡逻、商队或路径引导系统。
}
```

### 4.4 通过 Godot group/meta 扫描玩法锚点

推荐给更解耦的运行时模块。

先创建 overlay：

```csharp
var overlay = new TerrainWorldPlanOverlay
{
    BuildGameplayAnchors = true,
    ShowPointMarkers = false,
    ShowRouteRibbons = false
};

AddChild(overlay);
overlay.ApplyPlan(plan, terrainWorld.Profile);
```

然后其他系统扫描：

```csharp
foreach (Node node in GetTree().GetNodesInGroup("terrain_poi"))
{
    string kind = node.GetMeta("terrain_poi_kind").AsString();
    float score = (float)node.GetMeta("terrain_poi_score");
    string tag = node.GetMeta("terrain_poi_gameplay_tag").AsString();
}

foreach (Node node in GetTree().GetNodesInGroup("terrain_route"))
{
    string routeKind = node.GetMeta("terrain_route_kind").AsString();
    float scenic = (float)node.GetMeta("terrain_route_scenic");
}
```

### 4.5 手动生成单块 tile

推荐给离线构建、编辑器工具、调试工具或独立系统。

```csharp
TerrainTileCoord coord = TerrainTileCoord.FromWorldPosition(worldPosition, profile.ChunkSize);
TerrainTileData data = TerrainTileBuilder.Build(
    coord,
    lod: 0,
    profile,
    includeCollision: true);

ArrayMesh mesh = TerrainMeshBuilder.CreateMesh(data);
```

如果希望 tile 受到世界计划路线和 POI 影响：

```csharp
TerrainRouteCorridorIndex corridors = TerrainRouteCorridorIndex.FromPlan(plan, profile);
TerrainPointOfInterestIndex poiIndex = TerrainPointOfInterestIndex.FromPlan(plan, profile);

TerrainTileData data = TerrainTileBuilder.Build(
    coord,
    lod: 0,
    profile,
    includeCollision: true,
    corridors,
    poiIndex);
```

### 4.6 导出地图和报告

```csharp
TerrainWorldPlanArtifactResult result =
    TerrainWorldPlanExporter.SaveOpenWorldArtifacts(
        plan,
        profile,
        imageSize: 512,
        outputDirectory: "user://terrain");

if (!result.Passed)
{
    GD.PushWarning(result.PlanningGate.Summary);
}
```

单独导出图层：

```csharp
TerrainMapExporter.SaveMap(
    profile,
    Vector2.Zero,
    worldSize: 12288.0f,
    imageSize: 1024,
    TerrainMapLayer.ResourcePotential,
    "user://terrain/resource_map.png");
```

## 5. 具体接口清单

### 5.1 Godot 可挂载节点和资源

| 接口 | 文件 | 用途 |
|---|---|---|
| `TerrainSettings : Resource` | `dao/Scripts/Terrain/TerrainSettings.cs` | 编辑器暴露的地形参数资源 |
| `TerrainWorld : Node3D` | `dao/Scripts/Terrain/Streaming/TerrainWorld.cs` | 运行时地形流送、异步 tile 生成、open-world plan 生成 |
| `TerrainWorldPlanOverlay : Node3D` | `dao/Scripts/Terrain/Runtime/TerrainWorldPlanOverlay.cs` | POI/route 可视化与 gameplay anchor 生成 |
| `TerrainChunk : Node3D` | `dao/Scripts/Terrain/Streaming/TerrainChunk.cs` | 单个流送 chunk 的 mesh、water、scatter、collision 挂载节点 |
| `TerrainWorldPointOfInterestAnchor : Marker3D` | `dao/Scripts/Terrain/Runtime/TerrainWorldPointOfInterestAnchor.cs` | POI 玩法锚点 |
| `TerrainWorldRouteAnchor : Node3D` | `dao/Scripts/Terrain/Runtime/TerrainWorldRouteAnchor.cs` | 路线玩法锚点 |

### 5.2 运行时主要方法

| 方法 | 用途 |
|---|---|
| `TerrainSettings.Snapshot()` | 生成不可变 `TerrainGenerationProfile` |
| `TerrainWorld.SetFocus(Node3D focus)` | 设置流送中心 |
| `TerrainWorld.SetWorldPlan(TerrainWorldPlan? worldPlan)` | 应用或清空世界计划 |
| `TerrainWorld.Regenerate()` | 重建 profile、plan、chunk、cache |
| `TerrainWorld.GenerateOpenWorldPlan(bool apply = true)` | 同步生成 plan，可选择应用到流送 |
| `TerrainWorld.CreateRuntimeOpenWorldPlan(...)` | 静态同步生成运行时 plan |
| `TerrainWorld.CreateRuntimeOpenWorldPlanAsync(...)` | 静态异步生成运行时 plan |
| `TerrainWorldPlanOverlay.ApplyPlan(plan, profile)` | 根据 plan 生成 overlay 和 anchors |
| `TerrainWorldPlanOverlay.ClearPlan()` | 清理 overlay 和 anchors |

### 5.3 生成、采样和查询接口

| 方法 | 用途 |
|---|---|
| `TerrainWorldFieldSampler.Sample(world, profile)` | 任意点完整地形场 |
| `TerrainWorldFieldSampler.SampleKnownHeight(world, profile, height)` | 已知高度时补齐语义场 |
| `TerrainWorldFieldSampler.LandBalanceOffsetFor(profile)` | 获取陆地比例平衡偏移 |
| `TerrainSampler.Sample(world, profile)` | 任意点地形简化采样 |
| `TerrainSampler.SampleWithSlope(world, profile, spacing)` | 带坡度和颜色的采样 |
| `TerrainSampler.NormalAt(world, profile, spacing)` | 获取近似法线 |
| `TerrainTileCoord.FromWorldPosition(worldPosition, chunkSize)` | 世界坐标转 tile 坐标 |
| `TerrainTileBuilder.Build(...)` | 构建 tile 数据 |
| `TerrainTileBuilder.ShouldUseNativeSamplerForTileGeneration(profile, lod)` | 查询 adaptive tile builder 是否会用 Native |
| `TerrainRouteCorridorIndex.FromPlan(plan, profile)` | 建立路线 corridor 空间索引 |
| `TerrainRouteCorridorIndex.Sample(world, coord)` | 查询点附近路线 corridor 影响 |
| `TerrainPointOfInterestIndex.FromPlan(plan, profile)` | 建立 POI footprint 空间索引 |
| `TerrainPointOfInterestIndex.GetPoints(coord)` | 获取影响某 tile 的 POI |

### 5.4 规划和验证接口

| 方法 | 用途 |
|---|---|
| `TerrainWorldPlanner.CreatePlan(...)` | 指定规划分辨率、POI 数、路线数生成 plan |
| `TerrainWorldPlanner.CreateOpenWorldPlan(...)` | 使用开放世界默认配置生成 plan |
| `TerrainWorldPlanner.AnalyzePlanning(plan)` | 统计 POI/路线规划质量 |
| `TerrainWorldPlanner.ValidateOpenWorldPlanning(plan)` | 开放世界规划门槛验证 |
| `TerrainQualityAnalyzer.Analyze(...)` | 地形质量采样统计 |
| `TerrainQualityAnalyzer.ValidateOpenWorldDefault(...)` | 开放世界地形质量门槛验证 |
| `TerrainExperienceAnalyzer.Analyze(plan)` | 分析体验指标 |
| `TerrainExperienceAnalyzer.ValidateOpenWorldDefault(...)` | 开放世界体验门槛验证 |
| `TerrainPointOfInterestArchetypeCatalog.ValidatePlanReadiness(plan)` | 检查 plan 中 POI 是否都有运行时 archetype |

### 5.5 渲染和导出接口

| 方法 | 用途 |
|---|---|
| `TerrainMeshBuilder.CreateMesh(data)` | tile 数据转 `ArrayMesh` |
| `TerrainMeshBuilder.CreateWaterMesh(data)` | tile 水面数据转 `ArrayMesh` |
| `TerrainMaterialFactory.CreateTerrainMaterial()` | 地形材质 |
| `TerrainMaterialFactory.CreateWaterMaterial()` | 大水面材质 |
| `TerrainMaterialFactory.CreateLocalWaterMaterial()` | 局部水面材质 |
| `TerrainMaterialFactory.CreateScatterMaterial(kind)` | 散布物材质 |
| `TerrainMapExporter.CreateMap(...)` | 创建指定图层地图 |
| `TerrainMapExporter.SaveMap(...)` | 保存指定图层 PNG |
| `TerrainWorldPlanExporter.CreatePlanMap(...)` | 创建带 POI/路线 overlay 的 plan 地图 |
| `TerrainWorldPlanExporter.SaveOpenWorldArtifacts(...)` | 保存 plan PNG 和文本报告 |

### 5.6 CLI 校验工具

主要命令：

```powershell
dotnet run --project tools\TerrainValidation\TerrainValidation.csproj
```

常用参数：

```powershell
--seed 613061
--seed-count 10
--seed-step 10007
--world-size 12288
--artifact-image-size 512
--artifact-output-dir <path>
--smoke-all-seeds
--native-smoke
--benchmark-tiles
--benchmark-tile-count 48
--verbose
```

## 6. 本次实测结果

### 6.1 构建

命令：

```powershell
dotnet build dao\dao.csproj
```

结果：

- 构建成功。
- 0 error。
- 有 `NU1900` warning，原因是无法访问 `https://api.nuget.org/v3/index.json` 获取包漏洞数据。该 warning 不影响本次编译产物。

### 6.2 地形校验

命令：

```powershell
dotnet run --project tools\TerrainValidation\TerrainValidation.csproj -- --seed-count 1 --artifact-image-size 128
```

结果：整体 PASS。

关键数据：

- Seed：613061。
- World size：12288。
- Planning grid：60 x 60。
- 地形：
  - Land ratio：0.522。
  - Scenic ratio：0.147。
  - Traversable land：0.499。
  - River coverage：0.089。
  - Landscape kinds：10。
  - Biome kinds：12。
  - Height range：-243.3 到 608.2。
- 规划：
  - POIs：48。
  - Routes：64。
  - POI kinds：8。
  - Route kinds：5。
  - Villages/Towns/Oasis hubs：9/5/4。
  - Connected point ratio：1.000。
  - Connected settlement ratio：1.000。
  - Settlement routes：17。
  - POI coverage：0.983。
  - Route coverage：0.983。
  - Average route scenic/traversability：0.620 / 0.827。
- 体验：
  - Encounter-rich/resource-rich/hazard-rich：0.575 / 0.870 / 0.248。
  - Average encounter potential：0.574。
  - Route rhythm：0.836。
  - Point of interest value：0.778。
  - Risk reward balance：0.791。
  - Scenic anchor ratio：0.708。
- 实体化 smoke：
  - Route corridor tile smoke：PASS。
  - Route scatter smoke：PASS。
  - POI tile landmark smoke：PASS。
  - Gameplay scatter smoke：PASS。
  - Biome scatter smoke：PASS。
  - Scenic landmark smoke：PASS。
  - Open world artifact smoke：PASS。
  - Runtime `TerrainWorld` smoke：PASS。
- Runtime smoke：
  - POIs materialized：48/48。
  - Routes：64。
  - Sampled tiles：127。
  - Async plan：394.9 ms，结果匹配同步生成。
  - Async cancellation：29.1 ms，PASS。
  - Route/POI indices：yes/yes。
  - Markers/bridges：591/145。
  - Settlement scatter：219。

## 7. 风险与缺口

### 7.1 生产级 3A 风险

当前系统更接近“强开放世界地形原型框架”，尚不是完整 3A 地形生产系统。主要缺口：

- 美术资产仍以 primitive mesh 和程序色为主，没有真实资产、材质层、HLOD、植被 impostor、地表 decal、道路材质混合管线。
- 没有完整地形编辑器工作流，例如手工修饰、锁定 POI、局部重生成、设计师覆盖层。
- 没有世界持久化协议，例如保存已生成 plan、tile cache、玩家改造、破坏或建筑。
- 没有 navmesh 或寻路图导出。路线 waypoint 可做高层路径，但不能替代角色/载具导航。
- 没有明确多人同步、跨平台浮点一致性和版本兼容契约。
- 运行时性能尚未在目标硬件、目标视距、目标植被密度下建立预算。
- 当前校验默认可以批量 seed，但本次仅实际跑了 1 个 seed；建议 CI 至少跑多 seed smoke。
- Native C++ 与 managed C# 有双实现，后续算法改动必须同时维护 parity test，否则容易漂移。
- Tile 生成使用 `Task.Run` 和自管理队列，后续大规模并发时需要更明确的 job scheduler、优先级、超时和内存预算。

### 7.2 集成风险

- 玩法模块如果直接依赖 enum ordinal 或 meta string，需要冻结命名和版本迁移策略。
- `TerrainWorldPlan` 当前作为内存对象传递，若任务/AI/存档长期持有引用，需要定义生命周期。
- `SetWorldPlan()` 会清理 chunk/cache 并重建，运行中切换 plan 可能导致短时视觉重载，需要上层处理过渡。
- `TerrainWorldPlanOverlay` 同时承担可视化和 gameplay anchor 构建，未来可考虑拆成 debug overlay 与 runtime anchor builder 两个职责。

## 8. 建议后续阶段

### 8.1 短期建议

- 把 `tools/TerrainValidation` 接入 CI，至少跑：
  - 默认 seed。
  - 10 seed batch。
  - `--native-smoke`。
  - 小规模 `--benchmark-tiles`。
- 增加一份“玩法集成示例场景”，展示：
  - 从 `terrain_poi` group 生成任务。
  - 从 route anchor 生成巡逻路线。
  - 从 `ResourcePotential` 投放采集物。
- 给 `TerrainWorldPlan` 增加 JSON 或二进制导出/导入，便于策划审核和运行时复用。
- 明确 enum/tag/meta 的稳定性规则，避免玩法模块后续被重命名破坏。

### 8.2 中期建议

- 分离 debug overlay 和 runtime anchor builder。
- 增加导航数据导出：
  - 区域通行成本图。
  - 路线图。
  - 可选 navmesh bake 输入。
- 建立资产替换层：
  - `TerrainScatterKind` 到 PackedScene/mesh/material 的可配置映射。
  - `TerrainLandmarkKind` 到真实 landmark prefab 的映射。
- 增加 world origin shifting 或大世界坐标策略。
- 增加 tile 生成性能报告基线，并固定目标帧预算。

### 8.3 长期建议

- 支持手工设计约束：
  - 固定主城/任务点。
  - 禁止区。
  - 路线强制连接。
  - 生物群系权重图。
- 支持增量重生成和局部锁定。
- 支持流送数据持久化和离线烘焙。
- 支持地表材质层、道路 decal、植被 HLOD、远景 impostor。
- 支持多人/存档版本化和确定性回归。

## 9. 结论

当前系统已经完成了一个有实际工程价值的程序化开放世界地形底座。它的核心优势是：地形生成、玩法语义、POI/路线规划、运行时流送、可视化锚点和自动化验收之间已经形成闭环。

当前最适合作为：

- 开放世界玩法原型底座。
- 程序化内容布局验证工具。
- 任务/遭遇/资源/聚落系统的上游数据源。
- 地形流送与 tile 生成技术验证平台。
- AI 生成内容质量门槛的回归测试样板。

下一阶段不建议继续单纯增加噪声或地貌类型，而应优先做生产化接口：稳定数据契约、可持久化 plan、玩法集成示例、CI 多 seed 验收、资产映射层和导航导出。
