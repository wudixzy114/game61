using System;
using Dao.Terrain;
using Dao.Terrain.Generation;
using Dao.Terrain.Runtime;
using Godot;

/// <summary>Defines fixed validation coverage tiers for CI and release gates.</summary>
internal readonly record struct TerrainValidationTierSpec(
    string Name,
    int SeedCount,
    bool SmokeAllSeeds,
    bool NativeSmoke,
    bool BenchmarkTiles,
    int BenchmarkTileCount)
{
    public bool IsCustom => string.Equals(Name, "custom", StringComparison.Ordinal);

    public static TerrainValidationTierSpec Custom { get; } = new(
        "custom",
        SeedCount: 1,
        SmokeAllSeeds: false,
        NativeSmoke: false,
        BenchmarkTiles: false,
        BenchmarkTileCount: 48);

    public static TerrainValidationTierSpec Pr { get; } = new(
        "pr",
        SeedCount: 1,
        SmokeAllSeeds: false,
        NativeSmoke: false,
        BenchmarkTiles: false,
        BenchmarkTileCount: 48);

    public static TerrainValidationTierSpec Nightly { get; } = new(
        "nightly",
        SeedCount: 10,
        SmokeAllSeeds: true,
        NativeSmoke: false,
        BenchmarkTiles: false,
        BenchmarkTileCount: 48);

    public static TerrainValidationTierSpec Release { get; } = new(
        "release",
        SeedCount: 25,
        SmokeAllSeeds: true,
        NativeSmoke: true,
        BenchmarkTiles: true,
        BenchmarkTileCount: 48);
}

/// <summary>Aggregates planning, quality, experience, and archetype gate results for a single seed.</summary>
internal readonly record struct TerrainValidationResult(
    int Seed,
    TerrainWorldPlan Plan,
    TerrainQualityGateResult QualityGate,
    TerrainWorldPlanningGateResult PlanningGate,
    TerrainExperienceGateResult ExperienceGate,
    TerrainPointOfInterestArchetypeValidationReport ArchetypeGate)
{
    public bool Passed => QualityGate.Passed && PlanningGate.Passed && ExperienceGate.Passed && ArchetypeGate.Passed;
}

/// <summary>Reports whether route corridors produce measurable height/color changes on a sampled tile.</summary>
internal readonly record struct TerrainRouteCorridorSmokeReport(
    bool Passed,
    int Seed,
    TerrainTileCoord Coord,
    int SegmentCount,
    float MaxHeightDelta,
    float MaxColorDelta,
    int InfluencedVertexCount,
    bool SegmentSnapshotIsolated,
    string Reason);

/// <summary>Reports whether corridor-driven road markers and bridge spans materialize across many tiles.</summary>
internal readonly record struct TerrainRouteScatterSmokeReport(
    bool Passed,
    int CandidateTileCount,
    int SampledTileCount,
    int RoadMarkerCount,
    int BridgeSpanCount,
    int RouteLandmarkCount,
    string Reason);

/// <summary>Reports whether planned POIs materialize as landmarks and settlement interior scatter on generated tiles.</summary>
internal readonly record struct TerrainPoiTileSmokeReport(
    bool Passed,
    int ExpectedPointCount,
    int MaterializedPointCount,
    int TileCount,
    int DistinctLandmarkKinds,
    int DistinctScatterLandmarkKinds,
    int VillageLandmarkCount,
    int TownLandmarkCount,
    int OasisHubLandmarkCount,
    int VillageScatterCount,
    int TownScatterCount,
    int OasisHubScatterCount,
    int SettlementLandmarkCount,
    int ExpectedSettlementPointCount,
    int SettlementInteriorScatterCount,
    int VillageHouseScatterCount,
    int TownBlockScatterCount,
    int OasisCanopyScatterCount,
    int SettlementPlazaScatterCount,
    int OasisPoolScatterCount,
    int VillageWellScatterCount,
    int MarketStallScatterCount,
    int WatchTowerScatterCount,
    int OasisGardenScatterCount,
    int SettlementGatewayScatterCount,
    int SettlementServiceScatterCount,
    int FootprintInfluencedVertexCount,
    float FootprintMaxHeightDelta,
    float FootprintMaxColorDelta,
    int LayoutColorVertexCount,
    float LayoutMaxColorDelta,
    int LandmarkScatterCount,
    bool PoiIndexSnapshotIsolated,
    string Reason);

