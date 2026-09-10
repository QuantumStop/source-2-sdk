namespace Sandbox.UI.Dev;

using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;

[Library( "devwindow" )]
public sealed class DevWindow : Panel
{
	[ConVar( "devui_window_force_render_while_interacting" )]
	public static bool ForceRenderWhileInteracting { get; set; } = true;

	[ConVar( "devui_window_drag_smooth_time" )]
	public static float DragSmoothTime { get; set; } = 0.0f;

	[ConVar( "devui_window_drag_lead_frames" )]
	public static float DragLeadFrames { get; set; } = 0.0f;

	const string CookieX = "devui.window.x";
	const string CookieY = "devui.window.y";
	const string CookieW = "devui.window.w";
	const string CookieH = "devui.window.h";
	const string CookieTab = "devui.window.tab"; // legacy int cookie
	const string CookieTabId = "devui.window.tabid";
	const float MinWindowWidth = 420f;
	const float MinWindowHeight = 320f;

	Panel Header;
	Panel Tabs;
	Panel LogToggles;
	
	Panel Content;
	Panel ResizeHandle;

	DevLogTab LogTab;
	DevDebugTab DebugTab;

	readonly List<Button> _tabButtons = new();
	readonly Dictionary<string, Panel> _tabPanels = new( StringComparer.OrdinalIgnoreCase );
	readonly Dictionary<string, int> _tabOrders = new( StringComparer.OrdinalIgnoreCase );
	string _activeTabId;
	int _seenTabRegistryVersion = -1;

	bool IsDragging;
	bool IsResizing;
	bool RootMouseUpHooked;
	internal bool IsInteracting => IsDragging || IsResizing;

	// Keep explicit resize direction flags for future extension.
	bool ResizingR;
	bool ResizingB;

	Vector2 DragStartMouse;
	Vector2 DragStartPos;
	Vector2 DragCurrentDelta;
	Vector2 DragVisualDelta;
	Vector2 DragVisualVelocity;
	Vector2 DragLastTargetDelta;
	Vector2 DragTargetVelocity;

	Vector2 ResizeStartMouse;
	Vector2 ResizeStartSize;
	Vector2 ResizeStartPos;

	bool _dragVisualUpdatedEarly;

	string ActiveTabId => string.IsNullOrWhiteSpace( _activeTabId ) ? "log" : _activeTabId;

	public bool Open { get; set; }
	public bool Focused { get; set; } = true;
	public bool WantsInput => Open && Focused;

	public DevWindow()
	{
		AddClass( "devwindow" );

		Style.Position = PositionMode.Absolute;

		var x = Game.Cookies.Get( CookieX, 64.0f );
		var y = Game.Cookies.Get( CookieY, 64.0f );
		var w = Game.Cookies.Get( CookieW, 640.0f );
		var h = Game.Cookies.Get( CookieH, 720.0f );

		Style.Left = Length.Pixels( x );
		Style.Top = Length.Pixels( y );
		Style.Width = Length.Pixels( w );
		Style.Height = Length.Pixels( h );

		_activeTabId = Game.Cookies.Get( CookieTabId, "" );
		if ( string.IsNullOrWhiteSpace( _activeTabId ) )
		{
			// Fallback to legacy enum cookie.
			var legacy = Game.Cookies.Get( CookieTab, 0 );
			_activeTabId = legacy switch
			{
				1 => "debug",
				_ => "log"
			};
		}

		Header = Add.Panel( "header" );

		var drag = Header.Add.Panel( "drag" );
		drag.AddEventListener( "onmousedown", () => StartDrag() );
		drag.AddEventListener( "onmouseup", () => StopInteractions() );
		drag.Add.Label( "CONSOLE", "title" );

		var close = Header.AddChild( new Button( null, "close", Close ) );
		close.AddClass( "close" );

		Tabs = Add.Panel( "tabs" );

		Tabs.Add.Panel( "spacer" );
		LogToggles = Tabs.Add.Panel( "log-toggles" );

		Content = Add.Panel( "content" );

		LogTab = Content.AddChild<DevLogTab>();
		DebugTab = Content.AddChild<DevDebugTab>();

		LogTab?.Console?.CreateLevelToggles( LogToggles );

		ResizeHandle = Add.Panel( "resize-handle" );
		ResizeHandle.SetCursor( CursorType.ResizeNWSE );
		ResizeHandle.AddEventListener( "onmousedown", () => StartResize() );
		ResizeHandle.AddEventListener( "onmouseup", () => StopInteractions() );

		RebuildTabs();
		SetTab( ActiveTabId );
	}

