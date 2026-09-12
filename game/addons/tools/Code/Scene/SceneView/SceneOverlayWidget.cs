namespace Editor;

using Editor.Preferences;

public class SceneOverlayWidget : Widget
{
	private const float FocusBorderPadding = 10.0f;
	private const float EjectedBorderPadding = 15.0f;
	private const float BorderThickness = 2.0f;
	private const float EjectedSquareSize = 3.0f;
	private const float EjectedSquareStep = 7.0f;

	public static SceneOverlayWidget Active { get; private set; }

	public Layout Header { get; private set; }

	internal SceneOverlayWidget( Widget parent ) : base( parent )
	{
		TranslucentBackground = true;
		NoSystemBackground = true;

		WindowFlags = WindowFlags.FramelessWindowHint | WindowFlags.Tool;

		// Cocoa and X11 won't let clicks through to the viewport underneath without it. On Windows it
		// would also stop the tool widgets parented in here (EditorTool.AddOverlay) getting any.
		if ( !OperatingSystem.IsWindows() )
			WindowFlags |= WindowFlags.WindowTransparentForInput;

		Active = this;

		Layout = Layout.Column();
		Layout.Margin = 16;

		var header = Layout.AddRow();
		header.AddStretchCell();
		Header = header.AddRow();
		Header.Spacing = 4;

		Layout.AddStretchCell();

		// doesn't handle floating windows, but there's no way to hook into dockwrapper events right now
		EditorWindow.Moved += UpdateDimensions;

		TransparentForMouseEvents = true;
	}

	public override void OnDestroyed()
	{
		base.OnDestroyed();

		if ( EditorWindow.IsValid() )
		{
			EditorWindow.Moved -= UpdateDimensions;
		}
	}

	int lastGeometryHash = -1;

	[EditorEvent.Frame]
	private void UpdateDimensions()
	{
		if ( !Parent.IsValid() )
			return;

		// this wasn't always being triggered properly when relying on widget events from the parent (causing HUGE jank)
		int geometryHash = HashCode.Combine( Parent.ScreenPosition, Parent.Size );
		if ( lastGeometryHash != geometryHash )
		{
			Position = Parent.ScreenPosition;
			Size = Parent.Size;
		}

		lastGeometryHash = geometryHash;
	}

	internal RealTimeSince timeSinceNeededRedraw = 0.0f;
	private bool _lastFocusedState;
	private bool _lastEjectedState;

	[EditorEvent.Frame]
	public void Frame()
	{
		if ( CustomEditorPreferences.ShowViewportStateOverlay && Parent is SceneViewportWidget viewport )
		{
			bool isEjected = ShouldShowEjectedState( viewport );
			bool isFocused = ShouldShowFocusedState( viewport );

			if ( isEjected != _lastEjectedState || isFocused != _lastFocusedState )
			{
				_lastEjectedState = isEjected;
				_lastFocusedState = isFocused;
				Update();
			}
		}

		if ( timeSinceNeededRedraw > 0.1f
			|| SceneOverlayNotifications.ShouldAnimate
			|| SceneEditorExtensions.ShouldDrawCenteredFlyCursor )
		{
			Update();
			timeSinceNeededRedraw = 0.0f;
		}
	}

	protected override void OnPaint()
	{
		Active = this;

		if ( CustomEditorPreferences.ShowViewportStateOverlay && Parent is SceneViewportWidget vw )
		{
			var isEjected = ShouldShowEjectedState( vw );
			var isFocused = ShouldShowFocusedState( vw );

			if ( isEjected )
			{
				DrawViewportStateBorderSquares( Theme.Yellow, EjectedBorderPadding );
				DrawViewportStateLabel( "Ejected", Theme.Yellow, EjectedBorderPadding );
			}
			else if ( isFocused )
			{
				DrawViewportStateBorder( Theme.Red, PenStyle.Solid, FocusBorderPadding );
			}

			if ( vw.SceneView.CurrentView == SceneViewWidget.ViewMode.Game )
			{
				EditorEvent.Run( "sceneview.paintoverlay" );
			}
			else
			{
				vw.DrawOrientationGizmo();
			}
		}

		SceneOverlayNotifications.Draw( this );

		if ( SceneEditorExtensions.ShouldDrawCenteredFlyCursor )
		{
			DrawCenteredCursor();
		}
	}

	private void DrawCenteredCursor()
	{
		var center = new Vector2( Size.x * 0.5f, Size.y * 0.5f );
		var line = 7.0f;
		var gap = 3.0f;
		var thickness = 1.5f;

		Paint.SetPen( Color.White.WithAlpha( 0.9f ), thickness );
		Paint.DrawLine( center + new Vector2( -line, 0 ), center + new Vector2( -gap, 0 ) );
		Paint.DrawLine( center + new Vector2( gap, 0 ), center + new Vector2( line, 0 ) );
		Paint.DrawLine( center + new Vector2( 0, -line ), center + new Vector2( 0, -gap ) );
		Paint.DrawLine( center + new Vector2( 0, gap ), center + new Vector2( 0, line ) );
	}

