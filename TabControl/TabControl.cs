﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BExplorer.Shell;
using BExplorer.Shell._Plugin_Interfaces;

namespace Wpf.Controls {
  public delegate void PreviewSelectionChangedEventHandler(object p_oSender, PreviewSelectionChangedEventArgs p_eEventArgs);

  public class PreviewSelectionChangedEventArgs {
    internal PreviewSelectionChangedEventArgs(IList p_lAddedItems, IList p_lRemovedItems) {
      this.AddedItems = p_lAddedItems;
      this.RemovedItems = p_lRemovedItems;
    }
    public bool Cancel;
    public IList AddedItems { get; private set; }
    public IList RemovedItems { get; private set; }
  }

  [TemplatePart(Name = "PART_DropDown", Type = typeof(ToggleButton))]
  [TemplatePart(Name = "PART_RepeatLeft", Type = typeof(RepeatButton))]
  [TemplatePart(Name = "PART_RepeatRight", Type = typeof(RepeatButton))]
  [TemplatePart(Name = "PART_NewTabButton", Type = typeof(ButtonBase))]
  [TemplatePart(Name = "PART_ScrollViewer", Type = typeof(ScrollViewer))]
  public class TabControl : System.Windows.Controls.TabControl {

    /// <summary>List of tabs that can be reopened by the user</summary>
    public List<NavigationLog> ReopenableTabs = new List<NavigationLog>();

    #region Properties

    public event PreviewSelectionChangedEventHandler PreviewSelectionChanged;
    private int? m_lLastSelectedIndex;
    public string StartUpLocation = KnownFolders.Libraries.ParsingName;
    public DragEventHandler newt_DragEnter, newt_DragOver, newt_Drop, newt_Leave;
    public GiveFeedbackEventHandler newt_GiveFeedback;
    public MouseEventHandler newt_PreviewMouseMove;
    public MouseButtonEventHandler newt_PreviewMouseDown;
    public Boolean ShouldNavigateOnSelection { get; set; }

    /// <summary>An <see cref="Action">Action</see> that is fired after a new tab is created</summary> 
    public Action ConstructMoveToCopyToMenu;

    public bool isGoingBackOrForward;

    [Obsolete("Assigned but not used")]
    public bool IsSelectionHandled = false;


    //public Wpf.Controls.TabItem CurrentTabItem;

    // TemplatePart controls
    private ToggleButton _toggleButton;
    private ButtonBase _addNewButton;

  /// <summary>Determines if the underlying list if fixed in size</summary>
  private bool IsFixedSize {
      get {
        var items = GetItems() as IList;
        return items == null || items.IsFixedSize;
      }
    }


    public bool IsUsingItemsSource {
      get { return (bool)GetValue(IsUsingItemsSourceProperty); }
      private set { SetValue(IsUsingItemsSourcePropertyKey, value); }
    }

    /// <summary>
    /// Allow the User to Add New TabItems
    /// </summary>
    public bool AllowAddNew {
      get { return (bool)GetValue(AllowAddNewProperty); }
      set { SetValue(AllowAddNewProperty, value); }
    }

    /// <summary>The location where new tabs start at</summary>
    public String DefaultTabPath {
      get { return (String)GetValue(DefaultTabPathProperty); }
      set { SetValue(DefaultTabPathProperty, value); }
    }


    public Brush TabItemNormalBackground {
      get { return (Brush)GetValue(TabItemNormalBackgroundProperty); }
      set { SetValue(TabItemNormalBackgroundProperty, value); }
    }

    public Brush NotTabItemNormalBackground {
      get { return (Brush)GetValue(NotTabItemNormalBackgroundProperty); }
      set { SetValue(NotTabItemNormalBackgroundProperty, value); }
    }


    public Brush TabItemMouseOverBackground {
      get { return (Brush)GetValue(TabItemMouseOverBackgroundProperty); }
      set { SetValue(TabItemMouseOverBackgroundProperty, value); }
    }


