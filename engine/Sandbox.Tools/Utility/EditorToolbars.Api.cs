namespace Editor;

using Sandbox;
using System;
using System.Collections.Generic;

public static partial class EditorToolBars
{
	/// <summary>
	/// Stores all registered toolbars for easy access by name.
	/// </summary>
	public static Dictionary<string, ToolBar> RegisteredToolbars { get; } = [];

	/// <summary>
	/// Creates a new toolbar with the given name and optional initial options.
	/// Automatically registers it in the public toolbar dictionary.
	/// </summary>
	public static ToolBar CreateToolBar( string name, List<ToolOptionDef> defs, DockWindow window )
	{
		if ( string.IsNullOrWhiteSpace( name ) ) 
			throw new ArgumentException( "Toolbar name cannot be null or empty.", nameof( name ) );

		var bar = new ToolBar( window, name );

		if ( defs != null && defs.Count > 0 )
			AddDefs( bar, defs );

		RegisterToolBar( name, bar, window );

		window.DockManager.RegisterDockType( $"Editor - {name}", "hammer/appicon.ico", () => bar );

		return bar;
	}

	/// <summary>
	/// Adds a set of options to an existing toolbar.
	/// </summary>
	public static void AddOptionsToToolBar( string toolbarName, List<ToolOptionDef> defs )
	{
		if ( !RegisteredToolbars.TryGetValue( toolbarName, out var bar ) || defs == null )
			return;

		AddDefs( bar, defs );
	}

	/// <summary>
	/// Enables or disables a specific toolbar button by name.
	/// </summary>
	public static void SetOptionEnabled( string toolbarName, string optionName, bool enabled )
	{
		if ( !RegisteredToolbars.TryGetValue( toolbarName, out var bar ) ) return;

		foreach ( var option in bar._options )
		{
			if ( option.Text == optionName )
			{
				option.Enabled = enabled;
				return;
			}
		}
	}

	/// <summary>
	/// Activate or deactivate a specific toolbar button by name.
	/// </summary>
	public static void SetOptionActive( string toolbarName, string optionName, bool active )
	{
		if ( !RegisteredToolbars.TryGetValue( toolbarName, out var bar ) ) return;

		foreach ( var option in bar._options )
		{
			if ( option.Text == optionName )
			{
				option.Checked = active;
				return;
			}
		}
	}

	/// <summary>
	/// Force-deactivate an option by name (also updates ToolOptionDef Active state if available)
	/// </summary>
	public static void ForceDeactivateOption( string toolbarName, List<ToolOptionDef> defs, string optionName )
	{
		if ( !RegisteredToolbars.TryGetValue( toolbarName, out var bar ) || defs == null ) return;

		var def = defs.Find( d => d.Name == optionName );
		if ( def != null )
		{
			def.Active = false;
			if ( def.Widget != null )
			{
				def.Widget.Checked = false;
				def.Widget.Icon = def.Icon;
			}
		}
	}

	/// <summary>
	/// Registers a toolbar so it can be accessed and manipulated via the public API.
	/// </summary>
	public static void RegisterToolBar( string name, ToolBar bar, DockWindow window )
	{
		if ( string.IsNullOrWhiteSpace( name ) || bar == null ) return;
		RegisteredToolbars[name] = bar;

		var dock = window.DockManager;
	}
}
