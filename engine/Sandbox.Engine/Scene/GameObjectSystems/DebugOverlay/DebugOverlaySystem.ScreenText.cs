namespace Sandbox;

using System.Text;

public partial class DebugOverlaySystem
{
	[ConVar( "debug_font_size", Saved = true, Min = 1, Max = 50 )]
	internal static int DebugFontSize { get; set; } = 10;

	[ConVar( "debug_font_name", Saved = true )]
	internal static string DebugFontName { get; set; } = "Courier New";

	[ConVar( "debug_font_smooth", Saved = true )]
	internal static UI.FontSmooth DebugFontSmooth { get; set; } = UI.FontSmooth.Auto;

	[ConVar( "debug_font_filter", Saved = true )]
	internal static Rendering.FilterMode DebugFilterMode { get; set; } = Rendering.FilterMode.Anisotropic;

	/// <summary>
	/// Draw text on the screen
	/// </summary>
	public void ScreenText( Vector2 pixelPosition, string text, float size = 14, TextFlag flags = TextFlag.Center, Color color = new Color(), float duration = 0 )
	{
		if ( color == default ) color = Color.White;

		var scope = new TextRendering.Scope( text, color, size, weight: 500 );
		scope.Shadow = new TextRendering.Shadow { Enabled = true, Color = Color.Black, Offset = 3, Size = 1 };

		ScreenText( pixelPosition, scope, flags, duration );
	}

	/// <summary>
	/// Draw text on the screen
	/// </summary>
	public void ScreenText( Vector2 pixelPosition, TextRendering.Scope textBlock, TextFlag flags = TextFlag.Center, float duration = 0 )
	{
		var so = new TextSceneObject( Scene.SceneWorld );
		so.ScreenPos = pixelPosition;
		so.ScreenSize = Screen.Size; // probably
		so.Flags.CastShadows = false;
		so.RenderLayer = SceneRenderLayer.OverlayWithoutDepth;
		so.TextBlock = textBlock;
		so.TextFlags = flags;
		so.BuildCommandList();

		Add( duration, so );
	}

	/// <summary>
	/// Toggles adding/removing debug overlay text on screen for entries marked with DebugExpose attribute
	/// Returns a bool so we can check elsewhere if it was added or removed.
	/// </summary>
	public bool ScreenTextOverlay( Component comp )
	{
		var existingRemoved = RemoveWhere( so =>
			so is DebugTextSceneObject d &&
			d.component == comp
		) > 0;

		if ( existingRemoved )
			return false; // Removed

		// Otherwise:
		var so = new DebugTextSceneObject( Scene.SceneWorld )
		{
			component = comp,
			GameCamera = Scene.Camera
		};

		// Make it live till the death of sun! FTW
		Add( float.MaxValue, so );

		return true; // Added
	}

	internal void ScreenText( Vector3 position, TextRendering.Scope scope )
	{
		Add( 0.0f, new ScreenTextSceneObject( Scene.SceneWorld )
		{
			TextBlock = scope,
			ScreenPos = position,
		} );
	}

}

file class ScreenTextSceneObject : SceneCustomObject
{
	public Vector3 ScreenPos { get; set; }
	public TextRendering.Scope TextBlock;

	public ScreenTextSceneObject( SceneWorld sceneWorld ) : base( sceneWorld )
	{
		RenderLayer = SceneRenderLayer.OverlayWithoutDepth;
		Flags.CastShadows = false;
		TextBlock = TextRendering.Scope.Default;
	}

	static bool ToScreenWithDirection( Vector3 world, out Vector2 screen )
	{
		var frustum = Graphics.ViewFrustum;
		var behind = frustum.ScreenTransform( world, out var result );
		var x = (result.x + 1f) / 2f;
		var y = ((result.y * -1f) + 1f) / 2f;

		var size = Graphics.Viewport.Size;
		screen = new Vector2( x, y ) * size;

		return behind;
	}

	public override void RenderSceneObject()
	{
		if ( ToScreenWithDirection( ScreenPos, out var screen ) )
			return;

		var size = Graphics.Viewport.Size;
		screen -= size * 0.5f;
		var rect = new Rect( screen, size );
		Graphics.DrawText( rect, TextBlock );
	}
}

/// <summary>
/// Scene object that renders real-time debug text overlay for a component.
/// </summary>
/// <remarks>
/// Used for development debugging only. Displays component state as screen-space text.
/// </remarks>
public class DebugTextSceneObject : SceneCustomObject
{
	public TextRendering.Scope TextBlock;
	public Component component { get; set; }
	public CameraComponent GameCamera { get; set; }

	private CameraComponent _currentCamera;

	private UI.FontSmooth _displayFontSmooth;
	private Rendering.FilterMode _displayFilterMode;
	private string _displayFontName;
	private float _lastFontSize = -1f;
	private string _cachedText;
	private float _nextRefreshTime;

	private const float RefreshInterval = 0.10f;
	private const float MaxDistanceSquared = 5000f * 5000f;

	private DebugTypeInfo _meta;
	private Rect _cachedRect;
	private bool _layoutDirty;

	private readonly StringBuilder _sb = new( 256 );

	public DebugTextSceneObject( SceneWorld sceneWorld ) : base( sceneWorld )
	{
		RenderLayer = SceneRenderLayer.OverlayWithoutDepth;
		Flags.CastShadows = false;

		TextBlock = TextRendering.Scope.Default;
		InitTextBlock();
	}

