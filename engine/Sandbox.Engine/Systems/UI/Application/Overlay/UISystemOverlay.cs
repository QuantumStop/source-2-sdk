using Sandbox;
using Sandbox.Engine;

namespace Sandbox.UI.Overlay;

public sealed class UISystemOverlay : RootPanel
{
	public static UISystemOverlay Instance { get; private set; }

	Panel _loadingOverlayPanel;
	string _loadingOverlayKey;
	bool _loadingVisibleLastTick;
	int _loadingVisibilityCycle;
	int _loadingDebugMode;

	public static void Init()
	{
		if ( Instance.IsValid() )
			return;

		Shutdown();
		Instance = new UISystemOverlay();
	}

	public static void Shutdown()
	{
		Instance?.Delete();
		Instance = null;
	}

	public UISystemOverlay()
	{
		// This persistent overlay is submitted by Graphics.OnLayer's final overlay stage,
		// matching the menu overlay path used by sbox-public.
		RenderedManually = true;
		_loadingDebugMode = LoadingScreen.EffectiveDebugOverlayMode;

		// Don't choose the panel once in the constructor - the desired overlay can change per load.
		EnsureLoadingOverlayPanel();
	}

	internal static void RenderFinalOverlay()
	{
		if ( !Instance.IsValid() )
			return;

		Instance.RenderManual();
	}

	public override void Tick()
	{
		var debugMode = LoadingScreen.EffectiveDebugOverlayMode;
		var debugSelectionChanged = debugMode != _loadingDebugMode;
		_loadingDebugMode = debugMode;

		// The root is persistent. Select its content independently from visibility, then only
		// treat the overlay as presented once LoadingScreen itself is visible.
		EnsureLoadingOverlayPanel( debugSelectionChanged );
		base.Tick();

		if ( LoadingScreen.IsVisible )
		{
			if ( !_loadingVisibleLastTick
				|| debugSelectionChanged
				|| _loadingVisibilityCycle != LoadingScreen.VisibilityCycle
				|| LoadingScreen.VisibleSince <= 0.0f )
			{
				LoadingScreen.MarkBecameVisible();
			}

			_loadingVisibleLastTick = true;
			_loadingVisibilityCycle = LoadingScreen.VisibilityCycle;
		}
		else
		{
			if ( _loadingVisibleLastTick )
			{
				LoadingScreen.ClearVisibleTimestamp();
			}

			_loadingVisibleLastTick = false;
		}
	}

	void EnsureLoadingOverlayPanel( bool allowVisiblePanelSwap = false )
	{
		// Clearing the loading context begins the selected panel's outro; it does not hand
		// ownership back to the fallback. Keep the current panel until another loading
		// context explicitly selects what should be shown next.
		if ( !LoadingScreen.IsVisible
			&& LoadingScreen.CurrentContext == LoadingScreen.Context.Unknown
			&& _loadingOverlayPanel.IsValid() )
		{
			return;
		}

		var desiredKey = GetDesiredOverlayKey();
		if ( desiredKey == _loadingOverlayKey && _loadingOverlayPanel.IsValid() )
			return;

		// Once the loading overlay is visible, don't swap/recreate the panel mid-visibility phase.
		// This prevents intro/fade animations from restarting when the loading system assigns overlay
		// metadata slightly later (or when projects pre-warm the overlay before starting a load).
		if ( LoadingScreen.IsVisible
			&& !allowVisiblePanelSwap
			&& _loadingVisibleLastTick
			&& _loadingVisibilityCycle == LoadingScreen.VisibilityCycle
			&& _loadingOverlayPanel.IsValid() )
		{
			return;
		}

		var panel = TryCreateDesiredOverlayPanel( desiredKey );
		if ( panel is null )
		{
			// If we couldn't create the desired panel (eg type not enrolled yet), keep the existing one
			// but allow retry on subsequent ticks. If we don't have any panel yet, show the engine default
			// rather than leaving the loading screen blank.
			if ( !_loadingOverlayPanel.IsValid() )
			{
				_loadingOverlayPanel = CreateDefaultOverlayPanel();
				_loadingOverlayKey = "default";
				AddChild( _loadingOverlayPanel );
			}

			return;
		}

		_loadingOverlayPanel?.Delete( true );
		_loadingOverlayPanel = panel;
		_loadingOverlayKey = desiredKey;
		AddChild( _loadingOverlayPanel );
	}

