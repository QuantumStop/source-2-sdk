namespace Sandbox.UI.Dev.Stats;

public sealed class SceneStats : Panel
{
	readonly StatValue Animation;
	readonly StatValue Audio;
	readonly StatValue Physics;
	readonly StatValue Particles;
	readonly StatValue RenderTime;
	readonly StatValue Update;
	readonly StatValue GcPause;

	public SceneStats()
	{
		AddClass( "statbox" );
		AddClass( "scenestats" );

		Animation = AddStat( "Animation" );
		Audio = AddStat( "Audio" );
		Physics = AddStat( "Physics" );
		Particles = AddStat( "Particles" );
		RenderTime = AddStat( "Render" );
		Update = AddStat( "Update" );
		GcPause = AddStat( "GcPause" );
	}

	public override void Tick()
	{
		base.Tick();

		Animation.Value = Sandbox.Diagnostics.PerformanceStats.Timings.Animation.AverageMs( 200 ).ToString( "0.00 ms" );
		Audio.Value = Sandbox.Diagnostics.PerformanceStats.Timings.Audio.AverageMs( 200 ).ToString( "0.00 ms" );
		Physics.Value = Sandbox.Diagnostics.PerformanceStats.Timings.Physics.AverageMs( 200 ).ToString( "0.00 ms" );
		Particles.Value = Sandbox.Diagnostics.PerformanceStats.Timings.Particles.AverageMs( 200 ).ToString( "0.00 ms" );
		RenderTime.Value = Sandbox.Diagnostics.PerformanceStats.Timings.Render.AverageMs( 200 ).ToString( "0.00 ms" );
		Update.Value = Sandbox.Diagnostics.PerformanceStats.Timings.Update.AverageMs( 200 ).ToString( "0.00 ms" );
		GcPause.Value = Sandbox.Diagnostics.PerformanceStats.Timings.GcPause.AverageMs( 200 ).ToString( "0.00 ms" );
	}

	StatValue AddStat( string title )
	{
		var stat = AddChild<StatValue>();
		stat.Title = title;
		return stat;
	}
}
