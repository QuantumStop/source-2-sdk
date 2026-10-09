using System;
using System.Collections.Generic;
using System.IO;

namespace Editor;

/// <summary>
/// A report and settings view. The shared session, not this window, owns the compile. 
/// Made for use in IgnisSDK, extends what's already there for nicer and more advanced compilation.
/// </summary>
internal sealed partial class IgnisSceneCompilerWindow : Dialog
{
	readonly SceneCompileSession _session = SceneCompileSession.Current;

	readonly Bar _bar;
	readonly SegmentedControl _tabs;
	readonly Dictionary<string, Widget> _pages = new();
	readonly ListView _report;
	readonly TextEdit _log;
	readonly Button _compile;
	readonly Button _abort;
	PopupDialogWidget _savePrompt;
	readonly Checkbox _loadAfterBuilding;
	readonly Checkbox _buildPhysics;
	readonly List<Button> _presets = new();
	readonly Widget _output;

	readonly List<Group> _groups = new();
	readonly List<Entry> _lines = new();

	SceneCompileReport _sources;
	string _error;
	string[] _summary;
	int _warningCount;

	IReadOnlyList<string> _displayedLines;
	int _displayedLineCount;
	bool _wasRunning;

	static IgnisSceneCompilerWindow _current;

	public override void OnDestroyed()
	{
		if ( _savePrompt is { IsValid: true } ) _savePrompt.Destroy();
		_session.Changed -= OnSessionChanged;
		if ( _current == this )
			_current = null;

		base.OnDestroyed();
	}

	/// <summary>
	/// Bring up the compiler for the active scene, reusing the window if it's already open.
	/// </summary>
	[Event( "scene.compile.show-report" )]
	[Shortcut( "scene.compiler.show", "F9", typeof( SceneViewWidget ) )]
	internal static void Open( string page = "Report" )
	{
		SceneCompileSession.Current.Refresh();

		if ( _current is { IsValid: true } )
		{
			_current.SelectPage( page );
			_current.Show();
			return;
		}

		_current = new IgnisSceneCompilerWindow( page );
	}

