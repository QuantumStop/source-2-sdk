using Microsoft.AspNetCore.Components;
using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;

namespace Sandbox.LauncherUI;

public sealed class ProjectCreatorWindowPanel : Panel
{
	[Parameter] public Editor.PanelWindow Window { get; set; }
	[Parameter] public Action<string> OnDone { get; set; }

	readonly ProjectCreatorPanel Creator;

	public ProjectCreatorWindowPanel()
	{
		AddClass( "creator-window-host" );
		AddClass( "ignis-window" );
		StyleSheet.Load( "/styles/editor.scss" );
		StyleSheet.Load( "/styles/ignis-project-creator.scss" );

		var titlebar = this.Add.Panel( "ignis-titlebar" );
		titlebar.AddClass( "window-drag" );
		titlebar.Add.Panel( "ignis-title-icon" );
		titlebar.Add.Label( "New Project", "ignis-window-title" );
		titlebar.Add.Panel( "grow" );

		var minimize = titlebar.Add.Panel( "ignis-windowbutton" );
		minimize.AddClass( "window-nodrag" );
		minimize.Add.Icon( "remove" );
		minimize.AddEventListener( "onclick", () => Window?.Minimize() );

		var close = titlebar.Add.Panel( "ignis-windowbutton" );
		close.AddClass( "window-nodrag" );
		close.AddClass( "close" );
		close.Add.Icon( "close" );
		close.AddEventListener( "onclick", () => Window?.RequestClose() );

		Creator = AddChild<ProjectCreatorPanel>();
		Creator.OnDone = result =>
		{
			OnDone?.Invoke( result );
			Window?.Dispose();
		};
	}
}
