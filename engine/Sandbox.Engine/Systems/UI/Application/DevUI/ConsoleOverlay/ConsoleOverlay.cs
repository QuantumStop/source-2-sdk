namespace Sandbox.UI.Dev;

using Sandbox;
using System.Linq;

[StyleSheet.Inline( "console-overlay-engine-fallback", Styles )]
public class ConsoleOverlay : Panel
{
	const string Styles = """
		consoleoverlay
		{
			position: absolute;
			left: 0;
			right: 0;
			top: 0;
			bottom: 0;
			flex-direction: column;
			font-family: Roboto Mono;
			pointer-events: none;
			z-index: 10000;
		}

		consoleoverlay.hidden
		{
			display: none;
		}

		consoleoverlay:not(.open) DevWindow
		{
			display: none;
		}

		consoleoverlay .window-bounds
		{
			position: absolute;
			left: 0;
			right: 0;
			top: 0;
			bottom: 0;
			padding: 0.25em;
			pointer-events: none;
			z-index: 2;
		}

		consoleoverlay .output
		{
			position: absolute;
			left: 20px;
			right: 20px;
			top: 10px;
			z-index: 1;
			flex-direction: column;
			justify-content: flex-end;
			overflow: hidden;
		}

		consoleoverlay .window-bounds > DevWindow
		{
			z-index: 2;
		}
		""";

	[ConVar( "consoleoverlay", Help = "Enable the console to draw at the top of the screen all the time", Saved = true )]
	public static bool ConsoleOverlayEnabled { get; set; }

	public static ConsoleOverlay Current { get; private set; }
	public static DevWindow MainWindow => Current?.Window;
	public static int ConsoleToggleSerial { get; private set; }

	static bool RequestedOpen;
	static bool RequestedFocused = true;
	static bool _keepConsoleOpenOnSceneChange;

	internal Panel Output;
	internal Panel WindowBounds;
	internal DevWindow Window;

	public static bool Open
	{
		get => MainWindow?.Open ?? RequestedOpen;
		set
		{
			RequestedOpen = value;

			if ( MainWindow.IsValid() )
				MainWindow.Open = value;
		}
	}

	public static bool Focused
	{
		get => MainWindow?.Focused ?? RequestedFocused;
		set
		{
			RequestedFocused = value;

			if ( MainWindow.IsValid() )
				MainWindow.Focused = value;
		}
	}

	public static bool WantsInput => MainWindow?.WantsInput ?? (Open && Focused);

	public static void CloseIfOwnsInput()
	{
		if ( !Open || !Focused )
			return;

		Open = false;
		Focused = false;
	}

	internal static void ResetStartupState()
	{
		RequestedOpen = false;
		RequestedFocused = true;
		Open = false;
		Focused = false;
		KeepConsoleOpenOnSceneChange = false;
	}

	/// <summary>
	/// When true, active scene changes won't automatically close the developer console.
	/// </summary>
	public static bool KeepConsoleOpenOnSceneChange
	{
		get => _keepConsoleOpenOnSceneChange;
		set => _keepConsoleOpenOnSceneChange = value;
	}

	internal static void CloseForSceneChange()
	{
		if ( KeepConsoleOpenOnSceneChange )
			return;

		Open = false;
		Focused = false;
	}

	// Keep this as an int so it can be set via `devui 0/1`.
	[ConVar( "devui", Help = "Toggle the developer UI" )]
	public static int DevUI
	{
		get => Open ? 1 : 0;
		set
		{
			if ( value != 0 )
			{
				Open = true;
				Focused = true;
			}
			else
			{
				Open = false;
			}
		}
	}

	bool wasOpen;
	bool wasFocused;
	bool syncedWindowState;

	public ConsoleOverlay()
	{
		WindowBounds = Add.Panel( "window-bounds" );
		Window = WindowBounds.AddChild<DevWindow>();
		Output = Add.Panel( "output" );
		MenuUtility.AddLogger( OnConsoleMessage );
	}

	public override void Tick()
	{
		base.Tick();

		Current = this;
		EnsureWindow();

		if ( Window.IsValid() )
		{
			if ( !syncedWindowState )
			{
				Window.Open = RequestedOpen;
				Window.Focused = RequestedFocused;
				syncedWindowState = true;
			}

			RequestedOpen = Window.Open;
			RequestedFocused = Window.Focused;
		}

		var open = Open;
		var focused = Focused;

		FindRootPanel()?.SetClass( "developermode", WantsInput );

		if ( open != wasOpen || focused != wasFocused )
		{
			wasOpen = open;
			wasFocused = focused;

			if ( open && focused )
				Window?.FocusConsole();
			else
				Window?.BlurConsole();
		}

		SetClass( "open", open );
		SetClass( "focused", focused );
		Window?.SetClass( "unfocused", open && !focused );
	}

	void EnsureWindow()
	{
		if ( Window.IsValid() )
			return;

		if ( !WindowBounds.IsValid() )
			WindowBounds = Add.Panel( "window-bounds" );

		Window = WindowBounds.ChildrenOfType<DevWindow>().FirstOrDefault( x => x.IsValid() ) ?? WindowBounds.AddChild<DevWindow>();
		syncedWindowState = false;
	}

	internal void TickDragEarly()
	{
		Window?.TickDragEarly();
	}

	public override Panel FindPopupPanel() => this;

	public override void OnDeleted()
	{
		base.OnDeleted();

		if ( Current == this )
			Current = null;

		MenuUtility.RemoveLogger( OnConsoleMessage );
	}

	private void OnConsoleMessage( LogEvent e )
	{
		if ( ConsoleSystem.GetValue( "consoleoverlay" ) != "True" )
			return;

		var entry = Output.AddChild<ConsoleEntry>();
		entry.SetLogEvent( e );
		entry.DeleteIn( 8 );

		var c = Output.Children.Count();

		for ( int i = 0; i < c - 10; i++ )
		{
			Output.Children.First().Delete( true );
		}
	}

	[ConCmd( "con_toggle" )]
	static void ToggleConsole()
	{
		if ( MainWindow.IsValid() )
		{
			if ( MainWindow.Open && !MainWindow.Focused )
				return;

			ConsoleToggleSerial++;
			MainWindow.ToggleOpen();
			RequestedOpen = MainWindow.Open;
			RequestedFocused = MainWindow.Focused;
			return;
		}

		if ( Open && !Focused )
			return;

		ConsoleToggleSerial++;
		Open = !Open;
		if ( Open )
			Focused = true;
	}

	[ConCmd( "con_focus" )]
	static void ToggleFocus()
	{
		if ( MainWindow.IsValid() )
		{
			MainWindow.ToggleFocus();
			RequestedOpen = MainWindow.Open;
			RequestedFocused = MainWindow.Focused;
			return;
		}

		if ( !Open )
		{
			Open = true;
			Focused = true;
			return;
		}

		Focused = !Focused;
	}
}
