using NativeEngine;

namespace Sandbox;

public partial class Scene : GameObject
{
	readonly List<LoadingContext> _loadingTasks = [];
	Task _loadingMainTask;

	float _loadingScreenShownAt;
	float _loadingScreenMinimumSeconds;
	bool _loadingScreenRequireInput;
	string _loadingScreenContinueInputAction;
	bool _loadingScreenActive;
	int _loadingScreenVisibilityCycle;

	internal void BeginLoadingScreen( SceneLoadOptions options )
	{
		// If the caller didn't explicitly set a context (startup/editor play), treat this as a scene transition.
		LoadingScreen.EnsureContext( LoadingScreen.Context.SceneTransition );

		_loadingScreenActive = true;
		_loadingScreenVisibilityCycle = LoadingScreen.VisibilityCycle + (LoadingScreen.IsVisible ? 0 : 1);
		_loadingScreenShownAt = RealTime.Now;
		var settings = ProjectSettings.Loading;
		var context = LoadingScreen.CurrentContext switch
		{
			LoadingScreen.Context.Startup => LoadingSettings.LoadingContext.Startup,
			LoadingScreen.Context.NetworkConnect => LoadingSettings.LoadingContext.NetworkConnect,
			LoadingScreen.Context.EditorPlay => LoadingSettings.LoadingContext.EditorPlay,
			_ => LoadingSettings.LoadingContext.SceneTransition
		};

		var defaults = settings?.GetPolicy( context ) ?? new LoadingSettings.Policy( LoadingScreen.DefaultMinimumVisibleSeconds, LoadingScreen.DefaultRequireInputToContinue, LoadingScreen.DefaultContinueInputAction, null );

		// Select overlay visuals for this load.
		LoadingScreen.OverlayPanelTypeName = options.LoadingOverlayPanelTypeNameOverride ?? defaults.OverlayPanelTypeName;

		_loadingScreenMinimumSeconds = options.MinimumLoadingScreenSeconds >= 0.0f
			? options.MinimumLoadingScreenSeconds
			: defaults.MinimumVisibleSeconds;

		_loadingScreenRequireInput = options.RequireInputToContinueOverride
			?? (options.RequireInputToContinue ? true : defaults.RequireInputToContinue);

		var action = options.ContinueInputActionOverride ?? options.ContinueInputAction;
		_loadingScreenContinueInputAction = string.IsNullOrWhiteSpace( action )
			? defaults.ContinueInputAction
			: action;

		LoadingScreen.IsAwaitingInput = false;
	}

	bool ShouldContinueFromLoadingScreen()
	{
		if ( !string.IsNullOrWhiteSpace( _loadingScreenContinueInputAction ) )
			return Input.Pressed( _loadingScreenContinueInputAction );

		return Input.AnyPressed();
	}

	async Task WaitForLoadingScreenGate()
	{
		if ( !OwnsLoadingScreenCycle() )
			return;

		// No UI/input - don't ever block scene completion on a server/headless instance.
		if ( Application.IsHeadless )
			return;

		var minSeconds = MathF.Max( 0.0f, _loadingScreenMinimumSeconds );

		// Prefer measuring minimum visible time from when the UI system actually displayed the loading overlay.
		// This avoids a fast startup load "consuming" the minimum time while a native splash screen is still up.
		if ( minSeconds > 0.0f && LoadingScreen.VisibleSince <= 0.0f )
		{
			var startedWaitingForUi = RealTime.Now;
			while ( OwnsLoadingScreenCycle() && LoadingScreen.VisibleSince <= 0.0f && (RealTime.Now - startedWaitingForUi) < 2.0f )
			{
				await Task.DelayRealtime( 16 );
			}
		}

		if ( !OwnsLoadingScreenCycle() )
			return;

		var shownAt = LoadingScreen.VisibleSince > 0.0f ? LoadingScreen.VisibleSince : _loadingScreenShownAt;
		var holdUntil = shownAt + minSeconds;
		while ( OwnsLoadingScreenCycle() && RealTime.Now < holdUntil )
		{
			await Task.DelayRealtime( 16 );
		}

		if ( !OwnsLoadingScreenCycle() )
			return;

		if ( !_loadingScreenRequireInput )
			return;

		LoadingScreen.IsAwaitingInput = true;

		// Ensure we don't consume input on the same continuation as load completion.
		await Task.DelayRealtime( 16 );

		// Small debounce so the press that dismisses a native splash / focuses the window doesn't instantly continue.
		var ignoreInputUntil = RealTime.Now + 0.15f;
		while ( OwnsLoadingScreenCycle() && RealTime.Now < ignoreInputUntil )
		{
			await Task.DelayRealtime( 16 );
		}

		while ( OwnsLoadingScreenCycle() )
		{
			if ( ShouldContinueFromLoadingScreen() )
				break;

			await Task.DelayRealtime( 16 );
		}

		LoadingScreen.IsAwaitingInput = false;
	}

