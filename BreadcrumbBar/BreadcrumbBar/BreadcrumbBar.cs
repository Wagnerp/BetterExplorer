﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using BExplorer.Shell;
using BExplorer.Shell._Plugin_Interfaces;

//###################################################################################
// Odyssey.Controls
// (c) Copyright 2008 Thomas Gerber
// All rights reserved.
//
//  THERE IS NO WARRANTY FOR THE PROGRAM, TO THE EXTENT PERMITTED BY
// APPLICABLE LAW.  EXCEPT WHEN OTHERWISE STATED IN WRITING THE COPYRIGHT
// HOLDERS AND/OR OTHER PARTIES PROVIDE THE PROGRAM "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING, BUT NOT LIMITED TO,
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE.  THE ENTIRE RISK AS TO THE QUALITY AND PERFORMANCE OF THE PROGRAM
// IS WITH YOU.  SHOULD THE PROGRAM PROVE DEFECTIVE, YOU ASSUME THE COST OF
// ALL NECESSARY SERVICING, REPAIR OR CORRECTION.
//###################################################################################
namespace Odyssey.Controls {

  /// <summary>
  /// A breadcrumb bar the contains breadcrumb items, a dropdown control, additional buttons and a progress bar.
  /// </summary>
  [ContentProperty("Root")]
  [TemplatePart(Name = partComboBox)]
  [TemplatePart(Name = partRoot)]
  public class BreadcrumbBar : Control {

    #region Constants

    private const string partComboBox = "PART_ComboBox";
    private const string partRoot = "PART_Root";

    /// <summary>
    /// Gets the number of the first breadcrumb to hide in the path if descending breadcrumbs are selected.
    /// </summary>
    private const int breadcrumbsToHide = 1;

    #endregion Constants

    #region Dependency Properties

    public static readonly DependencyProperty HasDropDownItemsProperty =
        DependencyProperty.Register("HasDropDownItems", typeof(bool), typeof(BreadcrumbBar), new UIPropertyMetadata(false));

    public static readonly DependencyProperty DropDownItemsPanelProperty =
        DependencyProperty.Register("DropDownItemsPanel", typeof(ItemsPanelTemplate), typeof(BreadcrumbBar), new UIPropertyMetadata(null));

    private static readonly DependencyPropertyKey IsRootSelectedPropertyKey =
        DependencyProperty.RegisterReadOnly("IsRootSelected", typeof(bool), typeof(BreadcrumbBar), new UIPropertyMetadata(true));

    public static readonly DependencyProperty IsRootSelectedProperty = IsRootSelectedPropertyKey.DependencyProperty;

    public static readonly DependencyProperty DropDownItemTemplateProperty =
        DependencyProperty.Register("DropDownItemTemplate", typeof(DataTemplate), typeof(BreadcrumbBar), new UIPropertyMetadata(null));

    public static readonly DependencyProperty DropDownItemTemplateSelectorProperty =
        DependencyProperty.Register("DropDownItemTemplateSelector", typeof(DataTemplateSelector), typeof(BreadcrumbBar), new UIPropertyMetadata(null));

