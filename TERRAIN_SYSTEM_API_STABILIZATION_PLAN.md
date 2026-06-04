# 地形系统 API 稳定化与模块对接计划

制定日期：2026-06-04 20:45  
适用项目：`d:\game61`  
关联报告：`TERRAIN_SYSTEM_PHASE_AUDIT.md`

## 1. 计划目标

本计划的核心目标是把当前程序化地形系统收敛为一个稳定、可靠、可被其他游戏模块长期依赖的“地形运行时基础设施”。

地形系统下一阶段的重点不是继续扩大成资产、任务、AI、资源、天气或美术表现系统，而是做好本职工作：

- 稳定地生成地形。
- 稳定地运行时流送地形。
- 稳定地查询地形语义。
- 稳定地暴露 POI、路线、地貌、水系和玩法锚点。
- 稳定地为其他模块提供契约化 API。
- 稳定地通过验证工具防止回归。

一句话目标：

> 地形系统负责“世界地表和地形语义的确定性生成与运行时供给”，其他游戏模块基于稳定 API 接入，不反向依赖地形内部实现。

## 2. 范围边界

### 2.1 地形系统负责

地形系统应负责以下内容：

- 地形参数和种子管理。
- 任意世界坐标的地形场采样。
- 高度、坡度、法线、颜色、生物群系、地貌、水系、可通行性、资源潜力、危险潜力、遭遇潜力等语义数据。
- tile 网格生成。
- tile LOD。
- tile 异步生成。
- tile 缓存。
- tile 碰撞。
- 局部水面。
- 基础占位 scatter 和 landmark 数据。
- 开放世界 plan 生成。
- POI、聚落、路线网络规划。
- route corridor 和 POI footprint 对 tile 的影响。
- runtime gameplay anchor 输出。
- 地图、报告、验证工具。
- Native sampler 可选加速和 managed fallback。

### 2.2 地形系统不负责

下一阶段明确不把以下内容放进地形核心：

- 最终美术资产选择。
- 最终地表材质系统。
- 植被美术资产管线。
- 任务系统。
- NPC 生成逻辑。
- 阵营系统。
- 商店系统。
- 怪物刷新规则。
- 角色级寻路。
- navmesh 烘焙本身。
- 天气系统本身。
- 音频系统本身。
- 存档系统本身。
- 玩家建造、破坏、地形改造的长期状态管理。

这些模块可以使用地形系统提供的数据，但不应成为地形系统内部职责。

## 3. 设计原则

### 3.1 稳定优先

公开 API 一旦被其他模块使用，后续应尽量只追加、不破坏。

具体规则：

- enum 只追加，不重排。
- meta key 只追加，不重命名。
- group 名称只追加，不重命名。
- public record 字段谨慎改名。
- public 方法签名谨慎变更。
- 需要破坏性调整时必须写迁移说明。

### 3.2 地形核心和表现层解耦

地形核心输出的是“这里是什么”和“这里适合做什么”，不是最终资产。

例如：

- 地形核心输出 `TerrainScatterKind.ResourceNode`。
- 资源系统决定具体实例化哪种矿石、草药、宝箱或拾取物。
- 地形核心输出 `TerrainLandmarkKind.Town`。
- 资产系统或关卡系统决定使用哪个城镇 prefab。

### 3.3 查询接口优先于内部结构暴露

其他模块不应到处直接组合内部类调用。应提供稳定查询入口。

推荐方向：

- `TerrainWorld` 作为运行时主入口。
- `TerrainWorldFieldSampler` 作为纯函数采样入口。
- `TerrainWorldPlan` 作为只读规划数据。
- anchor group/meta 作为 Godot 场景树解耦入口。

### 3.4 验证工具保护 API

任何影响生成、规划、流送、anchor、Native sampler 的改动，都应有验证工具覆盖。

下一阶段不是只看画面，而是通过 CLI 形成可重复验收：

- build 过。
- seed 过。
- plan 过。
- tile 过。
- anchor 过。
- Native parity 过。

## 4. 当前推荐稳定 API 分层

### 4.1 一级稳定 API

一级稳定 API 是其他游戏模块可以直接依赖的接口。后续应重点保护兼容性。

