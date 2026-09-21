namespace Sandbox.UI.Dev;

using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;

[Library( "devwindow" )]
public sealed class DevWindow : DevWindowFrame
{
	const string CookieX = "devui.window.x";
	const string CookieY = "devui.window.y";
	const string CookieW = "devui.window.w";
	const string CookieH = "devui.window.h";
	const string CookieTab = "devui.window.tab";
	const string CookieTabId = "devui.window.tabid";

	Panel LogToggles;
	Panel LogResizeHandle;

	DevLogTab LogTab;
	DevDebugTab DebugTab;

	readonly List<Button> _tabButtons = new();
	readonly Dictionary<string, Panel> _tabPanels = new( StringComparer.OrdinalIgnoreCase );
	readonly Dictionary<string, int> _tabOrders = new( StringComparer.OrdinalIgnoreCase );
	string _activeTabId;
	int _seenTabRegistryVersion = -1;

	internal static bool MainWindowIsInteracting => ConsoleOverlay.MainWindow?.IsInteracting == true;
	string ActiveTabId => string.IsNullOrWhiteSpace( _activeTabId ) ? "log" : _activeTabId;

	public DevWindow()
	{
		SetTitle( "CONSOLE" );

		var x = Game.Cookies.Get( CookieX, 64.0f );
		var y = Game.Cookies.Get( CookieY, 64.0f );
		var w = Game.Cookies.Get( CookieW, 640.0f );
		var h = Game.Cookies.Get( CookieH, 720.0f );

		Style.Left = Length.Pixels( x );
		Style.Top = Length.Pixels( y );
		Style.Width = Length.Pixels( w );
		Style.Height = Length.Pixels( h );

		_activeTabId = Game.Cookies.Get( CookieTabId, "" );
		if ( string.IsNullOrWhiteSpace( _activeTabId ) )
		{
			var legacy = Game.Cookies.Get( CookieTab, 0 );
			_activeTabId = legacy switch
			{
				1 => "debug",
				_ => "log"
			};
		}

		TabPanel.Add.Panel( "spacer" );
		LogToggles = TabPanel.Add.Panel( "log-toggles" );

		LogTab = ContentPanel.AddChild<DevLogTab>();
		DebugTab = ContentPanel.AddChild<DevDebugTab>();

		LogTab?.Console?.CreateLevelToggles( LogToggles );

		LogResizeHandle = AddResizeHandle( this );
		LogResizeHandle.AddClass( "log-resize-handle" );

		RebuildTabs();
		SetTab( ActiveTabId );
	}

	public void FocusConsole()
	{
		SetTab( "log" );
		LogTab?.FocusConsole();
	}

	public void BlurConsole() => LogTab?.BlurConsole();

	public override void Tick()
	{
		base.Tick();

		if ( _seenTabRegistryVersion != DevUiTabRegistry.Version )
		{
			RebuildTabs();
			SetTab( ActiveTabId );
		}
	}

	protected override void SaveWindowState()
	{
		Game.Cookies.Set( CookieX, Style.Left?.Value ?? 0.0f );
		Game.Cookies.Set( CookieY, Style.Top?.Value ?? 0.0f );
		Game.Cookies.Set( CookieW, Style.Width?.Value ?? 0.0f );
		Game.Cookies.Set( CookieH, Style.Height?.Value ?? 0.0f );
		Game.Cookies.Set( CookieTabId, ActiveTabId );
	}

	void RebuildTabs()
	{
		foreach ( var child in ContentPanel?.Children?.ToArray() ?? Array.Empty<Panel>() )
		{
			if ( child is null ) continue;
			if ( child == LogTab ) continue;
			if ( child == DebugTab ) continue;
			child.Delete( true );
		}

		TabPanel?.DeleteChildren( true );
		_tabButtons.Clear();
		_tabPanels.Clear();
		_tabOrders.Clear();

		void AddTab( string id, string title, int order, Panel panel )
		{
			_tabPanels[id] = panel;
			_tabOrders[id] = order;

			var b = TabPanel.AddChild( new Button( title, null, () => SetTab( id ) ) );
			b.SetAttribute( "tabid", id );
			_tabButtons.Add( b );
		}

		AddTab( "log", "LOG", 0, LogTab );
		AddTab( "debug", "DEBUG", 10, DebugTab );

		TabPanel.Add.Panel( "spacer" );
		LogToggles = TabPanel.Add.Panel( "log-toggles" );
		LogTab?.Console?.CreateLevelToggles( LogToggles );

		_seenTabRegistryVersion = DevUiTabRegistry.Version;

		foreach ( var tab in DevUiTabRegistry.Tabs )
		{
			if ( string.Equals( tab.Id, "log", StringComparison.OrdinalIgnoreCase ) ) continue;
			if ( string.Equals( tab.Id, "debug", StringComparison.OrdinalIgnoreCase ) ) continue;

			Panel panel;
			try
			{
				panel = Activator.CreateInstance( tab.Type ) as Panel;
			}
			catch
			{
				continue;
			}

			if ( panel is null )
				continue;

			var host = ContentPanel.AddChild<DevCustomTabHost>();
			host.SetContent( panel );

			AddTab( tab.Id, tab.Title, tab.Order, host );
		}

		var ordered = _tabButtons
			.Select( b => new { Button = b, Id = b.GetAttribute( "tabid", "" ) } )
			.OrderBy( x => _tabOrders.TryGetValue( x.Id, out var o ) ? o : 10_000 )
			.ThenBy( x => x.Button.Text, StringComparer.OrdinalIgnoreCase )
			.ToList();

		for ( var i = 0; i < ordered.Count; i++ )
		{
			TabPanel.SetChildIndex( ordered[i].Button, i );
		}

		var spacer = TabPanel.Children.FirstOrDefault( x => x.HasClass( "spacer" ) );
		if ( spacer is not null ) TabPanel.SetChildIndex( spacer, TabPanel.ChildrenCount - 1 );
		if ( LogToggles is not null ) TabPanel.SetChildIndex( LogToggles, TabPanel.ChildrenCount - 1 );
	}

	void SetTab( string id )
	{
		_activeTabId = id ?? "log";
		Game.Cookies.Set( CookieTabId, ActiveTabId );
		SetClass( "log-active", string.Equals( ActiveTabId, "log", StringComparison.OrdinalIgnoreCase ) );

		foreach ( var child in ContentPanel?.Children ?? Array.Empty<Panel>() )
		{
			child?.SetClass( "active", false );
		}

		foreach ( var kv in _tabPanels )
		{
			kv.Value?.SetClass( "active", string.Equals( kv.Key, ActiveTabId, StringComparison.OrdinalIgnoreCase ) );
		}

		foreach ( var b in _tabButtons )
		{
			var tabId = b.GetAttribute( "tabid", "" );
			b.Active = string.Equals( tabId, ActiveTabId, StringComparison.OrdinalIgnoreCase );
		}

		LogToggles?.SetClass( "hidden", !string.Equals( ActiveTabId, "log", StringComparison.OrdinalIgnoreCase ) );
	}
}
