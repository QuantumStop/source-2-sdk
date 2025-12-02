using Editor;

namespace Editor;
public class Toggle : Widget
{
	public Action<bool> ValueChanged;

	private bool _value;

	public bool Value
	{
		get => _value;
		set
		{
			if ( _value == value )
				return;

			_value = value;
			Update();
			ValueChanged?.Invoke( _value );
		}
	}

	public string Text { get; set; }

	public Toggle( string text, Widget parent = null ) : base( parent )
	{
		Text = text;
	}

	protected override Vector2 SizeHint()
	{
		return new Vector2( 35, 20 );
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;

		var r = LocalRect;
		r.Size = new Vector2( 35, 20 );

		if ( Value )
			Paint.SetBrush( Theme.ButtonBackground );
		else
			Paint.SetBrush( Theme.ControlBackground );

		Paint.SetPen( Theme.Text.WithAlpha( 0.25f ), 1.0f );
		Paint.DrawRect( r, 10.0f );
		Paint.ClearPen();

		if ( Paint.HasMouseOver )
		{
			Paint.SetBrush( Theme.Text.WithAlpha( 0.2f ) );
			Paint.DrawRect( r, 10.0f );
		}

		r = r.Shrink( 4.0f );

		{
			var circle = r;
			circle.Size = circle.Size.WithX( circle.Size.y );

			if ( Value )
				circle.Position = circle.Position.WithX( r.Right - 12.0f );

			Paint.SetBrush( Theme.Text.WithAlpha( 0.5f ) );
			Paint.DrawRect( circle, 16.0f );
		}

		// Draw label
		if ( !string.IsNullOrEmpty( Text ) )
		{
			r = LocalRect + new Vector2( 35 + 8, 0 );
			Paint.SetDefaultFont();
			Paint.ClearBrush();
			Paint.SetPen( Theme.TextControl );
			Paint.DrawText( r, Text, TextFlag.LeftCenter );
		}
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		Value = !Value;
	}
}
