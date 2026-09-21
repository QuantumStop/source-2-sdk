namespace Sandbox.UI.Dev;

using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

[Library( "console" )]
public class Console : Panel
{
	const int MaxBufferedEvents = 2000;
	static readonly List<LogEvent> BufferedEvents = [];
	static readonly List<Console> Instances = [];
	static bool Hooked;

	internal static void Hook()
	{
		if ( Hooked )
			return;

		Hooked = true;
		Sandbox.Diagnostics.Logging.OnMessage += OnConsoleMessage;
	}

	static void OnConsoleMessage( LogEvent e )
	{
		if ( e.Message.Contains( '\n' ) || e.Message.Contains( '\r' ) )
		{
			var parts = e.Message.Split( ['\n', '\r'], StringSplitOptions.RemoveEmptyEntries );
			foreach ( var part in parts )
			{
				var ee = e;
				ee.Message = part;
				OnConsoleMessage( ee );
			}

			return;
		}

		AddBufferedEvent( e );

		foreach ( var instance in Instances.ToArray() )
		{
			if ( instance.IsValid() )
				instance.AddEvent( e );
		}
	}

	static void AddBufferedEvent( LogEvent e )
	{
		BufferedEvents.Add( e );

		if ( BufferedEvents.Count > MaxBufferedEvents )
			BufferedEvents.RemoveRange( 0, BufferedEvents.Count - MaxBufferedEvents );
	}

	internal List<LogEvent> Entries = [];
	internal ConsoleVirtualList Output;
	internal TextEntry Input;
//	internal TextEntry Filter; // kept for later, hidden for now

	LogEventPanel logEventPanel;
	Panel InputBar;
	int MaxVisibleChars;
	float LastContentWidthHint = -1;

	struct MessageCategory
	{
		public Button Button;
		public int Count;
		public bool Disabled;

		public void Toggle()
		{
			Disabled = !Disabled;
			if ( Button.IsValid() )
				Button.SetClass( "disabled", Disabled );
		}

		public void Clear()
		{
			Count = 0;

			if ( Button.IsValid() )
				Button.Text = "0";
		}
	}

	MessageCategory Message;
	MessageCategory Warning;
	MessageCategory Error;

	public int MessageCount => Message.Count;
	public int WarningCount => Warning.Count;
	public int ErrorCount => Error.Count;

	public bool MessagesDisabled => Message.Disabled;
	public bool WarningsDisabled => Warning.Disabled;
	public bool ErrorsDisabled => Error.Disabled;

	public void ToggleMessages() { Message.Toggle(); RebuildVisible(); }
	public void ToggleWarnings() { Warning.Toggle(); RebuildVisible(); }
	public void ToggleErrors() { Error.Toggle(); RebuildVisible(); }

	public Console()
	{
		CanDragScroll = false;

		Output = AddChild<ConsoleVirtualList>();
		Output.AddClass( "console_output" );
		Output.CanDragScroll = false;
		Output.ItemHeight = 19;
		Output.PreferScrollToBottom = true;
		Output.OnCreateCell = CreateOutputCell;
		Output.OnBindCell = BindOutputCell;

		logEventPanel = AddChild<LogEventPanel>();

		InputBar = Add.Panel( "inputbar" );
		InputBar.AddEventListener( "onmousedown", OnInputBarMouseDown );

		Input = InputBar.AddChild<TextEntry>();		
		Input.HistoryCookie = "console-input-history";
		Input.AddClass( "input" );
		Input.AutoComplete = FillAutoComplete;
		Input.AddEventListener( "onsubmit", OnSubmit );

		// Filtering is currently disabled
		// Filter = InputBar.AddChild<TextEntry>();
		// Filter.AddClass( "filter" );
		// Filter.Placeholder = "Filter..";
		// Filter.AddEventListener( "onchange", OnFilter );

		Hook();
		Instances.Add( this );

		foreach ( var entry in BufferedEvents )
		{
			AddEvent( entry );
		}

		Output.AcceptsFocus = true;
		Output.AllowChildSelection = false;
		AllowChildSelection = false;
	}

	void OnInputBarMouseDown( PanelEvent e )
	{
		if ( e is not MousePanelEvent me )
			return;

		if ( me.Button != "mouseleft" )
			return;

		// Only handle clicks on the empty area of the input bar, let the TextEntry handle direct clicks.
		if ( me.Target != InputBar )
			return;

		Input?.Focus();

		// Clicking outside the rendered text should place the caret at the end.
		Input?.CaretPosition = Input.TextLength;

		e.StopPropagation();
	}

	void CreateOutputCell( Panel cell, object data )
	{
		var row = cell.AddChild<ConsoleRow>();
		row.OnEntryClicked = logEventPanel.Switch;
		row.OnRowMouseDown = ClearOutputSelection;
		cell.UserData = row;
	}

