namespace Sandbox.UI.Dev;

public enum DevCommandButtonKind
{
	Press,
	Toggle,
	Cycle,
	Dropdown
}

public enum DevCommandDropdownHandleStyle
{
	Default,
	Compact
}

public sealed record DevCommandButtonSpec(
	string Title,
	DevCommandButtonKind Kind = DevCommandButtonKind.Press,
	string Command = "",
	string Value = "",
	string OffValue = "",
	DevCommandButtonOption[] Options = null,
	string Icon = "",
	DevCommandDropdownHandleStyle DropdownHandleStyle = DevCommandDropdownHandleStyle.Default );

public sealed record DevCommandButtonOption( string Title, string Value, string Icon = "" );
