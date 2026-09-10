namespace Sandbox.UI.Dev;

using Sandbox.UI;
using Sandbox.UI.Construct;

/// <summary>
/// Reusable DevUI "group" block: icon + title header with a flexible body container.
/// </summary>
public sealed class DevGroup : Panel
{
	public Panel Header { get; }
	public IconPanel Icon { get; }
	public Label TitleLabel { get; }
	public Panel Body { get; }
	readonly Panel _headerRight;
	readonly Panel _footer;

	/// <summary>
	/// Optional container on the right side of the header row.
	/// Accessing this property makes it visible.
	/// </summary>
	public Panel HeaderRight
	{
		get
		{
			_headerRight.Style.Display = DisplayMode.Flex;
			_headerRight.Style.Dirty();
			return _headerRight;
		}
	}

	/// <summary>
	/// Optional footer container below the body.
	/// Accessing this property makes it visible.
	/// </summary>
	public Panel Footer
	{
		get
		{
			_footer.Style.Display = DisplayMode.Flex;
			_footer.Style.Dirty();
			return _footer;
		}
	}

	public string Title
	{
		get => TitleLabel.Text;
		set => TitleLabel.Text = value ?? string.Empty;
	}

	public string IconName
	{
		get => Icon.Text;
		set
		{
			Icon.Text = value ?? string.Empty;
			Icon.Style.Display = string.IsNullOrWhiteSpace( Icon.Text ) ? DisplayMode.None : DisplayMode.Flex;
			Icon.Style.Dirty();
		}
	}

	public DevGroup()
	{
		AddClass( "devgroup" );

		Header = Add.Panel( "devgroup-header" );

		Icon = Header.Add.Icon( null, "devgroup-icon" );
		Icon.Style.Display = DisplayMode.None;

		var titleBar = Header.Add.Panel( "devgroup-titlebar" );
		TitleLabel = titleBar.Add.Label( "", "devgroup-title" );

		_headerRight = Header.Add.Panel( "devgroup-header-right" );
		_headerRight.Style.Display = DisplayMode.None;

		Body = Add.Panel( "devgroup-body" );

		_footer = Add.Panel( "devgroup-footer" );
		_footer.Style.Display = DisplayMode.None;
	}
}
