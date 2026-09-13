namespace Sandbox.UI.Dev;

using Sandbox;
using Sandbox.UI;

public sealed class DevLayerSceneEvents( Scene scene ) : GameObjectSystem<DevLayerSceneEvents>( scene ), ISceneLoadingEvents
{
	void ISceneLoadingEvents.BeforeLoad( Scene scene, SceneLoadOptions options )
	{
		DeveloperMode.CloseForSceneChange();
	}
}

public sealed class DevLayerHost
{
	static DevLayerHost Current;
	static DevLayerComponent CurrentComponent;

	GameObject GameObject;
	DevLayerComponent Component;

	public bool IsValid => GameObject.IsValid() && Component.IsValid();

	DevLayerHost( GameObject gameObject, DevLayerComponent component )
	{
		GameObject = gameObject;
		Component = component;
	}

	public static DevLayerHost Create()
	{
		var scene = Game.ActiveScene;
		if ( !scene.IsValid() )
			return null;

		if ( Current?.IsValid == true )
		{
			DestroyDuplicateHosts( Current.GameObject.Scene, Current.Component );
			DestroyDuplicateHosts( scene, Current.Component );
			return Current;
		}

		var existing = scene.GetAllComponents<DevLayerComponent>()
			.FirstOrDefault( component => component.IsValid() && component.GameObject.IsValid() );

		if ( existing.IsValid() )
		{
			Current = new DevLayerHost( existing.GameObject, existing );
			CurrentComponent = existing;
			DestroyDuplicateHosts( scene, existing );
			return Current;
		}

		var gameObject = scene.CreateObject();
		gameObject.Name = "DevLayer Host";

		var screen = gameObject.Components.Create<ScreenPanel>();
		screen.AutoScreenScale = false;
		screen.Scale = Screen.DesktopScale * DevLayer.DevUI_Scale;
		screen.ZIndex = 1000;

		var component = gameObject.Components.Create<DevLayerComponent>();
		Current = new DevLayerHost( gameObject, component );
		CurrentComponent = component;
		return Current;
	}

	public void Delete()
	{
		if ( GameObject.IsValid() )
			GameObject.Destroy();

		if ( Current == this )
			Current = null;

		if ( CurrentComponent == Component )
			CurrentComponent = null;

		GameObject = null;
		Component = null;
	}

	public void OnHotloaded()
	{
		Component?.OnHotloaded();
	}

	internal static bool Register( DevLayerComponent component )
	{
		if ( !component.IsValid() || !component.GameObject.IsValid() )
			return false;

		if ( CurrentComponent.IsValid() && CurrentComponent != component )
			return false;

		CurrentComponent = component;
		Current = new DevLayerHost( component.GameObject, component );
		DestroyDuplicateHosts( component.Scene, component );
		return true;
	}

	internal static bool IsPrimary( DevLayerComponent component )
	{
		if ( !CurrentComponent.IsValid() )
			return Register( component );

		return CurrentComponent == component;
	}

	static void DestroyDuplicateHosts( Scene scene, DevLayerComponent keep )
	{
		if ( !scene.IsValid() || !keep.IsValid() )
			return;

		foreach ( var component in scene.GetAllComponents<DevLayerComponent>() )
		{
			if ( !component.IsValid() || component == keep )
				continue;

			component.GameObject?.Destroy();
		}
	}
}

public sealed class DevLayerComponent : PanelComponent
{
	DevLayer DevLayer;
	ScreenPanel ScreenPanel;

	protected override void OnStart()
	{
		base.OnStart();

		if ( !DevLayerHost.Register( this ) )
		{
			GameObject.Destroy();
			return;
		}

		GameObject.Components.TryGet( out ScreenPanel );

		if ( !Tags.Has( "devui" ) )
			Tags.Add( "devui" );

		Panel.Style.Position = PositionMode.Absolute;
		Panel.Style.Left = 0;
		Panel.Style.Top = 0;
		Panel.Style.Width = Length.Percent( 100 );
		Panel.Style.Height = Length.Percent( 100 );
		Panel.Style.PointerEvents = PointerEvents.None;

		DevLayer = Panel.AddChild<DevLayer>();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( !DevLayerHost.IsPrimary( this ) )
		{
			GameObject.Destroy();
			return;
		}

		if ( ScreenPanel.IsValid() )
			ScreenPanel.Scale = Screen.DesktopScale * DevLayer.DevUI_Scale;

		DevLayer?.TickDragEarly();
	}

	public void OnHotloaded()
	{
		DevLayer?.OnHotloaded();
	}
}

