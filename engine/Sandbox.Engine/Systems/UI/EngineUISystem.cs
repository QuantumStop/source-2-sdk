using Sandbox.Internal;
using Sandbox.UI.Dev;
using Sandbox.UI.Overlay;
using Sandbox.Engine;

namespace Sandbox;

internal sealed class EngineUISystem : IUISystem
{
	DevLayerHost _devLayerHost;

	public bool ForceCursorVisible => LoadingScreen.IsVisible || DeveloperMode.WantsInput;

	public void Init()
	{
		if ( Application.IsHeadless || GlobalContext.Current?.UISystem is null )
			return;

		UISystemOverlay.Init();
	}

	public void Shutdown()
	{
		_devLayerHost?.Delete();
		_devLayerHost = null;

		UISystemOverlay.Shutdown();
	}

	public void Tick()
	{
		if ( Application.IsHeadless || GlobalContext.Current?.UISystem is null )
			return;

		UISystemOverlay.Init();

		if ( _devLayerHost?.IsValid != true )
			_devLayerHost = DevLayerHost.Create();
	}
}
