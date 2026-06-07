using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Dao.Terrain;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Editor;

[Tool]
public partial class TerrainEditorDockPanel : VBoxContainer
{
    private const string DefaultTerrainSettingsPath = "res://Resources/Terrain/DefaultTerrainSettings.tres";
    private const string DefaultTerrainVisualCatalogPath = "res://Resources/Terrain/DefaultTerrainVisualCatalog.tres";
    private bool _uiBuilt;
    private LineEdit? _settingsPathEdit;
    private LineEdit? _visualCatalogPathEdit;
    private Label? _statusLabel;
    private Label? _profileLabel;
    private SpinBox? _previewSeedSpin;
    private SpinBox? _worldSizeSpin;
    private SpinBox? _imageSizeSpin;
    private LineEdit? _presetPathEdit;
    private LineEdit? _outputDirectoryEdit;
    private OptionButton? _baseLayerOption;
    private TextureRect? _previewTextureRect;
    private TextEdit? _summaryText;
    private SpinBox? _sampleXSpin;
    private SpinBox? _sampleZSpin;
    private TextEdit? _sampleText;
    private TextEdit? _visualCatalogText;
    private LineEdit? _fromPoiIdEdit;
    private LineEdit? _toPoiIdEdit;
    private TextEdit? _pathText;
    private TextEdit? _validationText;

    private string _resolvedSettingsPath = string.Empty;
    private string _resolvedVisualCatalogPath = string.Empty;
    private string _cachedPlanKey = string.Empty;
    private TerrainSettings? _cachedSettings;
    private TerrainGenerationProfile _cachedProfile;
    private TerrainWorldPlan? _cachedPlan;
    private TerrainRouteGraphSnapshot? _cachedRouteGraph;
    private bool _validationRunning;

    public override void _Ready()
    {
        if (_uiBuilt)
        {
            return;
        }

        _uiBuilt = true;
        BuildUi();
        if (_settingsPathEdit is not null &&
            string.IsNullOrWhiteSpace(_settingsPathEdit.Text) &&
            File.Exists(ProjectSettings.GlobalizePath(DefaultTerrainSettingsPath)))
        {
            SetSettingsPath(DefaultTerrainSettingsPath);
        }

        if (_visualCatalogPathEdit is not null &&
            string.IsNullOrWhiteSpace(_visualCatalogPathEdit.Text) &&
            File.Exists(ProjectSettings.GlobalizePath(DefaultTerrainVisualCatalogPath)))
        {
            SetVisualCatalogPath(DefaultTerrainVisualCatalogPath);
        }

        if (!string.IsNullOrWhiteSpace(_resolvedSettingsPath))
        {
            SetStatus($"Using default TerrainSettings resource '{DefaultTerrainSettingsPath}'.");
            return;
        }

        SetStatus("Select a TerrainSettings resource from the FileSystem dock or paste a res:// path.");
    }

    private void BuildUi()
    {
        Name = "TerrainEditorDock";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(scroll);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        scroll.AddChild(content);

        content.AddChild(CreateSectionTitle("Terrain Settings"));

        _settingsPathEdit = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "res://path/to/terrain_settings.tres"
        };
        content.AddChild(_settingsPathEdit);

        content.AddChild(CreateSectionTitle("Terrain Visual Catalog"));