	IgnisSceneCompilerWindow( string page ) : base( EditorWindow )
	{
		Window.Title = $"Build Scene {_session.Name}";
		Window.SetWindowIcon( "hammer/appicon.png" );
		var titleBar = Window.MenuWidget;
		titleBar.OnPaintOverride = () =>
		{
			Paint.SetDefaultFont();
			Paint.SetPen( Theme.TextControl );
			Paint.DrawText( titleBar.LocalRect.Shrink( 36, 0, 120, 0 ), Window.Title, TextFlag.LeftCenter );
			return true;
		};
		Window.Size = new Vector2( 1040, 820 );
		Window.StateCookie = "IgnisSceneCompiler";

		Layout = Layout.Column();
		Layout.Margin = 1;
		Layout.Spacing = 0;

		var splitter = Layout.Add( new Splitter( this ), 1 );
		splitter.IsHorizontal = true;
		splitter.HandleWidth = 5;

		var controls = new Widget( this ) { Layout = Layout.Column(), MinimumWidth = 480, MaximumWidth = 480 };
		controls.SetStyles( "background-color: #303030;" );
		controls.Layout.Margin = 8;
		controls.Layout.Spacing = 8;
		
		splitter.AddWidget( controls );
		splitter.SetCollapsible( 0, false );
		splitter.SetStretch( 0, 1 );

		var presets = controls.Layout.Add( new CompilerGroup( controls, "Presets" ) );
		var presetRow = presets.Body.Layout.AddRow();
		presetRow.Spacing = 6;
		AddPreset( presetRow, "Full\nCompile", "hammer/build_preset_full.png", "Standard compile of all map components" );
		AddPreset( presetRow, "Fast\nCompile", "hammer/build_preset_fast.png", "Build World, Physics, and NAV, but no vis or lighting" );
		AddPreset( presetRow, "Final\nCompile", "hammer/build_preset_final.png", "Build everything, including 'final' quality Lighting" );
		AddPreset( presetRow, "Custom\nCompile", "hammer/build_preset_custom.png", "(Custom)" );
		presetRow.AddStretchCell();

		var settingsGroup = controls.Layout.Add( new CompilerGroup( controls, "Settings", grow: true ), 1 );
		// All settings groups share one viewport beneath the Settings header.
		var scroll = settingsGroup.Body.Layout.Add( new ScrollArea( settingsGroup.Body ), 1 );
		scroll.HorizontalScrollbarMode = ScrollbarMode.Off;
		scroll.VerticalScrollbarMode = ScrollbarMode.Auto;
		var settings = new Widget( scroll ) { Layout = Layout.Column() };
		// Leave room for the editor's overlay scrollbar, even when it appears on resize.
		settings.Layout.Margin = new Sandbox.UI.Margin( 0, 0, 12, 0 );
		settings.Layout.Spacing = 8;
		scroll.Canvas = settings;
		var geometry = settings.Layout.Add( new CompilerGroup( settings, "World", collapsible: true ) );
		geometry.Body.Layout.Add( new SceneCompileSettingsWidget( geometry.Body ) );
		var physics = settings.Layout.Add( new CompilerGroup( settings, "Physics" ) );
		_buildPhysics = physics.Body.Layout.Add( new Checkbox( "Build physics", physics.Body ) );
		_buildPhysics.Value = _session.BuildPhysics;
		_buildPhysics.ToolTip = "Build collision for compiled meshes and aggregated props. Other scene colliders remain unchanged.";
		_buildPhysics.StateChanged += _ =>
		{
			if ( _buildPhysics.Enabled && _session.BuildPhysics != _buildPhysics.Value )
				_session.BuildPhysics = _buildPhysics.Value;
		};
		var nav = settings.Layout.Add( new CompilerGroup( settings, "Nav" ) );
		nav.Body.Layout.Add( new Checkbox( "Build nav", nav.Body )
		{
			Enabled = false,
			ToolTip = "Not implemented. Navmesh baking needs an awaited bake/save step before scene compilation."
		} );
		settings.Layout.AddStretchCell( 1 );

		var loading = controls.Layout.Add( new CompilerGroup( controls, "Load Scene in Engine" ) );
		_loadAfterBuilding = loading.Body.Layout.Add( new Checkbox( "Load scene after building", loading.Body ) );
		_loadAfterBuilding.Value = EditorCookie.Get( "ignis.scenecompiler.loadafterbuilding", false );
		_loadAfterBuilding.StateChanged += _ => EditorCookie.Set( "ignis.scenecompiler.loadafterbuilding", _loadAfterBuilding.Value );
		_loadAfterBuilding.ToolTip = "Run the compiled scene after a successful build.";
		loading.Body.Layout.Add( new Checkbox( "Build cubemaps on load", loading.Body )
		{
			Enabled = false,
			ToolTip = "Not implemented. Baked probes do not rebake on load. Baking requires an editor scene and a bake/save step before compilation."
		} );

		var footer = controls.Layout.AddRow();
		footer.Spacing = 6;
		
		_compile = footer.Add( new Button( "Build" ) { Clicked = OnCompile }, 1 );
		_abort = footer.Add( new Button( "Abort" ) { Clicked = _session.RequestCancel }, 1 );
	
		footer.Add( new Button( "Close" ) { Clicked = Close }, 1 );
	
		_bar = controls.Layout.Add( new Bar() );
		_bar.FixedHeight = 14;

		_output = new Widget( this ) { Layout = Layout.Column(), MinimumWidth = 320 };
		_output.SetStyles( "background-color: #080808;" );
		_output.Layout.Margin = 8;
		_output.Layout.Spacing = 8;

		splitter.AddWidget( _output );
		splitter.SetCollapsible( 1, false );
		splitter.SetStretch( 1, 1 );

		_tabs = _output.Layout.Add( new SegmentedControl() );
		_tabs.OnSelectedChanged = ShowPage;

		_report = new ListView( _output )
		{
			ItemSize = new Vector2( 0, 22 ),
			ItemPaint = PaintEntry,
			ItemClicked = OnEntryClicked,
			ItemContextMenu = OnEntryContextMenu,
			Margin = 4
		};
		_report.SetStyles( "background-color: #080808;" );

		_log = new TextEdit( _output );
		_log.ReadOnly = true;
		_log.HorizontalScrollbarMode = ScrollbarMode.Off;
		_log.SetStyles( "background-color: #080808; color: #dddddd; border: none; font-family: Consolas, monospace; padding: 8px;" );

		AddPage( "Report", "list", _report );
		AddPage( "Log", "notes", _log );

		_session.Changed += OnSessionChanged;
		_wasRunning = _session.Running;
		BuildReport();
		RefreshView();
		SelectPage( page );

		Show();
	}

