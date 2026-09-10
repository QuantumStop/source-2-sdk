namespace Sandbox.UI.Dev;

using Sandbox.UI.Construct;

/// <summary>
/// Wraps a custom (project) DevUI tab inside a standard scroll + padded canvas.
/// This keeps custom tabs consistent with built-in tabs (and avoids everyone re-implementing scroll/padding).
/// </summary>
public sealed class DevCustomTabHost : Panel
{
	public DevScrollPanel Scroll { get; }
	public Panel Canvas { get; }

	public DevCustomTabHost()
	{
		AddClass( "devtab" );
		AddClass( "customtab" );

		Scroll = AddChild<DevScrollPanel>();
		Scroll.AddClass( "customtab-scroll" );

		Canvas = Scroll.Canvas;
		Canvas.AddClass( "customtab-canvas" );
	}

	public void SetContent( Panel content )
	{
		if ( content is null )
			return;

		// Content panels should not be marked as devtab (that class hides them by default).
		content.RemoveClass( "devtab" );
		content.Parent = Canvas;
		content.AddClass( "customtab-content" );

		// Sensible default: let the content flow and fill width.
		content.Style.Width = Length.Percent( 100 );
		content.Style.Dirty();
	}
}

