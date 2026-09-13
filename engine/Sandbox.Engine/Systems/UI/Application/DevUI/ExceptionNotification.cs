using Sandbox.UI.Construct;

namespace Sandbox.UI.Dev;

[StyleSheet.Inline( "exception-notification-engine-fallback", Styles )]
public class ExceptionNotification : Panel
{
	const string Styles = """
		exceptionnotification
		{
			position: absolute;
			left: 0;
			right: 0;
			top: 2vh;
			flex-direction: row;
			justify-content: center;
			color: white;
			pointer-events: none;
			z-index: 10001;
		}

		exceptionnotification .inner
		{
			background-color: rgba(32, 32, 32, 0.8);
			backdrop-filter: blur(16px);
			border: 2px #bf773b;
			border-radius: 5px;
			padding: 5px;
			gap: 5px;
			flex-direction: row;
			align-items: center;
			pointer-events: none;
			position: relative;
			top: 0px;
			transition: top 0.4s ease-out, opacity 0.2s ease-out;
		}

		exceptionnotification.hidden .inner
		{
			top: -20px;
			opacity: 0;
			transition: top 0.3s ease-in, opacity 0.15s ease-in;
		}

		exceptionnotification.fresh .inner
		{
			background-color: rgba(200, 32, 32, 0.8);
		}

		exceptionnotification .icon-small
		{
			width: 64px;
			height: 64px;
		}

		exceptionnotification .column
		{
			flex-direction: column;
			font-size: 20px;
		}

		exceptionnotification .column > label:first-child
		{
			color: #bf773b;
			font-size: 15px;
			font-weight: 600;
		}

		exceptionnotification .message
		{
			font-size: 20px;
		}
		""";

	Label message;
	RealTimeSince TimeSinceLastError;
	Panel inner;

	public ExceptionNotification()
	{
		inner = new Panel( this, "inner" );

		var img = inner.Add.Image( null, "icon-small" );
		img.SetTexture( "tools/images/common/generic_hud_warning.png" );
		img.Style.Width = 64;
		img.Style.Height = 64;

		var column = new Panel( inner, "column" );

		column.AddChild( new Label() { Text = "Code Error" } );
		message = column.Add.Label( "Something went wrong! This is an exception notice!", "message" );

		SetClass( "hidden", true );
		TimeSinceLastError = 100;
	}

	public override void Tick()
	{
		base.Tick();

		SetClass( "hidden", TimeSinceLastError > 8 );
		SetClass( "fresh", TimeSinceLastError < 0.2f );
	}

	internal void OnException( LogEvent entry )
	{
		message.Text = entry.Message?.Split( '\n', System.StringSplitOptions.RemoveEmptyEntries ).FirstOrDefault()?.Trim() ?? "null";
		TimeSinceLastError = 0;
	}
}
