namespace Editor;

using Editor;
using Editor.MeshEditor;
using Editor.TerrainEditor;
using Sandbox;
using Sandbox.UI;
using System.Collections.Generic;

public static partial class EditorToolBars
{
	// --- MAIN TOOL DEFINITIONS ---
	private static List<ToolOptionDef> CreateMainToolDefs()
	{
		return
		[
			new() { Name="Select",
				Icon="hammer/select_tool_icon.png",
				ToggledIcon="hammer/select_tool_icon_activated.png",
				Hotkey="Shift+S",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.ActivateSelect,
				Description="Select. Select groups, objects or mesh components",
				Active = true,
				ActiveResolver = () =>
				{
					return EditorToolManager.CurrentModeName == nameof( MeshTool ) && SceneViewWidget.Current?.Tools?.CurrentTool is MeshTool meshTool
						&& meshTool.MoveMode?.GetType() == typeof( ResizeMode ); } },

			new() { Name="Move",
				ShortcutAction = "mesh.position.mode",
				Icon="hammer/move_tool_icon.png",
				ToggledIcon="hammer/move_tool_icon_activated.png",
				Hotkey="",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.ActivateMove,
				Description="Translate. Move the selected objects",
				ActiveResolver = () =>
				{
					return EditorToolManager.CurrentModeName == nameof( MeshTool ) && SceneViewWidget.Current?.Tools?.CurrentTool is MeshTool meshTool
						&& meshTool.MoveMode?.GetType() == typeof( MeshEditor.PositionMode ); } },

			new() { Name="Rotate",
				ShortcutAction = "mesh.rotate.mode",
				Icon="hammer/rotate_tool_icon.png",
				ToggledIcon="hammer/rotate_tool_icon_activated.png",
				Hotkey="",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.ActivateRotate,
				Description="Rotate. Rotate the selected objects",
				ActiveResolver = () =>
				{
					return EditorToolManager.CurrentModeName == nameof( MeshTool ) && SceneViewWidget.Current?.Tools?.CurrentTool is MeshTool meshTool
						&& meshTool.MoveMode?.GetType() == typeof( RotateMode ); } },

			new() { Name="Scale",
				ShortcutAction = "mesh.scale.mode",
				Icon="hammer/scale_tool_icon.png",
				ToggledIcon="hammer/scale_tool_icon_activated.png",
				Hotkey="",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.ActivateScale,
				Description="Scale. Scale the selected objects",
				ActiveResolver = () =>
				{
					return EditorToolManager.CurrentModeName == nameof( MeshTool ) && SceneViewWidget.Current?.Tools?.CurrentTool is MeshTool meshTool
						&& meshTool.MoveMode?.GetType() == typeof( ScaleMode ); } },

			new() { Name="Pivot",
				Icon="hammer/pivot_tool_icon.png",
				ToggledIcon="hammer/pivot_tool_icon_activated.png",
				Hotkey="",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.ActivatePivot,
				Description="Pivot Manipulation. Set the location of the gizmo for the current selection",
				ActiveResolver = () =>
				{
					return EditorToolManager.CurrentModeName == nameof( MeshTool ) && SceneViewWidget.Current?.Tools?.CurrentTool is MeshTool meshTool
						&& meshTool.MoveMode?.GetType() == typeof( PivotMode ); } },

			new() { Separator=true },
			new() { Separator=true },
			new() { Separator=true },

			new() { Name="Object Placer",
				Icon="hammer/entity_tool_icon.png",
				ToggledIcon="hammer/entity_tool_icon_activated.png",
				Hotkey="Shift+E",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectObjectPlacerTool,
				Description="Add new objects to the scene",
				ActiveResolver = () => EditorToolManager.CurrentModeName == nameof( ObjectPlacerTool ) },

			new() { Name="Block Tool",
				ShortcutAction = "tools.block-tool",
				Icon="hammer/block_tool_icon.png",
				ToggledIcon="hammer/block_tool_icon_activated.png",
				Hotkey="Shift+B",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectBlockTool,
				Description="Create new shapes by dragging out a box",
				ActiveResolver = () =>
				{
					if ( EditorToolManager.CurrentModeName != nameof( MeshTool ) )
						return false;

					var subMode = EditorToolManager.CurrentSubModeName;
					return subMode == nameof( PrimitiveTool ) || subMode == nameof( MeshTool );
				} },

			new() { Name="Path Tool",
				Icon="hammer/path_tool_icon.png",
				ToggledIcon="hammer/path_tool_icon_activated.png",
				Hotkey="Shift+P",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectPathTool,
				Description="Create path entities or primitives",
				ActiveResolver = () => EditorToolManager.CurrentModeName == "PathTool" },

			// John: Exists within Block Tool context, seems fine enough to merge I think.

			// new() { Name="Polygon Tool",
			// 	Icon="hammer/polygon_tool_icon.png",
			// 	ToggledIcon="hammer/polygon_tool_icon_activated.png",
			// 	Group = "MainTools",
			// 	GroupType = ToolBarOptionGroupType.SingleExclusive,
			// 	Checkable=true,
			// 	Description="Draw a polygon mesh" },

			new() { Name="Clipping Tool",
				ShortcutAction = "tools.clip-tool",
				Icon="hammer/clipping_tool_icon.png",
				ToggledIcon="hammer/clipping_tool_icon_activated.png",
				Hotkey="Shift+X",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectClippingTool,
				Description="Slice the selection by a plane",
				ActiveResolver = () => EditorToolManager.CurrentModeName == nameof( MeshTool )
					&& EditorToolManager.CurrentSubModeName == nameof( ClipTool ) },

			new() { Name="Mirror Tool",
				ShortcutAction = "tools.mirror-tool",
				Icon="hammer/mirror_tool_icon.png",
				ToggledIcon="hammer/mirror_tool_icon_activated.png",
				Hotkey="Shift+F",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectMirrorTool,
				Description="Mirror the selection",
				ActiveResolver = () => EditorToolManager.CurrentModeName == nameof( MeshTool )
					&& EditorToolManager.CurrentSubModeName == nameof( MirrorTool ) },
				
			// John: very niche specific tooling, we don't have it anyways

			// new() { Name="Texture Projection Tool",
			// 	Icon="hammer/textureprojection_tool_icon.png",
			// 	ToggledIcon="hammer/textureprojection_tool_icon_activated.png",
			// 	Group = "MainTools",
			// 	GroupType = ToolBarOptionGroupType.SingleExclusive,
			// 	Checkable=true,
			// 	Description="Modify texture mapping using projection tools" },

			new() { Name="Blend Painting Tool",
				ShortcutAction = "tools.vertex-paint-tool",
				Icon="hammer/paint_tool_icon.png",
				ToggledIcon="hammer/paint_tool_icon_activated.png",
				Hotkey="Shift+V",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectBlendTool,
				Description="Paint vertex blends, weights and colors",
				ActiveResolver = () => EditorToolManager.CurrentModeName == nameof( MeshTool )
					&& EditorToolManager.CurrentSubModeName == nameof( VertexPaintTool ) },

			new() { Name="Displacement Tool",
				Icon="hammer/displacement_tool_icon.png",
				ToggledIcon="hammer/displacement_tool_icon_activated.png",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectDisplacementTool,
				Description="Modify subdivided faces using brush tools",
				ActiveResolver = () => EditorToolManager.CurrentModeName == nameof( MeshTool )
					&& EditorToolManager.CurrentSubModeName == nameof( DisplacementTool ) },

			new() { Name="Physics Tool",
				Icon="hammer/physics_tool_icon.png",
				ToggledIcon="hammer/physics_tool_icon_activated.png",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectPhysicsTool,
				Description="Manipulate objects using physics simulation",
				ActiveResolver = () => EditorToolManager.CurrentModeName == nameof( PhysicsEditorTool ) },

			new() { Name="Terrain Tool",
				Icon="hammer/tool_terrain_icon.png",
				ToggledIcon="hammer/tool_terrain_icon_activated.png",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectTerraintool,
				Description="Create and modify terrain",
				ActiveResolver = () => EditorToolManager.CurrentModeName == nameof( TerrainEditorTool ) },

			new() { Name="Asset Spray Tool",
				Icon="hammer/asset_spray_tool_icon.png",
				ToggledIcon="hammer/asset_spray_tool_icon_activated.png",
				Group = "MainTools",
				GroupType = ToolBarOptionGroupType.SingleExclusive,
				Checkable=true,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectClutterTool,
				Description="Place pre-configured groups of objects on to a surface with randomization",
				ActiveResolver = () => EditorToolManager.CurrentModeName == nameof( ClutterTool ) },

			// John: These are very specific, pretty much not used in CS2/HLVR/HLX
			// So for now just commenting it out.

			// new() { Name="Tile Editor Tool",
			// 	Icon="hammer/tile_grid_tool_icon.png",
			// 	ToggledIcon="hammer/tile_grid_tool_icon_activated.png",
			// 	Group = "MainTools",
			// 	GroupType = ToolBarOptionGroupType.SingleExclusive,
			// 	Checkable=true,
			// 	Description="Edit tiled surfaces" },

			// new() { Name="Vertex Normal Paint",
			// 	Icon="hammer/vertex_normal_paint.png",
			// 	ToggledIcon="hammer/vertex_normal_paint_activated.png",
			// 	Group = "MainTools",
			// 	GroupType = ToolBarOptionGroupType.SingleExclusive,
			// 	Checkable=true,
			// 	Description="Paint vertex normals" },

			new() { Separator=true },
			new() { Separator=true },
		];
	}

