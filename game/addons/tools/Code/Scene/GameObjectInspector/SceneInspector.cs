namespace Editor.Inspectors;

public interface ISceneInspectorExtension
{
	void Extend( Layout layout, SerializedObject sceneSo );
}

[Inspector( typeof( Scene ) )]
public class SceneInspector : InspectorWidget
{
	public static List<ISceneInspectorExtension> Extensions { get; } = [];

	private SceneInformation GetOrCreateInfo()
	{
		var scene = SerializedObject.Targets.OfType<Scene>().FirstOrDefault();
		if ( scene == null ) return null;

		var info = scene.GetAllComponents<SceneInformation>().FirstOrDefault();
		if ( info == null )
		{
			var go = scene.CreateObject();
			go.Name = "Scene Metadata";
			go.Tags.Add( "scene_metadata" );
			go.Flags |= GameObjectFlags.Hidden;
			info = go.Components.Create<SceneInformation>();
		}
		return info;
	}

	public SceneInspector( SerializedObject so ) : base( so )
	{
		var info = GetOrCreateInfo();
		if ( info == null ) return;

		var infoSo = EditorTypeLibrary.GetSerializedObject( info );

		var cs = new ControlSheet();
		cs.Margin = 8;
		cs.AddRow( so.GetProperty( nameof( Scene.TimeScale ) ) );
		cs.AddRow( so.GetProperty( nameof( Scene.WantsSystemScene ) ) );
		cs.AddRow( SerializedObject.GetProperty( nameof( Scene.PhysicsMode ) ) );

		var metaLabel = new Label( "Metadata" );
		metaLabel.SetStyles( "font-weight: bold; padding: 5px 8px;" );

		var csMeta = new ControlSheet();
		csMeta.Margin = 8;
		csMeta.AddRow( infoSo.GetProperty( nameof( SceneInformation.Title ) ) );
		csMeta.AddRow( infoSo.GetProperty( nameof( SceneInformation.SceneTags ) ) );

		Layout = Layout.Column();
		Layout.Add( cs );
		Layout.Add( metaLabel );
		Layout.Add( csMeta );

		foreach ( var ext in Extensions )
			ext.Extend( Layout, so );

		Layout.AddStretchCell();
	}
}
