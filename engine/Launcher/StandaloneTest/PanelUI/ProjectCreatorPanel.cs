using Sandbox.DataModel;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Sandbox.LauncherUI;

/// <summary>
/// New project screen - pick a template, name it, create it. The creation itself is the same as
/// the Qt creator's: copy the template with $ident/$title substitution, write the .sbproj.
/// </summary>
public class ProjectCreatorPanel : Panel
{
	/// <summary>
	/// Done - with the new .sbproj path, or null if cancelled.
	/// </summary>
	public Action<string> OnDone { get; set; }

	record Template( string Path, ProjectConfig Config, string Icon, string Description, int Order );

	readonly List<Template> _templates = new();
	Template _selected;

	TextEntry _nameBox;
	TextEntry _identBox;
	Editor.FolderSelector _folderBox;
	Checkbox _createGitIgnore;
	Checkbox _setDefaultLocation;
	Checkbox _enableBranding;
	Label _description;
	Label _pathPreview;
	Panel _iconPreview;
	Panel _bannerPreview;
	Panel _splashPreview;

	string _selectedIconPath;
	string _selectedBannerPath;
	string _selectedSplashPath;
	bool _identEdited;

	Button _createButton;

	public ProjectCreatorPanel()
	{
		AddClass( "creator" );

		var container = this.Add.Panel( "creator-container" );
		var content = container.Add.Panel( "creator-content" );
		var body = content.Add.Panel( "creator-body" );

		var templates = body.Add.Panel( "template-pane" );
		templates.Add.Label( "Choose a starting point", "section-heading" );

		FindTemplates();
		BuildTemplateGrid( templates );

		var settings = body.Add.Panel( "settings" );
		settings.Add.Label( "Project details", "section-heading" );
		var settingsFields = settings.Add.Panel( "settings-fields" );

		var nameField = Field( settingsFields, "Title" );
		_nameBox = nameField.AddChild<TextEntry>();
		_nameBox.Placeholder = "Source 2 Project";
		_nameBox.Text = DefaultProjectName();
		_nameBox.OnTextEdited = _ =>
		{
			if ( !_identEdited )
				_identBox.Text = MakeIdent( ProjectTitle );

			UpdatePathPreview();
		};

		var identField = Field( settingsFields, "Ident" );
		_identBox = identField.AddChild<TextEntry>();
		_identBox.Placeholder = "source2project";
		_identBox.Text = MakeIdent( ProjectTitle );
		_identBox.OnTextEdited = _ =>
		{
			_identEdited = true;
			UpdatePathPreview();
		};

		var brandingField = Field( settingsFields, "Project Branding" );
		var branding = brandingField.Add.Panel( "branding" );

		_iconPreview = BrandingPicker( branding, "Icon", "256x256", () => PickBrandingImage( "Icon", path => _selectedIconPath = path, _iconPreview ) );

		_enableBranding = brandingField.AddChild<Checkbox>();
		_enableBranding.LabelText = "Enable optional branding";
		_enableBranding.ValueChanged = enabled =>
		{
			_bannerPreview.SetClass( "hidden", !enabled );
			_splashPreview.SetClass( "hidden", !enabled );
		};

		_bannerPreview = BrandingPicker( branding, "Banner", "555x115", () => PickBrandingImage( "Banner", path => _selectedBannerPath = path, _bannerPreview ) );
		_bannerPreview.AddClass( "banner" );
		_bannerPreview.AddClass( "hidden" );

		_splashPreview = BrandingPicker( branding, "Splash", "580x370", () => PickBrandingImage( "Splashscreen", path => _selectedSplashPath = path, _splashPreview ) );
		_splashPreview.AddClass( "splash" );
		_splashPreview.AddClass( "hidden" );

		var folderField = Field( settingsFields, "Location" );
		_folderBox = folderField.AddChild<Editor.FolderSelector>();
		_folderBox.Text = LauncherPreferences.DefaultProjectLocation;
		_folderBox.ValueChanged = _ => UpdatePathPreview();

		var otherField = Field( settingsFields, "Other" );
		otherField.AddClass( "inline" );
		otherField.SetClass( "otherfields", true );

		_createGitIgnore = otherField.AddChild<Checkbox>();
		_createGitIgnore.LabelText = "Create .gitignore";
		_createGitIgnore.Checked = true;

		_setDefaultLocation = otherField.AddChild<Checkbox>();
		_setDefaultLocation.LabelText = "Set as Default Project Location";

		var buttons = container.Add.Panel( "buttons" );

		_pathPreview = buttons.Add.Label( "", "path-preview" );
		
		buttons.Add.Panel( "grow" );

		_createButton = buttons.AddChild( new Button( "Create", "add_box", "primarybutton", Create ) );

		UpdatePathPreview();
	}

