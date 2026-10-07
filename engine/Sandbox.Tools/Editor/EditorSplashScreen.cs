using System;
using System.IO;

internal sealed class EditorSplashScreen : PanelWindow
{
	internal static EditorSplashScreen Singleton;

		Pixmap BackgroundImage;

		const float InfoAreaHeight = 64;

		public EditorSplashScreen() : base( null, true )
		{
			WindowFlags = WindowFlags.Window | WindowFlags.Customized | WindowFlags.FramelessWindowHint | WindowFlags.MSWindowsFixedSizeDialogHint;
			Singleton = this;
			DeleteOnClose = true;

			WindowTitle = "Opening s&box Editor";
			SetWindowIcon( Pixmap.FromFile( "logo_rounded.png" ) );
			BackgroundImage = LoadSplashImage();

			// load any saved geometry
			string geometryCookie = EditorCookie.GetString( "splash.geometry", null );
			RestoreGeometry( geometryCookie );

			var aspect = (float)BackgroundImage.Height / BackgroundImage.Width;
			Size = new( 700, (700 * aspect).FloorToInt() + InfoAreaHeight );

			Show();

			UpdateGeometry();
			CenterWindow();

			//
			// Resample background image if dpi scale is gonna make us draw it bigger
			//
			if ( DpiScale != 1.0f )
			{
				BackgroundImage = BackgroundImage.Resize( BackgroundImage.Size * DpiScale );
			}

			WidgetUtil.MakeWindowDraggable( _widget );

			ConstrainToScreen();

			g_pToolFramework2.SetStallMonitorMainThreadWindow( _widget );
		}

		/// <summary>
		/// Try to load project's splash_screen.png from its root,
		/// Falls back to the default built-in screen
		/// </summary>
		static Pixmap LoadSplashImage()
		{
			var projectPath = Sandbox.Utility.CommandLine.GetSwitch( "-project", "" ).TrimQuoted();

			if ( !string.IsNullOrEmpty( projectPath ) )
			{
				var projectDir = Path.GetDirectoryName( Path.GetFullPath( projectPath ) );
				var customSplash = Path.Combine( projectDir, "splash_screen.png" );

				if ( File.Exists( customSplash ) )
				{
					var pixmap = Pixmap.FromFile( customSplash );
					if ( pixmap is not null )
						return pixmap;
				}
			}

			return Pixmap.FromFile( "splash_screen.png" );
		}

		public override void OnDestroyed()
		{
			base.OnDestroyed();
			Singleton = null;
		}

		public static void StartupFinish()
		{
			if ( Singleton.IsValid() )
			{
				EditorCookie.Set( "splash.geometry", Singleton.SaveGeometry() );
				Singleton.Destroy();
			}

			Singleton = null;
		}

		string LatestMessage;
		float Progress;

	/// <summary>
	/// Updates the progress bar.
	/// </summary>
	public static void SetProgress( float progress )
	{
		if ( Singleton is not { IsOpen: true } splash ) return;
		splash.ProgressBar.Style.Width = Length.Percent( progress.Clamp( 0f, 1f ) * 100 );
		Pump();
	}

	/// <summary>
	/// Set the current displayed message.
	/// </summary>
	public static void SetMessage( string message )
	{
		if ( Singleton is not { IsOpen: true } splash ) return;
		splash.MessageLabel.Text = message ?? "Starting editor…";
		Application.Spin();
		NativeEngine.EngineGlobal.ToolsStallMonitor_IndicateActivity();
	}

		protected override bool OnClose()
		{
			return false;
		}

		protected override void OnPaint()
		{
			var imageRect = LocalRect;
			imageRect.Bottom -= BottomAreaHeight;
			Paint.Draw( imageRect, BackgroundImage );

			DisplayedMessage = PendingMessage;

			var logRect = new Rect( imageRect.Left, imageRect.Top, imageRect.Width, LogOverlayHeight );
			var progressAreaRect = new Rect( LocalRect.Left, imageRect.Bottom, LocalRect.Width, ProgressAreaHeight );

			Paint.ClearPen();
			Paint.SetBrush( Color.Black.WithAlpha( 0.55f ) );
			Paint.DrawRect( logRect );

			var textRect = logRect.Shrink( 8, 4 );

			Paint.SetPen( Color.White.WithAlpha( 0.85f ) );
			Paint.SetFont( "Century Gothic", 8, 400 );
			Paint.DrawText( textRect, LatestMessage ?? DisplayedMessage ?? "Bootstrapping..", TextFlag.LeftCenter );

			Paint.ClearPen();
			Paint.SetBrush( ProgressTrackColor );
			Paint.DrawRect( progressAreaRect );

			if ( Progress > 0f )
			{
				var fillRect = progressAreaRect.Shrink( ProgressInset );
				fillRect.Width *= Progress;

				Paint.SetBrush( ProgressFillColor );
				Paint.DrawRect( fillRect, 2.0f );
			}
		}

		private string ResolveProjectTitle( JsonElement root )
		{
			if ( root.TryGetProperty( "Title", out var titleProp ) )
				return titleProp.GetString();

			return "S&Box Editor";
		}

		void UpdateProgressColorsFromSplash()
		{
			if ( BackgroundImage is null || BackgroundImage.Width <= 0 || BackgroundImage.Height <= 0 )
				return;

			int stepX = Math.Max( 1, BackgroundImage.Width / 56 );
			int stepY = Math.Max( 1, BackgroundImage.Height / 56 );

			double sumR = 0;
			double sumG = 0;
			double sumB = 0;
			double weightSum = 0;

			for ( int y = 0; y < BackgroundImage.Height; y += stepY )
			{
				for ( int x = 0; x < BackgroundImage.Width; x += stepX )
				{
					var c = BackgroundImage.GetPixel( x, y );
					if ( c.a <= 0.01f )
						continue;

					double w = c.a;
					sumR += c.r * w;
					sumG += c.g * w;
					sumB += c.b * w;
					weightSum += w;
				}
			}

			if ( weightSum <= 0.0 )
				return;

			float avgR = (float)(sumR / weightSum);
			float avgG = (float)(sumG / weightSum);
			float avgB = (float)(sumB / weightSum);

			// Keep the splash tone, but nudge it brighter for a clearer progress fill.
			ProgressFillColor = new Color(
				Math.Min( 1f, avgR * 0.85f + 0.12f ),
				Math.Min( 1f, avgG * 0.85f + 0.12f ),
				Math.Min( 1f, avgB * 0.85f + 0.12f ),
				1f
			);

			// Same hue family, much darker for contrast against the fill.
			ProgressTrackColor = new Color(
				Math.Max( 0.03f, ProgressFillColor.r * 0.22f ),
				Math.Max( 0.03f, ProgressFillColor.g * 0.22f ),
				Math.Max( 0.03f, ProgressFillColor.b * 0.22f ),
				1f
			);
		}
	}
}
