using Sandbox.UI.Construct;

namespace Sandbox.UI.Dev;

[StyleSheet.Inline( "console-entry-engine-fallback", Styles )]
public class ConsoleEntry : Panel
{
	const string Styles = """
		consoleentry
		{
			flex-shrink: 0;
			color: #fff;
			cursor: pointer;
		}

		consoleentry label
		{
			text-shadow: 2px 2px 0px #000;
			padding: 2px 10px;
			font-size: 12px;
			font-weight: 600;
			background-color: #000d;
			white-space: nowrap;
		}

		consoleentry.trace
		{
			color: #b6ff00;
		}

		consoleentry.warning,
		consoleentry.warn
		{
			color: #ffd800;
		}

		consoleentry.error
		{
			color: #ff0000;
		}
		""";

	public Label Time;
	public Label Message;
	public LogEvent Event;

	public bool AutoDelete;
	public RealTimeUntil TimeUntilDelete;

	public ConsoleEntry()
	{
		Time = Add.Label( null, "time" );
		Time.Selectable = false;

		Message = Add.Label( null, "message" );
	}

	public override void Tick()
	{
		base.Tick();

		if ( AutoDelete && TimeUntilDelete <= 0 )
		{
			Delete();
		}
	}

	internal void SetLogEvent( LogEvent e )
	{
		Event = e;

		Time.Text = Event.Time.ToString( "hh:mm:ss" );
		Message.Text = Event.Message;
		AddClass( e.Level.ToString() );

		if ( e.Logger != null )
		{
			AddClass( e.Logger );
		}
	}

	internal void DeleteIn( float seconds )
	{
		AutoDelete = true;
		TimeUntilDelete = seconds;
	}
}