	void BindOutputCell( Panel cell, object data )
	{
		var row = cell.UserData as ConsoleRow;
		if ( row is null )
			return;

		row.SetLogEvent( (LogEvent)data );
	}

	public override void OnDeleted()
	{
		base.OnDeleted();

		Instances.Remove( this );
	}

	void AddEvent( LogEvent e )
	{
		Entries.Add( e );

		if ( ShouldShowEvent( e ) )
		{
			Output.AddItem( e );
			MaxVisibleChars = Math.Max( MaxVisibleChars, EstimateContentChars( e ) );
		}

		UpdateScrollSizes();

		if ( e.Level == LogLevel.Info || e.Level == LogLevel.Trace )
		{
			Message.Count++;
			if ( Message.Button.IsValid() )
			{
				Message.Button.Text = $"{Message.Count:n0}";
			}
		}

		if ( e.Level == LogLevel.Warn )
		{
			Warning.Count++;
			if ( Warning.Button.IsValid() )
			{
				Warning.Button.Text = $"{Warning.Count:n0}";
			}
		}

		if ( e.Level == LogLevel.Error )
		{
			Error.Count++;
			if ( Error.Button.IsValid() )
			{
				Error.Button.Text = $"{Error.Count:n0}";
			}
		}
	}

	internal void CreateLevelToggles( Panel parent )
	{
		if ( parent is null )
			return;

		// These buttons live in the DevWindow tab strip. If the tab strip is rebuilt (i.e hotload),
		// the old button panels can be deleted while our references remain. Recreate when needed.
		var needsRecreate =
			!Error.Button.IsValid() || Error.Button.Parent != parent ||
			!Warning.Button.IsValid() || Warning.Button.Parent != parent ||
			!Message.Button.IsValid() || Message.Button.Parent != parent;

		if ( !needsRecreate )
			return;

		Error.Button?.Delete( true );
		Warning.Button?.Delete( true );
		Message.Button?.Delete( true );

		Error.Button = null;
		Warning.Button = null;
		Message.Button = null;

		Error.Button = parent.AddChild( new Button( $"{Error.Count:n0}", null, "type err", null ) );
		Error.Button.AddEventListener( "onclick", () => ToggleErrors() );
		Error.Button.SetClass( "disabled", Error.Disabled );

		Warning.Button = parent.AddChild( new Button( $"{Warning.Count:n0}", null, "type wrn", null ) );
		Warning.Button.AddEventListener( "onclick", () => ToggleWarnings() );
		Warning.Button.SetClass( "disabled", Warning.Disabled );

		Message.Button = parent.AddChild( new Button( $"{Message.Count:n0}", null, "type msg", null ) );
		Message.Button.AddEventListener( "onclick", () => ToggleMessages() );
		Message.Button.SetClass( "disabled", Message.Disabled );
	}

	void OnFilter()
	{
		Output.SetItems( Entries.Where( x => ShouldShowEvent( x ) ).Select( x => x as object ) );
		RecalculateVisibleContentWidth();
		UpdateScrollSizes();
	}

	void RebuildVisible()
	{
		OnFilter();
	}

	bool ShouldShowEvent( LogEvent e )
	{
		if ( e.Level == LogLevel.Error && Error.Disabled ) return false;
		if ( e.Level == LogLevel.Warn && Warning.Disabled ) return false;
		if ( e.Level == LogLevel.Info && Message.Disabled ) return false;
		if ( e.Level == LogLevel.Trace && Message.Disabled ) return false;

		// if ( Filter is null || string.IsNullOrWhiteSpace( Filter.Text ) )
		// 	return true;

		return true;

		// return e.Message.Contains( Filter.Text, StringComparison.OrdinalIgnoreCase );
	}

	public override void Tick()
	{
		base.Tick();
	}

	void OnClear()
	{
		Output.Clear();
		Entries.Clear();
		MaxVisibleChars = 0;
		LastContentWidthHint = -1;

		Message.Clear();
		Warning.Clear();
		Error.Clear();
	}

	void OpenLogsFolder()
	{
		// John: Depreacting this for now, since I thought it's not very useful.
		// Can return it back if needed.
		// MenuUtility.OpenFolder( Environment.CurrentDirectory + "/logs/" );
	}

	void OutputLine( string line )
	{
		var e = new LogEvent() { Message = line, Level = LogLevel.Info, Logger = "in", Time = DateTime.Now };
		Entries.Add( e );
		Output.AddItem( e );
		MaxVisibleChars = Math.Max( MaxVisibleChars, EstimateContentChars( e ) );
		UpdateScrollSizes();

		// Don't throw exceptions through UI event processing for bad/unknown commands.
		DevConsoleAccess.Run( line, allowProtected: true );
	}

