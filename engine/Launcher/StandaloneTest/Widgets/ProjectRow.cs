using System.IO;
using System.Text;
using System.Diagnostics;

namespace Editor;

public partial class ProjectRow : ItemRow
{
	public delegate void OpenProjectDelegate( string args );

	public Action OnPinStateChanged { get; set; }
	public Action OnProjectRemove { get; set; }
	public OpenProjectDelegate OnProjectOpen { get; set; }

	public Project Project { get; }
	protected Package Package { get; set; }

	private ControlModeSettings ControlModes => Project.Config.GetMetaOrDefault( "ControlModes", new ControlModeSettings() );

	private IconButton PinButton { get; set; }
	private IconButton MoreButton { get; set; }
	private IconButton DefaultButton { get; set; }

	public int StripeIndex { get; set; } = 0;

	private HomeWidget Home => Parent as HomeWidget ?? this.FindParent<HomeWidget>();

	public bool IsSelected => Home.SelectedProject == Project;
	public bool IsLaunching => Home.IsProjectLaunching( Project );

	public ProjectRow( Project project, Widget parent ) : base( parent )
	{
		Project = project;
		Title = project.Config.Title;

		_ = UpdatePackageAsync();
		Init();
	}

	protected override List<InfoItem> GetInfo()
	{
		var info = new List<InfoItem>();

		// Last opened date
		string lastOpenedText = Project.LastOpened.ToLocalTime().ToString( "g" );
		if ( Project.Config.Org == "local" )
		{
			info.Add( ("schedule", lastOpenedText) );
		}
		else
		{
			info.Add( ("group", Project.Package?.Org.Title ?? "Unknown Org") );
			info.Add( ("schedule", lastOpenedText) );
		}

		// VR info
		if ( ControlModes.VR )
		{
			info.Add( ("panorama_photosphere", ControlModes.IsVROnly ? "VR Only" : "VR Compatible") );
		}

		return info;
	}

	protected override void CreateUI()
	{
		Cursor = CursorShape.Finger;

		// More button
		MoreButton = AddButton( "more_vert", "More..." );
		MoreButton.OnClick = () =>
		{
			var menu = OpenContextMenu();
			menu.OpenAt( MoreButton.ScreenPosition );
		};
		MoreButton.OnPaintOverride = () =>
		{
			Paint.Antialiasing = true;
			Paint.ClearPen();
			Paint.SetPen( Paint.HasMouseOver ? Theme.TextHighlight : Theme.TextLight );
			Paint.DrawIcon( MoreButton.LocalRect, "more_vert", 16.0f );
			return true;
		};
		MoreButton.Visible = false;

		// Default button
		DefaultButton = AddButton( "star", Project.IsDefault ? "Default" : "Set as Default", () =>
		{
			DefaultButton.Visible = !Project.IsDefault;
			Home.SetDefaultProject( Project );
			DefaultButton.ToolTip = Project.IsDefault ? "Default" : "Set as Default";
		} );
		DefaultButton.OnPaintOverride = () =>
		{
			Paint.Antialiasing = true;
			Paint.ClearPen();
			Paint.SetPen( Project.IsDefault ? Theme.Text : Paint.HasMouseOver ? Theme.TextHighlight : Theme.TextLight );
			Paint.DrawIcon( PinButton.LocalRect, "star", 16.0f );
			return true;
		};
		DefaultButton.Visible = Project.IsDefault;

		// Pin button
		PinButton = AddButton( "push_pin", Project.Pinned ? "Unpin this project" : "Pin this project", () =>
		{
			Project.Pinned = !Project.Pinned;
			PinButton.ToolTip = Project.Pinned ? "Unpin this project" : "Pin this project";
			OnPinStateChanged?.Invoke();
		} );
		PinButton.OnPaintOverride = () =>
		{
			Paint.Antialiasing = true;
			Paint.ClearPen();
			Paint.SetPen( Project.Pinned ? Theme.Text : Paint.HasMouseOver ? Theme.TextHighlight : Theme.TextLight );
			Paint.DrawIcon( PinButton.LocalRect, "push_pin", 16.0f );
			return true;
		};
		PinButton.Visible = Project.Pinned;

		_ = UpdatePackageAsync();
	}

	protected async Task UpdatePackageAsync()
	{
		if ( Project.Config.Org == "local" )
			return;

		Package = await Package.Fetch( Project.Config.FullIdent, true );

		if ( !this.IsValid() )
			return;

		Update();
	}

	public override void OnClick()
	{
		OpenProject();
	}

	private string GetLaunchArgs( LaunchFlags launchFlags )
	{
		var args = new StringBuilder();
		if ( launchFlags.Contains( LaunchFlags.VR ) || launchFlags.Contains( LaunchFlags.VRDebug ) ) args.Append( " -vr" );
		if ( launchFlags.Contains( LaunchFlags.VulkanValidation ) ) args.Append( " -vulkan_enable_validation -vulkan_validation_error_assert" );
		if ( launchFlags.Contains( LaunchFlags.VRDebug ) ) args.Append( " -vrdebug" );

		return args.ToString();
	}