    public Brush TabItemSelectedBackground {
      get { return (Brush)GetValue(TabItemSelectedBackgroundProperty); }
      set { SetValue(TabItemSelectedBackgroundProperty, value); }
    }

    /// <summary>
    /// Allow the User to Delete TabItems
    /// </summary>
    public bool AllowDelete {
      get { return (bool)GetValue(AllowDeleteProperty); }
      set { SetValue(AllowDeleteProperty, value); }
    }

    /// <summary>
    /// Set new Header as the current selection
    /// </summary>
    public bool SelectNewTabOnCreate {
      get { return (bool)GetValue(SelectNewTabOnCreateProperty); }
      set { SetValue(SelectNewTabOnCreateProperty, value); }
    } //TODO: Find out if we really need this

    /// <summary>
    /// Determines where new TabItems are added to the TabControl
    /// </summary>
    /// <remarks>
    ///     Set to true (default) to add all new Tabs to the end of the TabControl
    ///     Set to False to insert new tabs after the current selection
    /// </remarks>
    public bool AddNewTabToEnd {
      get { return (bool)GetValue(AddNewTabToEndProperty); }
      set { SetValue(AddNewTabToEndProperty, value); }
    }


    /// <summary>
    /// defines the Minimum width of a Header
    /// </summary>
    [DefaultValue(20.0)]
    [Category("Layout")]
    [Description("Gets or Sets the minimum Width Constraint shared by all Items in the Control, individual child elements MinWidth property will overide this property")]
    public double TabItemMinWidth {
      get { return (double)GetValue(TabItemMinWidthProperty); }
      set { SetValue(TabItemMinWidthProperty, value); }
    }

    /// <summary>
    /// defines the Minimum height of a Header
    /// </summary>
    [DefaultValue(20.0)]
    [Category("Layout")]
    [Description("Gets or Sets the minimum Height Constraint shared by all Items in the Control, individual child elements MinHeight property will override this value")]
    public double TabItemMinHeight {
      get { return (double)GetValue(TabItemMinHeightProperty); }
      set { SetValue(TabItemMinHeightProperty, value); }
    }


    /// <summary>
    /// defines the Maximum width of a Header
    /// </summary>
    [DefaultValue(double.PositiveInfinity)]
    [Category("Layout")]
    [Description("Gets or Sets the maximum width Constraint shared by all Items in the Control, individual child elements MaxWidth property will override this value")]
    public double TabItemMaxWidth {
      get { return (double)GetValue(TabItemMaxWidthProperty); }
      set { SetValue(TabItemMaxWidthProperty, value); }
    }

    /// <summary>
    /// defines the Maximum width of a Header
    /// </summary>
    [DefaultValue(double.PositiveInfinity)]
    [Category("Layout")]
    [Description("Gets or Sets the maximum height Constraint shared by all Items in the Control, individual child elements MaxHeight property will override this value")]
    public double TabItemMaxHeight {
      get { return (double)GetValue(TabItemMaxHeightProperty); }
      set { SetValue(TabItemMaxHeightProperty, value); }
    }


    #endregion Properties/Locals

    #region Static

