namespace Sandbox.UI.Dev;

using Sandbox;
using Sandbox.UI.Construct;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class StackTraceVirtualList : VirtualList
{
	const float EstimatedCharWidth = 6f;

	[Parameter] public float ContentWidthHint { get; set; }

	public StackTraceVirtualList()
	{
		AddClass( "stack-scroll" );
		AddClass( "dev-native-scroll" );
		AddClass( "compact" );

		CanDragScroll = false;
		AllowChildSelection = false;
		AcceptsFocus = false;
		ItemHeight = 12;
		OverscanItems = 16;
		OnCreateCell = CreateLineCell;
	}

	public void SetLines( IReadOnlyList<string> lines )
	{
		lines ??= [];

		if ( ItemCount == lines.Count )
		{
			var same = true;
			for ( var i = 0; i < lines.Count; i++ )
			{
				if ( !Equals( GetItem( i ), lines[i] ) )
				{
					same = false;
					break;
				}
			}

			if ( same )
				return;
		}

		Clear();
		AddItems( lines.Cast<object>() );

		var maxChars = lines.Count == 0 ? 0 : lines.Max( x => x?.Length ?? 0 );
		ContentWidthHint = (maxChars * EstimatedCharWidth) + 24f;
		NeedsRebuild = true;
	}

	protected override void PositionPanel( int index, Panel panel )
	{
		base.PositionPanel( index, panel );

		panel.Style.Width = MathF.Max( ContentWidthHint, Box.Rect.Width * ScaleFromScreen );
	}

	protected override float GetTotalWidth( int itemCount )
	{
		return MathF.Max( base.GetTotalWidth( itemCount ), ContentWidthHint );
	}

	object GetItem( int index )
	{
		if ( !HasData( index ) )
			return null;

		return _items[index];
	}

	static void CreateLineCell( Panel cell, object data )
	{
		cell.AllowChildSelection = false;

		var label = cell.Add.Label( data?.ToString() ?? string.Empty, "stackline" );
		label.Selectable = false;
	}
}
