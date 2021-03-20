/*
  SamplePlugin - An Example KeePass Plugin
  Copyright (C) 2003-2019 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

using KeePass.Forms;
using KeePass.Plugins;
using KeePass.Resources;
using KeePass.UI;

using KeePassLib;
using KeePassLib.Security;
using KeePassLib.Utility;

// The namespace name must be the same as the file name of the
// plugin without its extension.
// For example, if you compile a plugin 'SamplePlugin.dll',
// the namespace must be named 'SamplePlugin'.
namespace SamplePlugin
{
	// Namespace name 'SamplePlugin' + 'Ext' = 'SamplePluginExt'
	public sealed class SamplePluginExt : Plugin
	{
		// The plugin remembers its host in this variable
		private IPluginHost m_host = null;

		// State of the option to add 30 entries instead of 10
		private bool m_bEntries30 = true;

		// Name of the configuration option to add 30 entries
		// instead of 10
		private const string OptionEntries30 = "SamplePlugin_Entries30";

		/// <summary>
		/// The <c>Initialize</c> method is called by KeePass when
		/// you should initialize your plugin.
		/// </summary>
		/// <param name="host">Plugin host interface. Through this
		/// interface you can access the KeePass main window, the
		/// currently opened database, etc.</param>
		/// <returns>You must return <c>true</c> in order to signal
		/// successful initialization. If you return <c>false</c>,
		/// KeePass unloads your plugin (without calling the
		/// <c>Terminate</c> method of your plugin).</returns>
		public override bool Initialize(IPluginHost host)
		{
			if(host == null) return false; // Fail; we need the host
			m_host = host;

			// Load the last state of the 30 entries option
			m_bEntries30 = m_host.CustomConfig.GetBool(OptionEntries30, true);

			// We want a notification when the user tried to save
			// the current database
			m_host.MainWindow.FileSaved += this.OnFileSaved;

			return true; // Initialization successful
		}

		/// <summary>
		/// The <c>Terminate</c> method is called by KeePass when
		/// you should free all resources, close files/streams,
		/// remove event handlers, etc.
		/// </summary>
		public override void Terminate()
		{
			// Save the state of the 30 entries option
			m_host.CustomConfig.SetBool(OptionEntries30, m_bEntries30);

			// Remove event handler (important!)
			m_host.MainWindow.FileSaved -= this.OnFileSaved;
		}

		/// <summary>
		/// Get a menu item of the plugin. See
		/// https://keepass.info/help/v2_dev/plg_index.html#co_menuitem
		/// </summary>
		/// <param name="t">Type of the menu that the plugin should
		/// return an item for.</param>
		public override ToolStripMenuItem GetMenuItem(PluginMenuType t)
		{
			// Our menu item below is intended for the main location(s),
			// not for other locations like the group or entry menus
			if(t != PluginMenuType.Main) return null;

			ToolStripMenuItem tsmi = new ToolStripMenuItem("SamplePlugin");

			// Add menu item 'Add Some Groups'
			ToolStripMenuItem tsmiAddGroups = new ToolStripMenuItem();
			tsmiAddGroups.Text = "Add Some Groups";
			tsmiAddGroups.Click += this.OnMenuAddGroups;
			tsmi.DropDownItems.Add(tsmiAddGroups);

			// Add menu item 'Add Some Entries'
			ToolStripMenuItem tsmiAddEntries = new ToolStripMenuItem();
			tsmiAddEntries.Text = "Add Some Entries";
			tsmiAddEntries.Click += this.OnMenuAddEntries;
			tsmi.DropDownItems.Add(tsmiAddEntries);

			tsmi.DropDownItems.Add(new ToolStripSeparator());

			ToolStripMenuItem tsmiEntries30 = new ToolStripMenuItem();
			tsmiEntries30.Text = "Add 30 Entries Instead Of 10";
			tsmiEntries30.Click += this.OnMenuEntries30;
			tsmi.DropDownItems.Add(tsmiEntries30);

			// By using an anonymous method as event handler, we do not
			// need to remember menu item references manually, and
			// multiple calls of the GetMenuItem method (to show the
			// menu item in multiple places) are no problem
			tsmi.DropDownOpening += delegate(object sender, EventArgs e)
			{
				// Disable the commands 'Add Some Groups' and
				// 'Add Some Entries' when the database is not open
				PwDatabase pd = m_host.Database;
				bool bOpen = ((pd != null) && pd.IsOpen);
				tsmiAddGroups.Enabled = bOpen;
				tsmiAddEntries.Enabled = bOpen;

				// Update the checkmark of the menu item
				UIUtil.SetChecked(tsmiEntries30, m_bEntries30);
			};

			return tsmi;
		}

		private void OnMenuAddGroups(object sender, EventArgs e)
		{
			PwDatabase pd = m_host.Database;
			if((pd == null) || !pd.IsOpen) { Debug.Assert(false); return; }

			PwGroup pgParent = pd.RootGroup;
			Random rnd = new Random();

			for(int i = 0; i < 5; ++i)
			{
				// Add a new group with a random icon
				PwGroup pg = new PwGroup(true, true, "Sample Group #" + i.ToString(),
					(PwIcon)rnd.Next(0, (int)PwIcon.Count));
				pgParent.AddGroup(pg, true);
			}

			m_host.MainWindow.UpdateUI(false, null, true, null, false, null, true);
		}

		private void OnMenuAddEntries(object sender, EventArgs e)
		{
			PwDatabase pd = m_host.Database;
			if((pd == null) || !pd.IsOpen) { Debug.Assert(false); return; }

			PwGroup pgParent = (m_host.MainWindow.GetSelectedGroup() ?? pd.RootGroup);

			int nEntriesToAdd = (m_bEntries30 ? 30 : 10);
			for(int i = 0; i < nEntriesToAdd; ++i)
			{
				PwEntry pe = new PwEntry(true, true);

				pe.Strings.Set(PwDefs.TitleField, new ProtectedString(
					pd.MemoryProtection.ProtectTitle, "Sample Entry #" + i.ToString()));
				pe.Strings.Set(PwDefs.UserNameField, new ProtectedString(
					pd.MemoryProtection.ProtectUserName, Guid.NewGuid().ToString()));
				pe.Strings.Set(PwDefs.PasswordField, new ProtectedString(
					pd.MemoryProtection.ProtectPassword, "The secret password"));

				pgParent.AddEntry(pe, true);
			}

			m_host.MainWindow.UpdateUI(false, null, true, pgParent, true, null, true);
		}

		private void OnMenuEntries30(object sender, EventArgs e)
		{
			m_bEntries30 = !m_bEntries30; // Toggle the option

			// The checkmark of the menu item is updated by
			// our DropDownOpening event handler
		}

		private void OnFileSaved(object sender, FileSavedEventArgs e)
		{
			MessageService.ShowInfo("SamplePlugin has been notified that the user tried to save to the following file:",
				e.Database.IOConnectionInfo.Path, "Result: " +
				(e.Success ? "success." : "failed."));
		}
	}
}
