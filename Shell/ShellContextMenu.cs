// BExplorer.Shell - A Windows Shell library for .Net.
// Copyright (C) 2007-2009 Steven J. Kirk
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either 
// version 2 of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public 
// License along with this program; if not, write to the Free 
// Software Foundation, Inc., 51 Franklin Street, Fifth Floor,  
// Boston, MA 2110-1301, USA.
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using BExplorer.Shell.Interop;
using Microsoft.Win32;
using System.Threading;
using System.Runtime.ExceptionServices;
using System.Linq;
using System.Security.AccessControl;
using System.Windows;
using BExplorer.Shell._Plugin_Interfaces;
using Settings;
using Clipboard = System.Windows.Forms.Clipboard;
using DataFormats = System.Windows.Forms.DataFormats;
using Point = System.Drawing.Point;
using Timer = System.Windows.Forms.Timer;

namespace BExplorer.Shell {
  /// <summary>
  /// Provides support for displaying the context menu of a shell item.
  /// </summary>
  /// 
  /// <remarks>
  /// <para>
  /// Use this class to display a context menu for a shell item, either
  /// as a popup menu, or as a main menu. 
  /// </para>
  /// 
  /// <para>
  /// To display a popup menu, simply call ShowContextMenu(...)/>
  /// with the parent control and the position at which the menu should
  /// be shown.
  /// </para>
  /// 
  /// <para>
  /// To display a shell context menu in a Form's main menu, call the
  /// <see cref="Populate"/> method to populate the menu. In addition, 
  /// you must intercept a number of special messages that will be sent 
  /// to the menu's parent form. To do this, you must override 
  /// <see cref="Form.WndProc"/> like so:
  /// </para>
  /// 
  /// <code>
  ///     protected override void WndProc(ref Message m) {
  ///         if ((m_ContextMenu == null) || (!m_ContextMenu.HandleMenuMessage(ref m))) {
  ///             base.WndProc(ref m);
  ///         }
  ///     }
  /// </code>
  /// 
  /// <para>
  /// Where m_ContextMenu is the <see cref="ShellContextMenu"/> being shown.
  /// </para>
  /// 
  /// Standard menu commands can also be invoked from this class, for 
  /// example InvokeDelete and InvokeRename.
  /// </remarks>
  public class ShellContextMenu {

    #region Locals
    const uint m_CmdFirst = 0x8000;
    MessageWindow m_MessageWindow;
    IContextMenu m_ComInterface;
    IContextMenu2 m_ComInterface2;
    IContextMenu3 m_ComInterface3;
    private IListItemEx[] _Items;
    IntPtr _NewMenuPtr = IntPtr.Zero;