	private static bool IsViewportFocused( SceneViewportWidget viewport )
	{
		if ( !viewport.IsActiveWindow )
			return false;

		var focusWidget = Application.FocusWidget;
		if ( !focusWidget.IsValid() )
			return false;

		return IsChildOf( focusWidget, viewport );
	}

	private static bool IsChildOf( Widget widget, Widget possibleParent )
	{
		for ( var it = widget; it.IsValid(); it = it.Parent )
		{
			if ( it == possibleParent )
				return true;
		}

		return false;
	}

	private static bool ShouldShowEjectedState( SceneViewportWidget viewport )
	{
		return viewport.SceneView.CurrentView == SceneViewWidget.ViewMode.GameEjected;
	}

	private static bool ShouldShowFocusedState( SceneViewportWidget viewport )
	{
		// If the game is running we don't want focus state, not necessary for now
		if ( viewport.SceneView.CurrentView == SceneViewWidget.ViewMode.Game )
			return false;

		// Ejected state uses its own yellow indicator.
		return viewport.SceneView.CurrentView != SceneViewWidget.ViewMode.GameEjected && IsViewportFocused( viewport );
	}

	private void DrawViewportStateBorder( Color color, PenStyle penStyle, float padding )
	{
		Paint.ClearBrush();
		Paint.SetPen( color.WithAlpha( 0.95f ), BorderThickness, penStyle );
		Paint.DrawRect( LocalRect.Shrink( padding ) );
	}

	private void DrawViewportStateBorderSquares( Color color, float padding )
	{
		var rect = LocalRect.Shrink( padding );
		Paint.ClearPen();
		Paint.SetBrush( color.WithAlpha( 0.95f ) );
		var left = MathF.Round( rect.Left );
		var top = MathF.Round( rect.Top );
		var right = MathF.Round( rect.Right - EjectedSquareSize );
		var bottom = MathF.Round( rect.Bottom - EjectedSquareSize );

		if ( right < left || bottom < top )
			return;

		var xSpan = right - left;
		var ySpan = bottom - top;
		var xSegments = xSpan <= 0.001f ? 0 : Math.Max( 1, (int)MathF.Round( xSpan / EjectedSquareStep ) );
		var ySegments = ySpan <= 0.001f ? 0 : Math.Max( 1, (int)MathF.Round( ySpan / EjectedSquareStep ) );

		if ( xSegments == 0 )
		{
			Paint.DrawRect( new Rect( left, top, EjectedSquareSize, EjectedSquareSize ) );
			Paint.DrawRect( new Rect( left, bottom, EjectedSquareSize, EjectedSquareSize ) );
		}
		else
		{
			for ( int i = 0; i <= xSegments; i++ )
			{
				var t = i / (float)xSegments;
				var x = MathF.Round( left + xSpan * t );
				Paint.DrawRect( new Rect( x, top, EjectedSquareSize, EjectedSquareSize ) );
				Paint.DrawRect( new Rect( x, bottom, EjectedSquareSize, EjectedSquareSize ) );
			}
		}

		if ( ySegments == 0 )
			return;

		// Skip first/last to avoid drawing corner squares twice.
		for ( int i = 1; i < ySegments; i++ )
		{
			var t = i / (float)ySegments;
			var y = MathF.Round( top + ySpan * t );
			Paint.DrawRect( new Rect( left, y, EjectedSquareSize, EjectedSquareSize ) );
			Paint.DrawRect( new Rect( right, y, EjectedSquareSize, EjectedSquareSize ) );
		}
	}

	private void DrawViewportStateLabel( string text, Color color, float borderPadding )
	{
		// Keep text centered on the marker line, while scaling with viewport size.
		var scaledPadding = MathF.Min( borderPadding, MathF.Max( 4.0f, Height * 0.08f ) );
		var fontSize = Math.Clamp( Height * 0.016f, 8.0f, 12.0f );
		var labelHeight = fontSize + 12.0f;
		var centerY = Height - scaledPadding - (EjectedSquareSize * 0.5f);
		var rect = new Rect( 0, centerY - (labelHeight * 0.5f), Width, labelHeight );
		Paint.SetDefaultFont( fontSize, 700 );
		Paint.SetPen( Color.Black.WithAlpha( 0.9f ) );
		Paint.DrawText( new Rect( rect.Position + new Vector2( -1, 0 ), rect.Size ), text, TextFlag.Center );
		Paint.DrawText( new Rect( rect.Position + new Vector2( 1, 0 ), rect.Size ), text, TextFlag.Center );
		Paint.DrawText( new Rect( rect.Position + new Vector2( 0, -1 ), rect.Size ), text, TextFlag.Center );
		Paint.DrawText( new Rect( rect.Position + new Vector2( 0, 1 ), rect.Size ), text, TextFlag.Center );
		Paint.SetPen( color );
		Paint.DrawText( rect, text, TextFlag.Center );
		Paint.SetDefaultFont();
	}
}
