using Sandbox.Internal;
using Sandbox.Network;
using Sandbox.Tasks;
using System.Threading;
using System.Threading.Tasks;

using IUISystem = Sandbox.Internal.IUISystem;

namespace Sandbox.Engine;

internal unsafe interface IGameInstanceDll
{
	public static IGameInstanceDll Current { get; set; }

	public void Bootstrap();
	public async Task Initialize()
	{
		const string UiSystemLibraryName = "UISystem";

		if ( IUISystem.Current != null )
		{
			Log.Info( "Aready inited?" );
			return;
		}

		{
			using var tx = Sandbox.Engine.Bootstrap.StartupTiming?.ScopeTimer( "UI - Fonts" );
			FontManager.Instance.LoadAll( FileSystem.Mounted );
		}

		IUISystem.Current = TypeLibrary.Create<IUISystem>( UiSystemLibraryName, complainOnMissing: false );

		//
		// If the UI system lives in an addon assembly (e.g. base), it might not have been
		// enrolled into the current TypeLibrary yet. Try to enroll the assembly containing
		// the expected type name and retry.
		//
		if ( IUISystem.Current is null )
		{
			var asm = FindAssemblyContainingType( UiSystemLibraryName );
			if ( asm is not null )
			{
				TypeLibrary.AddAssembly( asm, isDynamic: false );
			}

			IUISystem.Current = TypeLibrary.Create<IUISystem>( UiSystemLibraryName, complainOnMissing: false );
		}

		if ( IUISystem.Current == null )
		{
			NativeEngine.EngineGlobal.Plat_MessageBox( "UI Load Error", $"Couldn't create {UiSystemLibraryName}!" );
			throw new System.Exception( $"UI system '{UiSystemLibraryName}' couldn't load. Can't continue." );
		}

		// Allow tasks in menu assembly to persist when game sessions end
		ExpirableSynchronizationContext.AllowPersistentTaskMethods( IUISystem.Current.GetType().Assembly );

		IUISystem.Current.Init();
	}

	static System.Reflection.Assembly FindAssemblyContainingType( string typeName )
	{
		// Prefer exact type name first, then a Sandbox.*-namespaced variant.
		var candidateNames = new[] { typeName, $"Sandbox.{typeName}" };

		foreach ( var asm in System.AppDomain.CurrentDomain.GetAssemblies() )
		{
			foreach ( var fullName in candidateNames )
			{
				var t = asm.GetType( fullName, throwOnError: false, ignoreCase: false );
				if ( t is null ) continue;

				if ( typeof( IUISystem ).IsAssignableFrom( t ) && !t.IsAbstract )
					return asm;
			}
		}

		return null;
	}

	public void Tick();
	public void Exiting();

	public InputContext InputContext => default;

	public void OnRender( SwapChainHandle_t swapChain );
	public void FinishLoadingAssemblies();
	public TypeLibrary TypeLibrary { get; }
	public void OnProjectConfigChanged( Package package );

	//
	// UI
	//
	public void ClosePopups( object panelClickedOn );
	public void SimulateUI();


	//
	// Game Menu Shit
	//
	public Task<bool> LoadGamePackageAsync( string ident, GameLoadingFlags flags, CancellationToken ct );

	//
	// Scene
	//
	public IDisposable PushScope();
	public void EditorPlay(); // play game button pressed in editor

	//
	// Network
	//

	GameNetworkSystem CreateGameNetworking( NetworkSystem system );
	Task<GameNetworkSystem> CreateGameNetworkingAsync( NetworkSystem system );
	public void InstallNetworkTables( NetworkSystem system );

	/// <summary>
	/// We took over as host; adopt anything mirrored from the previous host.
	/// </summary>
	public void OnBecameHost() { }
	public Task<bool> LoadNetworkTables( NetworkSystem system );

	/// <summary>
	/// Called when the "disconnect" command is ran.
	/// </summary>
	public void Disconnect( string message = null );

	/// <summary>
	/// Closes the current GameInstance immediately
	/// </summary>
	public void CloseGame();

	void ResetSceneListenerMetrics();
	object GetSceneListenerMetrics();

	/// <summary>
	/// Get the replicated var value from the host
	/// </summary>
	public bool TryGetReplicatedVarValue( string name, out string value );

	/// <summary>
	/// Load the assemblies from this package into the current game instance
	/// </summary>
	public Task LoadPackageAssembliesAsync( Package package );
}

[Flags]
public enum GameLoadingFlags
{
	/// <summary>
	/// Set if we're loading a game as a result of joining a server
	/// </summary>
	Remote = 1,

	/// <summary>
	/// Set if we're the hosting as the result of starting our own server
	/// </summary>
	Host = 2,

	/// <summary>
	/// Set if we want to reload the game, even if it's already loaded
	/// </summary>
	Reload = 4,

	/// <summary>
	/// Set if this is a developer session. It started from an editor session and as such we shouldn't load
	/// assemblies from the package, they should be loaded from the Network Tables instead.
	/// </summary>
	Developer = 8
}