| API                                                       | 文件                                                          | 用途                            |
| --------------------------------------------------------- | ------------------------------------------------------------- | ------------------------------- |
| `TerrainSettings`                                         | `dao/Scripts/Terrain/TerrainSettings.cs`                      | Godot 编辑器暴露地形配置        |
| `TerrainGenerationProfile`                                | `dao/Scripts/Terrain/TerrainSettings.cs`                      | 运行时不可变配置快照            |
| `TerrainWorld.Profile`                                    | `dao/Scripts/Terrain/Streaming/TerrainWorld.cs`               | 当前地形 profile                |
| `TerrainWorld.WorldPlan`                                  | `dao/Scripts/Terrain/Streaming/TerrainWorld.cs`               | 当前开放世界 plan               |
| `TerrainWorld.IsOpenWorldPlanGenerationPending`           | `dao/Scripts/Terrain/Streaming/TerrainWorld.cs`               | plan 异步生成状态               |
| `TerrainWorld.SetFocus(Node3D)`                           | `dao/Scripts/Terrain/Streaming/TerrainWorld.cs`               | 设置流送中心                    |
| `TerrainWorld.SetWorldPlan(TerrainWorldPlan?)`            | `dao/Scripts/Terrain/Streaming/TerrainWorld.cs`               | 应用或清空 plan                 |
| `TerrainWorld.Regenerate()`                               | `dao/Scripts/Terrain/Streaming/TerrainWorld.cs`               | 重新生成地形运行时状态          |
| `TerrainWorld.GenerateOpenWorldPlan(bool)`                | `dao/Scripts/Terrain/Streaming/TerrainWorld.cs`               | 生成并可选应用 plan             |
| `TerrainWorld.CreateRuntimeOpenWorldPlan(...)`            | `dao/Scripts/Terrain/Streaming/TerrainWorld.cs`               | 同步生成运行时 plan             |
| `TerrainWorld.CreateRuntimeOpenWorldPlanAsync(...)`       | `dao/Scripts/Terrain/Streaming/TerrainWorld.cs`               | 异步生成运行时 plan             |
| `TerrainWorldFieldSampler.Sample(...)`                    | `dao/Scripts/Terrain/Generation/TerrainWorldField.cs`         | 任意位置完整地形语义            |
| `TerrainSampler.SampleWithSlope(...)`                     | `dao/Scripts/Terrain/Generation/TerrainSampler.cs`            | 带坡度和颜色的地表采样          |
| `TerrainSampler.NormalAt(...)`                            | `dao/Scripts/Terrain/Generation/TerrainSampler.cs`            | 任意位置法线                    |
| `TerrainWorldPlanner.CreateOpenWorldPlan(...)`            | `dao/Scripts/Terrain/Generation/TerrainWorldPlanner.cs`       | 创建开放世界 plan               |
| `TerrainWorldPlanner.ValidateOpenWorldPlanning(...)`      | `dao/Scripts/Terrain/Generation/TerrainWorldPlanner.cs`       | 验证规划质量                    |
| `TerrainQualityAnalyzer.ValidateOpenWorldDefault(...)`    | `dao/Scripts/Terrain/Generation/TerrainQualityAnalyzer.cs`    | 验证地形质量                    |
| `TerrainExperienceAnalyzer.ValidateOpenWorldDefault(...)` | `dao/Scripts/Terrain/Generation/TerrainExperienceAnalyzer.cs` | 验证体验指标                    |
| `TerrainWorldPlanOverlay.ApplyPlan(...)`                  | `dao/Scripts/Terrain/Runtime/TerrainWorldPlanOverlay.cs`      | 当前阶段生成可视化和玩法 anchor |
| `TerrainMapExporter.SaveMap(...)`                         | `dao/Scripts/Terrain/Generation/TerrainMapExporter.cs`        | 导出地形图层                    |
| `TerrainWorldPlanExporter.SaveOpenWorldArtifacts(...)`    | `dao/Scripts/Terrain/Generation/TerrainWorldPlanExporter.cs`  | 导出 plan 地图和报告            |

### 4.2 二级半稳定 API

