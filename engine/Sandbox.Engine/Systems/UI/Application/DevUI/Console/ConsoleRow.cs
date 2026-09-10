namespace Sandbox.UI.Dev;

using Sandbox;
using Sandbox.UI.Construct;

public sealed class ConsoleRow : Panel
{
	static ConsoleRow selectedLine;

	public LogEvent Event;
	public bool AutoDelete;
	public RealTimeUntil TimeUntilDelete;
	public Action<LogEvent> OnEntryClicked;

	readonly Label LoggerLabel;
	readonly Label MessageLabel;

	[ConVar( "console_msg_font_size", Help = "Font size of every console message.", Saved = true )]
	public static float ConsoleMsgFontSize { get; set; } = 11;

	public ConsoleRow()
	{
		AddClass( "consolerow" );
		LoggerLabel = Add.Label( "", "logger" );
		MessageLabel = Add.Label( "", "message" );
		Style.FontSize = ConsoleMsgFontSize;
	}

	internal void SetLogEvent( LogEvent e )
	{
		Event = e;
		Style.FontSize = ConsoleMsgFontSize;

		SetClass( "highlight", selectedLine == this );
		SetClass( "trace", e.Level == LogLevel.Trace );
		SetClass( "info", e.Level == LogLevel.Info );
		SetClass( "warn", e.Level == LogLevel.Warn );
		SetClass( "error", e.Level == LogLevel.Error );
		SetClass( "in", e.Logger == "in" );

		var logger = DisplayLogger;
		LoggerLabel.Text = string.IsNullOrEmpty( logger ) ? "" : $"[{logger}]";
		LoggerLabel.Style.Display = string.IsNullOrEmpty( logger ) ? DisplayMode.None : DisplayMode.Flex;
		LoggerLabel.Style.Dirty();

		MessageLabel.Text = DisplayMessage;
	}

	string DisplayLogger
	{
		get
		{
			var logger = Event.Logger;
			if ( string.IsNullOrWhiteSpace( logger ) )
				return string.Empty;

			if ( logger == "Generic" || logger == "in" )
				return string.Empty;

			return logger;
		}
	}

	string DisplayMessage
	{
		get
		{
			var message = Event.Message ?? string.Empty;
			var logger = DisplayLogger;

			if ( string.IsNullOrEmpty( logger ) || string.IsNullOrEmpty( message ) )
				return message;

			var prefix = $"[{logger}]";
			if ( message.StartsWith( prefix, StringComparison.Ordinal ) )
			{
				message = message[prefix.Length..];
				if ( message.StartsWith( ' ' ) )
					message = message[1..];
			}

			return message;
		}
	}

	protected override void OnClick( MousePanelEvent e )
	{
		OnEntryClicked?.Invoke( Event );
		selectedLine = this;
		SetClass( "highlight", true );
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		e.StopPropagation();
	}
}