	void AddPreset( Layout layout, string title, string icon, string tip )
	{
		var button = layout.Add( new PresetButton( title, icon ) );
		button.IsToggle = true;
		button.ToolTip = tip;
		button.Clicked = () =>
		{
			foreach ( var preset in _presets ) preset.IsChecked = preset == button;
		};
		_presets.Add( button );
	}

	void AddPage( string title, string icon, Widget page )
	{
		_pages[title] = page;

		_tabs.AddOption( title, icon );

		_output.Layout.Add( page, 1 );
	}

	void ShowPage( string title )
	{
		foreach ( var (name, page) in _pages )
		{
			page.Visible = name == title;
		}
	}

	/// <summary>
	/// Bring a page up, moving the tabs with it.
	/// </summary>
	void SelectPage( string title )
	{
		if ( !_pages.ContainsKey( title ) ) title = "Report";
		_tabs.Selected = title;

		ShowPage( title );
	}

	void OnSessionChanged()
	{
		if ( !IsValid )
			return;

		var finished = _wasRunning && !_session.Running;
		var started = !_wasRunning && _session.Running;
		_wasRunning = _session.Running;

		if ( finished )
		{
			BuildReport();
			SelectPage( _session.Status == "Failed" ? "Log" : "Report" );
		}
		else if ( started )
		{
			SelectPage( "Log" );
		}

		RefreshView();
	}

	void RefreshView()
	{
		_bar.Status = _session.Status;
		_bar.Text = _session.Running ? "Building ||" : _session.Status switch
		{
			"Done" => "Done",
			"" => NeedsSave ? "Ready" : _session.Error ?? "Ready",
			_ => _session.Status
		};
		_bar.ToolTip = NeedsSave ? "Ready" : _session.Error ?? _session.Status;
		_bar.Fraction = _session.Fraction;
		_bar.Update();

		if ( !ReferenceEquals( _displayedLines, _session.Lines ) )
		{
			_displayedLines = _session.Lines;
			_log.Clear();
			_displayedLineCount = 0;
		}

		while ( _displayedLineCount < _displayedLines.Count )
			_log.AppendPlainText( _displayedLines[_displayedLineCount++] );

		_log.ScrollToBottom();

		if ( _sources != _session.Report || _summary != _session.Summary || _error != _session.Error || _warningCount != _session.Warnings.Count )
			BuildReport();

		UpdateControls();
	}

	[EditorEvent.Frame]
	void UpdateControls()
	{
		if ( !IsValid )
			return;

		if ( _session.Running )
			_bar.Update();

		Window.Title = $"Build Scene {_session.Name}.scene";
		var running = _session.Running;
		_compile.Enabled = CanBuildScene;
		_abort.Text = _session.Cancelling ? "Cancelling" : "Abort";
		_abort.Enabled = running && !_session.Cancelling;
		_loadAfterBuilding.Enabled = !running && !Game.IsPlaying;
		_buildPhysics.Enabled = !running && !Game.IsPlaying && _session.Scene.IsValid()
			&& SceneEditorSession.Active is { IsPrefabSession: false } editor && editor.Scene == _session.Scene;
		_buildPhysics.Value = _session.BuildPhysics;
		
		foreach ( var preset in _presets ) preset.Enabled = !running;
	}

	bool CanRunScene => !_session.Running && !Game.IsPlaying
		&& SceneEditorSession.Active is { IsPrefabSession: false, IsMounted: false, HasUnsavedChanges: false } editor
		&& editor.Scene.IsValid() && editor.Scene == _session.Scene && editor.Scene.Source is not null;

	bool NeedsSave => SceneEditorSession.Active is { } editor && editor.Scene.IsValid() && editor.Scene == _session.Scene
		&& (editor.HasUnsavedChanges || editor.Scene.Source is null);

	bool CanBuildScene => !_session.Running && !Game.IsPlaying && _savePrompt is not { IsValid: true }
		&& SceneEditorSession.Active is { IsPrefabSession: false, IsMounted: false } editor
		&& editor.Scene.IsValid() && editor.Scene == _session.Scene && (_session.CanCompile || NeedsSave);

