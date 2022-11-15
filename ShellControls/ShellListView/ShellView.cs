﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Xml.Linq;
using BExplorer.Shell;
using BExplorer.Shell._Plugin_Interfaces;
using BExplorer.Shell.DropTargetHelper;
using BExplorer.Shell.Interop;
using Settings;
using ShellControls.ShellContextMenu;
using ShellLibrary.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WPFUI.Win32;
using Application = System.Windows.Application;
using Color = System.Drawing.Color;
using DataObject = BExplorer.Shell.DataObject;
using DPoint = System.Drawing.Point;
using F = System.Windows.Forms;
using FileAttributes = BExplorer.Shell.FileAttributes;
using ImageList = BExplorer.Shell.ImageList;
using KnownFolders = BExplorer.Shell.KnownFolders;
using MSG = BExplorer.Shell.Interop.MSG;
using Pen = System.Drawing.Pen;
using SQLite = System.Data.SQLite;
using SystemProperties = BExplorer.Shell.SystemProperties;

namespace ShellControls.ShellListView {
  /// <summary> The ShellFileListView class that visualize contents of a directory </summary>
  public partial class ShellView : F.UserControl, IShellControl {
    #region Event Handler

    public event EventHandler<NavigatingEventArgs> Navigating;
    public event EventHandler<ColumnAddEventArgs> AfterCollumsPopulate;

    /// <summary> Occurs when the <see cref="ShellView" /> control navigates to a new folder. </summary>
    public event EventHandler<NavigatedEventArgs> Navigated;

    public event EventHandler<ListViewColumnDropDownArgs> OnListViewColumnDropDownClicked;

    public event EventHandler<CollumnsChangedArgs> OnListViewCollumnsChanged;

    /// <summary>
    /// Occurs when the <see cref="ShellView"/>'s current selection
    /// changes.
    /// </summary>
    public event EventHandler SelectionChanged;

    public event EventHandler<ItemUpdatedEventArgs> ItemUpdated;

    public event EventHandler<ItemUpdatedEventArgs> NewItemAvailable;

    public event EventHandler<ViewChangedEventArgs> ViewStyleChanged;

    public event EventHandler<NavigatedEventArgs> ItemMiddleClick;

    /// <summary>
    /// Occurs when the user right-clicks on the blank area of the column header area
    /// </summary>
    public event F.MouseEventHandler ColumnHeaderRightClick;

    /// <summary>
    /// Raised whenever a key is pressed, with the intention of doing a key jump. Please use
    /// KeyDown to catch when any key is pressed.
    /// </summary>
    public event F.KeyEventHandler KeyJumpKeyDown;

    public event EventHandler<RenameEventArgs> BeginItemLabelEdit;

    /// <summary>Raised whenever file/folder name is finished editing. Boolean: is event canceled</summary>
    public event EventHandler<Boolean> EndItemLabelEdit;

    /// <summary> Raised when the timer finishes for the Key Jump timer. </summary>
    public event EventHandler KeyJumpTimerDone;

    public event EventHandler<ScrollEventArgs> OnLVScroll;


    #endregion Event Handler

    #region Public Members
    public Dictionary<PROPERTYKEY, Collumns?> AllAvailableColumns;
    public List<Collumns> Collumns = new List<Collumns>();
    public List<ListViewGroupEx> Groups = new List<ListViewGroupEx>();
    public LVTheme Theme { get; set; }

    public Boolean IsRenameNeeded { get; set; }

    // public Boolean IsLibraryInModify { get; set; }
    public Boolean IsFileExtensionShown { get; set; }

    public Boolean IsCancelRequested;
    public Boolean IsNavigationCancelRequested = false;
    public Boolean IsNavigationInProgress = false;

    public Boolean IsGroupsEnabled { get; set; }
    public FileOperationDialog OperationDialog { get; set; }

    // public Boolean IsTraditionalNameGrouping { get; set; }

    /// <summary> Returns the key jump string as it currently is.</summary>
    public String KeyJumpString { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the folder currently being browsed by the <see
    /// cref="ShellView" /> has parent folder which can be navigated to by calling <see
    /// cref="NavigateParent" />.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Boolean CanNavigateParent => this.CurrentFolder?.ParsingName != ShellItem.Desktop.ParsingName;

    /// <summary>
    /// Gets/sets a <see cref="ShellItem" /> describing the folder currently being browsed by
    /// the <see cref="ShellView" />.
    /// </summary>
    [Browsable(false)]
    public IListItemEx CurrentFolder { get; private set; }

    public Int32 IconSize { get; private set; }

    public List<IListItemEx> Items { get; private set; }

    public String LastSortedColumnId { get; set; }

    public SortOrder LastSortOrder { get; set; }

    public Collumns LastGroupCollumn { get; private set; }

    public SortOrder LastGroupOrder { get; private set; }

    public IntPtr LVHandle { get; private set; }

    public IntPtr LVHeaderHandle { get; set; }

    public ObservableCollectionEx<LVItemColor> LVItemsColorCodes { get; set; }

    public System.Windows.Controls.GridView Header {
      get => this._Header;
      set {
        this._Header = value;
        this._Header.Columns.CollectionChanged += ColumnsOnCollectionChanged;

      }
    }

    private void ColumnsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
      if (e.Action == NotifyCollectionChangedAction.Move) {
        if (e.OldStartingIndex == 0 || e.NewStartingIndex == 0) {
          var itemNew = e.NewItems?.Cast<ListViewColumnHeader>().FirstOrDefault();
          this.Header.Columns.RemoveAt(e.NewStartingIndex);
          this.Header.Columns.Insert(e.OldStartingIndex, itemNew);
        }
      }
    }

    public ImageListEx LargeImageList;
    public ImageListEx SmallImageList;
    private ConcurrentDictionary<Int32, Int32> CurrentlyUpdatingItems = new ConcurrentDictionary<Int32, Int32>();
    private Boolean _HasScrollbar = false;
    private Boolean Test { get; set; }
    private System.Windows.Controls.GridView _Header;
    User32.WinEventDelegate dele = null;
    private IntPtr _winHook;
    private static readonly IntPtr _BlackBrush = Gdi32.CreateSolidBrush(0);