	public void FocusConsole()
	{
		SetTab( "log" );
		LogTab?.FocusConsole();
	}

	public void BlurConsole() => LogTab?.BlurConsole();

	public void OpenWindow( bool focus = true )
	{
		Open = true;
		Focused = focus;
	}

	public void Close()
	{
		Open = false;
		Focused = false;
	}

	public void ToggleOpen()
	{
		if ( Open )
		{
			Close();
			return;
		}

		OpenWindow();
	}

	public void ToggleFocus()
	{
		if ( !Open )
		{
			OpenWindow();
			return;
		}

		Focused = !Focused;
	}

	void StartDrag()
	{
		EnsureRootMouseUpHook();
		IsDragging = true;
		DragStartMouse = Mouse.Position;
		DragStartPos = new Vector2( Style.Left?.Value ?? 0.0f, Style.Top?.Value ?? 0.0f );
		DragCurrentDelta = 0;
		DragVisualDelta = 0;
		DragVisualVelocity = 0;
		DragLastTargetDelta = 0;
		DragTargetVelocity = 0;
		ClearDragTransform();
		this.SetCursor( CursorType.Move );
	}

	void StartResize()
	{
		EnsureRootMouseUpHook();
		IsResizing = true;
		ResizingR = true;
		ResizingB = true;
		ResizeStartMouse = Mouse.Position;
		ResizeStartSize = new Vector2( Style.Width?.Value ?? 0.0f, Style.Height?.Value ?? 0.0f );
		ResizeStartPos = new Vector2( Style.Left?.Value ?? 0.0f, Style.Top?.Value ?? 0.0f );
		this.SetCursor( CursorType.ResizeNWSE );
	}

	void StopInteractions()
	{
		if ( IsDragging )
		{
			// Commit final position and clear visual transform.
			DragCurrentDelta = GetClampedDragDeltaFromScreen( Mouse.Position - DragStartMouse );
			SetPosition( DragStartPos + DragCurrentDelta );
			ClearDragTransform();
		}

		if ( IsResizing )
		{
			// Ensure final size/position are clamped before persisting.
			ClampToBounds();
		}

		if ( IsDragging || IsResizing )
		{
			SaveCookies();
		}

		IsDragging = false;
		IsResizing = false;
		ResizingR = false;
		ResizingB = false;
		this.SetCursor( CursorType.Default );
	}

	void EnsureRootMouseUpHook()
	{
		if ( RootMouseUpHooked )
			return;

		var root = FindRootPanel();
		if ( root is null )
			return;

		// Without mouse capture, we need to handle mouse-up anywhere to avoid getting stuck dragging/resizing.
		root.AddEventListener( "onmouseup", StopInteractions );
		RootMouseUpHooked = true;
	}

	public override void Tick()
	{
		base.Tick();
		EnsureRootMouseUpHook();

		// Rebuild extension tabs after hotload.
		if ( _seenTabRegistryVersion != DevUiTabRegistry.Version )
		{
			RebuildTabs();
			SetTab( ActiveTabId );
		}

		SetClass( "interacting", IsInteracting );

		// Keep the window inside its parent bounds when not actively interacting (screen resize, padding changes, etc).
		if ( !IsInteracting )
		{
			ClampToBounds();
		}

		if ( IsDragging )
		{
			if ( _dragVisualUpdatedEarly )
			{
				_dragVisualUpdatedEarly = false;
			}
			else
			{
				UpdateDragVisual( advanceSmoothing: true );
			}
		}

		if ( IsResizing )
		{
			// Use resize flags so we can extend to edge/corner resizing later (XGUI-style).
			if ( ResizingR && ResizingB )
			{
				this.SetCursor( CursorType.ResizeNWSE );
			}

			var delta = (Mouse.Position - ResizeStartMouse) * ScaleFromScreen;
			SetSize( ResizeStartPos, ResizeStartSize + delta );
		}

		// If the window contents are static, the UI renderer can effectively "cache" and only redraw when something
		// marks itself dirty. During drag/resize we want consistent redraw to avoid an extra frame of perceived lag
		// compared to the hardware cursor.
		if ( ForceRenderWhileInteracting && IsInteracting )
		{
			MarkRenderDirty();
		}

	}

