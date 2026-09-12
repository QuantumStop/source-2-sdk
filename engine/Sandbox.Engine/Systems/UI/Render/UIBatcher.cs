using Sandbox.Rendering;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Sandbox.UI;

internal class UIBatcher
{
	static GpuBuffer<int> quadIndexBuffer;
	readonly List<ScissorInstance> scissorTable = new();
	readonly Dictionary<int, int> scissorLookup = new();
	readonly List<TransformInstance> transformTable = new();
	readonly Dictionary<int, int> transformLookup = new();
	readonly List<GPUGradientInstance> gradientTable = new();
	readonly Dictionary<int, int> gradientLookup = new();
	readonly List<GPUBorderShape> shapeTable = new();
	readonly Dictionary<int, int> shapeLookup = new();

	// All tables are cumulative within a frame and only need one buffer per frame slot.
	const int FrameCount = 3;
	readonly GpuBuffer<GPUBoxInstance>[] boxBuffers = new GpuBuffer<GPUBoxInstance>[FrameCount];
	readonly GpuBuffer<GPUGlyphInstance>[] glyphBuffers = new GpuBuffer<GPUGlyphInstance>[FrameCount];
	readonly GpuBuffer<uint>[] refBuffers = new GpuBuffer<uint>[FrameCount];
	readonly GpuBuffer<ScissorInstance>[] scissorBuffers = new GpuBuffer<ScissorInstance>[FrameCount];
	readonly GpuBuffer<TransformInstance>[] transformBuffers = new GpuBuffer<TransformInstance>[FrameCount];
	readonly GpuBuffer<GPUGradientInstance>[] gradientBuffers = new GpuBuffer<GPUGradientInstance>[FrameCount];
	readonly GpuBuffer<GPUBorderShape>[] shapeBuffers = new GpuBuffer<GPUBorderShape>[FrameCount];
	int frameIndex;
	readonly List<GPUBoxInstance> frameInstances = new();
	readonly List<GPUGlyphInstance> frameGlyphs = new();
	readonly List<uint> frameRefs = new();

	// Track upload progress so each flush only writes new entries
	int boxUploaded, glyphUploaded, refUploaded;
	int scissorUploaded;
	int transformUploaded;
	int gradientUploaded;
	int shapeUploaded;

	internal int ScissorCount => scissorTable.Count;
	internal int TransformCount => transformTable.Count;
	internal int GradientCount => gradientTable.Count;
	internal int BorderShapeCount => shapeTable.Count;

	internal int GpuBufferCount
	{
		get
		{
			int count = 0;
			for ( int i = 0; i < FrameCount; i++ )
			{
				if ( boxBuffers[i] != null ) count++;
				if ( glyphBuffers[i] != null ) count++;
				if ( refBuffers[i] != null ) count++;
				if ( scissorBuffers[i] != null ) count++;
				if ( transformBuffers[i] != null ) count++;
				if ( gradientBuffers[i] != null ) count++;
				if ( shapeBuffers[i] != null ) count++;
			}
			return count;
		}
	}

	internal void AdvanceFrame()
	{
		frameIndex = (frameIndex + 1) % FrameCount;

		scissorTable.Clear();
		scissorLookup.Clear();
		transformTable.Clear();
		transformLookup.Clear();
		gradientTable.Clear();
		gradientLookup.Clear();
		shapeTable.Clear();
		shapeLookup.Clear();
		frameInstances.Clear();
		frameGlyphs.Clear();
		frameRefs.Clear();
		boxUploaded = glyphUploaded = refUploaded = 0;
		scissorUploaded = 0;
		transformUploaded = 0;
		gradientUploaded = 0;
		shapeUploaded = 0;
	}

	internal int GetOrAddScissor( PanelRenderer.GPUScissor scissor )
	{
		if ( scissor.IsEmpty )
			return -1;

		var hash = scissor.GetHash();

		if ( scissorLookup.TryGetValue( hash, out var existing ) )
			return existing;

		// A glyph packs both indices into 16 bits, the scissor one signed
		Assert.True( scissorTable.Count < 0x7FFF, "UI scissor table is full" );

		var index = scissorTable.Count;
		scissorTable.Add( ScissorInstance.From( scissor ) );

		scissorLookup[hash] = index;
		return index;
	}

