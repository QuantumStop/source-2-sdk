namespace Sandbox;

public partial class Surface
{
	/// <summary>
	/// Holds a dictionary of common sounds associated with a surface. This allows you to pick and choose an appropriate sound.
	/// </summary>
	public struct SurfaceSoundCollection
	{
		/// <summary>
		/// The name of the parameter to use for this surface
		/// </summary>
		public string SurfaceParameter { get; set; }
		/// <summary>
		/// Left footstep sound.
		/// </summary>
		[Space] public string FootLeft { get; set; }

		/// <summary>
		/// Right footstep sound.
		/// </summary>
		public string FootRight { get; set; }

		/// <summary>
		/// Jump sound for this surface.
		/// </summary>
		public string FootLaunch { get; set; }

		/// <summary>
		/// Landing sound for this surface.
		/// </summary>
		public string FootLand { get; set; }

		/// <summary>
		/// Bullet impact sound for this surface.
		/// </summary>
		public string Bullet { get; set; }

		/// <summary>
		/// Hard, high velocity impact sound.
		/// </summary>
		public string ImpactHard { get; set; }

		/// <summary>
		/// Soft, low velocity impact sound.
		/// </summary>
		public string ImpactSoft { get; set; }

		/// <summary>
		/// Rough scraping sound when scraping against another surface.
		/// </summary>
		public string ScrapeRough { get; set; }

		/// <summary>
		/// Smooth scraping sound when scraping against another surface.
		/// </summary>
		public string ScrapeSmooth { get; set; }

		/// <summary>
		/// Sound to play when an object made of this breaks
		/// </summary>
		public string Break { get; set; }
	}

	/// <summary>
	/// Sounds for this surface material
	/// </summary>
	[InlineEditor, Title( "Sounds" )]
	public SurfaceSoundCollection SoundCollection
	{
		get => _sounds;
		set
		{
			_sounds = value;
			if ( !_nativeSurface.IsNull )
				_nativeSurface.m_bHasScrapeSounds = HasScrapeSounds;
		}
	}

	SurfaceSoundCollection _sounds;

	internal bool HasScrapeSounds => _sounds.ScrapeRough is not null || _sounds.ScrapeSmooth is not null;
}