    public static readonly DependencyProperty OverflowItemTemplateSelectorProperty =
        DependencyProperty.Register("OverflowItemTemplateSelector", typeof(DataTemplateSelector), typeof(BreadcrumbBar), new FrameworkPropertyMetadata(null,
            FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty OverflowItemTemplateProperty =
        DependencyProperty.Register("OverflowItemTemplate", typeof(DataTemplate), typeof(BreadcrumbBar), new FrameworkPropertyMetadata(null,
            FrameworkPropertyMetadataOptions.Inherits));

    private static readonly DependencyPropertyKey CollapsedTracesPropertyKey =
        DependencyProperty.RegisterReadOnly("CollapsedTraces", typeof(IEnumerable), typeof(BreadcrumbBar), new UIPropertyMetadata(null));

    public static readonly DependencyProperty CollapsedTracesProperty = CollapsedTracesPropertyKey.DependencyProperty;

    public static readonly DependencyProperty RootProperty =
        DependencyProperty.Register("Root", typeof(object), typeof(BreadcrumbBar), new FrameworkPropertyMetadata(null,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsArrange,
            RootPropertyChanged));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register("SelectedItem", typeof(object), typeof(BreadcrumbBar), new FrameworkPropertyMetadata(null,
            FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsMeasure));

    private static readonly DependencyPropertyKey SelectedBreadcrumbPropertyKey =
        DependencyProperty.RegisterReadOnly("SelectedBreadcrumb", typeof(BreadcrumbItem), typeof(BreadcrumbBar), new FrameworkPropertyMetadata(null,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsArrange,
            SelectedBreadcrumbPropertyChanged));

    public static readonly DependencyProperty SelectedBreadcrumbProperty = SelectedBreadcrumbPropertyKey.DependencyProperty;

    public static readonly DependencyProperty IsOverflowPressedProperty =
        DependencyProperty.Register("IsOverflowPressed", typeof(bool), typeof(BreadcrumbBar), new UIPropertyMetadata(false, OverflowPressedChanged));

    private static readonly DependencyPropertyKey RootItemPropertyKey =
        DependencyProperty.RegisterReadOnly("RootItem", typeof(BreadcrumbItem), typeof(BreadcrumbBar), new FrameworkPropertyMetadata(null,
            FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsMeasure
            ));

    private static readonly DependencyProperty RootItemProperty = RootItemPropertyKey.DependencyProperty;

    public static readonly DependencyProperty BreadcrumbItemTemplateSelectorProperty =
        DependencyProperty.Register("BreadcrumbItemTemplateSelector", typeof(DataTemplateSelector), typeof(BreadcrumbBar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty BreadcrumbItemTemplateProperty =
        DependencyProperty.Register("BreadcrumbItemTemplate", typeof(DataTemplate), typeof(BreadcrumbBar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    private static readonly DependencyPropertyKey OverflowModePropertyKey =
        DependencyProperty.RegisterReadOnly("OverflowMode", typeof(BreadcrumbButton.ButtonMode), typeof(BreadcrumbBar),
        new FrameworkPropertyMetadata(BreadcrumbButton.ButtonMode.Overflow,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OverflowModeProperty = OverflowModePropertyKey.DependencyProperty;

    public static readonly DependencyProperty IsDropDownOpenProperty =
    DependencyProperty.Register("IsDropDownOpen", typeof(bool), typeof(BreadcrumbBar), new UIPropertyMetadata(false, IsDropDownOpenChanged));

    public static readonly DependencyProperty SelectedDropDownIndexProperty =
        DependencyProperty.Register("SelectedDropDownIndex", typeof(int), typeof(BreadcrumbBar), new UIPropertyMetadata(-1));

    public static readonly DependencyProperty ProgressValueProperty =
        DependencyProperty.Register("ProgressValue", typeof(double), typeof(BreadcrumbBar),
        new UIPropertyMetadata((double)0.0, ProgressValuePropertyChanged, CoerceProgressValue));

    public static readonly DependencyProperty ProgressMaximumProperty =
        DependencyProperty.Register("ProgressMaximum", typeof(double), typeof(BreadcrumbBar), new UIPropertyMetadata(100.0, null, CoerceProgressMaximum));

    public static readonly DependencyProperty ProgressMinimumProperty =
        DependencyProperty.Register("ProgressMinimum", typeof(double), typeof(BreadcrumbBar), new UIPropertyMetadata(0.0, null, CoerceProgressMinimum));

    public event EventHandler<EditModeToggleEventArgs> OnEditModeToggle;

    #endregion Dependency Properties

    #region RoutedEvents

    public static readonly RoutedEvent BreadcrumbItemDropDownOpenedEvent = EventManager.RegisterRoutedEvent("BreadcrumbItemDropDownOpened",
        RoutingStrategy.Bubble, typeof(BreadcrumbItemEventHandler), typeof(BreadcrumbBar));

    public static readonly RoutedEvent BreadcrumbItemDropDownClosedEvent = EventManager.RegisterRoutedEvent("BreadcrumbItemDropDownClosed",
        RoutingStrategy.Bubble, typeof(BreadcrumbItemEventHandler), typeof(BreadcrumbBar));

    public static readonly RoutedEvent ProgressValueChangedEvent = EventManager.RegisterRoutedEvent("ProgressValueChanged",
        RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(BreadcrumbBar));

    public static readonly RoutedEvent ApplyPropertiesEvent = EventManager.RegisterRoutedEvent("ApplyProperties",
        RoutingStrategy.Bubble, typeof(ApplyPropertiesEventHandler), typeof(BreadcrumbBar));

    public static readonly RoutedEvent SelectedBreadcrumbChangedEvent = EventManager.RegisterRoutedEvent("SelectedBreadcrumbChanged",
        RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<BreadcrumbItem>), typeof(BreadcrumbBar));

    #endregion RoutedEvents

    #region RoutedUICommand

    /// <summary>
    /// This command shows the drop down part of the combobox.
    /// </summary>
    public static RoutedUICommand ShowDropDownCommand => showDropDownCommand;

    private static RoutedUICommand showDropDownCommand = new RoutedUICommand("Show DropDown", "ShowDropDownCommand", typeof(BreadcrumbBar));

    /// <summary>
    /// This command selects the BreadcrumbItem that is specified as Parameter.
    /// </summary>
    public static RoutedUICommand SelectTraceCommand => selectTraceCommand;

    /// <summary>
    /// This command selects the root.
    /// </summary>
    public static RoutedUICommand SelectRootCommand => selectRootCommand;

    private static RoutedUICommand selectRootCommand = new RoutedUICommand("Select", "SelectRootCommand", typeof(BreadcrumbBar));
    private static RoutedUICommand selectTraceCommand = new RoutedUICommand("Select", "SelectTraceCommand", typeof(BreadcrumbBar));

    static BreadcrumbBar() {
      DefaultStyleKeyProperty.OverrideMetadata(typeof(BreadcrumbBar), new FrameworkPropertyMetadata(typeof(BreadcrumbBar)));
      BorderThicknessProperty.OverrideMetadata(typeof(BreadcrumbBar), new FrameworkPropertyMetadata(new Thickness(1)));
      BorderBrushProperty.OverrideMetadata(typeof(BreadcrumbBar), new FrameworkPropertyMetadata(Brushes.Black));
      BackgroundProperty.OverrideMetadata(typeof(BreadcrumbBar), new FrameworkPropertyMetadata(new SolidColorBrush(new Color() { R = 245, G = 245, B = 245, A = 255 })));

      CommandManager.RegisterClassCommandBinding(typeof(FrameworkElement), new CommandBinding(selectRootCommand, SelectRootCommandExecuted));
      CommandManager.RegisterClassCommandBinding(typeof(FrameworkElement), new CommandBinding(selectTraceCommand, SelectTraceCommandExecuted));
      CommandManager.RegisterClassCommandBinding(typeof(FrameworkElement), new CommandBinding(showDropDownCommand, ShowDropDownExecuted));
    }

    #endregion RoutedUICommand

    #region Events

    /// <summary>
    /// Occurs after a BreadcrumbItem is created for which to apply additional properties.
    /// </summary>
    public event ApplyPropertiesEventHandler ApplyProperties {
      add { AddHandler(BreadcrumbBar.ApplyPropertiesEvent, value); }
      remove { RemoveHandler(BreadcrumbBar.ApplyPropertiesEvent, value); }
    }

    /// <summary>
    /// Occurs when the selected BreadcrumbItem is changed.
    /// </summary>
    public event RoutedEventHandler SelectedBreadcrumbChanged {
      add { AddHandler(BreadcrumbBar.SelectedBreadcrumbChangedEvent, value); }
      remove { RemoveHandler(BreadcrumbBar.SelectedBreadcrumbChangedEvent, value); }
    }

    #endregion Events

    #region Properties

    /// <summary>Occurs after navigation has occurred</summary>
    public Action<IListItemEx> OnNavigate { get; set; }

    private bool breadcrumbItemTraceValueChanged_IsFired { get; set; }

    private ObservableCollection<object> traces;
    private ComboBox comboBox;
    private BreadcrumbButton rootButton;

    /// <summary>
    /// Gets or sets the TraceBinding property that will be set to every child BreadcrumbItem. This is not a dependency property!
    /// </summary>
    public BindingBase TraceBinding { get; set; }

    /// <summary>
    /// Gets or sets the ImageBinding property that will be set to every child BreadcrumbItem. This is not a dependency property!
    /// </summary>
    public BindingBase ImageBinding { get; set; }


    /// <summary>
    /// Gets whether the selected breadcrumb is the RootItem.
    /// </summary>
    public bool IsRootSelected {
      get { return (bool)GetValue(IsRootSelectedProperty); }
      private set { SetValue(IsRootSelectedPropertyKey, value); }
    }

    //public ShellItem Root { get; set; }

    /// <summary>
    /// Gets or sets the root of the breadcrumb which can be a hierarchical data source or a BreadcrumbItem.
    /// </summary>
    public ShellItem Root {
      get { return (ShellItem)GetValue(RootProperty); }
      set { SetValue(RootProperty, value); }
    }

    private string _Path = "";

    /// <summary>
    /// Gets or sets the selected path.
    /// </summary>
    /// <remarks>Nothing will be changed if the new path is the same as the old path</remarks>
    public string Path {
      private get {
        return _Path;
      }
      set {
        if (_Path == value) return;
        _Path = value;
        var isLoaded = false;

        Task.Run(() => {
          if (IsInitialized) {
            try {
              Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() => {
                isLoaded = IsLoaded;
              }));
            } catch (Exception) {
              isLoaded = IsLoaded;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)(() => {
              BuildBreadcrumbsFromPath(value);
            }));
            if (isLoaded && !breadcrumbItemTraceValueChanged_IsFired && value != null) {
              Int64 pidl;
              bool isValidPidl = Int64.TryParse(value.TrimEnd(Char.Parse(@"\")), out pidl);
              try {
                var item = isValidPidl ? FileSystemListItem.ToFileSystemItem(IntPtr.Zero, (IntPtr)pidl) : FileSystemListItem.ToFileSystemItem(IntPtr.Zero, _Path.ToShellParsingName());
                if (item != null && this._ShouldNavigate) {
                  Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)(() => {
                    OnNavigate(item);
                  }));
                } else
                  this._ShouldNavigate = true;
              } catch (Exception) {
                //FIXME: fix this exception in case of searchfolder!!!!
              }
            }
          }
        });
      }
    }
    private Boolean _ShouldNavigate = true;
    public void SetPathWithoutNavigate(string path) {
      this._ShouldNavigate = false;
      this.Path = path;
    }

    /// <summary>
    /// Gets or sets the selected item.
    /// </summary>
    public ShellItem SelectedItem {
      get { return (ShellItem)GetValue(SelectedItemProperty); }
      private set { SetValue(SelectedItemProperty, value); }
    }

    /// <summary>
    /// Gets the collapsed traces.
    /// </summary>
    public IEnumerable CollapsedTraces {
      get { return (IEnumerable)GetValue(CollapsedTracesProperty); }
      private set { SetValue(CollapsedTracesPropertyKey, value); }
    }

    //private BreadcrumbItem selectedBreadcrumb;

    /// <summary>
    /// Gets the selected BreadcrumbItem
    /// </summary>
    public BreadcrumbItem SelectedBreadcrumb {
      get { return (BreadcrumbItem)GetValue(SelectedBreadcrumbProperty); }
      private set { SetValue(SelectedBreadcrumbPropertyKey, value); }
    }

    /// <summary>
    /// Gets the Root BreadcrumbItem.
    /// </summary>
    public BreadcrumbItem RootItem {
      get { return (BreadcrumbItem)GetValue(RootItemProperty); }
      protected set { SetValue(RootItemPropertyKey, value); }
    }

    /// <summary>
    /// Gets or sets whether the combobox dropdown is opened.
    /// </summary>
    public bool IsDropDownOpen {
      get { return (bool)GetValue(IsDropDownOpenProperty); }
      set { SetValue(IsDropDownOpenProperty, value); }
    }

    /// <summary>
    /// Gets or sets the string that is used to separate between traces.
    /// </summary>
    public string SeparatorString { get; set; }

    /// <summary>
    /// Gets the collection of buttons to appear on the right of the breadcrumb bar.
    /// </summary>
    public ObservableCollection<ButtonBase> Buttons { get; set; }

    /// <summary>A helper class to store the DropDownItems since ItemCollection has no public creator:</summary>
    private ItemsControl comboBoxControlItems;

    /// <summary>
    /// Gets or sets the DropDownItems for the combobox.
    /// </summary>
    public ItemCollection DropDownItems => comboBoxControlItems.Items;



    /// <summary>
    /// Gets whether the dropdown has items.
    /// </summary>
    [Obsolete("I want to remove this and just have it always true")]
    public bool HasDropDownItems {
      get { return (bool)GetValue(HasDropDownItemsProperty); }
      private set { SetValue(HasDropDownItemsProperty, value); }
    }

    #endregion Properties

    /// <summary>
    /// Creates a new BreadcrumbBar.
    /// </summary>
    public BreadcrumbBar()
      : base() {
      this.SeparatorString = "\\";
      this.Root = ((ShellItem)KnownFolders.Desktop);

      foreach (ShellItem item in ((ShellItem)KnownFolders.Desktop).Where(w => w.IsFolder)) {
        this.RootItem.Items.Add(item);
      }

      Buttons = new ObservableCollection<ButtonBase>();

      comboBoxControlItems = new ItemsControl();
      Binding b = new Binding("HasItems");
      b.Source = comboBoxControlItems;
      this.SetBinding(BreadcrumbBar.HasDropDownItemsProperty, b);

      traces = new ObservableCollection<object>();
      CollapsedTraces = traces;
      AddHandler(BreadcrumbItem.SelectionChangedEvent, new RoutedEventHandler(breadcrumbItemSelectedItemChanged));
      AddHandler(BreadcrumbItem.TraceChangedEvent, new RoutedEventHandler(breadcrumbItemTraceValueChanged));
      AddHandler(BreadcrumbItem.SelectionChangedEvent, new RoutedEventHandler(breadcrumbItemSelectionChangedEvent));
      AddHandler(BreadcrumbItem.DropDownPressedChangedEvent, new RoutedEventHandler(breadcrumbItemDropDownChangedEvent));
      traces.Add(null);

      InputBindings.Add(new KeyBinding(BreadcrumbBar.ShowDropDownCommand, new KeyGesture(Key.Down, ModifierKeys.Alt)));
    }

    private static void IsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      BreadcrumbBar bar = d as BreadcrumbBar;
      bar.OnDropDownOpenChanged((bool)e.OldValue, (bool)e.NewValue);
    }

    private static void SelectedBreadcrumbPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      BreadcrumbBar bar = d as BreadcrumbBar;
      BreadcrumbItem selected = e.NewValue as BreadcrumbItem;
      bar.IsRootSelected = selected == bar.RootItem;
      if (bar.IsInitialized) {
        var args = new RoutedPropertyChangedEventArgs<BreadcrumbItem>(e.OldValue as BreadcrumbItem, e.NewValue as BreadcrumbItem, BreadcrumbBar.SelectedBreadcrumbChangedEvent);
        bar.RaiseEvent(args);
      }
    }

    /// <summary>
    /// Traces the specified path and builds the associated BreadcrumbItems.
    /// </summary>
    /// <param name="newPath">The traces separated by the SepearatorString property.</param>
    internal void BuildBreadcrumbsFromPath(string newPath) {
      if (newPath != null && newPath.StartsWith("%"))
        newPath = Environment.ExpandEnvironmentVariables(newPath);

      string newPathToShellParsingName = newPath.ToShellParsingName();
      Int64 pidl;
      bool isValidPidl = Int64.TryParse(RemoveLastEmptySeparator(newPathToShellParsingName), out pidl);
      try {
        ShellItem shellItem = isValidPidl ? new ShellItem((IntPtr)pidl) : new ShellItem(RemoveLastEmptySeparator(newPathToShellParsingName));
        if (shellItem == null) return;

        BreadcrumbItem item = RootItem;
        if (item == null) return;

        newPath = RemoveLastEmptySeparator(newPath);

        var traces = new List<ShellItem>() { shellItem };
        while (shellItem != null && shellItem.Parent != null) {
          traces.Add(shellItem.Parent);
          shellItem = shellItem.Parent;
        }

        if (traces.Count == 0) RootItem.SelectedItem = null;
        traces.Reverse();

        var itemIndex = new List<Tuple<int, ShellItem>>();
        int index = 0;
        int length = traces.Count;
        int max = breadcrumbsToHide;
        // if the root is specified as first trace, then skip:
        if (max > 0 && traces[index].GetHashCode() == (shellItem.GetHashCode())) {
          length--;
          index++;
          max--;
        }

        for (int i = index; i < traces.Count; i++) {
          //Why do we have [if (item == null) break;] It seems like we add an if to the For(...) or it should NEVER be null
          if (item == null) break;
          var trace = traces[i];
          ////OnPopulateItems(item);
          //pop_items(item);
          object next = item.GetTraceItem(trace);
          if (next == null && item.Data.GetHashCode() == trace.Parent.GetHashCode()) {
            //missingItem = item;
            //lItem = trace;
            item.Items.Add(trace);

            next = item.GetTraceItem(trace);
          }
          if (next == null) break;
          itemIndex.Add(new Tuple<int, ShellItem>(item.Items.IndexOf(next), trace));
          BreadcrumbItem container = item.ContainerFromItem(next);

          item = container;
        }

        if (length != itemIndex.Count) {
          //recover the last path:
          Path = GetDisplayPath();
          return;
        }

        // temporarily remove the SelectionChangedEvent handler to minimize processing of events while building the breadcrumb items:
        RemoveHandler(BreadcrumbItem.SelectionChangedEvent, new RoutedEventHandler(breadcrumbItemSelectedItemChanged));

        try {
          var item2 = RootItem;
          foreach (var key in itemIndex) {
            if (item == null) break;
            if (item2.Items.OfType<ShellItem>().Count() == 1) {
              var firstItem = item2.Items.OfType<ShellItem>().First();
              if (firstItem.CachedParsingName != key.Item2.CachedParsingName) {
                item2.Items.Clear();
                item2.Items.Add(key.Item2);
              }
            } else if (item2.Items.OfType<ShellItem>().Count() == 0) {
              item2.Items.Add(key.Item2);
            }

            item2.SelectedIndex = item2.Items.IndexOf(item2.Items.OfType<ShellItem>().FirstOrDefault(w => w.CachedParsingName == key.Item2.CachedParsingName));
            item2 = item2.SelectedBreadcrumb;
          }
          if (item2 != null) item2.SelectedItem = null;
          SelectedBreadcrumb = item2;
          SelectedItem = item2 != null ? item2.Data : null;
        } finally {
          AddHandler(BreadcrumbItem.SelectionChangedEvent, new RoutedEventHandler(breadcrumbItemSelectedItemChanged));
        }

      } catch (FileNotFoundException ex) {
        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    /// <summary>
    /// Remove the last separator string from the path if there is no additional trace.
    /// </summary>
    /// <param name="path">The path from which to remove the last separator.</param>
    /// <returns>The path without an unnecessary last separator.</returns>
    private string RemoveLastEmptySeparator(string path) {
      if (path.Contains(":") && !(path.StartsWith("shell::") || path.StartsWith("::"))) return path;

      path = path.Trim();
      int sepLength = SeparatorString.Length;

      if (path.EndsWith(SeparatorString)) path = path.Remove(path.Length - sepLength, sepLength);
      return path;
    }

    /// <summary>
    /// Occurs when the IsDropDownOpen property is changed.
    /// </summary>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    protected virtual void OnDropDownOpenChanged(bool oldValue, bool newValue) {
      if (comboBox != null && newValue) {
        SetInputState();
        comboBox.Visibility = Visibility.Visible;
        comboBox.IsDropDownOpen = true;
      }
    }

    private static void SelectRootCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
      var item = e.Parameter as BreadcrumbItem;
      if (item != null)
        item.SelectedItem = null;
    }

    private static void SelectTraceCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
      var item = e.Parameter as BreadcrumbItem;
      if (item != null)
        item.SelectedItem = null;
    }

    private static void ShowDropDownExecuted(object sender, ExecutedRoutedEventArgs e) {
      BreadcrumbBar bar = sender as BreadcrumbBar;
      if (bar.DropDownItems.Count > 0) bar.IsDropDownOpen = true;
    }

    private void breadcrumbItemSelectedItemChanged(object sender, RoutedEventArgs e) {
      var breadcrumb = e.OriginalSource as BreadcrumbItem;
      if (breadcrumb != null && breadcrumb.SelectedBreadcrumb != null) breadcrumb = breadcrumb.SelectedBreadcrumb;
      SelectedBreadcrumb = breadcrumb;

      if (SelectedBreadcrumb != null)
        SelectedItem = SelectedBreadcrumb.Data;

      Path = GetDisplayPath();
    }

    private void breadcrumbItemTraceValueChanged(object sender, RoutedEventArgs e) {
      if (e.OriginalSource == RootItem) {
        breadcrumbItemTraceValueChanged_IsFired = true;
        //Path = GetDisplayPath();
        breadcrumbItemTraceValueChanged_IsFired = false;
      }
    }

    private void breadcrumbItemSelectionChangedEvent(object sender, RoutedEventArgs e) {
      var parent = e.Source as BreadcrumbItem;
      if (parent != null && parent.SelectedBreadcrumb != null) {
        if (parent.SelectedBreadcrumb.Items.Count == 0) {
          if (parent.SelectedBreadcrumb.TraceValue.Equals(((ShellItem)KnownFolders.Computer).DisplayName)) {
            foreach (ShellItem s in KnownFolders.Computer) {
              if (s.IsValidShellFolder)
                parent.SelectedBreadcrumb.Items.Add(s);
            }
          }
        }
      }
    }

    private void breadcrumbItemDropDownChangedEvent(object sender, RoutedEventArgs e) {
      BreadcrumbItem breadcrumb = e.Source as BreadcrumbItem;
      if (breadcrumb.IsDropDownPressed)
        OnBreadcrumbItemDropDownOpened(e);
      else
        OnBreadcrumbItemDropDownClosed(e);
    }

    /// <summary>
    /// Occurs when the dropdown of a BreadcrumbItem is opened.
    /// </summary>
    public event BreadcrumbItemEventHandler BreadcrumbItemDropDownOpened {
      add { AddHandler(BreadcrumbBar.BreadcrumbItemDropDownOpenedEvent, value); }
      remove { RemoveHandler(BreadcrumbBar.BreadcrumbItemDropDownOpenedEvent, value); }
    }

    /// <summary>
    /// Occurs when the dropdown of a BreadcrumbItem is closed.
    /// </summary>
    public event BreadcrumbItemEventHandler BreadcrumbItemDropDownClosed {
      add { AddHandler(BreadcrumbBar.BreadcrumbItemDropDownClosedEvent, value); }
      remove { RemoveHandler(BreadcrumbBar.BreadcrumbItemDropDownClosedEvent, value); }
    }

    /// <summary>
    /// Occurs when the dropdown of a BreadcrumbItem is opened.
    /// </summary>
    protected virtual void OnBreadcrumbItemDropDownOpened(RoutedEventArgs e) {
      BreadcrumbItemEventArgs args = new BreadcrumbItemEventArgs(e.Source as BreadcrumbItem, BreadcrumbItemDropDownOpenedEvent);
      RaiseEvent(args);
    }

    /// <summary>
    /// Occurs when the dropdown of a BreadcrumbItem is closed.
    /// </summary>
    protected virtual void OnBreadcrumbItemDropDownClosed(RoutedEventArgs e) {
      BreadcrumbItemEventArgs args = new BreadcrumbItemEventArgs(e.Source as BreadcrumbItem, BreadcrumbItemDropDownClosedEvent);
      RaiseEvent(args);
    }

    protected override Size ArrangeOverride(Size arrangeBounds) {
      Size size = base.ArrangeOverride(arrangeBounds);
      bool isOverflow = (RootItem != null && RootItem.SelectedBreadcrumb != null && RootItem.SelectedBreadcrumb.IsOverflow);
      OverflowMode = isOverflow ? BreadcrumbButton.ButtonMode.Overflow : BreadcrumbButton.ButtonMode.Breadcrumb;
      return size;
    }

    private object GetImage(ImageSource imageSource) {
      if (imageSource == null) return null;
      Image image = new Image();
      image.Source = imageSource;
      image.Stretch = Stretch.Fill;
      image.SnapsToDevicePixels = true;
      image.Width = image.Height = 16;

      return image;
    }

    private void menuItem_Click(object sender, RoutedEventArgs e) {
      MenuItem item = e.Source as MenuItem;
      if (RootItem != null && item != null) {
        object dataItem = item.Tag;
        if (dataItem != null && dataItem.Equals(RootItem.SelectedItem)) RootItem.SelectedItem = null;
        RootItem.SelectedItem = dataItem;
      }
    }

    /// <summary>
    /// Gets or sets the DataTemplateSelector for the overflow items.
    /// </summary>
    public DataTemplateSelector OverflowItemTemplateSelector {
      get { return (DataTemplateSelector)GetValue(OverflowItemTemplateSelectorProperty); }
      set { SetValue(OverflowItemTemplateSelectorProperty, value); }
    }

    /// <summary>
    /// Gets or set the DataTemplate for the OverflowItem.
    /// </summary>
    public DataTemplate OverflowItemTemplate {
      get { return (DataTemplate)GetValue(OverflowItemTemplateProperty); }
      set { SetValue(OverflowItemTemplateProperty, value); }
    }

    private static void RootPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      BreadcrumbBar bar = d as BreadcrumbBar;
      bar.OnRootChanged(e.OldValue, e.NewValue);
    }

    /// <summary>
    /// Occurs when the Root property is changed.
    /// </summary>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    public virtual void OnRootChanged(object oldValue, object newValue) {
      //newValue = GetFirstItem(newValue);
      //if (oldRoot != null) {
      if (oldValue is BreadcrumbItem) {
        ((BreadcrumbItem)oldValue).IsRoot = false;
      }

      if (newValue == null) {
        RootItem = null;
        Path = null;
      } else {
        var root = newValue as BreadcrumbItem;

        /*
        if (root == null)
        {
            root = BreadcrumbItem.CreateItem(newValue);
            root.IsRoot = true;
        }
        else
            root.IsRoot = true;
        */

        if (root == null) root = BreadcrumbItem.CreateItem(newValue);
        root.IsRoot = true;
        this.RemoveLogicalChild(oldValue);
        RootItem = root;

        if (root != null && LogicalTreeHelper.GetParent(root) == null) this.AddLogicalChild(root);

        SelectedItem = root != null ? (ShellItem)root.DataContext : null;
        if (IsInitialized) SelectedBreadcrumb = root;
      }
    }

