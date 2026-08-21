namespace Editor.Preferences;

/// <summary>
/// Custom editor preferences storage.
/// This is essentially an extension for editor additions that get stored in settings.
/// </summary>
public static partial class CustomEditorPreferences
{
	public enum ViewportToolbarMode
	{
		Source2Styled = 0,
		LegacySbox = 1
	}

	public enum ViewFlyMode
	{
		Source2HybridFly = 0,
		Source2HybridFlyAlt = 1,
		LegacyHoldFlyEyeCursor = 2
	}

	public enum AssetBrowserSidebarMode
	{
		EnabledFull = 0,
		EnabledNoCloud = 1,
		Disabled = 2
	}

	private const string Prefix = "custom_editor_prefs.";

	[Title( "Viewport Toolbar Mode" )]
	public static ViewportToolbarMode ToolbarMode
	{
		get
		{
			var raw = Get( "toolbars.viewport_mode", -1 );
			if ( raw >= 0 )
			{
				return (ViewportToolbarMode)raw;
			}

			var legacy = Get( "toolbars.show_legacy_viewport", true );
			return legacy ? ViewportToolbarMode.LegacySbox : ViewportToolbarMode.Source2Styled;
		}
		set
		{
			Set( "toolbars.viewport_mode", (int)value );
			Set( "toolbars.show_legacy_viewport", value == ViewportToolbarMode.LegacySbox );
		}
	}

	[Title( "Show Legacy Viewport Toolbar" )]
	public static bool ShowLegacyViewportToolbar
	{
		get => ToolbarMode == ViewportToolbarMode.LegacySbox;
		set => ToolbarMode = value ? ViewportToolbarMode.LegacySbox : ViewportToolbarMode.Source2Styled;
	}

	[Title( "Build Toolbars On Startup" )]
	public static bool BuildToolbarsOnStartup
	{
		get => Get( "toolbars.build_on_startup", false );
		set => Set( "toolbars.build_on_startup", value );
	}

	[Title( "Fly Mode Style" )]
	public static ViewFlyMode FlyModeStyle
	{
		get => (ViewFlyMode)Get( "view.fly_mode_style", (int)ViewFlyMode.LegacyHoldFlyEyeCursor );
		set => Set( "view.fly_mode_style", (int)value );
	}

	[Title( "Show Viewport Focus/Ejected Overlay" )]
	public static bool ShowViewportStateOverlay
	{
		get => Get( "view.show_viewport_state_overlay", true );
		set => Set( "view.show_viewport_state_overlay", value );
	}

	[Title( "Asset Browser Sidebar" )]
	public static AssetBrowserSidebarMode AssetBrowserSidebar
	{
		get
		{
			var raw = Get( "asset_browser.sidebar", -1 );
			if ( raw >= 0 )
			{
				return (AssetBrowserSidebarMode)raw;
			}

			return Get( "cloud_services.show_sidebar", true )
				? AssetBrowserSidebarMode.EnabledFull
				: AssetBrowserSidebarMode.Disabled;
		}
		set => Set( "asset_browser.sidebar", (int)value );
	}

	public static T Get<T>( string key, T defaultValue = default )
	{
		return EditorCookie.Get( Prefix + key, defaultValue );
	}

	public static void Set<T>( string key, T value )
	{
		EditorCookie.Set( Prefix + key, value );
	}
}
