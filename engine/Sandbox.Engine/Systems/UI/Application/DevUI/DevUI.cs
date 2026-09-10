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

public sealed class DevLayer : Panel
{
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

		// Use non-rooted paths here so we don't end up loading the same sheet twice
		// (auto-loaded sheets are typically non-rooted via ClassFileLocationAttribute).
		StyleSheet.Load( "UISystem/DevUI/DevUI.cs.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Window/DevWindow.cs.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Tabs/DevTabs.cs.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/DevMode/DeveloperMode.razor.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/DevMode/ConvarToggle.razor.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/DevMode/RenderModeSelect.razor.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Stats/StatsContainer.razor.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Stats/StatValue.razor.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/ConsoleOverlay/ConsoleOverlay.cs.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Console/Console.cs.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Console/ConsoleRow.razor.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Console/LogEventPanel.razor.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Layouts/DevGroup.cs.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Layouts/DevColumns.razor.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Layouts/DevRow.razor.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Controls/DevScrollPanel.cs.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Controls/DevScrollBar.cs.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Controls/DevScrollView.cs.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Controls/DevVirtualList.cs.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Controls/DevPercentSlider.razor.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/Controls/DevCommandGrid.razor.scss", true, failSilently );
		StyleSheet.Load( "UISystem/DevUI/ExceptionNotification.cs.scss", true, failSilently );

		// Generic UI controls used by DevUI extension tabs.
		StyleSheet.Load( "UI/Controls/VideoPanel.razor.scss", true, failSilently );
		StyleSheet.Load( "UI/Controls/VideoControls.razor.scss", true, failSilently );
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