	// --- SELECTION MODE DEFINITIONS ---
	private static List<ToolOptionDef> CreateSelectionModeDefs()
	{
		return
		[
			new() { Name="Vertices",
				Icon="hammer/selection_mode_vertices.png",
				ShortcutAction="tools.vertex-tool",
				ToggledIcon="hammer/selection_mode_vertices_activated.png",
				Hotkey="1",
				Checkable=true,
				Group="SelectionMode",
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectVertices,
				Description="Selection Mode: Vertices",
				ActiveResolver = () => EditorToolManager.CurrentModeName == nameof( MeshTool )
					&& EditorToolManager.CurrentSubModeName == nameof( VertexTool ) },

			new() { Name="Edges",
				Icon="hammer/selection_mode_edges.png",
				ToggledIcon="hammer/selection_mode_edges_activated.png",
				Hotkey="2",
				Checkable=true,
				Group="SelectionMode",
				GroupType=ToolBarOptionGroupType.SingleExclusive,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectEdges,
				Description="Selection Mode: Edges",
				ActiveResolver = () => EditorToolManager.CurrentModeName == nameof( MeshTool )
					&& EditorToolManager.CurrentSubModeName == nameof( EdgeTool ) },

			new() { Name="Faces",
				ShortcutAction = "mesh.face",
				Icon="hammer/selection_mode_faces.png",
				ToggledIcon="hammer/selection_mode_faces_activated.png",
				Hotkey="3",
				Checkable=true,
				Group="SelectionMode",
				GroupType=ToolBarOptionGroupType.SingleExclusive,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectFaces,
				Description="Selection Mode: Faces",
				ActiveResolver = () => EditorToolManager.CurrentModeName == nameof( MeshTool )
					&& EditorToolManager.CurrentSubModeName == nameof( FaceTool ) },

			new() { Name="Meshes",
				ShortcutAction = "mesh.mesh",
				Icon="hammer/selection_mode_solids.png",
				ToggledIcon="hammer/selection_mode_solids_activated.png",
				Hotkey="4",
				Checkable=true,
				Group="SelectionMode",
				GroupType=ToolBarOptionGroupType.SingleExclusive,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectMeshes,
				Description="Selection Mode: Meshes",
				ActiveResolver = () => EditorToolManager.CurrentModeName == nameof( MeshTool )
					&& EditorToolManager.CurrentSubModeName == nameof( MeshSelection ) },

			new() { Name="Objects",
				ShortcutAction = "mesh.objects",
				Icon="hammer/selection_mode_objects.png",
				ToggledIcon="hammer/selection_mode_objects_activated.png",
				Hotkey="5",
				Checkable=true,
				Group="SelectionMode",
				GroupType=ToolBarOptionGroupType.SingleExclusive,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectObjects,
				Description="Selection Mode: Objects",
				Active = true,
				ActiveResolver = () => EditorToolManager.CurrentModeName == nameof( MeshTool )
					&& EditorToolManager.CurrentSubModeName == nameof( ObjectSelection ) },

			new () { Name="Groups",
				Icon="hammer/selection_mode_groups.png",
				ToggledIcon="hammer/selection_mode_groups_activated.png",
				Hotkey="6",
				Checkable=true,
				Group="SelectionMode",
				GroupType=ToolBarOptionGroupType.SingleExclusive,
				Description="Selection Mode: Groups" },

			new() { Name="Navigation",
				ShortcutAction="tools.navmesh-tool",
				Icon="hammer/selection_mode_nav.png",
				ToggledIcon="hammer/selection_mode_nav_activated.png",
				Hotkey="8",
				Checkable=true,
				Group="SelectionMode",
				GroupType=ToolBarOptionGroupType.SingleExclusive,
				ActionType = ToolActionType.MethodCall,
				Method = EditorToolBarsActions.SelectNavigation,
				Description="Selection Mode: Navigation",
				ActiveResolver = () => EditorToolManager.CurrentModeName == nameof( NavMeshTool ) },
		];
	}