/// <summary>Reports whether POI footprints produce height and color changes on a sampled tile.</summary>
internal readonly record struct TerrainPoiFootprintSmokeReport(
    bool Passed,
    int InfluencedVertexCount,
    float MaxHeightDelta,
    float MaxColorDelta,
    int LayoutColorVertexCount,
    float LayoutMaxColorDelta);

/// <summary>Reports whether validation tier command-line contracts reject weakening overrides.</summary>
internal readonly record struct TerrainValidationCliContractSmokeReport(
    bool Passed,
    bool TierSelectionPassed,
    bool FixedTierConfigurationPassed,
    bool CustomFallbackPassed,
    bool SkipOverrideRejected,
    bool SeedOverrideRejected,
    bool WorldOverrideRejected,
    bool NativeOverrideRejected,
    bool SmokeAllSeedsOverrideRejected,
    bool BenchmarkOverrideRejected,
    bool UnknownTierRejected,
    string Reason);

/// <summary>Reports whether gameplay scatter (understory, resource nodes, hazard outcrops) materializes across tiles.</summary>
internal readonly record struct TerrainGameplayScatterSmokeReport(
    bool Passed,
    int CandidateTileCount,
    int SampledTileCount,
    int UnderstoryCount,
    int ResourceNodeCount,
    int HazardOutcropCount,
    int TotalGameplayScatterCount,
    string Reason);

/// <summary>Reports whether biome-specific scatter (grass, desert, wetland, snow, coast, alpine) materializes across diverse tiles.</summary>
internal readonly record struct TerrainBiomeScatterSmokeReport(
    bool Passed,
    int CandidateTileCount,
    int SampledTileCount,
    int RequiredCategoryCount,
    int MaterializedCategoryCount,
    int GrassTuftCount,
    int DesertShrubCount,
    int CactusClusterCount,
    int ReedClusterCount,
    int SnowClumpCount,
    int AlpinePineCount,
    int CoastalPalmCount,
    int DriftwoodCount,
    int MangroveRootCount,
    int LakeReedCount,
    int WaterLilyCount,
    int BiomeScatterCount,
    int LakeWaterCellCount,
    int RiverWaterCellCount,
    int OasisWaterCellCount,
    int TotalWaterCellCount,
    string Reason);

/// <summary>Reports whether scenic natural landmarks (waterfalls, dunes, monoliths, etc.) materialize across diverse terrain types.</summary>
internal readonly record struct TerrainScenicLandmarkSmokeReport(
    bool Passed,
    int CandidateTileCount,
    int SampledTileCount,
    int WaterfallCount,
    int DuneCrestCount,
    int DesertMonolithCount,
    int CanyonNeedleCount,
    int IceSpireCount,
    int NaturalArchCount,
    int GeothermalSpringCount,
    int GlacialRidgeCount,
    int DistinctGeneratedKindCount,
    int ScenicLandmarkCount,
    string Reason);

/// <summary>Reports whether open-world map and text artifacts are exported and contain meaningful terrain, routes, and POI overlays.</summary>
internal readonly record struct TerrainArtifactSmokeReport(
    bool Passed,
    string OutputDirectory,
    string JsonPath,
    string MapPath,
    string ReportPath,
    string TraversalCostMapPath,
    long JsonBytes,
    long MapBytes,
    long ReportBytes,
    long TraversalCostMapBytes,
    int ImageSize,
    int DistinctColorBuckets,
    int NonDarkSampleCount,
    int OverlayChangedPixels,
    float MaxOverlayColorDelta,
    bool MapRasterSnapshotIsolated,
    int TraversalCostColorBuckets,
    int TraversalCostBlockedSamples,
    int TraversalCostGridSize,
    int TraversalCostGridFiniteSamples,
    int TraversalCostGridBlockedSamples,
    bool TraversalCostGridSnapshotIsolated,
    bool ReportContainsRequiredSections,
    string Reason);

/// <summary>Reports whether terrain plans roundtrip through the stable JSON persistence schema and reject drift.</summary>
internal readonly record struct TerrainPlanJsonSmokeReport(
    bool Passed,
    bool MetadataPassed,
    bool SchemaShapePassed,
    bool StringLoadPassed,
    bool StringRoundtripMatches,
    bool FileLoadPassed,
    bool FileRoundtripMatches,
    bool SeedMismatchRejected,
    bool ProfileHashMismatchRejected,
    bool LegacyApiVersionAccepted,
    bool PreviousApiVersionAccepted,
    bool ApiVersion12Accepted,
    bool ApiVersion13Accepted,
    bool ApiVersion14Accepted,
    bool ApiVersion15Accepted,
    bool ApiVersion16Accepted,
    bool VersionDriftRejected,
    bool EnumNameDriftRejected,
    bool EnumValueDriftRejected,
    bool RoundtripIsolationPassed,
    bool SetWorldPlanPassed,
    int JsonBytes,
    long FileBytes,
    string Reason);