	Panel Field( Panel parent, string title )
	{
		var field = parent.AddChild<Panel>();
		field.AddClass( "field" );
		field.Add.Label( title, "label" );
		return field;
	}

	Panel BrandingPicker( Panel parent, string title, string size, Action onClick )
	{
		var picker = parent.Add.Panel( "branding-picker" );
		picker.AddEventListener( "onclick", onClick );
		picker.Add.Icon( "add_photo_alternate", "icon" );
		picker.Add.Label( title, "title" );
		picker.Add.Label( size, "size" );
		return picker;
	}

	async void PickBrandingImage( string title, Action<string> setter, Panel preview )
	{
		var window = Editor.PanelWindow.FromPanel( this );
		if ( window is null ) return;

		var path = await window.PickOpenFile( LauncherPreferences.DefaultProjectLocation, "Images|png" );
		if ( string.IsNullOrWhiteSpace( path ) ) return;

		setter( path );
		preview.Style.Set( "background-image", $"url( \"{ProjectMediaImage.CreateDataUri( path )}\" )" );
		preview.SetClass( "has-image", true );
	}

	/// <summary>
	/// The same template scan the Qt creator did - every folder in /templates with an
	/// $ident.sbproj in it. The ProjectTemplate meta block is what marks one as shown here,
	/// and carries its icon, description and sort order.
	/// </summary>
	void FindTemplates()
	{
		var root = EngineFileSystem.Root;

		if ( !root.DirectoryExists( "/templates" ) ) return;

		foreach ( var directory in root.FindDirectory( "/templates" ) )
		{
			var configPath = $"/templates/{directory}/$ident.sbproj";
			if ( !root.FileExists( configPath ) ) continue;

			try
			{
				var config = JsonSerializer.Deserialize<ProjectConfig>( root.ReadAllText( configPath ) );

				if ( !config.TryGetMeta( "ProjectTemplate", out JsonElement display ) ) continue;

				var icon = "question_mark";
				if ( display.TryGetProperty( "Icon", out var iconProperty ) )
					icon = iconProperty.GetString() ?? icon;

				var description = "No description provided.";
				if ( display.TryGetProperty( "Description", out var descriptionProperty ) )
					description = descriptionProperty.GetString() ?? description;

				var order = 0;
				if ( display.TryGetProperty( "Order", out var orderProperty ) && orderProperty.TryGetInt32( out var o ) )
					order = o;

				_templates.Add( new Template( root.GetFullPath( $"/templates/{directory}" ), config, icon, description, order ) );
			}
			catch
			{
				// A broken template doesn't take the creator down
			}
		}

		_templates.Sort( ( a, b ) => a.Order.CompareTo( b.Order ) );
	}

	void BuildTemplateGrid( Panel parent )
	{
		var grid = parent.AddChild<Panel>();
		grid.AddClass( "templates" );

		var cells = new Dictionary<Template, Panel>();

		for ( int i = 0; i < _templates.Count; i++ )
		{
			var current = _templates[i];

			var cell = grid.Add.Panel( "template" );
			cell.AddClass( $"stripe-{i % 2}" );
			cell.AddEventListener( "onclick", () =>
			{
				_selected = current;
				_description.Text = current.Description;

				foreach ( var (t, p) in cells )
					p.SetClass( "selected", t == current );
			} );

			var thumb = cell.Add.Panel( "thumb" );
			thumb.Add.Icon( current.Icon, "icon" );

			var text = cell.Add.Panel( "text" );
			text.Add.Label( current.Config.Title, "name" );
			text.Add.Label( current.Description, "sub" );

			cells[current] = cell;
		}

		_description = parent.Add.Label( "", "template-description" );

		_selected = _templates.FirstOrDefault();

		if ( _selected is not null && cells.TryGetValue( _selected, out var first ) )
		{
			first.SetClass( "selected", true );
			_description.Text = _selected.Description;
		}
	}

	static string MakeIdent( string title )
	{
		var ident = System.Text.RegularExpressions.Regex.Replace( title.ToLower(), "[^a-z0-9_]", "_" ).Trim( '_' );
		return ident.Length > 32 ? ident[..32] : ident;
	}

	static string DefaultProjectName()
	{
		string name = "My Project";
		int i = 1;

		while ( Path.Exists( Path.Combine( LauncherPreferences.DefaultProjectLocation, MakeIdent( name ) ) ) )
			name = $"My Project {i++}";

		return name;
	}

	string ProjectTitle => string.IsNullOrWhiteSpace( _nameBox.Text ) ? "My Project" : _nameBox.Text.Trim();

