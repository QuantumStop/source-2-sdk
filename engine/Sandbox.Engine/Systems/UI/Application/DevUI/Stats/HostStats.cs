namespace Sandbox.UI.Dev.Stats;

public sealed class HostStats : Panel
{
	readonly StatValue Fps;
	readonly StatValue KbIn;
	readonly StatValue KbOut;

	public HostStats()
	{
		AddClass( "statbox" );
		AddClass( "hoststats" );

		Fps = AddStat( "FPS" );
		KbIn = AddStat( "KB In Per Second" );
		KbOut = AddStat( "KB Out Per Second" );
	}

	public override void Tick()
	{
		base.Tick();

		var stats = Networking.HostStats;
		Fps.Value = stats.Fps.ToString();
		KbIn.Value = (stats.InBytesPerSecond / 1024f).ToString( "0.00" );
		KbOut.Value = (stats.OutBytesPerSecond / 1024f).ToString( "0.00" );
	}

	StatValue AddStat( string title )
	{
		var stat = AddChild<StatValue>();
		stat.Title = title;
		return stat;
	}
}
