using NativeEngine;
using Sandbox.DataModel;
using Sandbox.Diagnostics;
using Sandbox.Resources;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Editor;

public class ProjectCreator : Dialog
{
	private static readonly Sandbox.Diagnostics.Logger Log = new Sandbox.Diagnostics.Logger( "ProjectCreator" );

	public Action<string> OnProjectCreated { get; set; }

	private Button OkayButton;
	private LineEdit TitleEdit;
	private LineEdit IdentEdit;
	private Checkbox CreateGitIgnore;
	private Checkbox SetDefaultProjectLocation;
	private FolderEdit FolderEdit;
	private FieldSubtitle FolderFullPath;
	private ProjectTemplate ActiveTemplate;
	private ProjectTemplates Templates;
	private ErrorBox FolderError;
	private bool identEdited;

	private ImageInput IconInput;
	private ImageInput BannerInput;

	private string SelectedIconPath;
	private string SelectedBannerPath;
	private string SelectedSplashPath;

	public ProjectCreator( Widget parent = null ) : base( null )
	{
		Window.Size = new Vector2( 800, 700 );
		Window.MaximumSize = Window.Size;
		Window.MinimumSize = Window.Size;
		Window.SetModal( true, true );

		Window.Title = "Create New Project";
		Window.SetWindowIcon( "sports_esports" );

		Layout = Layout.Row();
		Layout.Spacing = 4;

		// Template list
		var column = Layout.AddColumn( 3 );
		column.Margin = 20;
		column.AddSpacingCell( 8.0f );
		column.Add( new FieldTitle( "Templates" ) );
		column.AddSpacingCell( 18.0f );
		Templates = column.Add( new ProjectTemplates( this ) );

		Layout.AddSeparator();

		var body = Layout.AddColumn( 2 );
		body.Margin = 20;
		body.Spacing = 8;

		body.AddSpacingCell( 8.0f );
		body.Add( new FieldTitle( "Project Setup" ) );
		body.AddSpacingCell( 12.0f );

		body.Add( new FieldTitle( "Title" ) );
		TitleEdit = body.Add( new LineEdit( "" ) { PlaceholderText = "Source 2 Project" } );
		TitleEdit.Text = DefaultProjectName();
		TitleEdit.TextEdited += ( x ) => Validate();

		body.AddSpacingCell( 8 );

		// Ident
		body.Add( new FieldTitle( "Ident" ) );
		body.Add( new FieldSubtitle( "Lowercase version of addon name, no special characters" ) );
		IdentEdit = body.Add( new LineEdit( "" ) { PlaceholderText = "source2project" } );
		IdentEdit.TextEdited += ( x ) => { Validate(); identEdited = true; };
		IdentEdit.SetValidator( "[a-z0-9_]{2,32}" );

		body.AddSpacingCell( 8 );

		// -- Branding Section -- //
		body.AddSpacingCell( 8 );
		body.Add( new FieldTitle( "Project Branding" ) );

		// -- Mandatory Icon -- //
		var iconRow = body.AddRow();
		iconRow.Spacing = 16;
		var iconCol = iconRow.AddColumn();
		iconCol.Spacing = 4;
		iconCol.Add( new FieldSubtitle( "Icon (256x256)" ) );
		IconInput = iconCol.Add( new ImageInput( null, ".png", new Vector2( 64, 64 ) ) );
		IconInput.OnSelected = () => SelectedIconPath = IconInput.Value;

		// -- Optional Banner & Splashscreen -- //
		body.AddSpacingCell( 4 );
		var extrasToggle = body.Add( new Checkbox() { Text = "Enable optional branding" } );

		// Create the optional row
		var optionalRow = body.AddRow();
		optionalRow.Spacing = 16;

		// Banner column
		var bannerCol = optionalRow.AddColumn();
		bannerCol.Spacing = 4;
		var bannerLabel = bannerCol.Add( new FieldSubtitle( "Banner (555x115) – optional" ) );
		BannerInput = bannerCol.Add( new ImageInput( null, ".png", new Vector2( 139, 29 ) ) );
		BannerInput.OnSelected = () => SelectedBannerPath = BannerInput.Value;

		// Splashscreen column
		var splashCol = optionalRow.AddColumn();
		splashCol.Spacing = 4;
		var splashLabel = splashCol.Add( new FieldSubtitle( "Splashscreen (580x370) – optional" ) );
		var splashInput = splashCol.Add( new ImageInput( null, ".png", new Vector2( 145, 92.5f ) ) );
		splashInput.OnSelected = () => SelectedSplashPath = splashInput.Value;

		// Collect all widgets in the optional row
		var optionalWidgets = new Widget[] { bannerLabel, BannerInput, splashLabel, splashInput };

		// Hide them by default
		foreach ( var w in optionalWidgets )
			w.Visible = false;

		// Toggle visibility when checkbox changes
		extrasToggle.StateChanged += ( CheckState state ) =>
		{
			bool enabled = state == CheckState.On;
			foreach ( var w in optionalWidgets )
				w.Visible = enabled;
		};

		body.AddSpacingCell( 8 );

		// Location
		body.Add( new FieldTitle( "Location" ) );
		FolderEdit = body.Add( new FolderEdit( null ) );
		FolderEdit.PlaceholderText = LauncherPreferences.DefaultProjectLocation.NormalizeFilename( false );
		FolderEdit.Text = LauncherPreferences.DefaultProjectLocation.NormalizeFilename( false );
		FolderEdit.TextEdited += ( x ) => Validate();
		FolderEdit.FolderSelected += ( x ) => Validate();

		FolderError = body.Add( new ErrorBox() );
		FolderError.Visible = false;
		FolderError.MinimumHeight = 34;
		FolderError.WordWrap = true;

		body.AddSpacingCell( 8 );

		// Other options
		body.Add( new FieldTitle( "Other" ) );
		CreateGitIgnore = body.Add( new Checkbox() { Value = true, Text = "Create .gitignore" } );
		SetDefaultProjectLocation = body.Add( new Checkbox() { Value = false, Text = "Set as Default Project Location" } );

		body.AddStretchCell( 1 );

		// Footer
		var footer = body.AddRow();
		footer.Spacing = 8;
		FolderFullPath = footer.Add( new FieldSubtitle( "" ) );
		footer.AddStretchCell();
		OkayButton = footer.Add( new Button.Primary( "Create", "add_box" ) { Clicked = CreateProject } );

		// Template selection
		Templates.ListView.ItemSelected += ( object item ) => ActiveTemplate = item as ProjectTemplate;
		ActiveTemplate = Templates.ListView.SelectedItems.First() as ProjectTemplate;

		Validate();
	}

