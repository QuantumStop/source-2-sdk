namespace Editor;

/// <summary>
/// Abstract class to enable easily creating ControlWidgets with dropdowns.
/// </summary>
public abstract class DropdownControlWidget<T> : ControlWidget
{
	public override bool SupportsMultiEdit => true;

	protected PopupWidget _menu;
	ScrollArea _scroller;
	Widget _listCanvas;
	LineEdit _searchEdit;
	string _searchText = string.Empty;
	object[] _entries;

	public DropdownControlWidget( SerializedProperty property ) : base( property )
	{
		Cursor = CursorShape.Finger;
	}

	public struct Entry
	{
		public T Value { get; set; }
		public string Label { get; set; }
		public string Icon { get; set; }

		/// <summary>
		/// A picture in place of <see cref="Icon"/>, like a thumbnail - cut to fill the icon's square
		/// </summary>
		public Pixmap Image { get; set; }

		public string Description { get; set; }
	}

	protected abstract IEnumerable<object> GetDropdownValues();

	protected virtual bool EnableSearch => false;
	protected virtual string SearchPlaceholder => "Search...";
	protected virtual float SearchEntryHeight => Theme.ControlHeight + 8.0f;
	protected virtual Color SearchEntryBackground => Theme.ControlBackground;

	protected virtual string GetSearchText( object item )
	{
		if ( item is Entry entry )
		{
			return $"{entry.Label}\n{entry.Description}\n{entry.Value}";
		}

		return item?.ToString() ?? string.Empty;
	}

	/// <summary>
	/// Returns the display text shown in the dropdown button.
	/// </summary>
	protected virtual string GetDisplayText()
	{
		return SerializedProperty.GetValue<object>()?.ToString() ?? "None";
	}

	/// <summary>
	/// Called when a dropdown item is selected. Override for custom selection behavior.
	/// </summary>
	protected virtual void OnItemSelected( object item )
	{
		if ( item is Entry e )
			SerializedProperty.SetValue( e.Value );
		else
			SerializedProperty.SetValue( item );
	}

	protected override void PaintControl()
	{
		var color = IsControlHovered ? Theme.Blue : Theme.TextControl;
		var rect = new Rect( 0, 0, Width, Theme.RowHeight ).Shrink( 8, 0 );

		Paint.SetPen( color );
		Paint.SetDefaultFont();

		if ( SerializedProperty.IsMultipleDifferentValues )
		{
			Paint.SetPen( Theme.MultipleValues );
			Paint.DrawText( rect, "Multiple Values", TextFlag.LeftCenter );
		}
		else
		{
			Paint.DrawText( rect, GetDisplayText(), TextFlag.LeftCenter );
		}

		Paint.SetPen( color );
		Paint.DrawIcon( rect, "Arrow_Drop_Down", 17, TextFlag.RightCenter );
	}

	public override void StartEditing()
	{
		if ( !_menu.IsValid )
		{
			OpenMenu();
		}
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( e.LeftMouseButton && !_menu.IsValid() )
		{
			OpenMenu();
		}
	}

	protected override void OnDoubleClick( MouseEvent e )
	{
		// nothing
	}

	void OpenMenu()
	{
		_menu = new PopupWidget( null );

		_menu.Layout = Layout.Column();
		_menu.MinimumWidth = ScreenRect.Width;
		_menu.MaximumWidth = ScreenRect.Width;

		if ( EnableSearch )
		{
			var searchRow = _menu.Layout.AddRow();
			searchRow.Margin = 6;
			searchRow.Spacing = 6;
			searchRow.Add( new IconButton( "search" ) { Background = Color.Transparent, TransparentForMouseEvents = true, IconSize = 16 } );

			_searchEdit = new LineEdit { Text = _searchText ?? string.Empty };
			_searchEdit.MinimumHeight = SearchEntryHeight;
			_searchEdit.MaximumHeight = SearchEntryHeight;
			_searchEdit.SetStyles( $"background-color: {SearchEntryBackground.Hex};" );
			_searchEdit.PlaceholderText = SearchPlaceholder;
			_searchEdit.TextChanged += value =>
			{
				_searchText = value ?? string.Empty;
				RebuildList();
			};
			searchRow.Add( _searchEdit, 0 );
		}

		_scroller = _menu.Layout.Add( new ScrollArea( this ), 1 );
		_scroller.Canvas = new Widget( _scroller )
		{
			Layout = Layout.Column(),
			VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand,
			MaximumWidth = ScreenRect.Width
		};
		_listCanvas = _scroller.Canvas;

		_entries = GetDropdownValues().ToArray();
		RebuildList();

		_menu.Position = ScreenRect.BottomLeft;
		_menu.Visible = true;
		_menu.AdjustSize();
		_menu.ConstrainToScreen();
		_menu.OnPaintOverride = PaintMenuBackground;

		_searchEdit?.Focus();
	}

