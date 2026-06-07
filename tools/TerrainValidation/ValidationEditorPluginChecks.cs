using System;
using System.IO;

internal static class TerrainValidationEditorPluginChecks
{
    internal static TerrainEditorPluginSmokeReport ValidateTerrainEditorPluginScaffold()
    {
        try
        {
            string repositoryRoot = Directory.GetCurrentDirectory();
            string pluginConfigPath = Path.Combine(repositoryRoot, "dao", "addons", "terrain_editor", "plugin.cfg");
            string pluginScriptPath = Path.Combine(repositoryRoot, "dao", "addons", "terrain_editor", "plugin.gd");
            string dockPanelPath = Path.Combine(repositoryRoot, "dao", "addons", "terrain_editor", "TerrainEditorDockPanel.cs");
            string defaultSettingsPath = Path.Combine(repositoryRoot, "dao", "Resources", "Terrain", "DefaultTerrainSettings.tres");
            string defaultVisualCatalogPath = Path.Combine(repositoryRoot, "dao", "Resources", "Terrain", "DefaultTerrainVisualCatalog.tres");
            string mainScenePath = Path.Combine(repositoryRoot, "dao", "Scenes", "Main.tscn");
            string terrainDemoPath = Path.Combine(repositoryRoot, "dao", "Scripts", "Demo", "TerrainDemo.cs");

            bool pluginConfigExists = File.Exists(pluginConfigPath);
            bool pluginScriptExists = File.Exists(pluginScriptPath);
            bool dockPanelExists = File.Exists(dockPanelPath);
            bool defaultSettingsResourceExists = File.Exists(defaultSettingsPath);
            bool defaultVisualCatalogResourceExists = File.Exists(defaultVisualCatalogPath);
            bool mainSceneExists = File.Exists(mainScenePath);

            string pluginConfig = pluginConfigExists ? File.ReadAllText(pluginConfigPath) : string.Empty;
            string pluginScript = pluginScriptExists ? File.ReadAllText(pluginScriptPath) : string.Empty;
            string dockPanel = dockPanelExists ? File.ReadAllText(dockPanelPath) : string.Empty;
            string mainScene = mainSceneExists ? File.ReadAllText(mainScenePath) : string.Empty;
            string terrainDemo = File.Exists(terrainDemoPath) ? File.ReadAllText(terrainDemoPath) : string.Empty;

            bool pluginConfigWiresScript =
                pluginConfig.Contains("[plugin]", StringComparison.Ordinal) &&
                pluginConfig.Contains("name=\"Terrain Editor\"", StringComparison.Ordinal) &&
                pluginConfig.Contains("script=\"res://addons/terrain_editor/plugin.gd\"", StringComparison.Ordinal);

            bool pluginScriptWiresDock =
                pluginScript.Contains("extends EditorPlugin", StringComparison.Ordinal) &&
                pluginScript.Contains("EditorDock.new()", StringComparison.Ordinal) &&
                pluginScript.Contains("add_dock(", StringComparison.Ordinal) &&
                pluginScript.Contains("remove_dock(", StringComparison.Ordinal) &&
                pluginScript.Contains("add_tool_menu_item(", StringComparison.Ordinal) &&
                pluginScript.Contains("remove_tool_menu_item(", StringComparison.Ordinal);

            bool dockPanelSupportsPreviewExportValidation =
                dockPanel.Contains("class TerrainEditorDockPanel", StringComparison.Ordinal) &&
                dockPanel.Contains("Preview Plan", StringComparison.Ordinal) &&
                dockPanel.Contains("Preview Seed", StringComparison.Ordinal) &&
                dockPanel.Contains("Export Artifacts", StringComparison.Ordinal) &&
                dockPanel.Contains("Save Preset Copy", StringComparison.Ordinal) &&
                dockPanel.Contains("Run PR Validation", StringComparison.Ordinal) &&
                dockPanel.Contains("Load Modification", StringComparison.Ordinal) &&
                dockPanel.Contains("Save Modification", StringComparison.Ordinal) &&
                dockPanel.Contains("Clear Modification", StringComparison.Ordinal) &&
                dockPanel.Contains("TerrainModificationLayer", StringComparison.Ordinal) &&
                dockPanel.Contains("TryLoadJson", StringComparison.Ordinal) &&
                dockPanel.Contains("QueryAffectedTiles", StringComparison.Ordinal) &&
                dockPanel.Contains("DefaultTerrainVisualCatalog.tres", StringComparison.Ordinal) &&
                dockPanel.Contains("Validate Visual Catalog", StringComparison.Ordinal) &&
                dockPanel.Contains("Use Default Catalog", StringComparison.Ordinal) &&
                dockPanel.Contains("TryResolveVisualCatalog", StringComparison.Ordinal) &&
                dockPanel.Contains("ValidateVisualCatalog", StringComparison.Ordinal) &&
                dockPanel.Contains("ValidateCatalog()", StringComparison.Ordinal) &&
                dockPanel.Contains("ReferencedResources", StringComparison.Ordinal) &&
                dockPanel.Contains("MissingScatterKinds", StringComparison.Ordinal) &&
                dockPanel.Contains("MissingLandmarkKinds", StringComparison.Ordinal) &&
                dockPanel.Contains("TerrainWorldPlanExporter", StringComparison.Ordinal) &&
                dockPanel.Contains("TerrainSemanticClassifier", StringComparison.Ordinal) &&
                dockPanel.Contains("TryFindPath", StringComparison.Ordinal) &&
                dockPanel.Contains("TryBuildPlanForSeedOverride", StringComparison.Ordinal) &&
                dockPanel.Contains("ResourceSaver.Save", StringComparison.Ordinal);

            bool mainSceneWiresDefaultSettings =
                mainScene.Contains("res://Resources/Terrain/DefaultTerrainSettings.tres", StringComparison.Ordinal) &&
                mainScene.Contains("TerrainSettingsResource = ExtResource", StringComparison.Ordinal);

            bool mainSceneWiresDefaultVisualCatalog =
                mainScene.Contains("res://Resources/Terrain/DefaultTerrainVisualCatalog.tres", StringComparison.Ordinal) &&
                mainScene.Contains("TerrainVisualCatalogResource = ExtResource", StringComparison.Ordinal);

            bool demoScriptSupportsSettingsResource =
                terrainDemo.Contains("TerrainSettingsResource", StringComparison.Ordinal) &&
                terrainDemo.Contains("DefaultTerrainSettings.tres", StringComparison.Ordinal) &&
                terrainDemo.Contains("ResolveTerrainSettings()", StringComparison.Ordinal);

            bool demoScriptSupportsVisualCatalogResource =
                terrainDemo.Contains("TerrainVisualCatalogResource", StringComparison.Ordinal) &&
                terrainDemo.Contains("DefaultTerrainVisualCatalog.tres", StringComparison.Ordinal) &&
                terrainDemo.Contains("ResolveTerrainVisualCatalog()", StringComparison.Ordinal) &&
                terrainDemo.Contains("VisualCatalog = visualCatalog", StringComparison.Ordinal);

            bool dockPanelUsesDefaultSettingsResource =
                dockPanel.Contains("DefaultTerrainSettings.tres", StringComparison.Ordinal) &&
                dockPanel.Contains("SetSettingsPath(DefaultTerrainSettingsPath)", StringComparison.Ordinal);

            bool passed =
                pluginConfigExists &&
                pluginScriptExists &&
                dockPanelExists &&
                defaultSettingsResourceExists &&
                defaultVisualCatalogResourceExists &&
                mainSceneExists &&
                pluginConfigWiresScript &&
                pluginScriptWiresDock &&
                dockPanelSupportsPreviewExportValidation &&
                mainSceneWiresDefaultSettings &&
                mainSceneWiresDefaultVisualCatalog &&
                demoScriptSupportsSettingsResource &&
                demoScriptSupportsVisualCatalogResource &&
                dockPanelUsesDefaultSettingsResource;

            return new TerrainEditorPluginSmokeReport(
                passed,
                pluginConfigExists,
                pluginScriptExists,
                dockPanelExists,
                defaultSettingsResourceExists,
                defaultVisualCatalogResourceExists,
                mainSceneExists,
                pluginConfigWiresScript,
                pluginScriptWiresDock,
                dockPanelSupportsPreviewExportValidation,
                mainSceneWiresDefaultSettings,
                mainSceneWiresDefaultVisualCatalog,
                demoScriptSupportsSettingsResource,
                demoScriptSupportsVisualCatalogResource,
                dockPanelUsesDefaultSettingsResource,
                passed
                    ? "terrain editor plugin scaffold and default terrain settings/visual catalog workflow exist and wire preview/export/validation/preset entry points"
                    : TerrainEditorPluginFailureReason(
                        pluginConfigExists,
                        pluginScriptExists,
                        dockPanelExists,
                        defaultSettingsResourceExists,
                        defaultVisualCatalogResourceExists,
                        mainSceneExists,
                        pluginConfigWiresScript,
                        pluginScriptWiresDock,
                        dockPanelSupportsPreviewExportValidation,
                        mainSceneWiresDefaultSettings,
                        mainSceneWiresDefaultVisualCatalog,
                        demoScriptSupportsSettingsResource,
                        demoScriptSupportsVisualCatalogResource,
                        dockPanelUsesDefaultSettingsResource));
        }
        catch (Exception ex)
        {
            return new TerrainEditorPluginSmokeReport(
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                $"terrain editor plugin smoke threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string TerrainEditorPluginFailureReason(
        bool pluginConfigExists,
        bool pluginScriptExists,
        bool dockPanelExists,
        bool defaultSettingsResourceExists,
        bool defaultVisualCatalogResourceExists,
        bool mainSceneExists,
        bool pluginConfigWiresScript,
        bool pluginScriptWiresDock,
        bool dockPanelSupportsPreviewExportValidation,
        bool mainSceneWiresDefaultSettings,
        bool mainSceneWiresDefaultVisualCatalog,
        bool demoScriptSupportsSettingsResource,
        bool demoScriptSupportsVisualCatalogResource,
        bool dockPanelUsesDefaultSettingsResource)
    {
        if (!pluginConfigExists)
        {
            return "terrain editor plugin.cfg was missing";
        }

        if (!pluginScriptExists)
        {
            return "terrain editor plugin.gd entry script was missing";
        }

        if (!dockPanelExists)
        {
            return "terrain editor C# dock panel was missing";
        }

        if (!defaultSettingsResourceExists)
        {
            return "default terrain settings resource was missing";
        }

        if (!defaultVisualCatalogResourceExists)
        {
            return "default terrain visual catalog resource was missing";
        }

        if (!mainSceneExists)
        {
            return "Main.tscn was missing";
        }

        if (!pluginConfigWiresScript)
        {
            return "terrain editor plugin.cfg did not wire the expected plugin script";
        }

        if (!pluginScriptWiresDock)
        {
            return "terrain editor plugin.gd did not wire the expected dock and tool menu entry points";
        }

        if (!dockPanelSupportsPreviewExportValidation)
        {
            return "terrain editor dock panel did not expose preview/export/validation/path-preview/visual-catalog capabilities";
        }

        if (!mainSceneWiresDefaultSettings)
        {
            return "Main.tscn did not wire the default terrain settings resource";
        }

        if (!mainSceneWiresDefaultVisualCatalog)
        {
            return "Main.tscn did not wire the default terrain visual catalog resource";
        }

        if (!demoScriptSupportsSettingsResource)
        {
            return "TerrainDemo did not support exported/default terrain settings resources";
        }

        if (!demoScriptSupportsVisualCatalogResource)
        {
            return "TerrainDemo did not support exported/default terrain visual catalog resources";
        }

        if (!dockPanelUsesDefaultSettingsResource)
        {
            return "terrain editor dock panel did not default to the repository terrain settings resource";
        }

        return "terrain editor plugin scaffold check failed";
    }
}