/// <summary>Reports whether public terrain enum names and numeric values match the stable external contract.</summary>
internal readonly record struct TerrainEnumContractSmokeReport(
    bool Passed,
    int CheckedTypeCount,
    int CheckedValueCount,
    string Reason);

/// <summary>Reports whether stable public terrain data carriers kept their property names and types.</summary>
internal readonly record struct TerrainPublicApiShapeSmokeReport(
    bool Passed,
    int CheckedTypeCount,
    int CheckedMemberCount,
    string Reason);

/// <summary>Reports whether generation profile hashing remains stable and field-sensitive.</summary>
internal readonly record struct TerrainProfileHashSmokeReport(
    bool Passed,
    string Hash,
    string ExpectedHash,
    bool FormatPassed,
    bool ExpectedHashPassed,
    bool FieldSensitivityPassed,
    int SensitiveFieldCount,
    int ExpectedFieldCount,
    string Reason);

/// <summary>Reports whether default planning, quality, and experience gate thresholds match the stable open-world contract.</summary>
internal readonly record struct TerrainThresholdContractSmokeReport(
    bool Passed,
    bool PlanningThresholdsPassed,
    bool QualityThresholdsPassed,
    bool ExperienceThresholdsPassed,
    bool BenchmarkThresholdsPassed,
    string Reason);

/// <summary>Reports whether public empty/default terrain sentinel states match the stable contract.</summary>
internal readonly record struct TerrainDefaultStateContractSmokeReport(
    bool Passed,
    bool CorridorNonePassed,
    bool CorridorIndexEmptyPassed,
    bool PointOfInterestIndexEmptyPassed,
    bool WaterSurfaceEmptyPassed,
    bool PlanSnapshotEmptyPassed,
    string Reason);

/// <summary>Reports whether gameplay-facing code keeps terrain implementation dependencies behind stable interfaces.</summary>
internal readonly record struct TerrainApiLayeringSmokeReport(
    bool Passed,
    int ScannedFileCount,
    int ViolationCount,
    string[] Violations,
    string Reason);

/// <summary>Reports whether the Godot terrain editor plugin scaffold exists and wires the expected dock/tool entry points.</summary>
internal readonly record struct TerrainEditorPluginSmokeReport(
    bool Passed,
    bool PluginConfigExists,
    bool PluginScriptExists,
    bool DockPanelExists,
    bool DefaultSettingsResourceExists,
    bool DefaultVisualCatalogResourceExists,
    bool MainSceneExists,
    bool PluginConfigWiresScript,
    bool PluginScriptWiresDock,
    bool DockPanelSupportsPreviewExportValidation,
    bool MainSceneWiresDefaultSettings,
    bool MainSceneWiresDefaultVisualCatalog,
    bool DemoScriptSupportsSettingsResource,
    bool DemoScriptSupportsVisualCatalogResource,
    bool DockPanelUsesDefaultSettingsResource,
    string Reason);

/// <summary>Reports whether terrain visual catalogs support mesh and scene assets with production metadata.</summary>
internal readonly record struct TerrainVisualCatalogSmokeReport(
    bool Passed,
    bool MeshEntryLookupPassed,
    bool SceneEntryLookupPassed,
    bool SceneOnlyEntriesAreAccepted,
    bool MissingEntryDetectionPassed,
    bool ValidationReportPassed,
    bool ReferencedResourceCollectionPassed,
    bool VisualEntryMetadataPassed,
    bool RuntimeScenePathPassed,
    string Reason);

/// <summary>Reports whether mutable terrain modification layers can be applied, queried, and persisted as save deltas.</summary>
internal readonly record struct TerrainModificationLayerSmokeReport(
    bool Passed,
    bool FieldOverlayPassed,
    bool AffectedTilesPassed,
    bool RouteStatePassed,
    bool ScatterMaterializationPassed,
    bool LandmarkMaterializationPassed,
    bool CollisionMaterializationPassed,
    bool JsonRoundtripPassed,
    bool FileRoundtripPassed,
    bool SnapshotIsolationPassed,
    bool DriftRejectionPassed,
    string ScatterDiagnostic,
    string Reason);