	private void OpenProject( LaunchFlags launchFlags = LaunchFlags.None )
	{
		if ( (DateTime.Now - Project.LastOpened).TotalSeconds < 2.0f )
			return;

		var args = GetLaunchArgs( launchFlags );
		OnProjectOpen?.Invoke( args );

		Update();
	}

	protected override void OnPaint()
	{
		UpdateButtonVisibilityForState();

		var horizontalInset = 2;
		var rect = new Rect( LocalRect.Left + horizontalInset, LocalRect.Top, LocalRect.Width - horizontalInset * 2, LocalRect.Height );
		var pulse = 0.5f + (MathF.Sin( Home.LaunchAnimationPhase ) * 0.5f);

		// Background stripes
		var bgColor = (StripeIndex % 2 == 0) ? Color.Parse( "#2b2b2c" ).Value : Color.Parse( "#232324" ).Value;

		if ( Project.IsDefault ) bgColor = Color.Parse( "#3d6df0" ).Value.WithAlpha( 0.2f );
		if ( IsSelected ) bgColor = Color.Parse( "#3d6df0" ).Value.WithAlpha( 0.6f );
		if ( IsLaunching )
		{
			var launchColor = Color.Parse( "#5f8fff" ).Value.WithAlpha( 0.38f + pulse * 0.12f );
			bgColor = launchColor;
		}

		Paint.SetBrush( bgColor );
		Paint.SetPen( bgColor );
		Paint.DrawRect( rect );

		// Hover highlight
		if ( Paint.HasMouseOver )
		{
			Paint.SetBrush( Color.White.WithAlpha( 0.05f ) );
			Paint.SetPen( bgColor );
			Paint.DrawRect( rect );
		}

		if ( IsLaunching )
		{
			// Heartbeat border pulse.
			var beatPhase = Home.LaunchAnimationPhase * 0.5f;
			var beatA = MathF.Max( 0.0f, MathF.Sin( beatPhase ) );
			var beatB = MathF.Max( 0.0f, MathF.Sin( beatPhase - 1.1f ) ) * 0.75f;
			var beat = MathF.Max( beatA, beatB );

			var edge = Color.Parse( "#d7e8ff" ).Value.WithAlpha( 0.08f + beat * 0.22f );
			Paint.Antialiasing = true;
			Paint.ClearBrush();
			Paint.SetPen( edge, 1.0f );
			Paint.DrawRect( rect.Shrink( 0.5f ) );

			Paint.SetPen( edge.WithAlpha( 0.08f + beat * 0.22f * 0.55f ), 1.0f );
			Paint.DrawRect( rect.Shrink( 1.5f ) );
		}

		base.OnPaint();
	}

	protected override void OnPaintIcon( Rect iconRect )
	{
		Paint.ClearPen();
		Paint.ClearBrush();

		var iconPath = Project?.Config?.GetMetaOrDefault<string>( "ProjectIcon", null );

		if ( string.IsNullOrEmpty( iconPath ) )
		{
			DrawFallbackIcon( iconRect );
			return;
		}

		var projectDir = Project?.Config?.Directory?.FullName;
		var fullIconPath = Path.Combine( projectDir ?? "", iconPath.Replace( '/', Path.DirectorySeparatorChar ) );

		if ( File.Exists( fullIconPath ) )
		{
			var pixmap = Paint.LoadImage( fullIconPath, (int)iconRect.Width, (int)iconRect.Height );
			Paint.Draw( iconRect, pixmap );
			return;
		}

		DrawFallbackIcon( iconRect );
	}

	private void DrawFallbackIcon( Rect iconRect )
	{
		Pixmap pixmap = null;
		pixmap = Paint.LoadImage( "common/logo", (int)iconRect.Width, (int)iconRect.Height );
		Paint.Draw( iconRect, pixmap );
	}

	protected override void OnMouseEnter()
	{
		UpdateButtonVisibilityForState();
		PinButton.Update();
		MoreButton.Update();
		DefaultButton.Update();
		Update();
	}

	protected override void OnMouseLeave()
	{
		UpdateButtonVisibilityForState();
		PinButton.Update();
		MoreButton.Update();
		DefaultButton.Update();
		Update();
	}

	private void UpdateButtonVisibilityForState()
	{
		if ( !Buttons.IsValid() )
			return;

		if ( IsLaunching )
		{
			Buttons.Visible = false;
			return;
		}

		Buttons.Visible = true;
		PinButton.Visible = IsUnderMouse || Project.Pinned;
		MoreButton.Visible = IsUnderMouse;
		DefaultButton.Visible = IsUnderMouse || Project.IsDefault;
	}
}
