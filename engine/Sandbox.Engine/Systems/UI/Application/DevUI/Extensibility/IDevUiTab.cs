namespace Sandbox.UI.Dev;

public interface IDevUiTab
{
	/// <summary>Stable identifier used for cookies and internal routing.</summary>
	/// <remarks>
	/// Optional. If empty or whitespace, DevUI will fall back to the implementing panel's full type name.
	/// Provide an explicit id if you need it to stay stable across refactors/renames.
	/// </remarks>
	string DevTabId => string.Empty;

	/// <summary>Text shown on the DevUI tab button.</summary>
	string DevTabTitle { get; }

	/// <summary>Sort order (lower comes first). Built-in tabs use 0-20.</summary>
	int DevTabOrder { get; }
}