/// <summary>Stable method signature expected by public API shape validation.</summary>
internal readonly record struct PublicMethodContract(
    string Name,
    bool IsStatic,
    Type ReturnType,
    Type[] ParameterTypes);

/// <summary>Reports whether TerrainWorld's public runtime query facade matches the underlying samplers and exposes isolated plan snapshots.</summary>
internal readonly record struct TerrainRuntimeApiSmokeReport(
    bool Passed,
    bool SampleFieldMatchesSampler,
    bool BaseFieldMatchesSampler,
    bool OverlayFieldDeltaPassed,
    bool SampleSurfaceMatchesSampler,
    bool BaseSurfaceMatchesSampler,
    bool OverlaySurfaceDeltaPassed,
    bool SurfacePositionAxesPassed,
    bool BaseSurfacePositionAxesPassed,
    bool NoPlanTryGetPassed,
    bool NoPlanSnapshotPassed,
    bool EmptyPlanCollectionsPassed,
    bool PlanTryGetPassed,
    bool PlanSnapshotTryGetPassed,
    int PointOfInterestCount,
    int RouteCount,
    bool TraversabilityQueryPassed,
    bool BaseTraversabilityQueryPassed,
    bool AboveWaterQueryPassed,
    bool BaseAboveWaterQueryPassed,
    bool WaterStateQueryPassed,
    bool BaseWaterStateQueryPassed,
    bool GameplayTagsQueryPassed,
    bool BaseGameplayTagsQueryPassed,
    bool TraversalCostQueryPassed,
    bool BaseTraversalCostQueryPassed,
    bool PlacementCandidatesQueryPassed,
    bool NoPlanRoutePlacementPassed,
    bool NavigationGridPassed,
    bool NavigationTileGridPassed,
    bool ModifiedNavigationTileGridPassed,
    bool NavigationRegionQueryPassed,
    bool ModifiedNavigationRegionQueryPassed,
    bool NavigationWaypointGraphPassed,
    bool NavigationWaypointGraphIsolated,
    bool NoPlanRouteGraphPassed,
    bool NoPlanRoutePathPassed,
    bool ModifiedRouteGraphPassed,
    bool StreamingSnapshotPassed,
    bool ApiVersionPassed,
    bool DeterminismContractPassed,
    bool PerformanceContractPassed,
    bool IntegrationInterfacesPassed,
    bool SignalContractsPassed,
    bool PointQueryPassed,
    bool PointSummaryQueryPassed,
    bool GameplayTagRegionQueryPassed,
    bool RouteQueryPassed,
    bool RouteSummaryQueryPassed,
    bool RouteCorridorQueryPassed,
    bool RoutePlacementQueryPassed,
    bool RouteGraphSnapshotPassed,
    bool RoutePathQueryPassed,
    bool RouteGraphSnapshotIsolated,
    bool PointSnapshotIsolated,
    bool RouteSnapshotIsolated,
    bool WorldPlanSnapshotIsolated,
    string Reason);

/// <summary>Reports whether runtime gameplay anchors expose stable builder, group, meta, and snapshot contracts.</summary>
internal readonly record struct TerrainAnchorContractSmokeReport(
    bool Passed,
    bool PointCountPassed,
    bool RouteCountPassed,
    bool PoiGroupMetaPassed,
    bool RouteGroupMetaPassed,
    bool PoiContractNamesPassed,
    bool RouteContractNamesPassed,
    bool RouteWaypointSnapshotPassed,
    bool DescriptorRebuildPassed,
    bool BuilderPlanSnapshotPassed,
    bool OverlayPlanSnapshotPassed,
    bool MetaKeySnapshotPassed,
    bool AnchorNodeConstantsPassed,
    int PointAnchorCount,
    int RouteAnchorCount,
    string Reason);