	internal void TickDragEarly()
	{
		if ( !IsDragging )
			return;

		UpdateDragVisual( advanceSmoothing: true );
		_dragVisualUpdatedEarly = true;
	}

	protected override void OnMouseMove( MousePanelEvent e )
	{
		if ( IsDragging && DragSmoothTime <= 0.0f )
		{
			UpdateDragVisual( advanceSmoothing: false );
			if ( ForceRenderWhileInteracting )
			{
				MarkRenderDirty();
			}
		}

		base.OnMouseMove( e );
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		Focused = true;
		base.OnMouseDown( e );
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );
		StopInteractions();
	}

	void SetPosition( Vector2 pos )
	{
		var size = GetCurrentSize();
		pos = ClampPositionToBounds( pos, size );

		Style.Left = Length.Pixels( pos.x );
		Style.Top = Length.Pixels( pos.y );
	}

	void SetSize( Vector2 pos, Vector2 size )
	{
		size = ClampSizeToBounds( pos, size );

		Style.Width = Length.Pixels( size.x );
		Style.Height = Length.Pixels( size.y );
	}

	Vector2 GetClampedDragDeltaFromScreen( Vector2 screenDelta )
	{
		// Convert screen-space mouse delta to panel/style-space delta and clamp there.
		// This keeps drag behavior consistent across UI scale factors.
		var delta = screenDelta * ScaleFromScreen;

		var size = GetCurrentSize();
		var pos = ClampPositionToBounds( DragStartPos + delta, size );

		return pos - DragStartPos;
	}

	void UpdateDragVisual( bool advanceSmoothing )
	{
		var targetDelta = GetClampedDragDeltaFromScreen( Mouse.Position - DragStartMouse );

		if ( advanceSmoothing )
		{
			UpdateDragTargetVelocity( targetDelta );
		}

		DragCurrentDelta = targetDelta;
		var predictedDelta = GetPredictedDragDelta( targetDelta );
		DragVisualDelta = advanceSmoothing ? GetSmoothedDragDelta( predictedDelta ) : predictedDelta;
		ApplyDragTransform( DragVisualDelta );
	}

	void UpdateDragTargetVelocity( Vector2 targetDelta )
	{
		var dt = Time.Delta;
		if ( dt > 0.0001f )
		{
			DragTargetVelocity = (targetDelta - DragLastTargetDelta) / dt;
		}

		DragLastTargetDelta = targetDelta;
	}

	Vector2 GetPredictedDragDelta( Vector2 targetDelta )
	{
		var leadFrames = DragLeadFrames.Clamp( 0.0f, 3.0f );
		if ( leadFrames <= 0.0f )
			return targetDelta;

		var predicted = targetDelta + DragTargetVelocity * Time.Delta * leadFrames;
		var size = GetCurrentSize();
		var pos = ClampPositionToBounds( DragStartPos + predicted, size );
		return pos - DragStartPos;
	}

	Vector2 GetSmoothedDragDelta( Vector2 targetDelta )
	{
		var smoothTime = DragSmoothTime;
		if ( smoothTime <= 0.0f )
		{
			DragVisualVelocity = 0;
			return targetDelta;
		}

		var vx = DragVisualVelocity.x;
		var vy = DragVisualVelocity.y;
		var dt = Time.Delta;

		var visual = new Vector2(
			MathX.SmoothDamp( DragVisualDelta.x, targetDelta.x, ref vx, smoothTime, dt ),
			MathX.SmoothDamp( DragVisualDelta.y, targetDelta.y, ref vy, smoothTime, dt ) );

		DragVisualVelocity = new Vector2( vx, vy );
		return visual;
	}

	Vector2 GetCurrentPos()
	{
		return new Vector2( Style.Left?.Value ?? 0f, Style.Top?.Value ?? 0f );
	}

	Vector2 GetCurrentSize()
	{
		var w = Style.Width?.Value ?? Box.Rect.Width;
		var h = Style.Height?.Value ?? Box.Rect.Height;
		return new Vector2( w, h );
	}

	Rect GetBoundsRect()
	{
		// Clamp relative to the parent padding.
		if ( Parent is null || !Parent.IsValid() )
		{
			var w = Screen.Width * ScaleFromScreen;
			var h = Screen.Height * ScaleFromScreen;
			return new Rect( 0, 0, w, h );
		}

		return Parent.Box.RectInner;
	}

	Vector2 ClampPositionToBounds( Vector2 pos, Vector2 size )
	{
		var bounds = GetBoundsRect();

		var minX = bounds.Left;
		var minY = bounds.Top;
		var maxX = bounds.Right - size.x;
		var maxY = bounds.Bottom - size.y;

		if ( maxX < minX ) maxX = minX;
		if ( maxY < minY ) maxY = minY;

		pos.x = pos.x.Clamp( minX, maxX );
		pos.y = pos.y.Clamp( minY, maxY );
		return pos;
	}

	Vector2 ClampSizeToBounds( Vector2 pos, Vector2 size )
	{
		var bounds = GetBoundsRect();

		var maxW = MathF.Max( 0, bounds.Right - pos.x );
		var maxH = MathF.Max( 0, bounds.Bottom - pos.y );

		// Keep mins, but if the parent can't fit them, shrink to what's available.
		var minW = MathF.Min( MinWindowWidth, maxW > 0 ? maxW : MinWindowWidth );
		var minH = MathF.Min( MinWindowHeight, maxH > 0 ? maxH : MinWindowHeight );

		if ( maxW > 0 )
			size.x = size.x.Clamp( minW, maxW );
		else
			size.x = MathF.Max( minW, size.x );

		if ( maxH > 0 )
			size.y = size.y.Clamp( minH, maxH );
		else
			size.y = MathF.Max( minH, size.y );

		return size;
	}

	void ClampToBounds()
	{
		var pos = GetCurrentPos();
		var size = GetCurrentSize();

		// Clamp size first, then position, to keep the bottom-right handle in-bounds.
		var clampedSize = ClampSizeToBounds( pos, size );
		var clampedPos = ClampPositionToBounds( pos, clampedSize );

		var posChanged = (clampedPos - pos).Length > 0.01f;
		var sizeChanged = (clampedSize - size).Length > 0.01f;

		if ( sizeChanged )
		{
			Style.Width = Length.Pixels( clampedSize.x );
			Style.Height = Length.Pixels( clampedSize.y );
		}

		if ( posChanged )
		{
			Style.Left = Length.Pixels( clampedPos.x );
			Style.Top = Length.Pixels( clampedPos.y );
		}
	}

	void ApplyDragTransform( Vector2 delta )
	{
		// Using transform avoids relayout churn from updating Left/Top every frame
		var tx = new PanelTransform();
		tx.AddTranslate( Length.Pixels( delta.x ), Length.Pixels( delta.y ) );
		Style.Transform = tx;
		Style.Dirty();
	}

	void ClearDragTransform()
	{
		Style.Transform = null;
		Style.Dirty();
	}

	void SaveCookies()
	{
		Game.Cookies.Set( CookieX, Style.Left?.Value ?? 0.0f );
		Game.Cookies.Set( CookieY, Style.Top?.Value ?? 0.0f );
		Game.Cookies.Set( CookieW, Style.Width?.Value ?? 0.0f );
		Game.Cookies.Set( CookieH, Style.Height?.Value ?? 0.0f );
		Game.Cookies.Set( CookieTabId, ActiveTabId );
	}

	void RebuildTabs()
	{
		// Remove previously-created extension tab panels to avoid leaving "active" panels around after hotload.
		// (Old panels are no longer tracked in _tabPanels, so SetTab() won't deactivate them.)
		foreach ( var child in Content?.Children?.ToArray() ?? Array.Empty<Panel>() )
		{
			if ( child is null ) continue;
			if ( child == LogTab ) continue;
			if ( child == DebugTab ) continue;
			child.Delete( true );
		}

		Tabs?.DeleteChildren( true );
		_tabButtons.Clear();
		_tabPanels.Clear();
		_tabOrders.Clear();

		void AddTab( string id, string title, int order, Panel panel )
		{
			_tabPanels[id] = panel;
			_tabOrders[id] = order;

			var b = Tabs.AddChild( new Button( title, null, () => SetTab( id ) ) );
			b.SetAttribute( "tabid", id );
			_tabButtons.Add( b );
		}

		// Built-in tabs first (stable order/ids).
		AddTab( "log", "LOG", 0, LogTab );
		AddTab( "debug", "DEBUG", 10, DebugTab );

		// Spacer + log toggles container (same row).
		Tabs.Add.Panel( "spacer" );
		LogToggles = Tabs.Add.Panel( "log-toggles" );
		LogTab?.Console?.CreateLevelToggles( LogToggles );

		// Extension tabs (project-defined).
		_seenTabRegistryVersion = DevUiTabRegistry.Version;

		foreach ( var tab in DevUiTabRegistry.Tabs )
		{
			if ( string.Equals( tab.Id, "log", StringComparison.OrdinalIgnoreCase ) ) continue;
			if ( string.Equals( tab.Id, "debug", StringComparison.OrdinalIgnoreCase ) ) continue;

			Panel panel;
			try
			{
				panel = Activator.CreateInstance( tab.Type ) as Panel;
			}
			catch
			{
				continue;
			}

			if ( panel is null )
				continue;

			// Wrap custom tabs in a standard scroll + padded canvas.
			var host = Content.AddChild<DevCustomTabHost>();
			host.SetContent( panel );

			AddTab( tab.Id, tab.Title, tab.Order, host );
		}

		// Keep tab buttons in the desired order.
		var ordered = _tabButtons
			.Select( b => new { Button = b, Id = b.GetAttribute( "tabid", "" ) } )
			.OrderBy( x => _tabOrders.TryGetValue( x.Id, out var o ) ? o : 10_000 )
			.ThenBy( x => x.Button.Text, StringComparer.OrdinalIgnoreCase )
			.ToList();

		for ( var i = 0; i < ordered.Count; i++ )
		{
			Tabs.SetChildIndex( ordered[i].Button, i );
		}

		// Ensure spacer + toggles are last (right aligned).
		var spacer = Tabs.Children.FirstOrDefault( x => x.HasClass( "spacer" ) );
		if ( spacer is not null ) Tabs.SetChildIndex( spacer, Tabs.ChildrenCount - 1 );
		if ( LogToggles is not null ) Tabs.SetChildIndex( LogToggles, Tabs.ChildrenCount - 1 );
	}

	void SetTab( string id )
	{
		_activeTabId = id ?? "log";
		Game.Cookies.Set( CookieTabId, ActiveTabId );

		// Clear any previously-active panels (including any leaked panels not tracked in _tabPanels).
		foreach ( var child in Content?.Children ?? Array.Empty<Panel>() )
		{
			child?.SetClass( "active", false );
		}

		foreach ( var kv in _tabPanels )
		{
			kv.Value?.SetClass( "active", string.Equals( kv.Key, ActiveTabId, StringComparison.OrdinalIgnoreCase ) );
		}

		foreach ( var b in _tabButtons )
		{
			var tabId = b.GetAttribute( "tabid", "" );
			b.Active = string.Equals( tabId, ActiveTabId, StringComparison.OrdinalIgnoreCase );
		}

		LogToggles?.SetClass( "hidden", !string.Equals( ActiveTabId, "log", StringComparison.OrdinalIgnoreCase ) );
	}

}
