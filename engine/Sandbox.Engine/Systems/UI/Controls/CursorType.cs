namespace Sandbox.UI;

/// <summary>
/// Common cursor styles for UI panels. These map to CSS cursor keywords via <see cref="CursorTypeExtensions.ToCssCursor"/>.
/// </summary>
public enum CursorType
{
	Default,

	None,
	Pointer,
	Move,

	Text,
	Crosshair,
	Wait,
	Help,
	Progress,
	NotAllowed,

	Grab,
	Grabbing,

	ZoomIn,
	ZoomOut,

	ResizeN,
	ResizeS,
	ResizeE,
	ResizeW,
	ResizeNE,
	ResizeNW,
	ResizeSE,
	ResizeSW,
	ResizeNS,
	ResizeEW,
	ResizeNESW,
	ResizeNWSE,

	ColResize,
	RowResize,

	ContextMenu,
	Copy,
	Alias,
	NoDrop,
	AllScroll,
	Cell,
	VerticalText
}

public static class CursorTypeExtensions
{
	public static string ToCssCursor( this CursorType cursorType )
	{
		return cursorType switch
		{
			CursorType.Default => null,

			CursorType.None => "none",
			CursorType.Pointer => "pointer",
			CursorType.Move => "move",

			CursorType.Text => "text",
			CursorType.Crosshair => "crosshair",
			CursorType.Wait => "wait",
			CursorType.Help => "help",
			CursorType.Progress => "progress",
			CursorType.NotAllowed => "not-allowed",

			CursorType.Grab => "grab",
			CursorType.Grabbing => "grabbing",

			CursorType.ZoomIn => "zoom-in",
			CursorType.ZoomOut => "zoom-out",

			CursorType.ResizeN => "n-resize",
			CursorType.ResizeS => "s-resize",
			CursorType.ResizeE => "e-resize",
			CursorType.ResizeW => "w-resize",
			CursorType.ResizeNE => "ne-resize",
			CursorType.ResizeNW => "nw-resize",
			CursorType.ResizeSE => "se-resize",
			CursorType.ResizeSW => "sw-resize",
			CursorType.ResizeNS => "ns-resize",
			CursorType.ResizeEW => "ew-resize",
			CursorType.ResizeNESW => "nesw-resize",
			CursorType.ResizeNWSE => "nwse-resize",

			CursorType.ColResize => "col-resize",
			CursorType.RowResize => "row-resize",

			CursorType.ContextMenu => "context-menu",
			CursorType.Copy => "copy",
			CursorType.Alias => "alias",
			CursorType.NoDrop => "no-drop",
			CursorType.AllScroll => "all-scroll",
			CursorType.Cell => "cell",
			CursorType.VerticalText => "vertical-text",

			_ => null
		};
	}

	public static void SetCursor( this Panel panel, CursorType cursorType )
	{
		panel.Style.Cursor = cursorType.ToCssCursor();
		panel.Style.Dirty();
	}
}