    /// <summary>Returns the currently selected item and removes any items in <see cref="_SelectedIndexes"/> not in <see cref="Items"/>  </summary>
    public List<IListItemEx> SelectedItems {
      get {
        var data = this._SelectedIndexes.ToArray();
        var selItems = new List<IListItemEx>();
        this._DraggedItemIndexes.AddRange(data);

        foreach (var index in data) {
          var item = this.Items.SingleOrDefault(s => s.ItemIndex == index);
          if (item == null) {
            this._SelectedIndexes.Remove(index);
          } else {
            selItems.Add(item);
          }
        }

        return selItems;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether a checkboxes should be shown for allowing selection
    /// </summary>
    public Boolean ShowCheckboxes {
      get => this._ShowCheckBoxes;

      set {
        if (value) {
          User32.SendMessage(this.LVHandle, MSG.LVM_SetExtendedStyle, (Int32)ListViewExtendedStyles.LVS_EX_AUTOCHECKSELECT, (Int32)ListViewExtendedStyles.LVS_EX_AUTOCHECKSELECT);
          User32.SendMessage(this.LVHandle, MSG.LVM_SetExtendedStyle, (Int32)ListViewExtendedStyles.CheckBoxes, (Int32)ListViewExtendedStyles.CheckBoxes);
        } else {
          User32.SendMessage(this.LVHandle, MSG.LVM_SetExtendedStyle, (Int32)ListViewExtendedStyles.LVS_EX_AUTOCHECKSELECT, 0);
          User32.SendMessage(this.LVHandle, MSG.LVM_SetExtendedStyle, (Int32)ListViewExtendedStyles.CheckBoxes, 0);
        }

        this._ShowCheckBoxes = value;
      }
    }

    public Boolean ShowHidden {
      get => this._ShowHidden;

      set => this._ShowHidden = value;
      //this.RefreshContents();
    }

    /// <summary> Gets or sets how items are displayed in the control. </summary>
    [DefaultValue(ShellViewStyle.Medium)]
    [Category("Appearance")]
    public ShellViewStyle View {
      get => this._MView;

      set {
        this._MView = value;
        this.IsViewSelectionAllowed = false;
        switch (value) {
          case ShellViewStyle.ExtraLargeIcon:
            User32.SendMessage(this.LVHandle, MSG.LVM_SETVIEW, (Int32)LV_VIEW.LV_VIEW_ICON, 0);
            this.ResizeIcons(256);
            break;

          case ShellViewStyle.LargeIcon:
            User32.SendMessage(this.LVHandle, MSG.LVM_SETVIEW, (Int32)LV_VIEW.LV_VIEW_ICON, 0);
            this.ResizeIcons(128);
            break;

          case ShellViewStyle.Medium:
            User32.SendMessage(this.LVHandle, MSG.LVM_SETVIEW, (Int32)LV_VIEW.LV_VIEW_ICON, 0);
            this.ResizeIcons(72);
            break;

          case ShellViewStyle.SmallIcon:
            this.ResizeIcons(48);
            User32.SendMessage(this.LVHandle, MSG.LVM_SETVIEW, (Int32)LV_VIEW.LV_VIEW_ICON, 0);
            break;

          case ShellViewStyle.List:
            User32.SendMessage(this.LVHandle, MSG.LVM_SETVIEW, (Int32)LV_VIEW.LV_VIEW_LIST, 0);
            this.ResizeIcons(16);
            break;

          case ShellViewStyle.Details:
            this.UpdateColsInView(true);
            User32.SendMessage(this.LVHandle, MSG.LVM_SETVIEW, (Int32)LV_VIEW.LV_VIEW_DETAILS, 0);
            this.ResizeIcons(16);
            break;

          case ShellViewStyle.Thumbnail:
            User32.SendMessage(this.LVHandle, MSG.LVM_SETVIEW, (Int32)LV_VIEW.LV_VIEW_ICON, 0);
            break;

          case ShellViewStyle.Tile:
            var isComputer = this.RequestedCurrentLocation.ParsingName.Equals(KnownFolders.Computer.ParsingName);
            User32.SendMessage(this.LVHandle, MSG.LVM_SETVIEW, (Int32)LV_VIEW.LV_VIEW_TILE, 0);
            if (isComputer) {
              var tvi = new LVTILEVIEWINFO {
                cLines = 2,
                rcLabelMargin = new User32.RECT() { Left = 2, Right = 0, Bottom = 60, Top = 5 },
                cbSize = (UInt32)Marshal.SizeOf(typeof(LVTILEVIEWINFO)),
                dwMask = (UInt32)LVTVIM.LVTVIM_COLUMNS | (UInt32)LVTVIM.LVTVIM_LABELMARGIN | (UInt32)LVTVIM.LVTVIM_TILESIZE,
                dwFlags = (UInt32)LVTVIF.LVTVIF_FIXEDSIZE,
                sizeTile = new INTEROP_SIZE() { cx = 270, cy = 90 },
              };

              User32.SendMessage(this.LVHandle, (Int32)MSG.LVM_SETTILEVIEWINFO, 0, ref tvi);
            } else {
              var tvi = new LVTILEVIEWINFO {
                cLines = 2,
                rcLabelMargin = new User32.RECT() { Left = 2, Right = 0, Bottom = 35, Top = 7 },
                cbSize = (UInt32)Marshal.SizeOf(typeof(LVTILEVIEWINFO)),
                dwMask = (UInt32)LVTVIM.LVTVIM_COLUMNS | (UInt32)LVTVIM.LVTVIM_LABELMARGIN | (UInt32)LVTVIM.LVTVIM_TILESIZE,
                dwFlags = (UInt32)LVTVIF.LVTVIF_FIXEDSIZE,
                sizeTile = new INTEROP_SIZE() { cx = 270, cy = 90 },
              };

              User32.SendMessage(this.LVHandle, (Int32)MSG.LVM_SETTILEVIEWINFO, 0, ref tvi);
            }

            this.ResizeIcons(64);
            break;

          case ShellViewStyle.Thumbstrip:
            break;

          case ShellViewStyle.Content:
            break;
        }

        if (value != ShellViewStyle.Details) {
          this.UpdateColsInView();
          this.AutosizeAllColumns(-2);
        }

        this.ViewStyleChanged?.Invoke(this, new ViewChangedEventArgs(value, this.IconSize));
        this.IsViewSelectionAllowed = true;
        this.ScrollUpdateThreadRun();
      }
    }

    public Int32 CurrentRefreshedItemIndex = -1; // TODO: Find out if we need this property

    /// <summary>Are we/have we navigated to a search folder</summary>
    public Boolean IsSearchNavigating = false;

    public Boolean IsRenameInProgress = false;
    public Boolean IsRenameofNewItem = false;

    public ManualResetEvent ScrollSyncEvent = new ManualResetEvent(true);

    #endregion Public Members

    #region Private Members

    private ShellNotifications _Notifications = new ShellNotifications();
    private IListView _IIListView;
    private IVisualProperties _IIVisualProperties;
    private FileSystemWatcher _FsWatcher = new FileSystemWatcher();
    private ListViewEditor _EditorSubclass;
    private readonly F.Timer _UnvalidateTimer = new F.Timer();
    private readonly F.Timer _FastUnvalidateTimer = new F.Timer();
    private readonly F.Timer _MaintenanceTimer = new F.Timer();
    private readonly F.Timer _NavWaitTimer = new F.Timer() { Interval = 150, Enabled = false };
    private readonly F.Timer _SearchUpdateTimer = new F.Timer() { Interval = 3000, Enabled = false };
    private readonly String _DBPath = Path.Combine(KnownFolders.RoamingAppData.ParsingName, @"BExplorer\Settings.sqlite");
    private Boolean _IsOverGroup = false;
    private IntPtr _OldWndProc;
    private IntPtr _OldHeaderWndProc;

    private List<Int32> _SelectedIndexes {
      get {
        var selItems = new List<Int32>();
        var iStart = -1;
        var lvi = default(LVITEMINDEX);
        while (lvi.iItem != -1) {
          try {
            lvi = this.ToLvItemIndex(iStart);
            User32.SendMessage(this.LVHandle, LVM.GETNEXTITEMINDEX, ref lvi, LVNI.LVNI_SELECTED);
            iStart = lvi.iItem;
          } catch {
            // ignored
          }

          if (lvi.iItem == -1) {
            continue;
          }

          if (!selItems.Contains(lvi.iItem)) {
            selItems.Add(lvi.iItem);
          } else {
            break;
          }
        }

        return selItems;
      }
    }

    private Boolean _ItemForRealNameIsAny => this._ItemForRename != -1;

    public Int32 _ItemForRename { get; set; }

    private Boolean _IsCanceledOperation { get; set; }

    private Int32 _LastItemForRename { get; set; }

    private System.Runtime.InteropServices.ComTypes.IDataObject _DataObject { get; set; }

    private Boolean _ShowCheckBoxes = false;
    private Boolean _ShowHidden;
    private F.Timer _ResetTimer = new F.Timer();
    private readonly List<Int32> _DraggedItemIndexes = new();
    private F.Timer _KeyJumpTimer = new F.Timer();
    private IListItemEx _Kpreselitem = null;
    private LVIS _IsDragSelect = 0;
    private BackgroundWorker _Bw = new BackgroundWorker();

    private ShellViewStyle _MView;

    private F.Timer _SelectionTimer = new F.Timer();
    private ImageList _Small = new ImageList(ImageListSize.SystemSmall);

    private ManualResetEvent _ResetEvent = new ManualResetEvent(true);
    private ManualResetEvent _ResetScrollEvent = new ManualResetEvent(true);

    private readonly List<Int32> _CuttedIndexes = new List<Int32>();
    private Int32 _LastDropHighLightedItemIndex = -1;

    public Dictionary<String, Dictionary<IListItemEx, List<String>>> BadgesData;
    private readonly QueueEx<Tuple<ItemUpdateType, IListItemEx>> _ItemsQueue = new QueueEx<Tuple<ItemUpdateType, IListItemEx>>();

    public IListItemEx RequestedCurrentLocation { get; set; }

    private readonly List<String> _TemporaryFiles = new List<String>();
    private Boolean _IsDisplayEmptyText = false;
    private readonly List<Thread> _Threads = new List<Thread>();
    public Boolean IsViewSelectionAllowed = true;
    private readonly ManualResetEvent _Mre = new ManualResetEvent(false);
    private readonly HashSet<IntPtr> _AddedItems = new HashSet<IntPtr>();
    private readonly F.Timer _SearchTimer = new F.Timer();
    private F.Timer _ItemLoadingTimer = new F.Timer();
    private readonly ManualResetEvent _Smre = new ManualResetEvent(true);
    private Int32 OldPosition { get; set; }
    private Boolean _IsManualScroll { get; set; }
    private User32.Win32WndProc _NewWndProc;
    private User32.Win32WndProc _NewHeaderWndProc;

    #endregion Private Members

    #region Initializer

    /// <summary> Main constructor </summary>
    public ShellView() {
      this._ItemForRename = -1;
      this.SetStyle(ControlStyles.DoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
      this.UpdateStyles();
      this.InitializeComponent();
      this.Items = new List<IListItemEx>();
      this.LVItemsColorCodes = new ObservableCollectionEx<LVItemColor>();
      this.AllAvailableColumns = this.AvailableColumns();
      this.AllowDrop = true;
      this.LargeImageList = new ImageListEx(48);
      this.SmallImageList = new ImageListEx(16);
      this.LargeImageList.AttachToListView(this, 0);
      this.SmallImageList.AttachToListView(this, 1);
      this._ResetTimer.Interval = 450;
      this._ResetTimer.Tick += this.resetTimer_Tick;
      this.MouseUp += this.ShellView_MouseUp;
      this._SelectionTimer.Interval = 100;
      this._SelectionTimer.Tick += this.selectionTimer_Tick;
    }

    #endregion Initializer

    #region Events

    private void _MaintenanceTimer_Tick(Object sender, EventArgs e) {
      var thread = new Thread(
        () => {
          Thread.Sleep(100);
          var curProcess = Process.GetCurrentProcess();
          if (curProcess.WorkingSet64 > 100 * 1024 * 1024) {
            Shell32.SetProcessWorkingSetSize(curProcess.Handle, -1, -1);
          }

          curProcess.Dispose();
        }) {
        IsBackground = true
      };
      thread.Start();
    }

    private void _UnvalidateTimer_Tick(Object sender, EventArgs e) {
      this._UnvalidateTimer.Stop();
      this._FastUnvalidateTimer.Stop();
      this.BeginInvoke((Action)(() => {
        if (this.CurrentFolder == null) {
          return;
        }
        //return;
        var isChanged = false;
        try {
          while (this._ItemsQueue.Count() > 0) {
            Thread.Sleep(1);
            var obj = this._ItemsQueue.Dequeue();
            if (obj.Item2.IsProcessed) {
              continue;
            }

            obj.Item2.IsProcessed = true;

            if (obj.Item1 == ItemUpdateType.RecycleBin) {
              this.RaiseRecycleBinUpdated();
              isChanged = true;
            }

            if (obj.Item1 == ItemUpdateType.Deleted) {
              // var worker = new Thread(() => {
              // var itemForDelete = this.Items.ToArray().SingleOrDefault(s =>
              // s.Equals(obj.Item2) || (
              // obj.Item2.Extension.Equals(".library-ms") &&
              // s.ParsingName.Equals(Path.Combine(KnownFolders.Libraries.ParsingName,
              // Path.GetFileName(obj.Item2.ParsingName)))
              // )
              // );

              // if (itemForDelete != null) {
              this.Items.Remove(obj.Item2);
              this._AddedItems.Remove(obj.Item2.PIDL);

              // TODO: Make this to work in threaded environment
              // itemForDelete.Dispose();
              // }

              obj.Item2.Dispose();
              isChanged = true;
              // this.Invoke((Action)(this.ResortListViewItems));
              // });
              // worker.SetApartmentState(ApartmentState.STA);
              // worker.Start();
            } else if (obj.Item1 == ItemUpdateType.Created) {
              if (obj.Item2.IsInCurrentFolder(this.CurrentFolder) &&
                  !this.Items.Contains(obj.Item2, new ShellItemEqualityComparer())) {
                obj.Item2.ItemIndex = this.Items.Count;
                this.Items.Add(obj.Item2);
                this._AddedItems.Add(obj.Item2.PIDL);
              }

              isChanged = true;
              obj.Item2.Dispose();
            } else if (obj.Item1 != ItemUpdateType.RecycleBin && obj.Item1 != ItemUpdateType.IconUpdated) {
              //continue;
              var existingItem = this.Items.FirstOrDefault(s => s.Equals(obj.Item2));
              if (existingItem == null) {
                if (obj.Item2.ParsingName.StartsWith(this.CurrentFolder.ParsingName)) {
                  if (!this.Items.Contains(obj.Item2, new ShellItemEqualityComparer()) &&
                      !String.IsNullOrEmpty(obj.Item2.ParsingName)) {
                    obj.Item2.ItemIndex = this.Items.Count;
                    this.Items.Add(obj.Item2);
                    this._AddedItems.Add(obj.Item2.PIDL);
                    obj.Item2.Dispose();
                  }
                } else {
                  var affectedItem = this.Items.FirstOrDefault(s => s.Equals(obj.Item2.Parent));
                  if (affectedItem != null) {
                    var index = affectedItem.ItemIndex;
                    this.RefreshItem(index, affectedItem.EnumPIDL);
                  }

                  affectedItem?.Dispose();
                }
              } else {
                if (this.IconSize == 16) {
                  this.SmallImageList.EnqueueOverlay(existingItem.ItemIndex);
                } else {
                  this.LargeImageList.EnqueueOverlay(existingItem.ItemIndex);
                }

                this.RefreshItem(existingItem.ItemIndex, existingItem.EnumPIDL);
                existingItem.Dispose();
                obj.Item2.Dispose();
                isChanged = true;
              }

              obj.Item2.IsProcessed = false;
              GC.Collect();
              //this.ResortListViewItems();
            } else {
              continue;
            }
          }



          foreach (var path in this._TemporaryFiles.ToArray()) {
            var item = this.Items.ToArray().SingleOrDefault(s => s.ParsingName.ToLower().Equals(path.ToLower()));
            if (item?.IsFolder == false && !File.Exists(path)) {
              this.Items.Remove(item);
              isChanged = true;
            }
          }


          ////if (this.Items.Count != this.CurrentFolder.Count()) {
          // var maintenanceThread = new Thread(() => {
          // var deletedItems = this.Items.ToArray().Where(p => !this.CurrentFolder.Any(p2 => p2.Equals(p)));
          // foreach (var deletedItem in deletedItems) {
          // Items.Remove(deletedItem);
          // //this._AddedItems.Remove(deletedItem.PIDL);
          // //deletedItem.Dispose();
          // }
          // this.Invoke((Action) (() => {
          // this.ResortListViewItems();
          // }));
          // });
          // maintenanceThread.Start();

          // }
          if (!this.RequestedCurrentLocation.IsSearchFolder) {
            if (isChanged) {
              this.ResortListViewItems();
            }
          }

          if (isChanged) {
            this.ItemUpdated?.Invoke(this, new ItemUpdatedEventArgs(ItemUpdateType.Created, null, null, -1));
          }
        } catch (Exception) {
        }

        //Shell32.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
        //GC.WaitForFullGCComplete(1000);
        //GC.Collect();
      }));
    }

    private void ResortListViewItems() {
      var col = this.Collumns.FirstOrDefault(w => w.ID == this.LastSortedColumnId);
      this.SetSortCollumn(true, col, this.LastSortOrder, false);
      if (this.IsGroupsEnabled) {
        this.SetGroupOrder(false);
      }
    }

    private void Column_OnClick(Int32 iItem) {
      var rect = default(User32.RECT);

      if (User32.SendMessage(this.LVHeaderHandle, 0x1200 + 7, iItem, ref rect) == IntPtr.Zero) {
        throw new Win32Exception();
      }
      var pt = this.PointToScreen(new DPoint(rect.Right - 17, rect.Bottom));
      this.OnListViewColumnDropDownClicked?.Invoke(this.Collumns[iItem], new ListViewColumnDropDownArgs(iItem, pt));
    }

    private Boolean _IsMouseButtonDown = false;
    private void selectionTimer_Tick(Object sender, EventArgs e) {
      (sender as F.Timer)?.Stop();
      this.BeginInvoke((Action)(() => {
        this.OnSelectionChanged();
        this.KeyJumpTimerDone?.Invoke(this, EventArgs.Empty);
      }));

      if (this._ItemForRename != this.GetFirstSelectedItemIndex() && !this.IsRenameInProgress) {
        (sender as F.Timer)?.Stop();
        this.EndLabelEdit();
      }
    }

    private void resetTimer_Tick(Object sender, EventArgs e) {
      this.BeginInvoke((Action)(() => {
        (sender as F.Timer)?.Stop();
        this._ResetEvent.Set();
        this.LargeImageList.ResetEvent.Set();
        this.IsCancelRequested = false;
        Shell32.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
        GC.WaitForFullGCComplete(1000);
        GC.Collect();
      }));

    }

    private void ShellView_MouseUp(Object sender, F.MouseEventArgs e) {
      if (this._IsDragSelect == LVIS.LVIS_SELECTED) {
        if (this._SelectionTimer.Enabled) {
          this._SelectionTimer.Stop();
        }

        this._SelectionTimer.Start();
      }
    }

    private void ShellView_GotFocus() {
      this.Focus();
      User32.SetForegroundWindow(this.LVHandle);
    }

    private Boolean ShellView_KeyDown(Keys e) {
      if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox && e != Keys.Escape && e != Keys.Enter) {
        var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey((Int32)e); // Key to send
        var target = System.Windows.Input.Keyboard.FocusedElement as System.Windows.Controls.TextBox; // Target element
        var routedEvent = System.Windows.Input.Keyboard.KeyDownEvent; // Event to send

        target?.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key) { RoutedEvent = routedEvent });
        return false;
      }

      if (this._ItemForRealNameIsAny) {
        if (e == Keys.Escape) {
          this.EndLabelEdit(true);
        } else if (e == Keys.F2) {
          // TODO: implement a conditional selection inside rename textbox!
        } else if (e == Keys.Enter)
          this.EndLabelEdit();
      }

      if ((ModifierKeys & Keys.Control) == Keys.Control && !(System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox)) {
        switch (e) {
          case Keys.A:
            this.SelectAll();
            break;

          case Keys.Add:
            break;

          case Keys.Alt:
            break;

          case Keys.Apps:
            break;

          case Keys.Attn:
            break;

          case Keys.B:
            break;

          case Keys.Back:
            this.NavigateParent();
            break;

          case Keys.BrowserBack:
            break;

          case Keys.BrowserFavorites:
            break;

          case Keys.BrowserForward:
            break;

          case Keys.BrowserHome:
            break;

          case Keys.BrowserRefresh:
            break;

          case Keys.BrowserSearch:
            break;

          case Keys.BrowserStop:
            break;

          case Keys.C:
            this.CopySelectedFiles();
            break;

          case Keys.Cancel:
            break;

          case Keys.Capital:
            break;

          case Keys.Clear:
            break;

          case Keys.Control:
            break;

          case Keys.ControlKey:
            break;

          case Keys.Crsel:
            break;

          case Keys.D:
            this.DeSelectAllItems();
            break;

          case Keys.D0:
            break;

          case Keys.D1:
            break;

          case Keys.D2:
            break;

          case Keys.D3:
            break;

          case Keys.D4:
            break;

          case Keys.D5:
            break;

          case Keys.D6:
            break;

          case Keys.D7:
            break;

          case Keys.D8:
            break;

          case Keys.D9:
            break;

          case Keys.Decimal:
            break;

          case Keys.Delete:
            break;

          case Keys.Divide:
            break;

          case Keys.Down:
            break;

          case Keys.E:
            break;

          case Keys.End:
            break;

          case Keys.Enter:
            break;

          case Keys.EraseEof:
            break;

          case Keys.Escape:
            break;

          case Keys.Execute:
            break;

          case Keys.Exsel:
            break;

          case Keys.F:
            break;

          case Keys.F1:
            break;

          case Keys.F10:
            break;

          case Keys.F11:
            break;

          case Keys.F12:
            break;

          case Keys.F13:
            break;

          case Keys.F14:
            break;

          case Keys.F15:
            break;

          case Keys.F16:
            break;

          case Keys.F17:
            break;

          case Keys.F18:
            break;

          case Keys.F19:
            break;

          case Keys.F2:
            break;

          case Keys.F20:
            break;

          case Keys.F21:
            break;

          case Keys.F22:
            break;

          case Keys.F23:
            break;

          case Keys.F24:
            break;

          case Keys.F3:
            break;

          case Keys.F4:
            break;

          case Keys.F5:
            break;

          case Keys.F6:
            break;

          case Keys.F7:
            break;

          case Keys.F8:
            break;

          case Keys.F9:
            break;

          case Keys.FinalMode:
            break;

          case Keys.G:
            break;

          case Keys.H:
            break;

          case Keys.HanguelMode:
            break;

          case Keys.HanjaMode:
            break;

          case Keys.Help:
            break;

          case Keys.Home:
            break;

          case Keys.I:
            this.InvertSelection();
            break;

          case Keys.IMEAccept:
            break;

          case Keys.IMEConvert:
            break;

          case Keys.IMEModeChange:
            break;

          case Keys.IMENonconvert:
            break;

          case Keys.Insert:
            break;

          case Keys.J:
            break;

          case Keys.JunjaMode:
            break;

          case Keys.K:
            break;

          case Keys.KeyCode:
            break;

          case Keys.L:
            break;

          case Keys.LButton:
            break;

          case Keys.LControlKey:
            break;

          case Keys.LMenu:
            break;

          case Keys.LShiftKey:
            break;

          case Keys.LWin:
            break;

          case Keys.LaunchApplication1:
            break;

          case Keys.LaunchApplication2:
            break;

          case Keys.LaunchMail:
            break;

          case Keys.Left:
            break;

          case Keys.LineFeed:
            break;

          case Keys.M:
            break;

          case Keys.MButton:
            break;

          case Keys.MediaNextTrack:
            break;

          case Keys.MediaPlayPause:
            break;

          case Keys.MediaPreviousTrack:
            break;

          case Keys.MediaStop:
            break;

          case Keys.Menu:
            break;

          case Keys.Modifiers:
            break;

          case Keys.Multiply:
            break;

          case Keys.N:
            break;

          case Keys.NoName:
            break;

          case Keys.None:
            break;

          case Keys.NumLock:
            break;

          case Keys.NumPad0:
            break;

          case Keys.NumPad1:
            break;

          case Keys.NumPad2:
            break;

          case Keys.NumPad3:
            break;

          case Keys.NumPad4:
            break;

          case Keys.NumPad5:
            break;

          case Keys.NumPad6:
            break;

          case Keys.NumPad7:
            break;

          case Keys.NumPad8:
            break;

          case Keys.NumPad9:
            break;

          case Keys.O:
            break;

          case Keys.Oem1:
            break;

          case Keys.Oem102:
            break;

          case Keys.Oem2:
            break;

          case Keys.Oem3:
            break;

          case Keys.Oem4:
            break;

          case Keys.Oem5:
            break;

          case Keys.Oem6:
            break;

          case Keys.Oem7:
            break;

          case Keys.Oem8:
            break;

          case Keys.OemClear:
            break;

          case Keys.OemMinus:
            break;

          case Keys.OemPeriod:
            break;

          case Keys.Oemcomma:
            break;

          case Keys.Oemplus:
            break;

          case Keys.P:
            break;

          case Keys.Pa1:
            break;

          case Keys.PageDown:
            break;

          case Keys.PageUp:
            break;

          case Keys.Pause:
            break;

          case Keys.Play:
            break;

          case Keys.Print:
            break;

          case Keys.PrintScreen:
            break;

          case Keys.ProcessKey:
            break;

          case Keys.Q:
            break;

          case Keys.R:
            break;

          case Keys.RButton:
            break;

          case Keys.RControlKey:
            break;

          case Keys.RMenu:
            break;

          case Keys.RShiftKey:
            break;

          case Keys.RWin:
            break;

          case Keys.Right:
            break;

          case Keys.S:
            break;

          case Keys.Scroll:
            break;

          case Keys.Select:
            break;

          case Keys.SelectMedia:
            break;

          case Keys.Separator:
            break;

          case Keys.Shift:
            break;

          case Keys.ShiftKey:
            break;

          case Keys.Space:
            break;

          case Keys.Subtract:
            break;

          case Keys.T:
            break;

          case Keys.Tab:
            break;

          case Keys.U:
            // var copy = new AsyncUnbuffCopy();
            // copy.AsyncCopyFileUnbuffered(@"J:\Downloads\advinst.msi", @"J:\Downloads\advinst(2).msi", true, false, false, 4096*5, false, 100000);
            break;

          case Keys.Up:
            break;

          case Keys.V:
            this.PasteAvailableFiles();
            break;

          case Keys.VolumeDown:
            break;

          case Keys.VolumeMute:
            break;

          case Keys.VolumeUp:
            break;

          case Keys.W:
            break;

          case Keys.X:
            this.CutSelectedFiles();
            break;

          case Keys.XButton1:
            break;

          case Keys.XButton2:
            break;

          case Keys.Y:
            break;

          case Keys.Z:
            break;

          case Keys.Zoom:
            break;

          default:
            break;
        }
      } else if (!(System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox)) {
        if (e == Keys.Back || e == Keys.BrowserBack) {
          this.NavigateParent();
        }

        if (e == Keys.Escape) {
          foreach (var index in this._CuttedIndexes) {
            this._IIListView.SetItemState(index, LVIF.LVIF_STATE, LVIS.LVIS_CUT, 0);
          }

          if (this._CuttedIndexes.Any()) {
            this._CuttedIndexes.Clear();
            F.Clipboard.Clear();
          }
        }

        if (e == Keys.Delete) {
          this.DeleteSelectedFiles((Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != System.Windows.Input.ModifierKeys.Shift);
        }

        if (e == Keys.F5) {
          this.RefreshContents();
        }
      }

      return true;
    }

    #endregion Events

    #region Overrides

    /// <inheritdoc/>
    protected override void OnDragDrop(F.DragEventArgs e) {
      var row = -1;
      var collumn = -1;
      this.HitTest(this.PointToClient(new DPoint(e.X, e.Y)), out row, out collumn);
      var destination = row != -1 ? this.Items[row] : this.CurrentFolder;
      if (!destination.IsFolder || (this._DraggedItemIndexes.Count > 0 && this._DraggedItemIndexes.Contains(row))) {
        if ((e.Effect == F.DragDropEffects.Link || e.Effect == F.DragDropEffects.Copy) && destination.Parent != null && destination.Parent.IsFolder) {
          if (e.Effect == F.DragDropEffects.Copy) {
            this.DoCopy(e.Data, destination);
          }
        } else
          e.Effect = F.DragDropEffects.None;
      } else {
        switch (e.Effect) {
          case F.DragDropEffects.Copy:
            this.DoCopy(e.Data, destination);
            break;

          case F.DragDropEffects.Link:
            System.Windows.MessageBox.Show("Link creation not implemented yet!", "Not implemented", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            break;

          case F.DragDropEffects.Move:
            this.DoMove(e.Data, destination);
            break;

          case F.DragDropEffects.All:
          case F.DragDropEffects.None:
          case F.DragDropEffects.Scroll:
            break;
        }
      }

      var wp = new DataObject.Win32Point() { X = e.X, Y = e.Y };

      if (e.Data.GetDataPresent("DragImageBits")) {
        DropTarget.Create.Drop((System.Runtime.InteropServices.ComTypes.IDataObject)e.Data, ref wp, (Int32)e.Effect);
      } else {
        base.OnDragDrop(e);
      }

      this.RefreshItem(this._LastDropHighLightedItemIndex);
      this._LastDropHighLightedItemIndex = -1;
    }

    /// <inheritdoc/>
    protected override void OnDragLeave(EventArgs e) {
      try {
        this.RefreshItem(this._LastDropHighLightedItemIndex);
        this._LastDropHighLightedItemIndex = -1;
      } catch {
        // ignored
      }

      DropTarget.Create.DragLeave();
    }

    /// <inheritdoc/>
    protected override void OnDragOver(F.DragEventArgs e) {
      var wp = new DataObject.Win32Point() { X = e.X, Y = e.Y };
      Drag_SetEffect(e);

      Int32 row = -1, collumn = -1;
      this.HitTest(this.PointToClient(new DPoint(e.X, e.Y)), out row, out collumn);
      var descinvalid = default(DataObject.DropDescription);
      descinvalid.type = (Int32)DataObject.DropImageType.Invalid;
      var ddResult = ((System.Runtime.InteropServices.ComTypes.IDataObject)e.Data).SetDropDescription(descinvalid);
      if (row != -1) {
        this.RefreshItem(this._LastDropHighLightedItemIndex);
        this._LastDropHighLightedItemIndex = row;
        this.RefreshItem(row);
        if (ddResult == HResult.S_OK) {
          var desc = default(DataObject.DropDescription);
          switch (e.Effect) {
            case F.DragDropEffects.Copy:
              desc.type = (Int32)DataObject.DropImageType.Copy;
              desc.szMessage = "Copy To %1";
              break;

            case F.DragDropEffects.Link:
              desc.type = (Int32)DataObject.DropImageType.Link;
              desc.szMessage = "Create Link in %1";
              break;

            case F.DragDropEffects.Move:
              desc.type = (Int32)DataObject.DropImageType.Move;
              desc.szMessage = "Move To %1";
              break;

            case F.DragDropEffects.None:
              desc.type = (Int32)DataObject.DropImageType.None;
              desc.szMessage = String.Empty;
              break;

            default:
              desc.type = (Int32)DataObject.DropImageType.Invalid;
              desc.szMessage = String.Empty;
              break;
          }

          desc.szInsert = this.Items[row].DisplayName;
          if (this._DraggedItemIndexes.Contains(row) || !this.Items[row].IsFolder) {
            if (this.Items[row].Extension == ".exe") {
              desc.type = (Int32)DataObject.DropImageType.Copy;
              desc.szMessage = "Open With %1";
            } else {
              desc.type = (Int32)DataObject.DropImageType.None;
              desc.szMessage = "Cant Drop Here!";
            }
          }

          ((System.Runtime.InteropServices.ComTypes.IDataObject)e.Data).SetDropDescription(desc);
        }
      } else {
        this.RefreshItem(this._LastDropHighLightedItemIndex);
        this._LastDropHighLightedItemIndex = -1;
        if (ddResult == HResult.S_OK) {
          switch (e.Effect) {
            case F.DragDropEffects.Link: {
                var desc = default(DataObject.DropDescription);
                desc.type = (Int32)DataObject.DropImageType.Link;
                desc.szMessage = "Create Link in %1";
                desc.szInsert = this.CurrentFolder.DisplayName;
                ((System.Runtime.InteropServices.ComTypes.IDataObject)e.Data).SetDropDescription(desc);
              }

              break;
            case F.DragDropEffects.Copy: {
                var desc = default(DataObject.DropDescription);
                desc.type = (Int32)DataObject.DropImageType.Link;
                desc.szMessage = "Create a copy in %1";
                desc.szInsert = this.CurrentFolder.DisplayName;
                ((System.Runtime.InteropServices.ComTypes.IDataObject)e.Data).SetDropDescription(desc);
              }

              break;
            case F.DragDropEffects.None:
              break;
            case F.DragDropEffects.Move:
              break;
            case F.DragDropEffects.Scroll:
              break;
            case F.DragDropEffects.All:
              break;
            default:
              throw new ArgumentOutOfRangeException();
          }
        }
      }

      if (e.Data.GetDataPresent("DragImageBits")) {
        DropTarget.Create.DragOver(ref wp, (Int32)e.Effect);
      } else {
        base.OnDragOver(e);
      }
    }

    /// <inheritdoc/>
    protected override void OnDragEnter(F.DragEventArgs e) {
      var wp = new DataObject.Win32Point() { X = e.X, Y = e.Y };
      Drag_SetEffect(e);

      if (e.Data.GetDataPresent("DragImageBits")) {
        DropTarget.Create.DragEnter(this.Handle, (System.Runtime.InteropServices.ComTypes.IDataObject)e.Data, ref wp, (Int32)e.Effect);
      } else {
        base.OnDragEnter(e);
      }
    }

    /// <inheritdoc/>
    protected override void OnQueryContinueDrag(F.QueryContinueDragEventArgs e) => base.OnQueryContinueDrag(e);

    /// <inheritdoc/>
    protected override void OnGiveFeedback(F.GiveFeedbackEventArgs e) {
      e.UseDefaultCursors = true;
      var doo = new F.DataObject(this._DataObject);

      if (doo.GetDataPresent("DragWindow")) {
        var hwnd = GetIntPtrFromData(doo.GetData("DragWindow"));
        User32.PostMessage(hwnd, 0x403, IntPtr.Zero, IntPtr.Zero);
      } else {
        e.UseDefaultCursors = true;
      }

      if (IsDropDescriptionValid(this._DataObject)) {
        e.UseDefaultCursors = false;
        F.Cursor.Current = F.Cursors.Arrow;
      } else {
        e.UseDefaultCursors = true;
      }

      if (IsShowingLayered(doo)) {
        e.UseDefaultCursors = false;
        F.Cursor.Current = F.Cursors.Arrow;
      } else {
        e.UseDefaultCursors = true;
      }
    }

    private Double _OldValue;
    private System.Windows.Controls.Primitives.ScrollBar _VScroll;
    [DllImport("user32.dll")]
    static extern int SetScrollInfo(IntPtr hwnd, int fnBar, [In] ref SCROLLINFO lpsi, bool fRedraw);
    public System.Windows.Controls.Primitives.ScrollBar VScroll {
      get => this._VScroll;
      set {
        this._VScroll = value;
        this.VScroll.ValueChanged += (sender, args) => {
          this.VScroll.UpdateLayout();
          this.VScroll.InvalidateVisual();
          this._PreventScrollEvent = true;
          if (!this._PreventScrollValueEvent) {
            //var dy1 = args.NewValue - this._OldValue;
            //if (dy1 > 120) {
            this._OldValue = args.OldValue;
            //} else {
            //  return;
            //}

            var dy = (Int32)Math.Round((args.NewValue - this._OldValue), 0);
            var shouldChange = Math.Abs(dy) >= 3;
            var curScroll = this.GetScrollPosition();
            if (args.NewValue == 0D) {
              dy = -curScroll.nPos;
            }

            if (args.NewValue >= curScroll.nMax - curScroll.nPage) {
              dy = curScroll.nMax - curScroll.nPos;
            }

            if (this.View == ShellViewStyle.Details && !this.IsGroupsEnabled) {
              var itemIndex = (Int32)(Math.Ceiling(args.NewValue)) - 1;
              var lvi = new LVITEMINDEX() { iItem = itemIndex, iGroup = this.GetGroupIndex(itemIndex) };
              this._IIListView.GetItemPosition(lvi, out var ptTo);
              var itemIndexFrom = (Int32)(Math.Ceiling(args.OldValue)) - 1;
              var lviFrom = new LVITEMINDEX() { iItem = itemIndexFrom, iGroup = this.GetGroupIndex(itemIndexFrom) };
              this._IIListView.GetItemPosition(lviFrom, out var ptFrom);
              dy = ptTo.y - ptFrom.y;
            }

            if (this.View == ShellViewStyle.Details && !this.IsGroupsEnabled) {
              if (true) {
                var t = new Thread((() => {
                  this.BeginInvoke((Action)(() => { User32.SendMessage(this.LVHandle, MSG.LVM_SCROLL, 0, dy); }));
                }));
                t.Priority = ThreadPriority.Lowest;
                t.Start();
              }
            } else {
              //var t = new Thread((() => { User32.SendMessage(this.LVHandle, MSG.LVM_SCROLL, 0, dy); }));
              //t.Start();
              User32.SendMessage(this.LVHandle, MSG.LVM_SCROLL, 0, dy);
            }


          } else {
            this._PreventScrollValueEvent = false;
          }
        };
      }
    }

    public SCROLLINFO GetScrollPosition() {
      var scrollinfo = new SCROLLINFO {
        cbSize = (uint)Marshal.SizeOf(typeof(SCROLLINFO)),
        fMask = ScrollInfoMask.SIF_ALL
      };
      if (User32.GetScrollInfo(this.LVHandle, (int)SBOrientation.SB_VERT, ref scrollinfo)) {
        return scrollinfo;
      } else {
        return new SCROLLINFO();
      }
    }

    private Boolean _PreventScrollEvent = false;
    private Boolean _PreventScrollValueEvent = false;
    private Int32 _ScrollAmount = 0;
    public void ScrollListView(System.Windows.Controls.Primitives.ScrollBar sb) {
      //this._ScrollAmount += dy;
      this._PreventScrollEvent = true;
      if (!this._PreventScrollValueEvent) {
        //var curScroll = this.GetScrollPosition();
        //if ((curScroll.nPos + dy) < 0) {
        //  dy = -curScroll.nPos;
        //}

        //var t = new Thread((() => {
        User32.SendMessage(this.LVHandle, MSG.LVM_SCROLL, 0, (Int32)(sb.Value - this._ScrollAmount));
        this._ScrollAmount = (Int32)sb.Value;
        //}));
        //t.SetApartmentState(ApartmentState.STA);
        //t.Start();

        //curScroll = this.GetScrollPosition();
        //this._PreventScrollValueEvent = true;
        //this.OnLVScroll?.Invoke(this, new ScrollEventArgs(curScroll) { IsPositionChangedOnly = true });

      } else {
        this._PreventScrollValueEvent = false;
      }
    }
    private Thread _ScrollUpdateThread { get; set; }
    /// <inheritdoc/>
    protected override void WndProc(ref Message m) {
      try {
        if (m.Msg == 0x007D) {

        }
        if (m.Msg == (Int32)WM.WM_PARENTNOTIFY && User32.LOWORD((Int32)m.WParam) == (Int32)WM.WM_MBUTTONDOWN) {
          this.OnItemMiddleClick();
        } else if (m.Msg == ShellNotifications.WM_SHNOTIFY) {
          this.ProcessShellNotifications(m.WParam, m.LParam);
        } else if (m.Msg == 78) {
          var nmhdr = m.GetLParam<NMHDR>();
          switch (nmhdr.code) {
            case WNM.LVN_GETEMPTYMARKUP:
              if (this._IsDisplayEmptyText) {
                var nmlvem = m.GetLParam<NMLVEMPTYMARKUP>();
                nmlvem.dwFlags = 0x1;
                nmlvem.szMarkup = "Working on it...";
                Marshal.StructureToPtr(nmlvem, m.LParam, false);
                m.Result = (IntPtr)1;
              } else {
                m.Result = IntPtr.Zero;
              }

              break;

            case WNM.LVN_GETDISPINFOW:
              var nmlv = m.GetLParam<NMLVDISPINFO>();
              if (this.Items.Count == 0 || this.Items.Count - 1 < nmlv.item.iItem || ((nmlv.item.mask & LVIF.LVIF_TEXT) == 0 && (nmlv.item.mask & LVIF.LVIF_COLUMNS) == 0)) {
                break;
              }
              if (this.CurrentlyUpdatingItems.ContainsKey(nmlv.item.iItem)) {
                break;
              }
              var currentItem = this.Items[nmlv.item.iItem];

              if ((nmlv.item.mask & LVIF.LVIF_TEXT) == LVIF.LVIF_TEXT && (this.View != ShellViewStyle.Tile)) {
                if (nmlv.item.iSubItem == 0) {
                  nmlv.item.pszText = currentItem.DisplayName;
                  Marshal.StructureToPtr(nmlv, m.LParam, false);
                } else {
                  break;
                }
              }

              if ((nmlv.item.mask & LVIF.LVIF_TEXT) == LVIF.LVIF_TEXT && this.View == ShellViewStyle.Tile && this.CurrentFolder?.ParsingName.Equals(KnownFolders.Computer.ParsingName) == false) {
                if (currentItem.cColumns == null) {
                  var refGuidPDL = typeof(IPropertyDescriptionList).GUID;
                  var refGuidPD = typeof(IPropertyDescription).GUID;
                  var iShellItem2 = (IShellItem2)currentItem.ComInterface;

                  var ptrPDL = IntPtr.Zero;
                  iShellItem2.GetPropertyDescriptionList(SpecialProperties.PropListTileInfo, ref refGuidPDL, out ptrPDL);
                  if (ptrPDL != IntPtr.Zero) {
                    var propertyDescriptionList = (IPropertyDescriptionList)Marshal.GetObjectForIUnknown(ptrPDL);
                    var descriptionsCount = 0u;
                    propertyDescriptionList.GetCount(out descriptionsCount);
                    var columns = new Int32[(Int32)descriptionsCount];
                    for (UInt32 i = 0; i < descriptionsCount; i++) {
                      propertyDescriptionList.GetAt(i, ref refGuidPD, out var propertyDescription);
                      propertyDescription.GetPropertyKey(out var pkey);
                      if (this.AllAvailableColumns.TryGetValue(pkey, out var column)) {
                        columns[i] = column.Index;
                      } else {
                        columns[i] = 0;
                      }
                    }

                    currentItem.cColumns = columns;
                    Marshal.ReleaseComObject(propertyDescriptionList);
                  }
                }

              }
              break;

            case WNM.LVN_COLUMNCLICK:
              var nlcv = m.GetLParam<NMLISTVIEW>();
              var sortOrder = SortOrder.Ascending;
              if (this.LastSortedColumnId == this.Collumns[nlcv.iSubItem].ID) {
                sortOrder = this.LastSortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
              }

              if (!this.IsGroupsEnabled) {
                this.SetSortCollumn(true, this.Collumns[nlcv.iSubItem], sortOrder);
              } else if (this.LastGroupCollumn == this.Collumns[nlcv.iSubItem]) {
                this.SetGroupOrder();
              } else {
                this.SetSortCollumn(true, this.Collumns[nlcv.iSubItem], sortOrder);
                this.SetGroupOrder(false);
              }

              break;

            case WNM.LVN_GETINFOTIP:
              var nmGetInfoTip = m.GetLParam<NMLVGETINFOTIP>();
              if (this.Items.Count == 0) {
                break;
              }

              if (this._SkipSelection) {
                this._SkipSelection = false;
                nmGetInfoTip.iItem = -1;
                nmGetInfoTip.pszText = IntPtr.Zero;
                Marshal.StructureToPtr(nmGetInfoTip, m.LParam, false);
                m.Result = IntPtr.Zero;
                break;
              }

              var itemInfotip = this.Items[nmGetInfoTip.iItem];
              var charBuf = "\0".ToCharArray();
              Marshal.Copy(charBuf, 0, nmGetInfoTip.pszText, Math.Min(charBuf.Length, nmGetInfoTip.cchTextMax));
              Marshal.StructureToPtr(nmGetInfoTip, m.LParam, false);


              ToolTip.HideTooltip();
              ToolTip.ShowTooltip(this, itemInfotip, nmGetInfoTip.iItem, nmGetInfoTip.dwFlags);

              break;

            case WNM.LVN_ODFINDITEM:
              ToolTip.HideTooltip();

              var findItem = m.GetLParam<NMLVFINDITEM>();
              this.KeyJumpString = findItem.lvfi.psz;

              this.KeyJumpKeyDown?.Invoke(this, new F.KeyEventArgs(Keys.A));
              var startindex = this.GetFirstSelectedItemIndex() + (this.KeyJumpString.Length > 1 ? 0 : 1);
              var selind = this.GetFirstIndexOf(this.KeyJumpString, startindex);
              if (selind != -1) {
                m.Result = (IntPtr)selind;
                if (this.IsGroupsEnabled) {
                  this.SelectItemByIndex(selind, true, true);
                }
              } else {
                var selindOver = this.GetFirstIndexOf(this.KeyJumpString, 0);
                if (selindOver != -1) {
                  m.Result = (IntPtr)selindOver;
                  if (this.IsGroupsEnabled) {
                    this.SelectItemByIndex(selindOver, true, true);
                  }
                }
              }

              break;

            case -175:
              var nmlvLe = m.GetLParam<NMLVDISPINFO>();

              ToolTip.HideTooltip();

              this.IsFocusAllowed = false;
              this._IsCanceledOperation = false;
              this._ItemForRename = nmlvLe.item.iItem;
              this.IsRenameInProgress = true;
              this.RefreshItem(this._ItemForRename);
              this.BeginItemLabelEdit?.Invoke(this, new RenameEventArgs(this._ItemForRename));
              m.Result = (IntPtr)1;
              //var editControl = User32.SendMessage(this.LVHandle, 0x1018, 0, 0);
              //this._EditorSubclass = new ListViewEditor(editControl, this);
              //var itemForRename = this.Items[this._ItemForRename];
              //var displayName = itemForRename.DisplayName;
              //if (this.View == ShellViewStyle.Tile) {
              //  User32.SetWindowText(editControl, displayName);
              //}

              //var indexLastDot = this.IsFileExtensionShown ? displayName.LastIndexOf(".", StringComparison.Ordinal) : displayName.Length;
              //User32.SendMessage(editControl, 0x00B1, 0, indexLastDot);
              break;

            case WNM.LVN_ENDLABELEDITW:
              //var nmlvedit = m.GetLParam<NMLVDISPINFO>();
              //if (!String.IsNullOrEmpty(nmlvedit.item.pszText)) {
              //  var item = this.Items[nmlvedit.item.iItem];
              //  if (!item.DisplayName.Equals(nmlvedit.item.pszText, StringComparison.InvariantCultureIgnoreCase)) {
              //    this.RenameShellItem(item.ComInterface, nmlvedit.item.pszText, item.DisplayName != Path.GetFileName(item.ParsingName) && !item.IsFolder, item.Extension);
              //  }
              //}
              //this.EndLabelEdit();

              //this.IsRenameInProgress = false;
              this._EditorSubclass?.DestroyHandle();
              break;

            case WNM.LVN_ITEMACTIVATE:
              var iac = m.GetLParam<NMITEMACTIVATE>();
              var lvitemindexac = new LVITEMINDEX();
              lvitemindexac.iGroup = this.GetGroupIndex(iac.iItem);
              lvitemindexac.iItem = iac.iItem;
              this._IIListView.GetItemRect(lvitemindexac, 0, out var itemRectac);
              if (!itemRectac.ToRectangle(2).Contains(iac.ptAction)) {
                iac.iItem = -1;
                Marshal.StructureToPtr(iac, m.LParam, false);
                break;
              }

              ToolTip.HideTooltip();


              if (this._ItemForRealNameIsAny && this.IsRenameInProgress) {
                this.EndLabelEdit();
              } else {

                var selectedItem = this.Items[iac.iItem];
                if (selectedItem.IsFolder) {
                  this.Navigate_Full(selectedItem, true);
                } else if (selectedItem.IsLink || selectedItem.ParsingName.EndsWith(".lnk")) {
                  var shellLink = new ShellLink(selectedItem.ParsingName);
                  var newSho = FileSystemListItem.ToFileSystemItem(this.LVHandle, shellLink.TargetPIDL);
                  if (newSho.IsFolder) {
                    this.Navigate_Full(newSho, true);
                  }
                } else {
                  this.StartProcessInCurrentDirectory(selectedItem);
                }
              }

              break;

            case WNM.LVN_BEGINSCROLL:
              this.EndLabelEdit();
              this.LargeImageList.ResetEvent.Reset();
              this._ResetEvent.Reset();
              this._ResetTimer.Stop();
              ToolTip.HideTooltip();
              this.ScrollSyncEvent.Reset();
              if (!this._PreventScrollEvent) {
                //var scrollInfo = this.GetScrollPosition();
                //this.Vscroll2.ScrollInfo = scrollInfo;
                //this.Vscroll2.Refresh();
                //this._PreventScrollValueEvent = true;
                //Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)(() => {
                var scrollInfo = this.GetScrollPosition();
                this._PreventScrollValueEvent = true;
                this.VScroll.Value = scrollInfo.nPos;
                //}));

              }

              break;

            case WNM.LVN_ENDSCROLL:
              this._PreventScrollValueEvent = false;
              this._PreventScrollEvent = false;
              this.ScrollUpdateThreadRun(true);
              //Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() => {
              //  var scrollInfo = this.GetScrollPosition();
              //  this._PreventScrollValueEvent = true;
              //  this.VScroll.Value = scrollInfo.nPos;
              //  this._PreventScrollEvent = false;
              //}));


              this._ResetTimer.Start();
              break;

            case WNM.LVN_ITEMCHANGED:
              var nlv = m.GetLParam<NMLISTVIEW>();
              //if (nlv.iItem > -1) {
              //  var cursorPos = User32.GetCursorPosition();
              //  var lvitemindexChanged = new LVITEMINDEX();
              //  lvitemindexChanged.iGroup = this.GetGroupIndex(nlv.iItem);
              //  lvitemindexChanged.iItem = nlv.iItem;
              //  this._IIListView.GetItemRect(lvitemindexChanged, 0, out var itemRectChanged);
              //  User32.GetWindowRect(this.LVHandle, out var rect);
              //  var pt = new System.Drawing.Point(cursorPos.X - rect.Left, cursorPos.Y - rect.Top);

              //  if (!itemRectChanged.ToRectangle(3).Contains(pt) &&
              //      (nlv.uNewState & LVIS.LVIS_SELECTED) == LVIS.LVIS_SELECTED) {
              //    this._IIListView.SetItemState(nlv.iItem, LVIF.LVIF_STATE, LVIS.LVIS_SELECTED, 0);
              //    break;
              //  }
              //}
              //if (this.IconSize == 16 && this._SkipSelection && (nlv.uNewState & LVIS.LVIS_SELECTED) == LVIS.LVIS_SELECTED) {
              //  this._IIListView.SetItemState(nlv.iItem, LVIF.LVIF_STATE, LVIS.LVIS_SELECTED, 0);
              //  this._SkipSelection = false;
              //}

              //if (this._SkipSelection) {
              //  this._SkipSelection = false;
              //}

              //if (this.IconSize == 16 && this._SkipSelection &&
              //    (nlv.uNewState & LVIS.LVIS_SELECTED) == LVIS.LVIS_SELECTED) {
              //  break;
              //}

              if ((nlv.uChanged & LVIF.LVIF_STATE) == LVIF.LVIF_STATE) {
                this._IsDragSelect = nlv.uNewState;
                if (nlv.iItem != this._ItemForRename) {
                  //this._LastItemForRename = -1;
                  this.EndLabelEdit();
                }

                if (!this._SelectionTimer.Enabled) {
                  this._SelectionTimer.Start();
                }
              }
              break;

            case WNM.LVN_ODSTATECHANGED:
              this.OnSelectionChanged();
              break;

            case WNM.LVN_KEYDOWN:
              this.Focus();
              var nkd = m.GetLParam<NMLVKEYDOWN>();
              if (!this.ShellView_KeyDown((Keys)((Int32)nkd.wVKey))) {
                m.Result = (IntPtr)1;
                break;
              }

              if (nkd.wVKey == (Int16)Keys.F2 && !(System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox)) {
                this.RenameSelectedItem();
              }

              if (!this._ItemForRealNameIsAny && !this.IsRenameInProgress && !(System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox)) {
                switch (nkd.wVKey) {
                  case (Int16)Keys.Enter:
                    //if (this._IsCanceledOperation) {
                    //  // this.IsRenameInProgress = false;
                    //  break;
                    //}

                    var selectedItem = this.GetFirstSelectedItem();
                    if (selectedItem.IsFolder) {
                      this.Navigate(selectedItem, false, false, this.IsNavigationInProgress);
                    } else if (selectedItem.IsLink && selectedItem.ParsingName.EndsWith(".lnk")) {
                      var shellLink = new ShellLink(selectedItem.ParsingName);
                      var newSho = new FileSystemListItem();
                      newSho.Initialize(this.LVHandle, shellLink.TargetPIDL);
                      if (newSho.IsFolder) {
                        this.Navigate(newSho, false, false, this.IsNavigationInProgress);
                      } else {
                        this.StartProcessInCurrentDirectory(newSho);
                      }

                      shellLink.Dispose();
                    } else {
                      this.StartProcessInCurrentDirectory(selectedItem);
                    }

                    break;
                }

                this.Focus();
              } else {
                switch (nkd.wVKey) {
                  case (Int16)Keys.Enter:
                    if (!this.IsRenameInProgress) {
                      this.EndLabelEdit();
                    }

                    this.Focus();
                    break;

                  case (Int16)Keys.Escape:
                    this.EndLabelEdit(true);
                    this.Focus();
                    break;
                  default:
                    break;
                }

                if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox) {
                  m.Result = (IntPtr)1;
                  break;
                }
              }

              break;

            case WNM.LVN_GROUPINFO: // TODO: Deal with this useless code
              m.Result = (IntPtr)1;
              // RedrawWindow();
              break;

            case WNM.LVN_HOTTRACK:
              var nlvHotTrack = m.GetLParam<NMLISTVIEW>();
              var lvitemindex = new LVITEMINDEX();
              if (nlvHotTrack.iItem > -1) {
                lvitemindex.iGroup = this.GetGroupIndex(nlvHotTrack.iItem);
                lvitemindex.iItem = nlvHotTrack.iItem;
                this._IIListView.GetItemRect(lvitemindex, 0, out var itemRect);
                if (!itemRect.ToRectangle(2).Contains(nlvHotTrack.ptAction.ToPoint())) {
                  if (true) {
                    nlvHotTrack.iItem = -1;
                    Marshal.StructureToPtr(nlvHotTrack, m.LParam, false);
                    this._SkipSelection = true;
                    ToolTip.HideTooltip();
                    //this.Focus();
                  }
                  break;
                }

                this._SkipSelection = false;
                if (ToolTip.Instance != null && nlvHotTrack.iItem != ToolTip.Instance.ItemIndex &&
                    ToolTip.Instance.ItemIndex > -1) {
                  ToolTip.HideTooltip();
                  this.Focus();
                }
              } else {
                this._SkipSelection = false;
              }

              break;

            case WNM.LVN_BEGINDRAG:
              this._DraggedItemIndexes.Clear();
              var dataObjPtr = IntPtr.Zero;
              this._DataObject = this.SelectedItems.ToArray().GetIDataObject(out dataObjPtr);

              // uint ef = 0;
              var ishell2 = (DataObject.IDragSourceHelper2)new DragDropHelper();
              ishell2.SetFlags(1);
              var wp = new DataObject.Win32Point() { X = F.Cursor.Position.X, Y = F.Cursor.Position.Y };
              ishell2.InitializeFromWindow(this.Handle, ref wp, this._DataObject);
              this.DoDragDrop(this._DataObject, F.DragDropEffects.All | F.DragDropEffects.Link);
              break;

            case WNM.NM_RCLICK:
              var nmhdrHdn = m.GetLParam<NMHEADER>();
              var itemActivate = m.GetLParam<NMITEMACTIVATE>();
              ToolTip.HideTooltip();
              //if (nmhdrHdn.iItem > -1) {
              //  lvitemindex = new LVITEMINDEX();
              //  lvitemindex.iGroup = this.GetGroupIndex(nmhdrHdn.iItem);
              //  lvitemindex.iItem = nmhdrHdn.iItem;
              //  this._IIListView.GetItemRect(lvitemindex, 0, out var itemRectRC);
              //  if (!itemRectRC.ToRectangle(2).Contains(itemActivate.ptAction)) {
              //    nmhdrHdn.iItem = -1;
              //    Marshal.StructureToPtr(nmhdrHdn, m.LParam, false);
              //  }
              //}

              this.IsFocusAllowed = false;
              if (nmhdrHdn.iItem != -1 && nmhdrHdn.hdr.hwndFrom == this.LVHandle) {
                // Workaround for cases where on right click the item is not actually selected
                if (this.GetSelectedCount() == 0) {
                  this.SelectItemByIndex(nmhdrHdn.iItem);
                }

                //var selitems = this.SelectedItems;
                //var cm = new ShellContextMenu.ShellContextMenu(selitems.ToArray(), SVGIO.SVGIO_SELECTION, this);
                //cm.ShowContextMenu(this, itemActivate.ptAction, CMF.CANRENAME);
                var cm = new ShellContextMenuEx(this);
                cm.ShowShellContextMenu(this, 0);
              } else if (nmhdrHdn.iItem == -1) {
                //var cm = new ShellContextMenu.ShellContextMenu(new IListItemEx[1] { this.CurrentFolder }, SVGIO.SVGIO_BACKGROUND, this);
                //cm.ShowContextMenu(this, itemActivate.ptAction, 0, true);
                var cm = new ShellContextMenuEx(this.CurrentFolder);
                cm.ShowShellContextMenu(this, 0);
              } else {
                this.IsFocusAllowed = true;
                this.ColumnHeaderRightClick?.Invoke(this, new F.MouseEventArgs(MouseButtons.Right, 1, MousePosition.X, MousePosition.Y, 0));
              }

              break;

            case WNM.NM_CLICK: // TODO: Deal with this useless code

              break;

            case WNM.NM_SETFOCUS:
              if (this.IsGroupsEnabled) {
                this.RedrawWindow();
              }

              this.ShellView_GotFocus();
              this.IsFocusAllowed = true;
              break;

            case WNM.NM_KILLFOCUS:
              if (this._ItemForRename != -1 && !this.IsRenameInProgress) {
                this.EndLabelEdit();
              }

              if (this.IsGroupsEnabled) {
                this.RedrawWindow();
              }

              ToolTip.HideTooltip();

              break;

            case CustomDraw.NM_CUSTOMDRAW:
              if (nmhdr.hwndFrom == this.LVHandle) {
                this.ProcessCustomDraw(ref m, ref nmhdr);
              } else if (nmhdr.hwndFrom == this.LVHeaderHandle) {
                this.ProcessHeaderCustomDraw(ref m);
              }
              break;
            case -13:
              break;
          }
        } else {
          if (this.IsRenameInProgress) {
            return;
          }
          base.WndProc(ref m);
        }
      } catch (Exception ex) {
      }

    }

    [DllImport("user32.dll")]
    static extern bool UpdateWindow(IntPtr hWnd);
    /// <inheritdoc/>
    protected override void OnSizeChanged(EventArgs e) {
      base.OnSizeChanged(e);
      //var currentStyle = User32.GetWindowLong(this.LVHandle, -16);
      //if ((currentStyle & 0x00200000) != 0) {
      //  this._VScrollWidth = 18;
      //} else {
      //  this._VScrollWidth = 0;
      //}
      //this.BeginInvoke((Action)(() => {
      if (this.VScroll != null && this.VScroll.Visibility == Visibility.Visible) {
        this._VScrollWidth = 18;
      }


      User32.MoveWindow(this.LVHandle, 0, 0, this.ClientRectangle.Width + this._VScrollWidth,
        this.ClientRectangle.Height, false);
      //}));
      //var rect = this.ClientRectangle;
      //User32.InvalidateRect(this.LVHandle, ref rect, true);

    }

    [DllImport("user32.dll")]
    static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT lpPaint);
    [DllImport("user32.dll")]
    static extern bool EndPaint(IntPtr hWnd, [In] ref PAINTSTRUCT lpPaint);

