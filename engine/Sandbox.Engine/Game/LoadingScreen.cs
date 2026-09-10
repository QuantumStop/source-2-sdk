namespace Sandbox;

/// <summary>
/// Holds metadata and raw data relating to a Saved Game.
/// </summary>
public static class LoadingScreen
{
	private static bool _loading;
	private static float _visibleSince;
	private static int _visibilityCycle;

	public enum Context
	{
		Unknown,
		Startup,
		SceneTransition,
		NetworkConnect,
		EditorPlay
	}

	/// <summary>
	/// Current context for the visible loading overlay (used to select visuals/default gating policy).
	/// </summary>
	public static Context CurrentContext { get; internal set; } = Context.Unknown;

	/// <summary>
	/// Optional fully qualified panel type name to use for the current loading overlay.
	/// If null/empty, default overlay visuals are used.
	/// </summary>
	public static string OverlayPanelTypeName { get; internal set; }

	/// <summary>
	/// Timestamp (<see cref="RealTime.Now"/>) for when the loading overlay actually became visible to the UI system.
	/// This is set by the UI system (not the scene loader) so minimum-visible durations can be based on what the
	/// player can see (eg, after a native splash screen has finished).
	/// </summary>
	public static float VisibleSince => _visibleSince;

	/// <summary>
	/// Identifies the current visible loading cycle so an old scene cannot dismiss a newer one.
	/// </summary>
	internal static int VisibilityCycle => _visibilityCycle;

	/// <summary>
	/// Called by the UI system when the loading overlay is actually being rendered.
	/// </summary>
	public static void MarkBecameVisible()
	{
		_visibleSince = RealTime.Now;
	}

	/// <summary>
	/// Called by the UI system when the loading overlay is no longer being rendered.
	/// </summary>
	public static void ClearVisibleTimestamp()
	{
		_visibleSince = 0.0f;
	}

	internal static void EnsureContext( Context ctx )
	{
		if ( CurrentContext == Context.Unknown )
			CurrentContext = ctx;
	}

	/// <summary>
	/// Prime the loading overlay selection before a scene load begins. This is useful for project-side
	/// "pre-warm" effects (fade-ins/blur/zoom) where you want to show the same overlay panel that will
	/// be used for the real load, without restarting animations when the load begins.
	/// </summary>
	public static void PrimeOverlay( Context context, string overlayPanelTypeName = null )
	{
		CurrentContext = context;
		OverlayPanelTypeName = overlayPanelTypeName;
	}

	internal static void ClearContext()
	{
		CurrentContext = Context.Unknown;
		OverlayPanelTypeName = null;
		_visibleSince = 0.0f;
	}

	/// <summary>
	/// Default minimum duration (in seconds) to keep the loading screen visible for scene loads.
	/// </summary>
	public static float DefaultMinimumVisibleSeconds { get; set; } = 0.0f;

	/// <summary>
	/// Default behavior for requiring input to continue once a scene finishes loading.
	/// </summary>
	public static bool DefaultRequireInputToContinue { get; set; } = false;

	/// <summary>
	/// Default input action name used when <see cref="DefaultRequireInputToContinue"/> is true.
	/// If null or whitespace, any key or input action press will continue.
	/// </summary>
	public static string DefaultContinueInputAction { get; set; }

	/// <summary>
	/// True when a scene load has finished and the engine is waiting for player input to continue.
	/// This is primarily for UI to display a "press any button" prompt.
	/// </summary>
	public static bool IsAwaitingInput { get; internal set; }

#if DEBUG
	/// <summary>
	/// Preview a loading overlay without starting a load. 0 disables the preview, 1 shows the
	/// configured scene-transition overlay, and 2 shows the engine default overlay.
	/// </summary>
	[ConVar( "loading_overlay_debug", Help = "Preview loading overlays: 0 = off, 1 = scene transition, 2 = engine default" )]
	public static int DebugOverlayMode { get; set; }

	internal static int EffectiveDebugOverlayMode => Math.Clamp( DebugOverlayMode, 0, 2 );
#else
	internal static int EffectiveDebugOverlayMode => 0;
#endif

	public static bool IsVisible
	{
		get => _loading || EffectiveDebugOverlayMode != 0;
		set
		{
			if ( _loading == value )
				return;

			//Log.Info( $"Loading: {value}\n{new StackTrace( true ).ToString()}" );

			_loading = value;

			if ( _loading )
			{
				_visibilityCycle++;
				_visibleSince = 0.0f;
			}

			if ( !_loading )
			{
				Tasks.Clear();
				IsAwaitingInput = false;
				_visibleSince = 0.0f;
			}
		}
	}

	/// <summary>
	/// A title to show
	/// </summary>
	public static string Title { get; set; } = "Loading..";

	/// <summary>
	/// A subtitle to show
	/// </summary>
	public static string Subtitle { get; set; } = "";

	/// <summary>
	/// A URL or filepath to show as the background image.
	/// </summary>
	public static string Media { get; set; }

	/// <summary>
	/// A list of tasks that are currently being awaited during loading.
	/// </summary>
	public static List<LoadingContext> Tasks { get; } = [];

	/// <summary>
	/// Called by the scene system to tell us about the loading tasks
	/// </summary>
	internal static void UpdateLoadingTasks( List<LoadingContext> incoming )
	{
		Tasks.Clear();

		if ( incoming.Count > 0 )
		{
			Tasks.AddRange( incoming );
		}
	}

}