二级 API 可供地形工具、编辑器工具、验证工具使用，但普通玩法模块不应直接依赖。

| API                                                               | 文件                                                            | 用途                    |
| ----------------------------------------------------------------- | --------------------------------------------------------------- | ----------------------- |
| `TerrainTileBuilder.Build(...)`                                   | `dao/Scripts/Terrain/Generation/TerrainTileBuilder.cs`          | 构建单块 tile           |
| `TerrainTileBuilder.ShouldUseNativeSamplerForTileGeneration(...)` | `dao/Scripts/Terrain/Generation/TerrainTileBuilder.cs`          | 查询 Native 选择策略    |
| `TerrainMeshBuilder.CreateMesh(...)`                              | `dao/Scripts/Terrain/Rendering/TerrainMeshBuilder.cs`           | tile 数据转 mesh        |
| `TerrainMeshBuilder.CreateWaterMesh(...)`                         | `dao/Scripts/Terrain/Rendering/TerrainMeshBuilder.cs`           | tile 水面转 mesh        |
| `TerrainRouteCorridorIndex.FromPlan(...)`                         | `dao/Scripts/Terrain/Generation/TerrainRouteCorridorIndex.cs`   | route corridor 索引     |
| `TerrainRouteCorridorIndex.Sample(...)`                           | `dao/Scripts/Terrain/Generation/TerrainRouteCorridorIndex.cs`   | route corridor 影响采样 |
| `TerrainPointOfInterestIndex.FromPlan(...)`                       | `dao/Scripts/Terrain/Generation/TerrainPointOfInterestIndex.cs` | POI footprint 索引      |
| `TerrainPointOfInterestIndex.GetPoints(...)`                      | `dao/Scripts/Terrain/Generation/TerrainPointOfInterestIndex.cs` | 查询 tile 关联 POI      |
| `NativeTerrainBridge`                                             | `dao/Scripts/Terrain/Generation/NativeTerrainBridge.cs`         | Native sampler 桥接     |

### 4.3 内部实现，不建议外部依赖

以下内容可以继续重构和优化，不建议承诺外部兼容：

- `TerrainTileBuilder.SurfaceScatter.cs` 内部概率和评分。
- `TerrainTileBuilder.ScenicLandmarks.cs` 内部评分。
- `TerrainTileBuilder.Settlements.cs` 内部 layout 规则。
- `TerrainTileBuilder.RouteScatter.cs` marker 和 bridge 的具体生成规则。
- `TerrainChunk` 内部 primitive mesh 构建。
- tile LRU cache 结构。
- tile job 队列细节。
- Native sampler 校准实现。

## 5. 建议新增的对外查询接口

当前系统已有足够底层能力，但对其他模块来说入口略分散。建议在 `TerrainWorld` 增加一组轻量 facade 方法。

### 5.1 建议新增 TerrainWorld 查询方法

目标文件：

- `dao/Scripts/Terrain/Streaming/TerrainWorld.cs`

建议新增：

```csharp
public TerrainWorldField SampleField(Vector2 world);
public TerrainSample SampleSurface(Vector2 world, float spacing = 4.0f);
public Vector3 SurfacePositionAt(Vector2 world, float heightOffset = 0.0f);
public bool TryGetWorldPlan(out TerrainWorldPlan plan);
public ReadOnlySpan<TerrainWorldPointOfInterest> GetPointsOfInterest();
public ReadOnlySpan<TerrainWorldRoute> GetRoutes();
public bool IsTraversable(Vector2 world, float minTraversability = 0.45f);
public bool IsAboveWater(Vector2 world, float margin = 0.0f);
```

如果 Godot/C# 版本或 API 对 `ReadOnlySpan` 暴露有约束，也可先使用数组：

```csharp
public TerrainWorldPointOfInterest[] GetPointsOfInterest();
public TerrainWorldRoute[] GetRoutes();
```

### 5.2 查询方法行为约定

`SampleField(Vector2 world)`：

- 使用当前 `Profile`。
- 不要求 `WorldPlan` 已经生成。
- 纯采样，不触发 tile 生成。

`SampleSurface(Vector2 world, float spacing)`：

