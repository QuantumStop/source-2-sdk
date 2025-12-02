using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using static Sandbox.Utility.DataProgress;

namespace Editor;

public class HomeWidget : Widget
{
	enum SortMethod { Date, Name, Org }

	private SortMethod _sort;
	private SortMethod Sort
	{
		get => _sort;
		set
		{
			if ( SortButton.IsValid() )
				SortButton.Icon = value switch
				{
					SortMethod.Date => "calendar_month",
					SortMethod.Name => "sort_by_alpha",
					SortMethod.Org => "groups",
					_ => ""
				};

			if ( _sort != value )
			{
				_sort = value;
				RefreshLocalProjects();
			}
		}
	}

	private string _filter;
	private string Filter
	{
		get => _filter;
		set
		{
			_filter = value;
			RefreshLocalProjects();
		}
	}

	private Layout LocalProjectLayout { get; set; }
	private ProjectList ProjectList { get; }
	public Project SelectedProject;

	private IconButton SortButton;
	private Checkbox CloseOnLaunch;

	private Pixmap BannerImage;

	private readonly string BannerFallbackPath = "common/launcher_banner.png";
	private const int BannerHeight = 115;
	private const int BannerWidth = 555;

	public HomeWidget( Widget parent = null ) : base( parent )
	{
		AcceptDrops = true;

		Layout = Layout.Column();
		Layout.Spacing = 4;

		ProjectList = new ProjectList();
		Layout.AddSpacingCell( 8.0f );

		// Banner
		var bannerContainer = Layout.Add( new Widget( this ), 0 );
		bannerContainer.Layout = Layout.Row();
		bannerContainer.Layout.Margin = new Sandbox.UI.Margin( 16, 2, 16, 2 );

		var bannerHeader = new Widget( bannerContainer )
		{
			FixedWidth = BannerWidth,
			FixedHeight = BannerHeight,
			OnPaintOverride = () =>
			{
				Paint.ClearPen();
				var rect = new Rect( 0, 0, BannerWidth, BannerHeight );

				if ( BannerImage != null )
					Paint.Draw( rect, BannerImage );
				else
				{
					Paint.SetBrush( Color.Gray.Darken( 0.2f ) );
					Paint.DrawRect( rect );
					Paint.SetDefaultFont( 14f );
					Paint.SetPen( Color.White );
					Paint.DrawText( rect, "No Banner" );
				}

				return true;
			}
		};
		bannerContainer.Layout.Add( bannerHeader, 0 );

		// Top menu row
		var menuRow = Layout.AddRow();
		menuRow.Spacing = 8;
		menuRow.Margin = new Sandbox.UI.Margin( 16, 6, 16, 0 );

		SortButton = menuRow.Add( new IconButton( "sort_by_alpha" ) { OnClick = OpenPopup, ToolTip = "Sort by" } );
		Sort = _sort;

		var search = menuRow.Add( new LineEdit() { PlaceholderText = "⌕  Search" }, 3 );
		search.SetStyles( "border-radius: 0px; background-color:#262627;" );
		search.TextChanged += _ => { Filter = search.Value; search.Focus(); };
		search.Blur();

		menuRow.AddStretchCell();
		menuRow.Add( new IconButton( "create_new_folder" ) { OnClick = AddProjectFromFile, ToolTip = "Add project from folder" } );
		menuRow.Add( new Button( "Create New Project..." ) { FixedHeight = Theme.RowHeight, Clicked = CreateProject, ToolTip = "Create a new project" } );

		Layout.AddSpacingCell( 8.0f );

		// Scroll area
		var scroller = Layout.Add( new ScrollArea( this ), 1 );
		scroller.Canvas = new Widget( scroller )
		{
			Layout = Layout.Column(),
			VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand,
			OnPaintOverride = () =>
			{
				Paint.ClearPen();
				var padding = new Sandbox.UI.Margin( 16, 0, 16, 2 );
				var paddedRect = new Rect(
					scroller.Canvas.LocalRect.Left + padding.Left,
					scroller.Canvas.LocalRect.Top + padding.Top,
					scroller.Canvas.LocalRect.Width - padding.Left - padding.Right,
					scroller.Canvas.LocalRect.Height - padding.Top - padding.Bottom
				);

				Paint.SetBrush( Color.Parse( "#262627" ).Value );
				Paint.SetPen( Color.Parse( "#494949" ).Value );
				Paint.DrawRect( paddedRect );

				scroller.Canvas.Layout.AddStretchCell();
				return true;
			}
		};
		LocalProjectLayout = scroller.Canvas.Layout.Add( Layout.Column(), 0 );

		RefreshLocalProjects();
		TryRestoreDefaultProject();
		UpdateBanner();

		Layout.AddSpacingCell( 8.0f );

		// Bottom bar
		var bottomRow = Layout.AddRow();
		bottomRow.Margin = new Sandbox.UI.Margin( 16, 0, 16, 16 );

		CloseOnLaunch = bottomRow.Add( new Checkbox( "Close On Launch" ) );
		CloseOnLaunch.Value = LauncherPreferences.CloseOnLaunch;
		CloseOnLaunch.Toggled += () =>
		{
			LauncherPreferences.CloseOnLaunch = CloseOnLaunch.Value;
			LauncherPreferences.Save();
		};

		bottomRow.AddStretchCell();
		bottomRow.Add( new Button.Primary( "Launch Project" )
		{
			FixedHeight = 40,
			FixedWidth = 200,
			Clicked = () => { if ( SelectedProject != null ) OpenProject( SelectedProject ); }
		} );
	}