	string ProjectFolder => string.IsNullOrWhiteSpace( _folderBox.Text ) ? LauncherPreferences.DefaultProjectLocation : _folderBox.Text.Trim();

	string ProjectIdent
	{
		get
		{
			var ident = MakeIdent( string.IsNullOrWhiteSpace( _identBox.Text ) ? ProjectTitle : _identBox.Text );
			return ident.Length == 0 ? "my_project" : ident;
		}
	}

	string ProjectPath => Path.Combine( ProjectFolder, ProjectIdent );

	/// <summary>
	/// The footer shows where the project will end up, and complains if it's already taken.
	/// </summary>
	void UpdatePathPreview()
	{
		var path = ProjectPath;
		var exists = Path.Exists( path );

		_pathPreview.Text = exists ? $"{path} already exists" : path;
		_pathPreview.SetClass( "error", exists );

		_createButton.Disabled = exists;
	}

	/// <summary>
	/// Make the project - the same steps as the Qt creator.
	/// </summary>
	void Create()
	{
		var projectPath = ProjectPath;

		if ( Path.Exists( projectPath ) )
		{
			UpdatePathPreview();
			return;
		}

		Directory.CreateDirectory( projectPath );
		var brandingDir = Path.Combine( projectPath, "Editor", "Media" );

		var config = _selected is not null
			? JsonSerializer.Deserialize<ProjectConfig>( _selected.Config.ToJson() )
			: new ProjectConfig();

		config.Ident = ProjectIdent;
		config.Title = ProjectTitle;
		config.Org = "local";
		config.Type ??= "game";
		config.Directory = new DirectoryInfo( projectPath );
		config.Schema = 1;

		var iconRelativePath = CopyBrandingImage( _selectedIconPath, brandingDir, "icon.png" );
		var bannerRelativePath = CopyBrandingImage( _selectedBannerPath, brandingDir, "banner.png" );
		var splashRelativePath = CopyBrandingImage( _selectedSplashPath, brandingDir, "splash.png" );

		config.ProjectIcon = iconRelativePath;
		config.ProjectBanner = bannerRelativePath;
		config.ProjectSplash = splashRelativePath;

		// clear out template info from our new project, it's not needed for end users
		config.SetMeta( "ProjectTemplate", null );

		if ( _selected is not null )
		{
			CopyTemplate( _selected.Path, projectPath, config.Ident, config.Title );
		}

		config.ProjectIcon ??= iconRelativePath;
		config.ProjectBanner ??= bannerRelativePath;
		config.ProjectSplash ??= splashRelativePath;

		var configPath = Path.Combine( projectPath, $"{config.Ident}.sbproj" );
		File.WriteAllText( configPath, config.ToJson() );

		if ( _createGitIgnore.Checked && !File.Exists( Path.Combine( projectPath, ".gitignore" ) ) )
		{
			File.Copy( EngineFileSystem.Root.GetFullPath( "/templates/template.gitignore" ), Path.Combine( projectPath, ".gitignore" ) );
		}

		if ( _setDefaultLocation.Checked )
		{
			LauncherPreferences.DefaultProjectLocation = ProjectFolder;
		}

		OnDone?.Invoke( configPath );
	}

	static string CopyBrandingImage( string source, string directory, string filename )
	{
		if ( string.IsNullOrWhiteSpace( source ) || !File.Exists( source ) )
			return null;

		Directory.CreateDirectory( directory );
		File.Copy( source, Path.Combine( directory, filename ), true );
		return $"Editor/Media/{filename}";
	}

	/// <summary>
	/// Copy a template into place, replacing $ident and $title the way the Qt creator does. The
	/// template's own $ident.sbproj is skipped - we write our own.
	/// </summary>
	static void CopyTemplate( string from, string to, string ident, string title )
	{
		foreach ( var file in Directory.EnumerateFiles( from ) )
		{
			if ( Path.GetFileName( file ) == "$ident.sbproj" ) continue;

			var target = Path.Combine( to, Path.GetFileName( file ).Replace( "$ident", ident ) );

			if ( file.EndsWith( ".cs" ) || file.EndsWith( ".json" ) )
			{
				var text = File.ReadAllText( file ).Replace( "$title", title ).Replace( "$ident", ident );
				File.WriteAllText( target, text );
			}
			else
			{
				File.Copy( file, target, true );
			}
		}

		foreach ( var directory in Directory.EnumerateDirectories( from ) )
		{
			var target = Path.Combine( to, Path.GetFileName( directory ) );
			Directory.CreateDirectory( target );
			CopyTemplate( directory, target, ident, title );
		}
	}
}
