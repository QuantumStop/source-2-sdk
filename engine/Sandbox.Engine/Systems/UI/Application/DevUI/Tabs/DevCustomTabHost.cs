namespace Sandbox.UI.Dev;

/// <summary>
/// Hosts a custom (project) DevUI tab as a normal tab root.
/// </summary>
public sealed class DevCustomTabHost : Panel
{
	public DevCustomTabHost()
	{
		CanDragScroll = false;  // It's really dumb that this is true by default

		AddClass( "devtab" );
		AddClass( "customtab" );
	}

	public void SetContent( Panel content )
	{
		if ( content is null )
			return;

		// Content panels flow inside this tab root. They shouldn't be marked as devtab,
		// because that class gives them tab visibility/positioning rules of their own.
		content.RemoveClass( "devtab" );
		content.Parent = this;
		content.AddClass( "customtab-content" );

		content.Style.Width = Length.Percent( 100 );
		content.Style.Dirty();
	}
}

