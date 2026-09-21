namespace Sandbox.UI.Dev;

using Sandbox;

public sealed class ConsoleRow : Panel
{
	internal const float EstimatedCharWidth = 6.6f;
	const int HorizontalBufferChars = 24;

	static ConsoleRow selectedLine;

	public LogEvent Event;
	public bool AutoDelete;
	public RealTimeUntil TimeUntilDelete;
	public Action<LogEvent> OnEntryClicked;
	public Action OnRowMouseDown;

	string DisplayText = string.Empty;
	string VisibleText = string.Empty;
	int VisibleStart;
	int VisibleLength;
	float VisibleOffsetX;
	int SelectionStart;
	int SelectionEnd;

	public ConsoleRow()
	{
		AddClass( "consolerow" );
	}

	internal void SetLogEvent( LogEvent e )
	{
		Event = e;

		if ( selectedLine == this && !selectedLine.IsValid() )
			selectedLine = null;

		SetClass( "highlight", selectedLine == this );
		SetClass( "trace", e.Level == LogLevel.Trace );
		SetClass( "info", e.Level == LogLevel.Info );
		SetClass( "warn", e.Level == LogLevel.Warn );
		SetClass( "error", e.Level == LogLevel.Error );
		SetClass( "in", e.Logger == "in" );

		DisplayText = DisplayLine;
		UpdateHorizontalWindow( 0, Box.Rect.Width, EstimatedCharWidth );
		ClearTextSelection();
	}

	internal void UpdateHorizontalWindow( float scrollX, float viewportWidth, float estimatedCharWidth )
	{
		if ( string.IsNullOrEmpty( DisplayText ) )
		{
			VisibleText = string.Empty;
			VisibleStart = 0;
			VisibleLength = 0;
			VisibleOffsetX = 0;
			return;
		}

		if ( viewportWidth <= 0 )
			viewportWidth = Box.Rect.Width;

		var start = Math.Max( 0, (int)MathF.Floor( scrollX / estimatedCharWidth ) - HorizontalBufferChars );
		start = Math.Min( start, DisplayText.Length );

		var length = Math.Max( 1, (int)MathF.Ceiling( viewportWidth / estimatedCharWidth ) + HorizontalBufferChars * 2 );
		length = Math.Min( length, DisplayText.Length - start );

		if ( VisibleStart == start && VisibleLength == length )
			return;

		VisibleStart = start;
		VisibleLength = length;
		VisibleOffsetX = start * estimatedCharWidth;
		VisibleText = DisplayText.Substring( start, length );
	}

	public override void OnDraw( Painter painter )
	{
		base.OnDraw( painter );

		if ( string.IsNullOrEmpty( VisibleText ) || ComputedStyle is null )
			return;

		DrawSelection( painter );

		var textWidth = MathF.Max( EstimatedCharWidth, (VisibleLength + 2) * EstimatedCharWidth );
		var clip = new Vector2( textWidth, Box.RectInner.Height ) * ScaleToScreen;
		if ( clip.x <= 0 || clip.y <= 0 )
			return;

		var scope = new TextRendering.Scope(
			VisibleText,
			ComputedStyle.FontColor ?? Color.White,
			(ComputedStyle.FontSize?.GetPixels( 100 ) ?? 11f) * ScaleToScreen,
			ComputedStyle.FontFamily ?? "Roboto Mono",
			ComputedStyle.FontWeight ?? 400 )
		{
			FontSmooth = ComputedStyle.FontSmooth ?? FontSmooth.Auto
		};

		var texture = TextRendering.GetOrCreateTexture( scope, clip, TextFlag.LeftTop | TextFlag.SingleLine );
		if ( texture?.IsValid() != true )
			return;

		var size = texture.Size * ScaleFromScreen;
		painter.Texture( texture, new Rect( new Vector2( VisibleOffsetX, 0 ), size ) );
	}

	void DrawSelection( Painter painter )
	{
		if ( SelectionStart == SelectionEnd )
			return;

		var start = Math.Min( SelectionStart, SelectionEnd ).Clamp( 0, DisplayText.Length );
		var end = Math.Max( SelectionStart, SelectionEnd ).Clamp( 0, DisplayText.Length );
		if ( start == end )
			return;

		var rect = new Rect(
			start * EstimatedCharWidth,
			0,
			MathF.Max( EstimatedCharWidth, (end - start) * EstimatedCharWidth ),
			Box.RectInner.Height );

		using ( var scope = painter.Scope() )
		{
			painter.Fill = new Fill( Color.Cyan.WithAlpha( 0.35f ) );
			painter.Rect( rect );
		}
	}