- 返回高度、坡度、颜色等表面数据。
- 用于角色落点、特效贴地、音频区域、任务候选点。

`SurfacePositionAt(Vector2 world, float heightOffset)`：

- 返回 Godot 3D 坐标 `Vector3(world.X, sampledHeight + offset, world.Y)`。
- 对其他模块减少 XZ/XY 坐标转换错误。

`TryGetWorldPlan(out TerrainWorldPlan plan)`：

- 如果 `WorldPlan` 为空，返回 false。
- 不隐式同步生成 plan，避免卡帧。

`GetPointsOfInterest()`：

- 如果 plan 未就绪，返回空集合。
- 不返回可修改内部数组，或文档说明调用方不得修改。

`GetRoutes()`：

- 如果 plan 未就绪，返回空集合。
- 路线 waypoint 由上层模块只读使用。

`IsTraversable(...)`：

- 使用 `TerrainWorldField.Traversability`。
- 默认阈值应保守。

`IsAboveWater(...)`：

- 使用 `Height` 和 `SeaLevel`。
- 不处理动态水体或特殊玩法水体。

## 6. Gameplay Anchor 稳定计划

### 6.1 当前状态

当前 `TerrainWorldPlanOverlay` 同时承担两类职责：

- Debug 可视化：
  - POI marker。
  - route ribbon。
- Runtime 对接：
  - `TerrainWorldPointOfInterestAnchor`。
  - `TerrainWorldRouteAnchor`。
  - group/meta。

短期可以继续使用，但中期建议拆开。

### 6.2 建议拆分

新增或重构为：

| 类型                                | 职责                        |
| ----------------------------------- | --------------------------- |
| `TerrainWorldPlanOverlay`           | 只负责 debug 可视化         |
| `TerrainWorldAnchorBuilder`         | 只负责生成 gameplay anchors |
| `TerrainWorldPointOfInterestAnchor` | 稳定 POI 对接节点           |
| `TerrainWorldRouteAnchor`           | 稳定 route 对接节点         |

建议新增文件：

- `dao/Scripts/Terrain/Runtime/TerrainWorldAnchorBuilder.cs`

建议 API：

```csharp
[GlobalClass]
public partial class TerrainWorldAnchorBuilder : Node3D
{
    [Export] public bool BuildOnReady { get; set; } = false;
    [Export] public NodePath TerrainWorldPath { get; set; } = new();
    [Export(PropertyHint.Range, "0,80,1")] public float AnchorHeightOffset { get; set; } = 3.0f;

    public TerrainWorldPlan? Plan { get; private set; }

    public void ApplyPlan(TerrainWorldPlan plan, TerrainGenerationProfile profile);
    public void ClearAnchors();
}
```

### 6.3 稳定 group 名称

POI：

```text
terrain_poi
```

Route：

```text
terrain_route
```

后续如果新增类型，应追加新 group，不修改旧 group：

```text
terrain_region
terrain_water_body
terrain_spawn_zone
```

### 6.4 稳定 POI meta keys

必须稳定：

```text
terrain_poi_id
terrain_poi_kind
terrain_poi_visual
terrain_poi_gameplay_tag
terrain_poi_score
terrain_poi_scenic
terrain_poi_traversability
terrain_poi_settlement_tier
terrain_poi_landscape
terrain_poi_interaction_radius
terrain_poi_encounter_budget
```

可后续追加：

```text
terrain_poi_biome
terrain_poi_height
terrain_poi_resource_potential
terrain_poi_hazard_potential
terrain_poi_world_x
terrain_poi_world_z
```

### 6.5 稳定 Route meta keys

必须稳定：

```text
terrain_route_kind
terrain_route_from
terrain_route_to
terrain_route_cost
terrain_route_scenic
terrain_route_traversability
```

可后续追加：

```text
terrain_route_waypoint_count
terrain_route_length
terrain_route_midpoint_x
terrain_route_midpoint_z
```

## 7. 数据契约版本化计划

建议新增：

- `dao/Scripts/Terrain/TerrainApiVersion.cs`

初始内容：

```csharp
namespace Dao.Terrain;

public static class TerrainApiVersion
{
    public const int Major = 1;
    public const int Minor = 0;
    public const int Patch = 0;
    public const string Contract = "terrain-api-v1";
}
```