    /// <summary>
    /// Gets whether the Overflow button is pressed.
    /// </summary>
    public bool IsOverflowPressed {
      get { return (bool)GetValue(IsOverflowPressedProperty); }
      private set { SetValue(IsOverflowPressedProperty, value); }
    }

    private static void OverflowPressedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      BreadcrumbBar bar = d as BreadcrumbBar;
      bar.OnOverflowPressedChanged();
    }

    /// <summary>
    /// Occurs when the IsOverflowPressed property is changed.
    /// </summary>
    protected virtual void OnOverflowPressedChanged() {
      // rebuild the list of traces to show in the pop-up of the overflow button:
      if (!IsOverflowPressed) return;

      BreadcrumbItem item = RootItem;

      traces.Clear();
      if (item != null && item.IsOverflow) {
        foreach (object trace in item.Items) {
          MenuItem menuItem = new MenuItem();
          menuItem.Tag = trace;
          BreadcrumbItem bcItem = item.ContainerFromItem(trace);
          menuItem.Header = bcItem.TraceValue;
          menuItem.Click += new RoutedEventHandler(menuItem_Click);
          menuItem.Icon = GetImage(bcItem != null ? bcItem.Image : null);
          if (trace == RootItem.SelectedItem) menuItem.FontWeight = FontWeights.Bold;
          traces.Add(menuItem);
        }
        traces.Insert(0, new Separator());
        MenuItem rootMenuItem = new MenuItem();
        rootMenuItem.Header = item.TraceValue;
        rootMenuItem.Command = BreadcrumbBar.SelectRootCommand;
        rootMenuItem.CommandParameter = item;
        rootMenuItem.Icon = GetImage(item.Image);
        traces.Insert(0, rootMenuItem);
      }

      item = item != null ? item.SelectedBreadcrumb : null;

      while (item != null) {
        if (!item.IsOverflow) break;
        MenuItem traceMenuItem = new MenuItem();
        traceMenuItem.Header = item.TraceValue;
        traceMenuItem.Command = BreadcrumbBar.SelectRootCommand;
        traceMenuItem.CommandParameter = item;
        traceMenuItem.Icon = GetImage(item.Image);
        traces.Insert(0, traceMenuItem);
        item = item.SelectedBreadcrumb;
      }
    }

