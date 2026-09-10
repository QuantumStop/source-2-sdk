namespace Sandbox.UI.Dev;

using Sandbox;
using System;
using System.Collections.Generic;

public sealed class DevScrollBar : Panel
{
	static Panel HookedRoot;
	static readonly HashSet<DevScrollBar> ActiveDrags = new();

	public enum ScrollAxis
	{
		Vertical,
		Horizontal
	}

	public ScrollAxis Axis { get; set; } = ScrollAxis.Vertical;

	public float Minimum { get; set; } = 0f;
	public float Maximum { get; set; } = 0f;
	public float PageStep { get; set; } = 100f;

	float _value;
	public float Value
	{
		get => _value;
		set
		{
			var clamped = value.Clamp( Minimum, Maximum );
			if ( MathF.Abs( clamped - _value ) < 0.001f ) return;
			_value = clamped;
			OnValueChanged?.Invoke( _value );
		}
	}

	public Action<float> OnValueChanged { get; set; }

	Panel Track;
	Panel Thumb;

	bool Dragging;
	float DragGrabOffset;

	public bool IsDragging => Dragging;

	public DevScrollBar()
	{
		AddClass( "devscrollbar" );

		Track = Add.Panel( "track" );
		Thumb = Track.Add.Panel( "thumb" );
		Track.Style.Position = PositionMode.Relative;
		Thumb.Style.Position = PositionMode.Absolute;

		Track.AddEventListener( "onmousedown", OnTrackDown );
		Thumb.AddEventListener( "onmousedown", OnThumbDown );

		// Fallback mouse-up handlers
		AddEventListener( "onmouseup", OnAnyMouseUp );
		Track.AddEventListener( "onmouseup", OnAnyMouseUp );
		Thumb.AddEventListener( "onmouseup", OnAnyMouseUp );
	}

	public override void Tick()
	{
		base.Tick();
		LayoutThumb();

		if ( Dragging )
		{
			UpdateDrag();
		}
	}

	void EnsureRootHook()
	{
		var root = FindRootPanel();
		if ( root is null )
			return;

		// Hook per-root (not once globally). UI can have multiple roots (viewport, editor panels, previews).
		if ( root != HookedRoot )
		{
			root.AddEventListener( "onmouseup", StopAllDrags );
			HookedRoot = root;
		}
	}

	static void StopAllDrags()
	{
		// Copy to avoid modifying the set while iterating.
		foreach ( var sb in ActiveDrags.ToArray() )
		{
			sb.StopDrag();
		}

		ActiveDrags.Clear();
	}

	void StopDrag()
	{
		Dragging = false;
		Thumb?.RemoveClass( "active" );
	}

	void OnAnyMouseUp( PanelEvent e )
	{
		if ( !Dragging )
			return;

		if ( e is not MousePanelEvent me )
			return;

		if ( me.Button != "mouseleft" )
			return;

		StopDrag();
		ActiveDrags.Remove( this );
		e.StopPropagation();
	}

	void LayoutThumb()
	{
		var trackSize = Axis == ScrollAxis.Vertical ? Track.Box.Rect.Height : Track.Box.Rect.Width;
		if ( trackSize <= 0 )
			return;

		var range = MathF.Max( 0, Maximum - Minimum );
		var view = MathF.Max( 1, PageStep );

		// If there's no scroll range, make thumb fill the track.
		var thumbSize = range <= 0 ? trackSize : (view / (view + range)) * trackSize;
		thumbSize = MathF.Max( 16f, MathF.Min( trackSize, thumbSize ) );

		var maxThumbPos = MathF.Max( 0, trackSize - thumbSize );
		var t = range <= 0 ? 0 : (Value - Minimum) / range;
		var thumbPos = (t * maxThumbPos).Clamp( 0, maxThumbPos );

		if ( Axis == ScrollAxis.Vertical )
		{
			Thumb.Style.Left = 0;
			Thumb.Style.Top = thumbPos;
			Thumb.Style.Right = 0;
			Thumb.Style.Height = thumbSize;
			Thumb.Style.Width = Length.Percent( 100 );
		}
		else
		{
			Thumb.Style.Top = 0;
			Thumb.Style.Left = thumbPos;
			Thumb.Style.Bottom = 0;
			Thumb.Style.Width = thumbSize;
			Thumb.Style.Height = Length.Percent( 100 );
		}

		Thumb.Style.Dirty();

		SetClass( "hidden", range <= 0.5f );
	}

	void OnThumbDown( PanelEvent e )
	{
		if ( e is not MousePanelEvent )
			return;

		EnsureRootHook();
		ActiveDrags.Add( this );

		Dragging = true;
		Thumb.AddClass( "active" );

		var local = Track.ScreenPositionToPanelPosition( Mouse.Position );
		var thumbPos = Axis == ScrollAxis.Vertical ? (Thumb.Style.Top?.Value ?? 0f) : (Thumb.Style.Left?.Value ?? 0f);
		DragGrabOffset = (Axis == ScrollAxis.Vertical ? local.y : local.x) - thumbPos;

		e.StopPropagation();
	}

	void OnTrackDown( PanelEvent e )
	{
		if ( e is not MousePanelEvent me )
			return;

		// Ignore thumb: it has its own handler.
		if ( me.Target == Thumb || Thumb.HasHovered )
			return;

		var local = me.LocalPosition;
		var thumbPos = Axis == ScrollAxis.Vertical ? (Thumb.Style.Top?.Value ?? 0f) : (Thumb.Style.Left?.Value ?? 0f);
		var thumbSize = Axis == ScrollAxis.Vertical ? (Thumb.Style.Height?.Value ?? 0f) : (Thumb.Style.Width?.Value ?? 0f);

		var click = Axis == ScrollAxis.Vertical ? local.y : local.x;
		if ( click < thumbPos )
			Value -= PageStep;
		else if ( click > thumbPos + thumbSize )
			Value += PageStep;

		e.StopPropagation();
	}

	void UpdateDrag()
	{
		// If mouse-up happens outside this panel, root hook will stop the drag.
		if ( !Dragging )
			return;

		var trackSize = Axis == ScrollAxis.Vertical ? Track.Box.Rect.Height : Track.Box.Rect.Width;
		var thumbSize = Axis == ScrollAxis.Vertical ? (Thumb.Style.Height?.Value ?? 16f) : (Thumb.Style.Width?.Value ?? 16f);
		var maxThumbPos = MathF.Max( 0, trackSize - thumbSize );
		if ( maxThumbPos <= 0 )
			return;

		var local = Track.ScreenPositionToPanelPosition( Mouse.Position );
		var desiredThumbPos = (Axis == ScrollAxis.Vertical ? local.y : local.x) - DragGrabOffset;
		desiredThumbPos = desiredThumbPos.Clamp( 0, maxThumbPos );

		var range = MathF.Max( 0, Maximum - Minimum );
		var t = maxThumbPos > 0 ? desiredThumbPos / maxThumbPos : 0f;
		Value = Minimum + t * range;
	}
}
