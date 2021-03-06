﻿using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PluginCore;
using PluginCore.Localization;
using ProjectManager.Controls.TreeView;
using SourceControl.Managers;

namespace SourceControl.Actions
{
    internal static class TreeContextMenuUpdate
    {
        static ToolStripMenuItem scItem;

        internal static void SetMenu(ProjectTreeView tree, ProjectSelectionState state)
        {
            if (tree is null || state.Manager is null) return;

            var menuItems = state.Manager.MenuItems;
            menuItems.CurrentNodes = tree.SelectedNodes.ToArray();
            menuItems.CurrentManager = state.Manager;

            AddSCMainItem(tree);
            scItem.DropDownItems.Clear();

            // let a VC provide a completely custom items list
            foreach (var item in menuItems.Items)
            {
                if (item.Value.Show.Invoke(state))
                {
                    scItem.DropDownItems.Add(item.Key);
                    if (item.Value.Enable != null)
                        item.Key.Enabled = item.Value.Enable.Invoke(state);
                }
            }

            // classical VC menu items

            var items = new List<ToolStripItem> {menuItems.Update, menuItems.Commit, menuItems.Push, menuItems.ShowLog};

            // generic
            int minLen = items.Count;

            // specific
            if (state.Files == 1 && state.Total == 1) items.Add(menuItems.Annotate);

            if (state.Files == 2 && state.Total == 2) items.Add(menuItems.Diff);
            if (state.Conflict == 1 && state.Total == 1) items.Add(menuItems.EditConflict);

            if (state.Unknown + state.Ignored > 0 || state.Dirs > 0) items.Add(menuItems.Add);
            if (state.Unknown + state.Ignored == state.Total) items.Add(menuItems.Ignore);

            if (state.Unknown + state.Ignored < state.Total)
            {
                if (state.Added > 0) items.Add(menuItems.UndoAdd);
                else if (state.Revert > 0)
                {
                    if (state.Diff > 0) items.Add(menuItems.DiffChange);
                    items.Add(menuItems.Revert);
                }
                else if (state.Total == 1) items.Add(menuItems.DiffChange);
            }

            if (items.Count > minLen) items.Insert(minLen, menuItems.MidSeparator);
            items.RemoveAll(item => item is null);
            scItem.DropDownItems.AddRange(items.ToArray());
        }

        static void AddSCMainItem(MultiSelectTreeView tree)
        {
            if (scItem is null)
            {
                scItem = new ToolStripMenuItem();
                scItem.Text = TextHelper.GetString("Label.SourceControl");
                scItem.Image = PluginBase.MainForm.FindImage("480");
            }
            // add in same group as Open/Execute/Shell menu...
            bool isProjectNode = tree.SelectedNodes.Count > 0 && tree.SelectedNodes[0].GetType().ToString().EndsWithOrdinal("ProjectNode");
            int index = GetNthSeparatorIndex(tree.ContextMenuStrip, isProjectNode ? 2 : 1);
            if (index >= 0) tree.ContextMenuStrip.Items.Insert(index, scItem);
            else tree.ContextMenuStrip.Items.Add(scItem);
        }

        static int GetNthSeparatorIndex(ToolStrip menu, int n)
        {
            int index = -1;
            foreach (ToolStripItem item in menu.Items)
            {
                index++;
                if (item is ToolStripSeparator && --n <= 0)
                    return index;
            }
            return -1;
        }
    }
}