	internal int GetOrAddGradient( in GradientInfo gradient )
	{
		var hash = gradient.GetHashCode();

		if ( gradientLookup.TryGetValue( hash, out var existing ) )
			return existing;

		var index = gradientTable.Count;
		gradientTable.Add( GPUGradientInstance.From( in gradient ) );

		gradientLookup[hash] = index;
		return index;
	}

	internal int GetOrAddShape( in GPUBorderShape shape )
	{
		if ( shape.Kind == 0 )
			return -1;

		var hash = shape.GetHash();

		if ( shapeLookup.TryGetValue( hash, out var existing ) )
			return existing;

		var index = shapeTable.Count;
		shapeTable.Add( shape );

		shapeLookup[hash] = index;
		return index;
	}

	internal int GetOrAddTransform( Matrix mat )
	{
		var hash = mat.GetHashCode();

		if ( transformLookup.TryGetValue( hash, out var existing ) )
			return existing;

		Assert.True( transformTable.Count < 0xFFFF, "UI transform table is full" );

		var index = transformTable.Count;
		transformTable.Add( new TransformInstance { Mat = mat } );

		transformLookup[hash] = index;
		return index;
	}

	/// <summary>Places a run's glyphs in this frame's buffer stamped with its scissor and transform, and points the run at them</summary>
	internal void AddGlyphs( List<GPUGlyphInstance> glyphs, ref GPUBoxInstance run )
	{
		Reserve( ref glyphBuffers[frameIndex], frameGlyphs, ref glyphUploaded, run.GlyphCount );

		uint scissorTransform = (ushort)run.ScissorIndex | (uint)(ushort)run.TransformIndex << 16;
		var source = CollectionsMarshal.AsSpan( glyphs ).Slice( run.GlyphStart, run.GlyphCount );
		run.GlyphStart = frameGlyphs.Count;
		frameGlyphs.AddRange( (ReadOnlySpan<GPUGlyphInstance>)source );

		foreach ( ref var glyph in GlyphSpan( run.GlyphStart, run.GlyphCount ) )
		{
			glyph.ScissorTransform = scissorTransform;
		}
	}

	/// <summary>This frame's copy of a run's glyphs, for the debug batch tint</summary>
	internal Span<GPUGlyphInstance> GlyphSpan( int start, int count ) => CollectionsMarshal.AsSpan( frameGlyphs ).Slice( start, count );

	/// <summary>Draws the batch as one instanced call. False when there was nothing to draw.</summary>
	internal bool Draw( List<GPUBoxInstance> instances, CommandList cl, int worldPanelCombo = 0, BlendMode blendMode = BlendMode.Normal )
	{
		int count = instances?.Count ?? 0;
		if ( count == 0 ) return false;

		EnsureQuadIndexBuffer();

		if ( Material.UI.BatchedBox?.IsValid() != true )
			return false;

		// A ref per quad says which buffer it lives in: a box, or a glyph already placed by AddGlyphs. An upload takes a
		// render context and barriers the whole buffer, so the buffers fill here and upload once per command list in Flush.
		var span = CollectionsMarshal.AsSpan( instances );
		int boxes = 0, glyphs = 0;
		for ( int i = 0; i < count; i++ )
		{
			if ( span[i].Mode == GpuFontText.ModeGlyphRun ) glyphs += span[i].GlyphCount;
			else boxes++;
		}

		Reserve( ref boxBuffers[frameIndex], frameInstances, ref boxUploaded, boxes );
		Reserve( ref glyphBuffers[frameIndex], frameGlyphs, ref glyphUploaded, 0 );
		Reserve( ref refBuffers[frameIndex], frameRefs, ref refUploaded, boxes + glyphs );

		BindTable( cl, "ScissorBuffer", ref scissorBuffers[frameIndex], scissorTable, ref scissorUploaded );
		BindTable( cl, "TransformBuffer", ref transformBuffers[frameIndex], transformTable, ref transformUploaded );
		BindTable( cl, "GradientBuffer", ref gradientBuffers[frameIndex], gradientTable, ref gradientUploaded );
		BindTable( cl, "BorderShapeBuffer", ref shapeBuffers[frameIndex], shapeTable, ref shapeUploaded );

		cl.Attributes.Set( "TransformMat", Matrix.Identity );
		cl.Attributes.Set( "HasScissor", 0 );
		cl.Attributes.Set( "BoxInstances", (GpuBuffer)boxBuffers[frameIndex] );
		cl.Attributes.Set( "GlyphInstances", (GpuBuffer)glyphBuffers[frameIndex] );
		cl.Attributes.Set( "InstanceRefs", (GpuBuffer)refBuffers[frameIndex] );
		cl.Attributes.SetCombo( "D_BLENDMODE", (int)blendMode );
		cl.Attributes.SetCombo( "D_WORLDPANEL", worldPanelCombo );

		// Refs in draw order, a glyph run expanding to its glyphs. The counts are known, so make room once.
		int first = frameRefs.Count;
		CollectionsMarshal.SetCount( frameRefs, first + boxes + glyphs );
		var refs = CollectionsMarshal.AsSpan( frameRefs );
		int r = first;

		for ( int i = 0; i < count; i++ )
		{
			ref var inst = ref span[i];

			if ( inst.Mode == GpuFontText.ModeGlyphRun )
			{
				for ( int k = 0; k < inst.GlyphCount; k++ )
					refs[r++] = GPUGlyphInstance.RefBit | (uint)(inst.GlyphStart + k);
			}
			else
			{
				refs[r++] = (uint)frameInstances.Count;
				frameInstances.Add( inst );
			}
		}

		cl.Attributes.Set( "InstanceOffset", first );
		cl.DrawIndexedInstanced( (GpuBuffer)quadIndexBuffer, Material.UI.BatchedBox, boxes + glyphs );
		return true;
	}

