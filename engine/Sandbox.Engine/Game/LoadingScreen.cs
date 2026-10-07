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

			// Each load's downloads are its own
			_downloads.Clear();

			if ( !value )
			{
				Progress = null;
				Package = null;
			}

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
	/// The game being loaded, once it's been looked up - so the loading screen can show its name, its
	/// art and what's new in it. Null before then, and when what's loading isn't a game.
	/// Cleared when the loading screen is hidden.
	/// </summary>
	public static Package Package { get; internal set; }

	/// <summary>
	/// A title to show
	/// </summary>
	public static string Title { get; set; } = "Loading..";

	/// <summary>
	/// A subtitle to show
	/// </summary>
	public static string Subtitle { get; set; } = "";

	/// <summary>
	/// A snapshot of the current download's progress, or null when progress is unavailable.
	/// Cleared when the loading screen is hidden.
	/// </summary>
	public static Menu.LoadingProgress? Progress { get; internal set; }

	/// <summary>
	/// Every package downloaded for the load under way, in the order they became known - for one bar
	/// across all of them, rather than <see cref="Progress"/>'s one download at a time. Packages known in
	/// advance (a server's, the game and its map) are added with their sizes before anything starts
	/// downloading; anything found later is added as it starts. Cleared when the loading screen's shown
	/// or hidden.
	/// </summary>
	public static IReadOnlyList<Menu.LoadingDownload> Downloads => _downloads;

	static readonly List<Menu.LoadingDownload> _downloads = new();

	/// <summary>
	/// The download for a package, added if it isn't there yet. Null while the loading screen's hidden -
	/// a running game downloading things isn't a load. Main thread only, like everything that touches the
	/// list - downloads update it from their main thread loop, the loading screen reads it each frame.
	/// </summary>
	internal static Menu.LoadingDownload TrackDownload( string ident, string title )
	{
		ThreadSafe.AssertIsMainThread();

		if ( !IsVisible || string.IsNullOrEmpty( ident ) )
			return null;

		var download = _downloads.FirstOrDefault( x => string.Equals( x.Ident, ident, StringComparison.OrdinalIgnoreCase ) );
		if ( download is null )
		{
			download = new Menu.LoadingDownload { Ident = ident, Title = title };
			_downloads.Add( download );
		}
		else if ( !string.IsNullOrEmpty( title ) )
		{
			download.Title = title;
		}

		return download;
	}

	/// <summary>
	/// Make room for packages about to be downloaded - each looked up and checked against the download
	/// cache, all at once, so <see cref="Downloads"/> knows the whole of it before the first byte comes
	/// down. Ones that aren't packages, or are local, are skipped. With <paramref name="withReferences"/>,
	/// everything they reference too, and everything that references - what installing them pulls in
	/// (a game's libraries and the cloud assets it uses), less anything already installed. Package info
	/// and manifests only; nothing's downloaded.
	/// </summary>
	internal static async Task ReserveDownloads( IEnumerable<string> idents, System.Threading.CancellationToken token = default, bool withReferences = false )
	{
		if ( !IsVisible ) return;

		var seen = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		bool Unseen( string ident ) { lock ( seen ) return seen.Add( ident ); }

		static bool IsPackage( string ident ) => !string.IsNullOrWhiteSpace( ident ) && !ident.StartsWith( "local.", StringComparison.OrdinalIgnoreCase );

		async Task Reserve( string ident )
		{
			try
			{
				var package = await Package.Fetch( ident, false );
				if ( package is null ) return;

				// What it pulls in, sized alongside it rather than after
				var references = withReferences
					? package.EnumerateInstallDependencies()
						.Where( x => IsPackage( x ) && PackageManager.Find( x, false ) is null && Unseen( x ) )
						.Select( Reserve )
						.ToList()
					: new List<Task>();

				if ( package.IsRemote )
				{
					var size = await package.GetDownloadSizeAsync( token: token );

					var download = size < 0 ? null : TrackDownload( package.FullIdent, package.Title );
					if ( download is not null && !download.IsComplete && !download.IsDownloading )
					{
						download.TotalSize = size;
						download.IsComplete = size == 0;
					}
				}

				await Task.WhenAll( references );
			}
			catch ( OperationCanceledException ) { }
			catch ( Exception e )
			{
				Log.Warning( e, $"Couldn't size up {ident}'s download: {e.Message}" );
			}
		}

		await Task.WhenAll( idents.Where( x => IsPackage( x ) && Unseen( x ) ).Select( Reserve ) );
		token.ThrowIfCancellationRequested();
	}

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
