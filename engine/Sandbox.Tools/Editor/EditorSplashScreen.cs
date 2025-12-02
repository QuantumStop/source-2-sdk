using NativeEngine;
using Sandbox.DataModel;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;
using static Editor.ProjectPublisher;

namespace Editor
{
	internal class EditorSplashScreen : Widget
	{
		internal static EditorSplashScreen Singleton;
		Pixmap BackgroundImage;

		string PendingMessage = "Starting...";
		string DisplayedMessage = "Starting...";

		internal const string DefaultSplashScreen = "common/splash_screen.png";
		internal const string DefaultIcon = "common/logo.png";

		//	private float LastDisplayTime;
		//	private float MessageCooldown = 0.05f;

		public EditorSplashScreen() : base( null, true )
		{
			WindowFlags = WindowFlags.Window | WindowFlags.Customized | WindowFlags.WindowTitle 
				| WindowFlags.MSWindowsFixedSizeDialogHint | WindowFlags.FramelessWindowHint;

			Singleton = this;
			DeleteOnClose = true;

			string projectFile = Sandbox.Utility.CommandLine.GetSwitch( "project", null );
			JsonElement root = default;

			if ( !string.IsNullOrEmpty( projectFile ) && File.Exists( projectFile ) )
			{
				using var doc = JsonDocument.Parse( File.ReadAllText( projectFile ) );
				root = doc.RootElement.Clone(); 
			}

			string projectName = ResolveProjectTitle( root );
			WindowTitle = $"Opening {projectName}";

			SetWindowIcon(
				EditorUtility.Projects.ResolveProjectAsset(
					root,
					projectFile,
					ProjectConfig.MetaIconKey,
					DefaultIcon,
					Pixmap.FromFile
				)
			);

			BackgroundImage = EditorUtility.Projects.ResolveProjectAsset(
				root,
				projectFile,
				ProjectConfig.MetaSplashKey,
				DefaultSplashScreen,
				Pixmap.FromFile
			);

			string geometryCookie = EditorCookie.GetString( "splash.geometry", null );

			RestoreGeometry( geometryCookie );			         // Restore saved geometry first

			Size *= DpiScale;			                         // Apply DPI scaling
			Size = ClampSplashSize( Size );			             // Clamp to allowed range
			MinimumSize = new Vector2( 100, 71.5f );
			MaximumSize = new Vector2( 700, 500 );

			BackgroundImage = BackgroundImage.Resize( Size );    // Resize background image to match final clamped size

			FixedWidth  = Size.x;
			FixedHeight = Size.y;
			Position    = ScreenGeometry.Center - (Size / 2);	 // Center the window on the screen

			Show();
			ConstrainToScreen();

			g_pToolFramework2.SetStallMonitorMainThreadWindow( _widget );
			Logging.OnMessage += OnConsoleMessage;
		}

		public override void OnDestroyed()
		{
			Logging.OnMessage -= OnConsoleMessage;

			base.OnDestroyed();
			Singleton = null;
		}

		void OnConsoleMessage( LogEvent e )
		{
			OnMessage( e.Message );

			g_pToolFramework2.Spin();
			NativeEngine.EngineGlobal.ToolsStallMonitor_IndicateActivity();
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

		public void OnMessage( string message )
		{
			PendingMessage = message;
			Update();
		}

		protected override bool OnClose()
		{
			return false;
		}

		protected override void OnPaint()
		{
			Paint.Draw( LocalRect, BackgroundImage );

			// TODO: Could be worth exploring I think, for now whatever.
			
			// float now = RealTime.Now;

			// Only update the displayed message at controlled speed
			// if ( now - LastDisplayTime >= MessageCooldown )
			// {
			//		LastDisplayTime = now;
			// }
			
			DisplayedMessage = PendingMessage;

			float barHeight = 20;
			var barRect = new Rect( 0, 0, LocalRect.Width, barHeight );

			Paint.ClearPen();
			Paint.SetBrush( new Color( 0, 0, 0, 0.5f ) );
			Paint.DrawRect( barRect );

			Paint.SetPen( Color.White );
			Paint.SetFont( "Century Gothic", 8, 400 );

			var textRect = barRect.Shrink( 6, 4 );
			Paint.DrawText( textRect, DisplayedMessage, TextFlag.LeftCenter );
		}

		private string ResolveProjectTitle( JsonElement root )
		{
			if ( root.TryGetProperty( "Title", out var titleProp ) )
				return titleProp.GetString();

			return "S&Box Editor"; // Fallback
		}

		private Vector2 ClampSplashSize( Vector2 s )
		{
			float w = Math.Clamp( s.x, 100, 700 );
			float h = Math.Clamp( s.y, 71.5f, 500 );
			return new Vector2( w, h );
		}
	}
}