	void OnCompile()
	{
		if ( !CanBuildScene ) return;
		var editor = SceneEditorSession.Active;
		if ( NeedsSave )
		{
			ShowSavePrompt( editor );
			return;
		}

		BuildScene( editor );
	}

	void ShowSavePrompt( SceneEditorSession editor )
	{
		var scene = editor.Scene;
		var popup = new PopupDialogWidget( "hammer/appicon.png" )
		{
			WindowTitle = "Save changes?",
			FixedWidth = 540
		};
		popup.SetWindowIcon( "hammer/appicon.png" );
		_savePrompt = popup;
		var sourcePath = scene.Source?.ResourcePath;
		var scenePath = string.IsNullOrEmpty( sourcePath ) ? scene.Name
			: AssetSystem.FindByPath( sourcePath ) is { } asset ? asset.GetSourceFile( true ) : sourcePath;
		if ( !string.IsNullOrEmpty( scenePath ) && !string.IsNullOrEmpty( sourcePath ) ) scenePath = Path.GetFullPath( scenePath );
		popup.MessageLabel.Text = $"The scene:\n\n\"{scenePath}\"\n\nhas unsaved changes. Save before building?";
		popup.ButtonLayout.Spacing = 6;
		popup.ButtonLayout.AddStretchCell();
		popup.ButtonLayout.Add( new Button( "Yes" )
		{
			Clicked = () =>
			{
				popup.Destroy();
				if ( !IsValid || SceneEditorSession.Active != editor || editor.Scene != scene
					|| !scene.IsValid() || Game.IsPlaying || _session.Running ) return;

				var settings = (_session.AggregateGeometry, _session.BuildPhysics, _session.AggregateCost, _session.MaxChunkSize);
				editor.Save( false );
				if ( !IsValid || SceneEditorSession.Active != editor || editor.Scene != scene || !scene.IsValid() ) return;
				_session.Refresh();
				// Save As can be cancelled, and saving can fail. Neither starts a build.
				if ( !editor.HasUnsavedChanges && scene.Source is not null )
				{
					// A first save changes the source path and reloads defaults. Keep the user's choice.
					_session.AggregateGeometry = settings.AggregateGeometry;
					_session.BuildPhysics = settings.BuildPhysics;
					_session.AggregateCost = settings.AggregateCost;
					_session.MaxChunkSize = settings.MaxChunkSize;
					BuildScene( editor );
				}
			}
		} );
		popup.ButtonLayout.Add( new Button( "No" ) { Clicked = popup.Destroy } );
		popup.ButtonLayout.Add( new Button( "Cancel" ) { Clicked = popup.Destroy } );
		popup.SetModal( true, true );
		popup.Hide();
		popup.Show();
	}

	async void BuildScene( SceneEditorSession editor )
	{
		if ( !IsValid || SceneEditorSession.Active != editor || !_session.CanCompile ) return;

		var scene = _session.Scene;
		var loadAfterBuilding = _loadAfterBuilding.Value;
		await _session.StartAsync();

		// A build can outlive this dialog. Only run the original scene if it is still active
		// and unchanged; never start a different scene after the user switches documents.
		if ( loadAfterBuilding && (_session.Status is "Done" or "Nothing to compile")
			&& _session.Error is null && CanRunScene && SceneEditorSession.Active == editor && editor.Scene == scene )
		{
			EditorScene.Play( false, editor );
		}
	}

