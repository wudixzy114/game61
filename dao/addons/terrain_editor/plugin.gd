@tool
extends EditorPlugin

const PANEL_SCRIPT := preload("res://addons/terrain_editor/TerrainEditorDockPanel.cs")
const MENU_ITEM_USE_SELECTED := "Terrain: Use Selected TerrainSettings"

var _dock: EditorDock
var _panel: Control

func _enter_tree() -> void:
	_panel = PANEL_SCRIPT.new()
	_dock = EditorDock.new()
	_dock.title = "Terrain"
	_dock.default_slot = EditorDock.DOCK_SLOT_RIGHT_UL
	_dock.add_child(_panel)
	add_dock(_dock)
	add_tool_menu_item(MENU_ITEM_USE_SELECTED, _on_use_selected_settings)

func _exit_tree() -> void:
	remove_tool_menu_item(MENU_ITEM_USE_SELECTED)
	if _dock != null:
		remove_dock(_dock)
		_dock.queue_free()
		_dock = null
		_panel = null

func _on_use_selected_settings() -> void:
	if _panel == null:
		return

	for path in get_editor_interface().get_selected_paths():
		if not (path.ends_with(".tres") or path.ends_with(".res")):
			continue

		var resource := load(path)
		if resource is TerrainSettings:
			if _panel.has_method("SetSettingsPath"):
				_panel.call("SetSettingsPath", path)
			elif _panel.has_method("set_settings_path"):
				_panel.call("set_settings_path", path)

			if _panel.has_method("SetExternalStatus"):
				_panel.call("SetExternalStatus", "Using selected TerrainSettings '%s'." % path, false)
			elif _panel.has_method("set_external_status"):
				_panel.call("set_external_status", "Using selected TerrainSettings '%s'." % path, false)
			return

	if _panel.has_method("SetExternalStatus"):
		_panel.call("SetExternalStatus", "No TerrainSettings resource is selected in the FileSystem dock.", true)
	elif _panel.has_method("set_external_status"):
		_panel.call("set_external_status", "No TerrainSettings resource is selected in the FileSystem dock.", true)