	bool OwnsLoadingScreenCycle()
		=> _loadingScreenActive
			&& IsValid
			&& LoadingScreen.IsVisible
			&& LoadingScreen.VisibilityCycle == _loadingScreenVisibilityCycle;

	internal void AddLoadingTask( LoadingContext loadingTask )
	{
		_loadingTasks.Add( loadingTask );
		LoadingScreen.UpdateLoadingTasks( _loadingTasks );
	}

	public void StartLoading()
	{
		if ( _loadingMainTask is not null )
			return;

		_loadingMainTask = WaitForLoading();
	}

	/// <summary>
	/// Return true if we're in an initial loading phase
	/// </summary>
	public bool IsLoading
	{
		get
		{
			_loadingTasks.RemoveAll( x => x.IsCompleted );

			if ( _loadingMainTask is null ) return false;
			if ( _loadingMainTask.IsCompleted ) return false;

			return true;
		}
	}

	/// <summary>
	/// Wait for scene loading to finish
	/// </summary>
	internal async Task WaitForLoading()
	{
		if ( _loadingMainTask is not null )
		{
			await _loadingMainTask;
			return;
		}

		try
		{
			var instance = IGameInstance.Current;

			// wait one frame for all the tasks to build up
			await Task.Yield();

			// wait for all the loading tasks to finish
			while ( _loadingTasks.Count > 0 )
			{
				LoadingScreen.UpdateLoadingTasks( _loadingTasks );
				await Task.WhenAny( _loadingTasks.Select( x => x.Task ) );
				_loadingTasks.RemoveAll( x => x.IsCompleted );
			}

			// Remove all the tasks
			LoadingScreen.UpdateLoadingTasks( [] );

			if ( !IsValid ) return;

			//
			// Some people are locking up forever. Need more info.
			//

			//while ( NativeEngine.ResourceSystem.HasPendingWork() )
			//{
			//	LoadingScreen.Subtitle = "Loading Resources..";
			//	await Task.DelayRealtime( 100 );
			//}

			// generated after everything is loaded
			if ( NavMesh.IsEnabled && this is not PrefabScene )
			{
				LoadingScreen.Subtitle = "Generating NavMesh..";

				await NavMesh.Load( PhysicsWorld );

				LoadingScreen.Subtitle = "Loading Finished..";
			}

			if ( !IsValid ) return;

			using ( Push() )
			{
				// tell the game instance we finished loading
				instance?.OnLoadingFinished();

				// shoot events
				RunEvent<ISceneLoadingEvents>( x => x.AfterLoad( this ) );

				// Run pending startups
				RunPendingStarts();

				if ( WantsSystemScene && this is not PrefabScene )
					g_pRenderDevice.FlushPipelineCache();

				// Tell networking we've finished loading, lets players join
				var sceneInformation = Components.Get<SceneInformation>();
				SceneNetworkSystem.OnLoadedScene( sceneInformation?.Title );
			}

			// Keep the loading screen visible (optionally) even after load completion is signaled.
			await WaitForLoadingScreenGate();
		}
		finally
		{
			_loadingMainTask = default;
			// A stopped scene may finish this task after another Editor Play has started. Only
			// the scene that owns the current cycle is allowed to dismiss the global overlay.
			if ( _loadingScreenActive && LoadingScreen.VisibilityCycle == _loadingScreenVisibilityCycle )
			{
				LoadingScreen.IsVisible = false;
				LoadingScreen.IsAwaitingInput = false;
				LoadingScreen.ClearContext();
			}

			_loadingScreenActive = false;
		}
	}
}

public class LoadingContext
{
	/// <summary>
	/// The title of this loading task
	/// </summary>
	public string Title { get; set; }

	/// <summary>
	/// True if the task has completed
	/// </summary>
	public bool IsCompleted => Task?.IsCompleted ?? true;

	/// <summary>
	/// The task itself
	/// </summary>
	internal Task Task { get; set; }
}
