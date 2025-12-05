using System;

namespace Editor
{
	public enum ToolButtonStyle
	{
		IconOnly,
		TextOnly,
		TextBesideIcon,
		TextUnderIcon,
		Default
	}

	public class ToolBar : Widget
	{
		internal Native.QToolBar _toolbar;

		public readonly List<Option> _options = [];

		internal ToolBar( Native.QToolBar widget ) : base( false )
		{
			NativeInit( widget );
		}

		public ToolBar( Widget parent, string name = null ) : base( false )
		{
			Sandbox.InteropSystem.Alloc( this );
			NativeInit( CToolBar.Create( parent?._widget ?? default, this ) );

			if ( name is not null )
			{
				Name = name;
				Title = name;
			}
		}

		public string Title
		{
			get => this.WindowTitle;
			set => WindowTitle = value;
		}

		public bool Movable
		{
			get => _toolbar.isMovable();
			set => _toolbar.setMovable( value );
		}

		public bool Floatable
		{
			get => _toolbar.isFloatable();
			set => _toolbar.setFloatable( value );
		}

		internal override void NativeInit( IntPtr ptr )
		{
			_toolbar = ptr;
			_toolbar.allowContextMenu( false );

			base.NativeInit( ptr );
		}

		internal override void NativeShutdown()
		{
			base.NativeShutdown();

			_toolbar = default;
		}

		public Option AddOption( string text, string icon = null, Action action = null )
		{
			return AddOption( new Option( this, text, icon, action ) );
		}

		public Option AddOption( Option option )
		{
			option.SetParent( this );
			_toolbar.insertAction( default, option._action );
			_options.Add( option );

			return option;
		}

		public void Clear()
		{
			_toolbar.clear();
			_options.Clear();
		}

		public Option AddSeparator()
		{
			return new Option( _toolbar.addSeparator() );
		}

		public T AddWidget<T>( T widget ) where T : Widget
		{
			_toolbar.addWidget( widget._widget );
			return widget;
		}

		Vector2 _iconSize;

		public void SetIconSize( Vector2 size )
		{
			if ( _iconSize == size )
				return;

			_toolbar.setIconSize( size );
			_iconSize = size;
		}

		public ToolButtonStyle ButtonStyle
		{
			get => _toolbar.toolButtonStyle();
			set => _toolbar.setToolButtonStyle( value );
		}

		// Q_PROPERTY(bool movable READ isMovable WRITE setMovable NOTIFY movableChanged)
		// Q_PROPERTY(Qt::ToolBarAreas allowedAreas READ allowedAreas WRITE setAllowedAreas NOTIFY allowedAreasChanged)
		// Q_PROPERTY(Qt::Orientation orientation READ orientation WRITE setOrientation NOTIFY orientationChanged)
		// Q_PROPERTY(bool floating READ isFloating)
		// Q_PROPERTY(bool floatable READ isFloatable WRITE setFloatable)
	}
}
