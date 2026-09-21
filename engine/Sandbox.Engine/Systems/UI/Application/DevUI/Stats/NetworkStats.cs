namespace Sandbox.UI.Dev.Stats;

public sealed class NetworkStats : Panel
{
	readonly StatValue Peers;
	readonly StatValue KbIn;
	readonly StatValue KbOut;
	readonly StatValue PacketsIn;
	readonly StatValue PacketsOut;

	public NetworkStats()
	{
		AddClass( "statbox" );
		AddClass( "networkstats" );

		Peers = AddStat( "Peers" );
		KbIn = AddStat( "KB In Per Second" );
		KbOut = AddStat( "KB Out Per Second" );
		PacketsIn = AddStat( "Packets In Per Second" );
		PacketsOut = AddStat( "Packets Out Per Second" );
	}

	public override void Tick()
	{
		base.Tick();

		if ( DevWindow.MainWindowIsInteracting )
			return;

		var hostConnection = Connection.Host;
		var peerConnections = Connection.All.Where( c => c != Connection.Local && c != hostConnection ).ToArray();

		var totalBytesIn = 0f;
		var totalBytesOut = 0f;
		var totalPacketsIn = 0f;
		var totalPacketsOut = 0f;

		if ( hostConnection is not null )
		{
			var stats = hostConnection.Stats;
			totalBytesIn += stats.InBytesPerSecond;
			totalBytesOut += stats.OutBytesPerSecond;
			totalPacketsIn += stats.InPacketsPerSecond;
			totalPacketsOut += stats.OutPacketsPerSecond;
		}

		foreach ( var c in peerConnections )
		{
			var stats = c.Stats;
			totalBytesIn += stats.InBytesPerSecond;
			totalBytesOut += stats.OutBytesPerSecond;
			totalPacketsIn += stats.InPacketsPerSecond;
			totalPacketsOut += stats.OutPacketsPerSecond;
		}

		var peerCount = peerConnections.Length;
		if ( hostConnection is not null && hostConnection != Connection.Local )
			peerCount++;

		Peers.Value = peerCount.ToString();
		KbIn.Value = (totalBytesIn / 1024f).ToString( "0.00" );
		KbOut.Value = (totalBytesOut / 1024f).ToString( "0.00" );
		PacketsIn.Value = totalPacketsIn.ToString( "0.00" );
		PacketsOut.Value = totalPacketsOut.ToString( "0.00" );
	}

	StatValue AddStat( string title )
	{
		var stat = AddChild<StatValue>();
		stat.Title = title;
		return stat;
	}
}