    public static readonly DependencyProperty TabItemNormalBackgroundProperty = DependencyProperty.Register("TabItemNormalBackground", typeof(Brush), typeof(TabControl), new UIPropertyMetadata(null));
    public static readonly DependencyProperty NotTabItemNormalBackgroundProperty = DependencyProperty.Register("NotTabItemNormalBackground", typeof(Brush), typeof(TabControl), new UIPropertyMetadata(null));
    public static readonly DependencyProperty TabItemMouseOverBackgroundProperty = DependencyProperty.Register("TabItemMouseOverBackground", typeof(Brush), typeof(TabControl), new UIPropertyMetadata(null));
    public static readonly DependencyProperty TabItemSelectedBackgroundProperty = DependencyProperty.Register("TabItemSelectedBackground", typeof(Brush), typeof(TabControl), new UIPropertyMetadata(null));
    public static readonly DependencyPropertyKey IsUsingItemsSourcePropertyKey = DependencyProperty.RegisterReadOnly("IsUsingItemsSource", typeof(bool), typeof(TabControl), new UIPropertyMetadata(false));
    public static readonly DependencyProperty IsUsingItemsSourceProperty = IsUsingItemsSourcePropertyKey.DependencyProperty;
    public static readonly DependencyProperty AllowAddNewProperty = DependencyProperty.Register("AllowAddNew", typeof(bool), typeof(TabControl), new FrameworkPropertyMetadata(true, new PropertyChangedCallback(OnAllowAddNewChanged), OnCoerceAllowAddNewCallback));
    public static readonly DependencyProperty DefaultTabPathProperty = DependencyProperty.Register("DefaultTabPath", typeof(String), typeof(TabControl), new UIPropertyMetadata(null));
    public static readonly DependencyProperty AllowDeleteProperty = DependencyProperty.Register("AllowDelete", typeof(bool), typeof(TabControl), new FrameworkPropertyMetadata(true, new PropertyChangedCallback(OnAllowDeleteChanged), OnCoerceAllowDeleteNewCallback));
    public static readonly DependencyProperty SelectNewTabOnCreateProperty = DependencyProperty.Register("SelectNewTabOnCreate", typeof(bool), typeof(TabControl), new UIPropertyMetadata(true));
    public static readonly DependencyProperty AddNewTabToEndProperty = DependencyProperty.Register("AddNewTabToEnd", typeof(bool), typeof(TabControl), new UIPropertyMetadata(true));
    public static readonly DependencyProperty TabItemMinWidthProperty = DependencyProperty.Register("TabItemMinWidth", typeof(double), typeof(TabControl), new FrameworkPropertyMetadata(20.0, new PropertyChangedCallback(OnMinMaxChanged), CoerceMinWidth));
    public static readonly DependencyProperty TabItemMinHeightProperty = DependencyProperty.Register("TabItemMinHeight", typeof(double), typeof(TabControl), new FrameworkPropertyMetadata(20.0, new PropertyChangedCallback(OnMinMaxChanged), CoerceMinHeight));
    public static readonly DependencyProperty TabItemMaxWidthProperty = DependencyProperty.Register("TabItemMaxWidth", typeof(double), typeof(TabControl), new FrameworkPropertyMetadata(double.PositiveInfinity, new PropertyChangedCallback(OnMinMaxChanged), CoerceMaxWidth));
    public static readonly DependencyProperty TabItemMaxHeightProperty = DependencyProperty.Register("TabItemMaxHeight", typeof(double), typeof(TabControl), new FrameworkPropertyMetadata(double.PositiveInfinity, new PropertyChangedCallback(OnMinMaxChanged), CoerceMaxHeight));


    private static void OnAllowAddNewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((TabControl)d).SetAddNewButtonVisibility(); }
    private static object OnCoerceAllowAddNewCallback(DependencyObject d, object basevalue) => ((TabControl)d).OnCoerceAllowAddNewCallback(basevalue);
    private static object OnCoerceAllowDeleteNewCallback(DependencyObject d, object basevalue) => ((TabControl)d).OnCoerceAllowDeleteCallback(basevalue);


