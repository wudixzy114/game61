# game61

> **Godot 4.6 上的程序化开放世界地形运行时**：用 C# 写了一套完整的"地形场采样 + 开放世界规划 + 流式 chunk 加载 + 渲染/碰撞分离 + 导航/资源放置/AI handoff"管线，配套一个 C++ GDExtension 做原生 sampler 加速和一个独立的 `TerrainValidation` CLI 做契约/性能校验。

## 项目定位 / 背景

game61 是 `Game58date` 的**技术下半场重构**：把 Stride 那边的整套"哲学模拟 + 英雄之旅"上层玩法拆掉，**只保留并升级地形 / 开放世界规划 / 流式加载这条底层世界链路**，并迁到 **Godot 4.6 + .NET 8** + **C++ GDExtension**。它要解决的是"如何在大世界里既能精确还原 gameplay-facing 的地形语义（高度 / biome / 河流 / 气候 / scenic / 危险 / 资源 / 遭遇），又能流式加载 / 按需构建 / 拆出可测试的 runtime API"。

技术上是一套**纯 C# 模块 + 单一 C++ 原生扩展**的组合：
- `Dao.Terrain.Generation` 用 `ProceduralNoise`（value noise + FBM + ridged + domain warping + terracing）合成 18 维 `TerrainWorldField`（height, continent, basin, shelf, mountains, broad_elevation, river, lake, moisture, temperature, scenic_potential, traversability, exposure, resource_potential, hazard_potential, encounter_potential, biome, landscape）
- `Dao.Terrain.Generation.NativeTerrainBridge` 通过 `NativeLibrary.TryLoad` 加载 `dao.windows.template_*.dll`，按版本优先用 v2 (`dao_native_sample_field_grid_v2`) 回退到 v1，再回退到 managed sampler——单点/网格/字段网格采样都做版本协商
- `Dao.Terrain.Streaming.TerrainWorld` 实现流式 chunk 加载 / 卸载 / 异步 plan 生成 / 缓存 / 信号分发（`PlanReady` / `ChunkLoaded` / `StreamingSnapshotChanged`）
- `Dao.Terrain.Generation.TerrainWorldPlanner` 跑 6 道关卡：采样规划网格 → 选 POI → 路径规划 → 质量分析 → 体验分析 → 汇总为 `TerrainWorldPlan`
- `Dao.Terrain.Generation.TerrainQualityAnalyzer` / `TerrainExperienceAnalyzer` 给出"open world plan PASS/FAIL"
- `Dao.Terrain.Runtime.*` 把规划快照渲染为可视化覆盖层（POI 标记 + 路线 ribbon + 地标锚点）
- `Dao.Terrain.Streaming.TerrainTileDataCache` 做 LRU + per-tile 锁定
- `tools/TerrainValidation` 是 ~9k 行的独立 CLI 工具，跑 5 关校验（契约 / 计划 JSON / 视觉目录 / 编辑器插件 / 基准 artifact）和多种 smoke（corridor / route scatter / POI tile / gameplay scatter / biome scatter / scenic landmark / plan JSON / enum contract / runtime API / anchor / runtime world），输出 PASS/FAIL 报告

**契约层**通过 `TERRAIN_RUNTIME_INTEGRATION_CONTRACT.md` 严格隔离：上游 quests / AI / resources / audio / map UI 只能依赖 `ITerrainQueryService` / `ITerrainPlanProvider` / `ITerrainStreamingDiagnostics` / `ITerrainPlacementService` / `ITerrainNavigationProvider` 五个稳定接口。

## 仓库结构

