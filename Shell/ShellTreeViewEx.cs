﻿namespace BExplorer.Shell {
  using System;
  using System.Collections.Generic;
  using System.Drawing;
  using System.IO;
  using System.Linq;
  using System.Runtime.ExceptionServices;
  using System.Runtime.InteropServices;
  using System.Runtime.InteropServices.ComTypes;
  using System.Security;
  using System.Threading;
  using System.Windows.Forms;
  using BExplorer.Shell._Plugin_Interfaces;
  using BExplorer.Shell.Interop;
  using F = System.Windows.Forms;

  public partial class ShellTreeViewEx : UserControl {

    #region Public Members

    /// <summary>Do you want to show hidden items/nodes in the list</summary>
    public bool IsShowHiddenItems { get; set; }

    /// <summary>The <see cref="ShellView">List</see> that this is paired with</summary>
    public ShellView ShellListView {
      private get { return this._ShellListView; }

      set {
        this._ShellListView = value;
        this._ShellListView.Navigated += this.ShellListView_Navigated;
      }
    }

    #endregion

    #region Event Handlers

    public event EventHandler<TreeNodeMouseClickEventArgs> NodeClick;

    public event EventHandler<NavigatedEventArgs> AfterSelect;

    #endregion Event Handlers

    #region Private Members

    private TreeViewBase ShellTreeView;
    private List<string> _PathsToBeAdd = new List<string>();
    private string _SearchingForFolders = "Searching for folders...";

    private TreeNode cuttedNode { get; set; }

    private ManualResetEvent _ResetEvent = new ManualResetEvent(true);
    private int folderImageListIndex;
    private ShellView _ShellListView;
    private List<IntPtr> UpdatedImages = new List<IntPtr>();
    private List<IntPtr> CheckedFroChilds = new List<IntPtr>();
    private SyncQueue<IntPtr> imagesQueue = new SyncQueue<IntPtr>(); // 7000
    private SyncQueue<IntPtr> childsQueue = new SyncQueue<IntPtr>(); // 7000
    private Thread imagesThread;
    private Thread childsThread;
    private bool isFromTreeview;
    private bool _IsNavigate;
    private string _EmptyItemString = "<!EMPTY!>";
    private bool _AcceptSelection = true;
    private ShellNotifications _NotificationNetWork = new ShellNotifications();
    private ShellNotifications _NotificationGlobal = new ShellNotifications();

    private System.Runtime.InteropServices.ComTypes.IDataObject _DataObject { get; set; }

    #endregion

    #region Private Methods

    private void InitRootItems() {
      this.imagesQueue.Clear();
      this.childsQueue.Clear();
      this.UpdatedImages.Clear();
      this.CheckedFroChilds.Clear();
      var favoritesItem = Utilities.WindowsVersion == WindowsVersions.Windows10
        ? FileSystemListItem.ToFileSystemItem(IntPtr.Zero, "shell:::{679f85cb-0220-4080-b29b-5540cc05aab6}")
        : FileSystemListItem.ToFileSystemItem(IntPtr.Zero, ((ShellItem)KnownFolders.Links).Pidl);
      var favoritesRoot = new TreeNode(favoritesItem.DisplayName);
      favoritesRoot.Tag = favoritesItem;
      favoritesRoot.ImageIndex = favoritesItem.GetSystemImageListIndex(favoritesItem.PIDL, ShellIconType.SmallIcon, ShellIconFlags.OpenIcon);
      favoritesRoot.SelectedImageIndex = favoritesRoot.ImageIndex;

      if (favoritesItem.Count() > 0) {
        favoritesRoot.Nodes.Add(this._EmptyItemString);
      }

      var librariesItem = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, ((ShellItem)KnownFolders.Libraries).Pidl);
      var librariesRoot = new TreeNode(librariesItem.DisplayName);
      librariesRoot.Tag = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, ((ShellItem)KnownFolders.Libraries).Pidl);
      librariesRoot.ImageIndex = librariesItem.GetSystemImageListIndex(librariesItem.PIDL, ShellIconType.SmallIcon, ShellIconFlags.OpenIcon);
      librariesRoot.SelectedImageIndex = librariesRoot.ImageIndex;
      if (librariesItem.HasSubFolders) {
        librariesRoot.Nodes.Add(this._EmptyItemString);
      }

      var computerItem = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, ((ShellItem)KnownFolders.Computer).Pidl);
      var computerRoot = new TreeNode(computerItem.DisplayName);
      computerRoot.Tag = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, ((ShellItem)KnownFolders.Computer).Pidl);
      computerRoot.ImageIndex = computerItem.GetSystemImageListIndex(computerItem.PIDL, ShellIconType.SmallIcon, ShellIconFlags.OpenIcon);
      computerRoot.SelectedImageIndex = computerRoot.ImageIndex;
      if (computerItem.HasSubFolders) {
        computerRoot.Nodes.Add(this._EmptyItemString);
      }

      var networkItem = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, ((ShellItem)KnownFolders.Network).Pidl);
      var networkRoot = new TreeNode(networkItem.DisplayName);
      networkRoot.Tag = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, ((ShellItem)KnownFolders.Network).Pidl);
      networkRoot.ImageIndex = networkItem.GetSystemImageListIndex(networkItem.PIDL, ShellIconType.SmallIcon, ShellIconFlags.OpenIcon);
      networkRoot.SelectedImageIndex = networkRoot.ImageIndex;
      networkRoot.Nodes.Add(this._EmptyItemString);

      this.ShellTreeView.Nodes.Add(favoritesRoot);
      favoritesRoot.Expand();

      this.ShellTreeView.Nodes.AddRange(new[] { new TreeNode(), librariesRoot, new TreeNode(), computerRoot, new TreeNode(), networkRoot });

      librariesRoot.Expand();
      computerRoot.Expand();
    }

    private void SetNodeImage(IntPtr node, IListItemEx item, IntPtr m_TreeViewHandle, bool isOverlayed) {
      try {
        var itemInfo = new TVITEMW();

        // We need to set the images for the item by sending a
        // TVM_SETITEMW message, as we need to set the overlay images,
        // and the .Net TreeView API does not support overlays.
        itemInfo.mask = TVIF.TVIF_IMAGE | TVIF.TVIF_SELECTEDIMAGE | TVIF.TVIF_STATE;
        itemInfo.hItem = node;
        var iconImageIndex = item.GetSystemImageListIndex(item.PIDL, ShellIconType.SmallIcon, 0);
        itemInfo.iImage = iconImageIndex;
        if (isOverlayed) {
          itemInfo.state = (TVIS)(itemInfo.iImage >> 16);
          itemInfo.stateMask = TVIS.TVIS_OVERLAYMASK;
        }

        itemInfo.iSelectedImage = item.GetSystemImageListIndex(item.PIDL, ShellIconType.SmallIcon, ShellIconFlags.OpenIcon);
        this.UpdatedImages.Add(node);
        User32.SendMessage(m_TreeViewHandle, MSG.TVM_SETITEMW, 0, ref itemInfo);
      } catch (Exception) {
      }
    }

    private TreeNode FromItem(IListItemEx item, TreeNode rootNode) {
      if (rootNode == null) {
        return null;
      }

      foreach (TreeNode node in rootNode.Nodes) {
        if ((node?.Tag as IListItemEx)?.Equals(item) == true) {
          return node;
        }

        TreeNode next = this.FromItem(item, node);
        if (next != null) {
          return next;
        }
      }

      return null;
    }

    private TreeNode FromItem(IListItemEx item) {
      foreach (TreeNode node in this.ShellTreeView.Nodes.OfType<TreeNode>().Where(w => {
        var nodeItem = w.Tag as IListItemEx;
        //if (nodeItem != null) {
        //  nodeItem = FileSystemListItem.ToFileSystemItem(item.ParentHandle, nodeItem.PIDL);
        //}
        return nodeItem != null && (w.Tag != null && !nodeItem.ParsingName.Equals(KnownFolders.Links.ParsingName) && !nodeItem.ParsingName.Equals("::{679f85cb-0220-4080-b29b-5540cc05aab6}"));
      })) {
        if ((node.Tag as IListItemEx)?.Equals(item) == true) {
          return node;
        }

        TreeNode next = this.FromItem(item, node);
        if (next != null) {
          return next;
        }
      }

      return null;
    }

    Stack<IListItemEx> parents = new Stack<IListItemEx>();

    private void FindItem(IListItemEx item) {
      var nodeNext = this.ShellTreeView.Nodes.OfType<TreeNode>().FirstOrDefault(s => s.Tag != null && (s.Tag as IListItemEx).Equals(item));
      if (nodeNext == null) {
        this.parents.Push(item);
        if (item.Parent != null) {
          this.BeginInvoke((Action)(() => { this.FindItem(item.Parent.Clone()); }));
        }
      } else {
        while (this.parents.Count > 0) {
          var obj = this.parents.Pop();
          this.BeginInvoke((Action)(() => {
            var newNode = this.FromItem(obj);
            if (newNode != null && !newNode.IsExpanded) {
              newNode.Expand();
            }
          }));
        }
      }
    }

    private void SelItem(IListItemEx item) {
      // item = FileSystemListItem.ToFileSystemItem(item.ParentHandle, item.PIDL);
      var node = this.FromItem(item);
      if (node == null) {
        this.FindItem(item.Clone());
      } else {
        this.BeginInvoke((Action)(() => { this.ShellTreeView.SelectedNode = node; }));
      }
    }

    private void DeleteItem(IListItemEx item) {
      TreeNode itemNode = null;
      foreach (TreeNode node in this.ShellTreeView.Nodes) {
        itemNode = this.FromItem(item, node);
        if (itemNode != null) {
          break;
        }
      }

      itemNode?.Remove();
    }

    private void AddItem(IListItemEx item) {
      TreeNode itemNode = null;
      foreach (TreeNode node in this.ShellTreeView.Nodes) {
        try {
          itemNode = this.FromItem(item.Parent, node);
        } catch (Exception) {
          continue;
        }

        if (itemNode != null) {
          break;
        }
      }

      if (itemNode != null) {
        var node = new TreeNode(item.DisplayName);
        var itemReal = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, item.PIDL);
        node.Tag = itemReal;
        var oldnodearray = itemNode.Nodes.OfType<TreeNode>().ToList();
        if (!oldnodearray.Any(s => s.Tag != null && ((IListItemEx)s.Tag).Equals(itemReal))) {
          oldnodearray.Add(node);
        }

        this.ShellTreeView.BeginUpdate();
        itemNode.Nodes.Clear();
        itemNode.Nodes.AddRange(oldnodearray.OrderBy(o => o.Text).ToArray());
        this.ShellTreeView.EndUpdate();

        if (itemNode?.Nodes?.Count > 0) {
          var newNode = itemNode.Nodes.OfType<TreeNode>().FirstOrDefault(s => s.Tag != null && ((IListItemEx)s.Tag).Equals(itemReal));
          if (newNode != null) {
            this.SetNodeImage(newNode.Handle, itemReal, this.ShellTreeView.Handle, !(newNode.Parent?.Tag != null && ((IListItemEx)newNode.Parent.Tag).ParsingName == KnownFolders.Links.ParsingName));
          }
        }
      }
    }

    private void RenameItem(IListItemEx prevItem, IListItemEx newItem) {
      if (!newItem.Equals(prevItem)) {
        this.DeleteItem(prevItem);
        this.AddItem(newItem);
      }
    }

    private void InitTreeView() {
      this.AllowDrop = true;
      this.ShellTreeView = new TreeViewBase();
      this.ShellTreeView.Dock = DockStyle.Fill;
      this.ShellTreeView.BackColor = Color.White;
      this.ShellTreeView.BorderStyle = BorderStyle.None;
      this.ShellTreeView.AllowDrop = true;
      this.ShellTreeView.HideSelection = false;
      this.ShellTreeView.ShowLines = false;
      this.ShellTreeView.HotTracking = true;
      this.ShellTreeView.LabelEdit = true;
      this.ShellTreeView.DrawMode = TreeViewDrawMode.OwnerDrawAll;
      this.ShellTreeView.DrawNode += this.ShellTreeView_DrawNode;
      this.ShellTreeView.BeforeExpand += this.ShellTreeView_BeforeExpand;
      this.ShellTreeView.AfterExpand += this.ShellTreeView_AfterExpand;
      this.ShellTreeView.MouseDown += this.ShellTreeView_MouseDown;
      this.ShellTreeView.HandleDestroyed += this.ShellTreeView_HandleDestroyed;
      this.ShellTreeView.ItemDrag += this.ShellTreeView_ItemDrag;
      this.ShellTreeView.AfterSelect += this.ShellTreeView_AfterSelect;
      this.ShellTreeView.NodeMouseClick += this.ShellTreeView_NodeMouseClick;
      this.ShellTreeView.AfterLabelEdit += this.ShellTreeView_AfterLabelEdit;
      this.ShellTreeView.KeyDown += this.ShellTreeView_KeyDown;
      this.ShellTreeView.DragEnter += this.ShellTreeView_DragEnter;
      this.ShellTreeView.DragOver += this.ShellTreeView_DragOver;
      this.ShellTreeView.DragLeave += this.ShellTreeView_DragLeave;
      this.ShellTreeView.DragDrop += this.ShellTreeView_DragDrop;
      this.ShellTreeView.GiveFeedback += this.ShellTreeView_GiveFeedback;
      this.ShellTreeView.MouseMove += this.ShellListView_MouseMove;
      this.ShellTreeView.MouseEnter += this.ShellTreeView_MouseEnter;
      this.ShellTreeView.MouseLeave += this.ShellTreeView_MouseLeave;

      // this.ShellTreeView.MouseWheel += ShellTreeView_MouseWheel;
      // this.ShellTreeView.VerticalScroll += ShellTreeView_VerticalScroll;
      // this.ShellTreeView.BeforeSelect += ShellTreeView_BeforeSelect;
      if (this.ShellListView != null) {
        this.ShellListView.Navigated += this.ShellListView_Navigated;
      }

      SystemImageList.UseSystemImageList(this.ShellTreeView);
      this.ShellTreeView.FullRowSelect = true;
      var defIconInfo = new Shell32.SHSTOCKICONINFO() { cbSize = (uint)Marshal.SizeOf(typeof(Shell32.SHSTOCKICONINFO)) };
      Shell32.SHGetStockIconInfo(Shell32.SHSTOCKICONID.SIID_FOLDER, Shell32.SHGSI.SHGSI_SYSICONINDEX, ref defIconInfo);
      this.folderImageListIndex = defIconInfo.iSysIconIndex;
      this.imagesThread = new Thread(new ThreadStart(this.LoadTreeImages)) { IsBackground = true };
      this.imagesThread.SetApartmentState(ApartmentState.STA);
      this.imagesThread.Start();
      this.childsThread = new Thread(new ThreadStart(this.LoadChilds)) { IsBackground = true };
      this.childsThread.Start();
    }

    private void ShellTreeView_GiveFeedback(object sender, GiveFeedbackEventArgs e) {
      e.UseDefaultCursors = true;
      var doo = new System.Windows.Forms.DataObject(this._DataObject);
      if (doo.GetDataPresent("DragWindow")) {
        IntPtr hwnd = ShellView.GetIntPtrFromData(doo.GetData("DragWindow"));
        User32.PostMessage(hwnd, 0x403, IntPtr.Zero, IntPtr.Zero);
      } else {
        e.UseDefaultCursors = true;
      }

      if (ShellView.IsDropDescriptionValid(this._DataObject)) {
        e.UseDefaultCursors = false;
        Cursor.Current = Cursors.Arrow;
      } else {
        e.UseDefaultCursors = true;
      }

      if (ShellView.IsShowingLayered(doo)) {
        e.UseDefaultCursors = false;
        Cursor.Current = Cursors.Arrow;
      } else {
        e.UseDefaultCursors = true;
      }

      this.OnGiveFeedback(e);
    }

    private void ShellTreeView_MouseLeave(object sender, EventArgs e) {
      if (this.ShellListView != null) {
        this.ShellListView.IsFocusAllowed = true;
      }
    }

    private void ShellTreeView_MouseEnter(object sender, EventArgs e) {
      if (this.ShellListView != null) {
        this.ShellListView.IsFocusAllowed = false;
      }

      this.ShellTreeView.Focus();
    }

    private void ShellListView_MouseMove(object sender, MouseEventArgs e) {
      if (!this.ShellTreeView.Focused) {
        this.ShellTreeView.Focus();
      }
    }

    private void ShellTreeView_MouseDown(object sender, MouseEventArgs e) {
      this.isFromTreeview = true;
      this.NodeClick?.Invoke(this, new TreeNodeMouseClickEventArgs(this.ShellTreeView.GetNodeAt(e.X, e.Y), e.Button, e.Clicks, e.X, e.Y));
    }

    private void RequestTreeImage(IntPtr handle) {
      var thread = new Thread(() => {
        TreeNode node = null;
        IntPtr treeHandle = IntPtr.Zero;
        IListItemEx itemReal = null;
        var visible = false;
        if (this.ShellTreeView != null) {
          this.ShellTreeView.Invoke((Action)(() => {
            try {
              node = TreeNode.FromHandle(this.ShellTreeView, handle);
              treeHandle = this.ShellTreeView.Handle;
              if (node != null) {
                visible = node.IsVisible;
                var item = node.Tag as IListItemEx;
                if (item != null) {
                  itemReal = item;
                }
              }
            } catch {
            }

          }));

          if (visible && itemReal != null) {
            var nodeHandle = handle;
            // Thread.Sleep(1);
            // Application.DoEvents();

            this.SetNodeImage(nodeHandle, itemReal, treeHandle, !(node.Parent != null && (node.Parent.Tag as IListItemEx)?.ParsingName == KnownFolders.Links.ParsingName));
          }
        }
      });
      thread.SetApartmentState(ApartmentState.STA);
      thread.Start();
    }

    /// <summary>Loads the images for each node in a permanent loop</summary>
    private void LoadTreeImages() {
      // return;
      while (true) {
        this._ResetEvent.WaitOne();
        Thread.Sleep(1);
        //Application.DoEvents();
        var handle = this.imagesQueue.Dequeue();
        TreeNode node = null;
        IntPtr treeHandle = IntPtr.Zero;

        // var hash = -1;
        IListItemEx itemReal = null;
        var visible = false;
        this.ShellTreeView?.BeginInvoke((Action)(() => {
          node = TreeNode.FromHandle(this.ShellTreeView, handle);
          treeHandle = this.ShellTreeView.Handle;
          if (node != null) {
            visible = node.IsVisible;
            itemReal = node.Tag as IListItemEx;
          }
        }));

        if (visible && itemReal != null) {
          var nodeHandle = handle;

          // Thread.Sleep(1);
          // Application.DoEvents();
          this.SetNodeImage(nodeHandle, itemReal, treeHandle, !(node?.Parent != null && (node?.Parent.Tag as IListItemEx)?.ParsingName == KnownFolders.Links.ParsingName));
        }
      }
    }

    private void RequestLoadChilds(IntPtr handle) {
      new Thread(() => {
        // Application.DoEvents();
        // Thread.Sleep(1);
        // this._ResetEvent.WaitOne();
        TreeNode node = null;
        IntPtr treeHandle = IntPtr.Zero;
        var visible = true;

        // var pidl = IntPtr.Zero;
        this.ShellTreeView?.BeginInvoke((Action)(() => {
          node = TreeNode.FromHandle(this.ShellTreeView, handle);
          treeHandle = this.ShellTreeView.Handle;
          if (node != null) {
            visible = node.IsVisible;
          }
        }));

        if (!visible) {
          return;
        }

        // if (node != null && node.Nodes.Count > 0)
        if (node?.Nodes?.Count > 0) {
          var childItem = node.Nodes[0];
          if (childItem != null) {
            // TODO: Try to remove this Try Catch! It's slowing this down!!
            try {
              var sho = node.Tag as IListItemEx;
              if (!sho.HasSubFolders) {
                User32.SendMessage(treeHandle, MSG.TVM_DELETEITEM, 0, childItem.Handle);
              }

              this.CheckedFroChilds.Add(handle);
            } catch (Exception) {
            }
          }
        }
      }).Start();
    }

    private void LoadChilds() {
      while (true) {
        // this._ResetEvent.WaitOne();
        var handle = this.childsQueue.Dequeue();
        TreeNode node = null;
        IntPtr treeHandle = IntPtr.Zero;
        var visible = true;

        // var pidl = IntPtr.Zero;
        this.ShellTreeView?.BeginInvoke((Action)(() => {
          node = TreeNode.FromHandle(this.ShellTreeView, handle);
          treeHandle = this.ShellTreeView.Handle;
          if (node != null) {
            visible = node.IsVisible;
          }
        }));

        if (!visible) {
          continue;
        }

        if (node?.Nodes?.Count > 0) {
          var childItem = node.Nodes[0];
          if (childItem != null) {
            var nodeHandle = childItem.Handle;

            try {
              var sho = node.Tag as IListItemEx;
              if (!sho.HasSubFolders) {
                User32.SendMessage(treeHandle, MSG.TVM_DELETEITEM, 0, nodeHandle);
              }

              this.CheckedFroChilds.Add(handle);
            } catch (Exception) {
            }
          }
        }
      }
    }

    /// <summary>Sets up the UI to allow the user to edit the currently selected node if and only if it is not currently being edited</summary>
    private void RenameSelectedNode() {
      var node = this.ShellTreeView.SelectedNode;
      if (node != null && !node.IsEditing) {
        node.BeginEdit();
      }
    }

    /// <summary>
    /// Moves the selected items to the destination on a separate thread
    /// </summary>
    /// <param name="dataObject">Contains the items you want to moe</param>
    /// <param name="destination">The place you want to move the items to</param>
    private void DoMove(F.IDataObject dataObject, IListItemEx destination) {
      var handle = this.Handle;
      var thread = new Thread(() => {
        IShellItem[] items = dataObject.GetDataPresent("FileDrop")
          ? items = ((F.DataObject)dataObject).GetFileDropList().OfType<string>().Select(s => FileSystemListItem.ToFileSystemItem(IntPtr.Zero, s.ToShellParsingName()).ComInterface).ToArray()
          : dataObject.ToShellItemArray().ToArray();

        try {
          var fo = new IIFileOperation(handle);
          foreach (IShellItem item in items) {
            fo.MoveItem(item, destination, null);
          }

          fo.PerformOperations();
        } catch (SecurityException) {
          throw;
        }
      });

      thread.SetApartmentState(ApartmentState.STA);
      thread.Start();
    }

    /*
		public void DoCopy(IDataObject dataObject, IListItemEx destination)
		{
				var handle = this.Handle;
				var thread = new Thread(() =>
				{
						var items = new IShellItem[0];
						if (dataObject.GetDataPresent("FileDrop"))
								items = ((F.DataObject)dataObject).GetFileDropList().OfType<String>().Select(s => FileSystemListItem.ToFileSystemItem(IntPtr.Zero, s.ToShellParsingName()).ComInterface).ToArray();
						else
								items = dataObject.ToShellItemArray().ToArray();

						try
						{
								var fo = new IIFileOperation(handle);
								foreach (var item in items)
								{
										fo.CopyItem(item, destination);
								}

								fo.PerformOperations();
						}
						catch (SecurityException)
						{
								throw;
						}
				});
				thread.SetApartmentState(ApartmentState.STA);
				thread.Start();
		}
		*/

    /// <summary>
    /// Pasted the files in the clipboard to the <see cref="ShellTreeView"/>'s currentlt <see cref="TreeView.SelectedNode">Selected Node</see> on a separate thread
    /// </summary>
    private void PasteAvailableFiles() {
      var selectedItem = this.ShellTreeView.SelectedNode.Tag as IListItemEx;
      if (selectedItem == null) {
        return;
      }

      var handle = this.Handle;
      var thread = new Thread(() => {
        var dataObject = Clipboard.GetDataObject();
        IShellItemArray items = null;
        if (dataObject.GetDataPresent("FileDrop")) {
          // TODO: Fix FileDorp option
          //items = ((String[])dataObject.GetData("FileDrop")).Select(s => ShellItem.ToShellParsingName(s).ComInterface).ToArray();
          //items = ((F.DataObject)dataObject).GetFileDropList().OfType<String>().Select(s => FileSystemListItem.ToFileSystemItem(IntPtr.Zero, s.ToShellParsingName()).ComInterface).ToArray();
        } else if (dataObject.GetDataPresent("Shell IDList Array")) {
          items = dataObject.ToShellItemArray();
        } else {
          return;
        }

        try {
          var fo = new IIFileOperation(handle);
          if (dataObject.GetDropEffect() == System.Windows.DragDropEffects.Copy) {
            fo.CopyItems(items, selectedItem);
          } else {
            fo.MoveItems(items, selectedItem);
          }

          fo.PerformOperations();
        } catch { }
      });

      thread.SetApartmentState(ApartmentState.STA);
      thread.Start();
    }

    /// <summary>Cuts the currently selected items (signals the UI and saves items into the clipboard)</summary>
    private void CutSelectedFiles() {
      var item = new TVITEMW() { mask = TVIF.TVIF_STATE, stateMask = TVIS.TVIS_CUT, state = TVIS.TVIS_CUT, hItem = this.ShellTreeView.SelectedNode.Handle, };

      User32.SendMessage(this.ShellTreeView.Handle, MSG.TVM_SETITEMW, 0, ref item);

      this.cuttedNode = this.ShellTreeView.SelectedNode;
      var selectedItems = new[] { this.ShellTreeView.SelectedNode.Tag as IListItemEx };
      var ddataObject = new F.DataObject();

      // Copy or Cut operation (5 = copy; 2 = cut)
      ddataObject.SetData("Preferred DropEffect", true, new MemoryStream(new byte[] { 2, 0, 0, 0 }));
      ddataObject.SetData("Shell IDList Array", true, selectedItems.CreateShellIDList());
      Clipboard.SetDataObject(ddataObject, true);
    }

    /// <summary>Copies the currently selected items (saves items into the clipboard)</summary>
    private void CopySelectedFiles() {
      var selectedItems = new[] { this.ShellTreeView.SelectedNode.Tag as IListItemEx };
      var ddataObject = new F.DataObject();
      ddataObject.SetData("Preferred DropEffect", true, new MemoryStream(new byte[] { 5, 0, 0, 0 }));
      ddataObject.SetData("Shell IDList Array", true, selectedItems.CreateShellIDList());
      Clipboard.SetDataObject(ddataObject, true);
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>Refreshes/rebuilds all nods (clears nodes => initializes root items => selects current folder from <see cref="ShellListView"/>)</summary>
    public void RefreshContents() {
      this.ShellTreeView.Nodes.Clear();
      this.InitRootItems();

      if (this.ShellListView?.CurrentFolder != null) {
        this.SelItem(this.ShellListView.CurrentFolder);
      }
    }

    #endregion Public Methods

    #region Events

    private void ShellTreeView_DrawNode(object sender, DrawTreeNodeEventArgs e) {
      e.DrawDefault = !string.IsNullOrEmpty(e.Node.Text);
      try {
        if (e.Node.Tag != null) {
          var item = e.Node.Tag as IListItemEx;
          if (!this.UpdatedImages.Contains(e.Node.Handle) && (item != null && item.Parent != null && item.Parent.ParsingName != KnownFolders.Network.ParsingName)) {
            this.RequestTreeImage(e.Node.Handle);
          }

          if (!this.CheckedFroChilds.Contains(e.Node.Handle)) {
            this.RequestLoadChilds(e.Node.Handle);
          }
        }
      } catch (Exception) {
        e.DrawDefault = true;

        // Do Nothing but prevent UI freeze
      }
    }

    private void ShellTreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e) {
      if (e.Action == TreeViewAction.Collapse) {
        this._AcceptSelection = false;
      }

      if (e.Action == TreeViewAction.Expand) {
        this._ResetEvent.Set();
        if (e.Node.Nodes.Count > 0 && (e.Node.Nodes[0].Text == this._EmptyItemString || e.Node.Nodes[0].Text == this._SearchingForFolders)) {
          e.Node.Nodes.Clear();
          this.imagesQueue.Clear();
          this.childsQueue.Clear();
          var sho = e.Node.Tag as IListItemEx;
          if (sho == null) {
            return;
          }
          //if (sho != null) {
          //  sho = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, sho.PIDL);
          //}
          // var lvSho = this.ShellListView != null && this.ShellListView.CurrentFolder != null ? this.ShellListView.CurrentFolder.Clone() : null;
          var lvSho = this.ShellListView?.CurrentFolder?.Clone();
          var node = e.Node;
          node.Nodes.Add(this._SearchingForFolders);
          var thread = new Thread(() => {
            var nodesTemp = new List<TreeNode>();
            if (sho?.IsLink == true) {
              try {
                var shellLink = new ShellLink(sho.ParsingName);
                var linkTarget = shellLink.TargetPIDL;
                sho = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, linkTarget);
                shellLink.Dispose();
              } catch {
              }
            }

            foreach (var item in sho.ParsingName != KnownFolders.Computer.ParsingName ? sho.GetContents(this.IsShowHiddenItems)
                                    .Where(w => (sho != null && !sho.IsFileSystem && Path.GetExtension(sho?.ParsingName)?.ToLowerInvariant() != ".library-ms") ||
                                    (w.IsFolder || w.IsLink || w.IsDrive)).OrderBy(o => o.DisplayName) : sho.GetContents(this.IsShowHiddenItems)
                                    .Where(w => (sho != null && !sho.IsFileSystem && Path.GetExtension(sho?.ParsingName)?.ToLowerInvariant() != ".library-ms") || (w.IsFolder || w.IsLink || w.IsDrive))) {
              if (item != null && !item.IsFolder && !item.IsLink) {
                continue;
              }
              if (item?.IsLink == true) {
                try {
                  var shellLink = new ShellLink(item.ParsingName);
                  var linkTarget = shellLink.TargetPIDL;
                  var itemLinkReal = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, linkTarget);
                  shellLink.Dispose();
                  if (!itemLinkReal.IsFolder) {
                    continue;
                  }
                } catch {
                }
              }

              var itemNode = new TreeNode(item.DisplayName);

              // IListItemEx itemReal = null;
              // if (item.Parent?.Parent != null && item.Parent.Parent.ParsingName == KnownFolders.Libraries.ParsingName) {
              var itemReal = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, item.PIDL);

              itemNode.Tag = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, item.PIDL);

              if (sho.IsNetworkPath && sho.ParsingName != KnownFolders.Network.ParsingName) {
                itemNode.ImageIndex = this.folderImageListIndex;
              } else if (itemReal.IconType == IExtractIconPWFlags.GIL_PERCLASS || sho.ParsingName == KnownFolders.Network.ParsingName) {
                itemNode.ImageIndex = itemReal.GetSystemImageListIndex(itemReal.PIDL, ShellIconType.SmallIcon, ShellIconFlags.OpenIcon);
                itemNode.SelectedImageIndex = itemNode.ImageIndex;
              } else {
                itemNode.ImageIndex = this.folderImageListIndex;
              }

              itemNode.Nodes.Add(this._EmptyItemString);
              if (item.ParsingName.EndsWith(".library-ms")) {
                var library = ShellLibrary.Load(Path.GetFileNameWithoutExtension(item.ParsingName), false);
                if (library.IsPinnedToNavigationPane) {
                  nodesTemp.Add(itemNode);
                }

                library.Close();
              } else {
                nodesTemp.Add(itemNode);
              }

              // Application.DoEvents();
            }

            this.BeginInvoke((Action)(() => {
              if (node.Nodes.Count == 1 && node.Nodes[0].Text == this._SearchingForFolders) {
                node.Nodes.RemoveAt(0);
              }

              node.Nodes.AddRange(nodesTemp.ToArray());
              if (lvSho != null) {
                this.SelItem(lvSho);
              }
            }));
          });
          thread.SetApartmentState(ApartmentState.STA);
          thread.Start();
        }
      }
    }

    private void ShellTreeView_HandleDestroyed(object sender, EventArgs e) {
      this.imagesThread?.Abort();
      this.childsThread?.Abort();
    }

    private void ShellTreeView_AfterExpand(object sender, TreeViewEventArgs e) {
      GC.Collect();
      this._ResetEvent.Reset();
    }

    private void ShellTreeView_AfterSelect(object sender, TreeViewEventArgs e) {
      if (!this._AcceptSelection) {
        this._AcceptSelection = true;
        return;
      }

      // var sho = e.Node != null ? e.Node.Tag as IListItemEx : null;
      var sho = e?.Node?.Tag as IListItemEx;
      if (sho != null) {
        IListItemEx linkSho = null;
        if (sho.IsLink) {
          try {
            var shellLink = new ShellLink(sho.ParsingName);
            var linkTarget = shellLink.TargetPIDL;
            linkSho = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, linkTarget);
            shellLink.Dispose();
          } catch {
          }
        }

        this.isFromTreeview = true;
        if (this._IsNavigate) {
          this.ShellListView.Navigate_Full(linkSho ?? sho, true, true);
        }

        this._IsNavigate = false;
        this.isFromTreeview = false;
      }
    }

    private void ShellListView_Navigated(object sender, NavigatedEventArgs e) {
      if (this.isFromTreeview) {
        return;
      }
      // TODO: Try to reenable this since sometimes it causes an exceptions
      var thread = new Thread(() => { this.SelItem(e.Folder); });
      thread.SetApartmentState(ApartmentState.STA);
      thread.Start();
    }

    private void ShellTreeView_ItemDrag(object sender, ItemDragEventArgs e) {
      // TODO: Finish code or remove
      IntPtr dataObjPtr = IntPtr.Zero;
      var shellItem = (e.Item as TreeNode).Tag as IListItemEx;
      if (shellItem != null) {
        // System.Runtime.InteropServices.ComTypes.IDataObject dataObject = shellItem.GetIDataObject(out dataObjPtr);

        // uint ef = 0;
        // Shell32.SHDoDragDrop(this.ShellListView.Handle, dataObject, null, unchecked((uint)F.DragDropEffects.All | (uint)F.DragDropEffects.Link), out ef);
      }
    }

    private void ShellTreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e) {
      if (e.Button == MouseButtons.Right) {
        if (e.Node.Tag != null) {
          new ShellContextMenu(this.ShellListView, e.Node.Tag as IListItemEx).ShowContextMenu(this, e.Location, CMF.CANRENAME);
        }
      } else if (e.Button == MouseButtons.Left) {
        if (e.X > e.Node.Bounds.Left - 5 - 16) {
          this._IsNavigate = true;
        }
      }
    }

    private void ShellTreeView_AfterLabelEdit(object sender, NodeLabelEditEventArgs e) {
      if (e.Label != null) {
        if (e.Label.Length > 0) {
          if (e.Label.IndexOfAny(new[] { '@', '.', ',', '!' }) == -1) {
            // Stop editing without canceling the label change.
            e.Node.EndEdit(false);
            var fo = new IIFileOperation(this.Handle);
            fo.RenameItem((e.Node.Tag as IListItemEx)?.ComInterface, e.Label);
            fo.PerformOperations();
          } else {
            /* Cancel the label edit action, inform the user, and
							 place the node in edit mode again. */
            e.CancelEdit = true;
            MessageBox.Show("Invalid tree node label.\n The invalid characters are: '@','.', ',', '!'", "Node Label Edit");
            e.Node.BeginEdit();
          }
        } else {
          /* Cancel the label edit action, inform the user, and place the node in edit mode again. */
          e.CancelEdit = true;
          MessageBox.Show("Invalid tree node label.\nThe label cannot be blank", "Node Label Edit");
          e.Node.BeginEdit();
        }
      }
    }

    private void ShellTreeView_KeyDown(object sender, KeyEventArgs e) {
      if ((ModifierKeys & Keys.Control) == Keys.Control) {
        switch (e.KeyCode) {
          case Keys.C:
            this.CopySelectedFiles();
            break;

          case Keys.V:
            this.PasteAvailableFiles();
            break;

          case Keys.X:
            this.CutSelectedFiles();
            break;
        }
      } else if (e.KeyCode == Keys.F2) {
        this.RenameSelectedNode();
      } else if (e.KeyCode == Keys.F5) {
        this.RefreshContents();
      } else if (e.KeyCode == Keys.Escape) {
        var item = new TVITEMW() { mask = TVIF.TVIF_STATE, stateMask = TVIS.TVIS_CUT, state = 0, hItem = this.cuttedNode.Handle };
        User32.SendMessage(this.ShellTreeView.Handle, MSG.TVM_SETITEMW, 0, ref item);
        Clipboard.Clear();
      }
    }

    private void ShellTreeView_DragEnter(object sender, DragEventArgs e) {
      this._DataObject = (System.Runtime.InteropServices.ComTypes.IDataObject)e.Data;
      var wp = new DataObject.Win32Point() { X = e.X, Y = e.Y };
      ShellView.Drag_SetEffect(e);

      if (e.Data.GetDataPresent("DragImageBits")) {
        DropTargetHelper.DropTarget.Create.DragEnter(this.Handle, (System.Runtime.InteropServices.ComTypes.IDataObject)e.Data, ref wp, (int)e.Effect);
      } else {
        this.OnDragEnter(e);
      }
    }

    private void ShellTreeView_DragOver(object sender, DragEventArgs e) {
      var wp = new DataObject.Win32Point() { X = e.X, Y = e.Y };
      ShellView.Drag_SetEffect(e);
      var descinvalid = new DataObject.DropDescription();
      descinvalid.type = (int)DataObject.DropImageType.Invalid;
      ((System.Runtime.InteropServices.ComTypes.IDataObject)e.Data).SetDropDescription(descinvalid);
      var node = this.ShellTreeView.GetNodeAt(this.PointToClient(new Point(e.X, e.Y)));
      if (node != null && !string.IsNullOrEmpty(node.Text) && node.Text != this._EmptyItemString) {
        User32.SendMessage(this.ShellTreeView.Handle, MSG.TVM_SETHOT, 0, node.Handle);
        var desc = new DataObject.DropDescription();
        switch (e.Effect) {
          case DragDropEffects.Copy:
            desc.type = (int)DataObject.DropImageType.Copy;
            desc.szMessage = "Copy To %1";
            break;
          case DragDropEffects.Link:
            desc.type = (int)DataObject.DropImageType.Link;
            desc.szMessage = "Create Link in %1";
            break;
          case DragDropEffects.Move:
            desc.type = (int)DataObject.DropImageType.Move;
            desc.szMessage = "Move To %1";
            break;
          case DragDropEffects.None:
            desc.type = (int)DataObject.DropImageType.None;
            desc.szMessage = "";
            break;
          default:
            desc.type = (int)DataObject.DropImageType.Invalid;
            desc.szMessage = "";
            break;
        }

        desc.szInsert = node.Text;
        ((System.Runtime.InteropServices.ComTypes.IDataObject)e.Data).SetDropDescription(desc);
      }

      if (e.Data.GetDataPresent("DragImageBits")) {
        DropTargetHelper.DropTarget.Create.DragOver(ref wp, (int)e.Effect);
      } else {
        this.OnDragOver(e);
      }
    }

    private void ShellTreeView_DragLeave(object sender, EventArgs e) => DropTargetHelper.DropTarget.Create.DragLeave();

    private void ShellTreeView_DragDrop(object sender, DragEventArgs e) {
      var hittestInfo = this.ShellTreeView.HitTest(this.PointToClient(new Point(e.X, e.Y)));
      IListItemEx destination = null;
      if (hittestInfo.Node == null) {
        e.Effect = DragDropEffects.None;
      } else {
        destination = hittestInfo.Node.Tag as IListItemEx;
      }

      switch (e.Effect) {
        case DragDropEffects.Copy:

          // this.DoCopy(e.Data, destination);
          break;

        case DragDropEffects.Link:
          System.Windows.MessageBox.Show("link");
          break;

        case DragDropEffects.Move:
          this.DoMove(e.Data, destination);
          break;

        case DragDropEffects.All:
        case DragDropEffects.None:
        case DragDropEffects.Scroll:
          break;

        default:
          break;
      }

      var wp = new DataObject.Win32Point() { X = e.X, Y = e.Y };

      if (e.Data.GetDataPresent("DragImageBits")) {
        DropTargetHelper.DropTarget.Create.Drop((System.Runtime.InteropServices.ComTypes.IDataObject)e.Data, ref wp, (int)e.Effect);
      } else {
        this.OnDragDrop(e);
      }
    }

    #endregion Events

    #region Initializer

    public ShellTreeViewEx() {
      this.InitTreeView();

      this.Controls.Add(this.ShellTreeView);

      this.InitializeComponent();

      this.InitRootItems();
      this._NotificationNetWork.RegisterChangeNotify(this.Handle, ShellNotifications.CSIDL.CSIDL_NETWORK, false);
      this._NotificationGlobal.RegisterChangeNotify(this.Handle, ShellNotifications.CSIDL.CSIDL_DESKTOP, true);
    }

    #endregion Initializer

    #region Overrides     

    protected override void OnHandleDestroyed(EventArgs e) {
      this._NotificationNetWork.UnregisterChangeNotify();
      this._NotificationGlobal.UnregisterChangeNotify();
      base.OnHandleDestroyed(e);
    }

    [HandleProcessCorruptedStateExceptions]
    protected override void WndProc(ref Message m) {
      base.WndProc(ref m);
      if (m.Msg == ShellNotifications.WM_SHNOTIFY) {
        // MessageBox.Show("1");
        if (this._NotificationGlobal.NotificationReceipt(m.WParam, m.LParam)) {
          // var computerNode = this.ShellTreeView.Nodes.OfType<TreeNode>().Single(s => s.Tag != null && (s.Tag as IListItemEx).ParsingName == KnownFolders.Computer.ParsingName);
          var computerNode = this.ShellTreeView.Nodes.OfType<TreeNode>().First(s => (s?.Tag as IListItemEx)?.ParsingName == KnownFolders.Computer.ParsingName);
          foreach (NotifyInfos info in this._NotificationGlobal.NotificationsReceived.ToArray()) {
            switch (info.Notification) {
              case ShellNotifications.SHCNE.SHCNE_RENAMEFOLDER:
                var objPrevDir = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, info.Item1);
                var objNewDir = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, info.Item2);
                this.RenameItem(objPrevDir, objNewDir);
                break;
              case ShellNotifications.SHCNE.SHCNE_MKDIR:
                try {
                  var obj = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, info.Item1);
                  obj = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, obj.PIDL);
                  if (obj.IsFileSystem) {
                    this.AddItem(obj);
                  }
                } catch (FileNotFoundException) {
                } catch (Exception) {
                }
                break;
              case ShellNotifications.SHCNE.SHCNE_RMDIR:
                var objDelDir = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, info.Item1);
                if (objDelDir.IsFolder && objDelDir.IsFileSystem) {
                  this.DeleteItem(objDelDir);
                }

                break;
              case ShellNotifications.SHCNE.SHCNE_MEDIAINSERTED:
              case ShellNotifications.SHCNE.SHCNE_MEDIAREMOVED:
                var objDm = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, info.Item1);
                var exisitingMItem = computerNode.Nodes.OfType<TreeNode>().FirstOrDefault(s => s.Tag != null & (s.Tag as IListItemEx).Equals(objDm));
                if (exisitingMItem != null) {
                  exisitingMItem.Text = objDm.DisplayName;
                  exisitingMItem.ImageIndex = objDm.GetSystemImageListIndex(objDm.PIDL, ShellIconType.SmallIcon, ShellIconFlags.OpenIcon);
                  exisitingMItem.SelectedImageIndex = exisitingMItem.ImageIndex;
                }

                break;
              case ShellNotifications.SHCNE.SHCNE_DRIVEREMOVED:
                var objDr = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, info.Item1);
                var theNode = computerNode.Nodes.OfType<TreeNode>().FirstOrDefault(s => s.Tag != null & (s.Tag as IListItemEx).Equals(objDr));
                if (theNode != null) {
                  computerNode.Nodes.Remove(theNode);
                }

                objDr.Dispose();
                break;
              case ShellNotifications.SHCNE.SHCNE_DRIVEADD:
                var objDa = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, info.Item1);

                var exisitingItem = computerNode.Nodes.OfType<TreeNode>().FirstOrDefault(s => s.Tag != null && (s.Tag as IListItemEx).Equals(objDa));
                if (exisitingItem == null && !this._PathsToBeAdd.Any(c => c.Equals(objDa.ParsingName, StringComparison.InvariantCultureIgnoreCase))) {
                  this._PathsToBeAdd.Add(objDa.ParsingName);
                  var newDrive = new TreeNode(objDa.DisplayName);
                  newDrive.Tag = objDa;
                  newDrive.ImageIndex = objDa.GetSystemImageListIndex(objDa.PIDL, ShellIconType.SmallIcon, ShellIconFlags.OpenIcon);
                  newDrive.SelectedImageIndex = newDrive.ImageIndex;
                  if (objDa.HasSubFolders) {
                    newDrive.Nodes.Add(this._EmptyItemString);
                  }

                  var nodesList = computerNode.Nodes.OfType<TreeNode>().Where(w => w.Tag != null).Select(s => s.Tag as IListItemEx).ToList();
                  nodesList.Add(objDa);
                  nodesList = nodesList.OrderBy(o => o.ParsingName).ToList();
                  var indexToInsert = nodesList.IndexOf(objDa);
                  nodesList = null;
                  GC.Collect();
                  computerNode.Nodes.Insert(indexToInsert, newDrive);
                  this._PathsToBeAdd.Clear();
                }

                break;
              case ShellNotifications.SHCNE.SHCNE_UPDATEDIR:
                break;
              default:
                break;
            }
            this._NotificationGlobal.NotificationsReceived.Remove(info);
          }
        }

        if (this._NotificationNetWork.NotificationReceipt(m.WParam, m.LParam)) {
          foreach (NotifyInfos info in this._NotificationNetWork.NotificationsReceived.ToArray()) {
            switch (info.Notification) {
              case ShellNotifications.SHCNE.SHCNE_RENAMEITEM:
                break;
              case ShellNotifications.SHCNE.SHCNE_MKDIR:

                // case ShellNotifications.SHCNE.SHCNE_CREATE:
                try {
                  var sho = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, info.Item1);
                  sho = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, sho.PIDL);
                  if (sho.Parent == null || !sho.Parent.ParsingName.Equals(KnownFolders.Network.ParsingName, StringComparison.InvariantCultureIgnoreCase)) {
                    break;
                  }

                  // var existingItem = this.ShellTreeView.Nodes.OfType<TreeNode>().Last().Nodes.OfType<TreeNode>().Where(w => (w.Tag as IListItemEx) != null && (w.Tag as IListItemEx)?.ParsingName == sho.ParsingName).Count();
                  var existingItem = this.ShellTreeView.Nodes.OfType<TreeNode>().Last().Nodes.OfType<TreeNode>().Where(w => (w.Tag as IListItemEx)?.ParsingName == sho.ParsingName).Count();

                  if (existingItem > 0) {
                    break;
                  }

                  var node = new TreeNode(sho.DisplayName);
                  node.ImageIndex = sho.GetSystemImageListIndex(sho.PIDL, ShellIconType.SmallIcon, ShellIconFlags.OpenIcon); // this.folderImageListIndex;
                  node.SelectedImageIndex = node.ImageIndex;
                  node.Tag = sho;
                  if (sho.HasSubFolders) {
                    node.Nodes.Add(this._SearchingForFolders);
                  }

                  // if (sho != null && sho.Parent != null && sho.Parent.ParsingName == KnownFolders.Network.ParsingName) {
                  // var firstNode = this.ShellTreeView.Nodes.OfType<TreeNode>().Last().Nodes.OfType<TreeNode>().FirstOrDefault();
                  // if (firstNode != null && firstNode.Text.Equals(this._SearchingForFolders)) {
                  // firstNode.Remove();
                  // }
                  this.ShellTreeView.Nodes.OfType<TreeNode>().Last().Nodes.Add(node);

                  // }
                } catch (AccessViolationException) {
                } catch (NullReferenceException) {
                }

                break;
              case ShellNotifications.SHCNE.SHCNE_DELETE:
                break;
              case ShellNotifications.SHCNE.SHCNE_RMDIR:
                break;
              case ShellNotifications.SHCNE.SHCNE_MEDIAINSERTED:
                break;
              case ShellNotifications.SHCNE.SHCNE_MEDIAREMOVED:
                break;
              case ShellNotifications.SHCNE.SHCNE_DRIVEREMOVED:
                break;
              case ShellNotifications.SHCNE.SHCNE_DRIVEADD:
                break;
              case ShellNotifications.SHCNE.SHCNE_NETSHARE:
                break;
              case ShellNotifications.SHCNE.SHCNE_NETUNSHARE:
                break;
              case ShellNotifications.SHCNE.SHCNE_ATTRIBUTES:
                break;
              case ShellNotifications.SHCNE.SHCNE_UPDATEDIR:
                break;
              case ShellNotifications.SHCNE.SHCNE_UPDATEITEM:
                break;
              case ShellNotifications.SHCNE.SHCNE_SERVERDISCONNECT:
                break;
              case ShellNotifications.SHCNE.SHCNE_UPDATEIMAGE:
                break;
              case ShellNotifications.SHCNE.SHCNE_DRIVEADDGUI:
                break;
              case ShellNotifications.SHCNE.SHCNE_RENAMEFOLDER:
                break;
              case ShellNotifications.SHCNE.SHCNE_FREESPACE:
                break;
              case ShellNotifications.SHCNE.SHCNE_EXTENDED_EVENT:
                break;
              case ShellNotifications.SHCNE.SHCNE_ASSOCCHANGED:
                break;
              case ShellNotifications.SHCNE.SHCNE_DISKEVENTS:
                break;
              case ShellNotifications.SHCNE.SHCNE_GLOBALEVENTS:
                break;
              case ShellNotifications.SHCNE.SHCNE_ALLEVENTS:
                break;
              case ShellNotifications.SHCNE.SHCNE_INTERRUPT:
                break;
              default:
                break;
            }
            this._NotificationNetWork.NotificationsReceived.Remove(info);
          }

        }
      }
    }

    #endregion
  }
}