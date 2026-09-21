namespace Sandbox.UI.Dev;

using Sandbox;
using Sandbox.UI;

public sealed class DevLayerSceneEvents( Scene scene ) : GameObjectSystem<DevLayerSceneEvents>( scene ), ISceneLoadingEvents
{
	void ISceneLoadingEvents.BeforeLoad( Scene scene, SceneLoadOptions options )
	{
		ConsoleOverlay.CloseForSceneChange();
	}
}

[StyleSheet.Inline( "devlayer-engine-fallback", DevLayerStyles )]
public sealed class DevLayer : RootPanel
{
	public static DevLayer Instance { get; private set; }

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
	ConsoleOverlay ConsoleOverlayPanel;

	public DevLayer()
	{
		RenderedManually = true;
		Instance = this;

		ConsoleOverlay.ResetStartupState();

		AddClass( "devui" );
		Style.Position = PositionMode.Absolute;
		Style.Left = 0;
		Style.Top = 0;
		Style.Width = Length.Percent( 100 );
		Style.Height = Length.Percent( 100 );

		// Stylesheet autoload depends on TypeLibrary metadata (ClassFileLocationAttribute),
		// which can be missing in some base-context init paths. Load the key DevUI sheets
		// explicitly so the UI is visible even without TypeLibrary enrollment.
		LoadDevUiStyles();

		AddChild<DeveloperMode>();
		ConsoleOverlayPanel = AddChild<ConsoleOverlay>();

		ExceptionNotification = AddChild<ExceptionNotification>();

		MenuUtility.AddLogger( OnConsoleMessage );
	}

	internal static void RenderFinalOverlay()
	{
		if ( !Instance.IsValid() )
			return;

		Instance.RenderManual();
	}

	public override void Tick()
	{
		base.Tick();

		// DevUI.cs.scss disables pointer events for the whole DevLayer unless this class is present.
		// Keep it in sync with the devui focused state so unfocused windows can stay visible without eating input.
		SetClass( "developermode", ConsoleOverlay.WantsInput );
		TickDragEarly();
	}

	internal void TickDragEarly()
	{
		ConsoleOverlayPanel?.TickDragEarly();
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

		if ( Instance == this )
			Instance = null;

		MenuUtility.RemoveLogger( OnConsoleMessage );
	}

	protected override void UpdateScale( Rect screenSize )
	{
		Scale = Screen.DesktopScale * DevUI_Scale;
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
