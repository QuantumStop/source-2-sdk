namespace Sandbox.UI.Dev;

using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;

public class DevWindowFrame : Panel
{
	static int NextZIndex = 10;

	public static bool ForceRenderWhileInteracting { get; set; } = true;

	const float MinWindowWidth = 420f;
	const float MinWindowHeight = 320f;

	protected Panel Header;
	protected Panel Tabs;
	protected Panel Content;
	protected Panel ResizeFooter;

	Panel ResizeHandle;
	Panel ResizePreview;
	Label TitleLabel;

	bool IsDragging;
	bool IsResizing;
	bool RootMouseUpHooked;
	internal bool IsInteracting => IsDragging || IsResizing;

	bool ResizingR;
	bool ResizingB;

	readonly DevState<bool> InteractingClass;

	Vector2 DragStartMouse;
	Vector2 DragStartPos;
	Vector2 DragCurrentDelta;

	Vector2 ResizeStartMouse;
	Vector2 ResizeStartSize;
	Vector2 ResizeStartPos;
	Vector2 LastResizeAppliedSize;
	Vector2 ResizePreviewSize;

	bool _dragVisualUpdatedEarly;

	public bool Open { get; set; }
	public bool Focused { get; set; } = true;
	public bool WantsInput => Open && Focused;
	public bool DeleteOnClose { get; set; }
	public bool PreviewResize { get; set; }

	protected Panel ContentPanel => Content;
	protected Panel TabPanel => EnsureTabPanel();

	public DevWindowFrame()
	{
		AddClass( "devwindow" );
		InteractingClass = new DevState<bool>( false, value => SetClass( "interacting", value ) );
		BringToFront();

		Style.Position = PositionMode.Absolute;
		Style.Left = Length.Pixels( 64 );
		Style.Top = Length.Pixels( 64 );
		Style.Width = Length.Pixels( 640 );
		Style.Height = Length.Pixels( 720 );

		Header = Add.Panel( "header" );

		var drag = Header.Add.Panel( "drag" );
		drag.AddEventListener( "onmousedown", () => StartDrag() );
		drag.AddEventListener( "onmouseup", () => StopInteractions() );
		TitleLabel = drag.Add.Label( "DEV WINDOW", "title" );

		var close = Header.AddChild( new Button( null, "close", Close ) );
		close.AddClass( "close" );

		Content = Add.Panel( "content" );

		ResizeFooter = Add.Panel( "resize-footer" );
		ResizeHandle = AddResizeHandle( ResizeFooter );
	}

	public void SetTitle( string title )
	{
		if ( TitleLabel.IsValid() )
			TitleLabel.Text = title ?? "";
	}

	Panel EnsureTabPanel()
	{
		if ( Tabs.IsValid() )
			return Tabs;

		Tabs = Add.Panel( "tabs" );
		if ( Content.IsValid() )
			SetChildIndex( Tabs, GetChildIndex( Content ) );

		return Tabs;
	}

	public void OpenWindow( bool focus = true )
	{
		Open = true;
		Focused = focus;
	}

	public virtual void Close()
	{
		if ( DeleteOnClose )
		{
			Delete( true );
			return;
		}

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
		ClearDragTransform();
		this.SetCursor( CursorType.Move );
	}

	protected void StartResize()
	{
		BringToFront();
		EnsureRootMouseUpHook();
		IsResizing = true;
		ResizingR = true;
		ResizingB = true;
		ResizeStartMouse = Mouse.Position;
		ResizeStartSize = new Vector2( Style.Width?.Value ?? 0.0f, Style.Height?.Value ?? 0.0f );
		ResizeStartPos = new Vector2( Style.Left?.Value ?? 0.0f, Style.Top?.Value ?? 0.0f );
		LastResizeAppliedSize = ResizeStartSize;
		ResizePreviewSize = ResizeStartSize;
		OnResizeStarted();

		this.SetCursor( CursorType.ResizeNWSE );
	}

	protected void StopInteractions()
	{
		if ( IsDragging )
		{
			DragCurrentDelta = GetClampedDragDeltaFromScreen( Mouse.Position - DragStartMouse );
			SetPosition( DragStartPos + DragCurrentDelta );
			ClearDragTransform();
		}

		if ( IsResizing )
		{
			OnResizeFinished();
			var delta = (Mouse.Position - ResizeStartMouse) * ScaleFromScreen;
			SetSize( ResizeStartPos, ResizeStartSize + delta );
			ClampToBounds();
			HideResizePreview();
		}

		if ( IsDragging || IsResizing )
		{
			SaveWindowState();
		}

		IsDragging = false;
		IsResizing = false;
		ResizingR = false;
		ResizingB = false;

		if ( IsValid )
			this.SetCursor( CursorType.Default );
	}

	void EnsureRootMouseUpHook()
	{
		if ( RootMouseUpHooked )
			return;

		var root = FindRootPanel();
		if ( root is null )
			return;

		root.AddEventListener( "onmouseup", StopInteractions );
		RootMouseUpHooked = true;
	}

