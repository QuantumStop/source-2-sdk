using System;
using System.IO;
using System.Text.Json;

namespace Editor;

public static partial class EditorUtility
{
	public static partial class Projects
	{
		public static IReadOnlyList<Project> GetAll() => Project.All.AsReadOnly();

		public static async Task<bool> Updated( Project addon )
		{
			// Save changes
			addon?.Save();

			//
			// If we're a transient project we don't need to dirty everything else.
			// we just really want to call the save callback and return.
			//
			if ( addon is not null && addon.IsTransient )
			{
				return true;
			}

			bool compileSuccess = await Project.CompileAsync();
			if ( !compileSuccess )
				return false;

			if ( addon is not null )
			{
				if ( addon.Compiler is not null && !addon.Compiler.BuildSuccess )
					return false;

				if ( addon.EditorCompiler is not null && !addon.EditorCompiler.BuildSuccess )
					return false;
			}

			await WaitForCompiles();

			EditorEvent.Run( "localaddons.changed" );
			SceneEditorSession.Active?.UpdateEditorTitle();

			return true;
		}

		/// <summary>
		/// Wait for the local compiles to be finished
		/// </summary>
		public static async Task WaitForCompiles()
		{
			// give time for any files to finish being written
			//await Task.Delay( 1000 );

			// force finding new files, running callbacks
			FileWatch.Tick();

			// wait for compiles to finish
			await Project.CompileAsync();

			// give time for any files to finish being written
			// this is horrible in this context. We have to wait for 
			// filewatch to tick again to make sure we've picked up all the written files
			// search for FileSystem.Watch( "/.bin/*.dll" ); We need a better way to trigger
			// this shit manually in PackageLoader to 
			// 1. Say this project changed so reload the new package
			// 2. Don't re-trigger after it detects filesystem changes
			await Task.Delay( 500 );

			// filesystem callbacks..
			FileWatch.Tick();

			// Tick the loader to actually load
			Sandbox.GameInstanceDll.PackageLoader.Tick();

			// give time for any files to finish being written
			//await Task.Delay( 1000 );
		}

		/// <summary>
		/// Regenerates the project's solution
		/// </summary>
		public static async Task GenerateSolution()
		{
			await Project.GenerateSolution();
		}

		/// <summary>
		/// Resolve a project asset using a metadata key.
		/// </summary>
		public static Pixmap ResolveProjectAsset(
			JsonElement root,
			string projectFile,
			string metadataKey,
			string fallbackPath,
			Func<string, Pixmap> loader )
		{
			if ( root.ValueKind != JsonValueKind.Object || string.IsNullOrEmpty( projectFile ) )
				return loader( fallbackPath );

			string relative = null;

			// Try metadata lookup: Metadata[metadataKey]
			if ( root.TryGetProperty( "Metadata", out var meta ) &&
				meta.TryGetProperty( metadataKey, out var metaValue ) )
			{
				relative = metaValue.GetString();
			}

			// If it's missing or empty
			if ( string.IsNullOrEmpty( relative ) )
				return loader( fallbackPath );

			string projectDir = Path.GetDirectoryName( Path.GetFullPath( projectFile ) );
			string fullPath = Path.Combine( projectDir, relative.Replace( '/', Path.DirectorySeparatorChar ) );

			// Load resolved file or fallback, just in case
			return File.Exists( fullPath )
				? loader( fullPath )
				: loader( fallbackPath );
		}

	}
}