	static string DefaultProjectName()
	{
		string name = "My Project";
		int i = 1;
		while ( Path.Exists( Path.Combine( LauncherPreferences.DefaultProjectLocation, ConvertToIdent( name ) ) ) )
			name = $"My Project {i++}";
		return name;
	}

	static string ConvertToIdent( string title )
	{
		return System.Text.RegularExpressions.Regex
			.Replace( title.ToLower(), "[^a-z0-9_]", "_" )
			.Trim( '_' );
	}

	void Validate()
	{
		if ( !identEdited )
			IdentEdit.Text = ConvertToIdent( TitleEdit.Text );

		bool enabled = true;
		if ( string.IsNullOrWhiteSpace( FolderEdit.Text ) ) enabled = false;
		if ( string.IsNullOrWhiteSpace( TitleEdit.Text ) ) enabled = false;
		if ( string.IsNullOrWhiteSpace( IdentEdit.Text ) ) enabled = false;

		FolderError.Visible = false;
		string fullPath = Path.Combine( FolderEdit.Text, IdentEdit.Text );
		FolderFullPath.Text = fullPath.NormalizeFilename( false );
		if ( Path.Exists( fullPath ) )
		{
			FolderError.Text = $"{FolderFullPath.Text} already exists";
			FolderError.Visible = true;
			enabled = false;
		}

		if ( IdentEdit.Text.Length >= 32 )
			IdentEdit.Text = IdentEdit.Text[..Math.Min( IdentEdit.Text.Length, 32 )];

		OkayButton.Enabled = enabled;
	}