	/// <summary>Uploads what the command list's draws appended, once per list rather than per batch</summary>
	internal void Flush()
	{
		UploadTail( boxBuffers[frameIndex], frameInstances, ref boxUploaded );
		UploadTail( glyphBuffers[frameIndex], frameGlyphs, ref glyphUploaded );
		UploadTail( refBuffers[frameIndex], frameRefs, ref refUploaded );
		UploadTail( scissorBuffers[frameIndex], scissorTable, ref scissorUploaded );
		UploadTail( transformBuffers[frameIndex], transformTable, ref transformUploaded );
		UploadTail( gradientBuffers[frameIndex], gradientTable, ref gradientUploaded );
		UploadTail( shapeBuffers[frameIndex], shapeTable, ref shapeUploaded );
	}

	// The table's entries for this list are in place by the time the list runs, so only the binding happens here
	void BindTable<T>( CommandList cl, string name, ref GpuBuffer<T> buffer, List<T> table, ref int uploaded ) where T : unmanaged
	{
		if ( table.Count == 0 ) return;
		Reserve( ref buffer, table, ref uploaded, 0 );
		cl.Attributes.Set( name, (GpuBuffer)buffer );
	}

	// A table can outgrow its buffer before anything reserves for it, and the entries past the end belong to the
	// bigger buffer that replaces it - only what fits is this buffer's to hold
	static void UploadTail<T>( GpuBuffer<T> buffer, List<T> list, ref int uploaded ) where T : unmanaged
	{
		int count = Math.Min( list.Count, buffer?.ElementCount ?? 0 );
		if ( count <= uploaded ) return;

		buffer.SetData<T>( CollectionsMarshal.AsSpan( list ).Slice( uploaded, count - uploaded ), uploaded );
		uploaded = count;
	}

	// Room for more; a buffer being replaced first gets what earlier draws in this list still expect from it
	static void Reserve<T>( ref GpuBuffer<T> buffer, List<T> list, ref int uploaded, int adding ) where T : unmanaged
	{
		int needed = Math.Max( list.Count + adding, 1 );
		if ( buffer is not null && buffer.ElementCount >= needed ) return;

		UploadTail( buffer, list, ref uploaded );
		uploaded = 0;

		// Don't Dispose the old one here — this can run off the main thread, so let the GC finalizer take it
		buffer = new GpuBuffer<T>( Math.Max( 64, (int)BitOperations.RoundUpToPowerOf2( (uint)needed ) ) );
	}

	static void EnsureQuadIndexBuffer()
	{
		if ( quadIndexBuffer != null ) return;

		int[] indices = [0, 1, 2, 0, 2, 3];
		quadIndexBuffer = new GpuBuffer<int>( 6, GpuBuffer.UsageFlags.Index );
		quadIndexBuffer.SetData( indices.AsSpan() );
	}
}
