namespace Editor;

using Editor;
using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Reflection;

public static partial class EditorToolBars
{
	public enum ToolBarOptionGroupType
	{
		None,						// Independent toggle
		SingleExclusive,			// Only one active in this group, cannot unselect
		SingleToggleable,			// Only one active, but can click again to unselect
		ConditionalPreserveState,   // Child disabled but state preserved
		ConditionalClearState,      // Child disabled and gets reset
		ExternallyControlled        // Disabled unless explicitly activated by code
	}

	public class ToolOptionDef
	{
		public string Name;
		public string Description;				// Optional

		public Option Widget;					// Stores the Option created in toolbar

		public string Icon;
		public string ToggledIcon;              // Optional
		public List<string> IconCycle;          // Optional cycle of icons

		public int CurrentIconIndex = 0;		// Index in IconCycle
		public string OverrideIcon;				// Externally forced icon

		public string Hotkey;					// Optional

		public bool Checkable = false;
		public bool Separator = false;
		public bool Active = false;				// Current active state

		public string Group = null;
		public string ConditionalOn = null;     // Only used if GroupType == Conditional

		public bool ExternalEnabled = false;

		public string ShortcutAction; // e.g. "mesh.vertex"

		public ToolBarOptionGroupType GroupType = ToolBarOptionGroupType.None;
	}

	/// <summary>
	/// Adds a collection of tool option definitions to the specified toolbar, configuring their selection behavior and
	/// initial state.
	/// </summary>
	/// <remarks>If single-selection mode is enabled, activating one option will automatically deactivate all
	/// others. Options marked as separators in the definitions will be added as separators to the toolbar. The method also
	/// supports group-based exclusive selection if defined in the option definitions.</remarks>
	/// <param name="bar">The toolbar to which the tool options and separators will be added.</param>
	/// <param name="defs">A list of tool option definitions that specify the options and separators to add to the toolbar.</param>
	/// <param name="singleSelect">true to enable single-selection mode, where only one option can be active at a time; otherwise, false to allow
	/// multiple options to be active.</param>
	public static void AddDefs( ToolBar bar, List<ToolOptionDef> defs, bool singleSelect = false )
	{
		foreach ( var def in defs )
		{
			if ( def.Separator )
			{
				bar.AddSeparator();
				continue;
			}

			Option option = null;

			void callback()
			{
				// GROUP LOGIC
				if ( def.GroupType == ToolBarOptionGroupType.SingleExclusive && !string.IsNullOrEmpty( def.Group ) )
				{
					foreach ( var d in defs )
					{
						if ( d.Group != def.Group || d == def )
							continue;

						d.Active = false;
						if ( d.Widget != null )
						{
							d.Widget.Checked = false;
							d.Widget.Icon = d.Icon;
						}
					}

					def.Active = true;
					option.Checked = true;
					UpdateOptionIcon( def ); // call your existing helper
				}
				else if ( singleSelect )
				{
					// Single-selection toolbar mode
					foreach ( var d in defs )
					{
						if ( d.Widget == null ) continue;

						if ( d == def )
						{
							d.Active = true;
							d.Widget.Checked = true;
						}
						else
						{
							d.Active = false;
							d.Widget.Checked = false;
						}

						// Always update icon for all buttons
						if ( d.Widget != null )
						{
							if ( d == def )
								UpdateOptionIcon( d );
							else
								d.Widget.Icon = d.Icon;
						}
					}
				}
				else
				{
					if ( def.IconCycle != null && def.IconCycle.Count > 0 )
					{
						def.CurrentIconIndex = (def.CurrentIconIndex + 1) % def.IconCycle.Count;
						UpdateOptionIcon( def );
					}
					else
					{
						// Multi-select toggle
						def.Active = !def.Active;
						option.Checked = def.Active;
						UpdateOptionIcon( def );
					}
				}

				HandleSpecialLogic( defs, def );

				// Here, fishy-fishy!
				ExecuteShortcutAction( def );
			}

			// Add the option to the toolbar
			option = bar.AddOption( def.Name, def.Active ? def.ToggledIcon ?? def.Icon : def.Icon, callback );
			option.Checkable = def.Checkable;
			option.ToolTip = !string.IsNullOrWhiteSpace( def.Hotkey )
				? $"{def.Name} [{def.Hotkey}]"
				: def.Name;

			def.Widget = option;

			UpdateOptionIcon( def );
		}
	}

