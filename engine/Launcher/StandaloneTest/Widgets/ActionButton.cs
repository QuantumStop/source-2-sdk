using Sandbox;
using System;

namespace Editor;


class ActionButton : Widget
{
	public string Title { get; set; }
	public string Icon { get; set; }

	public bool IsExternal { get; set; }
	public string Url { get; set; }

	public string Subtitle { get; set; }

	public Action OnClick { get; set; }

	public ActionButton( string title, string icon, Action onClick, Widget parent = null ) : base( parent )
	{
		Title = title;
		Icon = icon;
		OnClick = onClick;

		FixedHeight = 28;
		Cursor = CursorShape.Finger;
	}

	public ActionButton( string title, string icon, string url, Widget parent = null ) : base( parent )
	{
		Title = title;
		Icon = icon;
		Url = url;
		IsExternal = true;

		FixedHeight = 28;
		Cursor = CursorShape.Finger;
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		base.OnMouseClick( e );

		if ( !string.IsNullOrEmpty( Url ) )
			EditorUtility.OpenFolder( Url );

		OnClick?.Invoke();
	}

	protected override void DoLayout()
	{
		base.DoLayout();

		FixedHeight = 28;

		if ( !string.IsNullOrEmpty( Subtitle ) )
			FixedHeight = 44;
	}

	protected override void OnPaint()
	{
		Paint.ClearPen();
		Paint.ClearBrush();
		Paint.SetDefaultFont();
		Paint.Antialiasing = true;

		var hasIcon = !string.IsNullOrEmpty( Icon );
		var hasSubtitle = !string.IsNullOrEmpty( Subtitle );

		var align = hasSubtitle ? TextFlag.LeftTop : TextFlag.LeftCenter;

		if ( Paint.HasMouseOver )
			Paint.SetBrush( Theme.SurfaceLightBackground );
		else
			Paint.SetBrush( Theme.SurfaceBackground );

		Paint.DrawRect( LocalRect, 4.0f );

		Paint.ClearBrush();
		Paint.ClearPen();
		Paint.SetPen( Theme.TextControl );

		var r = LocalRect.Shrink( 12.0f, 0 );

		if ( hasSubtitle )
			r.Top += 8.0f;

		//
		// Icon
		//
		if ( hasIcon )
		{
			Paint.DrawIcon( r, Icon, 12.0f, align );
			r = r.Shrink( 20, 0, 0, 0 );
		}

		//
		// Title
		//
		{
			Paint.DrawText( r, Title, align );
		}

		r = r.Shrink( 0, 8.0f );

		//
		// Subtitle
		//
		if ( hasSubtitle )
		{
			Paint.SetPen( Theme.Text.WithAlpha( 0.5f ) );

			r.Top += 8.0f;
			Paint.DrawText( r, Subtitle, align );
		}

		//
		// Link icon
		//
		if ( IsExternal )
		{
			Paint.SetPen( Theme.TextControl.WithAlpha( 0.5f ) );

			var iconRect = r.Align( 12.0f, TextFlag.RightCenter );
			Paint.DrawIcon( iconRect, "open_in_new", 12.0f );
		}
	}
}
