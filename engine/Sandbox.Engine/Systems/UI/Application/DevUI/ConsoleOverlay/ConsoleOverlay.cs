namespace Sandbox.UI.Dev;

using Sandbox;

[StyleSheet.Inline( "console-overlay-engine-fallback", Styles )]
public class ConsoleOverlay : Panel
{
	const string Styles = """
		consoleoverlay
		{
			position: absolute;
			left: 20px;
			right: 20px;
			top: 10px;
			flex-direction: column;
			font-family: Roboto Mono;
			pointer-events: none;
			z-index: 10000;
		}

		consoleoverlay.hidden
		{
			display: none;
		}

		consoleoverlay .output
		{
			flex-direction: column;
			justify-content: flex-end;
			overflow: hidden;
			flex-grow: 1;
		}
		""";

	[ConVar( "consoleoverlay", Help = "Enable the console to draw at the top of the screen all the time", Saved = true )]
	public static bool ConsoleOverlayEnabled { get; set; }

	internal Panel Output;

	public ConsoleOverlay()
	{
		Output = Add.Panel( "output" );
		MenuUtility.AddLogger( OnConsoleMessage );
	}

	public override void OnDeleted()
	{
		base.OnDeleted();

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
}
