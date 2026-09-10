namespace Sandbox.UI.Dev;

using Sandbox;
using System;

/// <summary>
/// Scroll container with Editor-style scrollbars: scrollbars own the scroll value, content listens.
/// </summary>
public class DevScrollView : Panel
{
	/// <summary>
	/// Compatibility surface for existing call sites. 
	/// In the simplified layout, the scroll view itself is the viewport.
	/// </summary>
	public Panel View => this;

	public Vector2 ContentSize { get; set; }

	Vector2 _scrollOffset;
	public Vector2 CurrentScrollOffset => _scrollOffset;

	public bool EnableVertical { get; set; } = true;
	public bool EnableHorizontal { get; set; } = true;

	public float ScrollStep { get; set; } = 48f;

	public Action<Vector2> OnScroll { get; set; }

	readonly DevScrollBar VBar;
	readonly DevScrollBar HBar;
	readonly Panel Corner;

	Vector2 _viewInset;
	Vector2 _viewSize;

	/// <summary>Current (right,bottom) inset reserved for scrollbars.</summary>
	public Vector2 ViewInset => _viewInset;

	/// <summary>Viewport size excluding scrollbar insets.</summary>
	public Vector2 ViewSize => _viewSize;

	bool _settingInternally;
	bool _mouseDown;
	bool _selectionDragging;

	public DevScrollView()
	{
		AddClass( "devscrollview" );
		CanDragScroll = false;

		VBar = AddChild<DevScrollBar>();
		VBar.Axis = DevScrollBar.ScrollAxis.Vertical;
		VBar.AddClass( "vertical" );
		VBar.OnValueChanged = OnVScrollChanged;

		HBar = AddChild<DevScrollBar>();
		HBar.Axis = DevScrollBar.ScrollAxis.Horizontal;
		HBar.AddClass( "horizontal" );
		HBar.OnValueChanged = OnHScrollChanged;

		Corner = Add.Panel( "corner" );
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );

