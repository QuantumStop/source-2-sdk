using Sandbox;
using Sandbox.Audio;
using Sandbox.Internal;
using Sandbox.UI.Construct;
using Sandbox.UI.Dev;
using System.Reflection;

[Library]
public partial class UISystem : IUISystem
{
	public static UISystem Instance;

	DevLayerHost Dev;
	Sandbox.UI.Overlay.UISystemOverlay Overlay;

	public Action<Package> OnPackageSelected { get; set; }

	public static void Reload()
	{
		ReloadDevLayer( force: true );
	}

	static void ReloadDevLayer( bool force )
	{
		var current = GetCurrentUISystem();
		if ( current is null )
		{
			Log.Warning( "dev_layer_reload: IUISystem.Current is null" );
			return;
		}

		// Prefer using the actual current instance (hotload can leave multiple UISystem types loaded).
		var currentType = current.GetType();

		// Update that type's static Instance field (if present) so other callers in that load context see it.
		var instanceField = currentType.GetField( "Instance", BindingFlags.Public | BindingFlags.Static );
		instanceField?.SetValue( null, current );

		var devField = currentType.GetField( "Dev", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public );
		if ( devField is null )
		{
			Log.Warning( "dev_layer_reload: couldn't find Dev field on current UI system" );
			return;
		}

		var oldDev = devField.GetValue( current );
		var devLayerType = currentType.Assembly.GetType( "Sandbox.UI.Dev.DevLayerHost", throwOnError: false, ignoreCase: false ) ?? typeof( DevLayerHost );
		if ( !force && oldDev is not null && oldDev.GetType() == devLayerType )
			return;

		TryDeletePanelLike( oldDev );
		var create = devLayerType.GetMethod( "Create", BindingFlags.Public | BindingFlags.Static );
		var newDev = create?.Invoke( null, null );
		devField.SetValue( current, newDev );
	}

	static void TryDeletePanelLike( object obj )
	{
		if ( obj is null ) return;

		var t = obj.GetType();
		var methods = t.GetMethods( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
			.Where( x => x.Name == "Delete" )
			.ToArray();

		var deleteBool = methods.FirstOrDefault( m => m.GetParameters() is { Length: 1 } p && p[0].ParameterType == typeof( bool ) );
		if ( deleteBool is not null )
		{
			deleteBool.Invoke( obj, new object[] { true } );
			return;
		}

		var deleteNoArgs = methods.FirstOrDefault( m => m.GetParameters().Length == 0 );
		if ( deleteNoArgs is not null )
		{
			deleteNoArgs.Invoke( obj, Array.Empty<object>() );
			return;
		}

		Log.Warning( $"dev_layer_reload: couldn't find a compatible Delete method on {t.FullName}" );
	}

	static object GetCurrentUISystem()
	{
		var iuiType = typeof( IUISystem );
		var prop = iuiType.GetProperty( "Current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static );
		return prop?.GetValue( null );
	}

	public void Init()
	{
		if ( Instance is not null && Instance != this )
		{
			Instance.Shutdown();
		}

		Instance = this;
		DeveloperMode.ResetStartupState();
 
		UpdateDevLayer();
		UpdateOverlay();
	}

	public void Shutdown()
	{
		//MenuOverlay.Shutdown();

		Dev?.Delete();
		Dev = null;

		Sandbox.UI.Overlay.UISystemOverlay.Shutdown();
		Overlay = null;

		// Null so GC can have it's way
		Instance = null;
	}

	public bool ForceCursorVisible => DeveloperMode.Open;

	public void Tick()
	{		
		UpdateDevLayer();
		UpdateOverlay();

		if ( Application.IsEditor ) return;

		UpdateMusic();
	}

	bool ShouldHaveDevLayer()
	{
		if ( Application.IsDedicatedServer )
			return false;

		return true;
	}

	void UpdateDevLayer()
	{
		var shouldHave = ShouldHaveDevLayer();

		if ( shouldHave )
		{
			if ( Dev is null || !Dev.IsValid )
			{
				Dev?.Delete();
				Dev = DevLayerHost.Create();
				Dev?.OnHotloaded();
			}
			return;
		}

		if ( Dev is not null )
		{
			Dev.Delete();
			Dev = null;
		}
	}

	void UpdateOverlay()
	{
		if ( Application.IsDedicatedServer )
			return;

		if ( Overlay.IsValid() )
			return;

		Sandbox.UI.Overlay.UISystemOverlay.Init();
		Overlay = Sandbox.UI.Overlay.UISystemOverlay.Instance;
	}

	// public void Popup( string type, string title, string subtitle )
	// {
	// 	var content = new Panel( null, "popup has-message" );
	// 	content.AddClass( type );
	// 	content.Add.Label( title, "message" );
	// 	content.Add.Label( subtitle, "subtitle" );
	// 	MenuOverlay.Queue( content );
	// }
	

	// John: We don't support questions, you have to be smart from the get go.

	// /// <summary>
	// /// Show a question
	// /// </summary>
	// public void Question( string message, string icon, Action yes, Action no )
	// {
	// 	MenuOverlay.Question( message, icon, yes, no );
	// }



	class MenuMusic
	{
		public bool Enabled;
		public float Volume;
		public float TargetVolume = 0.5f;
		string file;
		MusicPlayer player;

		public MenuMusic( string filename )
		{
			file = filename;
		}

		public void Update()
		{
			float targetVolume = Enabled ? 1 : 0;
			if ( targetVolume == Volume )
				return;

			Volume = Volume.Approach( targetVolume, RealTime.SmoothDelta * 2.0f ); // 0.5s fade
			if ( Volume <= 0.001f )
			{
				player?.Dispose();
				player = null;
				return;
			}

			if ( player is null )
			{
				try
				{
					player = MusicPlayer.Play( FileSystem.Mounted, file );
				}
				catch ( ArgumentException )
				{
					// music not found, fuck it
					return;
				}

				player.Repeat = true;
			}

			player.Volume = Volume * TargetVolume;
			player.Position = new Vector3( 0, 0, 0 );
			player.ListenLocal = true;
			player.TargetMixer = Mixer.FindMixerByName( "music" );
		}
	}

	MenuMusic menu = new MenuMusic( "music/menu-bg.wav" );
	MenuMusic loading = new MenuMusic( "music/menu-loading.wav" );

	void UpdateMusic()
	{
		bool isLoadingScreen = LoadingScreen.IsVisible;

		menu.Enabled = false; // Game.IsMainMenuVisible && !isLoadingScreen && !isAvatarMenu;
		menu.Update();

		loading.Enabled = LoadingScreen.IsVisible && (IGameInstance.Current is null || IGameInstance.Current.IsLoading);
		loading.Update();
	}

	// void IMenuSystem.OnPackageClosed( Package package )
	// {
	// 	var panel = new GameClosedToast() { Package = package };
	// 	MenuOverlay.Instance.BottomRight.Queue( panel, duration: 0 );
	// }

	// [MenuConCmd( "menu_packageclosed" )]
	// public static async Task PackageClosedTest( string ident )
	// {
	// 	var package = await Package.FetchAsync( ident, false );
	// 	((IMenuSystem)MenuSystem.Instance).OnPackageClosed( package );
	// }
}
