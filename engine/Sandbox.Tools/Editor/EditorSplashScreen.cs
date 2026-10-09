using Sandbox.DataModel;
using Sandbox.Engine;
using Sandbox.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using PanelLabel = Sandbox.UI.Label;

namespace Editor;

internal sealed class EditorSplashScreen : PanelWindow
{
	internal static EditorSplashScreen Singleton;

	internal const string DefaultSplashScreen = "common/splash_screen.png";
	internal const string DefaultIcon = "common/logo.png";
	const float SplashWidth = 580;
	const float ProgressAreaHeight = 14;
	const float LogOverlayHeight = 30;
	const float ProgressInset = 2;

	readonly Texture BackgroundImage;
	readonly Panel ProgressBar;
	readonly PanelLabel MessageLabel;
	Color ProgressTrackColor = new( 42f / 255f, 52f / 255f, 79f / 255f, 1f );
	Color ProgressFillColor = new( 52f / 255f, 80f / 255f, 160f / 255f, 1f );
	long LastFrame;
	bool IsPumping;
	string LatestMessage = "Starting...";

	public EditorSplashScreen() : base( "Opening S&Box Editor", new Vector2( SplashWidth, 384 ), EditorCookie.Get<Vector2?>( "splash.position", null ), borderless: true )
	{
		try
		{
			Resizable = false;
			CanMaximize = false;
			CanClose = false;
			ResizeBorder = 0;
			DropShadow = false;
			RoundedCorners = false;

			var projectFile = Sandbox.Utility.CommandLine.GetSwitch( "-project", "" ).TrimQuoted();
			var config = ReadProjectConfig( projectFile );
			Title = $"Opening {ResolveProjectTitle( config )}";

			using var image = EditorUtility.Projects.ResolveProjectAsset<Bitmap>( config, projectFile, ProjectConfig.MetaSplashKey, DefaultSplashScreen, LoadImage );
			using var icon = EditorUtility.Projects.ResolveProjectAsset<Bitmap>( config, projectFile, ProjectConfig.MetaIconKey, DefaultIcon, LoadImage );
			SetIcon( icon );
			BackgroundImage = image.ToTexture();
			UpdateProgressColorsFromSplash( image );

			var imageHeight = MathF.Floor( SplashWidth * image.Height / image.Width );
			Size = new Vector2( SplashWidth, imageHeight + ProgressAreaHeight );
			MoveToCenter();

			Root.AddClass( "window-drag" );
			Root.Style.Position = PositionMode.Relative;
			Root.Style.FlexDirection = FlexDirection.Column;

			var artwork = Root.Add.Panel();
			artwork.Style.Position = PositionMode.Relative;
			artwork.Style.Height = imageHeight;
			artwork.Style.FlexShrink = 0;
			artwork.Style.BackgroundImage = BackgroundImage;
			artwork.Style.Set( "background-size: 100% 100%; background-repeat: no-repeat;" );

			// Keep our translucent status strip over the artwork.
			var status = artwork.Add.Panel();
			status.Style.Set( "position: absolute; left: 0; right: 0; top: 0; padding: 4px 8px; align-items: center;" );
			status.Style.Height = LogOverlayHeight;
			status.Style.BackgroundColor = Color.Black.WithAlpha( 0.55f );
			MessageLabel = status.AddChild<PanelLabel>();
			MessageLabel.Selectable = false;
			MessageLabel.Text = LatestMessage;
			MessageLabel.Style.Set( "font-family: Century Gothic; font-size: 13px; font-weight: 400; white-space: nowrap; overflow: hidden;" );
			MessageLabel.Style.FontColor = Color.White.WithAlpha( 0.85f );

			var track = Root.Add.Panel();
			track.Style.Height = ProgressAreaHeight;
			track.Style.FlexShrink = 0;
			track.Style.Padding = ProgressInset;
			track.Style.BackgroundColor = ProgressTrackColor;
			ProgressBar = track.Add.Panel();
			ProgressBar.Style.Width = 0;
			ProgressBar.Style.Height = Length.Percent( 100 );
			ProgressBar.Style.Set( "border-radius: 2px;" );
			ProgressBar.Style.BackgroundColor = ProgressFillColor;

			Singleton = this;
			Logging.OnMessage += OnConsoleMessage;
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	static JsonElement ReadProjectConfig( string projectFile )
	{
		if ( string.IsNullOrEmpty( projectFile ) || !File.Exists( projectFile ) ) return default;
		try
		{
			using var document = JsonDocument.Parse( File.ReadAllText( projectFile ) );
			return document.RootElement.Clone();
		}
		catch ( Exception e )
		{
			Log.Warning( $"Couldn't read splash branding from '{projectFile}': {e.Message}" );
			return default;
		}
	}

	static string ResolveProjectTitle( JsonElement root )
	{
		if ( root.ValueKind == JsonValueKind.Object && root.TryGetProperty( "Title", out var title ) && title.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace( title.GetString() ) )
			return title.GetString();

		return "S&Box Editor";
	}

	static Bitmap LoadImage( string path )
	{
		try
		{
			// Project assets are absolute paths: the project has not been mounted yet.
			var bytes = Path.IsPathRooted( path )
				? File.ReadAllBytes( path )
				: FileSystem.Root.ReadAllBytes( $"/core/tools/images/{path}" ).ToArray();
			return Bitmap.CreateFromBytes( bytes );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Couldn't load splash asset '{path}': {e.Message}" );
			return null;
		}
	}

	void OnConsoleMessage( LogEvent message )
	{
		// Logging can reenter while a frame is being drawn. Only touch panels in Pump.
		LatestMessage = message.Message;
		Pump();
	}

	private protected override void OnClosing()
	{
		Logging.OnMessage -= OnConsoleMessage;
		EditorCookie.Set( "splash.position", Position );
		if ( Singleton == this )
		{
			g_pToolFramework2.SetStallMonitorPanelWindow( IntPtr.Zero );
			Singleton = null;
		}
		BackgroundImage?.Dispose();
		base.OnClosing();
	}

	public static void StartupFinish() => Singleton?.Dispose();

	public static void SetProgress( float progress )
	{
		if ( Singleton is not { IsOpen: true } splash ) return;
		splash.ProgressBar.Style.Width = Length.Percent( progress.Clamp( 0f, 1f ) * 100 );
		Pump();
	}

	public static void SetMessage( string message )
	{
		if ( Singleton is not { IsOpen: true } splash ) return;
		splash.LatestMessage = message ?? "Starting...";
		Application.Spin();
		NativeEngine.EngineGlobal.ToolsStallMonitor_IndicateActivity();
	}

	internal static void RestoreStallMonitor()
	{
		if ( Singleton is { IsOpen: true } splash )
			g_pToolFramework2.SetStallMonitorPanelWindow( splash.Handle );
	}

	/// <summary>Present during blocking startup, including nested Qt event loops.</summary>
	internal static void Pump()
	{
		if ( Singleton is not { IsOpen: true } splash || splash.IsPumping ) return;
		if ( Stopwatch.GetElapsedTime( splash.LastFrame ).TotalMilliseconds < 16 ) return;

		splash.IsPumping = true;
		try
		{
			SdlEvents.Poll();
			if ( !splash.IsOpen || (!splash.IsVisible && splash.IsShown) ) return;
			splash.MessageLabel.Text = splash.LatestMessage;
			if ( splash.Frame() ) PanelWindows.FrameEnd();
			RestoreStallMonitor();
			splash.LastFrame = Stopwatch.GetTimestamp();
		}
		finally
		{
			splash.IsPumping = false;
		}
	}

	void UpdateProgressColorsFromSplash( Bitmap image )
	{
		int stepX = Math.Max( 1, image.Width / 56 );
		int stepY = Math.Max( 1, image.Height / 56 );
		double sumR = 0, sumG = 0, sumB = 0, weightSum = 0;

		for ( int y = 0; y < image.Height; y += stepY )
		{
			for ( int x = 0; x < image.Width; x += stepX )
			{
				var color = image.GetPixel( x, y );
				if ( color.a <= 0.01f ) continue;
				sumR += color.r * color.a;
				sumG += color.g * color.a;
				sumB += color.b * color.a;
				weightSum += color.a;
			}
		}

		if ( weightSum <= 0 ) return;
		ProgressFillColor = new Color(
			Math.Min( 1f, (float)(sumR / weightSum) * 0.85f + 0.12f ),
			Math.Min( 1f, (float)(sumG / weightSum) * 0.85f + 0.12f ),
			Math.Min( 1f, (float)(sumB / weightSum) * 0.85f + 0.12f ), 1f );
		ProgressTrackColor = new Color(
			Math.Max( 0.03f, ProgressFillColor.r * 0.22f ),
			Math.Max( 0.03f, ProgressFillColor.g * 0.22f ),
			Math.Max( 0.03f, ProgressFillColor.b * 0.22f ), 1f );
	}
}
