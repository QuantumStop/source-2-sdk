namespace Sandbox.UI.Dev;

using Sandbox.UI.Construct;

public sealed class ConvarToggle : Panel
{
	readonly Label TitleLabel;

	string _title;

	[Parameter]
	public string Title
	{
		get => _title;
		set
		{
			_title = value ?? "";
			if ( TitleLabel.IsValid() )
				TitleLabel.Text = _title;
		}
	}

	[Parameter] public string ConVar { get; set; }
	[Parameter] public string On { get; set; } = "1";
	[Parameter] public string Off { get; set; } = "0";

	public ConvarToggle()
	{
		TitleLabel = Add.Label( "" );
	}

	public void Toggle()
	{
		if ( ConVar == null )
			return;

		var val = DevConsoleAccess.GetValue( ConVar );
		var status = string.Equals( val, On, StringComparison.OrdinalIgnoreCase );
		DevConsoleAccess.SetValue( ConVar, status ? Off : On, allowProtected: true );
	}

	public override void Tick()
	{
		base.Tick();

		if ( ConVar == null )
			return;

		var val = DevConsoleAccess.GetValue( ConVar );
		if ( val == null )
			return;

		SetClass( "active", string.Equals( val, On, StringComparison.OrdinalIgnoreCase ) );
	}

	protected override void OnClick( MousePanelEvent e )
	{
		Toggle();
	}
}
