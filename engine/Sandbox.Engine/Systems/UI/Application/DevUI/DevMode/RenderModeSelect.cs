namespace Sandbox.UI.Dev;

using Sandbox.UI.Construct;

public sealed class RenderModeSelect : Panel
{
	static readonly (SceneCameraDebugMode value, DisplayInfo info)[] DebugModes
		= DisplayInfo.ForEnumValues<SceneCameraDebugMode>();

	readonly Label TitleLabel;
	readonly IconPanel DropIcon;
	SceneCameraDebugMode _selectedMode;

	string CurrentTitle => _selectedMode == SceneCameraDebugMode.Normal
		? "Image Lit"
		: (GetDisplayInfo().Name ?? _selectedMode.ToString());

	DisplayInfo GetDisplayInfo()
	{
		foreach ( var (value, info) in DebugModes )
			if ( value == _selectedMode )
				return info;

		return default;
	}

	public RenderModeSelect()
	{
		var titleBar = Add.Panel( "titlebar" );
		TitleLabel = titleBar.Add.Label( "", "title" );

		var drop = Add.Panel( "drop" );
		DropIcon = drop.Add.Icon( "arrow_drop_down" );
	}

	void SetMode( SceneCameraDebugMode mode )
	{
		_selectedMode = mode;
		DevConsoleAccess.SetValue( "mat_toolsvis", ((int)mode).ToString(), allowProtected: true );

		if ( Game.ActiveScene?.Camera is CameraComponent cam )
			cam.DebugMode = mode;

		UpdateDisplay();
	}

	void UpdateDisplay()
	{
		TitleLabel.Text = CurrentTitle;
		DropIcon.Text = "arrow_drop_down";
		SetClass( "active", _selectedMode != SceneCameraDebugMode.Normal );
	}

	public override void Tick()
	{
		base.Tick();

		var actual = Game.ActiveScene?.Camera?.DebugMode ?? SceneCameraDebugMode.Normal;
		if ( _selectedMode != actual )
			_selectedMode = actual;

		UpdateDisplay();
	}

	protected override void OnClick( MousePanelEvent e )
	{
		var popup = new Popup( this, Popup.PositionMode.BelowLeft, 4f );
		popup.CloseWhenParentIsHidden = true;

		foreach ( var (mode, info) in DebugModes )
		{
			var captured = mode;
			var option = popup.AddOption( info.Name, info.Icon, () => SetMode( captured ) );

			if ( mode == _selectedMode )
				option.AddClass( "active" );
		}
	}
}
