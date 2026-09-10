namespace Sandbox.UI.Dev;

using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Reflection;

/// <summary>
/// Finds project-defined DevUI tabs by scanning loaded assemblies for panels implementing <see cref="IDevUiTab"/>.
/// </summary>
public static class DevUiTabRegistry
{
	public sealed record TabInfo( string Id, string Title, int Order, Type Type, int AssemblyOrder );

	static List<TabInfo> _tabs = new();
	public static IReadOnlyList<TabInfo> Tabs
	{
		get
		{
			EnsureBuilt();
			return _tabs;
		}
	}

	static int _version;
	public static int Version
	{
		get
		{
			EnsureBuilt();
			return _version;
		}
	}

	static volatile bool _dirty = true;
	static bool _building;
	static int _nextAssemblyOrder;
	static readonly Dictionary<Guid, int> _moduleOrder = new();

	static DevUiTabRegistry()
	{
		// Establish an initial order for already-loaded assemblies, then watch for new loads.
		try
		{
			foreach ( var asm in AppDomain.CurrentDomain.GetAssemblies() )
			{
				if ( asm is null )
					continue;

				RememberAssemblyOrder( asm );
			}

			AppDomain.CurrentDomain.AssemblyLoad += (_, args) =>
			{
				if ( args?.LoadedAssembly is Assembly loaded )
					RememberAssemblyOrder( loaded );

				_dirty = true;
			};
		}
		catch
		{
			// Best effort only
		}
	}

	[Event( "hotloaded" )]
	public static void RebuildOnHotload()
	{
		// Mark dirty, next EnsureBuilt() will rebuild.
		_dirty = true;
	}

	static void EnsureBuilt()
	{
		// On a cold boot build lazily.
		if ( _building )
			return;

		if ( _version == 0 || _dirty )
			Build();
	}

	public static void Build()
	{
		if ( _building )
			return;

		_building = true;

		try
		{
			var byId = new Dictionary<string, TabInfo>( StringComparer.OrdinalIgnoreCase );

			var activeAssemblies = GetLatestAssemblies();

			foreach ( var asm in activeAssemblies )
			{
				Type[] types;
				try
				{
					types = asm.GetTypes();
				}
				catch ( ReflectionTypeLoadException e )
				{
					types = e.Types?.Where( x => x is not null ).ToArray();
					if ( types is null || types.Length == 0 )
						continue;
				}
				catch
				{
					continue;
				}

				foreach ( var t in types )
				{
					if ( t is null ) continue;
					if ( t.IsAbstract ) continue;
					if ( t.IsGenericTypeDefinition ) continue;

					if ( !typeof( Panel ).IsAssignableFrom( t ) ) continue;
					if ( !typeof( IDevUiTab ).IsAssignableFrom( t ) ) continue;

					if ( t.GetConstructor( Type.EmptyTypes ) is null )
						continue;

					try
					{
						// Instantiate once to read metadata. Panels without a parent are fine for this.
						if ( Activator.CreateInstance( t ) is not IDevUiTab tab )
							continue;

					var id = tab.DevTabId;
					if ( string.IsNullOrWhiteSpace( id ) )
						id = t.FullName ?? t.Name;

					if ( string.IsNullOrWhiteSpace( id ) )
						continue;

						var title = tab.DevTabTitle;
						if ( string.IsNullOrWhiteSpace( title ) )
							title = id;

						var idTrim = id.Trim();
						var candidate = new TabInfo( idTrim, title.Trim(), tab.DevTabOrder, t, GetAssemblyOrder( asm ) );

						// Multiple assemblies can still contain the same id, we prefer newest.
						if ( byId.TryGetValue( idTrim, out var existing ) )
						{
							if ( candidate.AssemblyOrder >= existing.AssemblyOrder )
								byId[idTrim] = candidate;
						}
						else
						{
							byId[idTrim] = candidate;
						}

					}
					catch ( Exception e )
					{
						Log.Warning( $"DevUiTabRegistry: Failed to load tab {t.FullName}: {e.Message}" );
					}
				}
			}

			var next = byId.Values
				.OrderBy( x => x.Order )
				.ThenBy( x => x.Title, StringComparer.OrdinalIgnoreCase )
				.ToList();

			_tabs = next;

			_dirty = false;
			_version++;
		}
		finally
		{
			_building = false;
		}
	}

	static int GetAssemblyOrder( Assembly asm )
	{
		if ( asm is null )
			return -1;

		var mvid = GetModuleId( asm );
		if ( mvid == Guid.Empty )
			return -1;

		lock ( _moduleOrder )
		{
			if ( _moduleOrder.TryGetValue( mvid, out var idx ) )
				return idx;

			idx = _nextAssemblyOrder++;
			_moduleOrder[mvid] = idx;
			return idx;
		}
	}

	static void RememberAssemblyOrder( Assembly asm )
	{
		if ( asm is null )
			return;

		var mvid = GetModuleId( asm );
		if ( mvid == Guid.Empty )
			return;

		lock ( _moduleOrder )
		{
			if ( !_moduleOrder.ContainsKey( mvid ) )
				_moduleOrder[mvid] = _nextAssemblyOrder++;
		}
	}

	static Guid GetModuleId( Assembly asm )
	{
		try
		{
			return asm?.ManifestModule?.ModuleVersionId ?? Guid.Empty;
		}
		catch
		{
			return Guid.Empty;
		}
	}

	static List<Assembly> GetLatestAssemblies()
	{
		var best = new Dictionary<string, (Version Version, int Order, Assembly Asm)>( StringComparer.OrdinalIgnoreCase );

		foreach ( var asm in AppDomain.CurrentDomain.GetAssemblies() )
		{
			if ( asm is null )
				continue;

			var an = asm.GetName();
			var name = an.Name ?? "";
			if ( name.StartsWith( "System", StringComparison.OrdinalIgnoreCase ) ) continue;
			if ( name.StartsWith( "Microsoft", StringComparison.OrdinalIgnoreCase ) ) continue;

			var ver = an.Version ?? new Version( 0, 0, 0, 0 );
			var order = GetAssemblyOrder( asm );

			if ( best.TryGetValue( name, out var existing ) )
			{
				if ( ver > existing.Version || (ver == existing.Version && order >= existing.Order) )
					best[name] = (ver, order, asm);
			}
			else
			{
				best[name] = (ver, order, asm);
			}
		}

		return best.Values.Select( x => x.Asm ).ToList();
	}
}