	/// <summary>
	/// Logic specific to certain toolbars.
	/// </summary>
	private static void HandleSpecialLogic( List<ToolOptionDef> defs, ToolOptionDef activated )
	{
		foreach ( var def in defs )
		{
			// CONDITIONAL LOGIC (Preserve or Reset)
			if ( (def.GroupType == ToolBarOptionGroupType.ConditionalPreserveState ||
				  def.GroupType == ToolBarOptionGroupType.ConditionalClearState)
				 && !string.IsNullOrEmpty( def.ConditionalOn ) )
			{
				// Find parent
				var parent = defs.Find( d => d.Name == def.ConditionalOn );
				bool parentActive = parent?.Active ?? false;

				if ( def.Widget != null )
				{
					def.Widget.Enabled = parentActive;

					if ( !parentActive )
					{
						if ( def.GroupType == ToolBarOptionGroupType.ConditionalClearState )
						{
							// HARD CONDITIONAL — reset state
							def.Active = false;
							def.Widget.Checked = false;
							def.Widget.Icon = def.Icon;
						}
					}
				}
			}

			// EXTERNALLY CONTROLLED
			if ( def.GroupType == ToolBarOptionGroupType.ExternallyControlled )
			{
				def.Widget.Enabled = def.ExternalEnabled;

				if ( !def.ExternalEnabled )
				{
					def.Active = false;

					if ( def.Checkable )
						def.Widget.Checked = false;

					def.Widget.Icon = def.Icon;
				}
			}

			// MUTUAL EXCLUSIVITY (unchanged)
			if ( def.GroupType == ToolBarOptionGroupType.SingleExclusive )
			{
				foreach ( var other in defs )
				{
					if ( other == def ) continue;
					if ( other.Group == def.Group && other.Widget != null )
					{
						other.Widget.Checked = other.Active;
						other.Widget.Icon = other.Active ? other.ToggledIcon ?? other.Icon : other.Icon;
					}
				}
			}
		}
	}

	/// <summary>
	/// Updates the displayed icon of a ToolOptionDef.
	/// Prioritizes external override icon, then cycles through IconCycle if set, and finally falls back to Active/ToggledIcon logic.
	/// </summary>
	private static void UpdateOptionIcon( ToolOptionDef def )
	{
		if ( def.Widget == null ) return;

		// External override takes priority
		if ( !string.IsNullOrEmpty( def.OverrideIcon ) )
		{
			def.Widget.Icon = def.OverrideIcon;
			return;
		}

		// If IconCycle is set, use current index
		if ( def.IconCycle != null && def.IconCycle.Count > 0 )
		{
			def.Widget.Icon = def.IconCycle[def.CurrentIconIndex];
			return;
		}

		// Group / Active logic fallback
		def.Widget.Icon = def.Active
			? def.ToggledIcon ?? def.Icon
			: def.Icon;
	}

	private static void ExecuteShortcutAction( ToolOptionDef def )
	{
		if ( string.IsNullOrWhiteSpace( def.ShortcutAction ) )
			return;

		foreach ( var asm in AppDomain.CurrentDomain.GetAssemblies() )
		{
			foreach ( var type in asm.GetTypes() )
			{
				var methods = type.GetMethods( BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic );

				foreach ( var m in methods )
				{
					var shortcutAttr = m.GetCustomAttribute<ShortcutAttribute>();
					if ( shortcutAttr == null )
						continue;

					if ( shortcutAttr.Identifier == def.ShortcutAction )
					{
						try
						{
							m.Invoke( null, null );
							return;
						}
						catch ( Exception e )
						{
							Log.Warning( $"[Toolbar] ShortcutAction error '{def.ShortcutAction}': {e}" );
							return;
						}
					}
				}
			}
		}

		Log.Warning( $"[Toolbar] No ShortcutAction found: {def.ShortcutAction}" );
	}



}