	/// <summary>
	/// Everything the compile is going to do, and everything it's going to leave alone grouped by
	/// why, with the objects listed under each so you can go and look at them.
	/// </summary>
	void BuildReport()
	{
		if ( !IsValid )
			return;

		_sources = _session.Report;
		_summary = _session.Summary;
		_error = _session.Error;
		_warningCount = _session.Warnings.Count;
		_lines.Clear();
		_groups.Clear();

		if ( _sources is null )
		{
			_lines.Add( new Entry { Text = _error ?? "No scene data to compile.", Icon = "error" } );

			Flatten();
			return;
		}

		if ( _error is not null )
			_lines.Add( new Entry { Text = _error, Icon = "error" } );

		if ( _session.Statistics is { } statistics )
		{
			_lines.Add( new Entry
			{
				Text = $"Generated model geometry: {statistics.VertexCount:n0} vertices, {statistics.TriangleCount:n0} triangles",
				Icon = "view_in_ar"
			} );
			_lines.Add( new Entry { Text = $"Aggregate fragments: {statistics.FragmentCount:n0}", Icon = "grid_view" } );
		}

		if ( _summary is not null )
		{
			foreach ( var line in _summary )
			{
				_lines.Add( new Entry { Text = line, Icon = "done" } );
			}
		}

		_lines.Add( new Entry
		{
			Text = $"{_sources.MeshCount:n0} {(_sources.MeshCount == 1 ? "mesh" : "meshes")}, {_sources.PropCount:n0} {(_sources.PropCount == 1 ? "prop" : "props")} selected for aggregation",
			Icon = "category"
		} );

		var groups = new Dictionary<(string Label, SceneCompileSkipReason Reason), Group>();
		var reasons = EditorTypeLibrary.GetEnumDescription( typeof( SceneCompileSkipReason ) );

		foreach ( var skip in _sources.Skipped )
		{
			var key = (skip.Label, skip.Reason);

			if ( !groups.TryGetValue( key, out var group ) )
			{
				group = new Group
				{
					Title = $"{skip.Label} excluded from aggregates - {reasons.GetEntry( skip.Reason ).Title}",
					Reason = skip.Reason
				};
				groups[key] = group;
				_groups.Add( group );
			}

			group.Objects.Add( skip.Component );
		}

		_groups.Sort( ( a, b ) => b.Objects.Count - a.Objects.Count );

		if ( _session.Statistics is { } completed )
		{
			var timings = new Group { Title = "compile stages" };
			foreach ( var stage in completed.Stages )
				timings.Entries.Add( new Entry { Text = $"{stage.Name}: {stage.Duration.TotalSeconds:n2} s", Icon = "schedule", Indent = 20.0f } );
			_groups.Insert( 0, timings );
		}

		if ( _session.Warnings.Count > 0 )
		{
			var warnings = new Group { Title = "compile warnings", Open = true };
			foreach ( var warning in _session.Warnings )
				warnings.Entries.Add( new Entry { Text = warning.Message, Icon = "warning", Indent = 20.0f, Target = warning.Component } );
			_groups.Insert( 0, warnings );
		}

		Flatten();
	}

	/// <summary>
	/// Feed the list what's on show right now. Folding a reason open or shut is just this again -
	/// the list paints its own rows, so nothing is created or destroyed to make it happen.
	/// </summary>
	void Flatten()
	{
		var items = new List<object>( _lines );

		foreach ( var group in _groups )
		{
			items.Add( group.Header );

			if ( !group.Open )
				continue;

			items.AddRange( group.Entries );
			foreach ( var component in group.Objects )
			{
				items.Add( new Entry { Text = component.IsValid() ? component.GameObject.Name : "(Deleted object)", Icon = "my_location", Indent = 20.0f, Target = component } );
			}
		}

		_report.SetItems( items );
	}

	void OnEntryClicked( object item )
	{
		if ( item is not Entry entry )
			return;

		if ( entry.Group is { } group )
		{
			group.Open = !group.Open;

			Flatten();
			return;
		}

		if ( entry.Target is not null )
		{
			Reveal( entry.Target );
		}
	}

	void OnEntryContextMenu( object item )
	{
		if ( item is not Entry { Group: { } group } )
			return;

		var menu = new ContextMenu( this );
		switch ( group.Reason )
		{
			case SceneCompileSkipReason.NotStatic:
				menu.AddOption( "Make All Static", "push_pin", () => MakeStatic( group ) ).Enabled =
					!_session.Running && !Game.IsPlaying
					&& group.Objects.Any( x => x.IsValid() && x.Scene == _session.Scene && !x.GameObject.IsStatic );
				break;
		}

		if ( menu.HasOptions || menu.HasMenus )
			menu.OpenAtCursor();
		else
			menu.Destroy();
	}

