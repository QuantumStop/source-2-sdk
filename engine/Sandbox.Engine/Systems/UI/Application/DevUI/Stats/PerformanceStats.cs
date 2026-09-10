namespace Sandbox.UI.Dev.Stats;

using Sandbox.UI.Construct;

public sealed class PerformanceStats : Panel
{
	readonly StatValue FrameMin;
	readonly StatValue FrameAvg;
	readonly StatValue FrameMax;

	public PerformanceStats()
	{
		AddClass( "statbox" );
		AddClass( "perfstats" );

		var row = Add.Panel( "stat-triple" );
		FrameMin = row.AddChild<StatValue>();
		FrameMin.Title = "Min";
		FrameAvg = row.AddChild<StatValue>();
		FrameAvg.Title = "Avg";
		FrameMax = row.AddChild<StatValue>();
		FrameMax.Title = "Max";
	}

	public override void Tick()
	{
		base.Tick();

		var stats = Sandbox.Diagnostics.PerformanceStats.LastSecond;
		FrameMin.Value = stats.FrameMin.ToString( "0 ms" );
		FrameAvg.Value = stats.FrameAvg.ToString( "0 ms" );
		FrameMax.Value = stats.FrameMax.ToString( "0 ms" );
	}
}
