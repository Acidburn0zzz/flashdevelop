﻿using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PluginCore.Controls;

namespace PluginCore
{
    public static class ShortcutKeysManager
    {
        private static PropertyInfo p_Shortcuts;
        private static PropertyInfo p_ToolStrips;
        private static PropertyInfo p_IsAssignedToDropDownItem;
        private static MethodInfo m_GetFirstDropDown;
        private static MethodInfo m_GetToplevelOwnerToolStrip;
        private static MethodInfo m_WindowsFormsUtils_GetRootHWnd;

        private static IList toolStrips;

        #region Properties

        internal static IList ToolStrips
        {
            get
            {
                if (toolStrips == null)
                {
                    if (p_ToolStrips == null)
                    {
                        p_ToolStrips = typeof(ToolStripManager).GetProperty("ToolStrips", BindingFlags.Static | BindingFlags.NonPublic);
                    }
                    toolStrips = (IList) p_ToolStrips.GetValue(null, null);
                }
                return toolStrips;
            }
        }

        #endregion

        #region Methods

        public static bool IsShortcutDefined(ShortcutKeys shortcut)
        {
            foreach (ToolStrip strip in ToolStrips)
            {
                if (strip != null && strip.Shortcuts().Contains(shortcut))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsValidShortcut(ShortcutKeys shortcut)
        {
            if (shortcut.IsExtended)
            {
                return IsValidExtendedShortcutFirst(shortcut.First) && IsValidExtendedShortcutSecond(shortcut.Second);
            }
            return IsValidSimpleShortcut(shortcut.First);
        }

        public static bool IsValidSimpleShortcut(Keys first)
        {
            return ToolStripManager.IsValidShortcut(first);
        }

        public static bool IsValidExtendedShortcutFirst(Keys first)
        {
            if (first == 0)
            {
                return false;
            }
            switch (first & Keys.KeyCode)
            {
                case Keys.None:
                case Keys.ShiftKey:
                case Keys.ControlKey:
                case Keys.Menu:
                    return false;
            }
            switch (first & Keys.Modifiers)
            {
                case Keys.None:
                case Keys.Shift:
                    return false;
            }
            return true;
        }

        public static bool IsValidExtendedShortcutSecond(Keys second)
        {
            if (second == 0)
            {
                return false;
            }
            switch (second & Keys.KeyCode)
            {
                case Keys.None:
                case Keys.ShiftKey:
                case Keys.ControlKey:
                case Keys.Menu:
                    return false;
            }
            return true;
        }

        public static bool ProcessCmdKey(ref Message m, ShortcutKeys keyData)
        {
            if (IsValidShortcut(keyData))
            {
                return ProcessShortcut(ref m, keyData);
            }
            return false;
        }

        internal static bool ProcessShortcut(ref Message m, ShortcutKeys shortcut)
        {
            if (!IsThreadUsingToolStrips() || !shortcut.IsExtended)
            {
                return false;
            }
            var control = Control.FromChildHandle(m.HWnd);
            var parent = control;
            if (parent == null)
            {
                return false;
            }
            do
            {
                if (parent.ContextMenuStrip != null)
                {
                    var parent_ContextMenuStrip_Shortcuts = parent.ContextMenuStrip.Shortcuts();
                    if (parent_ContextMenuStrip_Shortcuts.Contains(shortcut))
                    {
                        var item = parent_ContextMenuStrip_Shortcuts[shortcut] as ToolStripMenuItemEx;
                        if (item != null && item.ProcessCmdKeyInternal(ref m, shortcut))
                        {
                            return true;
                        }
                    }
                }
                parent = parent.Parent;
            }
            while (parent != null);
            if (parent != null)
            {
                control = parent;
            }
            bool handled = false;
            bool prune = false;
            for (int i = 0; i < ToolStrips.Count; i++)
            {
                var strip = ToolStrips[i] as ToolStrip;
                bool found = false;
                bool isAssignedToDropDownItem = false;
                if (strip == null)
                {
                    prune = true;
                    continue;
                }
                if (control == null || strip != control.ContextMenuStrip && strip.Shortcuts().Contains(shortcut))
                {
                    if (strip.IsDropDown)
                    {
                        var down = strip as ToolStripDropDown;
                        var firstDropDown = down.GetFirstDropDown() as ContextMenuStrip;
                        if (firstDropDown != null)
                        {
                            isAssignedToDropDownItem = firstDropDown.IsAssignedToDropDownItem();
                            if (!isAssignedToDropDownItem)
                            {
                                if (firstDropDown != control.ContextMenuStrip)
                                {
                                    continue;
                                }
                                found = true;
                            }
                        }
                    }
                    if (!found)
                    {
                        var toplevelOwnerToolStrip = strip.GetToplevelOwnerToolStrip();
                        if (toplevelOwnerToolStrip != null && control != null)
                        {
                            var rootHWnd = WindowsFormsUtils_GetRootHWnd(toplevelOwnerToolStrip);
                            var controlRef = WindowsFormsUtils_GetRootHWnd(control);
                            found = rootHWnd.Handle == controlRef.Handle;
                            if (found)
                            {
                                var form = Control.FromHandle(controlRef.Handle) as Form;
                                if (form != null && form.IsMdiContainer)
                                {
                                    var form2 = toplevelOwnerToolStrip.FindForm();
                                    if (form2 != form && form2 != null)
                                    {
                                        found = form2 == form.ActiveMdiChild;
                                    }
                                }
                            }
                        }
                    }
                    if (found || isAssignedToDropDownItem)
                    {
                        var item = strip.Shortcuts()[shortcut] as ToolStripMenuItemEx;
                        if (item != null && item.ProcessCmdKeyInternal(ref m, shortcut))
                        {
                            handled = true;
                            break;
                        }
                    }
                }
            }
            if (prune)
            {
                PruneToolStripList();
            }
            return handled;
        }

        internal static bool IsThreadUsingToolStrips()
        {
            return ToolStrips != null && ToolStrips.Count > 0;
        }

        internal static void PruneToolStripList()
        {
            if (IsThreadUsingToolStrips())
            {
                for (int i = toolStrips.Count - 1; i >= 0; i--)
                {
                    if (toolStrips[i] == null)
                    {
                        toolStrips.RemoveAt(i);
                    }
                }
            }
        }

        #endregion

        #region Reflections

        internal static Hashtable Shortcuts(this ToolStrip @this)
        {
            if (p_Shortcuts == null)
            {
                p_Shortcuts = typeof(ToolStrip).GetProperty("Shortcuts", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            return (Hashtable) p_Shortcuts.GetValue(@this, null);
        }

        internal static bool IsAssignedToDropDownItem(this ToolStripDropDown @this)
        {
            if (p_IsAssignedToDropDownItem == null)
            {
                p_IsAssignedToDropDownItem = typeof(ToolStripDropDown).GetProperty("IsAssignedToDropDownItem", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            return (bool) p_IsAssignedToDropDownItem.GetValue(@this, null);
        }

        internal static ToolStripDropDown GetFirstDropDown(this ToolStripDropDown @this)
        {
            if (m_GetFirstDropDown == null)
            {
                m_GetFirstDropDown = typeof(ToolStripDropDown).GetMethod("GetFirstDropDown", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            return (ToolStripDropDown) m_GetFirstDropDown.Invoke(@this, null);
        }

        internal static ToolStrip GetToplevelOwnerToolStrip(this ToolStrip @this)
        {
            if (m_GetToplevelOwnerToolStrip == null)
            {
                m_GetToplevelOwnerToolStrip = typeof(ToolStrip).GetMethod("GetToplevelOwnerToolStrip", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            return (ToolStrip) m_GetToplevelOwnerToolStrip.Invoke(@this, null);
        }

        internal static HandleRef WindowsFormsUtils_GetRootHWnd(Control control)
        {
            if (m_WindowsFormsUtils_GetRootHWnd == null)
            {
                var WindowsFormsUtils = typeof(Form).Assembly.GetType("System.Windows.Forms.WindowsFormsUtils");
                m_WindowsFormsUtils_GetRootHWnd = WindowsFormsUtils.GetMethod("GetRootHWnd", BindingFlags.Static | BindingFlags.NonPublic);
            }
            return (HandleRef) m_WindowsFormsUtils_GetRootHWnd.Invoke(null, new object[] { control });
        }

        #endregion
    }
}
