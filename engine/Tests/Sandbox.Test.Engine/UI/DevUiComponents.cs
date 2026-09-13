using Microsoft.AspNetCore.Components;
using Sandbox.UI;
using Sandbox.UI.Dev;

namespace UITests;

[TestClass]
[DoNotParallelize]
public class DevUiComponents
{
	[TestMethod]
	public void DevColumnsRendersNamedFragments()
	{
		RenderFragment left = builder =>
		{
			builder.OpenElement<Panel>( 0 );
			builder.AddAttribute( 1, "class", "left-probe" );
			builder.CloseElement();
		};

		RenderFragment right = builder =>
		{
			builder.OpenElement<Panel>( 0 );
			builder.AddAttribute( 1, "class", "right-probe" );
			builder.CloseElement();
		};

		var panel = new DevColumns
		{
			Left = left,
			Right = right
		};

		panel.TickInternal();

		Assert.IsNotNull( panel.Descendants.FirstOrDefault( x => x.HasClass( "devcolumns-col" ) && x.HasClass( "left" ) ) );
		Assert.IsNotNull( panel.Descendants.FirstOrDefault( x => x.HasClass( "devcolumns-col" ) && x.HasClass( "right" ) ) );
		Assert.IsNotNull( panel.Descendants.FirstOrDefault( x => x.HasClass( "left-probe" ) ) );
		Assert.IsNotNull( panel.Descendants.FirstOrDefault( x => x.HasClass( "right-probe" ) ) );
	}
}