    private static void OnAllowDeleteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      TabControl tc = (TabControl)d;
      tc.SetTabItemsCloseButtonVisibility();
    }

    private static object CoerceMinWidth(DependencyObject d, object value) {
      TabControl tc = (TabControl)d;
      double newValue = (double)value;

      if (newValue > tc.TabItemMaxWidth) return tc.TabItemMaxWidth;
      return newValue > 0 ? newValue : 0;
    }

    private static object CoerceMinHeight(DependencyObject d, object value) {
      TabControl tc = (TabControl)d;
      double newValue = (double)value;

      if (newValue > tc.TabItemMaxHeight) return tc.TabItemMaxHeight;
      return newValue > 0 ? newValue : 0;
    }

    private static object CoerceMaxWidth(DependencyObject d, object value) {
      TabControl tc = (TabControl)d;
      double newValue = (double)value;
      return newValue < tc.TabItemMinWidth ? tc.TabItemMinWidth : newValue;
    }

    private static object CoerceMaxHeight(DependencyObject d, object value) {
      TabControl tc = (TabControl)d;
      double newValue = (double)value;
      return newValue < tc.TabItemMinHeight ? tc.TabItemMinHeight : newValue;
    }

    /// <summary>
    /// OnMinMaxChanged callback responds to any of the Min/Max dependency properties changing
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private static void OnMinMaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      TabControl tc = (TabControl)d;
      if (tc.Template == null) return;

      foreach (TabItem child in tc.InternalChildren()) {
        if (child != null)
          child.Dimension = null;
      }

      TabPanel tp = Helper.FindVirtualizingTabPanel(tc);
      tp?.InvalidateMeasure();
    }

    /// <summary>
    /// OnTabStripPlacementChanged property callback
    /// </summary>
    /// <remarks>
    ///     We need to supplement the base implementation with this method as the base method does not work when
    ///     we are using virtualization in the tabpanel, it only updates visible items
    /// </remarks>
    private static void OnTabStripPlacementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      foreach (TabItem tabItem in ((TabControl)d).InternalChildren()) {
        if (tabItem != null) {
          tabItem.Dimension = null;
          tabItem.CoerceValue(System.Windows.Controls.TabItem.TabStripPlacementProperty);
        }
      }
    }

    #endregion Dependency properties

    #region Event Stuff

    //public event EventHandler<CancelEventArgs> TabItemAdding;
    //public event EventHandler<TabItemEventArgs> TabItemAdded;
    //public event EventHandler<NewTabItemEventArgs> NewTabItem;
    public event EventHandler<TabClickEventArgs> OnTabClicked;
    /*
    private event EventHandler<TabItemCancelEventArgs> TabItemClosing;
    private event EventHandler<TabItemEventArgs> TabItemClosed;
    */

    /// <summary>
    /// Raises the <see cref="OnTabClicked"/> event if and only if <paramref name="tab"/> is not nothing
    /// </summary>
    /// <param name="tab">The tab you want to raise the event on</param>
    public void RaiseTabClick(TabItem tab) {
      this.IsInTabDragDrop = false;
      this.OnTabClicked?.Invoke(this, new TabClickEventArgs(tab));
    }

    #endregion Event Stuff

    #region Tab Stuff

    [Obsolete("Assigned values but not used")]
    public bool IsInTabDragDrop = true;

    /// <summary>
    /// Creates a new tab, optionally selects it then erturns it
    /// </summary>
    /// <param name="DefPath">The <see cref="IListItemEx"/> that represents the location of this tab</param>
    /// <param name="IsNavigate">Do you want the new tab to be selected?</param>
    /// <returns></returns>
    public TabItem NewTab(IListItemEx DefPath, bool IsNavigate) {
      this.IsInTabDragDrop = false;
      var newt = new TabItem(DefPath) {
        Header = DefPath.DisplayName,
        Icon = DefPath.ThumbnailSource(16, BExplorer.Shell.Interop.ShellThumbnailFormatOption.IconOnly, BExplorer.Shell.Interop.ShellThumbnailRetrievalOption.Default),
        ToolTip = DefPath.ParsingName.Replace("%20", " ").Replace("%3A", ":").Replace("%5C", @"\"),
        AllowDrop = true
      };

      newt.DragEnter += new DragEventHandler(newt_DragEnter);
      newt.DragOver += new DragEventHandler(newt_DragOver);
      newt.PreviewMouseMove += new MouseEventHandler(newt_PreviewMouseMove);
      newt.PreviewMouseUp += newt_PreviewMouseDown;
      newt.Drop += new DragEventHandler(newt_Drop);
      newt.DragLeave += newt_Leave;
      newt.GiveFeedback += newt_GiveFeedback;

      //TODO: Try to remove this Try Catch
      try {
        Items.Add(newt);
        if (IsNavigate) {
          this.SelectedItem = newt;
        }
      } catch (Exception) {

      }

      ConstructMoveToCopyToMenu();
      this.IsInTabDragDrop = true;
      return newt;
    }

    /// <summary>
    /// Creates a new tab, optionally selects it then returns it
    /// </summary>
    /// <param name="Location">The file path of the new Tab</param>
    /// <param name="IsNavigate">Do you want the new tab to be selected?</param>
    /// <returns></returns>
    public TabItem NewTab(string Location, bool IsNavigate = false) => NewTab(FileSystemListItem.ToFileSystemItem(IntPtr.Zero, Location.ToShellParsingName()), IsNavigate);

  /// <summary>
  /// Creates a new tab starting at the libraries folder
  /// </summary>
    public void NewTab() {
      IListItemEx DefPath;
      if (StartUpLocation.StartsWith("::") && !StartUpLocation.Contains(@"\"))
        DefPath = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, "shell:" + StartUpLocation);
      else
        try {
          DefPath = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, StartUpLocation);
        } catch {
          DefPath = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, ((ShellItem)KnownFolders.Libraries).ParsingName.ToShellParsingName());
        }

      NewTab(DefPath, true);
    }

    /// <summary> Re-opens a previously closed tab using that tab's navigation log data then removes it from reopenabletabs. </summary>
    /// <param name="log"> The navigation log data from the previously closed tab. </param>
    public void ReOpenTab(NavigationLog log) {
      var Tab = NewTab(log.CurrentLocation, false);
      Tab.log.ImportData(log);
      ReopenableTabs.Remove(log);
    }

    /// <summary>
    /// Called by a child Header that wants to remove itself by clicking on the close button
    /// </summary>
    /// <param name="tabItem"></param>
    public void RemoveTabItem(TabItem tabItem, Boolean allowReopening = true, Boolean isCloseLastTab = false) {
      this.IsInTabDragDrop = false;
      if (IsFixedSize)
        throw new InvalidOperationException("ItemsSource is Fixed Size");

      if (allowReopening)
        this.ReopenableTabs.Add(tabItem.log);

      if (this.Items.OfType<TabItem>().Count() == 1 && !isCloseLastTab)
        return;
      else if (ItemsSource == null)
        this.Items.Remove(tabItem);
      else {
        var list = ItemsSource as IList;
        object listItem = ItemContainerGenerator.ItemFromContainer(tabItem);
        if (listItem != null && list != null)
          list.Remove(listItem);
      }

      if (this.SelectedItem == null && !isCloseLastTab)
        this.SelectedItem = this.Items.OfType<TabItem>().ToArray()[this.Items.OfType<TabItem>().Count() - 1];
    }
  
  /// <summary>
  /// Clones the tab and If <see cref="AddNewTabToEnd"/> Then adds at the end else inserts after the <see cref="SelectedIndex">currently selected item's index</see>
  /// </summary>
  /// <param name="theTab"></param>
    public void CloneTabItem(TabItem theTab) {
      int i = this.SelectedIndex;
      var newt = new TabItem(theTab.ShellObject) {
        Header = theTab.Header,
        Icon = theTab.Icon,
        ToolTip = theTab.ShellObject.ParsingName.Replace("%20", " ").Replace("%3A", ":").Replace("%5C", @"\"),
        AllowDrop = true,
        SelectedItems = theTab.SelectedItems
      };
      newt.log.CurrentLocation = theTab.ShellObject;
      newt.log.ImportData(theTab.log);
      if (i == -1 || i == this.Items.Count - 1 || AddNewTabToEnd)
        this.Items.Add(newt);
      else
        this.Items.Insert(++i, newt);

      ConstructMoveToCopyToMenu();
    }

  /// <summary>
  /// Closes all tabs but the provided one using <see cref="RemoveTabItem"/>
  /// </summary>
  /// <param name="tabItem">The Tab you want to keep</param>
    public void CloseAllTabsButThis(TabItem tabItem) {
      foreach (TabItem tab in this.Items.OfType<TabItem>().Where(x => x != tabItem)) {
        this.RemoveTabItem(tab);
      }
    }

    #endregion Tab Stuff

    #region Overrides

    /// <summary>
    /// OnApplyTemplate override
    /// </summary>
    public override void OnApplyTemplate() {
      base.OnApplyTemplate();

      // set up the event handler for the template parts
      _toggleButton = this.Template.FindName("PART_DropDown", this) as ToggleButton;
      if (_toggleButton != null) {
        // create a context menu for the togglebutton
        var cm = new ContextMenu { PlacementTarget = _toggleButton, Placement = PlacementMode.Bottom };

        // create a binding between the togglebutton's IsChecked Property
        // and the Context Menu's IsOpen Property
        var b = new Binding { Source = _toggleButton, Mode = BindingMode.TwoWay, Path = new PropertyPath(ToggleButton.IsCheckedProperty) };
        cm.SetBinding(ContextMenu.IsOpenProperty, b);

        _toggleButton.ContextMenu = cm;
        _toggleButton.Checked += DropdownButton_Checked;
      }

      var scrollViewer = this.Template.FindName("PART_ScrollViewer", this) as ScrollViewer;

      // set up event handlers for the RepeatButtons Click event
      var repeatLeft = this.Template.FindName("PART_RepeatLeft", this) as RepeatButton;
      if (repeatLeft != null) {
        repeatLeft.Click += delegate {
          if (scrollViewer != null)
            scrollViewer.LineRight();
        };
      }

      var repeatRight = this.Template.FindName("PART_RepeatRight", this) as RepeatButton;
      if (repeatRight != null) {
        repeatRight.Click += delegate {
          if (scrollViewer != null)
            scrollViewer.LineLeft();
        };
      }

      // set up the event handler for the 'New Tab' Button Click event
      _addNewButton = this.Template.FindName("PART_NewTabButton", this) as ButtonBase;
      if (_addNewButton != null)
        //_addNewButton.Click += ((sender, routedEventArgs) => AddTabItem());
        _addNewButton.Click += ((sender, routedEventArgs) => NewTab());
    }

    /// <summary>
    /// IsItemItsOwnContainerOverride
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    protected override bool IsItemItsOwnContainerOverride(object item) {
      return item is TabItem;
    }

    /// <summary>
    /// GetContainerForItemOverride
    /// </summary>
    /// <returns></returns>
    protected override DependencyObject GetContainerForItemOverride() {
      return new TabItem(FileSystemListItem.ToFileSystemItem(IntPtr.Zero, ((ShellItem)KnownFolders.Desktop).ParsingName));
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e) {
      var tabsCount = GetTabsCount();
      if (tabsCount == 0)
        return;

      TabItem ti = null;

      switch (e.Key) {
        case Key.Home:
          ti = GetTabItem(0);
          break;

        case Key.End:
          ti = GetTabItem(tabsCount - 1);
          break;

        case Key.Tab:
          if (e.KeyboardDevice.Modifiers == ModifierKeys.Control) {
            var index = SelectedIndex;
            var direction = e.KeyboardDevice.Modifiers == ModifierKeys.Shift ? -1 : 1;

            while (true) {
              index += direction;
              if (index < 0)
                index = tabsCount - 1;
              else if (index > tabsCount - 1)
                index = 0;

              FrameworkElement ui = GetTabItem(index);
              if (ui != null) {
                if (ui.Visibility == Visibility.Visible && ui.IsEnabled) {
                  ti = GetTabItem(index);
                  break;
                }
              }
            }
          }
          break;
      }

      TabPanel panel = Helper.FindVirtualizingTabPanel(this);
      if (panel != null && ti != null) {
        panel.MakeVisible(ti, Rect.Empty);
        SelectedItem = ti;

        e.Handled = ti.Focus();
      }
      base.OnPreviewKeyDown(e);
    }	
  
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e) {
      base.OnItemsChanged(e);
      //if (e.Action == NotifyCollectionChangedAction.Add && !SelectNewTabOnCreate) {
      //	this.IsSelectionHandled = true;
      //}
      //if (e.Action == NotifyCollectionChangedAction.Add && SelectNewTabOnCreate) {
      //	var tabItem = (TabItem)this.ItemContainerGenerator.ContainerFromItem(e.NewItems[e.NewItems.Count - 1]);
      //	SelectedItem = tabItem;

      //	TabPanel itemsHost = Helper.FindVirtualizingTabPanel(this);
      //	if (itemsHost != null)
      //		itemsHost.MakeVisible(tabItem, Rect.Empty);

      //	tabItem.Focus();
      //}
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
      base.OnSelectionChanged(e);
      var eEventArgs = new PreviewSelectionChangedEventArgs(e.AddedItems, e.RemovedItems);
      if (m_lLastSelectedIndex.HasValue)
        PreviewSelectionChanged?.Invoke(this, eEventArgs);

      if (eEventArgs.Cancel)
        this.SelectedIndex = m_lLastSelectedIndex.Value;
      else
        m_lLastSelectedIndex = this.SelectedIndex;
    }

    protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue) {
      base.OnItemsSourceChanged(oldValue, newValue);

      IsUsingItemsSource = newValue != null;
      if (IsFixedSize)
        AllowAddNew = AllowDelete = false;

      SetAddNewButtonVisibility();
      SetTabItemsCloseButtonVisibility();
    }

    #endregion Overrides

    #region Constructors

    static TabControl() {
      DefaultStyleKeyProperty.OverrideMetadata(typeof(TabControl), new FrameworkPropertyMetadata(typeof(TabControl)));
      TabStripPlacementProperty.AddOwner(typeof(TabControl), new FrameworkPropertyMetadata(Dock.Top, new PropertyChangedCallback(OnTabStripPlacementChanged)));
    }

    public TabControl() {
      this.ShouldNavigateOnSelection = false;
      this.Loaded +=
        delegate {
          SetAddNewButtonVisibility();
          SetTabItemsCloseButtonVisibility();
          IsUsingItemsSource = BindingOperations.IsDataBound(this, ItemsSourceProperty);

          if (IsUsingItemsSource && IsFixedSize)
            AllowAddNew = AllowDelete = false;
          this.MouseDoubleClick += TabControl_MouseDoubleClick;
        };
    }

    #endregion Constructors

    #region IEnumerable     

    internal IEnumerable GetItems() => IsUsingItemsSource ? ItemsSource : Items;

    private IEnumerable InternalChildren() {
      IEnumerator enumerator = GetItems().GetEnumerator();
      while (enumerator.MoveNext()) {
        if (enumerator.Current is TabItem)
          yield return enumerator.Current;
        else
          yield return this.ItemContainerGenerator.ContainerFromItem(enumerator.Current) as TabItem;
      }
    }

    #endregion

    #region Private/Internal      

    [Obsolete("Exactly the same as OnCoerceAllowDeleteCallback(...)")]
    private object OnCoerceAllowAddNewCallback(object basevalue) {
      if (ItemsSource == null)
        return basevalue;
      else if (ItemsSource is IList)
        return (ItemsSource as IList).IsFixedSize ? false : basevalue;
      else
        return false;
    }

    [Obsolete("Exactly the same as OnCoerceAllowAddNewCallback(...)")]
    private object OnCoerceAllowDeleteCallback(object basevalue) {
      if (ItemsSource == null)
        return basevalue;
      else if (ItemsSource is IList)
        return (ItemsSource as IList).IsFixedSize ? false : basevalue;
      else
        return false;
    }

    private void TabControl_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
      this.NewTab();
    }

    /// <summary>
    /// Handle the ToggleButton Checked event that displays a context menu of Header Headers
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void DropdownButton_Checked(object sender, RoutedEventArgs e) {
      if (_toggleButton == null) return;

      _toggleButton.ContextMenu.Items.Clear();
      _toggleButton.ContextMenu.Placement = TabStripPlacement == Dock.Bottom ? PlacementMode.Top : PlacementMode.Bottom;

      int index = 0;
      foreach (TabItem tabItem in this.InternalChildren()) {
        if (tabItem != null) {
          var header = Helper.CloneElement(tabItem.Header);
          var icon = tabItem.Icon == null ? null : (tabItem.Icon as BitmapSource).Clone();

          var mi = new MenuItem { Header = header, Icon = icon, Tag = index++.ToString() };
          mi.Click += ContextMenuItem_Click;

          _toggleButton.ContextMenu.Items.Add(mi);
        }
      }
    }

    /// <summary>
    /// Handle the MenuItem's Click event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ContextMenuItem_Click(object sender, RoutedEventArgs e) {
      var mi = sender as MenuItem;
      if (mi == null) return;

      int index;
      // get the index of the Header from the manuitems Tag property
      if (int.TryParse(mi.Tag.ToString(), out index)) {
        TabItem tabItem = GetTabItem(index);
        if (tabItem != null) {
          TabPanel itemsHost = Helper.FindVirtualizingTabPanel(this);
          if (itemsHost != null)
            itemsHost.MakeVisible(tabItem, Rect.Empty);

          tabItem.Focus();
        }
      }
    }

    private void SetAddNewButtonVisibility() {
      if (this.Template == null) return;

      ButtonBase button = this.Template.FindName("PART_NewTabButton", this) as ButtonBase;
      if (button == null)
        return;
      else if (IsFixedSize)
        button.Visibility = Visibility.Collapsed;
      else
        button.Visibility = AllowAddNew ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetTabItemsCloseButtonVisibility() {
      bool isFixedSize = IsFixedSize;

      var tabsCount = GetTabsCount();
      for (int i = 0; i < tabsCount; i++) {
        TabItem ti = GetTabItem(i);
        if (ti != null)
          ti.AllowDelete = !isFixedSize && this.AllowDelete;
      }
    }

    internal int GetTabsCount() {
      if (BindingOperations.IsDataBound(this, ItemsSourceProperty)) {
        if (ItemsSource is IList)
          return (ItemsSource as IList).Count;

        // ItemsSource is only an IEnumerable
        int i = 0;
        IEnumerator enumerator = ItemsSource.GetEnumerator();
        while (enumerator.MoveNext()) i++;
        return i;
      }

      return Items == null ? 0 : Items.Count;
    }

    internal TabItem GetTabItem(int index) {
      if (BindingOperations.IsDataBound(this, ItemsSourceProperty)) {
        IList list = ItemsSource as IList;
        if (list != null)
          return this.ItemContainerGenerator.ContainerFromItem(list[index]) as TabItem;

        // ItemsSource is at least an IEnumerable
        int i = 0;
        IEnumerator enumerator = ItemsSource.GetEnumerator();
        while (enumerator.MoveNext()) {
          if (i == index)
            return this.ItemContainerGenerator.ContainerFromItem(enumerator.Current) as TabItem;
          i++;
        }
        return null;
      }
      return Items[index] as TabItem;
    }

    #endregion

  }

  public class TabClickEventArgs : EventArgs {
    public TabItem ClickedItem { get; private set; }
    public TabClickEventArgs(TabItem tab) {
      this.ClickedItem = tab;
    }
  }
}