	// --- EDITING SETTINGS DEFINITIONS ---
	private static List<ToolOptionDef> CreateEditingSettingDefs()
	{
		return
	[
		new() { Name = "World Space",
			ShortcutAction = "scene.toggle-global-space",
			Icon = "hammer/coordinate_frame_world.png",
			ToggledIcon = "hammer/coordinate_frame_world_activated.png",
			Checkable = true,
			Description = "Use World Space Axes for Gizmo",
			Group = "3DTypeSpace",
			GroupType = ToolBarOptionGroupType.SingleExclusive,
			ActionType = ToolActionType.PropertySet,
			SetterAction = () => EditorScene.GizmoSettings.GlobalSpace = true,
			Active = true,
			ActiveResolver = () => EditorScene.GizmoSettings.GlobalSpace },

		new() { Name = "Local Space",
			ShortcutAction = "scene.toggle-local-space",
			Icon = "hammer/coordinate_frame_local.png",
			ToggledIcon = "hammer/coordinate_frame_local_activated.png",
			Checkable = true,
			Description = "Use Selection's Local Space Axes for Gizmo",
			Group = "3DTypeSpace",
			ActionType = ToolActionType.PropertySet,
			SetterAction = () => EditorScene.GizmoSettings.GlobalSpace = false,
			GroupType = ToolBarOptionGroupType.SingleExclusive,
			ActiveResolver = () => !EditorScene.GizmoSettings.GlobalSpace },
/*
		new() { Name = "Pick Workplane",
			Icon = "hammer/workplane_tool_icon.png",
			ToggledIcon = "hammer/workplane_tool_icon_activated.png",
			Checkable = true,
			Hotkey = "Shift+Q",
			Description = "Pick Workplane from objects and surfaces" },

		new() { Name = "Reset Workplane",
			Icon = "hammer/workplane_reset_icon.png",
			ToggledIcon = "hammer/workplane_reset_icon_activated.png",
			Hotkey = "Alt+Shift+Q",
			Checkable = false,
			GroupType = ToolBarOptionGroupType.ExternallyControlled,
			ConditionalOn = "Pick Workplane",
			Description = "Reset the workplane when custom workplane is active" },
*/
		new() { Separator=true },

		new() { Name = "Texture Lock",
			Icon = "hammer/toggle_texture_lock_component.png",
			ToggledIcon = "hammer/toggle_texture_lock_component_activated.png",
			Checkable = true,
			Hotkey = "Ctrl+Shift+Y",
			Description = "Texture Lock",
			ActionType = ToolActionType.PropertyToggle,
			Getter = () => EditorToolBarsActions.TextureLock,
			Setter = v => EditorToolBarsActions.TextureLock = v,
			ActiveResolver = () => EditorToolBarsActions.TextureLock},

		new() { Name = "Texture Lock Scale",
			Icon = "hammer/toggle_texture_lock_scale.png",
			ToggledIcon = "hammer/toggle_texture_lock_scale_activated.png",
			Checkable = true,
			Description = "Texture Lock Scale",
			ActionType = ToolActionType.PropertyToggle,
			Getter = () => EditorToolBarsActions.TextureLockScale,
			Setter = v => EditorToolBarsActions.TextureLockScale = v,
			ActiveResolver = () => EditorToolBarsActions.TextureLockScale},

		new() { Name = "Texture Lock Component",
			Icon = "hammer/toggle_texture_lock_component.png",
			ToggledIcon = "hammer/toggle_texture_lock_component_activated.png",
			Checkable = true,
			Description = "Texture Lock Component Manipulations",
			ActionType = ToolActionType.PropertyToggle,
			Getter = () => EditorToolBarsActions.TextureLockComponent,
			Setter = v => EditorToolBarsActions.TextureLockComponent = v,
			ActiveResolver = () => EditorToolBarsActions.TextureLockComponent},

		new() { Name = "Lasso Through",
			Icon = "hammer/toggle_select_through.png",
			ToggledIcon = "hammer/toggle_select_through_activated.png",
			Checkable = true,
			Hotkey = "Ctrl+Shift+L",
			Description = "Enable Lasso Select Through",
			ActionType = ToolActionType.PropertyToggle,
			Getter = () => EditorToolBarsActions.SelectionThrough,
			Setter = v => EditorToolBarsActions.SelectionThrough = v,
			ActiveResolver = () => EditorToolBarsActions.SelectionThrough},

		new() { Name = "Lasso Partial",
			Icon = "hammer/toggle_select_intersecting.png",
			ToggledIcon = "hammer/toggle_select_intersecting_activated.png",
			Checkable = true,
			Description = "Enable Lasso Select Partial Intersections",
			ActionType = ToolActionType.PropertyToggle,
			Getter = () => EditorToolBarsActions.LassoPartialSelection,
			Setter = v => EditorToolBarsActions.LassoPartialSelection = v,
			ActiveResolver = () => EditorToolBarsActions.LassoPartialSelection},

		new () {
		Name = "Backface Select",
			Icon = "hammer/toggle_select_backfacing.png",
			Checkable = true,
			Hotkey = "Ctrl+Shift+F9",
			Description = "Enable Backface Selection" ,
			ActionType = ToolActionType.PropertyToggle,
			Getter = () => EditorPreferences.BackfaceSelection,
			Setter = v => EditorPreferences.BackfaceSelection = v,
			ActiveResolver = () => EditorPreferences.BackfaceSelection},

		new () { Separator=true },
		new () { Separator=true },
		new () { Separator=true },

		new () {
		Name = "Toggle Fullscreen",
			ShortcutAction = "editor.eject",
			Icon = "hammer/fullscreen_activated.png",
			Hotkey = "F3",
			Description = "Toggle Fullscreen",
			Checkable = false,
			ActionType = ToolActionType.MethodCall,
			Method = () => ViewportTools.ToggleFullscreen(),
			DisableDuringPlay = false,
			ConditionalOn = "Run Game"
		},

		new () { Separator=true },

		new () {
	Name = "Popup Game Window",
			Icon = "hammer/popup_game_window.png",
			ToggledIcon = "hammer/popup_game_window_activated.png",
			Checkable = true,
			Description = "Run game in a separate popup window",
			ActionType = ToolActionType.PropertyToggle,
			Getter = () => SceneViewWidget.UsePopupGameWindow,
			Setter = v => SceneViewWidget.UsePopupGameWindow = v,
			DisableDuringPlay = false,
			ActiveResolver = () => SceneViewWidget.UsePopupGameWindow
},

		new () { Separator=true },

		new () {
	Name = "Run Game",
			ShortcutAction = "editor.toggle-play",
			Icon = "hammer/play_map.png",
			ToggledIcon = "hammer/play_map_activated.png",
			Hotkey = "F5",
			Description = "Run Game",
			ActionType = ToolActionType.MethodCall,
			Method = () =>
			{
		if ( !Game.IsPlaying )
		{
			EditorScene.Play( SceneViewWidget.Current.Session );
		}
		else
		{
			EditorScene.Stop();
		}
	},
			DisableDuringPlay = false,
			Checkable = true,
			ActiveResolver = () => Game.IsPlaying
},

		new () {
	Name = "Pause Game",
			ShortcutAction = "editor.pause",
			Icon = "hammer/pause_map.png",
			ToggledIcon = "hammer/pause_map_activated.png",
			Hotkey = "F6",
			Description = "Pause Game",
			Checkable = true,
			GroupType = ToolBarOptionGroupType.ConditionalClearState,
			ActionType = ToolActionType.PropertySet,
			SetterAction = () => Game.IsPaused = !Game.IsPaused,
			DisableDuringPlay = false,
			ConditionalOn = "Run Game",
			ActiveResolver = () => Game.IsPaused
},

		new () {
	Name = "Eject",
			ShortcutAction = "editor.eject",
			Icon = "hammer/eject.png",
			ToggledIcon = "hammer/eject_activated.png",
			Hotkey = "F7",
			Description = "Eject",
			Checkable = true,
			GroupType = ToolBarOptionGroupType.ConditionalClearState,
			ActionType = ToolActionType.MethodCall,
			Method = () => SceneViewWidget.Current.ToggleEject(),
			DisableDuringPlay = false,
			ConditionalOn = "Run Game",
			ActiveResolver = () => SceneViewWidget.Current?.CurrentView == SceneViewWidget.ViewMode.GameEjected
},

		new () { Separator=true },
		new () { Separator=true },
		new () {
	Name = "Compile Map",
			ShortcutAction = "editor.compile",
			Icon = "hammer/run_map.png",
			Description = "Compile Map",
			Checkable = false,
			ActionType = ToolActionType.MethodCall,
			Method = () => SceneCompilerWindow.Open(),
			DisableDuringPlay = false,
			ConditionalOn = "Run Game"
		},
		new () {
	Name = "Graphics Quality",
			Icon = "hammer/gfx_quality_activated.png",
			Description = "Graphics Quality",
			Checkable = false,
			ActionType = ToolActionType.MethodCall,
			Method = () => SceneViewWidget.Current.ViewportTools.OpenQualityMenu(),
			DisableDuringPlay = false,
			ConditionalOn = "Run Game"
		},
		new () {
	Name = "Network Settings",
			ShortcutAction = "editor.eject",
			Icon = "hammer/network_settings_activated.png",
			Description = "Network Settings",
			Checkable = false,
			ActionType = ToolActionType.MethodCall,
			Method = () => SceneViewWidget.Current.ViewportTools.OpenNetworkSettings(),
			DisableDuringPlay = false,
			ConditionalOn = "Run Game"
		},
	];
	}

