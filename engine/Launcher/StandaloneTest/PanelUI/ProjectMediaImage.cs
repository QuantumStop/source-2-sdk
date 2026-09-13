using Microsoft.AspNetCore.Components;
using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.IO;

namespace Sandbox.LauncherUI;

public sealed class ProjectMediaImage : Image
{
	string projectRoot;
	string source;
	string fallback;
	static readonly Dictionary<string, Texture> TextureCache = new( StringComparer.OrdinalIgnoreCase );

	[Parameter]
	public string ProjectRoot
	{
		get => projectRoot;
		set
		{
			if ( projectRoot == value ) return;
			projectRoot = value;
			ReloadTexture();
		}
	}

	[Parameter]
	public string Source
	{
		get => source;
		set
		{
			if ( source == value ) return;
			source = value;
			ReloadTexture();
		}
	}

	[Parameter]
	public string Fallback
	{
		get => fallback;
		set
		{
			if ( fallback == value ) return;
			fallback = value;
			ReloadTexture();
		}
	}

	void ReloadTexture()
	{
		Texture = LoadProjectTexture() ?? LoadFallbackTexture();
		IsRenderDirty = true;
		LayoutTree.MarkDirty();
	}

	Texture LoadProjectTexture()
	{
		if ( string.IsNullOrWhiteSpace( source ) )
			return null;

		string fullPath = ResolveFullPath( source );
		if ( string.IsNullOrWhiteSpace( fullPath ) || !File.Exists( fullPath ) )
			return null;

		if ( TextureCache.TryGetValue( fullPath, out var cached ) && cached.IsValid() )
			return cached;

		string uri = CreateDataUri( fullPath );
		if ( string.IsNullOrWhiteSpace( uri ) )
			return null;

		var texture = Texture.Load( uri, false );
		if ( texture.IsValid() )
			TextureCache[fullPath] = texture;

		return texture;
	}

	Texture LoadFallbackTexture()
	{
		if ( string.IsNullOrWhiteSpace( fallback ) )
			return null;

		return Texture.Load( fallback, false );
	}

	string ResolveFullPath( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return null;

		path = path.Replace( '/', Path.DirectorySeparatorChar );

		if ( Path.IsPathRooted( path ) )
			return path;

		if ( string.IsNullOrWhiteSpace( projectRoot ) )
			return null;

		return Path.Combine( projectRoot, path );
	}

	public static string CreateDataUri( string path )
	{
		try
		{
			string mime = Path.GetExtension( path ).ToLowerInvariant() switch
			{
				".jpg" or ".jpeg" => "image/jpeg",
				".webp" => "image/webp",
				".svg" => "image/svg+xml",
				_ => "image/png"
			};

			return $"data:{mime};base64,{Convert.ToBase64String( File.ReadAllBytes( path ) )}";
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"Couldn't load project media '{path}'" );
			return null;
		}
	}
}