    [StructLayout(LayoutKind.Sequential)]
    struct PAINTSTRUCT {
      public IntPtr hdc;
      public bool fErase;
      public User32.RECT rcPaint;
      public bool fRestore;
      public bool fIncUpdate;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
    }


    private Boolean _IsLMouseButtonDown;
    private Boolean _IsDDOpen;
    private IntPtr LVHeaderWndProc(IntPtr hWnd, int uMsg, IntPtr wParam, IntPtr lParam) {
      //return User32.CallWindowProc(this._OldHeaderWndProc, hWnd, uMsg, wParam, lParam);
      //if (!this.IsRenameInProgress) {
      switch (uMsg) {
        case (int)WM.WM_ERASEBKGND:
          return IntPtr.Zero;
        case (int)WM.WM_WINDOWPOSCHANGING:
          var wp = (User32.WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(User32.WINDOWPOS));
          wp.cy = 0;
          Marshal.StructureToPtr(wp, lParam, true);
          break;
        case (int)WM.WM_PAINT:
          PAINTSTRUCT paintStruct;
          var hdc = BeginPaint(hWnd, out paintStruct);
          Gdi32.SetBkMode(hdc, 1);
          var brushBack = new SolidBrush(this.Theme.HeaderBackgroundColor.ToDrawingColor());
          var penBorder = new Pen(((System.Windows.Media.Color)Application.Current.Resources["ControlStrokeColorDefault"]).ToDrawingColor());
          var arrowPen = new Pen(this.Theme.HeaderArrowColor.ToDrawingColor(), 2);
          var bottomLine = 0;
          using (var g = Graphics.FromHdc(hdc)) {
            for (var i = 0; i < this.Collumns.Count; i++) {
              var hdHitTestInfo = new User32.HDHITTESTINFO();
              var point = this.PointToClient(F.Cursor.Position);
              hdHitTestInfo.pt.x = point.X;
              hdHitTestInfo.pt.y = point.Y;
              User32.SendMessage(this.LVHeaderHandle, 0x1200 + 6, 0, ref hdHitTestInfo);
              var collumnse = this.Collumns[i];
              var rect2 = new User32.RECT();
              User32.SendMessage(this.LVHeaderHandle, 0x1200 + 7, i, ref rect2);
              bottomLine = rect2.Bottom;
              //collumnse.SetSplitButton(this.LVHeaderHandle, i);

              var textRect = new User32.RECT(rect2.X + 5, rect2.Y, rect2.Right - 17, rect2.Bottom);

              var isHot = (i == hdHitTestInfo.iItem || this._IsLMouseButtonDown);
              var brush = new SolidBrush(isHot ? this.Theme.HeaderSelectionColor.ToDrawingColor() : collumnse.ID == this.LastSortedColumnId && this.View == ShellViewStyle.Details ? this.Theme.SortColumnColor.ToDrawingColor() : this.Theme.HeaderBackgroundColor.ToDrawingColor());

              g.FillRectangle(brush, rect2.X - 1, rect2.Y, rect2.Width + 2, rect2.Height);
              //g.DrawLine(penBorder, rect2.X, rect2.Y, rect2.Right - 2, rect2.Y);
              //g.DrawLine(penBorder, rect2.X, rect2.Bottom - 1, rect2.Right - 2, rect2.Bottom - 1);
              var separatorOffset = Settings.BESettings.CurrentTheme == "Dark" ? 2 : 1;
              g.DrawLine(penBorder, rect2.Left - separatorOffset, rect2.Y - 1, rect2.Left - separatorOffset, rect2.Bottom - 1);
              if (i == this.Collumns.Count - 1) {
                g.FillRectangle(brushBack, rect2.Right - 1, rect2.Y, this.ClientRectangle.Width - rect2.Right + 1, rect2.Height);
                g.DrawLine(penBorder, rect2.Right - separatorOffset, rect2.Y - 1, rect2.Right - separatorOffset, rect2.Bottom - 1);
                //g.DrawLine(penBorder, rect2.Right, rect2.Y, this.ClientRectangle.Width - rect2.Right, rect2.Y);
                //g.DrawLine(penBorder, rect2.Right, rect2.Bottom - 1, this.ClientRectangle.Width - rect2.Right, rect2.Bottom - 1);
              }

              if (isHot) {
                g.DrawLine(penBorder, rect2.Right - 17, rect2.Y, rect2.Right - 17, rect2.Bottom);
                g.DrawArrowHead(arrowPen, new PointF(rect2.Right - 17 + 15 / 2f, rect2.Height / 2f), 0, 4, 1);
              }

              if (collumnse.ID == this.LastSortedColumnId) {
                g.DrawArrowHead(arrowPen, new PointF(rect2.X + rect2.Width / 2f, this.LastSortOrder == SortOrder.Ascending ? 1 : 4), 0, this.LastSortOrder == SortOrder.Ascending ? -4 : 4, 1);
              }


              brush.Dispose();


              //if (paintStruct.rcPaint.Right == rect2.Right + 1) {
              var hFont = this.Font.ToHfont();
              Gdi32.SelectObject(hdc, hFont);
              Gdi32.SetTextColor(hdc, (int)this.Theme.TextColor.ToDrawingColor().ToWin32Color());


              User32.DrawText(hdc, collumnse.Name, -1, ref textRect, User32.TextFormatFlags.SingleLine | User32.TextFormatFlags.VCenter | User32.TextFormatFlags.EndEllipsis | User32.TextFormatFlags.NoPrefix);
              Gdi32.DeleteObject(hFont);


              //}
            }
            if (this.View != ShellViewStyle.Details) {
              g.DrawLine(penBorder, 0, bottomLine - 1, this.ClientRectangle.Width, bottomLine - 1);
            }
          }
          arrowPen.Dispose();
          penBorder.Dispose();
          brushBack.Dispose();
          //User32.InvalidateRect(this.LVHeaderHandle, IntPtr.Zero, true);
          EndPaint(hWnd, ref paintStruct);
          break;
        case (int)WM.WM_LBUTTONDOWN:
          break;
        case (int)WM.WM_LBUTTONUP:
          var hitPoint = lParam.ToPoint();
          var hdHitTestInfo2 = new User32.HDHITTESTINFO();
          hdHitTestInfo2.pt.x = hitPoint.X;
          hdHitTestInfo2.pt.y = hitPoint.Y;
          User32.SendMessage(this.LVHeaderHandle, 0x1200 + 6, 0, ref hdHitTestInfo2);

          var rectItem = new User32.RECT();
          User32.SendMessage(this.LVHeaderHandle, 0x1200 + 7, hdHitTestInfo2.iItem, ref rectItem);

          rectItem.Left = rectItem.Right - 17;

          var rc = new Rectangle(rectItem.X, rectItem.Y, rectItem.Width, rectItem.Height);
          if (rc.Contains(hitPoint)) {
            this.Column_OnClick(hdHitTestInfo2.iItem);
            return IntPtr.Zero;
          }
          break;
      }


      return User32.CallWindowProc(this._OldHeaderWndProc, hWnd, uMsg, wParam, lParam);
    }
    private Boolean _SkipSelection { get; set; }
    [StructLayout(LayoutKind.Sequential)]
    private struct STYLESTRUCT {
      public long styleOld;
      public long styleNew;
    }

    private Int32 _VScrollWidth = 0;
    public int HIWORD(int i) {
      return (short)(i >> 16);
    }
    private IntPtr LVWndProc(IntPtr hWnd, int uMsg, IntPtr wParam, IntPtr lParam) {
      //if (this.IsRenameInProgress) {
      //  //this.Focus(false, true);
      //  return IntPtr.Zero;//User32.CallWindowProc(this._OldWndProc, hWnd, uMsg, wParam, lParam);
      //}

      //if (uMsg == 0x0133) {
      //  Gdi32.SetBkMode(wParam, 1);
      //  Gdi32.SetBkColor(wParam, ShellView._BlackBrush);
      //  Gdi32.SetTextColor(wParam, Color.WhiteSmoke.ToWin32Color().ToInt32());
      //  if (this.IconSize != 16) {
      //    var itemForRename = this.Items[this._ItemForRename];
      //    User32.GetWindowRect(wParam, out var lpRect);
      //    var ptLocal = this.PointToClient(new DPoint(lpRect.X, lpRect.Y));
      //    User32.SetWindowPos(wParam, IntPtr.Zero, ptLocal.X, itemForRename.LabelBounds.Y - 2,
      //      0, 0, User32.SetWindowPosFlags.IgnoreResize | User32.SetWindowPosFlags.DoNotActivate | User32.SetWindowPosFlags.AsynchronousWindowPosition);
      //  }
      //  return ShellView._BlackBrush;

      //}

      //if (uMsg == 0x0111 && !this.IsRenameNeeded) {
      //  if (this.HIWORD(wParam.ToInt32()) == 0x0100) {
      //    //if (this.IconSize != 16) {
      //    //  var itemForRename = this.Items[this._ItemForRename];
      //    //  User32.SetWindowPos(lParam, IntPtr.Zero, itemForRename.LabelBounds.X - 6, itemForRename.LabelBounds.Y - 2,
      //    //    itemForRename.LabelBounds.Width + 12, itemForRename.LabelBounds.Height + 2, User32.SetWindowPosFlags.DoNotActivate | User32.SetWindowPosFlags.DoNotRedraw);
      //    //}
      //  } else if (this.HIWORD(wParam.ToInt32()) == 0x0300) {
      //    //if (this.IconSize != 16) {
      //    //  var itemForRename = this.Items[this._ItemForRename];
      //    //  User32.GetWindowRect(lParam, out var lpRect);
      //    //  var ptLocal = this.PointToClient(new DPoint(lpRect.X, lpRect.Y));
      //    //  User32.SetWindowPos(lParam, IntPtr.Zero, ptLocal.X, itemForRename.LabelBounds.Y - 2,
      //    //    0, 0, User32.SetWindowPosFlags.IgnoreResize | User32.SetWindowPosFlags.DoNotActivate | User32.SetWindowPosFlags.AsynchronousWindowPosition);
      //    //}
      //  } else if (this.HIWORD(wParam.ToInt32()) == 0x0400) {
      //    //if (this.IconSize != 16) {
      //    //  var itemForRename = this.Items[this._ItemForRename];
      //    //  User32.SetWindowPos(lParam, IntPtr.Zero, itemForRename.LabelBounds.X - 4, itemForRename.LabelBounds.Y - 2,
      //    //    itemForRename.LabelBounds.Width + 8, itemForRename.LabelBounds.Height + 2, User32.SetWindowPosFlags.DoNotActivate);
      //    //}
      //  }
      //}
      if (uMsg == 0x007D) {
        if (wParam == (IntPtr)(-16)) {
          var structStyle = Marshal.PtrToStructure<STYLESTRUCT>(lParam);
          if ((structStyle.styleNew & 0x00200000) != 0 && (structStyle.styleOld & 0x00200000) == 0) {
            this._VScrollWidth = 18;
          }

          //if (((structStyle.styleNew & 0x00200000) != 0 && (structStyle.styleOld & 0x00200000) == 0) || (structStyle.styleNew & 0x00200000) == 0 && (structStyle.styleOld & 0x00200000) != 0) {
          User32.MoveWindow(this.LVHandle, 0, 0, this.ClientRectangle.Width + this._VScrollWidth,
            this.ClientRectangle.Height, false);
          //}
        }
      }
      //var point = lParam.ToPoint();
      //var lvHitTestInfop = new LVHITTESTINFO();
      //lvHitTestInfop.pt.x = point.X;
      //lvHitTestInfop.pt.y = point.Y;
      //if (User32.SendMessage(this.LVHandle, 0x1000 + 57, -1, ref lvHitTestInfop) != -1 && lvHitTestInfop.iItem > -1) {
      //  var lvitemindexChanged = new LVITEMINDEX();
      //  lvitemindexChanged.iGroup = this.GetGroupIndex(lvHitTestInfop.iItem);
      //  lvitemindexChanged.iItem = lvHitTestInfop.iItem;
      //  this._IIListView.GetItemRect(lvitemindexChanged, 0, out var itemRectChanged);
      //  if (!itemRectChanged.ToRectangle(4).Contains(point)) {
      //    this._SkipSelection = true;
      //  } else {
      //    this._SkipSelection = false;
      //  }
      //}
      if (uMsg == 0x0100) {

      }

      if (uMsg == 0x0201) {
        this._IsMouseButtonDown = true;
      }

      if (uMsg == 0x0202) {
        this._IsMouseButtonDown = false;
      }

      if (uMsg is 0x0202 or 0x0201 or 0x0203) {
        //User32.ReleaseCapture();
        var hitPoint = lParam.ToPoint();
        var lvHitTestInfo = new LVHITTESTINFO();
        lvHitTestInfo.pt.x = hitPoint.X;
        lvHitTestInfo.pt.y = hitPoint.Y;
        if (User32.SendMessage(this.LVHandle, 0x1000 + 57, -1, ref lvHitTestInfo) != -1) {
          var rect = new User32.RECT {
            Top = 1
          };
          User32.SendMessage(this.LVHandle, 0x1062, lvHitTestInfo.iGroup, ref rect);
          if (lvHitTestInfo.iItem > -1 && (lvHitTestInfo.flags & ListViewHitTestFlag.LVHT_EX_GROUP) == 0) {
            var lvitemindexChanged = new LVITEMINDEX();
            lvitemindexChanged.iGroup = this.GetGroupIndex(lvHitTestInfo.iItem);
            lvitemindexChanged.iItem = lvHitTestInfo.iItem;
            this._IIListView.GetItemRect(lvitemindexChanged, 0, out var itemRectChanged);
            if (!itemRectChanged.ToRectangle(2).Contains(hitPoint)) {
              this.DeSelectAllItems();
              return IntPtr.Zero;
            }
          }
          var rectExpander = new Rectangle(rect.Right - 30, rect.Y, 30, rect.Height);
          if (rectExpander.Contains(hitPoint) || (uMsg == 0x0203 && (lvHitTestInfo.flags & ListViewHitTestFlag.LVHT_EX_GROUP) != 0)) {
            if (uMsg == 0x0201 || uMsg == 0x0203) {
              var state = User32.SendMessage(this.LVHandle, MSG.LVM_GETGROUPSTATE, (uint)lvHitTestInfo.iGroup, 0x00000001);
              var lvGroup = new LVGROUP2 {
                cbSize = (UInt32)Marshal.SizeOf(typeof(LVGROUP2)),
                iGroupId = lvHitTestInfo.iGroup,
                mask = 0x00000004,
                stateMask = (uint)(state == 1 ? 0x00000000 : 0x00000001),
                state = (uint)(state == 1 ? 0x00000000 : 0x00000001)
              };
              User32.SendMessage(this.LVHandle, 0x1000 + 147, lvHitTestInfo.iGroup, ref lvGroup);
            }

            return IntPtr.Zero;
          }
        } else if (uMsg == 515 && Settings.BESettings.NavigateParentWithDblClickEmpty) {
          this.NavigateParent();
        }

      }

      if (uMsg == (int)WM.WM_NOTIFY) {
        var nmhdrHeader = (NMHEADER)Marshal.PtrToStructure(lParam, typeof(NMHEADER));
        if (nmhdrHeader.hdr.code == -530) {
          if (this._SkipSelection) {
            this._SkipSelection = false;
            return IntPtr.Zero;
          }
        }

        if (nmhdrHeader.hdr.code == (Int32)HDN.HDN_ITEMCHANGINGW) {
          try {
            var hdItem = Marshal.PtrToStructure<HDITEM>(nmhdrHeader.pitem);
            var mask = hdItem.mask;
            var width = hdItem.cxy;
            if (mask == HDITEM.Mask.Width) {
              //(this.Header.Columns[nmhdrHeader.iItem] as ListViewColumnHeader).SetColWidth(width);
            }
          } catch (Exception ex) {

          }
        }
        if (nmhdrHeader.hdr.code == (Int32)HDN.HDN_DROPDOWN)
          this.Column_OnClick(nmhdrHeader.iItem);

        // F.MessageBox.Show(nmhdrHeader.iItem.ToString());
        else if (nmhdrHeader.hdr.code == (Int32)HDN.HDN_BEGINTRACKW) {
          if (this.View != ShellViewStyle.Details) {
            return (IntPtr)1;
          } else {
            this._IsLMouseButtonDown = true;
          }
        } else if (nmhdrHeader.hdr.code == (Int32)HDN.HDN_ENDTRACKW) {
          var column = this.Collumns[nmhdrHeader.iItem];
          column.SetColumnWidth(this);
          //this.UpdateColInView(column, true);
          this._IsLMouseButtonDown = false;
        }

      }

      if (uMsg == (int)WM.WM_ERASEBKGND) {
        return (IntPtr)1;
      }

      if (uMsg == (int)WM.WM_NCPAINT) {
        var currentStyle = User32.GetWindowLong(this.LVHandle, -16);
        if ((currentStyle & 0x00200000) != 0) {
          //this._VScrollWidth = 18;
          try {
            this.Invoke((Action)(() => {
              var scrollInfo = this.GetScrollPosition();
              if (scrollInfo.nMax == 0) {
                this._VScrollWidth = 18;
                User32.MoveWindow(this.LVHandle, 0, 0, this.ClientRectangle.Width + this._VScrollWidth, this.ClientRectangle.Height, false);
                this.ScrollUpdateThreadRun();
                return;
              }
              //this._PreventScrollValueEvent = true;
              this.VScroll.Minimum = scrollInfo.nMin;
              this.VScroll.Maximum = scrollInfo.nMax - scrollInfo.nPage;
              if (this.View == ShellViewStyle.Details && !this.IsGroupsEnabled) {
                this.VScroll.SmallChange = 1;
                this.VScroll.Maximum += 1;
              } else {
                this.VScroll.SmallChange = scrollInfo.nPage / 10D;
              }

              this.VScroll.LargeChange = scrollInfo.nPage;
              //  //this.sbVertical.LargeChange = 120*3;
              //  //var vpsize = this.ShellViewEx.ClientRectangle.Height / 16D;
              //  //if (vpsize)
              var thumbSize = (this.ClientRectangle.Height * (this.ClientRectangle.Height / 20)) /
                              (this.VScroll.Maximum + (this.ClientRectangle.Height / 20));
              if (thumbSize < 50) {
                var vp = (this.VScroll.Maximum * 50) / (this.ClientRectangle.Height - 50);
                this.VScroll.ViewportSize = vp;
              } else {
                this.VScroll.ViewportSize = this.ClientRectangle.Height / 20;
              }
              this._VScrollWidth = 18;
              this.VScroll.Visibility = Visibility.Visible;
              User32.MoveWindow(this.LVHandle, 0, 0, this.ClientRectangle.Width + this._VScrollWidth,
                this.ClientRectangle.Height, false);
            }));
          } catch (Exception ex) {

          }

        } else {
          try {
            this._VScrollWidth = 0;
            this.VScroll.Visibility = Visibility.Collapsed;
          } catch (Exception ex) {

          }
        }

        //if (true) {
        //  //User32.MoveWindow(this.LVHandle, 0, 0, this.ClientRectangle.Width + vScrollbarWidth,
        //  //  this.ClientRectangle.Height, false);
        //  this.Focus(false, true);
        //}
      }

      return User32.CallWindowProc(this._OldWndProc, hWnd, uMsg, wParam, lParam);
    }
    private IntPtr _OriginalFont { get; set; }
    /// <inheritdoc/>
    protected override void OnHandleCreated(EventArgs e) {
      this._NewWndProc = this.LVWndProc;
      this._NewHeaderWndProc = this.LVHeaderWndProc;
      base.OnHandleCreated(e);

      this.Theme = new LVTheme(Settings.BESettings.CurrentTheme == "Dark" ? ThemeColors.Dark : ThemeColors.Light);
      this.BackColor = Color.Black;

      //this._Notifications.RegisterChangeNotify(this.Handle, ShellNotifications.CSIDL.CSIDL_DESKTOP, true);
      this._UnvalidateTimer.Interval = 175;
      this._UnvalidateTimer.Tick += this._UnvalidateTimer_Tick;
      this._UnvalidateTimer.Stop();

      this._FastUnvalidateTimer.Interval = 120;
      this._FastUnvalidateTimer.Tick += this._UnvalidateTimer_Tick;
      this._FastUnvalidateTimer.Stop();

      this._MaintenanceTimer.Interval = 1000 * 60;
      this._MaintenanceTimer.Tick += this._MaintenanceTimer_Tick;
      this._MaintenanceTimer.Start();

      this._SearchTimer.Interval = 1500;
      this._SearchTimer.Enabled = false;
      this._SearchTimer.Tick += (sender, args) => {
        if (this.Items.Count > 0) {
          this._Smre.Reset();
          this.Items = this.Items.OrderBy(o => o.DisplayName).ToList();
          for (var j = 0; j < this.Items.Count; j++) {
            this.Items[j].ItemIndex = j;
          }

          this.BeginInvoke((Action)(() => {
            this._IIListView.SetItemCount(this.Items.Count, 0x2);
            this._Smre.Set();
            this.ScrollUpdateThreadRun();
          }));
        }
      };
      this._SearchTimer.Stop();

      this._NavWaitTimer.Tick += (sender, args) => {
        this.BeginInvoke((Action)(() => {
          this._IsDisplayEmptyText = true;
          this._IIListView.ResetEmptyText();
        }));
      };
      this._NavWaitTimer.Stop();

      var icc = new ComCtl32.INITCOMMONCONTROLSEX() { dwSize = Marshal.SizeOf(typeof(ComCtl32.INITCOMMONCONTROLSEX)), dwICC = 1 };
      var res = ComCtl32.InitCommonControlsEx(ref icc);
      var t = typeof(ShellView);
      var m = t.Module;
      var hInstance = IntPtr.Zero;//  Marshal.GetHINSTANCE(m);

      this.LVHandle = User32.CreateWindowEx(0, "SysListView32", String.Empty,
        User32.WindowStyles.WS_CHILD | User32.WindowStyles.WS_CLIPCHILDREN | User32.WindowStyles.WS_CLIPSIBLINGS | (User32.WindowStyles)User32.LVS_EDITLABELS | (User32.WindowStyles)User32.LVS_OWNERDATA | (User32.WindowStyles)0x00200000 | (User32.WindowStyles)0x0008 |
        //(User32.WindowStyles)User32.LVS_SHOWSELALWAYS | (User32.WindowStyles)User32.LVS_AUTOARRANGE, 0, 0, this.ClientRectangle.Width + SystemInformation.VerticalScrollBarWidth, this.ClientRectangle.Height, this.Handle, IntPtr.Zero, hInstance, IntPtr.Zero);
        (User32.WindowStyles)User32.LVS_SHOWSELALWAYS | (User32.WindowStyles)User32.LVS_AUTOARRANGE | (User32.WindowStyles)User32.LVS_NOCOLUMNHEADER, 0, 0, this.ClientRectangle.Width, this.ClientRectangle.Height, this.Handle, IntPtr.Zero, hInstance, IntPtr.Zero);

      this._OldWndProc = User32.SetWindowLongPtr(this.LVHandle, User32.GWL_WNDPROC, this._NewWndProc);
      var le = Marshal.GetLastWin32Error();
      User32.ShowWindow(this.LVHandle, User32.ShowWindowCommands.Show);
      this._OriginalFont = (IntPtr)User32.SendMessage(this.LVHandle, 0x0031, IntPtr.Zero, IntPtr.Zero);
      //var fnt = new Font("Calibri", 14);

      //User32.SendMessage(this.LVHandle, 0x0030, fnt.ToHfont(), new IntPtr((0 << 16) | (1 & 0xFFFF)));
      //fnt.Dispose();

      this.AddDefaultColumns(true);
      this.LVHeaderHandle = User32.SendMessage(this.LVHandle, MSG.LVM_GETHEADER, 0, 0);
      this.AfterCollumsPopulate?.Invoke(this, new ColumnAddEventArgs(null) { Collumns = this.Collumns });

      for (var i = 0; i < this.Collumns.Count; i++) {
        this.Collumns[i].SetSplitButton(this.LVHeaderHandle, i);
      }



      this.IsViewSelectionAllowed = false;
      this.View = ShellViewStyle.Medium;

      //User32.SendMessage(this.LVHandle, MSG.LVM_SetExtendedStyle, (Int32)ListViewExtendedStyles.HeaderInAllViews, (Int32)ListViewExtendedStyles.HeaderInAllViews);
      User32.SendMessage(this.LVHandle, MSG.LVM_SetExtendedStyle, (Int32)ListViewExtendedStyles.LVS_EX_DOUBLEBUFFER, (Int32)ListViewExtendedStyles.LVS_EX_DOUBLEBUFFER);
      User32.SendMessage(this.LVHandle, MSG.LVM_SetExtendedStyle, (Int32)ListViewExtendedStyles.FullRowSelect, (Int32)ListViewExtendedStyles.FullRowSelect);
      User32.SendMessage(this.LVHandle, MSG.LVM_SetExtendedStyle, (Int32)ListViewExtendedStyles.HeaderDragDrop, (Int32)ListViewExtendedStyles.HeaderDragDrop);
      User32.SendMessage(this.LVHandle, MSG.LVM_SetExtendedStyle, (Int32)ListViewExtendedStyles.LabelTip, (Int32)ListViewExtendedStyles.LabelTip);
      User32.SendMessage(this.LVHandle, MSG.LVM_SetExtendedStyle, (Int32)ListViewExtendedStyles.InfoTip, (Int32)ListViewExtendedStyles.InfoTip);
      User32.SendMessage(this.LVHandle, MSG.LVM_SetExtendedStyle, (Int32)ListViewExtendedStyles.AutosizeColumns, (Int32)ListViewExtendedStyles.AutosizeColumns);


      var iiListViewPrt = IntPtr.Zero;
      var iid = typeof(IListView).GUID;
      User32.SendMessage(this.LVHandle, 0x10BD, ref iid, out iiListViewPrt);
      this._IIListView = (IListView)Marshal.GetTypedObjectForIUnknown(iiListViewPrt, typeof(IListView));

      this._IIListView.SetSelectionFlags(1, 1);
      //var res1 = this._IIListView.EnableAlphaShadow(true);
      this._IIListView.SetTextBackgroundColor((IntPtr)(-1));//ColorTranslator.ToWin32(Color.White));
      this._IIListView.SetBackgroundColor(this.Theme.BackgroundColor.ToDrawingColor().ToWin32Color());
      //this._IIListView.SetBackgroundColor(Color.FromArgb(27, 27, 27).ToWin32Color());
      this._IIListView.SetTextColor(this.Theme.TextColor.ToDrawingColor().ToWin32Color());


      var iiVisualPropertiesPtr = IntPtr.Zero;
      var iidVP = typeof(IVisualProperties).GUID;
      User32.SendMessage(this.LVHandle, 0x10BD, ref iidVP, out iiVisualPropertiesPtr);
      this._IIVisualProperties = (IVisualProperties)Marshal.GetTypedObjectForIUnknown(iiVisualPropertiesPtr, typeof(IVisualProperties));

      this.Focus();

      this._IIVisualProperties.SetColor(VPCOLORFLAGS.VPCF_SORTCOLUMN, this.Theme.SortColumnColor.ToDrawingColor().ToWin32Color());
      User32.SetForegroundWindow(this.LVHandle);
      //UxTheme.SetWindowTheme(this.LVHandle, "StartMenu", 0);
      UxTheme.AllowDarkModeForWindow(this.LVHandle, true);
      UxTheme.SetWindowTheme(this.LVHandle, "Explorer", 0);
      ShellItem.MessageHandle = this.LVHandle;
      //this.IsViewSelectionAllowed = true;
      this._OldHeaderWndProc = User32.SetWindowLongPtr(this.LVHeaderHandle, User32.GWL_WNDPROC, this._NewHeaderWndProc);
      //dele = new User32.WinEventDelegate(WinEventProc);
      //this._winHook = User32.SetWinEventHook(0x8000, 0x8000, IntPtr.Zero, dele, 0, 0, User32.WINEVENT_OUTOFCONTEXT);
    }

