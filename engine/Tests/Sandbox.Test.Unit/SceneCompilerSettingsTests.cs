using Editor;
using System.Text.Json;

[TestClass]
public class SceneCompilerSettingsTests
{
	[TestMethod]
	public void ExistingMetadataKeepsAggregationEnabled()
	{
		var settings = JsonSerializer.Deserialize<SceneCompilerSettings>( "{\"AggregateCost\":64,\"MaxChunkSize\":1024}" );
		Assert.IsTrue( settings.AggregateGeometry );
		Assert.IsTrue( settings.BuildPhysics );
		Assert.AreEqual( 64f, settings.AggregateCost );
		Assert.AreEqual( 1024f, settings.MaxChunkSize );
		settings.Validate();
	}

	[TestMethod]
	public void DisabledAggregationSurvivesMetadataRoundTrip()
	{
		var settings = new SceneCompilerSettings { AggregateGeometry = false, AggregateCost = 64, MaxChunkSize = 512 };
		var restored = JsonSerializer.Deserialize<SceneCompilerSettings>( JsonSerializer.Serialize( settings ) );
		Assert.IsFalse( restored.AggregateGeometry );
		Assert.AreEqual( settings, restored );
		restored.Validate();
	}

	[TestMethod]
	public void DisabledPhysicsSurvivesMetadataRoundTrip()
	{
		var settings = new SceneCompilerSettings { BuildPhysics = false };
		var restored = JsonSerializer.Deserialize<SceneCompilerSettings>( JsonSerializer.Serialize( settings ) );
		Assert.IsFalse( restored.BuildPhysics );
		Assert.IsTrue( restored.AggregateGeometry );
		Assert.AreEqual( settings, restored );
		restored.Validate();
	}
}