版本规则：

- `Patch`：内部 bugfix，不影响公开 API。
- `Minor`：只追加兼容字段、方法、enum、meta key。
- `Major`：破坏性 API 变更。

建议在导出的 plan report 中写入：

```text
Terrain API Contract: terrain-api-v1
Terrain API Version: 1.0.0
```

## 8. 其他模块对接方案

### 8.1 任务系统

任务系统应该使用：

- `TerrainWorld.WorldPlan`
- `TerrainWorldPointOfInterestAnchor`
- group `terrain_poi`
- POI meta keys。

推荐接入方式：

1. 等待 `TerrainWorld.WorldPlan` 非空，或扫描 `terrain_poi` group。
2. 按 `terrain_poi_kind` 过滤任务地点。
3. 按 `terrain_poi_score`、`terrain_poi_scenic` 排序。
4. 任务系统自己决定任务类型和奖励。

示例策略：

- `Vista`：观景、摄影、地图揭示。
- `AncientSite`：遗迹探索、解谜、剧情线索。
- `ResourceGrove`：采集、护送、争夺。
- `SettlementCandidate` / `Village` / `Town`：任务 hub。
- `Oasis`：补给、贸易、危险区域边缘避难。

不建议任务系统依赖：

- tile scatter 的具体实例数量。
- landmark primitive mesh。
- 内部评分函数。

### 8.2 AI 和遭遇系统

AI/遭遇系统应该使用：

- `TerrainWorldField.EncounterPotential`
- `TerrainWorldField.HazardPotential`
- `TerrainWorldField.ResourcePotential`
- `TerrainWorldField.Traversability`
- `TerrainWorldRoute.Waypoints`
- `TerrainWorldRoute.Kind`

推荐策略：

- 普通巡逻优先使用路线 waypoints。
- 高风险遭遇使用 `HazardPotential` 高、`Traversability` 中等的区域。
- 资源争夺遭遇使用 `ResourcePotential` 和 `EncounterPotential` 同时较高的区域。
- 避免在 `Height < SeaLevel` 或 `Traversability` 过低处生成普通地面单位。

不建议 AI 系统依赖：

- 地形 chunk 当前是否已加载。
- `TerrainChunk` 内部节点结构。
- 当前 primitive scatter mesh。

### 8.3 资源系统

资源系统应该使用：

- `TerrainWorldField.ResourcePotential`
- `TerrainBiomeKind`
- `TerrainLandscapeKind`
- `TerrainPointOfInterestKind.ResourceGrove`
- `TerrainScatterKind.ResourceNode` 作为占位点语义。

推荐策略：

- 地形系统给候选区域和权重。
- 资源系统根据设计表选择真实资源 prefab。
- 资源系统负责刷新、采集状态、掉落、存档。

不建议地形系统负责：

- 资源库存。
- 掉落表。
- 玩家采集状态。
- 资源重生计时。

### 8.4 导航系统

导航系统应该使用：

- `TerrainWorldField.Traversability`
- `TerrainSample.Slope`
- `TerrainWorldRoute.Waypoints`
- `TerrainRouteKind`

推荐策略：

- 地形系统输出高层路线和通行成本。
- 导航系统自己生成 navmesh、navigation graph 或 flow field。
- 路线 waypoints 可作为长距离路径 hint。

不建议地形系统负责：

- 角色级 A\*。
- navmesh bake。
- 动态避障。
- 载具物理路径。

### 8.5 音频和天气系统

音频/天气系统应该使用：

- `TerrainBiomeKind`
- `TerrainLandscapeKind`
- `Moisture`
- `Temperature`
- `Height`
- `Exposure`

推荐策略：

- 根据 biome 切换环境音。
- 根据 landscape 增加峡谷、森林、海岸、雪地差异。
- 根据 exposure 调整风声。
- 根据 moisture/temperature 作为天气权重输入。

不建议地形系统负责：

- 天气状态机。
- 音频播放。
- 音频混音。
- 粒子系统状态。

### 8.6 存档系统

存档系统应该保存：

