using Editor;
using System.IO;
using System.Text.Json;

[TestClass]
public class ProjectSplashAssetTests
{
	DirectoryInfo directory;
	string ProjectFile => Path.Combine( directory.FullName, "custom.sbproj" );
	const string Fallback = "common/splash_screen.png";

	[TestInitialize]
	public void Setup() => directory = Directory.CreateTempSubdirectory( "sbox-splash-test-" );

	[TestCleanup]
	public void Cleanup() => directory.Delete( recursive: true );

	[DataTestMethod]
	[DataRow( "ProjectSplash" )]
	[DataRow( "ProjectIcon" )]
	public void ResolvesBrandingRelativeToUnmountedProject( string key )
	{
		var artworkDirectory = Directory.CreateDirectory( Path.Combine( directory.FullName, "Brand Assets" ) );
		var artwork = Path.Combine( artworkDirectory.FullName, "custom.png" );
		File.WriteAllText( artwork, "placeholder" );
		using var config = JsonDocument.Parse( "{\"Metadata\":{\"" + key + "\":\"Brand Assets/custom.png\"}}" );

		var resolved = EditorUtility.Projects.ResolveProjectAsset<string>( config.RootElement, ProjectFile, key, Fallback, path => path );

		Assert.AreEqual( artwork, resolved );
	}

	[DataTestMethod]
	[DataRow( "{}" )]
	[DataRow( "{\"Metadata\":null}" )]
	[DataRow( "{\"Metadata\":{\"ProjectSplash\":null}}" )]
	[DataRow( "{\"Metadata\":{\"ProjectSplash\":42}}" )]
	[DataRow( "{\"Metadata\":{\"ProjectSplash\":\"missing.png\"}}" )]
	public void MissingOrInvalidBrandingUsesOurFallback( string json )
	{
		using var config = JsonDocument.Parse( json );
		var resolved = EditorUtility.Projects.ResolveProjectAsset<string>( config.RootElement, ProjectFile, "ProjectSplash", Fallback, path => path );
		Assert.AreEqual( Fallback, resolved );
	}

	[TestMethod]
	public void UndecodableCustomArtworkUsesOurFallback()
	{
		var artwork = Path.Combine( directory.FullName, "broken.png" );
		File.WriteAllText( artwork, "not an image" );
		using var config = JsonDocument.Parse( """{"Metadata":{"ProjectSplash":"broken.png"}}""" );

		var resolved = EditorUtility.Projects.ResolveProjectAsset<string>( config.RootElement, ProjectFile, "ProjectSplash", Fallback,
			path => path == artwork ? null : path );

		Assert.AreEqual( Fallback, resolved );
	}

	[TestMethod]
	public void StartupWithoutProjectUsesOurFallback()
	{
		var resolved = EditorUtility.Projects.ResolveProjectAsset<string>( default, null, "ProjectSplash", Fallback, path => path );
		Assert.AreEqual( Fallback, resolved );
	}
}
