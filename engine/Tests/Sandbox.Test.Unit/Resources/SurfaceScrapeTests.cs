using System.Runtime.InteropServices;

namespace ResourceTests;

// The fork uses FMOD event names; retain coverage of the shared surface/contact data.
[TestClass]
public class SurfaceScrapeTests
{
	[TestMethod]
	public void ScrapeEligibilityTracksSoundChanges()
	{
		var surface = new Surface();
		Assert.IsFalse( surface.HasScrapeSounds );
		surface.SoundCollection = new() { ScrapeSmooth = "event:/physics/scrape_smooth" };
		Assert.IsTrue( surface.HasScrapeSounds );
		surface.SoundCollection = new() { ScrapeRough = "event:/physics/scrape_rough" };
		Assert.IsTrue( surface.HasScrapeSounds );
		surface.SoundCollection = default;
		Assert.IsFalse( surface.HasScrapeSounds );
	}

	[TestMethod]
	public void ContactLayoutMatchesNative()
	{
		Assert.AreEqual( 40, Marshal.SizeOf<PhysicsBody3d.ScrapeContact>() );
		Assert.AreEqual( 4, Marshal.OffsetOf<PhysicsBody3d.ScrapeContact>( "Point" ).ToInt32() );
		Assert.AreEqual( 16, Marshal.OffsetOf<PhysicsBody3d.ScrapeContact>( "Normal" ).ToInt32() );
		Assert.AreEqual( 28, Marshal.OffsetOf<PhysicsBody3d.ScrapeContact>( "FrictionForce" ).ToInt32() );
		Assert.AreEqual( 32, Marshal.OffsetOf<PhysicsBody3d.ScrapeContact>( "SelfSurfaceIndex" ).ToInt32() );
		Assert.AreEqual( 36, Marshal.OffsetOf<PhysicsBody3d.ScrapeContact>( "OtherSurfaceIndex" ).ToInt32() );
	}

	[TestMethod]
	public void UnassignedContactHasNoBodyOrSurface()
	{
		var contact = new PhysicsBody3d.ScrapeContact { OtherBodyIndex = -1, SelfSurfaceIndex = -1, OtherSurfaceIndex = -1 };
		Assert.IsNull( contact.OtherBody );
		Assert.IsNull( contact.Surface );
		contact.SelfSurfaceIndex = int.MaxValue;
		Assert.IsNull( contact.Surface );
	}
}
