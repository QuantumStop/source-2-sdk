namespace Sandbox.UI.Dev.Stats;

public sealed class RenderStats : Panel
{
	readonly StatValue ShadowMaps;
	readonly StatValue UnshadowedLights;
	readonly StatValue ShadowedLights;
	readonly StatValue DrawCalls;
	readonly StatValue TrianglesRendered;
	readonly StatValue ObjectsRendered;

	public RenderStats()
	{
		AddClass( "statbox" );
		AddClass( "renderstats" );

		ShadowMaps = AddStat( "Shadow Maps" );
		UnshadowedLights = AddStat( "Unshadowed Lights" );
		ShadowedLights = AddStat( "Shadowed Lights" );
		DrawCalls = AddStat( "Draw Calls" );
		TrianglesRendered = AddStat( "Triangles Rendered" );
		ObjectsRendered = AddStat( "Objects Rendered" );
	}

	public override void Tick()
	{
		base.Tick();

		if ( DevWindow.MainWindowIsInteracting )
			return;

		var stats = Sandbox.Diagnostics.FrameStats.Current;
		ShadowMaps.Value = stats.ShadowMaps.ToMetric();
		UnshadowedLights.Value = stats.UnshadowedLightsInView.ToMetric();
		ShadowedLights.Value = stats.ShadowedLightsInView.ToMetric();
		DrawCalls.Value = stats.DrawCalls.ToMetric();
		TrianglesRendered.Value = stats.TrianglesRendered.ToMetric();
		ObjectsRendered.Value = stats.ObjectsRendered.ToMetric();
	}

	StatValue AddStat( string title )
	{
		var stat = AddChild<StatValue>();
		stat.Title = title;
		return stat;
	}
}