    /// <summary>
    /// Gets or sets the TemplateSelector for an embedded BreadcrumbItem.
    /// </summary>
    public DataTemplateSelector BreadcrumbItemTemplateSelector {
      get { return (DataTemplateSelector)GetValue(BreadcrumbItemTemplateSelectorProperty); }
      set { SetValue(BreadcrumbItemTemplateSelectorProperty, value); }
    }

    /// <summary>
    /// Gets or sets the Template for an embedded BreadcrumbItem.
    /// </summary>
    public DataTemplate BreadcrumbItemTemplate {
      get { return (DataTemplate)GetValue(BreadcrumbItemTemplateProperty); }
      set { SetValue(BreadcrumbItemTemplateProperty, value); }
    }

    /// <summary>
    /// Gets the overflow mode for the Overflow BreadcrumbButton (PART_Root).
    /// </summary>
    public BreadcrumbButton.ButtonMode OverflowMode {
      get { return (BreadcrumbButton.ButtonMode)GetValue(OverflowModeProperty); }
      private set { SetValue(OverflowModePropertyKey, value); }
    }

    public override void OnApplyTemplate() {
      base.OnApplyTemplate();
      comboBox = GetTemplateChild(partComboBox) as ComboBox;
      rootButton = GetTemplateChild(partRoot) as BreadcrumbButton;
      if (comboBox != null) {
        comboBox.StaysOpenOnEdit = true;
        comboBox.DropDownClosed += new EventHandler(comboBox_DropDownClosed);
        comboBox.KeyDown += new KeyEventHandler(comboBox_KeyDown);
        comboBox.Loaded += comboBox_Loaded;
      }

      if (rootButton != null) rootButton.Click += new RoutedEventHandler(rootButton_Click);
    }