/// <summary>Reports whether TerrainWorld's runtime plan entry creates indexed open-world content that materializes on tiles.</summary>
internal readonly record struct TerrainRuntimeWorldSmokeReport(
    bool Passed,
    int PointOfInterestCount,
    int RouteCount,
    int SampledTileCount,
    int MaterializedPointCount,
    int RoadMarkerCount,
    int BridgeSpanCount,
    int SettlementInteriorScatterCount,
    double AsyncPlanMilliseconds,
    bool AsyncPlanMatchesSync,
    double AsyncPlanCancellationMilliseconds,
    bool AsyncPlanCancellationPassed,
    bool HasCorridorIndex,
    bool HasPointIndex,
    bool QualityGatePassed,
    bool PlanningGatePassed,
    bool ExperienceGatePassed,
    bool ArchetypeGatePassed,
    bool ModificationQueryConsistencyPassed,
    bool ModificationInvalidationPassed,
    bool SetWorldPlanInvalidationPassed,
    string Reason);

/// <summary>Reports whether async runtime plan generation responds to cancellation.</summary>
internal readonly record struct TerrainRuntimeWorldCancellationReport(
    bool Passed,
    double ElapsedMilliseconds);

/// <summary>Pairs a world position with a score for sorting scatter candidate regions.</summary>
internal readonly record struct GameplayScatterRegionCandidate(
    Vector2 WorldPosition,
    float Score);

/// <summary>Reports whether native sampler height grids and tile output match the managed sampler within tolerance.</summary>
internal readonly record struct TerrainNativeSamplerSmokeReport(
    bool Passed,
    int Seed,
    string ProfileHash,
    bool Available,
    TerrainTileCoord Coord,
    int Resolution,
    int ComparedSampleCount,
    bool FieldGridAvailable,
    bool FieldGridContainsDerivedData,
    int ComparedFieldValueCount,
    float MaxFieldDelta,
    float AverageFieldDelta,
    int FieldClassificationMismatchCount,
    float MaxHeightDelta,
    float AverageHeightDelta,
    int TileVertexCount,
    float TileMaxHeightDelta,
    float TileMaxColorDelta,
    string Reason);

/// <summary>Reports how representative the benchmark tile set is across gameplay, routes, POIs, biomes, and landscapes.</summary>
internal readonly record struct TerrainTileBenchmarkCoverage(
    int DistinctBiomeKinds,
    int DistinctLandscapeKinds,
    int PointOfInterestTileCount,
    int RouteTileCount,
    int GameplayRichTileCount);

/// <summary>Reports managed vs native tile generation benchmark results including timing, allocations, parity, and sample coverage.</summary>
internal readonly record struct TerrainTileBenchmarkReport(
    bool Passed,
    int Seed,
    string ProfileHash,
    string ManagedBackendMode,
    string NativeBackendMode,
    bool NativeAvailable,
    bool NativeSelectedForTileGeneration,
    int RequestedTileCount,
    int MeasuredTileCount,
    TerrainTileBenchmarkCoverage Coverage,
    TerrainTileBenchmarkPass Managed,
    TerrainTileBenchmarkPass Native,
    int ParityTileCount,
    float MaxHeightDelta,
    float MaxColorDelta,
    double NativeSpeedup,
    int MeasurementPassCount,
    TerrainTileBenchmarkThresholds Thresholds,
    string Reason);

/// <summary>Thresholds for tile generation benchmark pass/fail criteria.</summary>
internal readonly record struct TerrainTileBenchmarkThresholds(
    double MaxManagedMillisecondsPerTile,
    double MaxNativeMillisecondsPerTile,
    double MaxManagedP50Milliseconds,
    double MaxManagedP95Milliseconds,
    double MaxManagedP99Milliseconds,
    double MaxNativeP50Milliseconds,
    double MaxNativeP95Milliseconds,
    double MaxNativeP99Milliseconds,
    double MaxAllocatedKilobytesPerTile,
    double MinNativeSpeedup,
    int MinParityTileCount,
    int MinBenchmarkBiomeKinds,
    int MinBenchmarkLandscapeKinds,
    int MinBenchmarkPointOfInterestTiles,
    int MinBenchmarkRouteTiles,
    int MinBenchmarkGameplayRichTiles,
    float MaxParityHeightDelta,
    float MaxParityColorDelta)
{
    public static TerrainTileBenchmarkThresholds Default { get; } = new(
        MaxManagedMillisecondsPerTile: TerrainPerformanceContract.MaxManagedMillisecondsPerTile,
        MaxNativeMillisecondsPerTile: TerrainPerformanceContract.MaxNativeMillisecondsPerTile,
        MaxManagedP50Milliseconds: TerrainPerformanceContract.MaxManagedP50Milliseconds,
        MaxManagedP95Milliseconds: TerrainPerformanceContract.MaxManagedP95Milliseconds,
        MaxManagedP99Milliseconds: TerrainPerformanceContract.MaxManagedP99Milliseconds,
        MaxNativeP50Milliseconds: TerrainPerformanceContract.MaxNativeP50Milliseconds,
        MaxNativeP95Milliseconds: TerrainPerformanceContract.MaxNativeP95Milliseconds,
        MaxNativeP99Milliseconds: TerrainPerformanceContract.MaxNativeP99Milliseconds,
        MaxAllocatedKilobytesPerTile: TerrainPerformanceContract.MaxAllocatedKilobytesPerTile,
        MinNativeSpeedup: TerrainPerformanceContract.MinNativeSpeedup,
        MinParityTileCount: TerrainPerformanceContract.MinParityTileCount,
        MinBenchmarkBiomeKinds: TerrainPerformanceContract.MinBenchmarkBiomeKinds,
        MinBenchmarkLandscapeKinds: TerrainPerformanceContract.MinBenchmarkLandscapeKinds,
        MinBenchmarkPointOfInterestTiles: TerrainPerformanceContract.MinBenchmarkPointOfInterestTiles,
        MinBenchmarkRouteTiles: TerrainPerformanceContract.MinBenchmarkRouteTiles,
        MinBenchmarkGameplayRichTiles: TerrainPerformanceContract.MinBenchmarkGameplayRichTiles,
        MaxParityHeightDelta: TerrainDeterminismContract.TileParityHeightEpsilon,
        MaxParityColorDelta: TerrainDeterminismContract.TileParityColorEpsilon);
}