```
game61/
├── dao/                              # Godot 4.6 + .NET 8 主项目（Godot.NET.Sdk/4.6.3）
│   ├── dao.csproj                    # net8.0 / Nullable enable / dynamic loading
│   ├── dao.sln
│   ├── addons/terrain_editor/        # Godot 编辑器插件（plugin.gd + C# dock panel）
│   ├── Scripts/
│   │   ├── Demo/
│   │   │   ├── TerrainDemo.cs        # Demo 入口：相机 + TerrainWorld + 计划覆盖 + 导出
│   │   │   └── DemoFlyCamera.cs      # 自由飞行相机
│   │   └── Terrain/
│   │       ├── *.cs                  # Settings/Profile/Catalog 资源类
│   │       ├── Generation/           # 采样、规划、地图导出、关卡分析
│   │       │   ├── NativeTerrainBridge.cs
│   │       │   ├── TerrainWorldField.cs / .Semantics / .HeightModel / .Classification / .ShapeSampling
│   │       │   ├── TerrainWorldPlanner.cs (+ .Models / .GridSampling / .RoutePlanning / ...)
│   │       │   ├── TerrainWorldPlanExporter.cs (+ .Drawing / .IO / .Report)
│   │       │   ├── TerrainMapExporter.cs (+ .Coloring / .Png)
│   │       │   ├── TerrainQualityAnalyzer.cs / TerrainQualityAnalysisService.cs
│   │       │   ├── TerrainExperienceAnalyzer.cs / TerrainExperienceAnalysisService.cs
│   │       │   ├── TerrainSampler.cs / TerrainSample.cs
│   │       │   ├── ProceduralNoise.cs
│   │       │   ├── TerrainTileCoord.cs / TerrainTileData.cs / TerrainTileBuilder.* (40+ 文件)
│   │       │   └── TerrainFeatureData.cs / TerrainPlacementData.cs / ...
│   │       ├── Rendering/            # TerrainMaterialFactory / TerrainMeshBuilder
│   │       ├── Runtime/              # POI 锚点 / 路线锚点 / 计划覆盖 / 锚点契约
│   │       └── Streaming/            # TerrainWorld (主类) + 10 个 partial 拆分类
│   └── ...
├── gdextension/                      # C++ GDExtension（godot-cpp 作 submodule）
│   ├── src/dao_extension.h / .cpp   # 健康检查 + 高度采样 + 字段网格采样
│   ├── src/register_types.cpp       # GDREGISTER_CLASS(DaoExtension)
│   └── README.md                     # scons 构建说明
├── tools/TerrainValidation/          # ~9k 行独立 CLI 校验工具
│   ├── Program.cs                    # 顶层入口（参数解析 + smoke 编排 + 报告输出）
│   ├── ValidationContractChecks.cs
│   ├── ValidationApiLayeringChecks.cs
│   ├── ValidationBenchmarkChecks.cs / ValidationBenchmarkArtifactChecks / ...Writer
│   ├── ValidationPlanJsonChecks.cs
│   ├── ValidationVisualCatalogChecks.cs
│   ├── ValidationEditorPluginChecks.cs
│   ├── ValidationContracts.cs
│   ├── ValidationOutput.cs
│   └── ValidationRuntimeProbeHelpers.cs
├── .github/workflows/
│   ├── dotnet.yml                    # CI：dotnet build/test on main
│   └── gdextension.yml               # CI：scons 构建 C++ 扩展
├── global.json                       # SDK pin: 8.0.100 / rollForward latestFeature
├── .gitmodules                       # godot-cpp 子模块
├── TERRAIN_RUNTIME_INTEGRATION_CONTRACT.md   # 稳定运行时接口契约（5 大服务）
└── TERRAIN_SYSTEM_PHASE_AUDIT_2026_06_06.md # 阶段审计（~38KB）
```

## 技术栈

| 领域 | 选型 | 用途 |
|---|---|---|
| 引擎 | Godot 4.6（Godot.NET.Sdk/4.6.3） | 渲染、信号系统、GDExtension host |
| 运行时 | .NET 8（`net8.0`，`Nullable enable`） | 跨平台主机（win/linux/android） |
| 原生扩展 | C++ GDExtension（`godot-cpp` submodule，target `template_debug/release`） | 高频地形 sampler 加速 |
| 噪声 | 自研 `ProceduralNoise`（value + FBM + ridged + domain warping + terracing） | 18 维 TerrainWorldField |
| 序列化 | System.Text.Json | 计划 JSON / 报告 |
| 导航 handoff | `TerrainRouteGraphSnapshot` / `TerrainNavigationWaypointGraph` | 路由图（points / directed edges / collapsed waypoints） |
| 校验 | `tools/TerrainValidation` CLI | 契约 / 性能 / 烟雾 / 计划 JSON / 视觉目录 |
| CI | GitHub Actions | `dotnet.yml` + `gdextension.yml`（scons 构建） |

## 核心模块