	void InitTextBlock()
	{
		TextBlock.FontName = DebugOverlaySystem.DebugFontName;
		TextBlock.FontSize = DebugOverlaySystem.DebugFontSize;
		TextBlock.FilterMode = DebugOverlaySystem.DebugFilterMode;
		TextBlock.FontSmooth = DebugOverlaySystem.DebugFontSmooth;

		TextBlock.Outline.Enabled = true;
		TextBlock.Outline.Size = DebugOverlaySystem.DebugFontSize / 4f;
		TextBlock.Outline.Color = Color.Black;
	}

	public override void RenderSceneObject()
	{
		if ( component == null || !component.IsValid )
			return;

		_meta ??= DebugExposeMetadata.Get( component.GetType() );

		if ( _meta == null || !_meta.HasMembers )
			return;

		var newCamera =
			DebugOverlaySystem.Current.IsGameEjected
				? Application.Editor?.Camera
				: GameCamera;

		if ( newCamera != _currentCamera )
			_currentCamera = newCamera;

		if ( !ShouldRender() )
			return;

		UpdateCachedText();
		SyncTextStyle();

		if ( _layoutDirty )
		{
			if ( !ReferenceEquals( TextBlock.Text, _cachedText ) )
				TextBlock.Text = _cachedText;

			_cachedRect = new Rect( GetPixelPosition(), Vector2.Zero );

			_layoutDirty = false;
		}
		else
		{
			// Camera moves so this still needs to be updated
			_cachedRect = new Rect( GetPixelPosition(), Vector2.Zero );
		}

		Graphics.DrawText( _cachedRect, TextBlock, TextFlag.Left );
	}

	void UpdateCachedText()
	{
		if ( component == null || _meta == null )
			return;

		float now = Time.Now;

		if ( now < _nextRefreshTime )
			return;

		var newText = BuildDebugText( component, _meta );

		if ( !ReferenceEquals( newText, _cachedText ) )
		{
			_cachedText = newText;
			_layoutDirty = true;
		}

		_nextRefreshTime = now + RefreshInterval;
	}

	string BuildDebugText( Component comp, DebugTypeInfo meta )
	{
		if ( !meta.HasMembers )
			return null;

		_sb.Clear();

		_sb.Append( "Component: " )
		   .AppendLine( comp.GetType().Name );

		_sb.Append( "Pos: " )
		   .AppendLine( FormatVector3( comp.WorldPosition ).ToString() );

		_sb.AppendLine();

		string currentGroup = null;

		foreach ( var member in meta.Members )
		{
			object rawValue;

			try
			{
				rawValue = member.ReadValue( comp );
			}
			catch
			{
				continue;
			}

			bool empty =
				rawValue == null ||
				(rawValue is string s && string.IsNullOrWhiteSpace( s ));

			if ( member.HideIfEmpty && empty )
				continue;

			if ( currentGroup != member.Group )
			{
				currentGroup = member.Group;
				_sb.Append( '[' )
				   .Append( currentGroup )
				   .AppendLine( "]" );
			}

			string formatted;

			try
			{
				formatted = member.FormatValue( rawValue );
			}
			catch
			{
				formatted = rawValue?.ToString() ?? "null";
			}

			_sb.Append( member.Label )
			   .Append( ": " )
			   .AppendLine( formatted );
		}

		return _sb.ToString();
	}

	void SyncTextStyle()
	{
		bool dirty = false;

		var targetSmooth = DebugOverlaySystem.DebugFontSmooth;
		var targetFilter = DebugOverlaySystem.DebugFilterMode;
		var targetFont = DebugOverlaySystem.DebugFontName;
		float targetSize = DebugOverlaySystem.DebugFontSize;

		if ( _displayFontSmooth != targetSmooth )
		{
			_displayFontSmooth = targetSmooth;
			TextBlock.FontSmooth = targetSmooth;
			dirty = true;
		}

		if ( _displayFilterMode != targetFilter )
		{
			_displayFilterMode = targetFilter;
			TextBlock.FilterMode = targetFilter;
			dirty = true;
		}

		if ( _displayFontName != targetFont )
		{
			_displayFontName = targetFont;
			TextBlock.FontName = targetFont;
			dirty = true;
		}

		if ( !targetSize.AlmostEqual( _lastFontSize ) )
		{
			TextBlock.FontSize = targetSize;
			TextBlock.Outline.Size = targetSize / 4f;

			_lastFontSize = targetSize;
			dirty = true;
		}

		if ( dirty )
			_layoutDirty = true;
	}

	Vector2 GetPixelPosition()
	{
		if ( _currentCamera == null )
			return Vector2.Zero;

		var world = component.WorldPosition + Vector3.Up * 8f;

		var norm = _currentCamera.PointToScreenNormal( world );

		return new Vector2(
			norm.x * Graphics.Viewport.Size.x,
			norm.y * Graphics.Viewport.Size.y
		);
	}

	string FormatVector3( Vector3 v )
	{
		return FormattableString.Invariant(
			$"{v.x:F1}, {v.y:F1}, {v.z:F1}"
		);
	}

	bool ShouldRender()
	{
		if ( _currentCamera == null )
			return false;

		var camPos = _currentCamera.WorldPosition;
		var compPos = component.WorldPosition + Vector3.Up * 8f;

		if ( (compPos - camPos).LengthSquared > MaxDistanceSquared )
			return false;

		var frustum = _currentCamera.GetFrustum();
		return frustum.IsInside( compPos );
	}
}
