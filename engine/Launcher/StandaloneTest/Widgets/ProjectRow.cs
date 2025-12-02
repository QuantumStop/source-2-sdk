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

	public override void OnClick() => Home.SelectProject( Project );

	protected override void OnPaint()
	{
		var horizontalInset = 2;
		var rect = new Rect( LocalRect.Left + horizontalInset, LocalRect.Top, LocalRect.Width - horizontalInset * 2, LocalRect.Height );

		// Background stripes
		var bgColor = (StripeIndex % 2 == 0) ? Color.Parse( "#2b2b2c" ).Value : Color.Parse( "#232324" ).Value;

		if ( Project.IsDefault ) bgColor = Color.Parse( "#3d6df0" ).Value.WithAlpha( 0.2f );
		if ( IsSelected ) bgColor = Color.Parse( "#3d6df0" ).Value.WithAlpha( 0.6f );

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
		PinButton.Visible = true;
		PinButton.Update();
		MoreButton.Visible = true;
		MoreButton.Update();
		DefaultButton.Visible = true;
		DefaultButton.Update();
		Update();
	}

	protected override void OnMouseLeave()
	{
		PinButton.Visible = Project.Pinned;
		PinButton.Update();
		MoreButton.Visible = false;
		MoreButton.Update();
		DefaultButton.Visible = Project.IsDefault;
		DefaultButton.Update();
		Update();
	}
}
