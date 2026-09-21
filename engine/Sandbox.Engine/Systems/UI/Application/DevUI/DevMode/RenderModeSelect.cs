namespace Sandbox.UI.Dev;

using System;
using System.Linq;

public sealed class RenderModeSelect : DevCommandButtonRow
{
	static readonly (SceneCameraDebugMode value, DisplayInfo info)[] DebugModes
		= DisplayInfo.ForEnumValues<SceneCameraDebugMode>();

	readonly DevCommandButtonOption[] Options;
	SceneCameraDebugMode _selectedMode;
	SceneCameraDebugMode _displayedMode;
	bool _displayInitialized;

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
		Options = DebugModes
			.Select( x => new DevCommandButtonOption( x.info.Name, ((int)x.value).ToString(), x.info.Icon ) )
			.ToArray();

		Index = 1;
		Columns = 2;
		UpdateDisplay();
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
		if ( _displayInitialized && _displayedMode == _selectedMode )
			return;

		SelectedOptionIndex = Math.Max( 0, Array.FindIndex( DebugModes, x => x.value == _selectedMode ) );

		Button = new DevCommandButtonSpec(
			CurrentTitle,
			DevCommandButtonKind.Dropdown,
			Options: Options,
			OnSelected: ( _, index ) => SetMode( DebugModes[index].value ) );

		OverrideLabel = CurrentTitle;
		StateHasChanged();

		SetClass( "active", _selectedMode != SceneCameraDebugMode.Normal );
		_displayedMode = _selectedMode;
		_displayInitialized = true;
	}

	public override void Tick()
	{
		base.Tick();

		var actual = Game.ActiveScene?.Camera?.DebugMode ?? SceneCameraDebugMode.Normal;
		if ( _selectedMode != actual )
			_selectedMode = actual;

		UpdateDisplay();
	}

}
