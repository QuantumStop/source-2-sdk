namespace Sandbox.UI.Dev;

using Sandbox;
using Sandbox.UI.Construct;
using System;

/// <summary>
/// Convenience scroll container for DevUI tabs/panels.
/// Wraps <see cref="DevScrollView"/> with a translated content canvas that auto-measures itself.
/// </summary>
public sealed class DevScrollPanel : DevScrollView
{
	/// <summary>Container for children (add your content here).</summary>
	public Panel Canvas { get; }

	public new Vector2 ScrollOffset
	{
		get => CurrentScrollOffset;
		set => SetScrollOffset( value );
	}

	public DevScrollPanel()
	{
		AddClass( "devscrollpanel" );
		CanDragScroll = false;

		Canvas = Add.Panel( "canvas" );
		Canvas.AddClass( "devscrollpanel_canvas" );
		Canvas.CanDragScroll = false;

		Canvas.Style.Position = PositionMode.Absolute;
		Canvas.Style.Left = 0;
		Canvas.Style.Top = 0;
		Canvas.Style.Dirty();
	}

	protected override void OnScrolled( Vector2 offset )
	{
		if ( Canvas.IsValid() )
		{
			Canvas.Style.Transform = null;
			Canvas.Style.Left = -offset.x;
			Canvas.Style.Top = -offset.y;
			Canvas.Style.Dirty();
		}

		base.OnScrolled( offset );
	}

	public override void Tick()
	{
		UpdateContentSize();
		base.Tick();
		UpdatePaddingClasses();
	}

	void UpdatePaddingClasses()
	{
		var vVisible = false;
		var hVisible = false;

		foreach ( var bar in ChildrenOfType<DevScrollBar>() )
		{
			if ( bar.HasClass( "hidden" ) )
				continue;

			if ( bar.HasClass( "vertical" ) ) vVisible = true;
			if ( bar.HasClass( "horizontal" ) ) hVisible = true;
		}

		SetClass( "has-vscroll", vVisible );
		SetClass( "has-hscroll", hVisible );
	}

	void UpdateContentSize()
	{
		var viewInset = ViewInset;
		var view = Box.Rect.Size;
		view.x = MathF.Max( 0, view.x - viewInset.x );
		view.y = MathF.Max( 0, view.y - viewInset.y );
		if ( view.x <= 0 || view.y <= 0 )
			return;

		var canvasRect = Canvas.Box.Rect;

		var minX = 0f;
		var minY = 0f;
		var maxX = 0f;
		var maxY = 0f;
		var any = false;

		foreach ( var child in Canvas.Children )
		{
			if ( child is null || !child.IsValid() )
				continue;

			var r = child.Box.Rect;
			any = true;
			minX = MathF.Min( minX, r.Left - canvasRect.Left );
			minY = MathF.Min( minY, r.Top - canvasRect.Top );
			maxX = MathF.Max( maxX, r.Right - canvasRect.Left );
			maxY = MathF.Max( maxY, r.Bottom - canvasRect.Top );
		}

		var measuredWidth = any ? (maxX - minX) : 0f;
		var measuredHeight = any ? (maxY - minY) : 0f;

		var contentWidth = EnableHorizontal ? MathF.Max( view.x, measuredWidth ) : view.x;
		var contentHeight = EnableVertical ? MathF.Max( view.y, measuredHeight ) : view.y;

		var size = new Vector2( contentWidth, contentHeight );
		ContentSize = size;
	}
}