	public override void Tick()
	{
		base.Tick();
		EnsureRootMouseUpHook();

		var interacting = IsInteracting;
		InteractingClass.Value = interacting;

		if ( !interacting )
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
				UpdateDragVisual();
			}
		}

		if ( IsResizing )
		{
			TickResize();
		}
	}

	void TickResize()
	{
		if ( ResizingR && ResizingB )
		{
			this.SetCursor( CursorType.ResizeNWSE );
		}

		var delta = (Mouse.Position - ResizeStartMouse) * ScaleFromScreen;
		var targetSize = ResizeStartSize + delta;

		if ( OnResizeTick( ResizeStartPos, targetSize ) )
			return;

		if ( PreviewResize )
		{
			UpdateResizePreview( ResizeStartPos, targetSize );
			return;
		}

		HideResizePreview();
		SetSize( ResizeStartPos, targetSize );
	}

	internal void TickDragEarly()
	{
		if ( !IsDragging )
			return;

		UpdateDragVisual();
		_dragVisualUpdatedEarly = true;
	}

	protected override void OnMouseMove( MousePanelEvent e )
	{
		if ( IsDragging )
		{
			UpdateDragVisual();
		}

		base.OnMouseMove( e );
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		BringToFront();
		Focused = true;
		base.OnMouseDown( e );
	}

	public void BringToFront()
	{
		Style.ZIndex = NextZIndex++;
		Style.Dirty();
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
		size = RoundResizeSize( size );

		if ( (size - LastResizeAppliedSize).Length < 0.5f )
			return;

		LastResizeAppliedSize = size;
		Style.Width = Length.Pixels( size.x );
		Style.Height = Length.Pixels( size.y );
	}

	Vector2 RoundResizeSize( Vector2 size )
	{
		return new Vector2( MathF.Round( size.x ), MathF.Round( size.y ) );
	}

	void UpdateResizePreview( Vector2 pos, Vector2 size )
	{
		size = ClampSizeToBounds( pos, size );
		size = RoundResizeSize( size );

		if ( ResizePreview.IsValid() && (size - ResizePreviewSize).Length < 0.5f )
			return;

		ResizePreviewSize = size;

		var preview = EnsureResizePreview();
		preview.Style.Left = Length.Pixels( pos.x );
		preview.Style.Top = Length.Pixels( pos.y );
		preview.Style.Width = Length.Pixels( size.x );
		preview.Style.Height = Length.Pixels( size.y );
		preview.Style.Dirty();
	}

	Panel EnsureResizePreview()
	{
		if ( ResizePreview.IsValid() )
			return ResizePreview;

		ResizePreview = Parent?.Add.Panel( "devwindow-resize-preview" );
		return ResizePreview;
	}

	void HideResizePreview()
	{
		if ( !ResizePreview.IsValid() )
			return;

		ResizePreview.Delete( true );
		ResizePreview = null;
	}

	Vector2 GetClampedDragDeltaFromScreen( Vector2 screenDelta )
	{
		var delta = screenDelta * ScaleFromScreen;
		var size = GetCurrentSize();
		var pos = ClampPositionToBounds( DragStartPos + delta, size );

		return pos - DragStartPos;
	}

	void UpdateDragVisual()
	{
		var targetDelta = GetClampedDragDeltaFromScreen( Mouse.Position - DragStartMouse );
		DragCurrentDelta = targetDelta;
		ApplyDragTransform( targetDelta );
	}

	protected Vector2 GetCurrentPos()
	{
		return new Vector2( Style.Left?.Value ?? 0f, Style.Top?.Value ?? 0f );
	}

	protected Vector2 GetCurrentSize()
	{
		var w = Style.Width?.Value ?? Box.Rect.Width;
		var h = Style.Height?.Value ?? Box.Rect.Height;
		return new Vector2( w, h );
	}

	Rect GetBoundsRect()
	{
		if ( Parent is null || !Parent.IsValid() )
		{
			var w = Screen.Width * ScaleFromScreen;
			var h = Screen.Height * ScaleFromScreen;
			return new Rect( 0, 0, w, h );
		}

		var bounds = Parent.Box.RectInner;
		bounds.Position -= Parent.Box.Rect.Position;
		return bounds * Parent.ScaleFromScreen;
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

	protected virtual void SaveWindowState()
	{
	}

	protected virtual void OnResizeStarted()
	{
	}

	protected virtual bool OnResizeTick( Vector2 pos, Vector2 size )
	{
		return false;
	}

	protected virtual void OnResizeFinished()
	{
	}

	protected Panel AddResizeHandle( Panel parent )
	{
		var handle = parent.Add.Panel( "resize-handle" );
		handle.SetCursor( CursorType.ResizeNWSE );
		handle.AddEventListener( "onmousedown", () => StartResize() );
		handle.AddEventListener( "onmouseup", () => StopInteractions() );
		return handle;
	}
}