	void CreateProject()
	{
		Debug.WriteLine( "=== CreateProject() START ===" );

		try
		{
			// Log input fields
			Debug.WriteLine( $"FolderEdit.Text = '{FolderEdit.Text}'" );
			Debug.WriteLine( $"IdentEdit.Text = '{IdentEdit.Text}'" );
			Debug.WriteLine( $"SelectedIconPath = '{SelectedIconPath}'" );
			Debug.WriteLine( $"SelectedBannerPath = '{SelectedBannerPath}'" );
			Debug.WriteLine( $"SelectedSplashPath = '{SelectedSplashPath}'" );

			// Validate essential inputs
			if ( string.IsNullOrWhiteSpace( FolderEdit.Text ) || string.IsNullOrWhiteSpace( IdentEdit.Text ) )
			{
				Debug.WriteLine( "ERROR: Folder or Ident is empty. Aborting project creation." );
				return;
			}

			// Create project directories
			var addonPath = Path.Combine( FolderEdit.Text, IdentEdit.Text );
			Directory.CreateDirectory( addonPath );

			var brandingDir = Path.Combine( addonPath, "Editor", "Media" );
			Directory.CreateDirectory( brandingDir );

			Debug.WriteLine( $"Addon Path = {addonPath}" );
			Debug.WriteLine( $"Branding Directory = {brandingDir}" );

			// Prepare relative paths
			string iconRelativePath = (!string.IsNullOrWhiteSpace( SelectedIconPath ) && File.Exists( SelectedIconPath ))
				? "Editor/Media/icon.png" : null;

			string bannerRelativePath = (!string.IsNullOrWhiteSpace( SelectedBannerPath ) && File.Exists( SelectedBannerPath ))
				? "Editor/Media/banner.png" : null;

			string splashRelativePath = (!string.IsNullOrWhiteSpace( SelectedSplashPath ) && File.Exists( SelectedSplashPath ))
				? "Editor/Media/splash.png" : null;

			Debug.WriteLine( $"iconRelativePath = {iconRelativePath}" );
			Debug.WriteLine( $"bannerRelativePath = {bannerRelativePath}" );
			Debug.WriteLine( $"splashRelativePath = {splashRelativePath}" );

			// Create project config
			var config = new ProjectConfig
			{
				ProjectIcon = iconRelativePath,
				ProjectBanner = bannerRelativePath,
				ProjectSplash = splashRelativePath,
				Title = TitleEdit.Text,
				Ident = IdentEdit.Text,
				Org = "local",
				Type = "game",
				Schema = 1,
				Directory = new DirectoryInfo( addonPath )
			};

			Debug.WriteLine( $"Config.ProjectIcon (AFTER INIT) = {config.ProjectIcon}" );

			// Copy files safely
			if ( iconRelativePath != null )
			{
				var iconDest = Path.Combine( brandingDir, "icon.png" );
				File.Copy( SelectedIconPath, iconDest, true );
				Debug.WriteLine( $"Icon copied: {SelectedIconPath} -> {iconDest}" );
			}

			if ( bannerRelativePath != null )
			{
				var bannerDest = Path.Combine( brandingDir, "banner.png" );
				File.Copy( SelectedBannerPath, bannerDest, true );
				Debug.WriteLine( $"Banner copied: {SelectedBannerPath} -> {bannerDest}" );
			}

			if ( splashRelativePath != null )
			{
				var splashDest = Path.Combine( brandingDir, "splash.png" );
				File.Copy( SelectedSplashPath, splashDest, true );
				Debug.WriteLine( $"Splash copied: {SelectedSplashPath} -> {splashDest}" );
			}

			// Apply template safely
			var pt = Templates.ListView.ChosenTemplate;
			if ( pt != null )
			{
				try
				{
					Debug.WriteLine( $"Applying template: {pt.Title}" );
					pt.Apply( addonPath, ref config );

					// Restore icon/banner/splash if template cleared them
					if ( string.IsNullOrWhiteSpace( config.ProjectIcon ) ) config.ProjectIcon = iconRelativePath;
					if ( string.IsNullOrWhiteSpace( config.ProjectBanner ) ) config.ProjectBanner = bannerRelativePath;
					if ( string.IsNullOrWhiteSpace( config.ProjectSplash ) ) config.ProjectSplash = splashRelativePath;
				}
				catch ( Exception ex )
				{
					Debug.WriteLine( $"WARNING: Template apply failed: {ex}" );
				}
			}

			Debug.WriteLine( $"Config.ProjectIcon (AFTER TEMPLATE FIX) = {config.ProjectIcon}" );

			// Save project config
			var configPath = Path.Combine( addonPath, $"{config.Ident}.sbproj" );
			File.WriteAllText( configPath, config.ToJson() );
			Debug.WriteLine( $"Config written to: {configPath}" );

			// Optional .gitignore
			if ( CreateGitIgnore.Value )
			{
				var gitPath = Path.Combine( addonPath, ".gitignore" );
				try
				{
					if ( !File.Exists( gitPath ) )
					{
						File.Copy( FileSystem.Root.GetFullPath( "/templates/template.gitignore" ), gitPath );
						Debug.WriteLine( "Copied .gitignore" );
					}
				}
				catch ( Exception ex )
				{
					Debug.WriteLine( $"WARNING: Failed to copy .gitignore: {ex}" );
				}
			}

			// Set default project location
			if ( SetDefaultProjectLocation.Value )
				LauncherPreferences.DefaultProjectLocation = FolderEdit.Text;

			// Close dialog and fire event
			Close();
			OnProjectCreated?.Invoke( configPath );

			Debug.WriteLine( "=== CreateProject() END ===" );
		}
		catch ( Exception ex )
		{
			Debug.WriteLine( $"FATAL ERROR in CreateProject(): {ex}" );
		}
	}

}