- seed。
- terrain profile 参数。
- plan 版本或导出的 plan 数据。
- 玩家改造数据。
- 任务、资源、NPC 等模块自己的运行时状态。

地形系统建议提供：

- profile 快照。
- plan 导出/导入。
- API 版本号。

不建议地形系统直接负责：

- 玩家存档文件。
- 任务状态。
- 采集状态。
- 已破坏对象。

## 9. 阶段路线图

### 阶段 0：文档和边界冻结

目标：让团队明确地形系统职责和公开 API。

交付物：

- `TERRAIN_SYSTEM_API_STABILIZATION_PLAN.md`
- 后续建议新增 `TERRAIN_API_CONTRACT.md`

任务：

- 列出一级稳定 API。
- 列出二级半稳定 API。
- 列出内部实现。
- 明确系统不负责最终资产、任务、AI、导航、存档。

验收标准：

- 其他模块能根据文档知道应该调用哪些接口。
- 文档中明确哪些东西不能依赖。

### 阶段 1：TerrainWorld Facade 查询接口

目标：让其他模块优先通过 `TerrainWorld` 获取运行时地形数据。

建议改动：

- 在 `TerrainWorld` 增加：
  - `SampleField`
  - `SampleSurface`
  - `SurfacePositionAt`
  - `TryGetWorldPlan`
  - `GetPointsOfInterest`
  - `GetRoutes`
  - `IsTraversable`
  - `IsAboveWater`

建议测试：

- 创建 profile 后调用所有查询方法。
- plan 未生成时 `TryGetWorldPlan` 返回 false。
- plan 生成后 POI/routes 非空。
- `SurfacePositionAt` 坐标轴符合 Godot X/Y/Z 规则。

验收标准：

- 普通玩法模块不需要直接知道 `TerrainWorldFieldSampler.Sample(world, profile)` 的组合细节。
- 查询方法不触发重型同步 plan 生成。

### 阶段 2：Gameplay Anchor Builder 拆分

目标：把 debug 可视化和 runtime anchor 输出解耦。

建议改动：

- 新增 `TerrainWorldAnchorBuilder`。
- `TerrainWorldPlanOverlay` 保留可视化职责。
- anchor 生成逻辑迁出 overlay，或 overlay 复用 builder。

建议测试：

- `ApplyPlan` 后 POI anchors 数量等于 plan POI 数量。
- route anchors 数量等于 plan route 数量。
- 所有 POI anchor 包含 `terrain_poi` group。
- 所有 route anchor 包含 `terrain_route` group。
- 必须 meta key 全部存在。

验收标准：

- 任务/AI/资源模块可以只使用 anchor builder，不打开 debug overlay。
- 可视化开关不会影响 gameplay anchor 输出。

### 阶段 3：API 版本化和契约测试

目标：建立长期兼容规则。

建议改动：

- 新增 `TerrainApiVersion`。
- 在 plan report 中输出 API 版本。
- validation 工具增加 contract smoke。

建议测试：

- 检查 `TerrainApiVersion.Contract == "terrain-api-v1"`。
- 检查 enum 覆盖。
- 检查 POI archetype 覆盖。
- 检查 group/meta key 覆盖。

验收标准：

- 破坏 API 前必须显式升级 major 版本。
- CI 可以发现 anchor/meta 破坏。

### 阶段 4：Plan 持久化

目标：让地形 plan 可导出、可审核、可复用。

建议新增：

- `TerrainWorldPlanSerializer`

候选文件：

- `dao/Scripts/Terrain/Generation/TerrainWorldPlanSerializer.cs`

建议 API：

```csharp
public static class TerrainWorldPlanSerializer
{
    public static string ToJson(TerrainWorldPlan plan);
    public static TerrainWorldPlan FromJson(string json);
    public static Error SaveJson(TerrainWorldPlan plan, string outputPath);
    public static bool TryLoadJson(string inputPath, out TerrainWorldPlan plan);
}
```

注意：

- Godot `Vector2` 和 `Color` 序列化要明确格式。
- 需要写入 API version。
- 初期可以只支持当前版本读取。

验收标准：