    private void comboBox_Loaded(object sender, RoutedEventArgs e) {
      TextBox tb = (TextBox)comboBox.Template.FindName("PART_EditableTextBox", comboBox);
      if (tb != null) tb.LostKeyboardFocus += (x, y) => Exit(false);
    }

    private void comboBox_KeyDown(object sender, KeyEventArgs e) {
      switch (e.Key) {
        case Key.Escape: Exit(false); break;
        case Key.Enter:
          if (!DropDownItems.Contains(comboBox.Text)) DropDownItems.Add(comboBox.Text);
          Exit(true);
          break;

        default: return;
      }
      e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e) {
      if (e.Handled) return;
      if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed) {
        e.Handled = true;
        SetInputState();
      }

      base.OnMouseDown(e);
    }

    private void rootButton_Click(object sender, RoutedEventArgs e) => SetInputState();


    public void SetInputState() {
      if (comboBox != null && comboBox.Visibility != System.Windows.Visibility.Visible) {
        if (this.OnEditModeToggle != null)
          this.OnEditModeToggle.Invoke(comboBox, new EditModeToggleEventArgs(false));
        if (this.SelectedBreadcrumb != null && this.SelectedBreadcrumb.Data != null)
          comboBox.Text = new ShellItem(this.SelectedBreadcrumb.Data.CachedParsingName.ToShellParsingName()).GetDisplayName(BExplorer.Shell.Interop.SIGDN.DESKTOPABSOLUTEEDITING);
        comboBox.Visibility = Visibility.Visible;
        comboBox.Focus();
      }
    }

    /// <summary>
    /// Gets the display path from the traces of the BreacrumbItems.
    /// </summary>
    /// <returns></returns>
    public string GetDisplayPath() {
      string separator = SeparatorString;
      StringBuilder sb = new StringBuilder();
      string result = "";
      BreadcrumbItem item = RootItem;
      int index = 0;
      while (item != null) {
        //if (sb.Length > 0) sb.Append(separator);
        if (index >= breadcrumbsToHide || item.SelectedItem == null) {
          //	sb.Append(item.GetTracePathValue());
        }
        index++;
        result = item.TraceValue == ((ShellItem)KnownFolders.Desktop).DisplayName ? KnownFolders.Desktop.ParsingName : item.Data.ParsingName;
        item = item.SelectedBreadcrumb;
      }

      return result;
    }

    /// <summary>
    /// Do what's necessary to do when the BreadcrumbBar has lost focus.
    /// </summary>
    public void Exit(bool updatePath) {
      if (comboBox != null) {
        if (updatePath && comboBox.IsVisible) Path = comboBox.Text;
        comboBox.Visibility = Visibility.Hidden;

        if (this.OnEditModeToggle != null) this.OnEditModeToggle.Invoke(comboBox, new EditModeToggleEventArgs(true));
      }
    }

    public void SetProgressValue(double value, Duration duration) {
      DoubleAnimation animation = new DoubleAnimation(this.ProgressValue, value, duration) { FillBehavior = Math.Abs(value - this.ProgressMaximum) < 0.5 ? FillBehavior.Stop : FillBehavior.HoldEnd };
      animation.Completed += (s, e) => {
        if (Math.Abs(value - this.ProgressMaximum) < 0.5) {
          this.SetProgressValue(0, TimeSpan.FromMilliseconds(0));
        }
      };
      this.BeginAnimation(BreadcrumbBar.ProgressValueProperty, animation, HandoffBehavior.Compose);
    }

    private void comboBox_DropDownClosed(object sender, EventArgs e) {
      IsDropDownOpen = false;
      Path = comboBox.Text;
    }

    /// <summary>
    /// Gets or sets the ItemsPanelTemplate for the DropDownItems of the combobox.
    /// </summary>
    public ItemsPanelTemplate DropDownItemsPanel {
      get { return (ItemsPanelTemplate)GetValue(DropDownItemsPanelProperty); }
      set { SetValue(DropDownItemsPanelProperty, value); }
    }

    /// <summary>
    /// Gets or sets the ItemsPanelTemplateSelector for the DropDownItems of the combobox.
    /// </summary>
    public DataTemplateSelector DropDownItemTemplateSelector {
      get { return (DataTemplateSelector)GetValue(DropDownItemTemplateSelectorProperty); }
      set { SetValue(DropDownItemTemplateSelectorProperty, value); }
    }

    /// <summary>
    /// Gets or sets the DataTemplate for the DropDownItems of the combobox.
    /// </summary>
    public DataTemplate DropDownItemTemplate {
      get { return (DataTemplate)GetValue(DropDownItemTemplateProperty); }
      set { SetValue(DropDownItemTemplateProperty, value); }
    }

    /// <summary>
    /// Gets or sets the SelectedIndex of the combobox.
    /// </summary>
    public int SelectedDropDownIndex {
      get { return (int)GetValue(SelectedDropDownIndexProperty); }
      set { SetValue(SelectedDropDownIndexProperty, value); }
    }

    /// <summary>
    /// Gets or sets the current progress indicator value.
    /// </summary>
    public double ProgressValue {
      get { return (double)GetValue(ProgressValueProperty); }
      set { SetValue(ProgressValueProperty, value); }
    }

    /// <summary>
    /// Check the desired value for ProgressValue and asure that it is between Minimum and Maximum:
    /// </summary>
    /// <param name="d"></param>
    /// <param name="baseValue"></param>
    /// <returns>The value between minimum and maximum.</returns>
    private static object CoerceProgressValue(DependencyObject d, object baseValue) {
      BreadcrumbBar bar = d as BreadcrumbBar;
      double value = (double)baseValue;
      if (value > bar.ProgressMaximum) value = bar.ProgressMaximum;
      if (value < bar.ProgressMimimum) value = bar.ProgressMimimum;

      return value;
    }

    /// <summary>
    /// Occurs when the ProgressValue is changed.
    /// </summary>
    public event RoutedEventHandler ProgressValueChanged {
      add { AddHandler(BreadcrumbBar.ProgressValueChangedEvent, value); }
      remove { RemoveHandler(BreadcrumbBar.ProgressValueChangedEvent, value); }
    }

    private static void ProgressValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      RoutedEventArgs args = new RoutedEventArgs(BreadcrumbBar.ProgressValueChangedEvent);
      BreadcrumbBar bar = d as BreadcrumbBar;
      bar.RaiseEvent(args);
    }

    private static object CoerceProgressMaximum(DependencyObject d, object baseValue) {
      BreadcrumbBar bar = d as BreadcrumbBar;
      double value = (double)baseValue;
      if (value < bar.ProgressMimimum) value = bar.ProgressMimimum;
      if (value < bar.ProgressValue) bar.ProgressValue = value;
      if (value < 0) value = 0;

      return value;
    }

    private static object CoerceProgressMinimum(DependencyObject d, object baseValue) {
      BreadcrumbBar bar = d as BreadcrumbBar;
      double value = (double)baseValue;
      if (value > bar.ProgressMaximum) value = bar.ProgressMaximum;
      if (value > bar.ProgressValue) bar.ProgressValue = value;

      return value;
    }

    /// <summary>
    /// Gets or sets the maximum progress value.
    /// </summary>
    public double ProgressMaximum {
      get { return (double)GetValue(ProgressMaximumProperty); }
      set { SetValue(ProgressMaximumProperty, value); }
    }

    /// <summary>
    /// Gets or sets the minimum progress value.
    /// </summary>
    public double ProgressMimimum {
      get { return (double)GetValue(ProgressMinimumProperty); }
      set { SetValue(ProgressMinimumProperty, value); }
    }

    protected override IEnumerator LogicalChildren {
      get {
        object content = this.RootItem;
        if (content == null) return base.LogicalChildren;

        if (base.TemplatedParent != null) {
          DependencyObject current = content as DependencyObject;
          if (current != null) {
            DependencyObject parent = LogicalTreeHelper.GetParent(current);
            if (parent != null && parent != this) return base.LogicalChildren;
          }
        }

        object[] array = new object[] { RootItem };
        return array.GetEnumerator();
      }
    }
  }
}