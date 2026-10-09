using System;
using System.Text;
using System.Text.Json;

namespace Editor;

partial class StandaloneExporter
{
	// Assemblies that must live inside the base addon folder for bootstrap-time loading.
	private readonly Dictionary<string, byte[]> _baseAssemblyFiles = new();

	async Task Compile()
	{
		var compilerSettings = Project.Config.GetCompileSettings();
		compilerSettings.Whitelist = false;
		if ( !compilerSettings.GetPreprocessorSymbols().Contains( "STANDALONE" ) )
			compilerSettings.DefineConstants += ";STANDALONE";

		var generated = await EditorUtility.Projects.Compile( Project, compilerSettings, ( s ) => Logger.Info( $"[Compiler] {s}" ) );
		if ( generated == null )
		{
			throw new System.Exception( "Failed to compile project" );
		}

		Dictionary<string, object> extrafiles = new();

		foreach ( var assembly in generated )
		{
			var assemblyName = assembly.Compiler.AssemblyName;

			// Base must be loadable before the project assemblies, so ship it with the base addon.
			if ( string.Equals( assemblyName, "package.base", StringComparison.OrdinalIgnoreCase )
				|| string.Equals( assemblyName, "package.local.base", StringComparison.OrdinalIgnoreCase ) )
			{
				_baseAssemblyFiles[$"addons/base/.bin/{assemblyName}.dll"] = assembly.AssemblyData;

				if ( !string.IsNullOrEmpty( assembly.XmlDocumentation ) )
					_baseAssemblyFiles[$"addons/base/.bin/{assemblyName}.xml"] = Encoding.UTF8.GetBytes( assembly.XmlDocumentation );

				var archiveBytes = assembly.Archive?.Serialize();
				if ( archiveBytes is not null && archiveBytes.Length > 0 )
					_baseAssemblyFiles[$"addons/base/.bin/{assemblyName}.cll"] = archiveBytes;

				Logger.Info( $"Adding: {assemblyName}.dll (base addon)" );
				PeekAssembly( assemblyName, assembly.AssemblyData );
				continue;
			}

			extrafiles[$".bin/{assemblyName}.dll"] = assembly.AssemblyData;
			if ( assembly.XmlDocumentation is not null && assembly.XmlDocumentation.Length > 0 )
				extrafiles[$".bin/{assemblyName}.xml"] = assembly.XmlDocumentation;
			extrafiles[$".bin/{assemblyName}.cll"] = assembly.Archive.Serialize();
			Logger.Info( $"Adding: {assemblyName}.dll" );

			PeekAssembly( assemblyName, assembly.AssemblyData );
		}

		_exportConfig.AssemblyFiles = extrafiles;
	}

	/// <summary>
	/// Look inside this assembly for anything useful we can fill the manifest with
	/// </summary>
	private void PeekAssembly( string title, byte[] contents )
	{
		var attr = AssemblyMetadata.GetCustomAttributes( contents );

		var assetAttributes = attr.Where( x => x.AttributeFullName == "Sandbox.Cloud/AssetAttribute" )
								.ToArray();

		foreach ( var a in assetAttributes )
		{
			var ident = $"{a.Arguments[0]}";

			if ( !Package.TryParseIdent( ident, out var parts ) )
			{
				Log.Warning( $"Couldn't parse ident {ident}" );
				continue;
			}

			// do we need to add .version here?
			_exportConfig.CodePackages.Add( $"{parts.org}.{parts.package}" );
		}
	}
}