// Custom Input for Image Preview and File Selection
internal class ImageInput : Widget
{
	private Vector2 _targetSize;
	private string _extension;
	public string Value { get; private set; }
	public Action OnSelected { get; set; }

	public ImageInput( Widget parent, string extension, Vector2 targetSize ) : base( parent )
	{
		_targetSize = targetSize;
		_extension = extension;
		Cursor = CursorShape.Finger;
		FixedSize = SizeHint();
	}

	protected override Vector2 SizeHint()
	{
		var aspect = _targetSize.x / _targetSize.y;
		var maxWidth = 256f;
		if ( _targetSize.x > maxWidth ) return new Vector2( maxWidth, maxWidth / aspect );
		return _targetSize;
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		var fd = new FileDialog( null );
		fd.Title = "Select File...";
		fd.Directory = Path.GetDirectoryName( LauncherPreferences.DefaultProjectLocation );
		fd.DefaultSuffix = _extension;
		fd.SetFindFile();
		fd.SetModeOpen();
		fd.SetNameFilter( $"Image (*{_extension})" );

		if ( !fd.Execute() )
			return;

		Value = fd.SelectedFile;
		OnSelected?.Invoke();
	}

	protected override void OnPaint()
	{
		Paint.ClearPen();
		Paint.ClearBrush();

		var r = ContentRect;

		// Background
		Paint.SetBrush( Theme.ControlBackground );
		if ( Paint.HasMouseOver ) Paint.SetBrush( Theme.ControlBackground.Lighten( 0.5f ) );
		Paint.DrawRect( r, 4f );

		// Draw image if selected
		if ( !string.IsNullOrEmpty( Value ) )
		{
			if ( !File.Exists( Value ) )
			{
				Paint.SetDefaultFont( 8f );
				Paint.SetPen( Color.Red );
				Paint.DrawText( r, "File not found!" );
				return;
			}

			try
			{
				var pixmap = Paint.LoadImage( Value, (int)Width, (int)Height );
				if ( pixmap == null )
				{
					Paint.SetDefaultFont( 8f );
					Paint.SetPen( Color.Red );
					Paint.DrawText( r, "Failed to load image!" );
				}
				else
				{
					Paint.SetBrush( pixmap );
					Paint.DrawRect( r );
				}
			}
			catch ( Exception ex )
			{
				Console.WriteLine( $"ImageInput: Exception loading image '{Value}': {ex.Message}" );
				Paint.SetDefaultFont( 8f );
				Paint.SetPen( Color.Red );
				Paint.DrawText( r, "Error loading image!" );
			}
		}
		else
		{
			Paint.SetDefaultFont( 8f );
			Paint.SetPen( Theme.TextControl.WithAlpha( 0.5f ) );
			Paint.DrawText( r, $"[Missing image]" );
		}
	}
}



// Simple helpers
internal class ErrorBox : Label { }
internal class FieldTitle : Label { public FieldTitle( string t ) : base( t ) { } }
internal class FieldSubtitle : Label { public FieldSubtitle( string t ) : base( t ) { WordWrap = true; } }