/// <summary>Measured performance data for a single tile build pass (managed or native).</summary>
internal readonly record struct TerrainTileBenchmarkPass(
    int TileCount,
    long TotalVertices,
    long TotalIndices,
    long TotalScatter,
    long TotalLandmarks,
    double ElapsedMilliseconds,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    long AllocatedBytes,
    double HeightChecksum)
{
    public double TilesPerSecond => ElapsedMilliseconds <= 0.0 ? 0.0 : TileCount / (ElapsedMilliseconds / 1000.0);
    public double MillisecondsPerTile => TileCount == 0 ? 0.0 : ElapsedMilliseconds / TileCount;
    public double AllocatedMegabytes => AllocatedBytes / (1024.0 * 1024.0);
    public double AllocatedKilobytesPerTile => TileCount == 0 ? 0.0 : (AllocatedBytes / 1024.0) / TileCount;
}

/// <summary>Aggregates min/max/average statistics across multiple seed validation results.</summary>
internal sealed class TerrainValidationAggregate
{
    private int _count;
    private double _landRatioSum;
    private double _scenicRatioSum;
    private double _traversableLandRatioSum;
    private double _poiCountSum;
    private double _routeCountSum;
    private double _connectedPointRatioSum;
    private double _connectedSettlementRatioSum;
    private double _settlementRouteCountSum;
    private double _pointOfInterestWorldCoverageSum;
    private double _routeWorldCoverageSum;
    private double _routeScenicPotentialSum;
    private double _routeTraversabilitySum;
    private double _encounterPotentialSum;
    private double _routeRhythmScoreSum;
    private double _riskRewardBalanceSum;
    private double _villageCountSum;
    private double _townCountSum;
    private double _oasisHubCountSum;