**`TerrainGenerationProfile`（不可变地形生成参数）**
种子、chunk size、resolution、stream/collision radius、LOD、height scale、sea level、continent/mountain scale、各噪声权重、river strength / carve depth、terrace strength、skirt depth、并发 tile 任务数、是否启用原生 sampler、是否生成碰撞。`WithSeed` 衍生不可变副本，确保上层 plan 不会意外修改共享 profile。

**`TerrainWorldFieldSampler`（主采样器）**
18 维 `TerrainWorldField` 采样：高度、6 个形状项（continent/basin/shelf/mountains/broad_elevation）、2 个水文项（river/lake）、2 个气候项（moisture/temperature）、5 个 gameplay 字段（scenic/traversability/exposure/resource/hazard/encounter），2 个分类项（biome/landscape）。`NativeFieldGridStride = 18` 对齐 C++ 端 `dao_native_sample_field_grid_v2` 的 stride。提供 `Sample` / `SampleKnownHeight` / `SampleNativeFieldGrid` / `SampleBaseField`（绕开 modification overlay 的 deterministic 入口）。

**`NativeTerrainBridge`（原生 sampler 桥）**
通过 `NativeLibrary.TryLoad` 在 4 个候选路径中加载 `dao.windows.template_*.dll` / `dao.linux.template_*.so`；优先用 v2 入口 `dao_native_sample_field_grid_v2` / `dao_native_sample_height_grid_v2`，回退到 v1；`TrySampleHeightGrid` / `TrySampleFieldGrid` 支持预 pin 缓冲区 + `landBalanceOffset`。`SupportsFieldGridSampler` / `SupportsHeightGridSampler` / `SupportsDerivedFieldGridSampler` 三个能力属性精确报告当前链接的 sampler 版本。失败时完全 silent fallback 到 managed `TerrainWorldFieldSampler`。

**`TerrainWorldPlanner`（开放世界规划）**
6 段管线：① 采样 planning grid（含 region 分类、landscape 分类、scoring）→ ② 用 `TerrainPointOfInterestRuleCatalog` 选 POI（最多 36 个）→ ③ 用 `TerrainRouteRuleCatalog` 跑 A* / 走廊规划（最多 18 条路线）→ ④ `TerrainQualityAnalyzer` 出 quality report → ⑤ `TerrainWorldPlanner.AnalyzePlanning` 出 planning report → ⑥ `TerrainExperienceAnalyzer` 出 experience report。`TerrainWorldPlanningGateResult` / `TerrainQualityGateResult` / `TerrainExperienceGateResult` 三道闸门决定 plan 是否 PASS。

**`TerrainWorld`（流式运行时主类）**
`[GlobalClass] partial class TerrainWorld : Node3D, ITerrainQueryService, ITerrainPlanProvider, ITerrainStreamingDiagnostics, ITerrainPlacementService, ITerrainNavigationProvider` —— 5 个稳定接口都集中在这里实现。partial 拆分为 `TerrainWorld.cs` + `TerrainWorld.Facade.cs` + `TerrainWorld.StreamingRuntime.cs` + `TerrainWorld.Integration.cs` + `TerrainWorld.Modifications.cs` + `TerrainWorld.PlanLifecycle.Service.cs` + `TerrainWorld.PlanQueries.cs` + `TerrainWorld.PlanQueryService.cs` + `TerrainWorld.RuntimeLifecycle.Service.cs` + `TerrainWorld.SignalDispatch.Service.cs`。信号包括 `PlanReady` / `PlanCleared` / `ChunkLoaded(chunk_x, chunk_z, lod, has_collision)` / `ChunkUnloaded` / `StreamingSnapshotChanged`。`UpdateStreaming` 在不阻塞主线程的前提下驱动异步 tile job 调度、`TerrainModificationLayer` 维护玩家对地形的修改、`_desiredCoords` 维护当前需要加载的 chunk 集合。

**`TerrainWorldPlanExporter`（离线导出）**
按 `worldSize` 导出 4 份 artifact：JSON 计划、PNG 地图、PNG traversal cost、HTML/Markdown 报告。`TerrainMapExporter` 走 RGBA raster，支持 13 种 `TerrainMapLayer`（biome / height / river / moisture / temperature / scenic / traversability / exposure / resource / hazard / encounter / landscape / traversal cost）。

**`TerrainMaterialFactory` + `TerrainMeshBuilder`（渲染）**
渲染实体和水体平面由 `TerrainWorld` 在 plan ready 后创建；`Material _terrainMaterial` / `Material _waterMaterial` / `Material _localWaterMaterial` 是单例。

