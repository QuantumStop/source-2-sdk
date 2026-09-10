namespace Sandbox.UI.Dev.Stats;

using Sandbox.UI.Construct;

public sealed class StatValue : Panel
{
	readonly Label ValueLabel;
	readonly Label TitleLabel;

	string _value;
	string _title;

	[Parameter]
	public string Value
	{
		get => _value;
		set
		{
			_value = value ?? "";
			if ( ValueLabel.IsValid() )
				ValueLabel.Text = _value;
		}
	}

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

	public StatValue()
	{
		ValueLabel = Add.Label( "", "value" );
		ValueLabel.ElementName = "value";

		TitleLabel = Add.Label( "", "name" );
		TitleLabel.ElementName = "name";
	}
}
