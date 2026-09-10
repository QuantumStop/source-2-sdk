namespace Sandbox.UI.Dev;

using Sandbox;

public sealed class DevLogTab : Panel
{
	internal Console Console;

	public DevLogTab()
	{
		AddClass( "devtab" );
		AddClass( "logtab" );

		Console = AddChild<Console>();
		Console.AddClass( "console-host" );
	}

	public void FocusConsole() => Console?.Input?.Focus();
	public void BlurConsole() => Console?.Input?.Blur();
}
