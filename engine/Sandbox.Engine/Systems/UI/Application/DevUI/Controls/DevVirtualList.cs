namespace Sandbox.UI.Dev;

using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Simple virtualized list whose scroll position is driven externally (eg by <see cref="DevScrollView"/>).
/// </summary>
public sealed class DevVirtualList : Panel
{
	public float ItemHeight { get; set; } = 18f;

	public Func<Panel> CreateCell { get; set; }
	public Action<Panel, object> BindCell { get; set; }

	/// <summary>
	/// Padding inside the virtualized content area. Because cells are absolutely positioned,
	/// this padding must be applied by the control (CSS padding on the content panel won't work).
	/// </summary>
	public float PaddingLeft { get; set; }
	public float PaddingRight { get; set; }
	public float PaddingTop { get; set; }
	public float PaddingBottom { get; set; }

	readonly Panel Content;

	readonly List<object> Items = new();
	readonly List<Panel> CellPool = new();

	Vector2 _virtualScrollOffset;

	public Vector2 VirtualScrollOffset
	{
		get => _virtualScrollOffset;
		set => _virtualScrollOffset = value;
	}

	public Vector2 ContentSize { get; private set; }

	// Optional width hint for horizontal scrolling (in pixels).
	public float ContentWidthHint { get; set; }

	public DevVirtualList()
	{
		AddClass( "devvirtuallist" );
		Style.Position = PositionMode.Relative;

		Content = Add.Panel( "content" );
		Content.Style.Position = PositionMode.Absolute;
	}

	public void Clear()
	{
		Items.Clear();
		ScrollOffset = Vector2.Zero;
		Invalidate();
	}

	public void AddItem( object item )
	{
		Items.Add( item );
		Invalidate();
	}

	public void SetItems( IEnumerable<object> items )
	{
		Items.Clear();
		Items.AddRange( items );
		Invalidate();
	}

	public new void TryScrollToBottom()
	{
		var maxY = MathF.Max( 0, (Items.Count * ItemHeight) - Box.Rect.Height );
		_virtualScrollOffset = new Vector2( _virtualScrollOffset.x, maxY );
	}

	public override void Tick()
	{
		base.Tick();

		var viewSize = Box.Rect.Size;
		var paddingX = MathF.Max( 0, PaddingLeft ) + MathF.Max( 0, PaddingRight );
		var paddingY = MathF.Max( 0, PaddingTop ) + MathF.Max( 0, PaddingBottom );

		// ContentWidthHint is the width of the inner content (excluding padding).
		var innerViewWidth = MathF.Max( 0, viewSize.x - paddingX );
		var innerContentWidth = MathF.Max( innerViewWidth, ContentWidthHint > 0 ? ContentWidthHint : innerViewWidth );

		var innerContentHeight = Items.Count * ItemHeight;

		var contentWidth = MathF.Max( viewSize.x, innerContentWidth + paddingX );
		var contentHeight = MathF.Max( viewSize.y, innerContentHeight + paddingY );
		ContentSize = new Vector2( contentWidth, contentHeight );

		// Clamp scroll
		var maxX = MathF.Max( 0, contentWidth - viewSize.x );
		var maxY = MathF.Max( 0, contentHeight - viewSize.y );
		_virtualScrollOffset = new Vector2( _virtualScrollOffset.x.Clamp( 0, maxX ), _virtualScrollOffset.y.Clamp( 0, maxY ) );

		// Position content
		Content.Style.Left = MathF.Max( 0, PaddingLeft ) - _virtualScrollOffset.x;
		Content.Style.Top = MathF.Max( 0, PaddingTop ) - _virtualScrollOffset.y;
		Content.Style.Width = contentWidth;
		Content.Style.Height = contentHeight;
		Content.Style.Dirty();

		UpdateVisible( innerContentWidth );
	}

	void UpdateVisible( float innerContentWidth )
	{
		if ( ItemHeight <= 0 ) return;

		var viewTop = _virtualScrollOffset.y;
		var viewBottom = viewTop + Box.Rect.Height;

		var firstIndex = Math.Max( 0, (int)MathF.Floor( viewTop / ItemHeight ) - 2 );
		var lastIndex = Math.Min( Items.Count - 1, (int)MathF.Ceiling( viewBottom / ItemHeight ) + 2 );

		var needed = Items.Count == 0 ? 0 : Math.Max( 0, lastIndex - firstIndex + 1 );
		EnsurePool( needed );

		for ( int i = 0; i < CellPool.Count; i++ )
		{
			var cell = CellPool[i];
			if ( i >= needed )
			{
				cell.Style.Display = DisplayMode.None;
				continue;
			}

			var itemIndex = firstIndex + i;
			var data = Items[itemIndex];

			cell.Style.Display = DisplayMode.Flex;
			cell.Style.Position = PositionMode.Absolute;
			cell.Style.Top = MathF.Max( 0, PaddingTop ) + (itemIndex * ItemHeight);
			cell.Style.Left = MathF.Max( 0, PaddingLeft );
			cell.Style.Height = ItemHeight;
			cell.Style.Width = MathF.Max( 0, innerContentWidth );
			cell.Style.Dirty();

			BindCell?.Invoke( cell, data );
		}
	}

	void EnsurePool( int count )
	{
		while ( CellPool.Count < count )
		{
			var cell = (CreateCell?.Invoke()) ?? new Panel();
			cell.Parent = Content;
			CellPool.Add( cell );
		}
	}

	void Invalidate() { }
}
