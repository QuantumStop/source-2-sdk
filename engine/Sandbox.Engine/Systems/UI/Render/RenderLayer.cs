using Sandbox.Rendering;
using System.Runtime.InteropServices;

namespace Sandbox.UI;

/// <summary>
/// All descriptor types build into this
/// This is higher than the GPUBoxInstance because of the texture refs
/// but I want the texture refs so we can refresh bindless indices at render time
/// But this should be refactored down into the descriptor itself somehow, since we want to support custom ones..
/// </summary>
internal struct RenderInstance
{
	public GPUBoxInstance GPU;
	public BlendMode BlendMode;
	public int Pass;
	public Texture BackgroundImage;
	public Texture BorderImage;
	public GradientInfo BackgroundGradient;
	// Resolved into the batcher's shape table at render time, like the gradient - the table resets every frame
	public GPUBorderShape BorderShapeData;
	// A second clip on top of the panel's own: box-shadows use it to stay outside (outset) or inside (inset) their box
	public bool HasExtraScissor;
	public PanelRenderer.GPUScissor ExtraScissor;
}

/// <summary>Pairs a user draw descriptor with its position in the instance stream.</summary>
internal struct UserRenderEntry
{
	public IPanelDraw Descriptor;
	public int InsertionIndex;
}

/// <summary>
/// Collects all draw instances for a panel's cached render data.
/// Rebuilt when dirty, drawn during the gather phase.
/// </summary>
internal class RenderLayer
{
	// Per-panel spatial/render state
	public Matrix TransformMat;
	public PanelRenderer.GPUScissor Scissor;

	// Draw descriptors
	public List<RenderInstance> Instances = [];

	// Glyphs packed once here; a ModeGlyphRun instance in Instances stands in for a range of them
	public List<GPUGlyphInstance> Glyphs = [];

	// These should be in the above list, but I'm bored of coding this
	public List<BackdropDrawDescriptor> Backdrops = [];

	// User draw descriptors, ordered by InsertionIndex into Instances
	public List<UserRenderEntry> CustomEntries = [];

	// Trackers whilst building
	int _buildPass;
	BlendMode _buildBlendMode = BlendMode.Normal;
	bool _buildAnyBox;

	public int Total => Instances.Count + Backdrops.Count + CustomEntries.Count;
	public bool IsEmpty => Instances.Count == 0 && Backdrops.Count == 0 && CustomEntries.Count == 0;

	public void AddShadow( in ShadowDrawDescriptor desc )
	{
		Instances.Add( new RenderInstance
		{
			GPU = GPUBoxInstance.FromShadow( desc ),
			BlendMode = desc.Inset ? desc.OverrideBlendMode : BlendMode.Normal,
			Pass = desc.Inset ? _buildPass : 0,
			HasExtraScissor = true,
			ExtraScissor = PanelRenderer.GPUScissor.Single( desc.PanelRect, desc.Radii, desc.ScissorTransformMat, invert: !desc.Inset ),
		} );
	}

	/// <summary>
	/// Quads share a pass until the blend mode changes, and a change starts a new one - the batcher sorts by
	/// pass, so this is what keeps them in the order they were built.
	/// </summary>
	void BeginPass( BlendMode blendMode )
	{
		if ( _buildAnyBox && blendMode != _buildBlendMode )
			_buildPass++;

		_buildBlendMode = blendMode;
		_buildAnyBox = true;
	}

	public void AddBox( in BoxDrawDescriptor desc )
	{
		if ( desc.IsTwoPass ) return;

		BeginPass( desc.OverrideBlendMode );

		Instances.Add( new RenderInstance
		{
			GPU = GPUBoxInstance.From( desc ),
			BlendMode = desc.OverrideBlendMode,
			Pass = _buildPass,
			BackgroundImage = desc.HasImage ? desc.BackgroundImage : null,
			BorderImage = desc.HasBorderImage ? desc.BorderImageTexture : null,
			// An image wins if both are somehow present - the gradient rides the same property.
			BackgroundGradient = !desc.HasImage && desc.HasGradient ? desc.BackgroundGradient : default,
			BorderShapeData = desc.BorderShapeData,
		} );
	}

	/// <summary>
	/// Text instances from <see cref="GpuFontText.Build"/>, drawn in order with the boxes. The block's gradient is
	/// attached to the instances that want it, so it resolves per frame like a background gradient.
	/// </summary>
	public void AddText( List<GPUBoxInstance> instances, BlendMode blendMode, in GradientInfo gradient )
	{
		if ( instances.Count == 0 ) return;

		BeginPass( blendMode );

		// Plain glyphs pack into Glyphs and draw as one run, so a label costs one instance a frame rather than one per glyph
		int runStart = -1;
		Rect runRect = default;

		foreach ( ref var inst in CollectionsMarshal.AsSpan( instances ) )
		{
			if ( GpuFontText.IsCompactGlyph( inst ) )
			{
				var rect = new Rect( inst.Rect.x, inst.Rect.y, inst.Rect.z, inst.Rect.w );
				if ( runStart < 0 ) { runStart = Glyphs.Count; runRect = rect; }
				else runRect.Add( rect );

				Glyphs.Add( GPUGlyphInstance.From( inst ) );
				continue;
			}

			CloseRun();
			Instances.Add( new RenderInstance
			{
				GPU = inst,
				BlendMode = blendMode,
				Pass = _buildPass,
				BackgroundGradient = GpuFontText.WantsGradient( inst ) ? gradient : default,
			} );
		}

		CloseRun();

		void CloseRun()
		{
			if ( runStart < 0 ) return;
			Instances.Add( new RenderInstance { GPU = GpuFontText.GlyphRun( runRect, runStart, Glyphs.Count - runStart ), BlendMode = blendMode, Pass = _buildPass } );
			runStart = -1;
		}
	}

	public void AddOutline( in OutlineDrawDescriptor desc )
	{
		// Same pass rules as AddBox, so the outline stays on top of text that came before it
		if ( _buildAnyBox && desc.OverrideBlendMode != _buildBlendMode )
			_buildPass++;

		_buildBlendMode = desc.OverrideBlendMode;

		Instances.Add( new RenderInstance
		{
			GPU = GPUBoxInstance.FromOutline( desc ),
			BlendMode = desc.OverrideBlendMode,
			Pass = _buildPass,
		} );
	}

	public void AddCustom( IPanelDraw desc )
	{
		if ( desc is null ) return;

		CustomEntries.Add( new UserRenderEntry
		{
			Descriptor = desc,
			InsertionIndex = Instances.Count,
		} );
	}

	/// <summary>
	/// Clear all instances from this layer.
	/// </summary>
	public void Clear()
	{
		Instances.Clear();
		Glyphs.Clear();
		Backdrops.Clear();
		CustomEntries.Clear();
		_buildPass = 0;
		_buildBlendMode = BlendMode.Normal;
		_buildAnyBox = false;
	}

	// Pool management
	static readonly List<RenderLayer> Pool = new();
	static int activeCount;

	internal static int ActiveCount => activeCount;
	internal static int PoolCount => Pool.Count;

	public static RenderLayer Rent()
	{
		activeCount++;

		if ( Pool.Count > 0 )
		{
			var layer = Pool[^1];
			Pool.RemoveAt( Pool.Count - 1 );
			layer.Clear();
			return layer;
		}

		return new RenderLayer();
	}

	public static void Return( RenderLayer layer )
	{
		activeCount--;
		layer.Clear();
		Pool.Add( layer );
	}
}