	public string GetSelectedText()
	{
		if ( SelectionStart == SelectionEnd || string.IsNullOrEmpty( DisplayText ) )
			return null;

		var start = Math.Min( SelectionStart, SelectionEnd ).Clamp( 0, DisplayText.Length );
		var end = Math.Max( SelectionStart, SelectionEnd ).Clamp( 0, DisplayText.Length );

		if ( start == end )
			return null;

		return DisplayText[start..end];
	}

	public void SelectAllText()
	{
		SelectionStart = 0;
		SelectionEnd = DisplayText?.Length ?? 0;
	}

	public void ClearTextSelection()
	{
		SelectionStart = 0;
		SelectionEnd = 0;
	}

	public void UpdateTextSelection( SelectionEvent e )
	{
		var rect = e.SelectionRect;
		if ( Box.Rect.Bottom < rect.Top || Box.Rect.Top > rect.Bottom )
		{
			ClearTextSelection();
			return;
		}

		if ( string.IsNullOrEmpty( DisplayText ) )
		{
			ClearTextSelection();
			return;
		}

		if ( e.StartPoint.y > e.EndPoint.y )
		{
			(e.EndPoint, e.StartPoint) = (e.StartPoint, e.EndPoint);
		}

		var start = Box.Rect.Top < rect.Top;
		var end = Box.Rect.Bottom > e.EndPoint.y;
		var negwidth = (e.EndPoint - e.StartPoint).x < 0;

		if ( start && end )
		{
			SelectionStart = GetLetterAtScreenPosition( new Vector2( rect.Left, rect.Top ) );
			SelectionEnd = GetLetterAtScreenPosition( new Vector2( rect.Right, rect.Bottom ) );
		}
		else if ( start )
		{
			var from = negwidth ? rect.Right : rect.Left;
			SelectionStart = GetLetterAtScreenPosition( new Vector2( from, rect.Top ) );
			SelectionEnd = DisplayText.Length;
		}
		else if ( end )
		{
			var to = negwidth ? rect.Left : rect.Right;
			SelectionStart = 0;
			SelectionEnd = GetLetterAtScreenPosition( new Vector2( to, rect.Bottom ) );
		}
		else
		{
			SelectionStart = 0;
			SelectionEnd = DisplayText.Length;
		}
	}

	int GetLetterAtScreenPosition( Vector2 screenPosition )
	{
		var local = ScreenPositionToPanelPosition( screenPosition );
		return ((int)MathF.Round( local.x / EstimatedCharWidth )).Clamp( 0, DisplayText?.Length ?? 0 );
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

	string DisplayLine
	{
		get
		{
			var logger = DisplayLogger;
			var message = DisplayMessage;

			if ( Event.Logger == "in" )
				return $"> {message}";

			if ( string.IsNullOrEmpty( logger ) || string.IsNullOrEmpty( message ) )
				return message;

			return $"[{logger}] {message}";
		}
	}

	void CopyMessage()
	{
		Clipboard.SetText( DisplayText );
	}

	void CopyStackTrace()
	{
		Clipboard.SetText( Event.Stack ?? string.Empty );
	}

	void OpenContextMenu()
	{
		var menu = new Menu();
		menu.AddClass( "console-context-menu" );

		menu.AddOption( "Copy Message", "content_copy", CopyMessage ).Shortcut = "Ctrl+C";
		menu.AddOption( "Copy Stack Trace", "subject", CopyStackTrace ).Enabled = !string.IsNullOrWhiteSpace( Event.Stack );

		menu.Open( this, Popup.PositionMode.UnderMouse );
	}

	protected override void OnClick( MousePanelEvent e )
	{
		OnEntryClicked?.Invoke( Event );
		if ( selectedLine.IsValid() && selectedLine != this )
			selectedLine.SetClass( "highlight", false );

		selectedLine = this;
		SetClass( "highlight", true );
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		OnRowMouseDown?.Invoke();
		e.StopPropagation();
	}

	protected override void OnRightClick( MousePanelEvent e )
	{
		base.OnRightClick( e );
		e.StopPropagation();
		OpenContextMenu();
	}
}