**`TerrainWorldPlanOverlay`（可视化覆盖层）**
`BuildGameplayAnchors = true` 时把 POI 转成可交互的 3D 锚点 + 路线 ribbon + 点标记，调试用。

**`tools/TerrainValidation`（独立 CLI）**
~9k 行，支持 `--tier`（`smoke` / `standard` / `release` / `custom`）+ 各种 `--skip-*-smoke` 开关 + `--seed-count` + `--smoke-all-seeds` + `--native-smoke` + `--benchmark-tiles`。`ValidationCliHelpers` 提供一致的 arg 解析；多个 `*Checks.cs` 跑独立的检查组；`ValidationBenchmarkArtifactWriter` 写离线基准 artifact（用于性能回归）。

**`gdextension/dao_extension.cpp`（C++ 原生 sampler）**
1500+ 行 C++，实现 `DaoExtension::health_check()` / `sample_height(...)` / `sample_height_grid(...)` / `sample_field_grid_v1` / `sample_field_grid_v2`。支持完整的世界配置参数（chunk size / height scale / sea level / continent scale / mountain scale / 各权重 / river strength / carve depth / terrace strength / land balance offset），并在内部分类 12 种 `LANDSCAPE_*`（ocean/coast/lowland/wetland/forest_basin/river_valley/canyon/highlands/mountain_massif/snowfield/vista_plateau/lake）+ 13 种 `BIOME_*`（ocean/coast/island/plains/grassland/desert/oasis/forest/wetland/hills/mountains/snowfield/lake）。

**`terrain_editor` 插件**
Godot Editor 的 dock panel（`plugin.gd` + `TerrainEditorDockPanel.cs`），让用户能在编辑器里直接调 profile / catalog 资源。

## 已完成 / 进行中

- ✅ 18 维 TerrainWorldField 采样（managed + C++ GDExtension + 版本协商 fallback）
- ✅ 6 段开放世界规划 + 三道闸门（planning/quality/experience）
- ✅ 流式 chunk 加载/卸载 + 异步 plan 生成 + 信号分发
- ✅ 5 大稳定运行时接口（Query/Plan/Diagnostics/Placement/Navigation）
- ✅ Plan 离线导出（JSON / PNG 地图 / PNG traversal cost / 报告）
- ✅ Godot Editor 插件（terrain_editor dock panel）
- ✅ 独立 CLI 校验工具（契约 / API 分层 / 性能基准 / plan JSON / 视觉目录 / 编辑器插件）
- ⏳ PlanExporter PNG 输出到磁盘后的 CI 自动化比对
- ⏳ 远景 / LOD 的 gameplay-side fallback 策略
- ⏳ 运行时多 profile 切换的语义（当前 profile 在 `_profile` 里 snapshot 化）
- ❌ 角色控制器 / 第一人称（依赖上层项目，本仓库是"地形子系统"）
- ❌ NPC / 剧情 / 征兆（拆出后由 game61 上层应用承载）

## 本地运行 / 构建

```powershell
# 拉 godot-cpp 子模块
git submodule update --init --recursive

# 编译 C++ GDExtension（MSVC 推荐；MinGW 也支持）
cd gdextension
scons target=template_debug platform=windows arch=x86_64 api_version=4.6
scons target=template_release platform=windows arch=x86_64 api_version=4.6

# 编译 C# 主项目
cd ../dao
dotnet build dao.csproj

# 在 Godot 4.6 中打开 dao/ 目录，按 F5 跑 TerrainDemo
# 烟雾校验
cd ../tools/TerrainValidation
dotnet run -- --tier smoke --native-smoke
```

> 健康检查（Godot 中）：
> ```gdscript
> DaoExtension.health_check()  # 应返回 "dao gdextension loaded"
> ```

## 状态

**v1.8.0 稳定地形运行时**。`TerrainApiVersion` 已经 1.0.0 ~ 1.8.0 全部纳入兼容；契约文档 `TERRAIN_RUNTIME_INTEGRATION_CONTRACT.md` 已经 5 大稳定接口全文公开。**可作为独立 Godot 插件发布**，但当前 CI 上没有 nightly build 验证。

## License

未指定 License。当前所有 commit 都来自 `xiezongyu`。