	void MakeStatic( Group group )
	{
		if ( _session.Running || Game.IsPlaying
			|| SceneEditorSession.Active is not { IsPrefabSession: false, IsMounted: false } editor
			|| editor.Scene != _session.Scene )
			return;

		var objects = group.Objects
			.Where( x => x.IsValid() && x.Scene == editor.Scene )
			.Select( x => x.GameObject )
			.Where( x => !x.IsStatic )
			.Distinct()
			.ToArray();

		if ( objects.Length == 0 )
			return;

		using var scene = editor.Scene.Push();
		using ( editor.UndoScope( "Make Objects Static" ).WithGameObjectChanges( objects, GameObjectUndoFlags.Properties ).Push() )
		{
			foreach ( var go in objects )
				go.IsStatic = true;
		}

		_session.Refresh();
	}

	static void PaintEntry( VirtualWidget item )
	{
		if ( item.Object is not Entry entry )
			return;

		var clickable = entry.Group is not null || entry.Target is not null;
		var hovered = item.Hovered && clickable;

		if ( hovered )
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.WidgetBackground.Lighten( 0.5f ) );
			Paint.DrawRect( item.Rect, 2.0f );
		}

		var rect = item.Rect.Shrink( 4 + entry.Indent, 0, 4, 0 );
		var color = hovered ? Theme.Blue : Theme.TextControl;

		Paint.SetDefaultFont();

		var icon = entry.Group is { } group ? (group.Open ? "expand_more" : "chevron_right") : entry.Icon;

		if ( !string.IsNullOrEmpty( icon ) )
		{
			Paint.SetPen( color.WithAlpha( 0.6f ) );
			rect.Left += Paint.DrawIcon( rect, icon, 14, TextFlag.LeftCenter ).Width + 6;
		}

		Paint.SetPen( color );
		Paint.DrawText( rect, entry.Text, TextFlag.LeftCenter );
	}

	/// <summary>
	/// Select an object we skipped and look at it, so a reason in the report leads straight to the
	/// thing that caused it.
	/// </summary>
	static void Reveal( Component component )
	{
		if ( !component.IsValid() )
			return;

		var go = component.GameObject;
		var session = SceneEditorSession.Resolve( go );

		if ( session is null )
			return;

		using ( session.Scene.Push() )
		{
			session.Selection.Set( go );
			session.FrameTo( go.GetBounds() );
		}
	}

	/// <summary>
	/// A reason things were skipped, and everything it happened to.
	/// </summary>
	sealed class Group
	{
		public string Title { get; init; }
		public SceneCompileSkipReason Reason { get; init; }
		public List<Component> Objects { get; } = new();
		public List<Entry> Entries { get; } = new();
		public bool Open { get; set; }

		Entry _header;

		/// <summary>
		/// The row that folds this group open and shut. Held onto rather than remade, so the list
		/// keeps the item it already has laid out when the group opens.
		/// </summary>
		public Entry Header => _header ??= new Entry { Text = $"{Objects.Count + Entries.Count} {Title}", Group = this };
	}

	/// <summary>
	/// A line of the report.
	/// </summary>
	sealed class Entry
	{
		public string Text { get; init; }
		public string Icon { get; init; }
		public float Indent { get; init; }
		public Group Group { get; init; }
		public Component Target { get; init; }
	}
	/// <summary>
	/// How far through the current phase we are, drawn as a bar because a compile has no idea how
	/// long it's going to take.
	/// </summary>
	sealed class Bar : Widget
	{
		public float Fraction { get; set; }
		public string Text { get; set; }
		public string Status { get; set; }

		protected override void OnPaint()
		{
			Paint.ClearPen();
			Paint.SetBrush( new Color( 0.5f, 0.5f, 0.5f ) );
			Paint.DrawRect( LocalRect );

			if ( Status == "Done" )
			{
				Paint.SetBrush( new Color( 0, 0.95f, 0 ) );
				Paint.DrawRect( LocalRect );
			}
			else if ( Status == "Failed" )
			{
				Paint.SetBrush( new Color( 0.65f, 0.15f, 0.15f ) );
				Paint.DrawRect( LocalRect );
			}
			else if ( Fraction != 0 )
			{
				Paint.SetBrush( new Color( 0.65f, 0.65f, 0.65f ) );
				Paint.DrawRect( SceneCompileProgress.Fill( LocalRect, Fraction ) );
			}

			Paint.SetDefaultFont();
			Paint.SetPen( Color.White );
			Paint.DrawText( LocalRect.Shrink( 2, 0 ), Text, TextFlag.LeftCenter );
		}
	}
}
