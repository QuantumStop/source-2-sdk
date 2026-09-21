using Microsoft.AspNetCore.Components;
using Sandbox.UI.Layout;

namespace Sandbox.UI;

/// <summary>
/// A virtualized, scrollable list panel that only creates item panels when visible.
/// </summary>
public class VirtualList : BaseVirtualPanel
{
	/// <summary>
	/// Vertical list layout used to position/measure items. (Swappable later if needed.)
	/// </summary>
	internal VerticalListLayout Layout { get; } = new();

	/// <summary>
	/// Fixed height of each item.
	/// </summary>
	[Parameter]
	public float ItemHeight
	{
		get => Layout.ItemHeight;
		set => Layout.ItemHeight = value;
	}

	protected override void UpdateLayoutSpacing( Vector2 spacing )
	{
		Layout.Spacing = spacing;
	}

	protected override bool UpdateLayout()
	{
		return Layout.Update( Box, ScaleFromScreen, ScrollOffset.y * ScaleFromScreen );
	}

	protected override void GetVisibleRange( out int first, out int pastEnd )
	{
		Layout.GetVisibleRange( out first, out pastEnd );
	}

	protected override void PositionPanel( int index, Panel panel )
	{
		Layout.Position( index, panel );
	}

	protected override float GetTotalHeight( int itemCount )
	{
		return Layout.GetHeight( itemCount );
	}
}