    //public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime) {
    //  if (hwnd != this.Handle && hwnd != (IntPtr)0x0000000000041896 && hwnd != (IntPtr)0x00000000034007c6) {
    //    //this.IsOpen = false;
    //    int nRet;
    //      // Pre-allocate 256 characters, since this is the maximum class name length.
    //      StringBuilder ClassName = new StringBuilder(256);
    //      //Get the window class name
    //      nRet = User32.GetClassName(hwnd, ClassName, ClassName.Capacity);
    //      if (nRet != 0) {
    //        if (ClassName.ToString() == "OperationStatusWindow" || ClassName.ToString() == "RateChartOverlayWindow" || ClassName.ToString().Contains("32770")) {
    //          WPFUI.Background.Manager.ApplyDarkMode(hwnd);
    //          //WPFUI.Background.Manager.Apply(WPFUI.Background.BackgroundType.Mica, hwnd);
    //          //var nonClientArea = new RibbonWindow.MARGINS();
    //          //nonClientArea.topHeight = -1;
    //          //nonClientArea.leftWidth = -1;
    //          //nonClientArea.bottomHeight = -1;
    //          //nonClientArea.rightWidth = -1;
    //          //RibbonWindow.DwmExtendFrameIntoClientArea(hwnd, ref nonClientArea);
    //          //var hdc = User32.GetWindowDC(hwnd);
    //          //Gdi32.SetTextColor(hdc, (int)Color.White.ToWin32Color());

    //          UxTheme.SetWindowTheme(hwnd, "DarkMode_Explorer", 0);
    //          //User32.SendMessage(hwnd, 0x1000 + 36, IntPtr.Zero, Color.Wheat.ToWin32Color());
    //          UxTheme.AllowDarkModeForWindow(hwnd, true);

    //        }
    //      }
    //      //if (text == "Progress") {
    //      //  WPFUI.Background.Manager.ApplyDarkMode(hwnd);
    //      //  //WPFUI.Background.Manager.Apply(WPFUI.Background.BackgroundType.Mica, hwnd);
    //      //}
    //  }
    //}

    /// <inheritdoc/>
    protected override void OnHandleDestroyed(EventArgs e) {
      this.KillAllThreads();
      Gdi32.DeleteObject(ShellView._BlackBrush);
      base.OnHandleDestroyed(e);
      //User32.UnhookWinEvent(this._winHook);
    }

    public void KillAllThreads() {
      try {
        this._FsWatcher?.Dispose();
        //this._Notifications.UnregisterChangeNotify();
        this.LargeImageList.Dispose();
        this.SmallImageList.Dispose();
        var t = new Thread(() => {
          this._Mre.Reset();
          foreach (var thread in this._Threads) {
            if (thread.IsAlive) {
              thread.Interrupt();
            }
          }
        }) {
          IsBackground = true
        };
        t.Start();
      } catch (ThreadInterruptedException) { } catch { }
    }

    #endregion Overrides

    #region Public Methods

    public void ChangeTheme(ThemeColors theme) {
      UxTheme.AllowDarkModeForApp(theme == ThemeColors.Dark);
      UxTheme.AllowDarkModeForWindow(this.LVHandle, theme == ThemeColors.Dark);
      UxTheme.SetWindowTheme(this.LVHandle, "Explorer", 0);
      UxTheme.FlushMenuThemes();
      this.Theme = new LVTheme(theme);
      //this.VScroll.Theme = this.Theme;
      this._IIListView.SetBackgroundColor(this.Theme.BackgroundColor.ToDrawingColor().ToWin32Color());
      this._IIListView.SetTextColor(this.Theme.TextColor.ToDrawingColor().ToWin32Color());
      this._IIVisualProperties.SetColor(VPCOLORFLAGS.VPCF_SORTCOLUMN, this.Theme.SortColumnColor.ToDrawingColor().ToWin32Color());

    }

    public IListView GetListViewInterface() {
      return this._IIListView;
    }

    public void RaiseMiddleClickOnItem(IListItemEx item) {
      this.ItemMiddleClick?.Invoke(this, e: new NavigatedEventArgs(item, item));
    }

    /// <summary>
    /// Saves the current <paramref name="destination">destination</paramref> settings to the SQLite database
    /// </summary>
    /// <param name="destination">The destination whos settings you want to save</param>
    public void SaveSettingsToDatabase(IListItemEx destination) {
      if (this.CurrentFolder == null || !this.CurrentFolder.IsFolder) {
        return;
      }

      var mDbConnection = new SQLite.SQLiteConnection("Data Source=" + this._DBPath + ";Version=3;");
      mDbConnection.Open();

      var command1 = new SQLite.SQLiteCommand("SELECT * FROM foldersettings WHERE Path=@Path", mDbConnection);
      command1.Parameters.AddWithValue("Path", destination.ParsingName);
      var reader = command1.ExecuteReader();
      var sql = reader.Read()
        ? @"UPDATE foldersettings
              SET Path = @Path, LastSortOrder = @LastSortOrder, LastGroupOrder = @LastGroupOrder, LastGroupCollumn = @LastGroupCollumn,
                   View = @View, LastSortedColumn = @LastSortedColumn, Columns = @Columns, IconSize = @IconSize
               WHERE Path = @Path"
        : @"INSERT into foldersettings (Path, LastSortOrder, LastGroupOrder, LastGroupCollumn, View, LastSortedColumn, Columns, IconSize)
              VALUES (@Path, @LastSortOrder, @LastGroupOrder, @LastGroupCollumn, @View, @LastSortedColumn, @Columns, @IconSize)";

      var orders = new Int32[this.Collumns.Count];
      User32.SendMessage(this.LVHandle, (UInt32)MSG.LVM_GETCOLUMNORDERARRAY, orders.Length, orders);

      var columnsXml = new XElement("Columns");
      foreach (var index in orders) {
        var collumn = this.Collumns[index];
        var width = (Int32)User32.SendMessage(this.LVHandle, MSG.LVM_GETCOLUMNWIDTH, index, 0);
        var xml = new XElement("Column");
        xml.Add(new XAttribute("ID", collumn.ID == null ? String.Empty : collumn.ID.ToString()));
        xml.Add(new XAttribute("Width", collumn.ID == null ? String.Empty : width.ToString()));
        columnsXml.Add(xml);
      }

      var values = new Dictionary<String, String>() {
        {"Path", destination.ParsingName},
        {"LastSortOrder", this.LastSortOrder.ToString()},
        {"LastGroupOrder", this.LastGroupOrder.ToString()},
        {"LastGroupCollumn", this.LastGroupCollumn?.ID},
        {"View", this.View.ToString()},
        {"LastSortedColumn", this.LastSortedColumnId?.ToString()},
        {"Columns", columnsXml.ToString()},
        {"IconSize", this.IconSize.ToString()}
      };

      var command2 = new SQLite.SQLiteCommand(sql, mDbConnection);
      foreach (var item in values) {
        command2.Parameters.AddWithValue(item.Key, item.Value);
      }

      command2.ExecuteNonQuery();
      reader.Close();
      mDbConnection.Close();
    }

    /// <summary>Resets the current folder's settings by deting it from the SQLIte database</summary>
    public void ResetFolderSettings() {
      var mDbConnection = new SQLite.SQLiteConnection("Data Source=" + this._DBPath + ";Version=3;");
      mDbConnection.Open();
      new SQLite.SQLiteCommand("DELETE FROM foldersettings", mDbConnection).ExecuteNonQuery();
    }

    public static Boolean IsDropDescriptionValid(System.Runtime.InteropServices.ComTypes.IDataObject dataObject) {
      var data = dataObject.GetDropDescription();
      return data is DataObject.DropDescription && (DataObject.DropImageType)((DataObject.DropDescription)data).type != DataObject.DropImageType.Invalid;
    }

    public static IntPtr GetIntPtrFromData(Object data) {
      Byte[] buf = null;

      if (data is MemoryStream stream) {
        buf = new Byte[4];
        if (stream.Read(buf, 0, 4) != 4) {
          throw new ArgumentException("Could not read an IntPtr from the MemoryStream");
        }
      }

      if (data is Byte[] bytes) {
        buf = bytes;
        if (buf.Length < 4) {
          throw new ArgumentException("Could not read an IntPtr from the byte array");
        }
      }

      if (buf == null) {
        throw new ArgumentException("Could not read an IntPtr from the " + data.GetType().ToString());
      }

      var p = (buf[3] << 24) | (buf[2] << 16) | (buf[1] << 8) | buf[0];
      return new IntPtr(p);
    }

    /// <summary>
    /// Inserts a new item into the control If and only If it is new. Returns the item's index OR -1 if already existing
    /// </summary>
    /// <param name="obj">The item you want to insert</param>
    /// <returns>If item is new Then returns <see cref="IListItemEx.ItemIndex">obj.ItemIndex</see> Else returns -1</returns>
    public Int32 InsertNewItem(IListItemEx obj) {
      if (!this._AddedItems.Contains(obj.PIDL) && !String.IsNullOrEmpty(obj.ParsingName) && obj.IsInCurrentFolder(this.CurrentFolder)) {
        this.Items.Add(obj);
        this._AddedItems.Add(obj.PIDL);
        var col = this.AllAvailableColumns.FirstOrDefault(w => w.Value.ID == this.LastSortedColumnId).Value;
        this.SetSortCollumn(true, col, this.LastSortOrder, false);
        //this.SetSortCollumn(true, col, SortOrder.Ascending, false);
        if (this.IsGroupsEnabled) {
          this.SetGroupOrder(false);
        }

        var itemIndex = obj.ItemIndex;
        return itemIndex;
      }

      return -1;
    }

    public void UpdateItem(IListItemEx obj1, IListItemEx obj2) {
      if (!obj2.IsInCurrentFolder(this.CurrentFolder) || obj2.Equals(obj1)) {
        return;
      }

      var currentlySelectedItems = this.SelectedItems.ToArray();
      var items = this.Items.ToArray();
      var oldSelectedItems = this.SelectedItems;
      var oldItem = items.SingleOrDefault(s => s.Equals(obj1) || (obj1.Extension.Equals(".library-ms") && s.ParsingName.Equals(Path.Combine(KnownFolders.Libraries.ParsingName, Path.GetFileName(obj1.ParsingName)))));

      var theItem = items.FirstOrDefault(s => s.ParsingName == obj2.ParsingName ||
                                              (obj2.Extension.Equals(".library-ms") && s.ParsingName.Equals(Path.Combine(KnownFolders.Libraries.ParsingName, Path.GetFileName(obj2.ParsingName)))));

      if (theItem == null) {
        if (oldItem != null) {
          this._AddedItems.Remove(oldItem.PIDL);
          this.Items[oldItem.ItemIndex] = obj2.Extension.Equals(".library-ms") ? FileSystemListItem.InitializeWithIShellItem(this.LVHandle, BExplorer.Shell.ShellLibrary.Load(obj2.DisplayName, true).ComInterface) : FileSystemListItem.ToFileSystemItem(this.LVHandle, obj2.PIDL);

        }

        var col = this.AllAvailableColumns.FirstOrDefault(w => w.Value.ID == this.LastSortedColumnId).Value;
        this.SetSortCollumn(true, col, this.LastSortOrder, false, false);
        if (this.IsGroupsEnabled) {
          this.SetGroupOrder(false);
        }
        var obj2Real2 = this.Items.FirstOrDefault(s => s.ParsingName == obj2.ParsingName || (obj2.Extension.Equals(".library-ms") && s.ParsingName.Equals(Path.Combine(KnownFolders.Libraries.ParsingName, Path.GetFileName(obj2.ParsingName)))));
        if (obj2Real2 != null) {
          if (oldSelectedItems.Contains(oldItem)) {
            oldSelectedItems.Remove(oldItem);
            oldSelectedItems.Add(obj2Real2);
          }

          this.RefreshItem(obj2Real2.ItemIndex);
          this.SelectItems(oldSelectedItems.ToArray());
        }
      } else if (oldItem == null && obj2.Extension == String.Empty) {
        // probably a temporary file
        this._TemporaryFiles.Add(obj2.ParsingName);
      }
      obj1.Dispose();
      obj2.Dispose();
      this.IsFocusAllowed = true;
      this.Focus();
    }

    public void RaiseRecycleBinUpdated() => this.ItemUpdated?.Invoke(this, new ItemUpdatedEventArgs(ItemUpdateType.RecycleBin, null, null, -1));

    public void RaiseItemUpdated(ItemUpdateType type, IListItemEx? old, IListItemEx? newItem, Int32 index) {
      this.ItemUpdated?.Invoke(this, new ItemUpdatedEventArgs(type, newItem, old, index));
    }

    public static Boolean IsShowingLayered(F.DataObject dataObject) {
      if (!dataObject.GetDataPresent("IsShowingLayered")) return false;
      var data = dataObject.GetData("IsShowingLayered");
      if (data != null) {
        return data is Stream stream && new BinaryReader(stream).ReadBoolean();
      }

      return false;
    }

    /// <summary>If the <see cref="GetFirstSelectedItem">Current</see> item <see cref="IListItemEx.IsFolder">IsFolder</see> Then navigate to it Else open item</summary>
    public void OpenOrNavigateItem() {
      var selectedItem = this.GetFirstSelectedItem();
      if (selectedItem.IsFolder) {
        this.Navigate_Full(selectedItem, true, true, false);
      } else {
        Process.Start(selectedItem.ParsingName);
      }
    }

    public Int32 GetGroupIndex(Int32 itemIndex) => this.IsGroupsEnabled ? itemIndex == -1 || itemIndex >= this.Items.Count ? -1 : this.Items[itemIndex].GroupIndex : -1;

    public void OpenShareUI() {
      //Shell32.ShowShareFolderUI(this.Handle,
      //  Marshal.StringToHGlobalAuto(this.GetFirstSelectedItem().ParsingName.Replace(@"\\", @"\")));
      IntPtr hwnd = new WindowInteropHelper(System.Windows.Application.Current.MainWindow!).Handle;
      var dtm = DataTransferManagerHelper.GetForWindow(hwnd);
      dtm.DataRequested += OnDataRequested;
      DataTransferManagerHelper.ShowShareUIForWindow(hwnd);
    }

    async void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args) {
      DataRequestDeferral dataRequestDeferral = args.Request.GetDeferral();
      List<IStorageItem> items = new List<IStorageItem>();
      DataRequest dataRequest = args.Request;

      /*dataRequest.Data.Properties.Title = "Data Shared From Files";
      dataRequest.Data.Properties.Description = "The items you selected will be shared";*/
      foreach (var selectedItem in this.SelectedItems) {
        if (!selectedItem.IsFolder) {
          var file = await StorageFile.GetFileFromPathAsync(selectedItem.ParsingName);
          items.Add(file);
        }
      }

      //foreach (ListedItem item in SlimContentPage.SelectedItems) {
      //  if (item is ShortcutItem shItem) {
      //    if (shItem.IsLinkItem && !string.IsNullOrEmpty(shItem.TargetPath)) {
      //      dataRequest.Data.Properties.Title = string.Format("ShareDialogTitle".GetLocalized(), item.ItemName);
      //      dataRequest.Data.Properties.Description = "ShareDialogSingleItemDescription".GetLocalized();
      //      dataRequest.Data.SetWebLink(new Uri(shItem.TargetPath));
      //      dataRequestDeferral.Complete();
      //      return;
      //    }
      //  } else if (item.PrimaryItemAttribute == StorageItemTypes.Folder && !item.IsZipItem) {
      //    if (await StorageItemHelpers.ToStorageItem<BaseStorageFolder>(item.ItemPath, associatedInstance) is BaseStorageFolder folder) {
      //      items.Add(folder);
      //    }
      //  } else {
      //    if (await StorageItemHelpers.ToStorageItem<BaseStorageFile>(item.ItemPath, associatedInstance) is BaseStorageFile file) {
      //      items.Add(file);
      //    }
      //  }
      //}

      if (items.Count == 1) {
        dataRequest.Data.Properties.Title = string.Format("ShareDialogTitle", items.First().Name);
        dataRequest.Data.Properties.Description = "ShareDialogSingleItemDescription";
      } else if (items.Count == 0) {
        dataRequest.FailWithDisplayText("ShareDialogFailMessage");
        dataRequestDeferral.Complete();
        return;
      } else {
        dataRequest.Data.Properties.Title = string.Format("ShareDialogTitleMultipleItems", items.Count,
            "ItemsCount.Text");
        dataRequest.Data.Properties.Description = "ShareDialogMultipleItemsDescription";
      }

      dataRequest.Data.SetStorageItems(items, false);
      dataRequestDeferral.Complete();
    }
    //}

    public void ShowPropPage(IntPtr hwnd, String filename, String proppage) => Shell32.SHObjectProperties(hwnd, 0x2, filename, proppage);

    public void ShowPropertiesPage() => Shell32.SHMultiFileProperties(this.SelectedItems.ToArray().GetIDataObject(out var dataObjPtr), 0);

    public void UpdateItem(Int32 index) => this.BeginInvoke(new MethodInvoker(() => this._IIListView.UpdateItem(index)));

    /// <summary> Navigates to the parent of the currently displayed folder. </summary>
    public void NavigateParent() {
      if (this.CurrentFolder != null) {
        this.Navigate_Full(this.CurrentFolder.Parent, true, true);
      }
    }

    /// <summary>Refreshes the contact (by navigating to the current folder If and only If the current folder is not null)</summary>
    public void RefreshContents() {
      if (this.CurrentFolder != null) {
        this.Navigate_Full(this.CurrentFolder, true, refresh: true);
      }
    }

    /// <summary>
    /// Refreshes a single item
    /// </summary>
    /// <param name="index">The index of the item you want to refresh</param>
    /// <param name="isForceRedraw">If <c>True</c> Resets everything in the Item to indicate that it needs to be refreshed/reloaded</param>
    public void RefreshItem(Int32 index, IntPtr? enumPidl = null) {
      if (this.CurrentlyUpdatingItems.ContainsKey(index)) {
        return;
      }

      this.CurrentlyUpdatingItems.TryAdd(index, 0);
      //if (isForceRedraw) {
      //  try {
      //    this._ResetEvent.Set();
      //    var newItem = FileSystemListItem.ToFileSystemItem(this.LVHandle, this.Items[index].ParsingName.ToShellParsingName());
      //    newItem.GroupIndex = this.Items[index].GroupIndex;
      //    newItem.ItemIndex = index;
      //    newItem.ColumnValues = this.Items[index].ColumnValues;
      //    this.Items[index] = newItem;
      //    this.Items[index].IsNeedRefreshing = true;
      //    this.Items[index].IsInvalid = true;
      //    //this.Items[index].IsShared = newItem.IsShared;

      //    // this.Items[index].OverlayIconIndex = -1;
      //    this.SmallImageList.EnqueueOverlay(index);
      //    this.Items[index].IsOnlyLowQuality = false;
      //    this.Items[index].IsIconLoaded = false;
      //    //newItem.Dispose();
      //  } catch (FileNotFoundException) {
      //    this._ResetEvent.Set();

      //    // In case the event late and the file is not there anymore or changed catch the exception
      //    var newItem = FileSystemListItem.ToFileSystemItem(this.LVHandle, this.Items[index].PIDL);
      //    newItem.GroupIndex = this.Items[index].GroupIndex;
      //    newItem.ItemIndex = index;
      //    newItem.ColumnValues = this.Items[index].ColumnValues;
      //    this.Items[index] = newItem;
      //    this.Items[index].IsNeedRefreshing = true;
      //    this.Items[index].IsInvalid = true;

      //    // this.Items[index].OverlayIconIndex = -1;
      //    this.SmallImageList.EnqueueOverlay(index);
      //    this.Items[index].IsOnlyLowQuality = false;
      //    this.Items[index].IsIconLoaded = false;
      //    //newItem.Dispose();
      //    //this.Items[index].IsShared = newItem.IsShared;
      //  } catch {
      //  }
      //}
      if (enumPidl != null) {
        this.Items[index].EnumPIDL = enumPidl.Value;
      }

      this.BeginInvoke(new MethodInvoker(() => {
        //this._IIListView.UpdateItem(index);
        this._IIListView.RedrawItems(index, index);
      }));
    }

    /// <summary>Renames the first selected item</summary>
    public void RenameSelectedItem() => this.RenameItem(this.GetFirstSelectedItemIndex());

    /// <summary>
    /// Renames the item at the specified index
    /// </summary>
    /// <param name="index">The index of the item you want to rename</param>
    public void RenameSelectedItem(Int32 index) => this.RenameItem(index);

    public void CutSelectedFiles() {
      foreach (var index in this._SelectedIndexes) {
        var item = new LVITEM() { mask = LVIF.LVIF_STATE, stateMask = LVIS.LVIS_CUT, state = LVIS.LVIS_CUT };
        User32.SendMessage(this.LVHandle, MSG.LVM_SETITEMSTATE, index, ref item);
      }

      this._CuttedIndexes.AddRange(this._SelectedIndexes.ToArray());
      var ddataObject = new F.DataObject();

      // Copy or Cut operation (5 = copy; 2 = cut)
      ddataObject.SetData("Preferred DropEffect", true, new MemoryStream(new Byte[] { 2, 0, 0, 0 }));
      ddataObject.SetData("Shell IDList Array", true, this.SelectedItems.ToArray().CreateShellIDList());
      F.Clipboard.SetDataObject(ddataObject, true);
    }

    public void CopySelectedFiles() {
      var ddataObject = new F.DataObject();

      // Copy or Cut operation (5 = copy; 2 = cut)
      ddataObject.SetData("Preferred DropEffect", true, new MemoryStream(new Byte[] { 5, 0, 0, 0 }));
      ddataObject.SetData("Shell IDList Array", true, this.SelectedItems.ToArray().CreateShellIDList());
      F.Clipboard.SetDataObject(ddataObject, true);
    }

    public void PasteAvailableFiles() {
      var handle = this.Handle;
      var view = this;
      var dlg = new FileOperation(this);
      try {
        var thread = new Thread(() => {
          var dataObject = F.Clipboard.GetDataObject();
          var dropEffect = dataObject.GetDropEffect();
          if (dataObject != null && dataObject.GetDataPresent("Shell IDList Array")) {
            var shellItemArray = dataObject.ToShellItemArray();
            var items = shellItemArray.ToArray();

            try {
              var controlItem = FileSystemListItem.InitializeWithIShellItem(this.LVHandle, items.First()).Parent;
              var fo = new IIFileOperation(dlg, handle, false, controlItem.Equals(this.CurrentFolder), controlItem, dlg);
              if (dropEffect == System.Windows.DragDropEffects.Copy) {
                fo.CopyItems(shellItemArray, this.CurrentFolder);
              } else {
                fo.MoveItems(shellItemArray, this.CurrentFolder);
              }

              fo.PerformOperations();
              Marshal.ReleaseComObject(shellItemArray);
            } catch { }
          } else if (dataObject != null && dataObject.GetDataPresent("FileDrop")) {
            var items = ((String[])dataObject.GetData("FileDrop")).Select(s => ShellItem.ToShellParsingName(s).ComInterface).ToArray();
            try {

              var controlItem = FileSystemListItem.InitializeWithIShellItem(this.LVHandle, items.First()).Parent;
              var fo = new IIFileOperation(dlg, handle, true, controlItem.Equals(this.CurrentFolder), controlItem, dlg);
              foreach (var item in items) {
                if (dropEffect == System.Windows.DragDropEffects.Copy) {
                  fo.CopyItem(item, this.CurrentFolder);
                } else {
                  fo.MoveItem(item, this.CurrentFolder, null);
                }
              }

              fo.PerformOperations();
            } catch { }
          } else {
            return;
          }
          this.LargeImageList.SupressThumbnailGeneration(false);
        });
        dlg.CurrentThread = thread;
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
      } catch (ThreadInterruptedException ex) {

      }
      //Shell32.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
      //GC.WaitForFullGCComplete(1000);
      //GC.Collect();
    }

    public void DoCopy(IListItemEx destination) => this.Do_Copy_OR_Move_Helper(true, destination, this.SelectedItems.Select(s => s.ComInterface).ToArray());

    public void DoCopy(System.Windows.IDataObject dataObject, IListItemEx destination) => this.Do_Copy_OR_Move_Helper(true, destination, dataObject.ToShellItemArray().ToArray());

    public void DoCopy(F.IDataObject dataObject, IListItemEx destination) => this.Do_Copy_OR_Move_Helper_2(true, destination, dataObject);

    public void DoMove(System.Windows.IDataObject dataObject, IListItemEx destination) => this.Do_Copy_OR_Move_Helper(false, destination, dataObject.ToShellItemArray().ToArray());

    public void DoMove(IListItemEx destination) => this.Do_Copy_OR_Move_Helper(false, destination, this.SelectedItems.Select(s => s.ComInterface).ToArray());

    public void DoMove(F.IDataObject dataObject, IListItemEx destination) => this.Do_Copy_OR_Move_Helper_2(false, destination, dataObject);

    public void DeleteSelectedFiles(Boolean isRecycling) {
      var handle = this.Handle;
      var view = this;
      var dlg = new FileOperation(view);
      if (!isRecycling) {
        if (DeleteNotificationDialog.ShowNotificationDialog(this.SelectedItems.ToArray()) == false) {
          return;
        }
      }
      var thread = new Thread(() => {
        var fo = new IIFileOperation(dlg, handle, isRecycling, dlg);
        foreach (var item in this.SelectedItems) {
          fo.DeleteItem(item);
          this.BeginInvoke(new MethodInvoker(() => { this._IIListView.SetItemState(item.ItemIndex, LVIF.LVIF_STATE, LVIS.LVIS_SELECTED, 0); }));
        }

        fo.PerformOperations();
        fo.Dispose();
        if (isRecycling) {
          this.RaiseRecycleBinUpdated();
        }
      });
      dlg.CurrentThread = thread;
      thread.SetApartmentState(ApartmentState.STA);
      //thread.IsBackground = true;
      thread.Start();
    }

    public void RenameShellItem(IShellItem item, String newName, Boolean isAddFileExtension, String extension = "") {
      var handle = this.Handle;
      var sink = new FileOperation(this);
      var fo = new IIFileOperation(sink, handle, false);
      fo.RenameItem(item, isAddFileExtension ? newName + extension : newName);
      fo.PerformOperations();
      if (fo.GetAnyOperationAborted()) {
        this._IsCanceledOperation = true;
      }
    }

    /// <summary>
    /// Resizes the icons
    /// </summary>
    /// <param name="value">The icon size you want</param>
    public void ResizeIcons(Int32 value) {
      try {
        this.IconSize = value;
        foreach (var obj in this.Items.ToArray()) {
          obj.IsIconLoaded = false;
          obj.IsNeedRefreshing = true;
        }

        this.LargeImageList.ResizeImages(value);
        this.LargeImageList.AttachToListView(this, 0);
        this.SmallImageList.AttachToListView(this, 1);
        User32.SendMessage(this.LVHandle, MSG.LVM_SETICONSPACING, 0, new IntPtr(((value + 16 + 42) << 16) | ((value + 10 + 30) & 0xFFFF)));
      } catch (Exception) { }
    }

