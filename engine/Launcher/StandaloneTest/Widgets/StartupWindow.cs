using Editor;

namespace Sandbox;

public partial class StartupWindow : BaseWindow
{
	private Vector2 WindowSize => new Vector2( 560, 650 );

	private Layout Body { get; set; }

	public StartupWindow()
	{
		WindowFlags = WindowFlags.MSWindowsFixedSizeDialogHint;

		Size = WindowSize;
		MinimumSize = WindowSize;
		MaximumSize = WindowSize;
		HasMaximizeButton = false;
		Visible = false;

		WindowTitle = "Project Selection";

		SetWindowIcon( Pixmap.FromFile( "common/logo_rounded.png" ) );

		CreateUI();
	}

	public override void Show()
	{
		base.Show();

		RestoreGeometry( LauncherPreferences.Cookie.Get( "startscreen.geometry", "" ) );
	}

	protected override bool OnClose()
	{
		EditorCookie = null;

		LauncherPreferences.Cookie.Set( "startscreen.geometry", SaveGeometry() );

		return base.OnClose();
	}

	private void CreateUI()
	{
		Layout = Layout.Column();

		Body = Layout.AddColumn( 1 );
		Body.Add( new HomeWidget( this ), 1 );
	}
}
