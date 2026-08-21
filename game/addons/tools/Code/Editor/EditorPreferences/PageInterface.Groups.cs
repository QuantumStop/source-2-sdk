namespace Editor.Preferences;

internal partial class PageInterface
{
	private const string SBoxIcon = "toolimages:logo_rounded.png";
	private const string S2Icon = "toolimages:logo_s2.png";


	// Populate this!
	static void RegisterGroups()
	{
		RegisterGroup( ViewGroup );
		RegisterGroup( AssetBrowserGroup );
		RegisterGroup( ToolbarsGroup );
	}

	// John: Free example for those who want to make their own groups :)
	private static void ToolbarsGroup( Layout layout )
	{
		var toolbarsGroup = new CollapsibleCategory( null, "S2 Styled Toolbars" );
		toolbarsGroup.Container.Layout.Spacing = 0;
		layout.Add( toolbarsGroup );

		var toolbarOptions = new List<string>
		{
			"QT Toolbars",
			"Legacy (Viewport)"
		};
		var toolbarOptionIcons = new List<string>
		{
			S2Icon,
			SBoxIcon
		};
		var selectedToolbarIndex = CustomEditorPreferences.ToolbarMode == CustomEditorPreferences.ViewportToolbarMode.LegacySbox ? 1 : 0;

		AddSegmentedRow(
			toolbarsGroup.Container.Layout,
			"Toolbars",
			toolbarOptions,
			selectedToolbarIndex,
			index =>
			{
				var newMode = index == 1
					? CustomEditorPreferences.ViewportToolbarMode.LegacySbox
					: CustomEditorPreferences.ViewportToolbarMode.Source2Styled;
				var changed = CustomEditorPreferences.ToolbarMode != newMode;

				CustomEditorPreferences.ToolbarMode = newMode;
				CustomEditorPreferences.BuildToolbarsOnStartup = true;

				if ( changed )
				{
					EditorToolBars.ApplyCurrentToolbarMode();
				}
			},
			toolbarOptionIcons );

		AddActionRow( toolbarsGroup.Container.Layout, "Force Rebuild S2 Toolbars", "autorenew", () => EditorToolBars.RebuildToolbars() );
	}

	private static void ViewGroup( Layout layout )
	{
		var viewGroup = new CollapsibleCategory( null, "View" );
		viewGroup.Container.Layout.Spacing = 0;
		layout.Add( viewGroup );

		var options = new List<string>
		{
			"Fly/Static RMB",
			"Legacy"
		};
		var optionIcons = new List<string>
		{
			S2Icon,
			SBoxIcon
		};

		var selectedIndex = CustomEditorPreferences.FlyModeStyle == CustomEditorPreferences.ViewFlyMode.LegacyHoldFlyEyeCursor ? 1 : 0;

		AddSegmentedRow(
			viewGroup.Container.Layout,
			"Fly Navigation",
			options,
			selectedIndex,
			index => CustomEditorPreferences.FlyModeStyle = index == 1
				? CustomEditorPreferences.ViewFlyMode.LegacyHoldFlyEyeCursor
				: CustomEditorPreferences.ViewFlyMode.Source2HybridFly,
			optionIcons );

		var overlayOptions = new List<string>
		{
			"Focus/Eject Overlay",
			"Disabled (Legacy)"
		};
		var overlayOptionIcons = new List<string>
		{
			S2Icon,
			SBoxIcon
		};
		var overlaySelectedIndex = CustomEditorPreferences.ShowViewportStateOverlay ? 0 : 1;

		AddSegmentedRow(
			viewGroup.Container.Layout,
			"Viewport State Overlay",
			overlayOptions,
			overlaySelectedIndex,
			index => CustomEditorPreferences.ShowViewportStateOverlay = index == 0,
			overlayOptionIcons );
	}

	private static void AssetBrowserGroup( Layout layout )
	{
		var assetBrowserGroup = new CollapsibleCategory( null, "Asset Browser" );
		assetBrowserGroup.Container.Layout.Spacing = 0;
		layout.Add( assetBrowserGroup );

		var options = new List<string>
		{
			"Default",
			"No Cloud",
			"Disabled"
		};
		var optionIcons = new List<string>
		{
			"folder_open",
			"cloud",
			"disabled_by_default"
		};
		var selectedIndex = (int)CustomEditorPreferences.AssetBrowserSidebar;

		AddSegmentedRow(
			assetBrowserGroup.Container.Layout,
			"Sidebar",
			options,
			selectedIndex,
			index =>
			{
				CustomEditorPreferences.AssetBrowserSidebar = (CustomEditorPreferences.AssetBrowserSidebarMode)index;
				global::Editor.MainAssetBrowser.ApplyAssetBrowserSidebarPreferenceToMain();
			},
			optionIcons );
	}
}