    /// <summary>Selects all items and sets this to focus</summary>
    public void SelectAll() {
      this._IIListView.SetItemState(-1, LVIF.LVIF_STATE, LVIS.LVIS_SELECTED, LVIS.LVIS_SELECTED);
      this.Focus();
    }
    /// <summary>
    /// Selects only the specified items. First runs <see cref="DeSelectAllItems">DeSelectAllItems</see> Then selects all items on a separate thread.
    /// </summary>
    /// <param name="shellObjectArray"></param>
    /// <param name="isEnsureVisible"></param>
    public void SelectItems(IListItemEx[] shellObjectArray, Boolean isEnsureVisible = false) {
      //User32.LockWindowUpdate(this.LVHandle);
      this.Invoke((Action)(() => { this._IIListView.SetItemState(-1, LVIF.LVIF_STATE, LVIS.LVIS_SELECTED, 0); }));
      //var selectionThread = new Thread(() => {
      var shellObjectArrayFiltered = shellObjectArray.Where(w => w != null && w.ParsingName != ShellItem.Desktop.ParsingName);
      var lastItem = -2;
      foreach (var item in shellObjectArrayFiltered) {
        try {
          if (item.Extension == ".search-ms") {
            continue;
          }
          var exestingItem = this.Items.ToArray().FirstOrDefault(s => s.Equals(item));
          if (exestingItem != null) {
            lastItem = exestingItem.ItemIndex;
          }
          var itemIndex = exestingItem?.ItemIndex ?? -1;
          if (itemIndex > -1) {
            var lvii = new LVITEMINDEX() { iItem = itemIndex, iGroup = this.GetGroupIndex(itemIndex) };
            var lvi = new LVITEM() { mask = LVIF.LVIF_STATE, stateMask = LVIS.LVIS_SELECTED | LVIS.LVIS_FOCUSED, state = LVIS.LVIS_SELECTED | LVIS.LVIS_FOCUSED };
            User32.SendMessage(this.LVHandle, MSG.LVM_SETITEMINDEXSTATE, ref lvii, ref lvi);
          }

          item.Dispose();
        } catch (Exception) {
          // catch the given key was not found. It happen on fast delete of items
        }
      }
      if (isEnsureVisible && lastItem > -1) {
        var lvii = new LVITEMINDEX() { iItem = lastItem, iGroup = this.GetGroupIndex(lastItem) };
        this.BeginInvoke((Action)(() => {
          this._IIListView.EnsureItemVisible(lvii, true);
          this.ScrollUpdateThreadRun(true);
        }));
      }
      this.BeginInvoke((Action)(() => {
        //User32.LockWindowUpdate(IntPtr.Zero);
      }));
      this.Focus();
      //});

      //selectionThread.SetApartmentState(ApartmentState.STA);
      //selectionThread.Start();
    }

    public void SelectItems(IEnumerable<Int32> shellObjectArray, Boolean isEnsureVisible = false) {
      //User32.LockWindowUpdate(this.LVHandle);
      this.Invoke((Action)(() => { this._IIListView.SetItemState(-1, LVIF.LVIF_STATE, LVIS.LVIS_SELECTED, 0); }));
      //var selectionThread = new Thread(() => {
      var shellObjectArrayFiltered = shellObjectArray.Where(w => w != -1);
      var lastItem = -2;
      foreach (var item in shellObjectArrayFiltered) {
        try {

          var lvii = new LVITEMINDEX() { iItem = item, iGroup = this.GetGroupIndex(item) };
          var lvi = new LVITEM() { mask = LVIF.LVIF_STATE, stateMask = LVIS.LVIS_SELECTED | LVIS.LVIS_FOCUSED, state = LVIS.LVIS_SELECTED | LVIS.LVIS_FOCUSED };
          User32.SendMessage(this.LVHandle, MSG.LVM_SETITEMINDEXSTATE, ref lvii, ref lvi);

        } catch (Exception) {
          // catch the given key was not found. It happen on fast delete of items
        }
      }
      if (isEnsureVisible && lastItem > -1) {
        var lvii = new LVITEMINDEX() { iItem = lastItem, iGroup = this.GetGroupIndex(lastItem) };
        this.BeginInvoke((Action)(() => {
          this._IIListView.EnsureItemVisible(lvii, true);
          this.ScrollUpdateThreadRun(true);
        }));
      }
      this.BeginInvoke((Action)(() => {
        //User32.LockWindowUpdate(IntPtr.Zero);
      }));
      this.Focus();
      //});

      //selectionThread.SetApartmentState(ApartmentState.STA);
      //selectionThread.Start();
    }

    /// <summary>
    /// Set this to focus then select an item by its index
    /// </summary>
    /// <param name="index">Index of item</param>
    /// <param name="ensureVisability">Ensure that the item is visible?</param>
    /// <param name="deselectOthers">Deselect all other items?</param>
    public void SelectItemByIndex(Int32 index, Boolean ensureVisability = false, Boolean deselectOthers = false) {
      this.Focus();
      //User32.LockWindowUpdate(this.LVHandle);
      if (deselectOthers) {
        this.Invoke((Action)(() => { this._IIListView.SetItemState(-1, LVIF.LVIF_STATE, LVIS.LVIS_SELECTED, 0); }));
      }

      var lvii = this.ToLvItemIndex(index);
      var lvi = new LVITEM() { mask = LVIF.LVIF_STATE, stateMask = LVIS.LVIS_SELECTED | LVIS.LVIS_FOCUSED, state = LVIS.LVIS_SELECTED | LVIS.LVIS_FOCUSED };

      if (ensureVisability) {
        this.Invoke((Action)(() => {
          this._IIListView.EnsureItemVisible(lvii, true);
          User32.SendMessage(this.LVHandle, MSG.LVM_SETITEMINDEXSTATE, ref lvii, ref lvi);
          //User32.LockWindowUpdate(IntPtr.Zero);
        }));
      } else {
        User32.SendMessage(this.LVHandle, MSG.LVM_SETITEMINDEXSTATE, ref lvii, ref lvi);
      }
    }
    public void SelectAndRenameItemByIndex(Int32 index, Boolean ensureVisability = false, Boolean deselectOthers = false) {
      this.Focus();
      //User32.LockWindowUpdate(this.LVHandle);
      if (deselectOthers) {
        this.BeginInvoke((Action)(() => { this._IIListView.SetItemState(-1, LVIF.LVIF_STATE, LVIS.LVIS_SELECTED, 0); }));
      }

      var lvii = this.ToLvItemIndex(index);
      var lvi = new LVITEM() { mask = LVIF.LVIF_STATE, stateMask = LVIS.LVIS_SELECTED | LVIS.LVIS_FOCUSED, state = LVIS.LVIS_SELECTED | LVIS.LVIS_FOCUSED };


      if (ensureVisability) {
        this.BeginInvoke((Action)(() => {
          User32.SendMessage(this.LVHandle, MSG.LVM_SETITEMINDEXSTATE, ref lvii, ref lvi);
          this._IIListView.EnsureItemVisible(lvii, true);
          //this.Focus(false, true);
          this.RenameItem(index);
          //this.IsRenameNeeded = false;
          //User32.LockWindowUpdate(IntPtr.Zero);
        }));
      } else {
        User32.SendMessage(this.LVHandle, MSG.LVM_SETITEMINDEXSTATE, ref lvii, ref lvi);
        //this.Focus(false, true);
        this.RenameItem(index);
        //this.Focus(false, true);
        //this.IsRenameNeeded = false;
      }
    }
    public void EnsureVisibility(Int32 index) {
      this.Focus();
      var lvii = this.ToLvItemIndex(index);
      var lvi = new LVITEM() { mask = LVIF.LVIF_STATE, stateMask = LVIS.LVIS_SELECTED | LVIS.LVIS_FOCUSED, state = LVIS.LVIS_SELECTED | LVIS.LVIS_FOCUSED };

      this.Invoke((Action)(() => {
        this._IIListView.EnsureItemVisible(lvii, true);
      }));

    }

    public void SetColInView(Collumns col, Boolean remove) {
      if (remove) {
        var theColumn = this.Collumns.FirstOrDefault(s => s.pkey.fmtid == col.pkey.fmtid && s.pkey.pid == col.pkey.pid);
        if (theColumn != null) {
          var colIndex = this.Collumns.IndexOf(theColumn);
          this.Collumns.Remove(theColumn);
          User32.SendMessage(this.LVHandle, MSG.LVM_DELETECOLUMN, colIndex, 0);
          if (theColumn.ID == this.LastSortedColumnId) {
            User32.SendMessage(this.LVHandle, MSG.LVM_SETSELECTEDCOLUMN, -1, 0);
          }
        }
      } else if (!this.Collumns.Any(s => s.pkey.fmtid == col.pkey.fmtid && s.pkey.pid == col.pkey.pid)) {
        this.Collumns.Add(col);
        var column = col.ToNativeColumn(this.View == ShellViewStyle.Details);
        var colIndex = this.Collumns.Count - 1;
        User32.SendMessage(this.LVHandle, MSG.LVM_INSERTCOLUMN, colIndex, ref column);
        if (col.ID == this.LastSortedColumnId) {
          this.SetSortIcon(colIndex, this.LastSortOrder);
          User32.SendMessage(this.LVHandle, MSG.LVM_SETSELECTEDCOLUMN, colIndex, 0);
        }

        if (this.View != ShellViewStyle.Details) {
          this.AutosizeColumn(this.Collumns.Count - 1, -2);
        }
      }

      var headerhandle = User32.SendMessage(this.LVHandle, MSG.LVM_GETHEADER, 0, 0);
      for (var i = 0; i < this.Collumns.Count; i++) {
        this.Collumns[i].SetSplitButton(headerhandle, i);
      }

      this.OnListViewCollumnsChanged?.Invoke(this, new CollumnsChangedArgs(remove));
    }

    public void RemoveAllCollumns(Boolean isReorder = false) {
      for (var i = this.Collumns.ToArray().Count() - 1; i > 0; i--) {
        this.Collumns.RemoveAt(i);
        if (!isReorder) {
          this.Header.Columns.RemoveAt(i);
        }

        User32.SendMessage(this.LVHandle, MSG.LVM_DELETECOLUMN, i, 0);
      }
      //this.Header.Columns.Clear();
      //this._IIListView.GetColumnWidth(0, out var width);
      //this.Header.Columns[0].Width = width;
      this.AfterCollumsPopulate?.Invoke(this, new ColumnAddEventArgs(null));
    }

    public void SetSortCollumn(Boolean isReorder, Collumns column, SortOrder order, Boolean reverseOrder = true, Boolean isReselect = true) {
      if (column == null) {
        return;
      }

      try {
        var itemsArray = this.Items;
        if (column.ID == this.LastSortedColumnId && reverseOrder) {
          // Reverse the current sort direction for this column.
          this.LastSortOrder = this.LastSortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
        } else {
          // Set the column number that is to be sorted; default to ascending.
          this.LastSortedColumnId = column.ID;
          this.LastSortOrder = order;
        }

        if (isReorder) {
          var itemsQuery = itemsArray.Where(w => this.ShowHidden || !w.IsHidden).OrderByDescending(o => o.IsFolder);
          if (column.CollumnType != typeof(String)) {
            if (order == SortOrder.Ascending) {
              this.Items = itemsQuery.ThenBy(o => o.GetPropertyValue(column.pkey, typeof(String)).Value ?? "1").ToList();
            } else {
              this.Items = itemsQuery.ThenByDescending(o => o.GetPropertyValue(column.pkey, typeof(String)).Value ?? "1").ToList();
            }
          } else {
            if (order == SortOrder.Ascending) {
              this.Items = itemsQuery.ThenBy(o => o.GetPropertyValue(column.pkey, typeof(String)).Value == null ? "1" : o.GetPropertyValue(column.pkey, typeof(String)).Value.ToString(), NaturalStringComparer.Default)
                .ToList();
            } else {
              this.Items = itemsQuery.ThenByDescending(o => o.GetPropertyValue(column.pkey, typeof(String)).Value == null ? "1" : o.GetPropertyValue(column.pkey, typeof(String)).Value.ToString(),
                NaturalStringComparer.Default).ToList();
            }
          }

          var i = 0;
          this.Items.ForEach(e => e.ItemIndex = i++);
        }
        var selectedItems = this.SelectedItems.Select(s => s.ItemIndex).ToArray();
        this.BeginInvoke((Action)(() => { this._IIListView.SetItemCount(this.Items.Count, 0x2); }));

        var colIndexReal = this.Collumns.IndexOf(this.Collumns.FirstOrDefault(w => w.ID == this.LastSortedColumnId));
        if (colIndexReal > -1) {
          User32.SendMessage(this.LVHandle, MSG.LVM_SETSELECTEDCOLUMN, colIndexReal, 0);
          this.Header.Columns.OfType<ListViewColumnHeader>().ToList().ForEach(fe => {
            fe.UpdateIsSelected();
          });
          this.SetSortIcon(colIndexReal, order);
        } else {
          User32.SendMessage(this.LVHandle, MSG.LVM_SETSELECTEDCOLUMN, -1, 0);
        }

        if (!this.IsRenameInProgress && isReselect) {
          this.SelectItems(selectedItems);
        }
        this.ScrollUpdateThreadRun();
      } catch {
      }
    }

    /// <summary>
    /// Navigate to a folder, set it as the current folder and optionally save the folder's settings to the database.
    /// </summary>
    /// <param name="destination">The folder you want to navigate to.</param>
    /// <param name="saveFolderSettings">Should the folder's settings be saved?</param>
    /// <param name="isInSameTab"></param>
    /// <param name="refresh">Should the List be Refreshed?</param>
    public void Navigate_Full(IListItemEx destination, Boolean saveFolderSettings, Boolean isInSameTab = false, Boolean refresh = false) {
      var handle = this.Handle;
      this.IsSearchNavigating = false;

      if (destination == null || !destination.IsFolder) {
        return;
      }

      this._ResetEvent.Set();
      this.Navigate(destination, isInSameTab, refresh, this.IsNavigationInProgress);
    }

    /// <summary>
    /// Navigates to a search folder
    /// </summary>
    /// <param name="searchQuery">The query of the search</param>
    /// <param name="saveFolderSettings">Should the folder's settings be saved?</param>
    /// <param name="isInSameTab"></param>
    /// <param name="refresh">Should the List be Refreshed?</param>
    public void Navigate_Full(String searchQuery, Boolean saveFolderSettings, Boolean isInSameTab = false, Boolean refresh = false) {
      this.IsSearchNavigating = true;
      if (saveFolderSettings) {
        this.SaveSettingsToDatabase(this.CurrentFolder);
      }

      this._ResetEvent.Set();
      var searchCondition = SearchConditionFactory.ParseStructuredQuery(this.PrepareSearchQuery(searchQuery));
      var shellItem = new ShellItem(this.CurrentFolder.PIDL);
      var searchFolder = new ShellSearchFolder(searchCondition, shellItem);
      IListItemEx searchItem = FileSystemListItem.ToFileSystemItem(this.LVHandle, searchFolder);
      this.NavigateSearch(searchItem, isInSameTab, refresh, this.IsNavigationInProgress);
    }

    /// <summary>Invalidates the director</summary>
    /// <remarks>Starts restarts <see cref="_UnvalidateTimer"/></remarks>
    public void UnvalidateDirectory(Boolean isFastUnvalidate = true) {
      Action worker = () => {
        if (isFastUnvalidate) {
          this._UnvalidateTimer.Stop();
          if (this._FastUnvalidateTimer.Enabled) {
            this._FastUnvalidateTimer.Stop();
          }

          this._FastUnvalidateTimer.Start();
        } else {
          this._FastUnvalidateTimer.Stop();
          if (this._UnvalidateTimer.Enabled) {
            this._UnvalidateTimer.Stop();
          }

          this._UnvalidateTimer.Start();
        }

      };

      //if (this.InvokeRequired) {
      this.BeginInvoke((Action)(() => worker()));
      //} else {
      //  worker();
      //}
    }

    /// <summary>Cancels navigation</summary>
    public void CancelNavigation() {
      this._SearchTimer.Stop();
      this.IsCancelRequested = true;
      if (this._Threads.Any()) {
        this._Mre.Set();
        this._ResetEvent.Set();
        foreach (var thread in this._Threads.ToArray()) {
          thread.Interrupt();
          this._Threads.Remove(thread);
        }
      }
    }

    /// <summary>Disables/Removes grouping</summary>
    public void DisableGroups() {
      if (!this.IsGroupsEnabled) {
        return;
      }

      this.Groups.Clear();
      this._IIListView.RemoveAllGroups();
      this._IIListView.EnableGroupView(0);
      this._IIListView.SetOwnerDataCallback(IntPtr.Zero);
      this.LastGroupCollumn = null;
      this.LastGroupOrder = SortOrder.None;
      this.ScrollUpdateThreadRun();
      this.IsGroupsEnabled = false;
    }

    private Boolean _IsScrollUpdateInProgress = false;
    private void ScrollUpdateThreadRun(Boolean onlyValue = false, Boolean withThread = true) {
      void ProcessScroolUpdate() {
        Application.Current.Dispatcher.Invoke(DispatcherPriority.Send, (Action)(() => {
          var scrollInfo = this.GetScrollPosition();
          //this._PreventScrollValueEvent = true;
          if (scrollInfo.nMax == 0) {
            return;
          }
          if (!onlyValue) {
            this.VScroll.Minimum = scrollInfo.nMin;
            this.VScroll.Maximum = scrollInfo.nMax - scrollInfo.nPage;
            if (this.View == ShellViewStyle.Details && !this.IsGroupsEnabled) {
              this.VScroll.SmallChange = 1;
              this.VScroll.Maximum += 1;
            } else {
              this.VScroll.SmallChange = scrollInfo.nPage / 10D;
            }

            this.VScroll.LargeChange = scrollInfo.nPage;
            //  //this.sbVertical.LargeChange = 120*3;
            //  //var vpsize = this.ShellViewEx.ClientRectangle.Height / 16D;
            //  //if (vpsize)
            var thumbSize = (this.ClientRectangle.Height * (this.ClientRectangle.Height / 20)) /
                            (this.VScroll.Maximum + (this.ClientRectangle.Height / 20));
            if (thumbSize < 50) {
              var vp = (this.VScroll.Maximum * 50) / (this.ClientRectangle.Height - 50);
              this.VScroll.ViewportSize = vp;
            } else {
              this.VScroll.ViewportSize = this.ClientRectangle.Height / 20;
            }

            if (scrollInfo.nMax > 0) {
              this._VScrollWidth = 18;
              this.VScroll.Visibility = Visibility.Visible;
            } else {
              this._VScrollWidth = 0;
              this.VScroll.Visibility = Visibility.Collapsed;
            }
          }

          this._PreventScrollValueEvent = true;
          this.VScroll.Value = scrollInfo.nPos;
          this._PreventScrollValueEvent = false;
        }));

        this._IsScrollUpdateInProgress = false;
      }

      if (!this._IsScrollUpdateInProgress) {
        this._IsScrollUpdateInProgress = true;
        if (withThread) {
          var scrollUpdateThread = new Thread(() => {
            var loopCondition = true;
            Thread.Sleep(50);
            //while (loopCondition) {
            ProcessScroolUpdate();
            //}
          });
          scrollUpdateThread.SetApartmentState(ApartmentState.STA);
          scrollUpdateThread.Priority = ThreadPriority.Normal;
          scrollUpdateThread.Start();
        } else {
          ProcessScroolUpdate();
        }
      }
    }

    /// <summary>Enables/Adds groupings</summary>
    public void EnableGroups() {
      if (this.IsGroupsEnabled || this.IsSearchNavigating) {
        return;
      }

      var ptr = Marshal.GetComInterfaceForObject(new VirtualGrouping(this), typeof(IOwnerDataCallback));
      this._IIListView.SetOwnerDataCallback(ptr);
      Marshal.Release(ptr);
      this._IIListView.EnableGroupView(1);
      this.IsGroupsEnabled = true;
    }

    /// <summary>
    /// Generates Groups
    /// </summary>
    /// <param name="col">The column you want to group by</param>
    /// <param name="reversed">Reverse order (This needs to be explained better)</param>
    [HandleProcessCorruptedStateExceptions]
    public void GenerateGroupsFromColumn(Collumns col, Boolean reversed = false) {
      if (col == null) {
        return;
      }

      this.BeginInvoke(new MethodInvoker(() => {
        this._IIListView.RemoveAllGroups();
        this.Groups.Clear();

        if (col.CollumnType == typeof(String)) {
          if (Settings.BESettings.IsTraditionalNameGrouping) {
            var groups = this.Items.ToArray().GroupBy(k => k.DisplayName.ToUpperInvariant().First(), e => e).OrderBy(o => o.Key);
            var i = reversed ? groups.Count() - 1 : 0;
            foreach (var group in groups) {
              var groupItems = group.Select(s => s).ToArray();
              groupItems.ToList().ForEach(c => c.GroupIndex = this.Groups.Count);
              this.Groups.Add(new ListViewGroupEx() { Items = groupItems, Index = reversed ? i-- : i++, Header = $"{group.Key.ToString()} ({groupItems.Count()})" });
            }
          } else {
            var i = reversed ? 3 : 0;

            Action<String, String, Boolean> addNameGroup = (String char1, String char2, Boolean isOthers) => {
              var testgrn = new ListViewGroupEx();
              if (isOthers) {
                testgrn.Items = this.Items.Where(w => (w.DisplayName.ToUpperInvariant().First() < Char.Parse("A") || w.DisplayName.ToUpperInvariant().First() > Char.Parse("Z")) &&
                                                      (w.DisplayName.ToUpperInvariant().First() < Char.Parse("0") || w.DisplayName.ToUpperInvariant().First() > Char.Parse("9"))).ToArray();
              } else {
                testgrn.Items = this.Items.Where(w => w.DisplayName.ToUpperInvariant().First() >= Char.Parse(char1) && w.DisplayName.ToUpperInvariant().First() <= Char.Parse(char2)).ToArray();
              }

              testgrn.Header = isOthers ? char1 + $" ({testgrn.Items.Count()})" : char1 + " - " + char2 + $" ({testgrn.Items.Count()})";
              testgrn.Index = reversed ? i-- : i++;
              this.Groups.Add(testgrn);
            };

            addNameGroup("0", "9", false);
            addNameGroup("A", "H", false);
            addNameGroup("I", "P", false);
            addNameGroup("Q", "Z", false);
            addNameGroup("Others", String.Empty, true);
          }
        } else if (col.CollumnType == typeof(Int64)) {
          var j = reversed ? 7 : 0;

          // TODO: Upgrade next to use an Action<>

          var uspec = new ListViewGroupEx {
            Items = this.Items.Where(w => w.IsFolder).ToArray()
          };
          uspec.Header = $"Unspecified ({uspec.Items.Count()})";
          uspec.Index = reversed ? j-- : j++;
          this.Groups.Add(uspec);

          var testgrn = new ListViewGroupEx {
            Items = this.Items.Where(w => Convert.ToInt64(w.GetPropertyValue(col.pkey, typeof(Int64)).Value) == 0 && !w.IsFolder).ToArray()
          };
          testgrn.Header = $"Empty ({testgrn.Items.Count()})";
          testgrn.Index = reversed ? j-- : j++;
          this.Groups.Add(testgrn);

          var testgr = new ListViewGroupEx {
            Items = this.Items.Where(w => Convert.ToInt64(w.GetPropertyValue(col.pkey, typeof(Int64)).Value) > 0 && Convert.ToInt64(w.GetPropertyValue(col.pkey, typeof(Int64)).Value) <= 10 * 1024).ToArray()
          };
          testgr.Header = $"Very Small ({testgr.Items.Count()})";
          testgr.Index = reversed ? j-- : j++;
          this.Groups.Add(testgr);

          var testgr2 = new ListViewGroupEx {
            Items = this.Items.Where(w => Convert.ToInt64(w.GetPropertyValue(col.pkey, typeof(Int64)).Value) > 10 * 1024 && Convert.ToInt64(w.GetPropertyValue(col.pkey, typeof(Int64)).Value) <= 100 * 1024).ToArray()
          };
          testgr2.Header = $"Small ({testgr2.Items.Count()})";
          testgr2.Index = reversed ? j-- : j++;
          this.Groups.Add(testgr2);

          var testgr3 = new ListViewGroupEx {
            Items = this.Items.Where(w => Convert.ToInt64(w.GetPropertyValue(col.pkey, typeof(Int64)).Value) > 100 * 1024 && Convert.ToInt64(w.GetPropertyValue(col.pkey, typeof(Int64)).Value) <= 1 * 1024 * 1024)
              .ToArray()
          };
          testgr3.Header = $"Medium ({testgr3.Items.Count()})";
          testgr3.Index = reversed ? j-- : j++;
          this.Groups.Add(testgr3);

          var testgr4 = new ListViewGroupEx {
            Items = this.Items
              .Where(w => Convert.ToInt64(w.GetPropertyValue(col.pkey, typeof(Int64)).Value) > 1 * 1024 * 1024 && Convert.ToInt64(w.GetPropertyValue(col.pkey, typeof(Int64)).Value) <= 16 * 1024 * 1024).ToArray()
          };
          testgr4.Header = $"Big ({testgr4.Items.Count()})";
          testgr4.Index = reversed ? j-- : j++;
          this.Groups.Add(testgr4);

          var testgr5 = new ListViewGroupEx {
            Items = this.Items.Where(w => Convert.ToInt64(w.GetPropertyValue(col.pkey, typeof(Int64)).Value) > 16 * 1024 * 1024 &&
                                          Convert.ToInt64(w.GetPropertyValue(col.pkey, typeof(Int64)).Value) <= 128 * 1024 * 1024).ToArray()
          };
          testgr5.Header = $"Huge ({testgr5.Items.Count()})";
          testgr5.Index = reversed ? j-- : j++;
          this.Groups.Add(testgr5);

          var testgr6 = new ListViewGroupEx {
            Items = this.Items.Where(w => Convert.ToInt64(w.GetPropertyValue(col.pkey, typeof(Int64)).Value) > 128 * 1024 * 1024).ToArray()
          };
          testgr6.Header = $"Gigantic ({testgr6.Items.Count()})";
          testgr6.Index = reversed ? j-- : j++;
          this.Groups.Add(testgr6);
        } else if (col.CollumnType == typeof(PerceivedType)) {
          var groups = this.Items.GroupBy(k => k.GetPropertyValue(col.pkey, typeof(String)).Value, e => e).OrderBy(o => o.Key);
          var i = reversed ? groups.Count() - 1 : 0;
          foreach (var group in groups.ToArray()) {
            var groupItems = group.Select(s => s).ToArray();
            this.Groups.Add(new ListViewGroupEx() { Items = groupItems, Index = reversed ? i-- : i++, Header = $"{((PerceivedType)group.Key).ToString()} ({groupItems.Count()})" });
          }
        } else {
          var groups = this.Items.GroupBy(k => k.GetPropertyValue(col.pkey, typeof(String)).Value, e => e).OrderBy(o => o.Key);
          var i = reversed ? groups.Count() - 1 : 0;
          foreach (var group in groups.ToArray()) {
            var groupItems = group.Select(s => s).ToArray();
            this.Groups.Add(new ListViewGroupEx() { Items = groupItems, Index = reversed ? i-- : i++, Header = $"{group.Key.ToString()} ({groupItems.Count()})" });
          }
        }

        if (reversed) {
          this.Groups.Reverse();
        }

        this._IIListView.SetItemCount(this.Items.Count, 0x2);
        foreach (var group in this.Groups.ToArray()) {
          group.Items.ToList().ForEach(e => e.GroupIndex = group.Index);
          var nativeGroup = group.ToNativeListViewGroup();
          var insertedPosition = -1;
          this._IIListView.InsertGroup(-1, nativeGroup, out insertedPosition);
        }

        this.LastGroupCollumn = col;
        this.LastGroupOrder = reversed ? SortOrder.Descending : SortOrder.Ascending;
        this.SetSortIcon(this.Collumns.IndexOf(col), this.LastGroupOrder);
      }));

      this.ScrollUpdateThreadRun();

    }

    /// <summary>
    /// Sets the Sort order of the Groups
    /// </summary>
    /// <param name="reverse">Reverse the Current Sort Order?</param>
    public void SetGroupOrder(Boolean reverse = true) => this.GenerateGroupsFromColumn(this.LastGroupCollumn, reverse && this.LastGroupOrder == SortOrder.Ascending);

    /// <summary>Returns the first selected item OR null if there is no selected item</summary>
    [DebuggerStepThrough]
    public IListItemEx GetFirstSelectedItem() {
      var lvi = this.ToLvItemIndex(-1);
      User32.SendMessage(this.LVHandle, LVM.GETNEXTITEMINDEX, ref lvi, LVNI.LVNI_SELECTED);
      return lvi.iItem == -1 || this.Items.Count < lvi.iItem ? null : this.Items[lvi.iItem];
    }

    /// <summary>Returns the first selected item's index OR -1 if there is no selected item</summary>
    public Int32 GetFirstSelectedItemIndex() {
      var lvi = this.ToLvItemIndex(-1);
      User32.SendMessage(this.LVHandle, LVM.GETNEXTITEMINDEX, ref lvi, LVNI.LVNI_SELECTED);
      return lvi.iItem;
    }