	void OnSubmit()
	{
		var t = Input.Text;
		if ( string.IsNullOrWhiteSpace( t ) )
		{
			Input.Text = "";
			return;
		}

		if ( t == "clear" )
		{
			OnClear();
		}
		else
		{
			if ( t.Contains( '\n' ) || t.Contains( '\r' ) )
			{
				var parts = t.Split( ['\n', '\r'], StringSplitOptions.RemoveEmptyEntries );
				foreach ( var part in parts )
				{
					OutputLine( part );
				}
			}
			else
			{
				OutputLine( t );
			}
		}

		Output.TryScrollToBottom();

		Input.Text = "";
		Input.AddToHistory( t );
		Input.DestroyAutoComplete();
		Input.Focus();
	}

	private object[] FillAutoComplete( string arg )
	{
		if ( string.IsNullOrWhiteSpace( arg ) )
			return [];

		if ( arg.Trim().Length < 2 )
			return [];

		return [.. ConVarSystem.GetAutoComplete( arg, 20 )
			.Select( x => (object)new TextEntry.AutocompleteEntry
			{
				Title = $"{x.Command} - {x.Description}".Trim( '-', ' ' ),
				Value = x.Command
			} )];
	}

	void UpdateScrollSizes()
	{
		if ( Output is null )
			return;

		const float estimatedCharWidth = 6.6f;
		var estimatedWidth = (MaxVisibleChars * estimatedCharWidth) + 64f;
		var contentWidthHint = MathF.Max( Output.Box.Rect.Width * Output.ScaleFromScreen, estimatedWidth );

		if ( MathF.Abs( contentWidthHint - LastContentWidthHint ) < 0.5f )
			return;

		LastContentWidthHint = contentWidthHint;
		Output.ContentWidthHint = contentWidthHint;
		Output.NeedsRebuild = true;
	}

	void RecalculateVisibleContentWidth()
	{
		MaxVisibleChars = 0;

		foreach ( var e in Entries )
		{
			if ( !ShouldShowEvent( e ) ) continue;
			MaxVisibleChars = Math.Max( MaxVisibleChars, EstimateContentChars( e ) );
		}
	}

	static int EstimateContentChars( LogEvent e )
	{
		var logger = e.Logger;
		var messageLength = e.Message?.Length ?? 0;

		if ( e.Logger == "in" )
			return messageLength + 2;

		if ( !string.IsNullOrWhiteSpace( logger ) && logger != "Generic" )
			return (logger.Length + 3) + messageLength; // [x] + space

		return messageLength;
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );

		ClearOutputSelection();
	}

	protected override void OnDragSelect( SelectionEvent e )
	{
		e.StopPropagation();

		foreach ( var row in Output.VisibleRows )
		{
			row.UpdateTextSelection( e );
		}
	}

	public override void OnButtonTyped( ButtonEvent e )
	{
		if ( e.Button == "a" && e.HasCtrl )
		{
			e.StopPropagation = true;

			foreach ( var row in Output.VisibleRows )
			{
				row.SelectAllText();
			}

			return;
		}

		base.OnButtonTyped( e );
	}

	public override string GetClipboardValue( bool cut )
	{
		var text = GetSelectedOutputText();
		return string.IsNullOrEmpty( text ) ? base.GetClipboardValue( cut ) : text;
	}

	void ClearOutputSelection()
	{
		foreach ( var row in Output.VisibleRows )
		{
			row.ClearTextSelection();
		}
	}

	string GetSelectedOutputText()
	{
		var lines = Output.VisibleRows
			.Select( x => x.GetSelectedText() )
			.Where( x => !string.IsNullOrEmpty( x ) );

		return string.Join( "\n", lines );
	}

}

public class ConsoleVirtualList : VirtualList
{
	public float ContentWidthHint { get; set; }
	float LastScrollX = float.MinValue;

	internal IEnumerable<ConsoleRow> VisibleRows
	{
		get
		{
			foreach ( var (_, child) in _created.OrderBy( x => x.Key ) )
			{
				if ( child.IsVisible && child.UserData is ConsoleRow row )
					yield return row;
			}
		}
	}

	protected override bool UpdateLayout()
	{
		var changed = base.UpdateLayout();
		var scrollX = ScrollOffset.x * ScaleFromScreen;

		if ( MathF.Abs( scrollX - LastScrollX ) > 0.1f )
		{
			LastScrollX = scrollX;
			return true;
		}

		return changed;
	}

	protected override void PositionPanel( int index, Panel panel )
	{
		base.PositionPanel( index, panel );

		if ( ContentWidthHint > 0 )
		{
			panel.Style.Width = MathF.Max( Box.Rect.Width * ScaleFromScreen, ContentWidthHint );
		}

		if ( panel.UserData is ConsoleRow row )
		{
			row.UpdateHorizontalWindow(
				ScrollOffset.x * ScaleFromScreen,
				Box.Rect.Width * ScaleFromScreen,
				ConsoleRow.EstimatedCharWidth );
		}
	}

	protected override float GetTotalWidth( int itemCount )
	{
		return MathF.Max( base.GetTotalWidth( itemCount ), ContentWidthHint );
	}
}
