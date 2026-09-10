namespace Sandbox.UI.Dev;

using System;
using System.Linq;
using System.Reflection;

/// <summary>
/// DevUI needs proper console access (protected engine convars/commands).
/// This class solves this specifically by calling ConVarSystem internals via reflection.
/// </summary>
public static class DevConsoleAccess
{
	static readonly Lazy<Type> ConVarSystemType = new( () =>
		typeof( ConsoleSystem ).Assembly.GetType( "Sandbox.ConVarSystem", throwOnError: false, ignoreCase: false ) );

	static MethodInfo GetMethod( string name, params Type[] parameterTypes )
	{
		var t = ConVarSystemType.Value;
		if ( t is null ) return null;

		return t.GetMethods( BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic )
			.FirstOrDefault( m => m.Name == name && m.GetParameters().Select( p => p.ParameterType ).SequenceEqual( parameterTypes ) );
	}

	static readonly Lazy<MethodInfo> RunMethod = new( () => GetMethod( "Run", typeof( string ), typeof( bool ) ) );
	static readonly Lazy<MethodInfo> SetValueMethod = new( () => GetMethod( "SetValue", typeof( string ), typeof( string ), typeof( bool ) ) );
	static readonly Lazy<MethodInfo> GetValueMethod = new( () => GetMethod( "GetValue", typeof( string ), typeof( string ), typeof( bool ) ) );

	public static void Run( string command, bool allowProtected = true )
	{
		try
		{
			if ( RunMethod.Value is not null )
			{
				RunMethod.Value.Invoke( null, new object[] { command, allowProtected } );
				return;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"DevConsoleAccess.Run failed: {command}" );
		}

		// Fallback: old path (may throw).
		try
		{
			ConsoleSystem.Run( command );
		}
		catch ( Exception )
		{
			// DevUI shouldn't throw panel events for bad commands.
		}
	}

	public static void SetValue( string name, string value, bool allowProtected = true )
	{
		try
		{
			if ( SetValueMethod.Value is not null )
			{
				SetValueMethod.Value.Invoke( null, new object[] { name, value, allowProtected } );
				return;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"DevConsoleAccess.SetValue failed: {name} {value}" );
		}

		// Fallback (will respect Game.IsMenu permissions).
		ConsoleSystem.SetValue( name, value );
	}

	public static string GetValue( string name, string defaultValue = null, bool allowEngineVariables = true )
	{
		try
		{
			if ( GetValueMethod.Value is not null )
			{
				return (string)GetValueMethod.Value.Invoke( null, new object[] { name, defaultValue, allowEngineVariables } );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"DevConsoleAccess.GetValue failed: {name}" );
		}

		return ConsoleSystem.GetValue( name, defaultValue );
	}
}

