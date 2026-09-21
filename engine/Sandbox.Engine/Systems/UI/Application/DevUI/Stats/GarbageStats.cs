namespace Sandbox.UI.Dev.Stats;

using Sandbox.UI.Construct;

public sealed class GarbageStats : Panel
{
	readonly StatValue Allocations;
	readonly StatValue Gc0;
	readonly StatValue Gc1;
	readonly StatValue Gc2;

	public GarbageStats()
	{
		AddClass( "statbox" );
		AddClass( "gcstats" );

		var left = Add.Panel( "gc-left" );
		Allocations = left.AddChild<StatValue>();
		Allocations.Title = "Allocations";

		var right = Add.Panel( "gc-right stat-triple" );
		Gc0 = right.AddChild<StatValue>();
		Gc0.Title = "Gen0";
		Gc1 = right.AddChild<StatValue>();
		Gc1.Title = "Gen1";
		Gc2 = right.AddChild<StatValue>();
		Gc2.Title = "Gen2";
	}

	public override void Tick()
	{
		base.Tick();

		if ( DevWindow.MainWindowIsInteracting )
			return;

		var stats = Sandbox.Diagnostics.PerformanceStats.LastSecond;
		Allocations.Value = stats.ByteAlloc.FormatBytes();
		Gc0.Value = stats.Gc0.ToMetric();
		Gc1.Value = stats.Gc1.ToMetric();
		Gc2.Value = stats.Gc2.ToMetric();
	}
}
