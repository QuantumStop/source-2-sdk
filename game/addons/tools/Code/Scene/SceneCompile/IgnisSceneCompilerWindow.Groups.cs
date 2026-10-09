namespace Editor;

internal sealed partial class IgnisSceneCompilerWindow
{
	/// <summary>
	/// Keep the group shell, inset body and nested content separate.
	/// Hiding the body leaves the header available to expand the group again.
	/// </summary>
	sealed class CompilerGroup : Widget
	{
		public Widget Body { get; }

		public CompilerGroup( Widget parent, string title, bool collapsible = false, bool grow = false ) : base( parent )
		{
			Layout = Layout.Column();
			Layout.Margin = 0;
			Layout.Spacing = 0;
			HorizontalSizeMode = SizeMode.Expand | SizeMode.CanGrow;
			VerticalSizeMode = grow ? SizeMode.CanShrink | SizeMode.CanGrow : SizeMode.CanShrink;

			var header = Layout.Add( new Widget( this ) { Layout = Layout.Row(), FixedHeight = 20 } );
			header.SetStyles( "background-color: #191919;" );
			header.Layout.Margin = 0;
			header.Layout.Spacing = 0;
			if ( collapsible ) header.Layout.AddSpacingCell( 16 );
			
			var label = header.Layout.Add( new Label( title ) { Alignment = TextFlag.Center }, 1 );
			label.SetStyles( "color: white; font-weight: bold; font-size: 14px;" );

			Body = Layout.Add( new Widget( this ) { Layout = Layout.Column() }, grow ? 1 : 0 );
			Body.Layout.Margin = 8;
			Body.Layout.Spacing = 6;
			Body.SetStyles( "background-color: #303030;" );
			Body.HorizontalSizeMode = SizeMode.Expand | SizeMode.CanGrow;
			Body.VerticalSizeMode = grow ? SizeMode.CanShrink | SizeMode.CanGrow : SizeMode.CanShrink;

			if ( collapsible )
			{
				float expandedMaximumHeight = 0;
				var toggle = header.Layout.Add( new Button( "−" ) );
				// Override the editor theme's normal button minimum width and padding.
				toggle.SetStyles( "min-width: 16px; max-width: 16px; min-height: 16px; max-height: 16px; padding: 0px; margin: 0px;" );
				toggle.FixedSize = new Vector2( 16, 16 );
				toggle.Clicked = () =>
				{
					if ( Body.Visible ) expandedMaximumHeight = MaximumHeight;
					Body.Visible = !Body.Visible;
					toggle.Text = Body.Visible ? "−" : "+";
					MaximumHeight = Body.Visible ? expandedMaximumHeight : 20;
				};
			}
		}
	}

	sealed class PresetButton : Button
	{
		public PresetButton( string title, string icon ) : base( title, icon )
		{	
			MinimumSize = new Vector2( 50, 62 );
			MaximumSize  = new Vector2( 50, 62 );
		}

		protected override void OnPaint()
		{
			Paint.ClearPen();
			Paint.SetBrush( IsChecked ? new Color( 0.28f, 0.28f, 0.28f ) : new Color( 0.23f, 0.23f, 0.23f ) );
			Paint.DrawRect( LocalRect, 4 );
			
			if ( IsChecked || Paint.HasMouseOver )
			{
				Paint.ClearBrush();
				Paint.SetPen( IsChecked ? Theme.Primary : Theme.TextLight.WithAlpha( 0.4f ), 1 );
				Paint.DrawRect( LocalRect.Shrink( 1 ), 2 );
			}
			
			Paint.SetPen( Color.White.WithAlpha( Enabled ? 1 : 0.4f ) );
			Paint.DrawIcon( new Rect( LocalRect.Center + new Vector2( 0, -14 ) ), Icon, 22, TextFlag.Center );
			Paint.SetDefaultFont();
			Paint.DrawText( new Rect( 2, 30, LocalRect.Width - 4, 30 ), Text, TextFlag.Center );
		}
	}
}