		if ( e.Button == "mouseleft" )
		{
			_mouseDown = true;
			_selectionDragging = false;
		}
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );

		if ( e.Button == "mouseleft" )
		{
			_mouseDown = false;
			_selectionDragging = false;
		}
	}

	protected override void OnDragSelect( SelectionEvent e )
	{
		// Only treat this as a selection drag if the drag started on actual content, not on the empty viewport.
		if ( !AllowChildSelection || !IsSelectionStartOnContent( e.Target ) )
			return;

		// Keep default selection behavior.
		base.OnDragSelect( e );

		_selectionDragging = true;
		AutoScrollSelection( e.EndPoint, immediate: true );
	}

	bool IsSelectionStartOnContent( Panel target )
	{
		if ( target is null )
			return false;

		if ( target == this )
			return false;

		// Let's not start selection drags from scrollbars/corner (or any of their children) either.
		if ( target.AncestorsAndSelf.Contains( VBar ) || target.AncestorsAndSelf.Contains( HBar ) || target.AncestorsAndSelf.Contains( Corner ) )
			return false;

		return true;
	}

	void OnVScrollChanged( float v )
	{
		if ( _settingInternally )
			return;

		SetScrollOffset( new Vector2( _scrollOffset.x, v ) );
	}

	void OnHScrollChanged( float v )
	{
		if ( _settingInternally )
			return;

		SetScrollOffset( new Vector2( v, _scrollOffset.y ) );
	}

	protected virtual void OnScrolled( Vector2 offset )
	{
		OnScroll?.Invoke( offset );
	}

	Vector2 GetViewportSize( Vector2 viewInset )
	{
		var size = Box.Rect.Size;
		size.x = MathF.Max( 0, size.x - viewInset.x );
		size.y = MathF.Max( 0, size.y - viewInset.y );
		return size;
	}

	Rect GetViewportRect( Vector2 viewInset )
	{
		var r = Box.Rect;
		r.Right -= viewInset.x;
		r.Bottom -= viewInset.y;
		return r;
	}

	public override void Tick()
	{
		base.Tick();

		// Determine scroll range and whether scrollbars are needed.
		// Do a short fixed-point iteration because showing a scrollbar reduces viewport size.
		var viewInset = Vector2.Zero;
		var defaultThickness = 10f;

		for ( var i = 0; i < 2; i++ )
		{
			var view = GetViewportSize( viewInset );

			var max = new Vector2(
				MathF.Max( 0, ContentSize.x - view.x ),
				MathF.Max( 0, ContentSize.y - view.y )
			);

			var showV = EnableVertical && max.y > 0.5f;
			var showH = EnableHorizontal && max.x > 0.5f;

			var vInset = showV ? MathF.Max( defaultThickness, VBar.Box.Rect.Width ) : 0f;
			var hInset = showH ? MathF.Max( defaultThickness, HBar.Box.Rect.Height ) : 0f;

			var next = new Vector2( vInset, hInset );
			if ( (next - viewInset).Length < 0.01f )
			{
				viewInset = next;
				break;
			}

			viewInset = next;
		}

		_viewInset = viewInset;
		_viewSize = GetViewportSize( viewInset );

		var maxFinal = new Vector2(
			MathF.Max( 0, ContentSize.x - _viewSize.x ),
			MathF.Max( 0, ContentSize.y - _viewSize.y )
		);

		// Auto-step based on view size.
		ScrollStep = MathF.Max( 32f, _viewSize.y * 0.1f );

		// Clamp scroll offset to the computed range.
		var clamped = new Vector2( _scrollOffset.x.Clamp( 0, maxFinal.x ), _scrollOffset.y.Clamp( 0, maxFinal.y ) );
		var changed = (clamped - _scrollOffset).Length > 0.01f;
		_scrollOffset = clamped;

		var showVFinal = EnableVertical && maxFinal.y > 0.5f;
		var showHFinal = EnableHorizontal && maxFinal.x > 0.5f;

		var vThickness = showVFinal ? MathF.Max( defaultThickness, VBar.Box.Rect.Width ) : 0f;
		var hThickness = showHFinal ? MathF.Max( defaultThickness, HBar.Box.Rect.Height ) : 0f;

		VBar.PageStep = _viewSize.y;
		VBar.Minimum = 0;
		VBar.Maximum = maxFinal.y;
		VBar.SetClass( "hidden", !showVFinal );

		HBar.PageStep = _viewSize.x;
		HBar.Minimum = 0;
		HBar.Maximum = maxFinal.x;
		HBar.SetClass( "hidden", !showHFinal );

		// Reserve space for scrollbars so content doesn't sit underneath them.
		Style.PaddingRight = vThickness;
		Style.PaddingBottom = hThickness;
		Style.Dirty();

		// Bars should meet each other (no gap between bars), only between content and bars.
		VBar.Style.Bottom = hThickness;
		VBar.Style.Right = 0;
		VBar.Style.Top = 0;
		VBar.Style.Dirty();

		HBar.Style.Right = vThickness;
		HBar.Style.Left = 0;
		HBar.Style.Bottom = 0;
		HBar.Style.Dirty();

		Corner.Style.Width = vThickness;
		Corner.Style.Height = hThickness;
		Corner.Style.Right = 0;
		Corner.Style.Bottom = 0;
		Corner.Style.Dirty();

		Corner.SetClass( "hidden", VBar.HasClass( "hidden" ) || HBar.HasClass( "hidden" ) );

		// Keep bar thumbs synced if content/view size changes.
		if ( !_settingInternally )
		{
			_settingInternally = true;
			VBar.Value = _scrollOffset.y.Clamp( VBar.Minimum, VBar.Maximum );
			HBar.Value = _scrollOffset.x.Clamp( HBar.Minimum, HBar.Maximum );
			_settingInternally = false;
		}

		if ( changed )
			OnScrolled( _scrollOffset );

		// Auto-scroll while drag-selecting near edges (horizontal + vertical).
		if ( _mouseDown && _selectionDragging && AllowChildSelection && !VBar.IsDragging && !HBar.IsDragging )
		{
			AutoScrollSelection( Mouse.Position, immediate: false );
		}
	}

	public override void OnMouseWheel( Vector2 value )
	{
		// We don't rely on CSS "overflow: scroll" so implement wheel scrolling ourselves.
		// Positive values scroll down / right (see Panel.OnMouseWheel docs).
		var delta = value * ScrollStep;

		if ( !EnableHorizontal ) delta.x = 0;
		if ( !EnableVertical ) delta.y = 0;

		if ( delta.IsNearZeroLength )
			return;

		SetScrollOffset( _scrollOffset + delta );
	}

	public void SetScrollOffset( Vector2 offset )
	{
		var view = _viewSize;
		var max = new Vector2(
			MathF.Max( 0, ContentSize.x - view.x ),
			MathF.Max( 0, ContentSize.y - view.y )
		);

		offset.x = offset.x.Clamp( 0, max.x );
		offset.y = offset.y.Clamp( 0, max.y );

		if ( (offset - _scrollOffset).Length < 0.01f )
			return;

		_scrollOffset = offset;

		_settingInternally = true;
		VBar.Value = _scrollOffset.y.Clamp( VBar.Minimum, VBar.Maximum );
		HBar.Value = _scrollOffset.x.Clamp( HBar.Minimum, HBar.Maximum );
		_settingInternally = false;

		OnScrolled( _scrollOffset );
	}

	void AutoScrollSelection( Vector2 screenPos, bool immediate )
	{
		var view = GetViewportRect( _viewInset );
		if ( view.Width <= 0 || view.Height <= 0 )
			return;

		var edge = 24f;
		var dx = 0f;
		var dy = 0f;

		if ( EnableHorizontal && HBar.Maximum > 0.5f )
		{
			if ( screenPos.x > view.Right - edge )
				dx = (screenPos.x - (view.Right - edge)).Clamp( 0, edge ) / edge;
			else if ( screenPos.x < view.Left + edge )
				dx = -( (view.Left + edge) - screenPos.x ).Clamp( 0, edge ) / edge;
		}

		if ( EnableVertical && VBar.Maximum > 0.5f )
		{
			if ( screenPos.y > view.Bottom - edge )
				dy = (screenPos.y - (view.Bottom - edge)).Clamp( 0, edge ) / edge;
			else if ( screenPos.y < view.Top + edge )
				dy = -( (view.Top + edge) - screenPos.y ).Clamp( 0, edge ) / edge;
		}

		if ( MathF.Abs( dx ) < 0.001f && MathF.Abs( dy ) < 0.001f )
			return;

		// Speed scales with how far into the edge region the cursor is.
		var speed = ScrollStep * 4f;
		var delta = new Vector2( dx, dy ) * speed;

		if ( !immediate )
			delta *= Time.Delta;

		SetScrollOffset( _scrollOffset + delta );
	}
}