    public float MinLandRatio { get; private set; } = float.PositiveInfinity;
    public float MaxLandRatio { get; private set; } = float.NegativeInfinity;
    public float MinScenicRatio { get; private set; } = float.PositiveInfinity;
    public float MaxScenicRatio { get; private set; } = float.NegativeInfinity;
    public float MinTraversableLandRatio { get; private set; } = float.PositiveInfinity;
    public float MaxTraversableLandRatio { get; private set; } = float.NegativeInfinity;
    public int MinPoiCount { get; private set; } = int.MaxValue;
    public int MaxPoiCount { get; private set; } = int.MinValue;
    public int MinRouteCount { get; private set; } = int.MaxValue;
    public int MaxRouteCount { get; private set; } = int.MinValue;
    public float MinConnectedPointRatio { get; private set; } = float.PositiveInfinity;
    public float MaxConnectedPointRatio { get; private set; } = float.NegativeInfinity;
    public float MinConnectedSettlementRatio { get; private set; } = float.PositiveInfinity;
    public float MaxConnectedSettlementRatio { get; private set; } = float.NegativeInfinity;
    public int MinSettlementRouteCount { get; private set; } = int.MaxValue;
    public int MaxSettlementRouteCount { get; private set; } = int.MinValue;
    public float MinPointOfInterestWorldCoverage { get; private set; } = float.PositiveInfinity;
    public float MaxPointOfInterestWorldCoverage { get; private set; } = float.NegativeInfinity;
    public float MinRouteWorldCoverage { get; private set; } = float.PositiveInfinity;
    public float MaxRouteWorldCoverage { get; private set; } = float.NegativeInfinity;
    public float MinRouteScenicPotential { get; private set; } = float.PositiveInfinity;
    public float MaxRouteScenicPotential { get; private set; } = float.NegativeInfinity;
    public float MinRouteTraversability { get; private set; } = float.PositiveInfinity;
    public float MaxRouteTraversability { get; private set; } = float.NegativeInfinity;
    public float MinEncounterPotential { get; private set; } = float.PositiveInfinity;
    public float MaxEncounterPotential { get; private set; } = float.NegativeInfinity;
    public float MinRouteRhythmScore { get; private set; } = float.PositiveInfinity;
    public float MaxRouteRhythmScore { get; private set; } = float.NegativeInfinity;
    public float MinRiskRewardBalance { get; private set; } = float.PositiveInfinity;
    public float MaxRiskRewardBalance { get; private set; } = float.NegativeInfinity;
    public int MinVillageCount { get; private set; } = int.MaxValue;
    public int MaxVillageCount { get; private set; } = int.MinValue;
    public int MinTownCount { get; private set; } = int.MaxValue;
    public int MaxTownCount { get; private set; } = int.MinValue;
    public int MinOasisHubCount { get; private set; } = int.MaxValue;
    public int MaxOasisHubCount { get; private set; } = int.MinValue;
    public int ExperienceFailureCount { get; private set; }
    public int ArchetypeFailureCount { get; private set; }

    public double AverageLandRatio => Average(_landRatioSum);
    public double AverageScenicRatio => Average(_scenicRatioSum);
    public double AverageTraversableLandRatio => Average(_traversableLandRatioSum);
    public double AveragePoiCount => Average(_poiCountSum);
    public double AverageRouteCount => Average(_routeCountSum);
    public double AverageConnectedPointRatio => Average(_connectedPointRatioSum);
    public double AverageConnectedSettlementRatio => Average(_connectedSettlementRatioSum);
    public double AverageSettlementRouteCount => Average(_settlementRouteCountSum);
    public double AveragePointOfInterestWorldCoverage => Average(_pointOfInterestWorldCoverageSum);
    public double AverageRouteWorldCoverage => Average(_routeWorldCoverageSum);
    public double AverageRouteScenicPotential => Average(_routeScenicPotentialSum);
    public double AverageRouteTraversability => Average(_routeTraversabilitySum);
    public double AverageEncounterPotential => Average(_encounterPotentialSum);
    public double AverageRouteRhythmScore => Average(_routeRhythmScoreSum);
    public double AverageRiskRewardBalance => Average(_riskRewardBalanceSum);
    public double AverageVillageCount => Average(_villageCountSum);
    public double AverageTownCount => Average(_townCountSum);
    public double AverageOasisHubCount => Average(_oasisHubCountSum);