	private void RefreshLocalProjects()
	{
		ProjectList.Refresh();
		var all = ProjectList.GetAll().Where( x => !x.IsBuiltIn ).ToList();
		UpdateProjectList( all );
	}

	private void TryRestoreDefaultProject()
	{
		var defaultIdent = LauncherPreferences.DefaultProjectIdent;
		var defaultProject = ProjectList.GetAll().FirstOrDefault( p => p.Config.FullIdent == defaultIdent );

		// If nothing saved, fallback to first non-built-in
		if ( defaultProject == null )
			defaultProject = ProjectList.GetAll().FirstOrDefault( p => !p.IsBuiltIn );

		SelectedProject ??= defaultProject;

		// Update each project’s IsDefault property
		foreach ( var p in ProjectList.GetAll() )
			p.IsDefault = (p == defaultProject);
	}

	private void UpdateProjectList( List<Project> projects )
	{
		using var suspend = SuspendUpdates.For( this );
		LocalProjectLayout.Clear( true );
		LocalProjectLayout.Margin = new Sandbox.UI.Margin( 16, 0, 16, 16 );

		// Sorting
		projects = Sort switch
		{
			SortMethod.Name => projects.OrderBy( x => x.Config.Title ).ToList(),
			SortMethod.Org => projects.OrderBy( x => x.Package.Org.Title ).ToList(),
			_ => projects.OrderByDescending( x => x.LastOpened ).ToList()
		};

		// Filtering
		if ( !string.IsNullOrEmpty( Filter ) )
		{
			var f = Filter.ToLower();
			projects = projects.Where( x => x.Config.Title.ToLower().Contains( f ) || x.Package.Title.ToLower().Contains( f ) ).ToList();
		}

		// Pinned/unpinned
		var pinned = projects.Where( x => x.Pinned ).ToList();
		var unpinned = projects.Where( x => !x.Pinned ).ToList();
		var previouslySelected = SelectedProject;

		if ( pinned.Any() )
		{
			var header = new Label.Subtitle( "Pinned Projects" ) { ContentMargins = 8 };
			header.SetStyles( "font-size:13px" );
			LocalProjectLayout.Add( header );
			CreateItemRows( pinned, LocalProjectLayout.AddColumn() );
		}

		if ( unpinned.Any() )
		{
			var header = new Label.Subtitle( "Projects" ) { ContentMargins = 8 };
			header.SetStyles( "font-size:13px" );
			LocalProjectLayout.Add( header );
			CreateItemRows( unpinned, LocalProjectLayout.AddColumn() );
		}

		if ( previouslySelected != null )
			SelectProject( previouslySelected );

		// Refresh visuals
		foreach ( var row in this.FindAllChildren<ProjectRow>() )
			row.Update();
	}

