namespace Editor;

using Editor;
using Sandbox;
using Sandbox.UI;

public static partial class EditorToolBars
{
	public static ToolBar MainTools;
	public static ToolBar SelectionModes;
	public static ToolBar EditingSettings;
	public static ToolBar ViewSettings;

	public static void BuildNative( DockWindow window )
	{
		BuildMainTools( window );
		BuildSelectionModes( window );
		BuildEditingSettings( window );
		BuildViewSettings( window );
	}

	// MAIN TOOLS
	private static void BuildMainTools( DockWindow window )
	{
		var dock = window.DockManager;
		MainTools = new ToolBar( window, "Main Tools" );
		MainTools.SetIconSize( new Vector2( 32, 32 ) );

		AddDefs( MainTools, MainToolDefs, singleSelect: true );

		dock.RegisterDockType( "Editor - Main Tools", "hammer/appicon.ico", () => MainTools );
		window.AddToolBar( MainTools, ToolbarPosition.Left );
		RegisterToolBar( "ViewSettings", ViewSettings, window );
	}

	// SELECTION MODES
	private static void BuildSelectionModes( DockWindow window )
	{
		var dock = window.DockManager;
		SelectionModes = new ToolBar( window, "Selection Modes" );
		SelectionModes.SetIconSize( 24 );
		SelectionModes.ButtonStyle = ToolButtonStyle.TextBesideIcon;

		Label label = new( SelectionModes )
		{
			Text = "Select: ",
			Color = Theme.Text
		};
		SelectionModes.AddWidget( label );

		AddDefs( SelectionModes, SelectionModeDefs, singleSelect: true );

		dock.RegisterDockType( "Editor - Selection Modes", "hammer/appicon.ico", () => SelectionModes );
		window.AddToolBar( SelectionModes, ToolbarPosition.Top );
		RegisterToolBar( "SelectionModes", SelectionModes, window );
	}

	// EDITING SETTINGS
	private static void BuildEditingSettings( DockWindow window )
	{
		var dock = window.DockManager;
		EditingSettings = new ToolBar( window, "Editing Settings" );
		SelectionModes.SetIconSize( 22 );

		Label label = new( EditingSettings )
		{
			Text = "Editing: ",
			Color = Theme.Text
		};
		EditingSettings.AddWidget( label );

		AddDefs( EditingSettings, EditingSettingDefs );

		dock.RegisterDockType( "Editor - Editing Settings", "hammer/appicon.ico", () => EditingSettings );
		window.AddToolBar( EditingSettings, ToolbarPosition.Top );
		RegisterToolBar( "EditingSettings", EditingSettings, window );
	}

	// VIEW SETTINGS
	private static void BuildViewSettings( DockWindow window )
	{
		var dock = window.DockManager;
		ViewSettings = new ToolBar( window, "View Settings" );
		SelectionModes.SetIconSize( 22 );

		Label label = new( ViewSettings )
		{
			Text = "View: ",
			Color = Theme.Text
		};
		ViewSettings.AddWidget( label );

		AddDefs( ViewSettings, ViewSettingDefs );

		dock.RegisterDockType( "Editor - View Settings", "hammer/appicon.ico", () => ViewSettings );
		window.AddToolBar( ViewSettings, ToolbarPosition.Top );
		RegisterToolBar( "ViewSettings", ViewSettings, window );
	}
}