    public void Add(TerrainValidationResult result)
    {
        TerrainQualityReport quality = result.QualityGate.Report;
        TerrainWorldPlanningReport planning = result.PlanningGate.Report;
        TerrainExperienceReport experience = result.ExperienceGate.Report;
        _count++;
        _landRatioSum += quality.LandRatio;
        _scenicRatioSum += quality.ScenicRatio;
        _traversableLandRatioSum += quality.TraversableLandRatio;
        _poiCountSum += planning.PointOfInterestCount;
        _routeCountSum += planning.RouteCount;
        _connectedPointRatioSum += planning.ConnectedPointRatio;
        _connectedSettlementRatioSum += planning.ConnectedSettlementRatio;
        _settlementRouteCountSum += planning.SettlementRouteCount;
        _pointOfInterestWorldCoverageSum += planning.PointOfInterestWorldCoverage;
        _routeWorldCoverageSum += planning.RouteWorldCoverage;
        _routeScenicPotentialSum += planning.AverageRouteScenicPotential;
        _routeTraversabilitySum += planning.AverageRouteTraversability;
        _encounterPotentialSum += experience.AverageEncounterPotential;
        _routeRhythmScoreSum += experience.RouteRhythmScore;
        _riskRewardBalanceSum += experience.RiskRewardBalance;
        _villageCountSum += planning.VillageCount;
        _townCountSum += planning.TownCount;
        _oasisHubCountSum += planning.OasisHubCount;

        MinLandRatio = Math.Min(MinLandRatio, quality.LandRatio);
        MaxLandRatio = Math.Max(MaxLandRatio, quality.LandRatio);
        MinScenicRatio = Math.Min(MinScenicRatio, quality.ScenicRatio);
        MaxScenicRatio = Math.Max(MaxScenicRatio, quality.ScenicRatio);
        MinTraversableLandRatio = Math.Min(MinTraversableLandRatio, quality.TraversableLandRatio);
        MaxTraversableLandRatio = Math.Max(MaxTraversableLandRatio, quality.TraversableLandRatio);
        MinPoiCount = Math.Min(MinPoiCount, planning.PointOfInterestCount);
        MaxPoiCount = Math.Max(MaxPoiCount, planning.PointOfInterestCount);
        MinRouteCount = Math.Min(MinRouteCount, planning.RouteCount);
        MaxRouteCount = Math.Max(MaxRouteCount, planning.RouteCount);
        MinConnectedPointRatio = Math.Min(MinConnectedPointRatio, planning.ConnectedPointRatio);
        MaxConnectedPointRatio = Math.Max(MaxConnectedPointRatio, planning.ConnectedPointRatio);
        MinConnectedSettlementRatio = Math.Min(MinConnectedSettlementRatio, planning.ConnectedSettlementRatio);
        MaxConnectedSettlementRatio = Math.Max(MaxConnectedSettlementRatio, planning.ConnectedSettlementRatio);
        MinSettlementRouteCount = Math.Min(MinSettlementRouteCount, planning.SettlementRouteCount);
        MaxSettlementRouteCount = Math.Max(MaxSettlementRouteCount, planning.SettlementRouteCount);
        MinPointOfInterestWorldCoverage = Math.Min(MinPointOfInterestWorldCoverage, planning.PointOfInterestWorldCoverage);
        MaxPointOfInterestWorldCoverage = Math.Max(MaxPointOfInterestWorldCoverage, planning.PointOfInterestWorldCoverage);
        MinRouteWorldCoverage = Math.Min(MinRouteWorldCoverage, planning.RouteWorldCoverage);
        MaxRouteWorldCoverage = Math.Max(MaxRouteWorldCoverage, planning.RouteWorldCoverage);
        MinRouteScenicPotential = Math.Min(MinRouteScenicPotential, planning.AverageRouteScenicPotential);
        MaxRouteScenicPotential = Math.Max(MaxRouteScenicPotential, planning.AverageRouteScenicPotential);
        MinRouteTraversability = Math.Min(MinRouteTraversability, planning.AverageRouteTraversability);
        MaxRouteTraversability = Math.Max(MaxRouteTraversability, planning.AverageRouteTraversability);
        MinEncounterPotential = Math.Min(MinEncounterPotential, experience.AverageEncounterPotential);
        MaxEncounterPotential = Math.Max(MaxEncounterPotential, experience.AverageEncounterPotential);
        MinRouteRhythmScore = Math.Min(MinRouteRhythmScore, experience.RouteRhythmScore);
        MaxRouteRhythmScore = Math.Max(MaxRouteRhythmScore, experience.RouteRhythmScore);
        MinRiskRewardBalance = Math.Min(MinRiskRewardBalance, experience.RiskRewardBalance);
        MaxRiskRewardBalance = Math.Max(MaxRiskRewardBalance, experience.RiskRewardBalance);
        MinVillageCount = Math.Min(MinVillageCount, planning.VillageCount);
        MaxVillageCount = Math.Max(MaxVillageCount, planning.VillageCount);
        MinTownCount = Math.Min(MinTownCount, planning.TownCount);
        MaxTownCount = Math.Max(MaxTownCount, planning.TownCount);
        MinOasisHubCount = Math.Min(MinOasisHubCount, planning.OasisHubCount);
        MaxOasisHubCount = Math.Max(MaxOasisHubCount, planning.OasisHubCount);

        if (!result.ExperienceGate.Passed)
        {
            ExperienceFailureCount++;
        }

        if (!result.ArchetypeGate.Passed)
        {
            ArchetypeFailureCount++;
        }
    }

    private double Average(double sum)
    {
        return _count == 0 ? 0.0 : sum / _count;
    }
}