	private void OpenPopup()
	{
		var popup = new ContextMenu( this )
		{
			Layout = Layout.Column(),
			Size = new Vector2( 160, 30 )
		};
		popup.Layout.Margin = 8;

		popup.AddOption( "Most Recent", "calendar_month", () => Sort = SortMethod.Date );
		popup.AddOption( "Name", "sort_by_alpha", () => Sort = SortMethod.Name );
		popup.AddOption( "Organization", "groups", () => Sort = SortMethod.Org );

		popup.Position = ScreenRect.TopLeft + new Vector2( 16, 48 );
		popup.Show();
	}

	internal void SelectProject( Project project )
	{
		// Only update if the selection changes
		if ( SelectedProject == project )
			return;

		SelectedProject = project;

		// Update banner
		UpdateBanner();

		// Maintain default project as-is unless explicitly set
		foreach ( var row in this.FindAllChildren<ProjectRow>() )
			row.Update();
	}

	internal void SetDefaultProject( Project project )
	{
		foreach ( var p in ProjectList.GetAll() )
			p.IsDefault = false;

		project.IsDefault = true;

		// Persist it
		LauncherPreferences.DefaultProjectIdent = project.Config.FullIdent;

		foreach ( var row in this.FindAllChildren<ProjectRow>() )
			row.Update();
	}

	public void AddProjectFromFile()
	{
		var fd = new FileDialog( null );
		if ( !Directory.Exists( fd.Directory ) ) Directory.CreateDirectory( fd.Directory );

		fd.Title = "Find project file";
		fd.SetNameFilter( "*.sbproj" );

		if ( fd.Execute() )
		{
			try
			{
				ProjectList.TryAddFromFile( fd.SelectedFile );
				ProjectList.SaveList();
				RefreshLocalProjects();
			}
			catch { }
		}
	}

	public void CreateProject()
	{
		var creatorWindow = new ProjectCreator();
		creatorWindow.OnProjectCreated = config =>
		{
			var project = ProjectList.TryAddFromFile( config );
			project.LastOpened = DateTime.Now;
			ProjectList.SaveList();
			RefreshLocalProjects();

			if ( project != null )
				OpenProject( project );
		};
		creatorWindow.Show();
	}

	public void OpenProject( Project project, string args = null )
	{
		Process.Start( new ProcessStartInfo( "sbox-dev.exe",
			$"{Environment.CommandLine} -project \"{project.ConfigFilePath}\" {args ?? ""}" )
		{
			UseShellExecute = true,
			CreateNoWindow = true,
			WorkingDirectory = Environment.CurrentDirectory
		} );

		if ( CloseOnLaunch.Value ) Parent.Destroy();
	}

	private void UpdateBanner()
	{
		string bannerPath = SelectedProject?.Config.ProjectBanner;

		// Always start with fallback
		Pixmap newBanner = Paint.LoadImage( BannerFallbackPath, BannerWidth, BannerHeight );

		if ( !string.IsNullOrEmpty( bannerPath ) )
		{
			var projectDir = SelectedProject.Config.Directory?.FullName;
			var fullBannerPath = Path.Combine( projectDir ?? "", bannerPath.Replace( '/', Path.DirectorySeparatorChar ) );

			if ( File.Exists( fullBannerPath ) )
			{
				var pix = Paint.LoadImage( fullBannerPath, BannerWidth, BannerHeight );
				if ( pix != null )
					newBanner = pix;
			}
		}

		// Only update if it changed
		if ( BannerImage != newBanner )
			BannerImage = newBanner;

		this.Update();
	}

	private void CreateItemRows( IEnumerable<Project> list, Layout layout )
	{
		var grid = new GridLayout { Spacing = 1, HorizontalSpacing = 16 };
		layout.Add( grid );

		int i = 0;
		foreach ( var project in list )
		{
			var row = new ProjectRow( project, this ) { StripeIndex = i };

			row.Click = () => SelectProject( project );
			row.OnPinStateChanged = () => { ProjectList.SaveList(); RefreshLocalProjects(); };
			row.OnProjectRemove = () => { ProjectList.Remove( project ); ProjectList.SaveList(); RefreshLocalProjects(); };
			row.OnProjectOpen = args => OpenProject( project, args );

			grid.AddCell( 0, i, row );
			row.Update();
			i++;
		}
	}

	protected override void OnPaint()
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.WindowBackground );
		Paint.DrawRect( LocalRect );
	}
}