- 同一个 plan 导出再导入后，POI 数、route 数、关键 report 数据一致。
- 导入 plan 可用于 `TerrainWorld.SetWorldPlan()`。

### 阶段 5：验证工具升级

目标：让 API 稳定性进入常规回归。

建议给 `tools/TerrainValidation` 增加：

- API contract smoke。
- anchor contract smoke。
- facade query smoke。
- plan serialization smoke。
- multi-seed 默认 CI 模式。

建议命令：

```powershell
dotnet run --project tools\TerrainValidation\TerrainValidation.csproj -- --seed-count 10
```

周期性命令：

```powershell
dotnet run --project tools\TerrainValidation\TerrainValidation.csproj -- --seed-count 10 --native-smoke --benchmark-tiles
```

验收标准：

- 默认 seed 通过。
- 多 seed 通过。
- anchor/meta 通过。
- Native parity 通过。
- benchmark 不低于当前阈值。

### 阶段 6：资产映射预留

目标：给后续手动资产接入留干净扩展点，但不把资产系统塞进地形核心。

建议新增一个轻量映射资源，而不是现在实现完整资产管线：

```csharp
[GlobalClass]
public partial class TerrainAssetMapping : Resource
{
    // 后续映射 TerrainScatterKind / TerrainLandmarkKind 到 PackedScene 或 Mesh。
}
```

短期只规划，不急于实现。

验收标准：

- 地形核心仍可只输出 scatter/landmark kind。
- 没有真实资产时，占位材质和 primitive mesh 继续可运行。
- 有真实资产时，外部系统可替换表现。

## 10. 优先级排序

### P0：必须优先

- 确定 API 分层和契约文档。
- 增加 `TerrainWorld` facade 查询方法。
- 固定 group/meta 命名。
- 增加 API/anchor contract smoke。
- 保持 `TerrainValidation` 全部通过。

### P1：应尽快做

- 拆分 `TerrainWorldAnchorBuilder`。
- 增加 `TerrainApiVersion`。
- 增加 plan JSON 保存/读取。
- 增加 CI 多 seed 校验。
- 增加 Native parity 常规测试。

### P2：后续做

- 资产映射资源。
- 导航成本图导出。
- 更多地图图层导出。
- world origin / 大世界坐标策略。
- tile 性能 benchmark 仪表盘。

### 暂不做

- 最终美术材质。
- 大规模真实植被。
- 复杂 prefab 资产系统。
- 任务逻辑。
- NPC/怪物刷新逻辑。
- navmesh 烘焙。
- 存档系统。

## 11. 建议验收清单

每次地形系统核心改动后，至少检查：

- `dotnet build dao\dao.csproj` 成功。
- `tools/TerrainValidation` 默认 seed PASS。
- `TerrainWorld` 可流送 tile。
- `WorldPlan` 可生成。
- POI 和 route 数量满足默认门槛。
- route corridor 能影响 tile。
- POI footprint 能影响 tile。
- gameplay scatter 有输出。
- biome scatter 有输出。
- scenic landmark 有输出。
- artifact 导出成功。
- runtime smoke 成功。
- anchor group/meta 完整。

发布或阶段审查前，建议检查：

- 10 个以上 seed 通过。
- Native parity 通过。
- benchmark 通过。
- plan 导出/导入通过。
- API contract 文档更新。

## 12. 计划结论

下一阶段的正确方向是收敛，而不是扩张。

地形系统应成为一个稳定基础设施：

- 它生成地形。
- 它流送地形。
- 它解释地形。
- 它暴露可被其他模块依赖的 plan、field、anchor 和 map artifact。
- 它用验证工具证明自己没有回归。

资产、任务、AI、资源、导航、天气和存档都应该作为外部模块接入。地形系统只需要提供清晰、稳定、确定性的输入数据。

推荐执行顺序：

1. 先冻结 API 契约。
2. 再补 `TerrainWorld` 查询 facade。
3. 再拆 gameplay anchor builder。
4. 再补版本化和 contract smoke。
5. 再做 plan 持久化。
6. 最后预留资产映射层。

按这个方向推进，系统会从“功能很多的 AI 生成原型”转变为“其他模块可以放心依赖的开放世界地形底座”。