	void RebuildList()
	{
		if ( !_menu.IsValid() || !_listCanvas.IsValid() )
			return;

		_listCanvas.Layout.Clear( true );

		var query = (_searchText ?? string.Empty).Trim();
		var filtered = string.IsNullOrEmpty( query )
			? _entries
			: _entries.Where( x => GetSearchText( x ).Contains( query, global::System.StringComparison.OrdinalIgnoreCase ) ).ToArray();

		if ( filtered.Length == 0 )
		{
			var hint = _listCanvas.Layout.Add( new Label( "No results" ) );
			hint.SetStyles( "padding: 8px; opacity: 0.6;" );
			_listCanvas.AdjustSize();
			return;
		}

		foreach ( var o in filtered )
		{
			var b = _listCanvas.Layout.Add( new MenuOption<T>( o, SerializedProperty ) );
			b.MouseLeftPress = () =>
			{
				OnItemSelected( o );
				_menu.Update();
				_menu.Close();
			};
		}

		_listCanvas.AdjustSize();
		_menu.AdjustSize();
		_menu.ConstrainToScreen();
	}

	bool PaintMenuBackground()
	{
		Paint.SetBrushAndPen( Theme.SurfaceBackground );
		Paint.DrawRect( Paint.LocalRect, 0 );
		return true;
	}

}

file class MenuOption<T> : Widget
{
	object info;
	SerializedProperty property;

	public MenuOption( object e, SerializedProperty p ) : base( null )
	{
		info = e;
		property = p;

		Layout = Layout.Row();
		Layout.Margin = 8;

		if ( e is DropdownControlWidget<T>.Entry entry )
		{
			if ( entry.Image is { } image )
			{
				var picture = Layout.Add( new Widget( this ) { FixedSize = Theme.RowHeight, TransparentForMouseEvents = true } );
				picture.OnPaintOverride = () =>
				{
					// Cut to fill the square, the corners rounded
					var covered = image.Resize( picture.Size );
					var offset = (new Vector2( covered.Width, covered.Height ) - picture.Size) * 0.5f;

					Paint.Antialiasing = true;
					Paint.ClearPen();
					Paint.Translate( -offset );
					Paint.SetBrush( covered );
					Paint.DrawRect( new Rect( offset, picture.Size ), 3 );
					Paint.Translate( offset );
					return true;
				};
			}
			else if ( !string.IsNullOrWhiteSpace( entry.Icon ) )
			{
				Layout.Add( new IconButton( entry.Icon ) { Background = Color.Transparent, TransparentForMouseEvents = true, IconSize = 18 } );
			}

			Layout.AddSpacingCell( 8 );
			var c = Layout.AddColumn();
			var title = c.Add( new Label( entry.Label ) );
			title.SetStyles( "font-size: 12px; font-weight: bold; font-family: Poppins; color: white;" );

			if ( !string.IsNullOrWhiteSpace( entry.Description ) )
			{
				var desc = c.Add( new Label( entry.Description.Trim( '\n', '\r', '\t', ' ' ) ) );
				desc.WordWrap = true;
				desc.MinimumHeight = 1;
				desc.MinimumWidth = 400;
			}
		}
		else
		{
			Layout.AddSpacingCell( 8 );
			var c = Layout.AddColumn();
			var title = c.Add( new Label( e.ToString() ) );
			title.SetStyles( "font-size: 12px; font-weight: bold; font-family: Poppins; color: white;" );
		}
	}

	bool HasValue()
	{
		if ( property.IsMultipleDifferentValues ) return false;

		var value = property.GetValue<object>( default );
		if ( info is DropdownControlWidget<T>.Entry entry )
			return Equals( value, entry.Value );

		return value == info;
	}

	protected override void OnPaint()
	{
		if ( Paint.HasMouseOver || HasValue() )
		{
			Paint.SetBrushAndPen( Theme.Blue.WithAlpha( HasValue() ? 0.3f : 0.1f ) );
			Paint.DrawRect( LocalRect.Shrink( 2 ), 2 );
		}
	}
}

/// <summary>
/// A generic dropdown control widget. This is useful in circimstances where you want to
/// manually present a bunch of different options which aren't enums etc.
/// </summary>
sealed class DropdownControlWidget : DropdownControlWidget<object>
{
	public List<Entry> Entries { get; } = new();

	public DropdownControlWidget( SerializedProperty property ) : base( property )
	{
	}

	/// <summary>
	/// Clear the dropdown options
	/// </summary>
	public void ClearOptions()
	{
		Entries.Clear();
		Update();
	}

	/// <summary>
	/// Add an option to the dropdown options
	/// </summary>
	public void AddOption( object value, string label, string description = null, string icon = null )
	{
		Entries.Add( new DropdownControlWidget<object>.Entry { Value = value, Label = label, Description = description, Icon = icon } );
		Update();
	}

	protected override IEnumerable<object> GetDropdownValues()
	{
		foreach ( var entry in Entries )
		{
			yield return entry;
		}
	}
}