[StyleSheet.Inline( "devlayer-engine-fallback", DevLayerStyles )]
public sealed class DevLayer : Panel
{
	const string DevLayerStyles = """
		devlayer
		{
			position: absolute;
			left: 0;
			top: 0;
			width: 100%;
			height: 100%;
			z-index: 10000;
			pointer-events: none;
			font-family: Roboto;
		}

		devlayer .popup-panel
		{
			pointer-events: all;
			font-family: Roboto Mono;
		}

		devlayer .popup-panel .button
		{
			font-size: 11px;
		}
		""";

	ExceptionNotification ExceptionNotification;
	DeveloperMode DeveloperModePanel;

	public DevLayer()
	{
		DeveloperMode.ResetStartupState();

		Style.Position = PositionMode.Absolute;
		Style.Left = 0;
		Style.Top = 0;
		Style.Width = Length.Percent( 100 );
		Style.Height = Length.Percent( 100 );

		// Stylesheet autoload depends on TypeLibrary metadata (ClassFileLocationAttribute),
		// which can be missing in some base-context init paths. Load the key DevUI sheets
		// explicitly so the UI is visible even without TypeLibrary enrollment.
		LoadDevUiStyles();

		DeveloperModePanel = AddChild<DeveloperMode>();
		AddChild<ConsoleOverlay>();

		ExceptionNotification = AddChild<ExceptionNotification>();

		MenuUtility.AddLogger( OnConsoleMessage );
	}

	public override void Tick()
	{
		base.Tick();

		// DevUI.cs.scss disables pointer events for the whole DevLayer unless this class is present.
		// Keep it in sync with the devui focused state so unfocused windows can stay visible without eating input.
		SetClass( "developermode", DeveloperMode.WantsInput );
	}

	internal void TickDragEarly()
	{
		DeveloperModePanel?.TickDragEarly();
	}

	void LoadDevUiStyles()
	{
		const bool failSilently = true;
		LoadDevUiStyle( "DevUI.cs.scss", failSilently );
		LoadDevUiStyle( "Window/DevWindow.cs.scss", failSilently );
		LoadDevUiStyle( "Tabs/DevTabs.cs.scss", failSilently );
		LoadDevUiStyle( "DevMode/DeveloperMode.razor.scss", failSilently );
		LoadDevUiStyle( "DevMode/ConvarToggle.razor.scss", failSilently );
		LoadDevUiStyle( "DevMode/ConvarCycle.razor.scss", failSilently );
		LoadDevUiStyle( "DevMode/RenderModeSelect.razor.scss", failSilently );
		LoadDevUiStyle( "Stats/StatsContainer.razor.scss", failSilently );
		LoadDevUiStyle( "Stats/StatValue.razor.scss", failSilently );
		LoadDevUiStyle( "ConsoleOverlay/ConsoleOverlay.cs.scss", failSilently );
		LoadDevUiStyle( "Console/Console.cs.scss", failSilently );
		LoadDevUiStyle( "Console/ConsoleRow.razor.scss", failSilently );
		LoadDevUiStyle( "Console/LogEventPanel.razor.scss", failSilently );
		LoadDevUiStyle( "Layouts/DevGroup.cs.scss", failSilently );
		LoadDevUiStyle( "Layouts/DevColumns.razor.scss", failSilently );
		LoadDevUiStyle( "Layouts/DevRow.razor.scss", failSilently );
		LoadDevUiStyle( "Controls/DevScrollPanel.cs.scss", failSilently );
		LoadDevUiStyle( "Controls/DevScrollBar.cs.scss", failSilently );
		LoadDevUiStyle( "Controls/DevScrollView.cs.scss", failSilently );
		LoadDevUiStyle( "Controls/DevVirtualList.cs.scss", failSilently );
		LoadDevUiStyle( "Controls/DevPercentSlider.razor.scss", failSilently );
		LoadDevUiStyle( "Controls/DevCommandGrid.razor.scss", failSilently );
		LoadDevUiStyle( "ExceptionNotification.cs.scss", failSilently );

		// Generic UI controls used by DevUI extension tabs.
		StyleSheet.Load( "UI/Controls/VideoPanel.razor.scss", true, failSilently );
		StyleSheet.Load( "UI/Controls/VideoControls.razor.scss", true, failSilently );
	}

	void LoadDevUiStyle( string relativePath, bool failSilently )
	{
		StyleSheet.Load( $"UISystem/DevUI/{relativePath}", true, failSilently );
	}

	public override void OnDeleted()
	{
		base.OnDeleted();

		MenuUtility.RemoveLogger( OnConsoleMessage );
	}

	[ConVar( "devui_scale" )]
	public static float DevUI_Scale { get; set; } = 1.0f;

	private void OnConsoleMessage( LogEvent entry )
	{
		if ( !ThreadSafe.IsMainThread )
			return;

		if ( entry.Level == LogLevel.Error )
		{
			ExceptionNotification.OnException( entry );
		}
	}
}