    /// <summary>
    /// Creates a new folder in the current directory and assigns a default name if none is specified. Returns the name
    /// </summary>
    /// <param name="name">The name of the new folder</param>
    /// <returns>Returns the name and assigns a default name if none is specified</returns>
    public String CreateNewFolder(String name) {
      if (String.IsNullOrEmpty(name)) {
        name = User32.LoadResourceString(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll"), 30396, "New Folder");
      }

      var fo = new IIFileOperation(this.Handle, false);
      fo.NewItem(this.CurrentFolder, name, FileAttributes.Directory | FileAttributes.Normal);
      fo.PerformOperations();

      return name;
    }

    /// <summary>
    /// Creates a new library folder
    /// </summary>
    /// <param name="name">The name of the lbrary folder youi want</param>
    /// <returns></returns>
    public BExplorer.Shell.ShellLibrary CreateNewLibrary(String name) {
      var endname = name;
      var suffix = 0;
      BExplorer.Shell.ShellLibrary lib = null;
      try {
        lib = BExplorer.Shell.ShellLibrary.Load(endname, true);
      } catch {
      }

      if (lib != null) {
        do {
          endname = name + $"({++suffix})";
          try {
            lib = BExplorer.Shell.ShellLibrary.Load(endname, true);
          } catch {
            lib = null;
          }
        } while (lib != null);
      }

      return new BExplorer.Shell.ShellLibrary(endname, false);
    }

    /// <summary>
    /// Sets the folder's icon
    /// </summary>
    /// <param name="wszPath">??</param>
    /// <param name="wszExpandedIconPath">??</param>
    /// <param name="iIcon">??</param>
    public void SetFolderIcon(String wszPath, String wszExpandedIconPath, Int32 iIcon) {
      var fcs = new Shell32.LPSHFOLDERCUSTOMSETTINGS() { iIconIndex = iIcon, cchIconFile = 0, dwMask = Shell32.FCSM_ICONFILE };
      fcs.dwSize = (UInt32)Marshal.SizeOf(fcs);
      fcs.pszIconFile = wszExpandedIconPath.Replace(@"\\", @"\");

      // Set the folder icon
      var hr = Shell32.SHGetSetFolderCustomSettings(ref fcs, wszPath.Replace(@"\\", @"\"), Shell32.FCS_FORCEWRITE);
      if (hr == HResult.S_OK) {
        this.UpdateIconCacheForFolder(wszPath); // Update the icon cache
      }

      this.RefreshItem(this._SelectedIndexes[0]);
    }

    private void UpdateIconCacheForFolder(String wszPath) {
      var sfi = default(SHFILEINFO);
      var res = Shell32.SHGetFileInfo(Marshal.StringToHGlobalAuto(wszPath), 0, out sfi, (Int32)Marshal.SizeOf(sfi), SHGFI.IconLocation);
      var iIconIndex = Shell32.Shell_GetCachedImageIndex(sfi.szDisplayName.Replace(@"\\", @"\"), sfi.iIcon, 0);
      Shell32.SHUpdateImage(sfi.szDisplayName.Replace(@"\\", @"\"), sfi.iIcon, 0x0002, iIconIndex);
      Shell32.SHChangeNotify(Shell32.HChangeNotifyEventID.SHCNE_UPDATEIMAGE, Shell32.HChangeNotifyFlags.SHCNF_DWORD | Shell32.HChangeNotifyFlags.SHCNF_FLUSHNOWAIT, IntPtr.Zero, (IntPtr)sfi.iIcon);
    }

    private Boolean _IsUpdateNotificationAllowed { get; set; } = true;
    /// <summary>
    /// Removes the folder's icon
    /// </summary>
    /// <param name="wszPath">??</param>
    /// <returns></returns>
    public HResult ClearFolderIcon(String wszPath) {
      this._IsUpdateNotificationAllowed = false;
      var fcs = new Shell32.LPSHFOLDERCUSTOMSETTINGS() { dwMask = Shell32.FCSM_ICONFILE };
      fcs.dwSize = (UInt32)Marshal.SizeOf(fcs);

      var hr = Shell32.SHGetSetFolderCustomSettings(ref fcs, wszPath, Shell32.FCS_FORCEWRITE);
      if (hr == HResult.S_OK) {
        // Update the icon cache
        this.UpdateIconCacheForFolder(wszPath.Replace(@"\\", @"\"));
      }

      this.RefreshItem(this._SelectedIndexes[0]);

      return hr;
    }

    /// <summary>Sets focus to tis control then deselects all items</summary>
    public void DeSelectAllItems() {
      this.BeginInvoke(new MethodInvoker(() => { this._IIListView.SetItemState(-1, LVIF.LVIF_STATE, LVIS.LVIS_SELECTED, 0); }));
      this.Focus();
    }

    public Boolean IsFocusAllowed = true;

    /// <summary>
    /// Gives the ShellListView focus
    /// </summary>
    /// <param name="isActiveCheck">Require this application's MainWindow to be activate the control</param>
    /// <param name="isForce">Force this to make the control active no matter what</param>
    public void Focus(Boolean isActiveCheck = true, Boolean isForce = false) {
      if (System.Windows.Application.Current == null || this.IsRenameInProgress) {
        return;
      }

      if (User32.GetForegroundWindow() != this.LVHandle) {
        try {
          this.BeginInvoke(new MethodInvoker(() => {
            if (isForce || ((System.Windows.Application.Current.MainWindow.IsActive || !isActiveCheck) && (this.IsFocusAllowed && this.Bounds.Contains(F.Cursor.Position)))) {
              User32.SetFocus(this.LVHandle);
              //User32.SetForegroundWindow(this.LVHandle);
            }
          }));
        } catch (Exception ex) {

        }
      }
      //Task.Run(() => {
      //  //GC.Collect();
      //  GC.WaitForFullGCComplete();
      //  GC.Collect();
      //});
    }

    public Int32 GetSelectedCount() => (Int32)User32.SendMessage(this.LVHandle, MSG.LVM_GETSELECTEDCOUNT, 0, 0);

    /// <summary>Inverse the selection of items</summary>
    public void InvertSelection() {
      this.BeginInvoke((Action)(() => {
        var itemCount = 0;
        this._IIListView.GetItemCount(out itemCount);

        for (var n = 0; n < itemCount; ++n) {
          var state = (LVIS)0;
          this._IIListView.GetItemState(n, LVIF.LVIF_STATE, LVIS.LVIS_SELECTED, out state);
          this._IIListView.SetItemState(n, LVIF.LVIF_STATE, LVIS.LVIS_SELECTED,
            (state & LVIS.LVIS_SELECTED) == LVIS.LVIS_SELECTED ? 0 : LVIS.LVIS_SELECTED);
        }

        this.Focus();
      }));
    }

    /// <summary>
    /// Automatically resize all controls
    /// </summary>
    /// <param name="autosizeParam">??</param>
    public void AutosizeAllColumns(Int32 autosizeParam) {
      return;
      for (var i = 0; i < this.Collumns.Count; i++) {
        this.AutosizeColumn(i, autosizeParam);
      }
    }

    public void RearangeColumns(IEnumerable<ListViewColumnHeader> cols) {
      this.RemoveAllCollumns(true);
      var i = 1;
      foreach (var col in cols.Select(s => s.Columns).Skip(1)) {
        var column2 = col.ToNativeColumn(this.View == ShellViewStyle.Details);
        User32.SendMessage(this.LVHandle, MSG.LVM_INSERTCOLUMN, i, ref column2);
        this.Collumns.Add(col);
        i++;
      }
    }

    #endregion Public Methods

    #region Private Methods

    public void EndLabelEdit(Boolean isCancel = false) {
      if (this._ItemForRename == -1 && !this.IsRenameInProgress) {
        return;
      }

      if (this.EndItemLabelEdit != null) {
        this.EndItemLabelEdit.Invoke(this, isCancel);
      }

      if (this._ItemForRename > -1) {



      }

      this._ItemForRename = -1;
      this._IsCanceledOperation = isCancel;
      this.IsRenameInProgress = false;
      this.IsRenameNeeded = false;
      this.RefreshItem(this.GetFirstSelectedItemIndex());


      this.Focus();
    }


    private Boolean ThreadRunHelper(SyncQueue<Int32?> queue, Boolean useComplexCheck, ref Int32? index) {
      try {
        index = queue.Dequeue();
        if (index == null) {
          return false;
        } else {
          var result = User32.SendMessage(this.LVHandle, MSG.LVM_ISITEMVISIBLE, index.Value, 0) != IntPtr.Zero;

          // var itemBounds = new User32.RECT();
          // var lvi = this.ToLvItemIndex(index.Value);

          // User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMINDEXRECT, ref lvi, ref itemBounds);

          // var r = new Rectangle(itemBounds.Left, itemBounds.Top, itemBounds.Right - itemBounds.Left, itemBounds.Bottom - itemBounds.Top);

          // if (useComplexCheck)
          // return index < Items.Count && r.IntersectsWith(this.ClientRectangle);
          // else
          // return r.IntersectsWith(this.ClientRectangle);
          return result;
        }
      } catch {
        return false;
      }
    }

    private async void RetrieveThumbnailByIndex(Int32 index) => await Task.Run(() => {
      if (this.IsCancelRequested) {
        return;
      }

      // F.Application.DoEvents();
      var itemBounds = default(User32.RECT);
      var lvi = new LVITEMINDEX() { iItem = index, iGroup = this.GetGroupIndex(index) };
      User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMINDEXRECT, ref lvi, ref itemBounds);

      var r = new Rectangle(itemBounds.Left, itemBounds.Top, itemBounds.Right - itemBounds.Left, itemBounds.Bottom - itemBounds.Top);

      if (r.IntersectsWith(this.ClientRectangle)) {
        var sho = this.Items[index];
        var icon = sho.GetHBitmap(this.IconSize, true, true);
        sho.IsThumbnailLoaded = true;
        sho.IsNeedRefreshing = false;
        if (icon != IntPtr.Zero) {
          Gdi32.ConvertPixelByPixel(icon, out var width, out var height);
          sho.IsOnlyLowQuality = (width > height && width != this.IconSize) || (width < height && height != this.IconSize) || (width == height && width != this.IconSize);
          Gdi32.DeleteObject(icon);
          this.RedrawItem(index);
        }
      }
    });

    private String PrepareSearchQuery(String query) {
      var prefix = "System.Generic.String:";
      if (query.StartsWith("*.")) {
        prefix = "fileextension:";
      }

      if (query.Contains(":")) {
        prefix = String.Empty;
      }

      return prefix + query;
    }

    private Boolean _IsFirstNavigate = true;
    private Boolean _IsFirstNavigate2 = true;
    /// <summary>
    /// Navigate to a folder.
    /// </summary>
    /// <param name="destination">The folder you want to navigate to.</param>
    /// <param name="isInSameTab">Do the navigation happens in same tab</param>
    /// <param name="refresh">Should the List be Refreshed?</param>
    /// <param name="isCancel">this.IsNavigationCancelRequested = isCancel</param>
    private void Navigate(IListItemEx? destination, Boolean isInSameTab = false, Boolean refresh = false, Boolean isCancel = false) {

      this.SaveSettingsToDatabase(this.CurrentFolder);

      // TODO: Document isCancel Param better
      if (destination == null) {
        return;
      }

      // destination = FileSystemListItem.ToFileSystemItem(destination.ParentHandle, destination.PIDL);
      if (this.RequestedCurrentLocation == destination && !refresh) {
        return;
      }

      //this.BeginInvoke((Action)(() => {
      this._VScrollWidth = 18;
      User32.MoveWindow(this.LVHandle, 0, 0, this.ClientRectangle.Width + this._VScrollWidth,
        this.ClientRectangle.Height, false);
      this.VScroll.Visibility = Visibility.Visible;
      //}));

      // if (this.RequestedCurrentLocation != destination) {
      //  this.IsCancelRequested = true;
      // }
      this.LargeImageList.ResetEvent.Set();
      this.SmallImageList.ResetEvent.Set();
      this._ResetEvent.Set();

      if (this._Threads.Count > 0) {
        this._Mre.Set();
        this._ResetEvent.Set();
        this.LargeImageList.ResetEvent.Set();
        this.SmallImageList.ResetEvent.Set();
        foreach (var thread in this._Threads.ToArray()) {
          thread.Interrupt();
          this._Threads.Remove(thread);
        }
      }

      this._UnvalidateTimer.Stop();
      this._IsDisplayEmptyText = false;
      var itemsForSel = new List<IListItemEx>();
      this.BeginInvoke((Action)(() => { itemsForSel.AddRange(this.SelectedItems); }));
      //User32.LockWindowUpdate(this.LVHeaderHandle);
      this.DisableGroups();
      this.RemoveAllCollumns();
      User32.SendMessage(this.LVHandle, MSG.LVM_SETITEMCOUNT, 0, 0);

      //User32.MoveWindow(this.LVHandle, 0, 0, this.ClientRectangle.Width + SystemInformation.VerticalScrollBarWidth, this.ClientRectangle.Height, false);
      //this.VScroll.Visible = false;
      //this.OnLVScroll?.Invoke(this, new ScrollEventArgs(new SCROLLINFO()));

      this.Focus(false, true);
      this._ItemForRename = -1;
      this._LastItemForRename = -1;
      this.Items.Clear();
      this._AddedItems.Clear();
      this.LargeImageList.ReInitQueues();
      this.SmallImageList.ReInitQueues();
      this._CuttedIndexes.Clear();
      this._ResetScrollEvent.Reset();
      this._TemporaryFiles.Clear();

      FolderSettings folderSettings;
      var isThereSettings = false;

      isThereSettings = this.LoadSettingsFromDatabase(destination, out folderSettings);

      this.RequestedCurrentLocation = destination;
      if (!refresh) {
        this.Navigating?.Invoke(this, new NavigatingEventArgs(destination, isInSameTab) { IsFirstItemAvailable = true });
      }

      var columns = new Collumns();
      Int32 currentI = 0, lastI = 0, k = 0;
      this.IsNavigationInProgress = true;

      this._ResetTimer.Stop();
      if (folderSettings.View == ShellViewStyle.Details || folderSettings.View == ShellViewStyle.SmallIcon || folderSettings.View == ShellViewStyle.List) {
        this.ResizeIcons(16);
        this.View = folderSettings.View;
      } else if (folderSettings.IconSize >= 16) {
        this.ResizeIcons(folderSettings.IconSize);
        if (folderSettings.View != ShellViewStyle.Tile && folderSettings.IconSize != 48 && folderSettings.IconSize != 72 && folderSettings.IconSize != 128 && folderSettings.IconSize != 256) {
          this.View = ShellViewStyle.Thumbnail;
        } else {
          this.View = folderSettings.View;
        }
      }
      if (isThereSettings) {
        if (folderSettings.Columns != null) {
          foreach (var collumn in folderSettings.Columns.Elements()) {
            var theColumn = this.AllAvailableColumns.FirstOrDefault(w => w.Value.ID == collumn?.Attribute("ID")?.Value).Value; // .Single();
            if (theColumn == null) {
              continue;
            }

            var theCollumnInternal = this.Collumns.SingleOrDefault(c => c.ID == theColumn?.ID);
            if (theCollumnInternal != null) {
              if (collumn.Attribute("Width")?.Value != "0") {
                theCollumnInternal.Width = Convert.ToInt32(collumn.Attribute("Width")?.Value);
              }
              var colIndex = this.Collumns.IndexOf(theCollumnInternal);
              if (this._IsFirstNavigate2) {
                //this.Invoke((Action)(() => {
                (this.Header.Columns[colIndex] as ListViewColumnHeader)?.SetColWidth(theCollumnInternal.Width, false);
                this._IsFirstNavigate2 = false;
                //}));
              } else {
                //this.Invoke((Action)(() => {
                (this.Header.Columns[colIndex] as ListViewColumnHeader)?.SetColWidth(theCollumnInternal.Width);
                // }));
              }

              continue;
            }

            if (collumn.Attribute("Width")?.Value != "0") {
              theColumn.Width = Convert.ToInt32(collumn.Attribute("Width")?.Value);
            }

            this.Collumns.Add(theColumn);
            //this.BeginInvoke((Action)(() => {
            this.Header.Columns.Add(new ListViewColumnHeader(this, theColumn, theColumn.ID == (String.IsNullOrEmpty(this.LastSortedColumnId) ? "A0" : this.LastSortedColumnId), !this._IsFirstNavigate));
            this._IsFirstNavigate = false;
            //}));
            var column2 = theColumn.ToNativeColumn(folderSettings.View == ShellViewStyle.Details);
            User32.SendMessage(this.LVHandle, MSG.LVM_INSERTCOLUMN, this.Collumns.Count - 1, ref column2);
            if (folderSettings.View != ShellViewStyle.Details) {
              this.AutosizeColumn(this.Collumns.Count - 1, -2);
            }

          }

        }
      } else {
        //this.Invoke((Action)(() => {
        this.AddDefaultColumns(false, true);
        //}));
        var value = destination.GetPropertyValue(SystemProperties.PerceivedType, typeof(PerceivedType))?.Value;
        if (value != null) {
          var perceivedType = (PerceivedType)value;
          if (perceivedType == PerceivedType.Image) {
            folderSettings = new FolderSettings {
              View = ShellViewStyle.ExtraLargeIcon,
              IconSize = 256
            };
            this.View = ShellViewStyle.ExtraLargeIcon;
          } else {
            this.View = ShellViewStyle.Details;
          }
        }


      }

      if (!String.IsNullOrEmpty(folderSettings.GroupCollumn)) {
        var colData = this.AllAvailableColumns.FirstOrDefault(w => w.Value.ID == folderSettings.GroupCollumn).Value;
        if (colData != null) {
          this.EnableGroups();
        } else {
          this.DisableGroups();
        }
      } else {
        this.DisableGroups();
      }


      columns = this.AllAvailableColumns.FirstOrDefault(w => w.Value.ID == folderSettings.SortColumn).Value;
      this.IsViewSelectionAllowed = false;


      //if (folderSettings.View == ShellViewStyle.Details || folderSettings.View == ShellViewStyle.SmallIcon || folderSettings.View == ShellViewStyle.List) {
      //  this.ResizeIcons(16);
      //  this.View = folderSettings.View;
      //} else if (folderSettings.IconSize >= 16) {
      //  this.ResizeIcons(folderSettings.IconSize);
      //  if (folderSettings.IconSize != 48 && folderSettings.IconSize != 96 && folderSettings.IconSize != 256) {
      //    this.View = ShellViewStyle.Thumbnail;
      //  } else {
      //    this.View = folderSettings.View;
      //  }
      //}

      this.IsViewSelectionAllowed = true;
      this.Invoke((Action)(() => this._NavWaitTimer.Start()));

      //for (var i = 0; i < this.Collumns.Count; i++) {
      //  this.Collumns[i].SetSplitButton(this.LVHeaderHandle, i);
      //}

      if (folderSettings.View != ShellViewStyle.Details) {
        this.AutosizeAllColumns(-2);
      }

      var sortColumnItem = this.Collumns.SingleOrDefault(s => s.ID == columns?.ID);
      var sortColIndex = sortColumnItem == null ? -1 : this.Collumns.IndexOf(sortColumnItem);
      if (isThereSettings) {
        if (sortColIndex > -1) {
          User32.SendMessage(this.LVHandle, MSG.LVM_SETSELECTEDCOLUMN, sortColIndex, 0);
        } else {
          User32.SendMessage(this.LVHandle, MSG.LVM_SETSELECTEDCOLUMN, -1, 0);
        }
      }

      if (sortColumnItem != null) {
        this.LastSortedColumnId = sortColumnItem.ID;
      }

      this.Header.Columns.OfType<ListViewColumnHeader>().ToList().ForEach(fe => {
        fe.UpdateIsSelected();
      });
      this._Notifications.UnregisterChangeNotify();
      this._Notifications.RegisterChangeNotify(this.Handle, destination, false);
      if (destination.IsFileSystem) {
        try {
          this._FsWatcher = new FileSystemWatcher(destination.ParsingName);
          //this._FsWatcher.Changed += (sender, args) => {
          //  try {
          //    //Thread.Sleep(2000);
          //    var objUpdateItem = FileSystemListItem.ToFileSystemItem(this.LVHandle, args.FullPath);
          //    if (objUpdateItem.IsInCurrentFolder(this.CurrentFolder)) {
          //      objUpdateItem = FileSystemListItem.ToFileSystemItem(this.LVHandle, objUpdateItem.PIDL);
          //      var exisitingUItem = this.Items.ToArray().FirstOrDefault(w => w.Equals(objUpdateItem));
          //      if (exisitingUItem != null) {
          //        if (this.View == ShellViewStyle.Details || this.View == ShellViewStyle.Tile) {
          //          foreach (var collumn in this.Collumns) {
          //            if ((collumn.Index > 0 && this.IconSize == 16) || (collumn.Index > 0 && this.View == ShellViewStyle.Tile)) {
          //              this.SmallImageList.EnqueueSubitemsGet(new Tuple<Int32, Int32, PROPERTYKEY>(exisitingUItem.ItemIndex, collumn.Index, collumn.pkey));
          //            }
          //          }
          //        }
          //        //else {
          //        if (this._IsUpdateNotificationAllowed) {
          //          if (this._ItemsQueue.Enqueue(new Tuple<ItemUpdateType, IListItemEx>(ItemUpdateType.Updated, exisitingUItem))) {
          //            this.UnvalidateDirectory(false);
          //          }
          //        } else {
          //          //this._IsUpdateNotificationAllowed = true;
          //        }
          //        //}
          //      }
          //    }
          //    objUpdateItem.Dispose();
          //  } catch (FileNotFoundException) {
          //    this.QueueDeleteItem(args, true);

          //    // Probably a temporary file 
          //    this._TemporaryFiles.Add(args.FullPath);
          //  } catch { }
          //};
          this._FsWatcher.Error += (sender, args) => {
            var ex = args.GetException();
          };
          //this._FsWatcher.Created += (sender, args) => {
          //  try {
          //    var existing = this.Items.FirstOrDefault(s => s.ParsingName.Equals(args.FullPath));
          //    if (existing != null) {
          //      return;
          //    }

          //    if (Path.GetExtension(args.FullPath)?.ToLowerInvariant() == ".tmp" || Path.GetExtension(args.FullPath) == String.Empty) {
          //      if (!this._TemporaryFiles.Contains(args.FullPath)) {
          //        this._TemporaryFiles.Add(args.FullPath);
          //      }
          //    }
          //    var obj = FileSystemListItem.ToFileSystemItem(this.LVHandle, args.FullPath.ToShellParsingName());
          //    if (obj.IsInCurrentFolder(this.CurrentFolder)) {
          //      if (!this.IsRenameNeeded) {
          //        if (this._ItemsQueue.Enqueue(new Tuple<ItemUpdateType, IListItemEx>(ItemUpdateType.Created, obj))) {
          //          this.UnvalidateDirectory();
          //        }
          //      }
          //    }
          //  } catch (FileNotFoundException) {
          //    this.QueueDeleteItem(args);
          //  } catch { }
          //};

          //this._FsWatcher.Deleted += (sender, args) => this.QueueDeleteItem(args);
          //this._FsWatcher.Renamed += (sender, args) => { };
          this._FsWatcher.IncludeSubdirectories = false;
          //this._FsWatcher.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.DirectoryName | NotifyFilters.Attributes | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Security |
          //                              NotifyFilters.Size;
        } catch (ArgumentException) {
          this._FsWatcher = new FileSystemWatcher();
        }


        try {
          if (!String.IsNullOrEmpty(_FsWatcher?.Path)) {
            this._FsWatcher.EnableRaisingEvents = true;
          }
        } catch (FileNotFoundException) {
        }
      }
      var navigationThread = new Thread(() => {
        try {
          this._ResetScrollEvent.Reset();
          this.IsCancelRequested = false;


          this.RequestedCurrentLocation = destination;
          var content = destination;

          foreach (var shellItem in this.IsSearchNavigating ? content.GetContents(this.ShowHidden).TakeWhile(shellItem => !this.IsCancelRequested) : content.GetContents(this.ShowHidden).TakeWhile(shellItem => !this.IsCancelRequested).SetSortCollumn(this, true, columns, isThereSettings ? folderSettings.SortOrder : SortOrder.Ascending, false)) {
            if (currentI == 0) {
              //this.Navigating?.Invoke(this, new NavigatingEventArgs(destination, isInSameTab) { IsFirstItemAvailable = true });
            }
            currentI++;
            if (shellItem == null) {
              continue;
            }
            if (!this.RequestedCurrentLocation.Equals(shellItem?.Parent) && this.IsNavigationCancelRequested) {
              this.IsNavigationCancelRequested = false;
              return;
            }

            shellItem!.ItemIndex = k++;

            this.Items.Add(shellItem);
            var delta = currentI - lastI;
            if (delta >= 20) {
              lastI = currentI;
            }

            if (this.IsSearchNavigating) {
              this.BeginInvoke((Action)(() => {
                this._NavWaitTimer.Stop();
                this._IsDisplayEmptyText = false;
                this._IIListView.ResetEmptyText();
              }));

              //  //  //if (this.IsGroupsEnabled) {
              //  //  //  var colData = this.AllAvailableColumns.FirstOrDefault(w => w.Value.ID == folderSettings.GroupCollumn).Value;
              //  //  //  this.GenerateGroupsFromColumn(colData, folderSettings.GroupOrder == SortOrder.Descending);
              //  //  //} else {
              //  //this.SetSortCollumn(true, columns, isThereSettings ? folderSettings.SortOrder : SortOrder.Ascending, false);
              User32.SendMessage(this.LVHandle, MSG.LVM_SETITEMCOUNT, this.Items.Count, 0x2);

              //  Thread.Sleep(2);
              //F.Application.DoEvents();
              if (delta >= 20) {
                this.ScrollUpdateThreadRun(false, false);
              }
              //  //  //}
            }

            shellItem.Dispose();
            if (currentI == 1) {
              this.BeginInvoke((Action)(() => {
                this._NavWaitTimer.Stop();
                this._IsDisplayEmptyText = false;
                this._IIListView.ResetEmptyText();
              }));
            }
          }

          this.IsCancelRequested = false;
          this.IsNavigationInProgress = false;

          if (this.RequestedCurrentLocation.NavigationStatus != HResult.S_OK) {
            this.BeginInvoke((Action)(() => {
              this._NavWaitTimer.Stop();
              this._IsDisplayEmptyText = false;
              this._IIListView.ResetEmptyText();
              this.Navigate(this.CurrentFolder, true);
            }));
            GC.Collect();
            Shell32.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
            if (this._Threads.Count <= 1) {
              return;
            }

            this._Mre.Set();
            this._ResetEvent.Set();
            this._Threads[0].Interrupt();
            this._Threads.RemoveAt(0);
            return;
          }

          if (this.IsGroupsEnabled) {
            var colData = this.AllAvailableColumns.FirstOrDefault(w => w.Value.ID == folderSettings.GroupCollumn).Value;
            this.GenerateGroupsFromColumn(colData, folderSettings.GroupOrder == SortOrder.Descending);
          } else {
            User32.SendMessage(this.LVHandle, MSG.LVM_SETITEMCOUNT, this.Items.Count, 0x2);
            //this.ScrollUpdateThreadRun();
          }
          //this.Invoke((Action)(() => {
          //  User32.LockWindowUpdate(IntPtr.Zero);
          //}));
          itemsForSel.Add(this.CurrentFolder);
          this.SelectItems(itemsForSel.ToArray(), true);
          this.ScrollUpdateThreadRun(true);
          this.Header.Columns.OfType<ListViewColumnHeader>().ToList().ForEach(e => {
            e.SetResizableState(this.View == ShellViewStyle.Details);
          });

          if (!isThereSettings) {
            this.LastSortedColumnId = "A0";
            this.LastSortOrder = SortOrder.Ascending;
            //this.SetSortIcon(0, SortOrder.Ascending);
            User32.SendMessage(this.LVHandle, MSG.LVM_SETSELECTEDCOLUMN, 0, 0);
            this.Header.Columns.OfType<ListViewColumnHeader>().ToList().ForEach(fe => {
              fe.UpdateIsSelected();
            });
          }

          this._IsDisplayEmptyText = false;
          this.BeginInvoke((Action)(() => {
            var navArgs = new NavigatedEventArgs(this.RequestedCurrentLocation, this.CurrentFolder, isInSameTab);
            this.CurrentFolder = this.RequestedCurrentLocation;
            //if (!refresh) {
            this.Navigated?.Invoke(this, navArgs);
            //}
          }));

          this.Focus();
          this.BeginInvoke((Action)(() => {
            this._NavWaitTimer.Stop();
            this._IsDisplayEmptyText = false;
            this._IIListView.ResetEmptyText();
          }));
          //GC.WaitForFullGCComplete();
          GC.Collect();

          Shell32.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
          this.BeginInvoke((Action)(() => {
            if (this._SearchTimer.Enabled) {
              this._SearchTimer.Stop();
            }
          }));


          this._ResetScrollEvent.Set();
          this._Mre.Reset();
          this._Mre.WaitOne();
        } catch (ThreadInterruptedException ex) {

        }

      });

      navigationThread.SetApartmentState(ApartmentState.STA);
      this._Threads.Add(navigationThread);
      navigationThread.Start();
    }

    public void QueueCreateItem(IListItemEx item) {
      if (this._ItemsQueue.Enqueue(new Tuple<ItemUpdateType, IListItemEx>(ItemUpdateType.Updated, item))) {
        this.UnvalidateDirectory();
      }
    }
    public void QueueDeleteItem(FileSystemEventArgs args, Boolean isSimpleEnqueue = false) {
      this._TemporaryFiles.Remove(args.FullPath);
      var existingItem = this.Items.ToArray().FirstOrDefault(s => s.ParsingName.Equals(args.FullPath));
      if (existingItem != null && (isSimpleEnqueue || (existingItem.IsFolder || this._TemporaryFiles.Count(c => c.Contains(Path.GetFileName(existingItem.ParsingName))) == 0))) {
        this._ItemsQueue.Enqueue(Tuple.Create(ItemUpdateType.Deleted, existingItem), true);
        this.UnvalidateDirectory();
      }
    }

    public void QueueDeleteItem(IListItemEx item, Boolean isSimpleEnqueue = false) {
      this._TemporaryFiles.Remove(item.ParsingName);
      var existingItem = this.Items.ToArray().FirstOrDefault(s => s.Equals(item));
      if (existingItem != null && (isSimpleEnqueue || (existingItem.IsFolder || this._TemporaryFiles.Count(c => c.Contains(Path.GetFileName(existingItem.ParsingName))) == 0))) {
        this._ItemsQueue.Enqueue(Tuple.Create(ItemUpdateType.Deleted, existingItem), true);
        this.UnvalidateDirectory();
      }
    }

    private void NavigateSearch(IListItemEx? destination, Boolean isInSameTab = false, Boolean refresh = false, Boolean isCancel = false) {
      this.SaveSettingsToDatabase(this.CurrentFolder);
      if (destination == null) {
        return;
      }

      if (this.RequestedCurrentLocation == destination && !refresh) {
        return;
      }

      this._ResetEvent.Set();

      if (this._Threads.Any()) {
        this._Mre.Set();
        this._ResetEvent.Set();
        foreach (var thread in this._Threads.ToArray()) {
          thread.Interrupt();
          this._Threads.Remove(thread);
        }
      }

      this._UnvalidateTimer.Stop();
      this._IsDisplayEmptyText = false;
      User32.SendMessage(this.LVHandle, MSG.LVM_SETITEMCOUNT, 0, 0);
      this.DisableGroups();

      this._ItemForRename = -1;
      this._LastItemForRename = -1;

      this.Items.Clear();
      this._AddedItems.Clear();
      this.LargeImageList.ReInitQueues();
      this.SmallImageList.ReInitQueues();
      this._CuttedIndexes.Clear();
      this.RequestedCurrentLocation = destination;
      if (!refresh) {
        this.Navigating?.Invoke(this, new NavigatingEventArgs(destination, isInSameTab));
      }

      var columns = new Collumns();
      Int32 currentI = 0, lastI = 0, k = 0;
      this.IsNavigationInProgress = true;

      this._ResetTimer.Stop();

      this.RemoveAllCollumns();
      this.AddDefaultColumns(false, true);
      this.AfterCollumsPopulate?.Invoke(this, new ColumnAddEventArgs(null) { Collumns = this.Collumns });

      this.IsViewSelectionAllowed = true;
      this.Invoke((Action)(() => this._NavWaitTimer.Start()));
      var navigationThread = new Thread(() => {
        destination = FileSystemListItem.ToFileSystemItem(destination.ParentHandle, destination.PIDL);
        this.RequestedCurrentLocation = destination;
        this.BeginInvoke((Action)(() => {
          if (!this._SearchTimer.Enabled) {
            this._SearchTimer.Start();
          }
        }));

        foreach (var shellItem in destination.TakeWhile(shellItem => !this.IsCancelRequested)) {
          currentI++;
          this._Smre.WaitOne();

          if (this.ShowHidden || !shellItem.IsHidden) {
            shellItem.ItemIndex = k++;
            this.Items.Add(shellItem);
            if (currentI == 1) {
              this.BeginInvoke((Action)(() => {
                this._NavWaitTimer.Stop();
                this._IsDisplayEmptyText = false;
                this._IIListView.ResetEmptyText();
              }));
            }
          }

          var delta = currentI - lastI;
          if (delta >= (this.IsSearchNavigating ? 1 : 5000)) {
            lastI = currentI;
          }
          if (this.IsSearchNavigating && delta >= 20) {
            Shell32.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
          }
        }
        this.IsCancelRequested = false;
        this.IsNavigationInProgress = false;

        if (this.RequestedCurrentLocation.NavigationStatus != HResult.S_OK) {
          this.BeginInvoke((Action)(() => {
            if (this._SearchTimer.Enabled) {
              this._SearchTimer.Stop();
            }
          }));
          this.BeginInvoke((Action)(() => {
            var navArgs = new NavigatedEventArgs(this.RequestedCurrentLocation, this.CurrentFolder, isInSameTab);
            this.CurrentFolder = this.RequestedCurrentLocation;

            if (!refresh) {
              this.Navigated?.Invoke(this, navArgs);
            }
          }));

          GC.Collect();
          Shell32.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
          if (this._Threads.Count <= 1) {
            return;
          }

          this._Mre.Set();
          this._ResetEvent.Set();
          this._Threads[0].Interrupt();
          this._Threads.RemoveAt(0);
          return;
        }


        for (var i = 0; i < this.Collumns.Count; i++) {
          this.Collumns[i].SetSplitButton(this.LVHeaderHandle, i);
        }

        if (this.View != ShellViewStyle.Details) {
          this.AutosizeAllColumns(-2);
        }

        var sortColIndex = 0;

        this.SetSortCollumn(false, this.Collumns.First(), SortOrder.Ascending, false);

        this.BeginInvoke((Action)(() => {
          var navArgs = new NavigatedEventArgs(this.RequestedCurrentLocation, this.CurrentFolder, isInSameTab);
          this.CurrentFolder = this.RequestedCurrentLocation;
          if (!refresh) {
            this.Navigated?.Invoke(this, navArgs);
          }
        }));

        GC.Collect();
        Shell32.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
        this.BeginInvoke((Action)(() => {
          if (this._SearchTimer.Enabled) {
            this._SearchTimer.Stop();
          }
        }));
        this._Mre.Reset();
        this._Mre.WaitOne();
      });
      navigationThread.SetApartmentState(ApartmentState.STA);
      this._Threads.Add(navigationThread);
      navigationThread.Start();
    }

    private Boolean LoadSettingsFromDatabase(IListItemEx directory, out FolderSettings folderSettings) {
      var result = false;
      var folderSetting = new FolderSettings();
      if (directory.IsSearchFolder) {
        folderSettings = folderSetting;
        return false;
      }

      try {
        var mDbConnection = new SQLite.SQLiteConnection("Data Source=" + this._DBPath + ";Version=3;");
        mDbConnection.Open();

        var command1 = new SQLite.SQLiteCommand("SELECT * FROM foldersettings WHERE Path=@0", mDbConnection);
        command1.Parameters.AddWithValue("0", directory.ParsingName);

        var reader = command1.ExecuteReader();
        if (reader.Read()) {
          var values = reader.GetValues();
          if (values.Count > 0) {
            result = true;
            var view = values?.GetValues("View")?.FirstOrDefault();
            var iconSize = values?.GetValues("IconSize")?.FirstOrDefault();
            var lastSortedColumnIndex = values?.GetValues("LastSortedColumn")?.FirstOrDefault();
            var lastSortOrder = values?.GetValues("LastSortOrder")?.FirstOrDefault();
            var lastGroupedColumnId = values?.GetValues("LastGroupCollumn")?.FirstOrDefault();
            var lastGroupoupOrder = values?.GetValues("LastGroupOrder")?.FirstOrDefault();

            if (view != null) {
              folderSetting.View = (ShellViewStyle)Enum.Parse(typeof(ShellViewStyle), view);
            }

            if (lastSortedColumnIndex != null) {
              folderSetting.SortColumn = lastSortedColumnIndex;
              folderSetting.SortOrder = (SortOrder)Enum.Parse(typeof(SortOrder), lastSortOrder);
            }

            folderSetting.GroupCollumn = lastGroupedColumnId;
            folderSetting.GroupOrder = lastGroupoupOrder == SortOrder.Ascending.ToString() ? SortOrder.Ascending : SortOrder.Descending;

            var collumns = values?.GetValues("Columns")?.FirstOrDefault();
            folderSetting.Columns = collumns != null ? XElement.Parse(collumns) : null;

            if (String.IsNullOrEmpty(iconSize)) {
              folderSetting.IconSize = 48;
            } else {
              folderSetting.IconSize = Int32.Parse(iconSize);
            }
          }
        }

        reader.Close();
      } catch (Exception) {
      }

      folderSettings = folderSetting;
      return result;
    }

    private void RenameItem(Int32 index) {
      this.Focus(false, true);
      this.IsFocusAllowed = false;
      this._IsCanceledOperation = false;
      this._ItemForRename = index;
      //this.IsRenameInProgress = true;
      var ptr = IntPtr.Zero;
      this.BeginInvoke(new MethodInvoker(() => this._IIListView.EditLabel(this.ToLvItemIndex(index), IntPtr.Zero, out ptr)));
    }

    private void Do_Copy_OR_Move_Helper(Boolean copy, IListItemEx destination, IShellItem[] items) {
      var handle = this.Handle;
      var dlg = new FileOperation(this);
      var thread = new Thread(() => {
        var fo = new IIFileOperation(dlg, handle, false, dlg);
        foreach (var item in items) {
          if (copy) {
            fo.CopyItem(item, destination);
          } else {
            fo.MoveItem(item, destination, null);
          }
        }
        fo.PerformOperations();
      });
      dlg.CurrentThread = thread;
      thread.SetApartmentState(ApartmentState.STA);
      thread.IsBackground = true;
      thread.Start();
    }

    private void Do_Copy_OR_Move_Helper_2(Boolean copy, IListItemEx destination, F.IDataObject dataObject) {
      var handle = this.Handle;
      IShellItemArray? shellItemArray = null;
      IShellItem[]? items = null;

      if (((F.DataObject)dataObject).ContainsFileDropList()) {
        items = ((F.DataObject)dataObject).GetFileDropList().OfType<String>().Select(s => ShellItem.ToShellParsingName(s).ComInterface).ToArray();
      } else {
        shellItemArray = dataObject.ToShellItemArray();
        items = shellItemArray.ToArray();
      }
      var dlg = new FileOperation(this);
      var thread = new Thread(() => {
        try {

          var fo = new IIFileOperation(dlg, handle, false, dlg);
          foreach (var item in items) {
            if (copy) {
              fo.CopyItem(item, destination);
            } else {
              fo.MoveItem(item, destination, null);
            }
          }

          fo.PerformOperations();
        } catch (SecurityException) {
          throw;
        }
      });
      dlg.CurrentThread = thread;
      thread.SetApartmentState(ApartmentState.STA);
      thread.IsBackground = true;
      thread.Start();
    }

    private void UpdateColsInView(Boolean isDetails = false) {
      foreach (var col in this.Collumns.ToArray()) {
        var colIndex = this.Collumns.IndexOf(col);
        var colNative = col.ToNativeColumn(isDetails);
        User32.SendMessage(this.LVHandle, MSG.LVM_SETCOLUMN, colIndex, ref colNative);
        col.SetSplitButton(this.LVHeaderHandle, colIndex);
        if (col.ID == this.LastSortedColumnId) {
          this.SetSortIcon(colIndex, this.LastSortOrder);
        }
      }
    }

    private void UpdateColInView(Collumns col, Boolean isDetails = false) {
      var colIndex = this.Collumns.IndexOf(col);
      var colNative = col.ToNativeColumn(isDetails);
      User32.SendMessage(this.LVHandle, MSG.LVM_SETCOLUMN, colIndex, ref colNative);
      col.SetSplitButton(this.LVHeaderHandle, colIndex);
      if (col.ID == this.LastSortedColumnId) {
        this.SetSortIcon(colIndex, this.LastSortOrder);
      }
    }

    private LVITEMINDEX ToLvItemIndex(Int32 index) => new LVITEMINDEX() { iItem = index, iGroup = this.GetGroupIndex(index) };

    public void RedrawItem(Int32 index, Int32 delay = -1) {
      if (delay > -1) {
        Thread.Sleep(delay);
      }

      //Thread.Sleep(8);
      var itemBounds = new User32.RECT() { Left = 1 };
      var lvi = new LVITEMINDEX() { iItem = index, iGroup = this.GetGroupIndex(index) };
      User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMINDEXRECT, ref lvi, ref itemBounds);
      itemBounds.Left -= 2;
      itemBounds.Top -= 2;
      itemBounds.Bottom += 2;
      itemBounds.Right += 2;
      this.Invoke(new MethodInvoker(() => this._IIListView.RedrawItems(index, index)));

      // TODO: Find out why we have this loop
      //for (Int32 i = 0; i < 1; i++) {
      //  if (this.IsGroupsEnabled) {
      //    this.RedrawWindow(itemBounds);
      //  }
      //}
    }

    private void ProcessShellNotifications(IntPtr wParam, IntPtr lParam) {
      if (this._Notifications.NotificationReceipt(wParam, lParam)) {
        foreach (var info in this._Notifications.NotificationsReceived.OfType<NotifyInfos>().ToArray()) {
          try {
            switch (info.Notification) {
              case ShellNotifications.SHCNE.SHCNE_MKDIR:
              case ShellNotifications.SHCNE.SHCNE_CREATE:
                try {
                  var obj = FileSystemListItem.ToFileSystemItem(this.LVHandle, info.Item1);
                  this.NewItemAvailable?.Invoke(this, new ItemUpdatedEventArgs(ItemUpdateType.Created, obj.Clone(), null, -1));
                  if (obj.IsInCurrentFolder(this.CurrentFolder)) {
                    obj = FileSystemListItem.ToFileSystemItem(this.LVHandle, obj.PIDL);
                    if (this.IsRenameNeeded) {
                      var existingItem = this.Items.FirstOrDefault(s => s.Equals(obj));
                      if (existingItem == null) {
                        var itemIndex = this.InsertNewItem(obj);
                        this.SelectAndRenameItemByIndex(itemIndex, true, true);
                      } else {
                        this.IsRenameofNewItem = true;
                        this.RenameSelectedItem(existingItem.ItemIndex);
                      }
                    } else if (this._ItemsQueue.Enqueue(new Tuple<ItemUpdateType, IListItemEx>(ItemUpdateType.Created, obj))) {
                      this.UnvalidateDirectory();
                    }
                  }
                  obj.Dispose();
                } catch (FileNotFoundException) { }
                break;

              case ShellNotifications.SHCNE.SHCNE_RMDIR:
              case ShellNotifications.SHCNE.SHCNE_DELETE:
                var objDelete = FileSystemListItem.ToFileSystemItem(this.LVHandle, info.Item1);
                objDelete = FileSystemListItem.ToFileSystemItem(this.LVHandle, objDelete.PIDL);
                if (this._ItemsQueue.Enqueue(Tuple.Create(ItemUpdateType.RecycleBin, FileSystemListItem.InitializeWithIShellItem(IntPtr.Zero, ((ShellItem)KnownFolders.RecycleBin).ComInterface)))) {
                  this.UnvalidateDirectory();
                }

                if (objDelete.IsInCurrentFolder(this.CurrentFolder) && this._ItemsQueue.Enqueue(Tuple.Create(ItemUpdateType.Deleted, objDelete.Clone()), true)) {
                  this.UnvalidateDirectory();
                  this.RaiseItemUpdated(ItemUpdateType.Deleted, null, objDelete, -1);
                  objDelete.Dispose();
                  break;
                }

                break;

              case ShellNotifications.SHCNE.SHCNE_UPDATEDIR:
                IListItemEx? objUpdate = null;
                try {
                  objUpdate = FileSystemListItem.ToFileSystemItem(this.LVHandle, info.Item1);
                  objUpdate = FileSystemListItem.ToFileSystemItem(this.LVHandle, objUpdate.PIDL);
                } catch { }
                if (objUpdate.IsInCurrentFolder(this.CurrentFolder)) {
                  this.UnvalidateDirectory();
                }
                objUpdate.Dispose();
                break;

              case ShellNotifications.SHCNE.SHCNE_UPDATEITEM:
                var objUpdateItem = FileSystemListItem.ToFileSystemItem(this.LVHandle, info.Item1);
                if (objUpdateItem.IsInCurrentFolder(this.CurrentFolder)) {
                  objUpdateItem = FileSystemListItem.ToFileSystemItem(this.LVHandle, objUpdateItem.PIDL);
                  var exisitingUItem = this.Items.ToArray().FirstOrDefault(w => w.Equals(objUpdateItem));
                  if (exisitingUItem != null) {
                    if (this.View == ShellViewStyle.Details || this.View == ShellViewStyle.Tile) {
                      foreach (var collumn in this.Collumns) {
                        if ((collumn.Index > 0 && this.IconSize == 16) || (collumn.Index > 0 && this.View == ShellViewStyle.Tile)) {
                          this.SmallImageList.EnqueueSubitemsGet(new Tuple<Int32, Int32, PROPERTYKEY>(exisitingUItem.ItemIndex, collumn.Index, collumn.pkey));
                        }
                      }
                    }
                    //else {
                    if (this._ItemsQueue.Enqueue(new Tuple<ItemUpdateType, IListItemEx>(ItemUpdateType.Updated, exisitingUItem))) {
                      this.UnvalidateDirectory(false);
                    }
                    //}
                  }
                  objUpdateItem.Dispose();
                }

                break;

              case ShellNotifications.SHCNE.SHCNE_RENAMEFOLDER:
              case ShellNotifications.SHCNE.SHCNE_RENAMEITEM:
                if (!this.IsRenameInProgress) {
                  var obj1 = FileSystemListItem.ToFileSystemItem(this.LVHandle, info.Item1, IntPtr.Zero);
                  var obj2 = FileSystemListItem.ToFileSystemItem(this.LVHandle, info.Item2, IntPtr.Zero);
                  if (!String.IsNullOrEmpty(obj1.ParsingName) && !String.IsNullOrEmpty(obj2.ParsingName)) {
                    this.UpdateItem(obj1, obj2);
                  }
                }

                //this.IsRenameInProgress = false;
                break;

              case ShellNotifications.SHCNE.SHCNE_NETSHARE:
              case ShellNotifications.SHCNE.SHCNE_NETUNSHARE:
              case ShellNotifications.SHCNE.SHCNE_ATTRIBUTES:
                var objNetA = FileSystemListItem.ToFileSystemItem(this.LVHandle, info.Item1);
                var exisitingItemNetA = this.Items.FirstOrDefault(w => w.Equals(objNetA));
                if (this.View == ShellViewStyle.Details || this.View == ShellViewStyle.Tile) {
                  foreach (var collumn in this.Collumns) {
                    if ((collumn.Index > 0 && this.IconSize == 16) || (collumn.Index > 0 && this.View == ShellViewStyle.Tile)) {
                      this.SmallImageList.EnqueueSubitemsGet(new Tuple<Int32, Int32, PROPERTYKEY>(exisitingItemNetA.ItemIndex, collumn.Index, collumn.pkey));
                    }
                  }
                }

                if (this._ItemsQueue.Enqueue(new Tuple<ItemUpdateType, IListItemEx>(ItemUpdateType.Updated, exisitingItemNetA))) {
                  this.UnvalidateDirectory(false);
                }
                objNetA.Dispose();
                break;

              case ShellNotifications.SHCNE.SHCNE_MEDIAINSERTED:
              case ShellNotifications.SHCNE.SHCNE_MEDIAREMOVED:
                if (this.CurrentFolder.ParsingName == KnownFolders.Computer.ParsingName) {
                  var objMedia = FileSystemListItem.ToFileSystemItem(this.LVHandle, info.Item1);
                  var exisitingItem = this.Items.SingleOrDefault(w => w.Equals(objMedia));
                  if (exisitingItem != null) {
                    this.UpdateItem(exisitingItem.ItemIndex);
                  }
                  objMedia.Dispose();
                }
                break;

              case ShellNotifications.SHCNE.SHCNE_DRIVEREMOVED:
                var objDr = FileSystemListItem.ToFileSystemItem(this.LVHandle, info.Item1);
                if (this.CurrentFolder != null && this.CurrentFolder.ParsingName.Equals(KnownFolders.Computer.ParsingName)) {
                  this.Items.Remove(objDr);
                  var i = 0;
                  this.Items.ToList().ForEach(e => e.ItemIndex = i++);
                  if (this.IsGroupsEnabled) {
                    this.SetGroupOrder(false);
                  }

                  User32.SendMessage(this.LVHandle, MSG.LVM_SETITEMCOUNT, this.Items.Count, 0);
                }

                this.RaiseItemUpdated(ItemUpdateType.DriveRemoved, null, objDr, -1);
                break;

              case ShellNotifications.SHCNE.SHCNE_DRIVEADD:
                if (this.CurrentFolder != null && this.CurrentFolder.ParsingName.Equals(KnownFolders.Computer.ParsingName)) {
                  this.InsertNewItem(FileSystemListItem.ToFileSystemItem(this.LVHandle, info.Item1));
                }

                break;
              case ShellNotifications.SHCNE.SHCNE_FREESPACE:
                // if (this._ItemsQueue.Enqueue(Tuple.Create(ItemUpdateType.RecycleBin, FileSystemListItem.InitializeWithIShellItem(IntPtr.Zero, ((ShellItem)KnownFolders.RecycleBin).ComInterface)))) {
                // this.UnvalidateDirectory();
                // }
                break;
              case ShellNotifications.SHCNE.SHCNE_UPDATEIMAGE:
                var objUpdateItemUI = FileSystemListItem.ToFileSystemItem(this.LVHandle, info.Item1);
                if (objUpdateItemUI.IsInCurrentFolder(this.CurrentFolder)) {
                  this.RaiseItemUpdated(ItemUpdateType.IconUpdated, objUpdateItemUI, objUpdateItemUI,
                    objUpdateItemUI.ItemIndex);
                  this._ItemsQueue.Enqueue(
                    new Tuple<ItemUpdateType, IListItemEx>(ItemUpdateType.IconUpdated, objUpdateItemUI));
                }

                this._IsUpdateNotificationAllowed = true;
                break;
            }
          } catch {
          }

          this._Notifications.NotificationsReceived.Remove(info);
        }
      }
    }

    internal static void Drag_SetEffect(F.DragEventArgs e) {
      if ((e.KeyState & (8 + 32)) == (8 + 32) && (e.AllowedEffect & F.DragDropEffects.Link) == F.DragDropEffects.Link) {
        e.Effect = F.DragDropEffects.Link; // Link drag-and-drop effect.// KeyState 8 + 32 = CTL + ALT
      } else if ((e.KeyState & 32) == 32 && (e.AllowedEffect & F.DragDropEffects.Link) == F.DragDropEffects.Link) {
        e.Effect = F.DragDropEffects.Link; // ALT KeyState for link.
      } else if ((e.KeyState & 4) == 4 && (e.AllowedEffect & F.DragDropEffects.Move) == F.DragDropEffects.Move) {
        e.Effect = F.DragDropEffects.Move; // SHIFT KeyState for move
      } else if ((e.KeyState & 8) == 8 && (e.AllowedEffect & F.DragDropEffects.Copy) == F.DragDropEffects.Copy) {
        e.Effect = F.DragDropEffects.Copy; // CTL KeyState for copy.
      } else if ((e.AllowedEffect & F.DragDropEffects.Move) == F.DragDropEffects.Move) {
        e.Effect = F.DragDropEffects.Move; // By default, the drop action should be move, if allowed.
      } else {
        e.Effect = F.DragDropEffects.Copy;
      }
    }

    private IListItemEx GetBadgeForPath(String path) {
      var allBadges = this.BadgesData.SelectMany(s => s.Value).ToArray();
      var foundBadge = allBadges.Where(w => w.Value.Count(c => c.ToLowerInvariant().Equals(path.ToLowerInvariant())) > 0).FirstOrDefault();
      return foundBadge.Equals(default(KeyValuePair<IListItemEx, List<String>>)) ? null : foundBadge.Key;
    }

    internal void OnSelectionChanged() {
      this.SelectionChanged?.Invoke(this, EventArgs.Empty);
      //this.ScrollUpdateThreadRun();
      Task.Run(() => {
        //GC.Collect();
        GC.WaitForFullGCComplete();
        GC.Collect();
      });

    }

    [Obsolete("Contains No Code")]
    private void RedrawWindow() {
    } // User32.InvalidateRect(this.LVHandle, IntPtr.Zero, false);

    [Obsolete("Contains No Code")]
    private void RedrawWindow(User32.RECT rect) {
    } // => User32.InvalidateRect(this.LVHandle, ref rect, false);

    /// <summary>
    /// Returns the index of the first item whose display name starts with the search string.
    /// </summary>
    /// <param name="search">     The string for which to search for. </param>
    /// <param name="startindex">
    /// The index from which to start searching. Enter '0' to search all items.
    /// </param>
    /// <returns> The index of an item within the list view. </returns>
    private Int32 GetFirstIndexOf(String search, Int32 startindex) {
      var i = startindex;
      while (true) {
        if (i >= this.Items.Count) {
          return -1;
        } else if (this.Items[i].DisplayName.ToUpperInvariant().StartsWith(search.ToUpperInvariant())) {
          return i;
        } else {
          i++;
        }
      }
    }

    private void StartProcessInCurrentDirectory(IListItemEx item) {
      var res = Process.Start(new ProcessStartInfo() { FileName = item.ParsingName, WorkingDirectory = this.CurrentFolder.ParsingName, UseShellExecute = true });
    }

    internal void OnItemMiddleClick() {
      if (this.ItemMiddleClick != null) {
        var row = -1;
        var column = -1;
        this.HitTest(this.PointToClient(F.Cursor.Position), out row, out column);
        if (row != -1 && this.Items[row].IsFolder) {
          this.ItemMiddleClick.Invoke(this, new NavigatedEventArgs(this.Items[row], this.Items[row]));
        }
      }
    }

    private String GetFilePropertiesString(Object value) {
      var valueFA = (FileAttributes)value;
      var isArhive = (valueFA & FileAttributes.Archive) == FileAttributes.Archive;
      var isDirectory = (valueFA & FileAttributes.Directory) == FileAttributes.Directory;
      var isHidden = (valueFA & FileAttributes.Hidden) == FileAttributes.Hidden;
      var isReadOnly = (valueFA & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
      var isSystem = (valueFA & FileAttributes.System) == FileAttributes.System;
      var isTemp = (valueFA & FileAttributes.Temporary) == FileAttributes.Temporary;
      var resultString = String.Empty;

      resultString += isArhive ? "A" : "-";
      resultString += isDirectory ? "D" : "-";
      resultString += isHidden ? "H" : "-";
      resultString += isReadOnly ? "R" : "-";
      resultString += isSystem ? "S" : "-";
      resultString += isTemp ? "T" : "-";

      return resultString;
    }

    /// <summary>
    /// This is only to be used in SetSortCollumn(...)
    /// </summary>
    /// <param name="columnIndex"></param>
    /// <param name="order"></param>
    public void SetSortIcon(Int32 columnIndex, SortOrder order) {
      for (var columnNumber = 0; columnNumber <= this.Collumns.Count - 1; columnNumber++) {
        var item = new HDITEM { mask = HDITEM.Mask.Format };

        if (User32.SendMessage(this.LVHeaderHandle, MSG.HDM_GETITEM, columnNumber, ref item) == IntPtr.Zero) {
          throw new Win32Exception();
        }

        if (order != SortOrder.None && columnNumber == columnIndex) {
          switch (order) {
            case SortOrder.Ascending:
              item.fmt &= ~HDITEM.Format.SortDown;
              item.fmt |= HDITEM.Format.SortUp;
              break;

            case SortOrder.Descending:
              item.fmt &= ~HDITEM.Format.SortUp;
              item.fmt |= HDITEM.Format.SortDown;
              break;
          }
        } else {
          item.fmt &= ~HDITEM.Format.SortDown & ~HDITEM.Format.SortUp;
        }

        if (User32.SendMessage(this.LVHeaderHandle, MSG.HDM_SETITEM, columnNumber, ref item) == IntPtr.Zero) {
          throw new Win32Exception();
        }
      }
    }

    private void AutosizeColumn(Int32 index, Int32 autosizeStyle) {
      User32.SendMessage(this.LVHandle, LVM.SETCOLUMNWIDTH, index, autosizeStyle);
    }

    private Int32 _CurrentDrawIndex = -1;
    private Boolean _IsAutoScrool;

    [SecurityPermission(SecurityAction.Demand, ControlThread = true)]
    private void ProcessCustomDrawPostPaint(ref Message m, User32.NMLVCUSTOMDRAW nmlvcd, Int32 index, IntPtr hdc, IListItemEx sho, Color? textColor, LVITEMINDEX lvi) {
      try {
        if (nmlvcd.clrTextBk == -1 && nmlvcd.dwItemType == 0 && this._CurrentDrawIndex == -1) {
          //var hFont = this.Font.ToHfont();
          //Gdi32.SelectObject(hdc, hFont);
          this._CurrentDrawIndex = index;
          var iconBounds = new User32.RECT() { Left = 1 };
          User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMINDEXRECT, ref lvi, ref iconBounds);
          if (this.View != ShellViewStyle.Details && this.View != ShellViewStyle.Tile) {
            iconBounds.Bottom -= 20;
          }
          var labelBounds = new User32.RECT() { Left = 2 };
          User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMINDEXRECT, ref lvi, ref labelBounds);
          //labelBounds.Left = labelBounds.Left + 8;
          //labelBounds.Right = labelBounds.Right - 8;
          //labelBounds.Bottom = labelBounds.Bottom + 18;
          if (this.View != ShellViewStyle.Details) {
            labelBounds.Left = labelBounds.Left + 6;
            labelBounds.Right = labelBounds.Right - 12;
            labelBounds.Top = labelBounds.Top - 8;
            labelBounds.Bottom = labelBounds.Bottom - 8;
          }

          var itemBounds = new User32.RECT();
          User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMINDEXRECT, ref lvi, ref itemBounds);
          itemBounds.Bottom = itemBounds.Bottom + 18;

          //var isSelected = (User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMSTATE, index, LVIS.LVIS_SELECTED) & LVIS.LVIS_SELECTED) == LVIS.LVIS_SELECTED;
          //var isHot = (nmlvcd.nmcd.uItemState & CDIS.HOT) == CDIS.HOT;

          //if (isSelected || isHot) {
          //  var gr = Graphics.FromHdc(hdc);
          //  gr.CompositingQuality = CompositingQuality.HighSpeed;
          //  gr.InterpolationMode = InterpolationMode.NearestNeighbor;
          //  gr.SmoothingMode = SmoothingMode.HighSpeed;
          //  var brush = new SolidBrush(this.Theme.SelectionColor.ToDrawingColor());
          //  var rectSel = new Rectangle(itemBounds.X, itemBounds.Y, itemBounds.Width, itemBounds.Height);
          //  var rect = new Rectangle(itemBounds.X, itemBounds.Y, itemBounds.Width - 1, itemBounds.Height - 1);
          //  gr.FillRectangle(brush, rectSel);
          //  if (isSelected) {
          //    var pen = new Pen(this.Theme.SelectionBorderColor.ToDrawingColor());
          //    gr.DrawRectangle(pen, rect);
          //    pen.Dispose();
          //  }

          //  brush.Dispose();
          //  gr.Dispose();
          //}

          if (sho != null) {
            var cutFlag = (User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMSTATE, index, LVIS.LVIS_CUT) & LVIS.LVIS_CUT) == LVIS.LVIS_CUT;
            var labelBoundsReal = new User32.RECT() { Left = 2 };
            User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMINDEXRECT, ref lvi, ref labelBoundsReal);
            Gdi32.SetTextColor(hdc, textColor?.ToWin32Color().ToInt32() ?? this.Theme.TextColor.ToDrawingColor().ToWin32Color().ToInt32());
            //var drawFontPtr = this.Font.ToHfont();//Gdi32.GetStockObject(Gdi32.StockObjects.DEFAULT_GUI_FONT);
            //Gdi32.SelectObject(hdc, drawFontPtr);
            if (this.IconSize == 16) {
              if (this.View == ShellViewStyle.Details) {
                labelBoundsReal.Left = labelBoundsReal.Left + 3;
                labelBoundsReal.Right = labelBoundsReal.Right - 2;
              }
              this.SmallImageList.DrawIcon(hdc, index, sho, iconBounds, sho.IsHidden || cutFlag || this._CuttedIndexes.Contains(index), (nmlvcd.nmcd.uItemState & CDIS.HOT) == CDIS.HOT);
              //Gdi32.SetTextColor(hdc, (int)Color.WhiteSmoke.ToWin32Color());
              if (index != this._ItemForRename) {
                User32.DrawText(hdc, sho.DisplayName, -1, ref labelBoundsReal, User32.TextFormatFlags.EditControl | User32.TextFormatFlags.EndEllipsis | User32.TextFormatFlags.SingleLine | User32.TextFormatFlags.VCenter | User32.TextFormatFlags.NoPrefix);
              }
              if (this.View == ShellViewStyle.Details) {
                for (var i = 1; i < this.Collumns.Count; i++) {
                  var labelBoundsSubitem = new User32.RECT() { Left = 2 };
                  User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMINDEXRECT, ref lvi, ref labelBoundsSubitem);
                  var rect2 = new User32.RECT();
                  User32.SendMessage(this.LVHeaderHandle, 0x1200 + 7, i, ref rect2);

                  var subItemRect = new User32.RECT {
                    Left = rect2.Left + 10,
                    Top = labelBoundsSubitem.Top,
                    Bottom = labelBoundsSubitem.Bottom,
                    Right = rect2.Right - 8
                  };

                  var currentCollumn = this.Collumns[i];
                  var val = String.Empty;
                  var align = User32.TextFormatFlags.Default;
                  if (sho.ColumnValues.TryGetValue(currentCollumn.pkey, out var valueCached)) {

                    if (valueCached != null) {
                      if (currentCollumn.CollumnType == typeof(DateTime)) {
                        val = ((DateTime)valueCached).ToPrettyFormattedString();
                      } else if (currentCollumn.CollumnType == typeof(Int64)) {
                        align = User32.TextFormatFlags.Right;
                        val = $"{Math.Ceiling(Convert.ToDouble(valueCached.ToString()) / 1024):# ### ### ##0} KB";
                      } else if (currentCollumn.CollumnType == typeof(PerceivedType)) {
                        val = ((PerceivedType)valueCached).ToString();
                      } else if (currentCollumn.CollumnType == typeof(FileAttributes)) {
                        val = this.GetFilePropertiesString(valueCached);
                      } else {
                        val = valueCached.ToString();
                      }
                    }

                    //nmlv.item.pszText = val.Trim();
                  } else {
                    var temp = sho;
                    var isi2 = (IShellItem2)temp.ComInterface;
                    var guid = new Guid(InterfaceGuids.IPropertyStore);
                    IPropertyStore propStore = null;
                    var pk = currentCollumn.pkey;
                    //var rgKeys = new PROPERTYKEY[1];
                    //rgKeys[0] = pk;
                    isi2.GetPropertyStore(GetPropertyStoreOptions.FastPropertiesOnly, ref guid, out propStore);
                    //var res = isi2.GetPropertyStoreForKeys(ref rgKeys, 1, GetPropertyStoreOptions.FastPropertiesOnly, ref guid, out propStore);
                    var pvar = new PropVariant();
                    if (propStore != null && propStore.GetValue(ref pk, pvar) == HResult.S_OK) {
                      if (pvar.Value == null) {
                        this.SmallImageList.EnqueueSubitemsGet(Tuple.Create(sho.ItemIndex, i, pk));
                      } else {
                        if (currentCollumn.CollumnType == typeof(DateTime)) {
                          val = ((DateTime)pvar.Value).ToPrettyFormattedString();
                        } else if (currentCollumn.CollumnType == typeof(Int64)) {
                          align = User32.TextFormatFlags.Right;
                          val = $"{Math.Ceiling(Convert.ToDouble(pvar.Value.ToString()) / 1024):# ### ### ##0} KB";
                        } else if (currentCollumn.CollumnType == typeof(PerceivedType)) {
                          val = ((PerceivedType)pvar.Value).ToString();
                        } else if (currentCollumn.CollumnType == typeof(FileAttributes)) {
                          val = this.GetFilePropertiesString(pvar.Value);
                        } else {
                          val = pvar.Value.ToString();
                        }
                        //currentItem.ColumnValues.Add(pk, pvar.Value);
                        //nmlv.item.pszText = val.Trim();
                        pvar.Dispose();
                        Marshal.ReleaseComObject(propStore);
                      }
                    }
                  }
                  User32.DrawText(hdc, val, -1, ref subItemRect, align | User32.TextFormatFlags.EditControl | User32.TextFormatFlags.EndEllipsis | User32.TextFormatFlags.SingleLine | User32.TextFormatFlags.VCenter | User32.TextFormatFlags.NoPrefix);
                }
              }
              //for (int i = 1; i < this.Collumns.Count; i++) {
              //  var labelBoundsSub = new User32.RECT() { Left = 2, Top = i };
              //  User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMINDEXRECT, ref lvi, ref labelBoundsSub);
              //  labelBoundsSub.Left = labelBoundsSub.Left + 4;
              //  labelBoundsSub.Right = labelBoundsSub.Right - 4;
              //  Object valText = String.Empty;
              //  if (sho.ColumnValues.TryGetValue(this.Collumns[i].pkey, out valText)) {
              //    if (!String.IsNullOrEmpty(valText?.ToString())) {
              //      User32.DrawText(hdc, valText.ToString(), -1, ref labelBoundsSub, User32.TextFormatFlags.EditControl | User32.TextFormatFlags.EndEllipsis | User32.TextFormatFlags.CalcRect | User32.TextFormatFlags.SingleLine | User32.TextFormatFlags.VCenter);
              //    }
              //  }
              //}
            } else {
              this.LargeImageList.DrawIcon(hdc, index, sho, iconBounds, sho.IsHidden || cutFlag || this._CuttedIndexes.Contains(index), (nmlvcd.nmcd.uItemState & CDIS.HOT) == CDIS.HOT);
              //var gr = Graphics.FromHdc(hdc);
              var align = User32.TextFormatFlags.Center;
              if (this.View == ShellViewStyle.Tile) {
                align = User32.TextFormatFlags.Default | User32.TextFormatFlags.SingleLine;
                labelBoundsReal.Top = labelBoundsReal.Top + 16;
                labelBoundsReal.Left = labelBoundsReal.Left + 2;
                labelBoundsReal.Bottom = labelBoundsReal.Bottom + 16;
                labelBoundsReal.Right = labelBoundsReal.Right - 16;
              } else {
                if (labelBounds.Left <= itemBounds.Left + 16) {
                  labelBoundsReal.Left = labelBoundsReal.Left + 12;
                  labelBoundsReal.Right = labelBoundsReal.Right - 16;
                }
                labelBoundsReal.Top = labelBoundsReal.Top - 26;
                labelBoundsReal.Bottom = labelBoundsReal.Bottom - 8;
                labelBoundsReal.Left = labelBoundsReal.Left + 2;
                labelBoundsReal.Right = labelBoundsReal.Right + 1;
                labelBoundsReal.Width = labelBoundsReal.Width + 2;
              }
              //User32.DrawText(hdc, sho.DisplayName, -1, ref labelBounds, User32.TextFormatFlags.Center | User32.TextFormatFlags.EditControl |User32.TextFormatFlags.CalcRect | User32.TextFormatFlags.WordBreak | User32.TextFormatFlags.EndEllipsis);
              if (index != this._ItemForRename && this.View != ShellViewStyle.Tile) {
                var height = User32.DrawText(hdc, sho.DisplayName, -1, ref labelBoundsReal, align | User32.TextFormatFlags.EditControl | User32.TextFormatFlags.WordBreak | User32.TextFormatFlags.EndEllipsis | User32.TextFormatFlags.NoPrefix);
                labelBoundsReal.Bottom = labelBoundsReal.Top + height + 2;
              }

              sho.LabelBounds = labelBoundsReal;
            }

            if (this.View == ShellViewStyle.Tile) {
              var multiplier = 0;
              var size = new BExplorer.Shell.Interop.Size();
              Gdi32.GetTextExtentPoint32(hdc, sho.DisplayName, sho.DisplayName.Length, ref size);
              var isDoubleLine = (size.Height > nmlvcd.nmcd.rc.Width - 53) && sho.DisplayName.Contains(" ");
              var align = User32.TextFormatFlags.Default;
              if (!isDoubleLine) {
                align = User32.TextFormatFlags.Default | User32.TextFormatFlags.SingleLine;
              } else {
                labelBoundsReal.Height = labelBoundsReal.Height * 2;
              }
              User32.DrawText(hdc, sho.DisplayName, -1, ref labelBoundsReal, align | User32.TextFormatFlags.EditControl | User32.TextFormatFlags.WordBreak | User32.TextFormatFlags.EndEllipsis | User32.TextFormatFlags.NoPrefix);
              labelBounds.Left = labelBounds.Left - 2;
              labelBounds.Top = labelBounds.Top + (isDoubleLine ? 20 : 8);
              if (!sho.IsDrive && !sho.IsNetworkPath) {
                var numberOfSubItems = sho.cColumns == null ? 0 :
                  sho.cColumns.Length > 2 ? isDoubleLine ? 1 : 2 : sho.cColumns.Length;
                for (var i = 0; i < numberOfSubItems; i++) {
                  var shoCColumn = sho.cColumns[i];
                  var currentCollumn = this.AllAvailableColumns.Values.First();
                  try {
                    currentCollumn = this.AllAvailableColumns.Values.ToArray()[shoCColumn];
                  } catch (Exception ex) { }

                  var val = String.Empty;
                  if (sho.ColumnValues.TryGetValue(currentCollumn.pkey, out var valueCached)) {

                    if (valueCached != null) {
                      if (currentCollumn.CollumnType == typeof(DateTime)) {
                        val = ((DateTime)valueCached).ToPrettyFormattedString();
                      } else if (currentCollumn.CollumnType == typeof(Int64)) {
                        val = ShlWapi.StrFormatByteSize(Convert.ToInt64(valueCached.ToString()));
                      } else if (currentCollumn.CollumnType == typeof(PerceivedType)) {
                        val = ((PerceivedType)valueCached).ToString();
                      } else if (currentCollumn.CollumnType == typeof(FileAttributes)) {
                        val = this.GetFilePropertiesString(valueCached);
                      } else {
                        val = valueCached.ToString();
                      }
                    }

                    //nmlv.item.pszText = val.Trim();
                  } else {
                    var temp = sho;
                    var isi2 = (IShellItem2)temp.ComInterface;
                    var guid = new Guid(InterfaceGuids.IPropertyStore);
                    IPropertyStore propStore = null;
                    var pk = currentCollumn.pkey;
                    //var rgKeys = new PROPERTYKEY[1];
                    //rgKeys[0] = pk;
                    isi2.GetPropertyStore(GetPropertyStoreOptions.FastPropertiesOnly, ref guid, out propStore);
                    //var res = isi2.GetPropertyStoreForKeys(ref rgKeys, 1, GetPropertyStoreOptions.FastPropertiesOnly, ref guid, out propStore);
                    var pvar = new PropVariant();
                    if (propStore != null && propStore.GetValue(ref pk, pvar) == HResult.S_OK) {
                      if (pvar.Value == null) {
                        this.SmallImageList.EnqueueSubitemsGet(Tuple.Create(sho.ItemIndex, shoCColumn, pk));
                      } else {
                        if (currentCollumn.CollumnType == typeof(DateTime)) {
                          val = ((DateTime)pvar.Value).ToPrettyFormattedString();
                        } else if (currentCollumn.CollumnType == typeof(Int64)) {
                          val = ShlWapi.StrFormatByteSize(Convert.ToInt64(pvar.Value.ToString()));
                        } else if (currentCollumn.CollumnType == typeof(PerceivedType)) {
                          val = ((PerceivedType)pvar.Value).ToString();
                        } else if (currentCollumn.CollumnType == typeof(FileAttributes)) {
                          val = this.GetFilePropertiesString(pvar.Value);
                        } else {
                          val = pvar.Value.ToString();
                        }

                        //currentItem.ColumnValues.Add(pk, pvar.Value);
                        //nmlv.item.pszText = val.Trim();
                        pvar.Dispose();
                        Marshal.ReleaseComObject(propStore);
                      }
                    }
                  }

                  multiplier++;
                  labelBoundsReal.Top = labelBoundsReal.Top + 20;
                  labelBoundsReal.Bottom = labelBoundsReal.Top + 15;
                  if (!String.IsNullOrEmpty(val)) {
                    Gdi32.SetTextColor(hdc,
                      textColor == null ? (int)Color.Cornsilk.ToWin32Color() : (int)textColor.Value.ToWin32Color());
                    User32.DrawText(hdc, val, -1, ref labelBoundsReal,
                      User32.TextFormatFlags.EditControl | User32.TextFormatFlags.EndEllipsis |
                      User32.TextFormatFlags.SingleLine | User32.TextFormatFlags.VCenter |
                      User32.TextFormatFlags.NoPrefix);
                  }
                }
              }
            }

            if (!sho.IsInitialised) {
              sho.IsInitialised = true;
            }
            sho.Dispose();
            //Gdi32.DeleteObject(drawFontPtr);
          }

          var ind = -1;
          this.CurrentlyUpdatingItems.TryRemove(index, out ind);
          m.Result = (IntPtr)CustomDraw.CDRF_SKIPDEFAULT;
        } else {
          m.Result = IntPtr.Zero;
        }

        this._CurrentDrawIndex = -1;
      } catch (Exception ex) {
        // Clean up the current item since it appears is missing or broken.
        this.QueueDeleteItem(sho, true);
      }
    }

    public void ProcessHeaderCustomDraw(ref Message m) {
      var nmcd = (User32.NMCUSTOMDRAW)m.GetLParam(typeof(User32.NMCUSTOMDRAW));
      switch (nmcd.dwDrawStage) {
        case 0x00000003:
          var t = 1;
          break;
        case CustomDraw.CDDS_PREPAINT:
          //var gr2 = Graphics.FromHdc(nmcd.hdc);
          //var textSize = new Size();
          //var brush2 = new SolidBrush(this.Theme.BackgroundColor.ToDrawingColor());
          //gr2.CompositingQuality = CompositingQuality.HighSpeed;
          //gr2.InterpolationMode = InterpolationMode.NearestNeighbor;
          //gr2.SmoothingMode = SmoothingMode.HighSpeed;
          ////gr.DrawLine(new Pen(this.Theme.SelectionBorderColor.ToDrawingColor()), nmlvcd.rcText.X + textSize.Height + 8, nmlvcd.rcText.Y + (nmlvcd.rcText.Height / 2) + 1, nmlvcd.rcText.Right - 20, nmlvcd.rcText.Y + (nmlvcd.rcText.Height / 2) + 1);
          //gr2.FillRectangle(brush2, new Rectangle(this.ClientRectangle.X, this.ClientRectangle.Y, this.ClientRectangle.Width, 22));
          //brush2.Dispose();
          //gr2.Dispose();
          //var grh = Graphics.FromHwnd(this.LVHeaderHandle);
          //grh.FillRectangle(Brushes.Aquamarine, new Rectangle(this.ClientRectangle.X, this.ClientRectangle.Y, this.ClientRectangle.Width, 22));
          //grh.Dispose();
          m.Result = (IntPtr)(CustomDraw.CDRF_NOTIFYITEMDRAW | CustomDraw.CDRF_NEWFONT);
          break;
        case CustomDraw.CDDS_ITEMPREPAINT:
          //Gdi32.SetTextColor(nmcd.hdc, (int)Color.Green.ToWin32Color());
          //var gr = Graphics.FromHdc(nmcd.hdc);
          //var textSize = new Size();
          //var brush = new SolidBrush(this.Theme.BackgroundColor.ToDrawingColor());
          //gr.CompositingQuality = CompositingQuality.HighSpeed;
          //gr.InterpolationMode = InterpolationMode.NearestNeighbor;
          //gr.SmoothingMode = SmoothingMode.HighSpeed;
          ////gr.DrawLine(new Pen(this.Theme.SelectionBorderColor.ToDrawingColor()), nmlvcd.rcText.X + textSize.Height + 8, nmlvcd.rcText.Y + (nmlvcd.rcText.Height / 2) + 1, nmlvcd.rcText.Right - 20, nmlvcd.rcText.Y + (nmlvcd.rcText.Height / 2) + 1);
          //gr.FillRectangle(brush, new Rectangle(nmcd.rc.X, nmcd.rc.Y, nmcd.rc.Width, nmcd.rc.Height));
          //brush.Dispose();
          //gr.Dispose();
          m.Result = (IntPtr)(CustomDraw.CDRF_NEWFONT | CustomDraw.CDRF_NOTIFYPOSTPAINT | CustomDraw.CDRF_NOTIFYSUBITEMDRAW);
          break;
        case CustomDraw.CDDS_ITEMPREPAINT | CustomDraw.CDDS_SUBITEM:
          m.Result = IntPtr.Zero;
          break;
        case CustomDraw.CDDS_ITEMPOSTPAINT:
          //var gr = Graphics.FromHdc(nmcd.hdc);
          ////var textSize = new Size();
          //var brush = new SolidBrush(this.Theme.BackgroundColor.ToDrawingColor());
          //gr.CompositingQuality = CompositingQuality.HighSpeed;
          //gr.InterpolationMode = InterpolationMode.NearestNeighbor;
          //gr.SmoothingMode = SmoothingMode.HighSpeed;
          ////gr.DrawLine(new Pen(this.Theme.SelectionBorderColor.ToDrawingColor()), nmlvcd.rcText.X + textSize.Height + 8, nmlvcd.rcText.Y + (nmlvcd.rcText.Height / 2) + 1, nmlvcd.rcText.Right - 20, nmlvcd.rcText.Y + (nmlvcd.rcText.Height / 2) + 1);
          //gr.FillRectangle(brush, new Rectangle(nmcd.rc.X, nmcd.rc.Y, nmcd.rc.Width, nmcd.rc.Height));
          //brush.Dispose();
          //gr.Dispose();
          m.Result = (IntPtr)CustomDraw.CDRF_SKIPDEFAULT;
          break;
      }
    }

    [SecurityPermission(SecurityAction.Demand, ControlThread = true)]
    private void ProcessCustomDraw(ref Message m, ref NMHDR nmhdr) {
      User32.SendMessage(this.LVHandle, 296, User32.MAKELONG(1, 1), 0);
      var nmlvcd = (User32.NMLVCUSTOMDRAW)m.GetLParam(typeof(User32.NMLVCUSTOMDRAW));
      var index = (Int32)nmlvcd.nmcd.dwItemSpec;
      var hdc = nmlvcd.nmcd.hdc;
      var lvi = default(LVITEMINDEX);
      lvi.iItem = index;
      lvi.iGroup = this.GetGroupIndex(index);


      var sho = this.Items.Count > index ? this.Items[index] : null;

      Color? textColor = null;


      //var style = User32.GetWindowLong(this.LVHandle, -16);
      //User32.SetWindowLong(this.LVHandle, -16, (style & ~0x00200000L));

      if (nmlvcd.dwItemType == 2) {
        if (nmlvcd.nmcd.rc.Left == this.ClientRectangle.Width - 17) {

        }
        //var scrollInfo = this.GetScrollPosition();
        //if (scrollInfo.nMax == 0 && scrollInfo.nPage == 0 || (scrollInfo.nPage == 1 && scrollInfo.fMask > 0) || scrollInfo.nPage > scrollInfo.nMax) {
        //  this._HasScrollbar = false;
        //  this.VScroll.Visible = false;

        //  //User32.MoveWindow(this.LVHandle, 0, 0, this.ClientRectangle.Width, this.ClientRectangle.Height, false);
        //} else {
        //  if (scrollInfo.nPage > 1) {

        //    this._HasScrollbar = true;


        //    this.VScroll.Visible = true;
        //    this.VScroll.LargeChange = (int)scrollInfo.nPage;
        //    this.VScroll.Minimum = 0;
        //    this.VScroll.Maximum = (int)(scrollInfo.nMax + 1);
        //    this.VScroll.SmallChange = this.View == ShellViewStyle.Details && !this.IsGroupsEnabled ? 1 : this.View == ShellViewStyle.Details ? 18 : this.IconSize + 50;
        //    //this.VScroll.Value = scrollInfo.nPos;
        //    //User32.MoveWindow(this.LVHandle, 0, 0, this.ClientRectangle.Width, this.ClientRectangle.Height, false);
        //  }
        //}

      }

      if (nmlvcd.dwItemType == 1) {
        if (nmlvcd.nmcd.dwDrawStage == CustomDraw.CDDS_PREPAINT) {

          var header = this.Groups[index].Header;
          var state = User32.SendMessage(this.LVHandle, MSG.LVM_GETGROUPSTATE, (uint)index, 0x00000001);
          nmlvcd.rcText.Left = nmlvcd.rcText.Left + 5;
          //nmlvcd.rcText.Right = nmlvcd.rcText.Right - 5;
          //nmlvcd.rcText.Right = nmlvcd.rcText.Right - 20;
          //Marshal.StructureToPtr(nmlvcd, m.LParam, false);
          var gr = Graphics.FromHdc(hdc);
          var textSize = new BExplorer.Shell.Interop.Size();
          Gdi32.GetTextExtentPoint32(hdc, header, header.Length, ref textSize);
          var brush = new SolidBrush(this.Theme.SelectionColor.ToDrawingColor());
          gr.CompositingQuality = CompositingQuality.HighSpeed;
          gr.InterpolationMode = InterpolationMode.NearestNeighbor;
          gr.SmoothingMode = SmoothingMode.HighSpeed;
          if (nmlvcd.rcText.Width < this.ClientRectangle.Width) {
            //this.VScroll.Visible = true;
          }

          var hitPoint = this.PointToClient(F.Cursor.Position);
          var rectangle = new Rectangle(nmlvcd.rcText.Right - 40, nmlvcd.rcText.Y, 60, nmlvcd.rcText.Height);
          this._IsOverGroup = rectangle.Contains(hitPoint);

          var lvHitTestInfo = new LVHITTESTINFO();
          lvHitTestInfo.pt.x = hitPoint.X;
          lvHitTestInfo.pt.y = hitPoint.Y;
          if (User32.SendMessage(this.LVHandle, 0x1000 + 57, -1, ref lvHitTestInfo) != -1) {
            if ((lvHitTestInfo.flags & ListViewHitTestFlag.LVHT_EX_GROUP_HEADER) != 0 && lvHitTestInfo.iGroup == index) {
              gr.FillRectangle(brush, new Rectangle(nmlvcd.rcText.X, nmlvcd.rcText.Y, nmlvcd.rcText.Width, nmlvcd.rcText.Height));
            }
          }
          var pen = new Pen(Color.FromArgb(150, 128, 128, 128));
          var arrowPen = new Pen(this._IsOverGroup ? Color.LightGray : this.Theme.SelectionBorderColor.ToDrawingColor(), 2);
          gr.DrawLine(pen, nmlvcd.rcText.X + textSize.Height + 8, nmlvcd.rcText.Y + (nmlvcd.rcText.Height / 2) + 1, nmlvcd.rcText.Right - 20, nmlvcd.rcText.Y + (nmlvcd.rcText.Height / 2) + 1);

          gr.DrawArrowHead(arrowPen, new PointF(nmlvcd.rcText.Right - 38 + 60 / 2f, state == 1 ? nmlvcd.rcText.Y + 8 : nmlvcd.rcText.Y + 12), 0, state == 1 ? -4 : 4, 1);
          arrowPen.Dispose();
          brush.Dispose();
          pen.Dispose();
          gr.Dispose();

          Gdi32.SetTextColor(hdc, (int)Color.WhiteSmoke.ToWin32Color());

          User32.DrawText(hdc, header, -1, ref nmlvcd.rcText, User32.TextFormatFlags.SingleLine | User32.TextFormatFlags.VCenter | User32.TextFormatFlags.NoPrefix);
        }

        m.Result = (IntPtr)CustomDraw.CDRF_SKIPDEFAULT;
      } else {
        this._IsOverGroup = false;
        switch (nmlvcd.nmcd.dwDrawStage) {
          case CustomDraw.CDDS_PREPAINT:
            Marshal.StructureToPtr(nmlvcd, m.LParam, true);
            if (nmlvcd.dwItemType == 2) { }
            if (nmlvcd.dwItemType != 1) {
              m.Result = (IntPtr)(CustomDraw.CDRF_NEWFONT | CustomDraw.CDRF_NOTIFYITEMDRAW | CustomDraw.CDRF_NOTIFYPOSTPAINT | 0x40);
            } else {
              m.Result = (IntPtr)CustomDraw.CDRF_DODEFAULT;
            }
            break;

          case CustomDraw.CDDS_POSTPAINT:
            m.Result = (IntPtr)CustomDraw.CDRF_SKIPDEFAULT;
            break;

          case CustomDraw.CDDS_ITEMPREPAINT:
            if (nmlvcd.clrTextBk == -1 && nmlvcd.dwItemType == 0) {
              if (sho != null && this.LVItemsColorCodes != null && this.LVItemsColorCodes.Count > 0 && !String.IsNullOrEmpty(sho.Extension)) {
                var extItemsAvailable = this.LVItemsColorCodes.Any(c => c.ExtensionList.ToLowerInvariant().Contains(sho.Extension.ToLowerInvariant()));
                if (extItemsAvailable) {
                  var color = this.LVItemsColorCodes?.SingleOrDefault(c => c.ExtensionList != null && sho.Extension != null && c.ExtensionList.ToLowerInvariant().Contains(sho.Extension.ToLowerInvariant()))?.TextColor;
                  textColor = Color.FromArgb(color.Value.A, color.Value.R, color.Value.G, color.Value.B);
                }
              }
              if ((nmlvcd.nmcd.uItemState & CDIS.DROPHILITED) == CDIS.DROPHILITED && index != this._LastDropHighLightedItemIndex) {
                nmlvcd.nmcd.uItemState = CDIS.DEFAULT;
              }

              if (index == this._LastDropHighLightedItemIndex) {
                nmlvcd.nmcd.uItemState |= CDIS.DROPHILITED;
              }

              //var rr = new User32.RECT(new Rectangle(nmlvcd.nmcd.rc.X + 4, nmlvcd.nmcd.rc.Y + 4, nmlvcd.nmcd.rc.Width - 8, nmlvcd.nmcd.rc.Height - 8));
              //nmlvcd.nmcd.rc = rr;
              //Marshal.StructureToPtr(nmlvcd, m.LParam, true);
              var itemBounds = nmlvcd.nmcd.rc;//new User32.RECT();
              //User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMINDEXRECT, ref lvi, ref itemBounds);

              if (this.IconSize == 16) {
                itemBounds.Left += 4;
                itemBounds.Right -= 4;
                itemBounds.Top += 2;
                itemBounds.Bottom -= 2;
              }

              var isSelected = (User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMSTATE, index, LVIS.LVIS_SELECTED) & LVIS.LVIS_SELECTED) == LVIS.LVIS_SELECTED;
              var isFocused = (User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMSTATE, index, LVIS.LVIS_FOCUSED) & LVIS.LVIS_FOCUSED) == LVIS.LVIS_FOCUSED;
              var isHot = (nmlvcd.nmcd.uItemState & CDIS.HOT) == CDIS.HOT || (nmlvcd.nmcd.uItemState & CDIS.DROPHILITED) == CDIS.DROPHILITED;

              var labelBoundsReal = new User32.RECT() { Left = 2 };
              User32.SendMessage(this.LVHandle, MSG.LVM_GETITEMINDEXRECT, ref lvi, ref labelBoundsReal);
              if (this.View == ShellViewStyle.Tile) {
                labelBoundsReal.Top = labelBoundsReal.Top + 7;
                labelBoundsReal.Left = labelBoundsReal.Left + 2;
              } else {
                if (labelBoundsReal.Left <= itemBounds.Left + 16) {
                  labelBoundsReal.Left = labelBoundsReal.Left + 12;
                  labelBoundsReal.Right = labelBoundsReal.Right - 16;
                }
                labelBoundsReal.Top = labelBoundsReal.Top - 24;
                labelBoundsReal.Bottom = labelBoundsReal.Bottom - 8;
              }

              var labelBottom = labelBoundsReal.Bottom;
              labelBoundsReal.Width = itemBounds.Width - 35;

              var height = User32.DrawText(hdc, sho.DisplayName, -1, ref labelBoundsReal, User32.TextFormatFlags.CalcRect | User32.TextFormatFlags.Center | User32.TextFormatFlags.EditControl | User32.TextFormatFlags.WordBreak | User32.TextFormatFlags.EndEllipsis | User32.TextFormatFlags.NoPrefix);
              //labelBottom = labelBoundsReal.Top + height + 2;
              if (labelBottom > labelBoundsReal.Bottom && labelBottom - labelBoundsReal.Bottom > 2) {
                labelBottom = labelBoundsReal.Bottom;
              }


              if (isSelected || isHot) {
                var rectSel = new Rectangle(itemBounds.X + (this.View == ShellViewStyle.Details ? 2 : 6), itemBounds.Y + (this.View == ShellViewStyle.Details ? 2 : 4), itemBounds.Width - (this.View == ShellViewStyle.Details ? 4 : 12), this.View == ShellViewStyle.Details || this.View == ShellViewStyle.Tile ? itemBounds.Height - 6 : labelBottom - itemBounds.Top);
                //var rect = new Rectangle(itemBounds.X + (this.View == ShellViewStyle.Details ? 2 : 6), itemBounds.Y + (this.View == ShellViewStyle.Details ? 2 : 4), itemBounds.Width - (this.View == ShellViewStyle.Details ? 4 : 12), itemBounds.Height - (this.View == ShellViewStyle.Details ? 4 : 8));
                //Gdi32.SetBoundsRect(User32.GetDC(this.LVHandle), new User32.RECT(rectSel), Gdi32.DCB.DCB_SET);
                var gr = Graphics.FromHdc(hdc);
                gr.CompositingQuality = CompositingQuality.HighQuality;
                gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
                gr.SmoothingMode = SmoothingMode.AntiAlias;
                //var brush = new SolidBrush(isHot ? isSelected ? this.Theme.SelectionFocusedColor.ToDrawingColor() : this.Theme.MouseOverColor.ToDrawingColor() :
                //  isFocused ? this.Theme.SelectionFocusedColor.ToDrawingColor() : this.Theme.SelectionColor.ToDrawingColor());
                var brush = new SolidBrush(this.Theme.MouseOverColor.ToDrawingColor());
                //if (!isSelected || this.View == ShellViewStyle.Details) {
                gr.FillRoundedRectangle(brush, rectSel, this.View == ShellViewStyle.Details ? 3 : this.View == ShellViewStyle.Tile ? 3 : 4);
                //}

                if (this.View == ShellViewStyle.Details && isSelected) {
                  var selectedBrush = new SolidBrush(this.Theme.SelectionBorderColor.ToDrawingColor());
                  gr.FillRoundedRectangle(selectedBrush, new Rectangle(itemBounds.X + 1, itemBounds.Y + 8, 2, itemBounds.Height - 18), 1);
                  selectedBrush.Dispose();
                }

                if (isSelected && this.View != ShellViewStyle.Details) {
                  var pen = new Pen(this.Theme.SelectionBorderColor.ToDrawingColor(), 2);
                  gr.DrawRoundedRectangle(pen, rectSel, this.View == ShellViewStyle.Tile ? 3 : 4);
                  pen.Dispose();
                }
                brush.Dispose();
                gr.Dispose();
                if (isSelected) {
                  var controlRect = this.Bounds;
                  //User32.InvalidateRect(this.LVHandle, ref controlRect, false);
                }
              }

              nmlvcd.clrFace = -1;
              //nmlvcd.nmcd.rc.Height += 20;

              //Marshal.StructureToPtr(nmlvcd, m.LParam, true);
              this.ProcessCustomDrawPostPaint(ref m, nmlvcd, index, hdc, sho, textColor, lvi);
              m.Result = (IntPtr)CustomDraw.CDRF_SKIPDEFAULT;
            } else {
              m.Result = (IntPtr)CustomDraw.CDRF_DODEFAULT;
            }
            break;

          case CustomDraw.CDDS_ITEMPREPAINT | CustomDraw.CDDS_SUBITEM:
            if (sho != null && this.LVItemsColorCodes != null && this.LVItemsColorCodes.Count > 0 && !String.IsNullOrEmpty(sho.Extension)) {
              var extItemsAvailable = this.LVItemsColorCodes.Any(c => c.ExtensionList.ToLowerInvariant().Contains(sho.Extension.ToLowerInvariant()));
              if (extItemsAvailable) {
                var color = this.LVItemsColorCodes?.SingleOrDefault(c => c.ExtensionList != null && sho.Extension != null && c.ExtensionList.ToLowerInvariant().Contains(sho.Extension.ToLowerInvariant()))?.TextColor;
                textColor = Color.FromArgb(color.Value.A, color.Value.R, color.Value.G, color.Value.B);
              }
            }
            if (textColor == null) {
              m.Result = (IntPtr)CustomDraw.CDRF_DODEFAULT;
            } else {
              nmlvcd.clrText = (UInt32)ColorTranslator.ToWin32(textColor.Value);
              Marshal.StructureToPtr(nmlvcd, m.LParam, false);
              m.Result = (IntPtr)(CustomDraw.CDRF_NEWFONT | CustomDraw.CDRF_NOTIFYPOSTPAINT | 0x40);
            }
            break;

          case CustomDraw.CDDS_ITEMPOSTPAINT:
            //this.ProcessCustomDrawPostPaint(ref m, nmlvcd, index, hdc, sho, textColor, lvi);
            break;
        }
      }
    }

    #endregion Private Methods
  }
}