	// --- VIEW SETTINGS DEFINITIONS ---
	private static List<ToolOptionDef> CreateViewSettingDefs()
	{
		return
		[
		/*
			new() { Name="Show Helpers",
				Icon="hammer/toggle_show_helpers.png",
				ToggledIcon="hammer/toggle_show_helpers_activated.png",
				Checkable=true,
				Hotkey="Ctrl+Shift+H",
				Description="Show Helpers" },
*/
			new() { Name="Editor Objects",
				Icon="hammer/toggle_editor_objects.png",
				ToggledIcon="hammer/toggle_editor_objects_activated.png",
				Checkable=true,
				Hotkey="Shift+O",
				GroupType = ToolBarOptionGroupType.ConditionalPreserveState,
				ActionType = ToolActionType.PropertyToggle,
				Getter = () => EditorScene.GizmoSettings.GizmosEnabled,
				Setter = v => EditorScene.GizmoSettings.GizmosEnabled = v,
				ActiveResolver = () => EditorScene.GizmoSettings.GizmosEnabled,
				Description="Show Editor Only Objects" },
/*
			new() { Name="Tools Materials",
				Icon="hammer/toggle_tools_materials.png",
				ToggledIcon="hammer/toggle_tools_materials_activated.png",
				Checkable=true,
				Hotkey="Ctrl+Shift+F2",
				GroupType = ToolBarOptionGroupType.ConditionalPreserveState,
				ConditionalOn = "Editor Objects",
				Description="Show Tools Materials (disabled if Editor Objects is off)" },

			new() { Name="Force Lights On",
				Icon="hammer/toggle_force_lights_on.png",
				ToggledIcon="hammer/toggle_force_lights_on_activated.png",
				Checkable=true,
				Description="Force all lights to be on in the editor even if disabled in the game" },

			new() { Name="Visibility Contributors",
				Icon="hammer/toggle_vis_preview.png",
				ToggledIcon="hammer/toggle_vis_preview_activated.png",
				Checkable=true,
				Description="Hide objects and materials which do not contribute to vis" },
*/
			new() { Name="Show Collision",
				Icon="hammer/toggle_collision_hulls.png",
				ToggledIcon="hammer/toggle_collision_hulls_activated.png",
				Checkable=true,
				Hotkey="Ctrl+Shift+F3",
				ActionType = ToolActionType.PropertySet,
				SetterAction = () => DebuggingMenus.ShowPhysicsDebug = !DebuggingMenus.ShowPhysicsDebug,
				Description="Show Collision Models" },

			new() { Name="Selection Overlay",
				Icon="hammer/toggle_selection_overlay.png",
				ToggledIcon="hammer/toggle_selection_overlay_activated.png",
				Checkable=true,
				Hotkey="Ctrl+Shift+F4",
				Description="Toggle Selection Overlay" ,
				ActionType = ToolActionType.PropertyToggle,
				Getter = () => EditorToolBarsActions.SelectionOverlay,
				Setter = v => EditorToolBarsActions.SelectionOverlay = v,
				DisableDuringPlay = false,
				ActiveResolver = () => EditorToolBarsActions.SelectionOverlay},

				// John: We won't have these for a while, commenting it out for now.

/*
			new() { Name="Gray Out Instances",
				Icon="hammer/toggle_instance_overlay.png",
				ToggledIcon="hammer/toggle_instance_overlay_activated.png",
				Checkable=true,
				Hotkey="Ctrl+Shift+F5",
				Description="Gray Out Objects Outside Instance" },		
			
			new() { Name="Mesh Subdivision",
				Icon="hammer/toggle_mesh_subdivision.png",
				ToggledIcon="hammer/toggle_mesh_subdivision_activated.png",
				Checkable=true,
				Hotkey="Ctrl+Shift+F6",
				Description="Toggle Mesh Subdivision" },

			new() { Name="Mesh Tiles 3D",
				Icon="hammer/toggle_mesh_tiles_3d.png",
				ToggledIcon="hammer/toggle_mesh_tiles_3d_activated.png",
				Checkable=true,
				Hotkey="Ctrl+Shift+F7",
				Description="Toggle Mesh Tiles in 3D View" },

			new() { Name="Mesh Tiles 2D",
				Icon="hammer/toggle_mesh_tiles_2d.png",
				ToggledIcon="hammer/toggle_mesh_tiles_2d_activated.png",
				Checkable=true,
				Hotkey="Ctrl+Shift+F8",
				Description="Toggle Mesh Tiles in 2D View" },

			new() { Name="Model Animation",
				Icon="hammer/toggle_model_animation.png",
				ToggledIcon="hammer/toggle_model_animation_activated.png",
				Checkable=true,
				Description="Toggle Model Animation" },

			new() { Name="Restart Particles",
				Icon="hammer/restart_particles.png",
				Checkable=false,
				Description="Restart the selected particle systems or all if none selected" },

			new() { Name="Particle Visibility",
				Icon="hammer/toggle_particles.png",
				ToggledIcon="hammer/toggle_particles_activated.png",
				Checkable=true,
				Description="Toggle Particle Visibility" },

			new() { Name="Toggle Grass",
				Icon="hammer/toggle_show_grass.png",
				ToggledIcon="hammer/toggle_show_grass_activated.png",
				Checkable=true,
				Description="Toggle Grass" },

			new() { Separator=true },

			new() { Name="Cycle View Distance",
				Icon="hammer/view_distance_short.png",
				IconCycle =
				[
					"hammer/view_distance_med_short.png",
					"hammer/view_distance_med.png",
					"hammer/view_distance_med_long.png",
					"hammer/view_distance_long.png",
					"hammer/view_distance_short.png"
				],
				Description="Cycle View Distance",
				Checkable=false },
	*/			
		];
	}

}