        _visualCatalogPathEdit = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "res://path/to/terrain_visual_catalog.tres"
        };
        content.AddChild(_visualCatalogPathEdit);

        var visualCatalogRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddChild(visualCatalogRow);
        visualCatalogRow.AddChild(CreateActionButton("Use Default Catalog", OnUseDefaultVisualCatalogPressed));
        visualCatalogRow.AddChild(CreateActionButton("Validate Visual Catalog", OnValidateVisualCatalogPressed));

        _visualCatalogText = CreateReadOnlyText(160.0f);
        content.AddChild(_visualCatalogText);

        var grid = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddChild(grid);

        _previewSeedSpin = CreateSpinBox(0.0, int.MaxValue, 1.0, 613061.0);
        AddLabeledControl(grid, "Preview Seed", _previewSeedSpin);

        _worldSizeSpin = CreateSpinBox(1024.0, 65536.0, 256.0, 12288.0);
        AddLabeledControl(grid, "World Size", _worldSizeSpin);

        _imageSizeSpin = CreateSpinBox(128.0, 4096.0, 64.0, 512.0);
        AddLabeledControl(grid, "Image Size", _imageSizeSpin);

        _presetPathEdit = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "res://Resources/Terrain/TerrainSettings_Seed_XXXXXX.tres"
        };
        AddLabeledControl(grid, "Preset Copy", _presetPathEdit);

        _outputDirectoryEdit = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Text = "user://terrain_editor"
        };
        AddLabeledControl(grid, "Output Dir", _outputDirectoryEdit);

        _baseLayerOption = new OptionButton();
        foreach (TerrainMapLayer layer in Enum.GetValues<TerrainMapLayer>())
        {
            _baseLayerOption.AddItem(layer.ToString());
        }

        _baseLayerOption.Select((int)TerrainMapLayer.Biome);
        AddLabeledControl(grid, "Preview Layer", _baseLayerOption);

        var actionRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddChild(actionRow);

        Button previewButton = CreateActionButton("Preview Plan", OnPreviewPlanPressed);
        Button previewSeedButton = CreateActionButton("Preview Seed", OnPreviewSeedPressed);
        Button exportButton = CreateActionButton("Export Artifacts", OnExportPressed);
        Button savePresetButton = CreateActionButton("Save Preset Copy", OnSavePresetCopyPressed);
        Button validateButton = CreateActionButton("Run PR Validation", OnRunValidationPressed);
        actionRow.AddChild(previewButton);
        actionRow.AddChild(previewSeedButton);
        actionRow.AddChild(exportButton);
        actionRow.AddChild(savePresetButton);
        actionRow.AddChild(validateButton);

        _statusLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddChild(_statusLabel);

        _profileLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddChild(_profileLabel);

        content.AddChild(CreateSectionTitle("Plan Preview"));
        _previewTextureRect = new TextureRect
        {
            CustomMinimumSize = new Vector2(480.0f, 480.0f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        content.AddChild(_previewTextureRect);

        _summaryText = CreateReadOnlyText(340.0f);
        content.AddChild(_summaryText);

        content.AddChild(CreateSectionTitle("Semantic Sampling"));
        var sampleGrid = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddChild(sampleGrid);

        _sampleXSpin = CreateSpinBox(-65536.0, 65536.0, 32.0, 0.0);
        _sampleZSpin = CreateSpinBox(-65536.0, 65536.0, 32.0, 0.0);
        AddLabeledControl(sampleGrid, "World X", _sampleXSpin);
        AddLabeledControl(sampleGrid, "World Z", _sampleZSpin);

        Button sampleButton = CreateActionButton("Sample Point", OnSamplePressed);
        content.AddChild(sampleButton);

        _sampleText = CreateReadOnlyText(180.0f);
        content.AddChild(_sampleText);

        content.AddChild(CreateSectionTitle("Route Graph Path"));
        var pathGrid = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddChild(pathGrid);

        _fromPoiIdEdit = new LineEdit { PlaceholderText = "From POI Id" };
        _toPoiIdEdit = new LineEdit { PlaceholderText = "To POI Id" };
        AddLabeledControl(pathGrid, "From Id", _fromPoiIdEdit);
        AddLabeledControl(pathGrid, "To Id", _toPoiIdEdit);

        Button pathButton = CreateActionButton("Preview Path", OnPreviewPathPressed);
        content.AddChild(pathButton);

        _pathText = CreateReadOnlyText(160.0f);
        content.AddChild(_pathText);

        content.AddChild(CreateSectionTitle("Validation"));
        _validationText = CreateReadOnlyText(260.0f);
        content.AddChild(_validationText);
    }

    private static Label CreateSectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            ThemeTypeVariation = "HeaderSmall"
        };
    }

    private static TextEdit CreateReadOnlyText(float minHeight)
    {
        return new TextEdit
        {
            Editable = false,
            CustomMinimumSize = new Vector2(0.0f, minHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
    }

    private static Button CreateActionButton(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        button.Pressed += action;
        return button;
    }

    private static SpinBox CreateSpinBox(double min, double max, double step, double value)
    {
        return new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
    }

    private static void AddLabeledControl(GridContainer parent, string label, Control control)
    {
        parent.AddChild(new Label { Text = label, VerticalAlignment = VerticalAlignment.Center });
        parent.AddChild(control);
    }

    public void SetSettingsPath(string path)
    {
        if (_settingsPathEdit is null)
        {
            return;
        }

        _settingsPathEdit.Text = NormalizeResourcePath(path);
        _resolvedSettingsPath = _settingsPathEdit.Text;
        if (TryResolveSettings(out TerrainSettings? settings, out _) && settings is not null)
        {
            SyncPreviewSeedFromSettings(settings);
        }
    }

    public void SetVisualCatalogPath(string path)
    {
        if (_visualCatalogPathEdit is null)
        {
            return;
        }

        _visualCatalogPathEdit.Text = NormalizeResourcePath(path);
        _resolvedVisualCatalogPath = _visualCatalogPathEdit.Text;
    }

    public void SetExternalStatus(string message, bool isError = false)
    {
        SetStatus(message, isError);
    }

    private void OnPreviewPlanPressed()
    {
        if (!TryBuildPlan(out TerrainSettings? settings, out TerrainGenerationProfile profile, out TerrainWorldPlan? plan, out string report, out string error))
        {
            SetStatus(error, isError: true);
            return;
        }

        TerrainWorldPlan resolvedPlan = plan!;
        _cachedSettings = settings;
        _cachedProfile = profile;
        _cachedPlan = resolvedPlan;
        _cachedRouteGraph = TerrainRouteGraphSnapshot.FromPlan(resolvedPlan);
        _cachedPlanKey = ComputePlanCacheKey(_resolvedSettingsPath, profile, resolvedPlan.WorldSize, SelectedBaseLayer());

        Image image = TerrainWorldPlanExporter.CreatePlanMap(resolvedPlan, profile, SelectedImageSize(), SelectedBaseLayer());
        _previewTextureRect!.Texture = ImageTexture.CreateFromImage(image);
        _summaryText!.Text = report;
        _sampleText!.Text = string.Empty;
        _pathText!.Text = string.Empty;
        UpdatePoiDefaults(resolvedPlan);
        UpdateProfileLabel(profile, resolvedPlan);
        SetStatus("Terrain plan preview updated.");
    }

    private void OnPreviewSeedPressed()
    {
        if (!TryBuildPlanForSeedOverride(SelectedPreviewSeed(), out TerrainSettings? settings, out TerrainGenerationProfile profile, out TerrainWorldPlan? plan, out string report, out string error))
        {
            SetStatus(error, isError: true);
            return;
        }

        TerrainWorldPlan resolvedPlan = plan!;
        _cachedSettings = settings;
        _cachedProfile = profile;
        _cachedPlan = resolvedPlan;
        _cachedRouteGraph = TerrainRouteGraphSnapshot.FromPlan(resolvedPlan);
        _cachedPlanKey = ComputePlanCacheKey(_resolvedSettingsPath, profile, resolvedPlan.WorldSize, SelectedBaseLayer());

        Image image = TerrainWorldPlanExporter.CreatePlanMap(resolvedPlan, profile, SelectedImageSize(), SelectedBaseLayer());
        _previewTextureRect!.Texture = ImageTexture.CreateFromImage(image);
        _summaryText!.Text =
            $"Preview Seed Override: {profile.Seed}{System.Environment.NewLine}" +
            $"Source Settings: {_resolvedSettingsPath}{System.Environment.NewLine}{System.Environment.NewLine}" +
            report;
        _sampleText!.Text = string.Empty;
        _pathText!.Text = string.Empty;
        UpdatePoiDefaults(resolvedPlan);
        UpdateProfileLabel(profile, resolvedPlan);
        SetStatus($"Terrain plan preview updated for seed override {profile.Seed}. Source resource was not mutated.");
    }

    private void OnExportPressed()
    {
        if (!TryBuildPlan(out TerrainSettings? _, out TerrainGenerationProfile profile, out TerrainWorldPlan? plan, out string report, out string error))
        {
            SetStatus(error, isError: true);
            return;
        }

        TerrainWorldPlan resolvedPlan = plan!;
        string outputDirectory = string.IsNullOrWhiteSpace(_outputDirectoryEdit!.Text)
            ? "user://terrain_editor"
            : _outputDirectoryEdit.Text.Trim();
        TerrainMapLayer layer = SelectedBaseLayer();
        int imageSize = SelectedImageSize();

        TerrainWorldPlanArtifactResult artifacts = TerrainWorldPlanExporter.SaveOpenWorldArtifacts(
            resolvedPlan,
            profile,
            imageSize,
            outputDirectory,
            layer);
        var exportSummary = new StringBuilder(report.Length + 512);
        exportSummary.AppendLine(report);
        exportSummary.AppendLine();
        exportSummary.AppendLine("Export Artifacts");
        exportSummary.AppendLine($"Plan JSON: {ProjectSettings.GlobalizePath(artifacts.JsonPath)} ({artifacts.JsonSaveError})");
        exportSummary.AppendLine($"Plan Map: {ProjectSettings.GlobalizePath(artifacts.MapPath)} ({artifacts.MapSaveError})");
        exportSummary.AppendLine($"Traversal Map: {ProjectSettings.GlobalizePath(artifacts.TraversalCostMapPath)} ({artifacts.TraversalCostMapSaveError})");
        exportSummary.AppendLine($"Text Report: {ProjectSettings.GlobalizePath(artifacts.ReportPath)} ({artifacts.ReportSaveError})");
        _summaryText!.Text = exportSummary.ToString();

        SetStatus(
            artifacts.Passed
                ? $"Terrain artifacts exported to '{outputDirectory}'."
                : $"Terrain artifact export completed with errors. JSON {artifacts.JsonSaveError}, traversal {artifacts.TraversalCostMapSaveError}, map {artifacts.MapSaveError}, report {artifacts.ReportSaveError}.",
            isError: !artifacts.Passed);
    }

    private void OnSavePresetCopyPressed()
    {
        if (!TryResolveSettings(out TerrainSettings? settings, out string error) || settings is null)
        {
            SetStatus(error, isError: true);
            return;
        }

        int seedOverride = SelectedPreviewSeed();
        TerrainSettings duplicate = DuplicateTerrainSettings(settings);
        ApplySeedOverride(duplicate, seedOverride);

        string outputPath = ResolvePresetOutputPath(seedOverride);
        Error directoryError = EnsureDirectoryForPath(outputPath);
        if (directoryError != Error.Ok)
        {
            SetStatus($"Failed to create preset directory for '{outputPath}' ({directoryError}).", isError: true);
            return;
        }

        Error saveError = ResourceSaver.Save(duplicate, outputPath, ResourceSaver.SaverFlags.ChangePath);
        if (saveError != Error.Ok)
        {
            SetStatus($"Failed to save TerrainSettings preset copy to '{outputPath}' ({saveError}).", isError: true);
            return;
        }

        if (_presetPathEdit is not null)
        {
            _presetPathEdit.Text = outputPath;
        }

        SetSettingsPath(outputPath);
        SetStatus($"Saved TerrainSettings preset copy to '{outputPath}' with seed {seedOverride}.");
    }

    private void OnUseDefaultVisualCatalogPressed()
    {
        SetVisualCatalogPath(DefaultTerrainVisualCatalogPath);
        OnValidateVisualCatalogPressed();
    }

    private void OnValidateVisualCatalogPressed()
    {
        if (!TryResolveVisualCatalog(out TerrainVisualCatalog? catalog, out string error) || catalog is null)
        {
            _visualCatalogText!.Text = string.Empty;
            SetStatus(error, isError: true);
            return;
        }

        TerrainVisualCatalogValidationSummary summary = ValidateVisualCatalog(catalog);
        _visualCatalogText!.Text = summary.Report;
        SetStatus(
            summary.Passed
                ? $"Terrain visual catalog '{_resolvedVisualCatalogPath}' is valid for the current fallback policy."
                : $"Terrain visual catalog '{_resolvedVisualCatalogPath}' has production-readiness issues.",
            isError: !summary.Passed);
    }

    private void OnSamplePressed()
    {
        if (!TryResolveProfile(out TerrainGenerationProfile profile, out string error))
        {
            SetStatus(error, isError: true);
            return;
        }

        Vector2 world = new((float)_sampleXSpin!.Value, (float)_sampleZSpin!.Value);
        TerrainWorldField field = TerrainWorldFieldSampler.Sample(world, profile);
        TerrainSample surface = TerrainSampler.SampleWithSlope(world, profile, spacing: 24.0f);
        TerrainWaterState water = TerrainSemanticClassifier.ClassifyWater(field, profile);
        TerrainGameplayTags tags = TerrainSemanticClassifier.ClassifyGameplayTags(field, profile);
        TerrainTraversalCost traversal = TerrainSemanticClassifier.ClassifyTraversalCost(field, surface, profile);

        var builder = new StringBuilder(512);
        builder.AppendLine($"World: {world.X:0.0}, {world.Y:0.0}");
        builder.AppendLine($"Height: {field.Height:0.00}  Slope: {surface.Slope:0.000}");
        builder.AppendLine($"Biome/Landscape: {field.BiomeKind} / {field.LandscapeKind}");
        builder.AppendLine($"Water: {water.Kind} depth {water.Depth:0.00} strength {water.Strength:0.000}");
        builder.AppendLine($"Tags: {tags.Flags}");
        builder.AppendLine($"Traversal: blocked {traversal.IsBlocked}, cost {traversal.Cost:0.000}, traversability {traversal.Traversability:0.000}");
        builder.AppendLine($"Scenic/Resource/Hazard/Encounter: {field.ScenicPotential:0.000} / {field.ResourcePotential:0.000} / {field.HazardPotential:0.000} / {field.EncounterPotential:0.000}");
        builder.AppendLine($"River/Lake/Moisture/Temperature: {field.River:0.000} / {field.Lake:0.000} / {field.Moisture:0.000} / {field.Temperature:0.000}");
        _sampleText!.Text = builder.ToString();
        SetStatus($"Sampled terrain semantics at {world.X:0.0}, {world.Y:0.0}.");
    }

    private void OnPreviewPathPressed()
    {
        if (!TryEnsureCurrentPlan(out TerrainGenerationProfile _, out TerrainWorldPlan plan, out string error))
        {
            SetStatus(error, isError: true);
            return;
        }

        if (_cachedRouteGraph is null)
        {
            _cachedRouteGraph = TerrainRouteGraphSnapshot.FromPlan(plan);
        }

        if (!TryParsePoiId(_fromPoiIdEdit!, out int fromPointId) ||
            !TryParsePoiId(_toPoiIdEdit!, out int toPointId))
        {
            SetStatus("Enter valid From/To POI ids before previewing a route graph path.", isError: true);
            return;
        }

        if (!_cachedRouteGraph.TryFindPath(fromPointId, toPointId, out TerrainRouteGraphPath? path) || path is null)
        {
            _pathText!.Text = string.Empty;
            SetStatus($"No route graph path was found between POI {fromPointId} and POI {toPointId}.", isError: true);
            return;
        }

        var builder = new StringBuilder(384);
        builder.AppendLine($"Path {path.StartPointId} -> {path.GoalPointId}");
        builder.AppendLine($"Points: {string.Join(" -> ", path.PointIds)}");
        builder.AppendLine($"Edges: {path.Edges.Length}");
        builder.AppendLine($"Waypoints: {path.Waypoints.Length}");
        builder.AppendLine($"Total Cost: {path.TotalCost:0.000}");
        builder.AppendLine($"Total Distance: {path.TotalDistance:0.0} m");

        if (path.Edges.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Directed Edges");
            for (int i = 0; i < path.Edges.Length; i++)
            {
                TerrainRouteGraphEdge edge = path.Edges[i];
                builder.AppendLine(
                    $"{i + 1}. {edge.FromPointId} -> {edge.ToPointId}  {edge.Kind}  cost {edge.Cost:0.000}  scenic {edge.AverageScenicPotential:0.000}  traversability {edge.AverageTraversability:0.000}");
            }
        }

        _pathText!.Text = builder.ToString();
        SetStatus($"Previewed route graph path between POI {fromPointId} and POI {toPointId}.");
    }

    private void OnRunValidationPressed()
    {
        if (_validationRunning)
        {
            SetStatus("Terrain validation is already running.");
            return;
        }

        _validationRunning = true;
        _validationText!.Text = "Running terrain PR validation...";
        SetStatus("Running terrain PR validation. This may take a few seconds.");

        string repositoryRoot = ResolveRepositoryRoot();
        _ = Task.Run(() => ExecuteValidationProcess(repositoryRoot));
    }

    private void ExecuteValidationProcess(string repositoryRoot)
    {
        try
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add("tools/TerrainValidation/TerrainValidation.csproj");
            startInfo.ArgumentList.Add("--configuration");
            startInfo.ArgumentList.Add("Release");
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add("--validation-tier");
            startInfo.ArgumentList.Add("pr");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                CallDeferred(nameof(ApplyValidationResult), "Failed to start dotnet terrain validation process.", 1);
                return;
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            string output = string.IsNullOrWhiteSpace(stderr)
                ? stdout
                : $"{stdout}{System.Environment.NewLine}{stderr}";
            CallDeferred(nameof(ApplyValidationResult), output, process.ExitCode);
        }
        catch (Exception exception)
        {
            CallDeferred(nameof(ApplyValidationResult), $"Terrain validation failed to execute: {exception.Message}", 1);
        }
    }

    private void ApplyValidationResult(string output, int exitCode)
    {
        _validationRunning = false;
        _validationText!.Text = output;
        SetStatus(
            exitCode == 0
                ? "Terrain PR validation passed."
                : $"Terrain PR validation failed with exit code {exitCode}.",
            isError: exitCode != 0);
    }

    private bool TryResolveProfile(out TerrainGenerationProfile profile, out string error)
    {
        if (!TryResolveSettings(out TerrainSettings? settings, out error))
        {
            profile = default;
            return false;
        }

        TerrainSettings resolvedSettings = settings!;
        profile = resolvedSettings.Snapshot();
        _cachedSettings = resolvedSettings;
        _cachedProfile = profile;
        return true;
    }

    private bool TryBuildPlanForSeedOverride(
        int seedOverride,
        out TerrainSettings? settings,
        out TerrainGenerationProfile profile,
        out TerrainWorldPlan? plan,
        out string report,
        out string error)
    {
        settings = null;
        profile = default;
        plan = null;
        report = string.Empty;
        error = string.Empty;

        if (!TryResolveSettings(out TerrainSettings? sourceSettings, out error) || sourceSettings is null)
        {
            return false;
        }

        TerrainSettings workingSettings = DuplicateTerrainSettings(sourceSettings);
        ApplySeedOverride(workingSettings, seedOverride);
        settings = workingSettings;
        profile = workingSettings.Snapshot();
        float worldSize = SelectedWorldSize();
        plan = TerrainWorldPlanner.CreateOpenWorldPlan(profile, Vector2.Zero, worldSize);
        TerrainWorldPlanningGateResult planningGate = TerrainWorldPlanner.ValidateOpenWorldPlanning(plan);
        TerrainQualityGateResult qualityGate = TerrainQualityAnalyzer.ValidateOpenWorldDefault(plan.QualityReport);
        TerrainExperienceGateResult experienceGate = TerrainExperienceAnalyzer.ValidateOpenWorldDefault(plan.ExperienceReport);
        report = TerrainWorldPlanExporter.CreateTextReport(plan, profile, planningGate, qualityGate, experienceGate);
        return true;
    }

    private bool TryBuildPlan(
        out TerrainSettings? settings,
        out TerrainGenerationProfile profile,
        out TerrainWorldPlan? plan,
        out string report,
        out string error)
    {
        settings = null;
        profile = default;
        plan = null;
        report = string.Empty;
        error = string.Empty;

        if (!TryResolveSettings(out settings, out error))
        {
            return false;
        }

        TerrainSettings resolvedSettings = settings!;
        profile = resolvedSettings.Snapshot();
        float worldSize = SelectedWorldSize();
        plan = TerrainWorldPlanner.CreateOpenWorldPlan(profile, Vector2.Zero, worldSize);
        TerrainWorldPlanningGateResult planningGate = TerrainWorldPlanner.ValidateOpenWorldPlanning(plan);
        TerrainQualityGateResult qualityGate = TerrainQualityAnalyzer.ValidateOpenWorldDefault(plan.QualityReport);
        TerrainExperienceGateResult experienceGate = TerrainExperienceAnalyzer.ValidateOpenWorldDefault(plan.ExperienceReport);
        report = TerrainWorldPlanExporter.CreateTextReport(plan, profile, planningGate, qualityGate, experienceGate);
        return true;
    }

    private bool TryEnsureCurrentPlan(
        out TerrainGenerationProfile profile,
        out TerrainWorldPlan plan,
        out string error)
    {
        error = string.Empty;
        if (!TryResolveProfile(out profile, out error))
        {
            plan = null!;
            return false;
        }

        string cacheKey = ComputePlanCacheKey(_resolvedSettingsPath, profile, SelectedWorldSize(), SelectedBaseLayer());
        if (_cachedPlan is not null &&
            string.Equals(cacheKey, _cachedPlanKey, StringComparison.Ordinal))
        {
            plan = _cachedPlan;
            return true;
        }

        if (!TryBuildPlan(out _, out profile, out TerrainWorldPlan? rebuiltPlan, out string report, out error) ||
            rebuiltPlan is null)
        {
            plan = null!;
            return false;
        }

        _cachedPlan = rebuiltPlan;
        _cachedRouteGraph = TerrainRouteGraphSnapshot.FromPlan(rebuiltPlan);
        _cachedPlanKey = cacheKey;
        _summaryText!.Text = report;
        UpdatePoiDefaults(rebuiltPlan);
        UpdateProfileLabel(profile, rebuiltPlan);
        plan = rebuiltPlan;
        return true;
    }

    private bool TryResolveSettings(out TerrainSettings? settings, out string error)
    {
        settings = null;
        error = string.Empty;
        if (_settingsPathEdit is null)
        {
            error = "Terrain editor dock was not initialized correctly.";
            return false;
        }

        string path = _settingsPathEdit.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Set a TerrainSettings resource path before generating terrain previews.";
            return false;
        }

        path = NormalizeResourcePath(path);
        Resource? resource = ResourceLoader.Load(path);
        if (resource is not TerrainSettings terrainSettings)
        {
            error = $"Resource '{path}' is not a TerrainSettings asset.";
            return false;
        }

        _resolvedSettingsPath = path;
        _settingsPathEdit.Text = path;
        settings = terrainSettings;
        SyncPreviewSeedFromSettings(terrainSettings);
        return true;
    }

    private bool TryResolveVisualCatalog(out TerrainVisualCatalog? catalog, out string error)
    {
        catalog = null;
        error = string.Empty;
        if (_visualCatalogPathEdit is null)
        {
            error = "Terrain editor dock was not initialized correctly.";
            return false;
        }

        string path = _visualCatalogPathEdit.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Set a TerrainVisualCatalog resource path before validating visual assets.";
            return false;
        }

        path = NormalizeResourcePath(path);
        Resource? resource = ResourceLoader.Load(path);
        if (resource is not TerrainVisualCatalog visualCatalog)
        {
            error = $"Resource '{path}' is not a TerrainVisualCatalog asset.";
            return false;
        }

        _resolvedVisualCatalogPath = path;
        _visualCatalogPathEdit.Text = path;
        catalog = visualCatalog;
        return true;
    }

    private static TerrainVisualCatalogValidationSummary ValidateVisualCatalog(TerrainVisualCatalog catalog)
    {
        TerrainVisualCatalogValidationReport report = catalog.ValidateCatalog();
        bool missingEntriesAllowed = report.UsePrimitiveFallbacks;

        var builder = new StringBuilder(768);
        builder.AppendLine($"Primitive Fallbacks: {report.UsePrimitiveFallbacks}");
        builder.AppendLine($"Referenced Resources: {report.ReferencedResources.Length}");
        builder.AppendLine($"Scatter Entries: {report.ScatterEntryCount}  mesh {report.ScatterMeshEntryCount}  scene {report.ScatterSceneEntryCount}  duplicates {report.ScatterDuplicateEntryCount}  invalid LOD {report.ScatterInvalidLodEntryCount}");
        builder.AppendLine($"Landmark Entries: {report.LandmarkEntryCount}  mesh {report.LandmarkMeshEntryCount}  scene {report.LandmarkSceneEntryCount}  duplicates {report.LandmarkDuplicateEntryCount}  invalid LOD {report.LandmarkInvalidLodEntryCount}");
        builder.AppendLine($"Missing Scatter Kinds: {report.MissingScatterKinds.Length}{(missingEntriesAllowed ? " (fallback allowed)" : string.Empty)}");
        builder.AppendLine($"Missing Landmark Kinds: {report.MissingLandmarkKinds.Length}{(missingEntriesAllowed ? " (fallback allowed)" : string.Empty)}");

        if (!report.Passed)
        {
            builder.AppendLine();
            builder.AppendLine("Issues");
            if (!missingEntriesAllowed && report.MissingScatterKinds.Length > 0)
            {
                builder.AppendLine($"Scatter kinds without Mesh or Scene: {string.Join(", ", report.MissingScatterKinds)}");
            }

            if (!missingEntriesAllowed && report.MissingLandmarkKinds.Length > 0)
            {
                builder.AppendLine($"Landmark kinds without Mesh or Scene: {string.Join(", ", report.MissingLandmarkKinds)}");
            }

            if (report.ScatterDuplicateEntryCount > 0 || report.LandmarkDuplicateEntryCount > 0)
            {
                builder.AppendLine("Duplicate entries use the first matching kind and should be removed.");
            }

            if (report.ScatterInvalidLodEntryCount > 0 || report.LandmarkInvalidLodEntryCount > 0)
            {
                builder.AppendLine("Invalid LOD entries have MaxLod lower than MinLod.");
            }
        }

        return new TerrainVisualCatalogValidationSummary(report.Passed, builder.ToString());
    }

    private string NormalizeResourcePath(string path)
    {
        string normalized = path.Replace('\\', '/');
        if (normalized.Contains("://", StringComparison.Ordinal))
        {
            return normalized;
        }

        string projectRoot = ProjectSettings.GlobalizePath("res://").Replace('\\', '/').TrimEnd('/');
        string fullPath = Path.GetFullPath(normalized).Replace('\\', '/');
        if (fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
        {
            string suffix = fullPath[projectRoot.Length..].TrimStart('/');
            return string.IsNullOrWhiteSpace(suffix) ? "res://" : $"res://{suffix}";
        }

        return normalized;
    }

    private string ResolveRepositoryRoot()
    {
        string projectRoot = ProjectSettings.GlobalizePath("res://");
        return Path.GetFullPath(Path.Combine(projectRoot, ".."));
    }

    private string ResolvePresetOutputPath(int seedOverride)
    {
        string requested = _presetPathEdit?.Text.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return NormalizeResourcePath(requested);
        }

        string baseDirectory = "res://Resources/Terrain";
        if (!string.IsNullOrWhiteSpace(_resolvedSettingsPath))
        {
            string normalized = _resolvedSettingsPath.Replace('\\', '/');
            int slash = normalized.LastIndexOf('/');
            if (slash > 0)
            {
                baseDirectory = normalized[..slash];
            }
        }

        return $"{baseDirectory.TrimEnd('/')}/TerrainSettings_Seed_{seedOverride}.tres";
    }

    private static Error EnsureDirectoryForPath(string path)
    {
        try
        {
            string normalized = path.Replace('\\', '/');
            int slash = normalized.LastIndexOf('/');
            if (slash <= 0)
            {
                return Error.Ok;
            }

            string directory = normalized[..slash];
            if (directory.Contains("://", StringComparison.Ordinal))
            {
                return DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(directory));
            }

            Directory.CreateDirectory(directory);
            return Error.Ok;
        }
        catch (Exception)
        {
            return Error.FileCantWrite;
        }
    }

    private static TerrainSettings DuplicateTerrainSettings(TerrainSettings source)
    {
        return source.Duplicate(true) as TerrainSettings ?? source;
    }

    private static void ApplySeedOverride(TerrainSettings settings, int seedOverride)
    {
        settings.Seed = seedOverride;
        if (settings.WorldProfile is not null)
        {
            settings.WorldProfile.Seed = seedOverride;
        }
    }

    private void SyncPreviewSeedFromSettings(TerrainSettings settings)
    {
        if (_previewSeedSpin is null)
        {
            return;
        }

        _previewSeedSpin.Value = settings.WorldProfile?.Seed ?? settings.Seed;
    }

    private void UpdatePoiDefaults(TerrainWorldPlan plan)
    {
        if (_fromPoiIdEdit is null || _toPoiIdEdit is null || plan.PointsOfInterest.Length == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_fromPoiIdEdit.Text))
        {
            _fromPoiIdEdit.Text = plan.PointsOfInterest[0].Id.ToString();
        }

        if (string.IsNullOrWhiteSpace(_toPoiIdEdit.Text))
        {
            int fallbackIndex = Math.Min(plan.PointsOfInterest.Length - 1, 1);
            _toPoiIdEdit.Text = plan.PointsOfInterest[fallbackIndex].Id.ToString();
        }
    }

    private void UpdateProfileLabel(TerrainGenerationProfile profile, TerrainWorldPlan plan)
    {
        if (_profileLabel is null)
        {
            return;
        }

        _profileLabel.Text =
            $"Terrain API {TerrainApiVersion.Version}  |  Profile {profile.StableHash()}  |  " +
            $"World {plan.WorldSize:0}  |  Grid {plan.GridResolution}x{plan.GridResolution}  |  " +
            $"POIs {plan.PointsOfInterest.Length}  |  Routes {plan.Routes.Length}";
    }

    private TerrainMapLayer SelectedBaseLayer()
    {
        if (_baseLayerOption is null || _baseLayerOption.Selected < 0)
        {
            return TerrainMapLayer.Biome;
        }

        string text = _baseLayerOption.GetItemText(_baseLayerOption.Selected);
        return Enum.TryParse(text, ignoreCase: false, out TerrainMapLayer layer)
            ? layer
            : TerrainMapLayer.Biome;
    }

    private int SelectedImageSize()
    {
        return Mathf.Clamp((int)Math.Round(_imageSizeSpin?.Value ?? 512.0), 128, 4096);
    }

    private int SelectedPreviewSeed()
    {
        return Math.Max(0, (int)Math.Round(_previewSeedSpin?.Value ?? 613061.0));
    }

    private float SelectedWorldSize()
    {
        return Mathf.Clamp((float)(_worldSizeSpin?.Value ?? 12288.0), 1024.0f, 65536.0f);
    }

    private static bool TryParsePoiId(LineEdit edit, out int pointId)
    {
        return int.TryParse(edit.Text.Trim(), out pointId);
    }

    private static string ComputePlanCacheKey(string settingsPath, TerrainGenerationProfile profile, float worldSize, TerrainMapLayer baseLayer)
    {
        return $"{settingsPath}|{profile.StableHash()}|{worldSize:0.###}|{(int)baseLayer}";
    }

    private readonly record struct TerrainVisualCatalogValidationSummary(bool Passed, string Report);

    private void SetStatus(string message, bool isError = false)
    {
        if (_statusLabel is null)
        {
            return;
        }

        _statusLabel.Text = message;
        _statusLabel.Modulate = isError
            ? new Color(0.96f, 0.42f, 0.34f)
            : new Color(0.82f, 0.90f, 0.98f);
    }
}