    /// <summary>The ShellView the ContextMenu is associated with</summary>
    private ShellView _ShellView { get; set; }
    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellContextMenu"/> class.
    /// </summary>
    /// <param name="shellView">The ShellView the ContextMenu is associated with</param>
    /// <param name="Use_GetNewContextMenu"></param>
    public ShellContextMenu(ShellView shellView, bool Use_GetNewContextMenu) {
      this._ShellView = shellView;

      IntPtr iContextMenu = IntPtr.Zero;

      if (Use_GetNewContextMenu)
        this.GetNewContextMenu(_ShellView.CurrentFolder, out iContextMenu, out m_ComInterface);
      else
        this.GetOpenWithContextMenu(_ShellView.SelectedItems.ToArray(), out iContextMenu, out m_ComInterface);

      m_ComInterface2 = m_ComInterface as IContextMenu2;
      m_ComInterface3 = m_ComInterface as IContextMenu3;
      m_MessageWindow = new MessageWindow(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellContextMenu"/> 
    /// class.
    /// </summary>
    /// 
    /// <param name="shellView">The <see cref="ShellView">ShellView</see> for this context menu</param>
    /// <param name="item">
    /// The item to which the context menu should refer.
    /// </param>
    public ShellContextMenu(ShellView shellView, IListItemEx item) {
      this._ShellView = shellView;
      Initialize(new IListItemEx[] { item });
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellContextMenu"/> class.
    /// </summary>
    /// 
    /// <param name="items">
    /// The items to which the context menu should refer.
    /// </param>
    /// <param name="svgio"></param>
    /// <param name="view">The ShellView the ContextMenu is associated with</param>
    public ShellContextMenu(IListItemEx[] items, SVGIO svgio = SVGIO.SVGIO_SELECTION, ShellView view = null) {
      this._ShellView = view;

      if (svgio == SVGIO.SVGIO_BACKGROUND)
        Initialize(items[0]);
      else
        Initialize(items);
    }

    #endregion

    /// <summary>
    /// Handles context menu messages when the <see cref="ShellContextMenu"/>
    /// is displayed on a Form's main menu bar.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>
    /// To display a shell context menu in a Form's main menu, call the
    /// <see cref="Populate"/> method to populate the menu with the shell
    /// item's menu items. In addition, you must intercept a number of
    /// special messages that will be sent to the menu's parent form. To
    /// do this, you must override <see cref="Form.WndProc"/> like so:
    /// </para>
    /// 
    /// <code>
    ///     protected override void WndProc(ref Message m) {
    ///         if ((m_ContextMenu == null) || (!m_ContextMenu.HandleMenuMessage(ref m))) {
    ///             base.WndProc(ref m);
    ///         }
    ///     }
    /// </code>
    /// 
    /// <para>
    /// Where m_ContextMenu is the <see cref="ShellContextMenu"/> being shown.
    /// </para>
    /// </remarks>
    /// 
    /// <param name="m">
    /// The message to handle.
    /// </param>
    /// 
    /// <returns>
    /// <see langword="true"/> if the message was a Shell Context Menu
    /// message, <see langword="false"/> if not. If the method returns false,
    /// then the message should be passed down to the base class's
    /// <see cref="Form.WndProc"/> method.
    /// </returns>
    public bool HandleMenuMessage(ref Message m) {
      //For send to menu in the ListView context menu
      if (m.Msg == (int)WM.WM_INITMENUPOPUP | m.Msg == (int)WM.WM_MEASUREITEM | m.Msg == (int)WM.WM_DRAWITEM) {
        if (m.Msg == (int)WM.WM_INITMENUPOPUP && m.WParam == _NewMenuPtr) {
          _ShellView.IsRenameNeeded = true;
        }
        if (m_ComInterface3 != null) {
          var ptr = IntPtr.Zero;
          return 0 == m_ComInterface3.HandleMenuMsg2(m.Msg, m.WParam, m.LParam, out ptr);
        }

      }
      return false;
    }

    /// <summary>
    /// Populates a <see cref="Menu"/> with the context menu items for
    /// a shell item.
    /// </summary>
    /// 
    /// <remarks>
    /// If this method is being used to populate a Form's main menu
    /// then you need to call <see cref="HandleMenuMessage"/> in the
    /// Form's message handler.
    /// </remarks>
    /// 
    /// <param name="menu">The menu to populate.</param>
    /// <param name="additionalFlags"></param>
    public void Populate(Menu menu, CMF additionalFlags) {
      m_ComInterface.QueryContextMenu(menu.Handle, 0, (int)m_CmdFirst, int.MaxValue, CMF.EXPLORE | CMF.SYNCCASCADEMENU | additionalFlags | (Control.ModifierKeys == Keys.Shift ? CMF.EXTENDEDVERBS : 0));
    }

    Timer _Timer = new Timer();
    /// <summary>
    /// Shows a context menu for a shell item.
    /// </summary>
    /// <param name="control">
    /// The parent control.
    /// </param>
    /// <param name="pos">
    /// The position on <paramref name="control"/> that the menu
    /// should be displayed at.
    /// </param>
    /// <param name="aditionalFlags">additional flags that are used to display items in context menu</param>
    /// <param name="IsOnEmpty">Is context menu invoked on empty space</param>
    public void ShowContextMenu(Control control, Point pos, CMF aditionalFlags = 0, bool IsOnEmpty = false) {
      using (ContextMenu mnu = new ContextMenu()) {
        pos = control.PointToScreen(pos);
        this.Populate(mnu, aditionalFlags);
        this._Timer.Interval = 2;
        this._Timer.Tick += TimerOnTick;
        this._Timer.Stop();

        var view = new ContextMenu();
        var sortMenu = new ContextMenu();
        var groupMenu = new ContextMenu();
        var count = User32.GetMenuItemCount(mnu.Handle);

        var itemInfo = new MENUITEMINFO();
        itemInfo.cbSize = MENUITEMINFO.SizeOf;
        itemInfo.fMask = MIIM.MIIM_FTYPE | MIIM.MIIM_DATA | MIIM.MIIM_STRING | MIIM.MIIM_SUBMENU | MIIM.MIIM_ID;
        if (User32.GetMenuItemInfo(mnu.Handle, (uint)count - 1, true, ref itemInfo)) {
          if ((itemInfo.fType & MFT.MFT_SEPARATOR) != 0) {
            User32.DeleteMenu(mnu.Handle, count - 1, MF.MF_BYPOSITION);
          }
        }
        if (control is ShellView) {
          mnu.Collapse += (sender, args) => ((ShellView)control).IsFocusAllowed = true;
        }

        if (IsOnEmpty) {
          this.RemoveDefaultExplorerItems(mnu, control, ref itemInfo);
        }

        User32.GetMenuItemInfo(mnu.Handle, (uint)User32.GetMenuItemCount(mnu.Handle) - 3, true, ref itemInfo);
        if (itemInfo.hSubMenu == IntPtr.Zero) {
          User32.GetMenuItemInfo(mnu.Handle, (uint)User32.GetMenuItemCount(mnu.Handle) - 1, true, ref itemInfo);
        }

        this._NewMenuPtr = itemInfo.hSubMenu;

        if (IsOnEmpty) {
          this.GenerateExplorerBackgroundMenuItems(view, mnu, sortMenu, groupMenu);
        } else {
          if (this._Items.FirstOrDefault()?.IsFolder == true) {
            this.GenerateMenuItem(mnu, System.Windows.Application.Current?.FindResource("mnuOpenNewTab")?.ToString(), 301, false, 1);
          }
        }

        this.RemoveDuplicatedSeparators(mnu);



        var command = User32.TrackPopupMenuEx(mnu.Handle, TPM.TPM_RETURNCMD, pos.X, pos.Y, m_MessageWindow.Handle, IntPtr.Zero);
        if (command > 0 && command < m_CmdFirst) {
          switch (command) {
            case 245:
              this._ShellView.SetGroupOrder(false);
              break;
            case 246:
              this._ShellView.SetGroupOrder();
              break;
            case 247:
              var colasc = this._ShellView.Collumns.FirstOrDefault(w => w.ID == this._ShellView.LastSortedColumnId);
              this._ShellView.SetSortCollumn(true, colasc, SortOrder.Ascending);
              break;
            case 248:
              var coldesc = this._ShellView.Collumns.FirstOrDefault(w => w.ID == this._ShellView.LastSortedColumnId);
              this._ShellView.SetSortCollumn(true, coldesc, SortOrder.Descending);
              break;
            case 249:
              this._ShellView.PasteAvailableFiles();
              break;
            case 250:
              this._ShellView.RefreshContents();
              break;
            case 251:
              this._ShellView.View = ShellViewStyle.ExtraLargeIcon;
              break;
            case 252:
              this._ShellView.View = ShellViewStyle.LargeIcon;
              break;
            case 253:
              this._ShellView.View = ShellViewStyle.Medium;
              break;
            case 254:
              this._ShellView.View = ShellViewStyle.SmallIcon;
              break;
            case 255:
              this._ShellView.View = ShellViewStyle.List;
              break;
            case 256:
              this._ShellView.View = ShellViewStyle.Details;
              break;
            case 257:
              this._ShellView.View = ShellViewStyle.Tile;
              break;
            case 258:
              this._ShellView.View = ShellViewStyle.Content;
              break;
            case 259:
              this._ShellView.View = ShellViewStyle.Thumbstrip;
              break;
            case 260:
              if (this._ShellView.IsGroupsEnabled) {
                this._ShellView.DisableGroups();
              }
              break;
            case 301:
              this._ShellView.RaiseMiddleClickOnItem(this._Items.First());
              break;
            default:
              break;
          }

          if (command >= 262 && command <= 262 + this._ShellView.Collumns.Count) {
            this._ShellView.SetSortCollumn(true, this._ShellView.Collumns[command - 262], SortOrder.Ascending);
          } else if (command > 260 && command != 301) {
            if (!this._ShellView.IsGroupsEnabled)
              this._ShellView.EnableGroups();
            this._ShellView.GenerateGroupsFromColumn(this._ShellView.Collumns[command - (262 + this._ShellView.Collumns.Count) - 1], false);
          }
        }

        if (command > m_CmdFirst) {
          var info = string.Empty;
          var bytes = new byte[256];
          IntPtr pszName = Marshal.AllocHGlobal(256);
          this.m_ComInterface3.GetCommandString(command - (int)m_CmdFirst, 4, 0, pszName, 260);

          //var index = 0;
          //while (index < bytes.Length - 1 && (bytes[index] != 0 || bytes[index + 1] != 0)) { index += 2; }

          //if (index < bytes.Length - 1) { info = Encoding.Unicode.GetString(bytes, 0, index); }
          info = Marshal.PtrToStringAuto(pszName);

          switch (info) {
            case "open":
              (control as ShellView)?.OpenOrNavigateItem();
              break;
            case "rename":
              (control as ShellView)?.RenameSelectedItem();
              break;
            case "cut":
              (control as ShellView)?.CutSelectedFiles();
              break;
            case "copy":
              (control as ShellView)?.CopySelectedFiles();
              break;
            default:
              CMINVOKECOMMANDINFOEX cminvokecommandinfoex = new CMINVOKECOMMANDINFOEX {
                lpVerb = (IntPtr)(command - m_CmdFirst),
                lpVerbW = (IntPtr)(command - m_CmdFirst)
              };
              cminvokecommandinfoex.cbSize = Marshal.SizeOf(cminvokecommandinfoex);

              //cminvokecommandinfoex.lpDirectory = this._ShellView.CurrentFolder.ParsingName;
              //cminvokecommandinfoex.lpDirectoryW = this._ShellView.CurrentFolder.ParsingName;

              cminvokecommandinfoex.fMask = ((CMIC.Asyncok | CMIC.PtInvoke | CMIC.Unicode | CMIC.FlagNoUi) | (((Control.ModifierKeys & Keys.Control) > Keys.None) ? CMIC.ControlDown : ((CMIC)0))) | (((Control.ModifierKeys & Keys.Shift) > Keys.None) ? CMIC.ShiftDown : ((CMIC)0));
              cminvokecommandinfoex.ptInvoke = pos;
              cminvokecommandinfoex.nShow = 1;
              //cminvokecommandinfoex.hwnd = this._ShellView.LVHandle;
              this.m_ComInterface3.InvokeCommand(ref cminvokecommandinfoex);
              //this.InvokeCommand((IntPtr)(command - m_CmdFirst), pos, (IntPtr)(command - m_CmdFirst));
              break;
          }
        }

        User32.DestroyMenu(mnu.Handle);
        view.Dispose();
        User32.DestroyMenu(view.Handle);
        sortMenu.Dispose();
        User32.DestroyMenu(sortMenu.Handle);
        groupMenu.Dispose();
        User32.DestroyMenu(groupMenu.Handle);
      }

      Marshal.ReleaseComObject(this.m_ComInterface);
      Marshal.ReleaseComObject(this.m_ComInterface2);
      Marshal.ReleaseComObject(this.m_ComInterface3);
      Marshal.Release(this._Result);
      this._Result = IntPtr.Zero;
    }
    public void ShowContextMenu(Point pos, CMF aditionalFlags, bool IsOnEmpty = false) {
      using (ContextMenu mnu = new ContextMenu()) {
        this.Populate(mnu, aditionalFlags);
        this._Timer.Interval = 2;
        this._Timer.Tick += TimerOnTick;
        this._Timer.Stop();

        var view = new ContextMenu();
        var sortMenu = new ContextMenu();
        var groupMenu = new ContextMenu();
        var count = User32.GetMenuItemCount(mnu.Handle);

        var itemInfo = new MENUITEMINFO();
        itemInfo.cbSize = MENUITEMINFO.SizeOf;
        itemInfo.fMask = MIIM.MIIM_FTYPE | MIIM.MIIM_DATA | MIIM.MIIM_STRING | MIIM.MIIM_SUBMENU | MIIM.MIIM_ID;
        if (User32.GetMenuItemInfo(mnu.Handle, (uint)count - 1, true, ref itemInfo)) {
          if ((itemInfo.fType & MFT.MFT_SEPARATOR) != 0) {
            User32.DeleteMenu(mnu.Handle, count - 1, MF.MF_BYPOSITION);
          }
        }
        
        User32.GetMenuItemInfo(mnu.Handle, (uint)User32.GetMenuItemCount(mnu.Handle) - 3, true, ref itemInfo);
        if (itemInfo.hSubMenu == IntPtr.Zero) {
          User32.GetMenuItemInfo(mnu.Handle, (uint)User32.GetMenuItemCount(mnu.Handle) - 1, true, ref itemInfo);
        }

        this._NewMenuPtr = itemInfo.hSubMenu;

        if (IsOnEmpty) {
          this.GenerateExplorerBackgroundMenuItems(view, mnu, sortMenu, groupMenu);
        } else {
          if (this._Items.FirstOrDefault()?.IsFolder == true) {
            this.GenerateMenuItem(mnu, System.Windows.Application.Current?.FindResource("mnuOpenNewTab")?.ToString(), 301, false, 1);
          }
        }

        this.RemoveDuplicatedSeparators(mnu);



        var command = User32.TrackPopupMenuEx(mnu.Handle, TPM.TPM_RETURNCMD, pos.X, pos.Y, m_MessageWindow.Handle, IntPtr.Zero);
        if (command > 0 && command < m_CmdFirst) {
          switch (command) {
            case 245:
              this._ShellView.SetGroupOrder(false);
              break;
            case 246:
              this._ShellView.SetGroupOrder();
              break;
            case 247:
              var colasc = this._ShellView.Collumns.FirstOrDefault(w => w.ID == this._ShellView.LastSortedColumnId);
              this._ShellView.SetSortCollumn(true, colasc, SortOrder.Ascending);
              break;
            case 248:
              var coldesc = this._ShellView.Collumns.FirstOrDefault(w => w.ID == this._ShellView.LastSortedColumnId);
              this._ShellView.SetSortCollumn(true, coldesc, SortOrder.Descending);
              break;
            case 249:
              this._ShellView.PasteAvailableFiles();
              break;
            case 250:
              this._ShellView.RefreshContents();
              break;
            case 251:
              this._ShellView.View = ShellViewStyle.ExtraLargeIcon;
              break;
            case 252:
              this._ShellView.View = ShellViewStyle.LargeIcon;
              break;
            case 253:
              this._ShellView.View = ShellViewStyle.Medium;
              break;
            case 254:
              this._ShellView.View = ShellViewStyle.SmallIcon;
              break;
            case 255:
              this._ShellView.View = ShellViewStyle.List;
              break;
            case 256:
              this._ShellView.View = ShellViewStyle.Details;
              break;
            case 257:
              this._ShellView.View = ShellViewStyle.Tile;
              break;
            case 258:
              this._ShellView.View = ShellViewStyle.Content;
              break;
            case 259:
              this._ShellView.View = ShellViewStyle.Thumbstrip;
              break;
            case 260:
              if (this._ShellView.IsGroupsEnabled) {
                this._ShellView.DisableGroups();
              }
              break;
            case 301:
              this._ShellView.RaiseMiddleClickOnItem(this._Items.First());
              break;
            default:
              break;
          }

          if (command >= 262 && command <= 262 + this._ShellView.Collumns.Count) {
            this._ShellView.SetSortCollumn(true, this._ShellView.Collumns[command - 262], SortOrder.Ascending);
          } else if (command > 260 && command != 301) {
            if (!this._ShellView.IsGroupsEnabled)
              this._ShellView.EnableGroups();
            this._ShellView.GenerateGroupsFromColumn(this._ShellView.Collumns[command - (262 + this._ShellView.Collumns.Count) - 1], false);
          }
        }

        if (command > m_CmdFirst) {
          var info = string.Empty;
          var bytes = new byte[256];
          IntPtr pszName = Marshal.AllocHGlobal(256);
          this.m_ComInterface3.GetCommandString(command - (int)m_CmdFirst, 4, 0, pszName, 260);

          //var index = 0;
          //while (index < bytes.Length - 1 && (bytes[index] != 0 || bytes[index + 1] != 0)) { index += 2; }

          //if (index < bytes.Length - 1) { info = Encoding.Unicode.GetString(bytes, 0, index); }
          info = Marshal.PtrToStringAuto(pszName);

          switch (info) {
            case "open":
              //(control as ShellView)?.OpenOrNavigateItem();
              break;
            case "rename":
              //(control as ShellView)?.RenameSelectedItem();
              break;
            case "cut":
              //(control as ShellView)?.CutSelectedFiles();
              break;
            case "copy":
              //(control as ShellView)?.CopySelectedFiles();
              break;
            default:
              CMINVOKECOMMANDINFOEX cminvokecommandinfoex = new CMINVOKECOMMANDINFOEX {
                lpVerb = (IntPtr)(command - m_CmdFirst),
                lpVerbW = (IntPtr)(command - m_CmdFirst)
              };
              cminvokecommandinfoex.cbSize = Marshal.SizeOf(cminvokecommandinfoex);

              //cminvokecommandinfoex.lpDirectory = this._ShellView.CurrentFolder.ParsingName;
              //cminvokecommandinfoex.lpDirectoryW = this._ShellView.CurrentFolder.ParsingName;

              cminvokecommandinfoex.fMask = ((CMIC.Asyncok | CMIC.PtInvoke | CMIC.Unicode | CMIC.FlagNoUi) | (((Control.ModifierKeys & Keys.Control) > Keys.None) ? CMIC.ControlDown : ((CMIC)0))) | (((Control.ModifierKeys & Keys.Shift) > Keys.None) ? CMIC.ShiftDown : ((CMIC)0));
              cminvokecommandinfoex.ptInvoke = pos;
              cminvokecommandinfoex.nShow = 1;
              //cminvokecommandinfoex.hwnd = this._ShellView.LVHandle;
              this.m_ComInterface3.InvokeCommand(ref cminvokecommandinfoex);
              //this.InvokeCommand((IntPtr)(command - m_CmdFirst), pos, (IntPtr)(command - m_CmdFirst));
              break;
          }
        }

        User32.DestroyMenu(mnu.Handle);
        view.Dispose();
        User32.DestroyMenu(view.Handle);
        sortMenu.Dispose();
        User32.DestroyMenu(sortMenu.Handle);
        groupMenu.Dispose();
        User32.DestroyMenu(groupMenu.Handle);
      }

      Marshal.ReleaseComObject(this.m_ComInterface);
      Marshal.ReleaseComObject(this.m_ComInterface2);
      Marshal.ReleaseComObject(this.m_ComInterface3);
      Marshal.Release(this._Result);
      this._Result = IntPtr.Zero;
    }
    private void TimerOnTick(Object sender, EventArgs e) {
      this._Timer.Stop();
      User32.SendMessage(this.m_MessageWindow.Handle, 0x001F, 0, 0);
    }
    private List<string> GetNewContextMenuItems() {
      var newEntrieslist = new List<string>();
      var reg = Registry.CurrentUser;
      var classesrk = reg.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Discardable\PostSetup\ShellNew");
      var classes = (string[])classesrk?.GetValue("Classes");
      if (classes != null) {
        newEntrieslist.AddRange(classes);
      }

      classesrk?.Close();
      reg.Close();
      return newEntrieslist;
    }

    public int ShowContextMenu(Point pos, int type = 0, Boolean shouldShow = true) {
      if (type == 0) {
        var newItems = this.GetNewContextMenuItems();
        using (ContextMenu menu = new ContextMenu()) {
          Populate(menu, CMF.EXPLORE);
          int command = User32.TrackPopupMenuEx(menu.Handle, TPM.TPM_RETURNCMD, pos.X, pos.Y, m_MessageWindow.Handle, IntPtr.Zero);
          if (command > 0) {
            var cmdID = command - m_CmdFirst;
            var verb = cmdID == 1 ? "newFolder" : cmdID == 2 ? ".lnk" : newItems[(int)cmdID - 3];
            var item = Marshal.StringToHGlobalUni(verb);
            this._ShellView.IsRenameNeeded = true;
            InvokeCommand(item, pos, Marshal.StringToHGlobalAnsi(verb));
          }
        }
        return 0;
      } else {
        using (ContextMenu menu = new ContextMenu()) {
          Populate(menu, CMF.EXPLORE);
          var submenuHandle = User32.GetSubMenu(menu.Handle, 0);
          if (shouldShow) {
            int command = User32.TrackPopupMenuEx(
                submenuHandle == IntPtr.Zero ? menu.Handle : submenuHandle, TPM.TPM_RETURNCMD, pos.X, pos.Y, m_MessageWindow.Handle, IntPtr.Zero
            );

            if (command > 0)
              InvokeCommand((IntPtr)(command - m_CmdFirst), pos, (IntPtr)(command - m_CmdFirst));
          }

          return User32.GetMenuItemCount(submenuHandle == IntPtr.Zero ? menu.Handle : submenuHandle);
        }
      }
    }

    private void GenerateExplorerBackgroundMenuItems(ContextMenu view, ContextMenu mnu, ContextMenu sortMenu, ContextMenu groupMenu) {
      this.GenerateMenuItem(view, "Thumbstrip", 259, _ShellView.View == ShellViewStyle.Thumbstrip);
      this.GenerateMenuItem(view, "Content", 258, _ShellView.View == ShellViewStyle.Content);
      this.GenerateMenuItem(view, "Tiles", 257, _ShellView.View == ShellViewStyle.Tile);
      this.GenerateMenuItem(view, "Details", 256, _ShellView.View == ShellViewStyle.Details);
      this.GenerateMenuItem(view, "List", 255, _ShellView.View == ShellViewStyle.List);
      this.GenerateMenuItem(view, "Small icon", 254, _ShellView.View == ShellViewStyle.SmallIcon);
      this.GenerateMenuItem(view, "Medium", 253, _ShellView.View == ShellViewStyle.Medium);
      this.GenerateMenuItem(view, "Large Icon", 252, _ShellView.View == ShellViewStyle.LargeIcon);
      this.GenerateMenuItem(view, "Extra Large Icon", 251, _ShellView.View == ShellViewStyle.ExtraLargeIcon);

      if (this._ShellView.CurrentFolder.IsFileSystem && (Clipboard.ContainsData(DataFormats.FileDrop) || Clipboard.ContainsData("Shell IDList Array"))) {
        this.GenerateSeparator(mnu);
        this.GenerateMenuItemExecutable(mnu, "Paste", 249);
      }

      this.GenerateSeparator(mnu);

      this.GenerateMenuItemExecutable(mnu, "Refresh", 250);

      var colID = 261 + this._ShellView.Collumns.Count;

      var colGroupID = colID + this._ShellView.Collumns.Count + 1;

      var collist = new List<Collumns>();
      collist.AddRange(this._ShellView.Collumns);
      collist.Reverse();
      this.GenerateMenuItem(sortMenu, "Descending", 248, this._ShellView.LastSortOrder == SortOrder.Descending);
      this.GenerateMenuItem(sortMenu, "Ascending", 247, this._ShellView.LastSortOrder == SortOrder.Ascending);
      this.GenerateSeparator(sortMenu);
      foreach (Collumns collumn in collist) {
        this.GenerateMenuItem(sortMenu, collumn.Name, colID--, collumn == this._ShellView.Collumns.FirstOrDefault(w => w.ID == this._ShellView.LastSortedColumnId));
      }
      this.GenerateMenuItem(groupMenu, "Descending", 246, this._ShellView.LastGroupOrder == SortOrder.Descending);
      this.GenerateMenuItem(groupMenu, "Ascending", 245, this._ShellView.LastGroupOrder == SortOrder.Ascending);
      this.GenerateSeparator(groupMenu);
      foreach (Collumns collumn in collist) {
        this.GenerateMenuItem(groupMenu, collumn.Name, colGroupID--, collumn == this._ShellView.LastGroupCollumn);
      }
      this.GenerateMenuItem(groupMenu, "(None)", 260, this._ShellView.LastGroupCollumn == null);
      collist.Clear();
      collist = null;

      this.GenerateSubmenu(groupMenu, mnu, "Group by");

      this.GenerateSubmenu(sortMenu, mnu, "Sort by");

      this.GenerateSubmenu(view, mnu, "View");
    }
    private void RemoveDuplicatedSeparators(ContextMenu mnu) {
      var duplicatedSeparators = new List<int>();
      int newCount = User32.GetMenuItemCount(mnu.Handle);
      for (uint i = 0; i < newCount - 1; i++) {
        var info = new MENUITEMINFO();
        info.cbSize = MENUITEMINFO.SizeOf;
        info.fMask = MIIM.MIIM_FTYPE | MIIM.MIIM_DATA | MIIM.MIIM_STRING | MIIM.MIIM_SUBMENU;
        if (User32.GetMenuItemInfo(mnu.Handle, i, true, ref info)) {
          var isSep = (info.fType & MFT.MFT_SEPARATOR) != 0;
          if (isSep) {
            var info2 = new MENUITEMINFO();
            info2.cbSize = MENUITEMINFO.SizeOf;
            info2.fMask = MIIM.MIIM_FTYPE | MIIM.MIIM_DATA | MIIM.MIIM_STRING | MIIM.MIIM_SUBMENU;
            if (User32.GetMenuItemInfo(mnu.Handle, i + 1, true, ref info2)) {
              var isSep2 = (info2.fType & MFT.MFT_SEPARATOR) != 0;
              if (isSep2) {
                duplicatedSeparators.Add((int)i + 1);
              }
            }
          }
        }
      }
      duplicatedSeparators.Reverse();
      duplicatedSeparators.ForEach(a => User32.DeleteMenu(mnu.Handle, a, MF.MF_BYPOSITION));
    }
    private void RemoveDefaultExplorerItems(ContextMenu mnu, Control control, ref MENUITEMINFO itemInfo) {
      User32.DeleteMenu(mnu.Handle, 0, MF.MF_BYPOSITION);
      User32.DeleteMenu(mnu.Handle, 0, MF.MF_BYPOSITION);
      User32.DeleteMenu(mnu.Handle, 0, MF.MF_BYPOSITION);
      User32.DeleteMenu(mnu.Handle, 0, MF.MF_BYPOSITION);
      User32.DeleteMenu(mnu.Handle, 0, MF.MF_BYPOSITION);
      User32.DeleteMenu(mnu.Handle, 0, MF.MF_BYPOSITION);
      User32.DeleteMenu(mnu.Handle, 0, MF.MF_BYPOSITION);
      User32.DeleteMenu(mnu.Handle, 0, MF.MF_BYPOSITION);
      if (!(control as ShellView).CurrentFolder.IsDrive && (control as ShellView).CurrentFolder.IsFileSystem && !(control as ShellView).CurrentFolder.IsNetworkPath && !(control as ShellView).CurrentFolder.ParsingName.StartsWith(@"\\")) {
        User32.GetMenuItemInfo(mnu.Handle, 1, true, ref itemInfo);
        if ((itemInfo.fType & MFT.MFT_SEPARATOR) != 0) {
          User32.DeleteMenu(mnu.Handle, 0, MF.MF_BYPOSITION);
        } else {
          if (User32.GetMenuItemID(mnu.Handle, 1) - m_CmdFirst == 1) {
            User32.DeleteMenu(mnu.Handle, 0, MF.MF_BYPOSITION);
            User32.DeleteMenu(mnu.Handle, 0, MF.MF_BYPOSITION);
          }
        }
      }
      if ((control as ShellView).CurrentFolder.IsNetworkPath) {
        User32.GetMenuItemInfo(mnu.Handle, 1, true, ref itemInfo);
        if ((itemInfo.fType & MFT.MFT_SEPARATOR) != 0) {
          User32.DeleteMenu(mnu.Handle, 0, MF.MF_BYPOSITION);
          User32.DeleteMenu(mnu.Handle, 0, MF.MF_BYPOSITION);
        }
      }
      
    }
    private void GenerateSubmenu(ContextMenu child, ContextMenu parent, String header) {
      MENUITEMINFO miiview = new MENUITEMINFO();
      miiview.cbSize = MENUITEMINFO.SizeOf;
      miiview.fMask = MIIM.MIIM_STRING | MIIM.MIIM_FTYPE | MIIM.MIIM_STATE | MIIM.MIIM_SUBMENU;
      miiview.fState = 0x0;
      miiview.fType = 0;
      miiview.hSubMenu = child.Handle;
      miiview.dwItemData = IntPtr.Zero;
      miiview.dwTypeData = header;
      User32.InsertMenuItem(parent.Handle, 0, true, ref miiview);
    }
    private void GenerateMenuItem(ContextMenu view, String header, int id, bool isRadio = false, uint atPosition = 0) {
      MENUITEMINFO miidetails = new MENUITEMINFO();
      miidetails.cbSize = MENUITEMINFO.SizeOf;
      miidetails.fMask = MIIM.MIIM_STRING | MIIM.MIIM_ID | MIIM.MIIM_FTYPE | MIIM.MIIM_STATE;
      miidetails.fState = (MFS)(isRadio ? 0x00000008 : 0x0);
      miidetails.fType = (MFT)(0 | 0x00000200);
      miidetails.wID = (uint)id;
      miidetails.dwItemData = IntPtr.Zero;
      miidetails.dwTypeData = header;
      User32.InsertMenuItem(view.Handle, atPosition, true, ref miidetails);
    }
    private void GenerateMenuItemExecutable(ContextMenu view, String header, int id) {
      MENUITEMINFO miidetails = new MENUITEMINFO();
      miidetails.cbSize = MENUITEMINFO.SizeOf;
      miidetails.fMask = MIIM.MIIM_STRING | MIIM.MIIM_ID | MIIM.MIIM_FTYPE | MIIM.MIIM_STATE;
      miidetails.fState = 0x0;
      miidetails.fType = 0;
      miidetails.wID = (uint)id;
      miidetails.dwItemData = IntPtr.Zero;
      miidetails.dwTypeData = header;
      User32.InsertMenuItem(view.Handle, 0, true, ref miidetails);
    }
    private void GenerateSeparator(ContextMenu view) {
      MENUITEMINFO miidetails = new MENUITEMINFO();
      miidetails.cbSize = MENUITEMINFO.SizeOf;
      miidetails.fMask = MIIM.MIIM_FTYPE;
      miidetails.fType = MFT.MFT_SEPARATOR;
      User32.InsertMenuItem(view.Handle, 0, true, ref miidetails);
    }
    void Initialize(IListItemEx[] items) {
      this._Items = items;
      IntPtr[] pidls = new IntPtr[items.Length];
      IListItemEx parent = null;

      for (int n = 0; n < items.Length; ++n) {
        pidls[n] = items[n].ILPidl;

        if (parent == null) {
          if (items[n].ParsingName.Equals(ShellItem.Desktop.ParsingName)) {
            parent = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, ShellItem.Desktop.Pidl);
          } else {
            parent = items[n].Parent;
          }
        } else if (!items[n].Parent.Equals(parent)) {
          throw new Exception("All shell items must have the same parent");
        }
      }

      if (items.Length == 0) {
        var desktop = KnownFolders.Desktop as ShellItem;
        var ishellViewPtr = desktop.GetIShellFolder().CreateViewObject(IntPtr.Zero, typeof(IShellView).GUID);
        var view = Marshal.GetObjectForIUnknown(ishellViewPtr) as IShellView;
        view.GetItemObject(SVGIO.SVGIO_BACKGROUND, typeof(IContextMenu).GUID, out _Result);
        Marshal.ReleaseComObject(view);
      } else {
        parent.GetIShellFolder().GetUIObjectOf(IntPtr.Zero, (uint)pidls.Length, pidls, typeof(IContextMenu).GUID, 0, out _Result);
      }

      m_ComInterface = (IContextMenu)Marshal.GetTypedObjectForIUnknown(_Result, typeof(IContextMenu));
      m_ComInterface2 = m_ComInterface as IContextMenu2;
      m_ComInterface3 = m_ComInterface as IContextMenu3;
      m_MessageWindow = new MessageWindow(this);
    }

    IntPtr _Result = IntPtr.Zero;

    void Initialize(IListItemEx item) {
      Guid iise = typeof(IShellExtInit).GUID;
      var ishellViewPtr = (item.IsDrive || !item.IsFileSystem || item.IsNetworkPath) ? item.GetIShellFolder().CreateViewObject(IntPtr.Zero, typeof(IShellView).GUID) : item.Parent.GetIShellFolder().CreateViewObject(IntPtr.Zero, typeof(IShellView).GUID);
      var view = Marshal.GetObjectForIUnknown(ishellViewPtr) as IShellView;
      view?.GetItemObject(SVGIO.SVGIO_BACKGROUND, typeof(IContextMenu).GUID, out _Result);
      if (view != null)
        Marshal.ReleaseComObject(view);
      m_ComInterface = (IContextMenu)Marshal.GetTypedObjectForIUnknown(_Result, typeof(IContextMenu));
      m_ComInterface2 = m_ComInterface as IContextMenu2;
      m_ComInterface3 = m_ComInterface as IContextMenu3;
      IntPtr iShellExtInitPtr;
      if (Marshal.QueryInterface(_Result, ref iise, out iShellExtInitPtr) == (int)HResult.S_OK) {
        var iShellExtInit = Marshal.GetTypedObjectForIUnknown(iShellExtInitPtr, typeof(IShellExtInit)) as IShellExtInit;

        try {
          var hhh = IntPtr.Zero;
          iShellExtInit?.Initialize(_ShellView.CurrentFolder.PIDL, null, 0);
          if (iShellExtInit != null)
            Marshal.ReleaseComObject(iShellExtInit);
          Marshal.Release(iShellExtInitPtr);
        } catch {

        }
      }
      m_MessageWindow = new MessageWindow(this);
    }

    void InvokeCommand(IntPtr command, Point pt, IntPtr ansiCommand) {
      const int SW_SHOWNORMAL = 1;
      var invoke = new CMINVOKECOMMANDINFOEX();
      invoke.cbSize = Marshal.SizeOf(invoke);
      invoke.nShow = SW_SHOWNORMAL;
      invoke.fMask = CMIC.FlagNoUi | CMIC.Unicode;
      invoke.lpVerb = ansiCommand;
      invoke.lpVerbW = command;
      //invoke.ptInvoke = pt;
      m_ComInterface.InvokeCommand(ref invoke);
    }

    void TagManagedMenuItems(Menu menu, int tag) {
      var info = new MENUINFO();
      info.cbSize = Marshal.SizeOf(info);
      info.fMask = MIM.MIM_MENUDATA;
      info.dwMenuData = tag;

      foreach (MenuItem item in menu.MenuItems) {
        User32.SetMenuInfo(item.Handle, ref info);
      }
    }

    void RemoveShellMenuItems(Menu menu) {
      const int tag = 0xAB;
      var remove = new List<int>();
      int count = User32.GetMenuItemCount(menu.Handle);
      var menuInfo = new MENUINFO();
      var itemInfo = new MENUITEMINFO();

      menuInfo.cbSize = Marshal.SizeOf(menuInfo);
      menuInfo.fMask = MIM.MIM_MENUDATA;
      itemInfo.cbSize = MENUITEMINFO.SizeOf;
      itemInfo.fMask = MIIM.MIIM_ID | MIIM.MIIM_SUBMENU;

      // First, tag the managed menu items with an arbitary 
      // value (0xAB).
      TagManagedMenuItems(menu, tag);

      for (int n = 0; n < count; ++n) {
        User32.GetMenuItemInfo(menu.Handle, (uint)n, true, ref itemInfo);

        if (itemInfo.hSubMenu == IntPtr.Zero) {
          // If the item has no submenu we can't get the tag, so 
          // check its ID to determine if it was added by the shell.
          if (itemInfo.wID >= m_CmdFirst)
            remove.Add(n);
        } else {
          User32.GetMenuInfo(itemInfo.hSubMenu, ref menuInfo);
          if (menuInfo.dwMenuData != tag)
            remove.Add(n);
        }
      }

      // Remove the unmanaged menu items.
      remove.Reverse();
      foreach (int position in remove) {
        User32.DeleteMenu(menu.Handle, position, MF.MF_BYPOSITION);
      }
    }

    public bool GetNewContextMenu(IListItemEx item, out IntPtr iContextMenuPtr, out IContextMenu iContextMenu) {
      Guid CLSID_NewMenu = new Guid("{D969A300-E7FF-11d0-A93B-00A0C90F2719}");
      Guid iicm = typeof(IContextMenu).GUID;
      Guid iise = typeof(IShellExtInit).GUID;
      if (Ole32.CoCreateInstance(
              ref CLSID_NewMenu,
              IntPtr.Zero,
              Ole32.CLSCTX.INPROC_SERVER,
              ref iicm,
              out iContextMenuPtr) == (int)HResult.S_OK) {
        iContextMenu = Marshal.GetObjectForIUnknown(iContextMenuPtr) as IContextMenu;

        IntPtr iShellExtInitPtr;
        if (Marshal.QueryInterface(
            iContextMenuPtr,
            ref iise,
            out iShellExtInitPtr) == (int)HResult.S_OK) {
          IShellExtInit iShellExtInit = Marshal.GetTypedObjectForIUnknown(
              iShellExtInitPtr, typeof(IShellExtInit)) as IShellExtInit;

          try {
            iShellExtInit.Initialize(item.PIDL, null, 0);

            Marshal.ReleaseComObject(iShellExtInit);
            Marshal.Release(iShellExtInitPtr);
            return true;
          } finally {

          }
        } else {
          if (iContextMenu != null) {
            Marshal.ReleaseComObject(iContextMenu);
            iContextMenu = null;
          }

          if (iContextMenuPtr != IntPtr.Zero) {
            Marshal.ReleaseComObject(iContextMenuPtr);
            iContextMenuPtr = IntPtr.Zero;
          }

          return false;
        }
      } else {
        iContextMenuPtr = IntPtr.Zero;
        iContextMenu = null;
        return false;
      }
    }

    public bool GetOpenWithContextMenu(IListItemEx[] itemArray, out IntPtr iContextMenuPtr, out IContextMenu iContextMenu) {
      Guid CLSID_OpenWith = new Guid(0x09799AFB, 0xAD67, 0x11d1, 0xAB, 0xCD, 0x00, 0xC0, 0x4F, 0xC3, 0x09, 0x36);
      Guid iicm = typeof(IContextMenu).GUID;
      Guid iise = typeof(IShellExtInit).GUID;
      if (Ole32.CoCreateInstance(
              ref CLSID_OpenWith,
              IntPtr.Zero,
              Ole32.CLSCTX.INPROC_SERVER,
              ref iicm,
              out iContextMenuPtr) == (int)HResult.S_OK) {
        iContextMenu = Marshal.GetObjectForIUnknown(iContextMenuPtr) as IContextMenu;

        IntPtr iShellExtInitPtr;
        if (Marshal.QueryInterface(
            iContextMenuPtr,
            ref iise,
            out iShellExtInitPtr) == (int)HResult.S_OK) {
          IShellExtInit iShellExtInit = Marshal.GetTypedObjectForIUnknown(iShellExtInitPtr, typeof(IShellExtInit)) as IShellExtInit;

          try {
            IntPtr doPtr;
            iShellExtInit.Initialize(IntPtr.Zero, itemArray.GetIDataObject(out doPtr), 0);

            Marshal.ReleaseComObject(iShellExtInit);
            Marshal.Release(iShellExtInitPtr);
            return true;
          } catch { } 
        } else {
          if (iContextMenu != null) {
            Marshal.ReleaseComObject(iContextMenu);
            iContextMenu = null;
          }

          return false;
        }
      } else {
        iContextMenuPtr = IntPtr.Zero;
        iContextMenu = null;
        return false;
      }
      return false;
    }

    class MessageWindow : Control {
      public MessageWindow(ShellContextMenu parent) {
        m_Parent = parent;
      }

      protected override void WndProc(ref Message m) {
        if (!m_Parent.HandleMenuMessage(ref m)) {
          base.WndProc(ref m);
        }
      }

      ShellContextMenu m_Parent;
    }
  }
}
