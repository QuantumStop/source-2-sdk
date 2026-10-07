using Native;
using System;

namespace Editor;

/// <summary>
/// Registers a widget with the input system, so it uses SDL.
/// </summary>
public static class GameMode
{
	static SceneRenderingWidget _inPlay;
	static IntPtr _playWindow;
	internal static IntPtr PlayWindow { get; private set; }
	internal static SceneRenderingWidget PlayWidget => _inPlay.IsValid() ? _inPlay : null;

	/// <summary>
	/// Is a render widget the active play widget
	/// </summary>
	internal static bool IsPlayWidget( SceneRenderingWidget widget ) => widget == _inPlay;

	/// <summary>
	/// Given a widget, register it for SDL input, and tell the engine this is the swapchain we have
	/// </summary>
	/// <param name="widget"></param>
	public static void SetPlayWidget( SceneRenderingWidget widget )
	{
		if ( _inPlay == widget ) return;

		ClearPlayMode();

		// Blur before registering so SDL's fresh wrapper can't snapshot this widget as its
		// keyboard focus window - relative mouse mode is driven from the main editor window
		widget.Blur();

		widget.Focused += WidgetFocused;
		widget.Blurred += WidgetBlurred;
		widget.MouseTracking = true;
		widget.MouseMove += OnPlayWidgetMouseMove;

		_playWindow = widget._widget.winId();
		NativeEngine.InputSystem.RegisterWindowWithSDL( _playWindow );
		PlayWindow = NativeEngine.GameWindowNative.FromNativeHandle( _playWindow );
		NativeEngine.GameWindowNative.SetRenderTarget( PlayWindow, widget.SwapChain );

		// The play widget is where the game renders, so make it the main window: flip the existing
		// m_bIsMainWindow flag so GetGPUFrameTimeMS reports the running game's GPU frame time.
		g_pRenderDevice.SetSwapChainIsMainWindow( widget.SwapChain, true );

		_focusWindowId = renderWindowId;
		_inPlay = widget;

		// Starting play through automation must not activate a background or minimized editor.
		widget.Focus( activateWindow: false );
	}

	/// <summary>
	/// Releases the active play widget and its borrowed input and render targets.
	/// </summary>
	public static void ClearPlayMode()
	{
		UnregisterCurrent();

		if ( _inPlay is null )
			return;

		var widget = _inPlay;
		_inPlay = null;

		widget.Focused -= WidgetFocused;
		widget.Blurred -= WidgetBlurred;
		widget.MouseMove -= OnPlayWidgetMouseMove;
		if ( widget.IsValid() )
		{
			widget.Blur();
			widget.MouseTracking = false;
		}

		// Teardown also runs after Qt destroys the widget, when winId() is no longer safe.
		Sandbox.Engine.WindowInput.OnEditorGameFocusChange( _playWindow, false );
		NativeEngine.GameWindowNative.SetRenderTarget( IntPtr.Zero, default );
		NativeEngine.InputSystem.UnregisterWindowFromSDL( _playWindow );
		_playWindow = default;
		PlayWindow = default;

		g_pRenderDevice.SetSwapChainIsMainWindow( widget.SwapChain, false );
	}

	/// <summary>
	/// When the editor gains focus of the game widget, tell the input system so it'll mouse capture (if it wants to)
	/// </summary>
	private static void WidgetFocused( FocusChangeReason reason )
	{
		if ( _focusWindowId == 0 )
			return;

		Sandbox.Engine.WindowInput.OnEditorGameFocusChange( _playWindow, true );
	}

	/// <summary>
	/// When the editor loses focus of the game widget, tell the input system so it stops trying to do mouse capture.
	/// </summary>
	private static void WidgetBlurred( FocusChangeReason reason )
	{
		if ( _focusWindowId == 0 )
			return;

		Sandbox.Engine.WindowInput.OnEditorGameFocusChange( _playWindow, false );
	}

	static void UnregisterCurrent()
	{
		if ( _inPlay.IsValid() )
		{
			_inPlay.Blur();
		}

		if ( _focusSource.IsValid() )
		{
			_focusSource.Focused -= WidgetFocused;
			_focusSource.Blurred -= WidgetBlurred;
		}

		if ( _focusWindowId != 0 )
		{
			NativeEngine.InputSystem.OnEditorGameFocusChange( _focusWindowId, false );
		}

		if ( _registeredRenderWindowId != 0 )
		{
			NativeEngine.InputSystem.UnregisterWindowFromSDL( _registeredRenderWindowId );
		}

		if ( _ownsHostWindowRegistration && _registeredHostWindowId != 0 )
		{
			NativeEngine.InputSystem.UnregisterWindowFromSDL( _registeredHostWindowId );
		}

		if ( _switchedEditorMainWindow && _editorMainWindowId != 0 )
		{
			NativeEngine.InputSystem.SetEditorMainWindow( _editorMainWindowId );
		}

		_inPlay = null;
		_focusSource = null;
		_registeredHostWindowId = 0;
		_registeredRenderWindowId = 0;
		_focusWindowId = 0;
		_editorMainWindowId = 0;
		_switchedEditorMainWindow = false;
		_ownsHostWindowRegistration = false;
	}

	static bool IsEditorMainWindow( nint windowId )
	{
		var editorMainId = GetEditorMainWindowId();
		return editorMainId != 0 && editorMainId == windowId;
	}

	static nint GetEditorMainWindowId()
	{
		var editorWindow = Sandbox.Internal.GlobalToolsNamespace.EditorWindow;
		if ( !editorWindow.IsValid() )
			return 0;

		return (nint)editorWindow._widget.winId();
	}
}
