using Sandbox.Internal;
using Sandbox.UI.Dev;
using Sandbox.UI.Overlay;
using Sandbox.Engine;

namespace Sandbox;

internal sealed class EngineUISystem : IUISystem
{
	DevLayer _devLayer;

	public bool ForceCursorVisible => LoadingScreen.IsVisible || ConsoleOverlay.WantsInput;

	public void Init()
	{
		if ( Application.IsHeadless || GlobalContext.Current?.UISystem is null )
			return;

		EnsureDevLayer();
		UISystemOverlay.Init();
	}

	public void Shutdown()
	{
		_devLayer?.Delete();
		_devLayer = null;

		UISystemOverlay.Shutdown();
	}

	public void Tick()
	{
		if ( Application.IsHeadless || GlobalContext.Current?.UISystem is null )
			return;

		EnsureDevLayer();
		UISystemOverlay.Init();
	}

	void EnsureDevLayer()
	{
		if ( _devLayer?.IsValid == true )
			return;

		_devLayer = new DevLayer();
	}
}