	string GetDesiredOverlayKey()
	{
		if ( _loadingDebugMode == 2 )
			return "default";

		if ( _loadingDebugMode == 1 )
		{
			var transitionOverlay = ProjectSettings.Loading?
				.GetPolicy( LoadingSettings.LoadingContext.SceneTransition )
				.OverlayPanelTypeName;

			return string.IsNullOrWhiteSpace( transitionOverlay )
				? "default"
				: $"type:{transitionOverlay}";
		}

		// Explicit per-context selection wins.
		if ( !string.IsNullOrWhiteSpace( LoadingScreen.OverlayPanelTypeName ) )
			return $"type:{LoadingScreen.OverlayPanelTypeName}";

		// If the caller didn't explicitly set a panel type name, try to infer one from project loading settings
		// based on the active loading context. This avoids a one-frame fallback to the default overlay when
		// LoadingScreen.IsVisible is toggled before the overlay type is assigned.
			if ( LoadingScreen.IsVisible || LoadingScreen.CurrentContext != LoadingScreen.Context.Unknown )
			{
				var settingsContext = LoadingScreen.CurrentContext switch
				{
					LoadingScreen.Context.Startup => LoadingSettings.LoadingContext.Startup,
					LoadingScreen.Context.NetworkConnect => LoadingSettings.LoadingContext.NetworkConnect,
					LoadingScreen.Context.EditorPlay => LoadingSettings.LoadingContext.EditorPlay,
					_ => LoadingSettings.LoadingContext.SceneTransition
				};

			var inferred = ProjectSettings.Loading?.GetPolicy( settingsContext ).OverlayPanelTypeName;
			if ( !string.IsNullOrWhiteSpace( inferred ) )
				return $"type:{inferred}";

			// If we already have a typed overlay active during this visible phase, keep it rather than swapping
			// back to the legacy/default overlay due to transient state changes.
			if ( _loadingVisibleLastTick && _loadingOverlayKey?.StartsWith( "type:" ) == true && _loadingOverlayPanel.IsValid() )
				return _loadingOverlayKey;
		}

		return "default";
	}

	Panel TryCreateDesiredOverlayPanel( string desiredKey )
	{
		// When a panel type is explicitly requested (key starts with type:), we should only succeed if we can
		// actually create that panel. Falling back to the base overlay while still claiming the type key would
		// "lock in" the fallback for the rest of the visible phase.
		if ( desiredKey?.StartsWith( "type:", StringComparison.Ordinal ) == true )
		{
			var typeName = desiredKey["type:".Length..];
			return TryCreatePanelByTypeName( typeName );
		}

		return CreateDefaultOverlayPanel();
	}

	Panel TryCreatePanelByTypeName( string typeName )
	{
		if ( string.IsNullOrWhiteSpace( typeName ) )
			return null;

		var panelType = Game.TypeLibrary?.GetType( typeof( Panel ), typeName, preferAddonAssembly: true, exactFullName: true )
			?? Sandbox.Internal.GlobalGameNamespace.TypeLibrary?.GetType( typeof( Panel ), typeName, preferAddonAssembly: true, exactFullName: true )
			?? Game.TypeLibrary?.GetType( typeof( Panel ), typeName, preferAddonAssembly: true )
			?? Sandbox.Internal.GlobalGameNamespace.TypeLibrary?.GetType( typeof( Panel ), typeName, preferAddonAssembly: true );

		var panel = panelType?.Create<Panel>();
		if ( panel is not null )
		{
			LoadConventionStylesheet( panel );
			return panel;
		}

		var runtimeType = AppDomain.CurrentDomain.GetAssemblies()
			.Select( asm => asm.GetType( typeName, throwOnError: false, ignoreCase: false ) )
			.FirstOrDefault( type => type is not null && !type.IsAbstract && typeof( Panel ).IsAssignableFrom( type ) );

		panel = runtimeType is null ? null : Activator.CreateInstance( runtimeType ) as Panel;
		LoadConventionStylesheet( panel );
		return panel;
	}

	void LoadConventionStylesheet( Panel panel )
	{
		if ( panel is null )
			return;

		var fullName = panel.GetType().FullName;
		var typeDesc = Game.TypeLibrary?.GetType( panel.GetType() )
			?? Sandbox.Internal.GlobalGameNamespace.TypeLibrary?.GetType( panel.GetType() );

		var classFileLocation = typeDesc?.GetAttributes<Sandbox.Internal.ClassFileLocationAttribute>()
			.MinBy( x => x.Path.Length );

		if ( classFileLocation is not null )
		{
			panel.StyleSheet.Load( classFileLocation.Path + ".scss", true, failSilently: true );
			return;
		}

		panel.StyleSheet.Load( $"UI/Loading/{panel.GetType().Name}.razor.scss", true, failSilently: true );

		if ( !string.IsNullOrWhiteSpace( fullName ) )
		{
			panel.StyleSheet.Load( fullName.Replace( '.', '/' ) + ".razor.scss", true, failSilently: true );
		}
	}

	Panel CreateDefaultOverlayPanel()
	{
		var panel = new Overlays.LoadingOverlay();

		// Keep fallback styling local to the fallback panel rather than adding it to the
		// shared overlay root inherited by project-provided loading panels.
		panel.StyleSheet.Load( "UISystem/Overlays/LoadingOverlay.razor.scss", true, failSilently: true );
		return panel;
	}

	protected override void UpdateScale( Rect screenSize )
	{
		Scale = Screen.DesktopScale;

		var minimumHeight = 1080.0f * Screen.DesktopScale;

		if ( screenSize.Height < minimumHeight )
		{
			Scale *= screenSize.Height / minimumHeight;
		}
	}
}
