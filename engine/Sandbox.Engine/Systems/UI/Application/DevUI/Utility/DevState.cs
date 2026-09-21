namespace Sandbox.UI.Dev;

using System;
using System.Collections.Generic;

internal sealed class DevState<T>
{
	readonly Action<T> _onChanged;
	T _value;

	public DevState( T initial, Action<T> onChanged )
	{
		_value = initial;
		_onChanged = onChanged;
	}

	public T Value
	{
		get => _value;
		set
		{
			if ( EqualityComparer<T>.Default.Equals( _value, value ) )
				return;

			_value = value;
			_onChanged?.Invoke( value );
		}
	}

	public override string ToString()
	{
		return _value?.ToString() ?? "";
	}
}
