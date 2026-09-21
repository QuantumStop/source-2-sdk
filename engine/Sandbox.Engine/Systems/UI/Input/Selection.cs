namespace Sandbox.UI;

internal class Selection
{
	Panel SelectionStart;
	Vector2 SelectionStartPos;
	Vector2 SelectionEndPos;

	public void UpdateSelection( Panel root, Panel hovered, bool dragging, bool started, bool ended, Vector2 pos )
	{
		if ( started )
		{
			SelectionStart = null;

			if ( hovered == null )
				return;

			if ( ScrollBar.Owns( hovered ) )
				return;

			ClearSelection();

			SelectionStart = hovered;
			SelectionStartPos = SelectionStart.ScreenPositionToPanelPosition( pos );
			SelectionEndPos = SelectionStartPos;
			return;
		}

		if ( SelectionStart == null )
			return;

		if ( dragging || ended )
		{
			var hash = HashCode.Combine( SelectionStart, SelectionStartPos, SelectionEndPos );

			SelectionEndPos = SelectionStart.ScreenPositionToPanelPosition( pos );
			var newHash = HashCode.Combine( SelectionStart, SelectionStartPos, SelectionEndPos );
			if ( newHash == hash ) return;

			SelectionEvent e = new SelectionEvent( "ondragselect", SelectionStart );
			e.StartPoint = SelectionStart.PanelPositionToScreenPosition( SelectionStartPos );
			e.EndPoint = SelectionStart.PanelPositionToScreenPosition( SelectionEndPos );
			e.SelectionRect = new Rect( e.StartPoint );
			e.SelectionRect = e.SelectionRect.AddPoint( e.EndPoint );

			SelectionStart.CreateEvent( e );
		}
	}

	void ClearSelection()
	{

	}
}
