namespace Sandbox.UI.Dev;

using Sandbox;
using Sandbox.UI.Construct;
using Sandbox.UI.Dev.Stats;

public sealed class DevDebugTab : Panel
{
	readonly DevScrollPanel _scroll;

	public DevDebugTab()
	{
		AddClass( "devtab" );
		AddClass( "debugtab" );

		_scroll = AddChild<DevScrollPanel>();
		_scroll.AddClass( "debug-scroll" );
		_scroll.EnableHorizontal = false;

		var grid = _scroll.Canvas.Add.Panel( "debug-grid" );

		var left = grid.Add.Panel( "col left" );
		var right = grid.Add.Panel( "col right" );

		var perf = AddGroup( left, "PERFORMANCE", "show_chart" );
		perf.Body.AddChild<PerformanceStats>();

		var gc = AddGroup( left, "GARBAGE COLLECTOR", "delete" );
		gc.Body.AddChild<GarbageStats>();

		var render = AddGroup( right, "RENDER STATS", "insights" );
		render.Body.AddChild<RenderStats>();

		var scene = AddGroup( right, "SCENE STATS", "view_in_ar" );
		scene.Body.AddChild<SceneStats>();

		if ( Networking.IsActive )
		{
			var net = AddGroup( left, "NETWORK STATS", "network_check" );
			net.Body.AddChild<NetworkStats>();

			if ( !Networking.IsHost )
			{
				var host = AddGroup( left, "HOST STATS", "dns" );
				host.Body.AddChild<HostStats>();
			}
		}

		var overlaysCard = AddGroup( left, "OVERLAYS", "dashboard_customize" );

		var overlays = overlaysCard.Body.Add.Panel( "overlays-grid" );
		var overlayIndex = 0;
		void AddOverlayRow( string title, string convar, string on = "1", string off = "0" )
		{
			var t = AddOverlay( overlays, title, convar, on, off );
			if ( overlayIndex % 2 == 1 )
				t.AddClass( "right" );
			overlayIndex++;
		}

		// Interleave left/right so the wrap layout forms two clean columns.
		AddOverlayRow( "PROFILER", "overlay_profile" );
		AddOverlayRow( "POSTPROCESS STACK", "overlay_pp" );
		AddOverlayRow( "FRAME STATS", "overlay_frame" );
		AddOverlayRow( "GPU PROFILER", "overlay_gpu" );
		AddOverlayRow( "ALLOCATIONS", "overlay_alloc" );
		AddOverlayRow( "RESOURCES", "overlay_resources" );
		AddOverlayRow( "NETWORK GRAPH", "overlay_network_graph" );
		AddOverlayRow( "PHYSICS DEBUG", "physics_debug_draw" );
		AddOverlayRow( "NETWORK MESSAGES", "overlay_network_calls" );
		AddOverlayRow( "CONSOLE OUTPUT", "consoleoverlay", on: "True", off: "False" );
		AddOverlayRow( "FRAME TIME GRAPH", "overlay_fps" );

		// Put the render mode dropdown on its own row, aligned to the right.
		var footer = overlaysCard.Body.Add.Panel( "overlays-footer" );

		var renderMode = footer.AddChild<RenderModeSelect>();
		renderMode.AddClass( "overlay-row right" );
	}

	public override void Tick()
	{
		base.Tick();

		// When the window is narrow, stack columns (wrap-to-vertical) instead of forcing a horizontal scroll/clipped layout.
		// Keep the threshold slightly above the configured min widths so it flips before things get unusable.
		SetClass( "narrow", Box.Rect.Width < 720f );
	}

	static DevGroup AddGroup( Panel parent, string title, string icon )
	{
		var g = parent.AddChild<DevGroup>();
		g.Title = title;
		g.IconName = icon;
		return g;
	}

	static ConvarToggle AddOverlay( Panel parent, string title, string convar, string on = "1", string off = "0" )
	{
		var t = parent.AddChild<ConvarToggle>();
		t.AddClass( "overlay-row" );
		t.Title = title;
		t.ConVar = convar;
		t.On = on;
		t.Off = off;
		return t;
	}

	static void SetLit()
	{
		DevConsoleAccess.SetValue( "mat_fullbright", "0", allowProtected: true );
		DevConsoleAccess.SetValue( "mat_toolsvis", "0", allowProtected: true );

		if ( Game.ActiveScene?.Camera is CameraComponent cam )
		{
			cam.DebugMode = SceneCameraDebugMode.Normal;
		}
	}
}
