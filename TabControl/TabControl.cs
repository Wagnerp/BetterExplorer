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

namespace Wpf.Controls {

	[TemplatePart(Name = "PART_DropDown", Type = typeof(ToggleButton))]
	[TemplatePart(Name = "PART_RepeatLeft", Type = typeof(RepeatButton))]
	[TemplatePart(Name = "PART_RepeatRight", Type = typeof(RepeatButton))]
	[TemplatePart(Name = "PART_NewTabButton", Type = typeof(ButtonBase))]
	[TemplatePart(Name = "PART_ScrollViewer", Type = typeof(ScrollViewer))]
	public class TabControl : System.Windows.Controls.TabControl {
		public List<NavigationLog> reopenabletabs = new List<NavigationLog>();
		public string StartUpLocation = KnownFolders.Libraries.ParsingName;
		public DragEventHandler newt_DragEnter, newt_DragOver, newt_Drop;
		public MouseEventHandler newt_PreviewMouseMove;
		public Action ConstructMoveToCopyToMenu;

		public bool isGoingBackOrForward;

		private List<string> Archives = new List<string>(new string[] { ".rar", ".zip", ".7z", ".tar", ".gz", ".xz", ".bz2" });
		private List<string> Images = new List<string>(new string[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".wmf" });
		private List<string> VirDisks = new List<string>(new string[] { ".iso", ".bin", ".vhd" });

		#region Properties/Locals

		// TemplatePart controls
		private ToggleButton _toggleButton;

		private ButtonBase _addNewButton;

		private bool IsFixedSize {
			get {
				IEnumerable items = GetItems();
				return items as IList == null || (items as IList).IsFixedSize;
			}
		}

		#endregion Properties/Locals

		#region Events

		public event EventHandler<CancelEventArgs> TabItemAdding;

		public event EventHandler<TabItemEventArgs> TabItemAdded;

		public event EventHandler<TabItemCancelEventArgs> TabItemClosing;

		public event EventHandler<TabItemEventArgs> TabItemClosed;

		public event EventHandler<NewTabItemEventArgs> NewTabItem;

		#endregion Events

		#region Dependency properties

		public bool IsUsingItemsSource {
			get { return (bool)GetValue(IsUsingItemsSourceProperty); }
			private set { SetValue(IsUsingItemsSourcePropertyKey, value); }
		}

		public static readonly DependencyPropertyKey IsUsingItemsSourcePropertyKey =
				DependencyProperty.RegisterReadOnly("IsUsingItemsSource", typeof(bool), typeof(TabControl), new UIPropertyMetadata(false));

		public static readonly DependencyProperty IsUsingItemsSourceProperty = IsUsingItemsSourcePropertyKey.DependencyProperty;

		#region Brushes

		public Brush TabItemNormalBackground {
			get { return (Brush)GetValue(TabItemNormalBackgroundProperty); }
			set { SetValue(TabItemNormalBackgroundProperty, value); }
		}

		public static readonly DependencyProperty TabItemNormalBackgroundProperty = DependencyProperty.Register("TabItemNormalBackground", typeof(Brush), typeof(TabControl), new UIPropertyMetadata(null));

		public Brush TabItemMouseOverBackground {
			get { return (Brush)GetValue(TabItemMouseOverBackgroundProperty); }
			set { SetValue(TabItemMouseOverBackgroundProperty, value); }
		}

		public static readonly DependencyProperty TabItemMouseOverBackgroundProperty = DependencyProperty.Register("TabItemMouseOverBackground", typeof(Brush), typeof(TabControl), new UIPropertyMetadata(null));

		public Brush TabItemSelectedBackground {
			get { return (Brush)GetValue(TabItemSelectedBackgroundProperty); }
			set { SetValue(TabItemSelectedBackgroundProperty, value); }
		}

		public static readonly DependencyProperty TabItemSelectedBackgroundProperty = DependencyProperty.Register("TabItemSelectedBackground", typeof(Brush), typeof(TabControl), new UIPropertyMetadata(null));

		#endregion Brushes

		/*
				 * Based on the whether the ControlTemplate implements the NewTab button and Close Buttons determines the functionality of the AllowAddNew & AllowDelete properties
				 * If they are in the control template, then the visibility of the AddNew & Header buttons are bound to these properties
				 *
				*/

		/// <summary>
		/// Allow the User to Add New TabItems
		/// </summary>
		public bool AllowAddNew {
			get { return (bool)GetValue(AllowAddNewProperty); }
			set { SetValue(AllowAddNewProperty, value); }
		}

		public static readonly DependencyProperty AllowAddNewProperty = DependencyProperty.Register("AllowAddNew", typeof(bool), typeof(TabControl),
				new FrameworkPropertyMetadata(true, new PropertyChangedCallback(OnAllowAddNewChanged), OnCoerceAllowAddNewCallback));

		public static readonly DependencyProperty DefaultTabPathProperty = DependencyProperty.Register("DefaultTabPath", typeof(String), typeof(TabControl),
					new UIPropertyMetadata(null));

		public String DefaultTabPath {
			get {
				return (String)GetValue(DefaultTabPathProperty);
			}
			set {
				SetValue(DefaultTabPathProperty, value);
			}
		}

		private static void OnAllowAddNewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			((TabControl)d).SetAddNewButtonVisibility();
		}

		private static object OnCoerceAllowAddNewCallback(DependencyObject d, object basevalue) {
			return ((TabControl)d).OnCoerceAllowAddNewCallback(basevalue);
		}

		private object OnCoerceAllowAddNewCallback(object basevalue) {
			if (ItemsSource != null) {
				IList list = ItemsSource as IList;
				if (list != null) {
					if (list.IsFixedSize)
						return false;
					return basevalue;
				}
				return false;
			}
			return basevalue;
		}

		/// <summary>
		/// Allow the User to Delete TabItems
		/// </summary>
		public bool AllowDelete {
			get { return (bool)GetValue(AllowDeleteProperty); }
			set { SetValue(AllowDeleteProperty, value); }
		}

		public static readonly DependencyProperty AllowDeleteProperty = DependencyProperty.Register("AllowDelete", typeof(bool), typeof(TabControl),
				new FrameworkPropertyMetadata(true, new PropertyChangedCallback(OnAllowDeleteChanged), OnCoerceAllowDeleteNewCallback));

		private static void OnAllowDeleteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			TabControl tc = (TabControl)d;
			tc.SetTabItemsCloseButtonVisibility();
		}

		private static object OnCoerceAllowDeleteNewCallback(DependencyObject d, object basevalue) {
			return ((TabControl)d).OnCoerceAllowDeleteCallback(basevalue);
		}

		private object OnCoerceAllowDeleteCallback(object basevalue) {
			if (ItemsSource != null) {
				IList list = ItemsSource as IList;
				if (list != null) {
					if (list.IsFixedSize)
						return false;
					return basevalue;
				}
				return false;
			}
			return basevalue;
		}

		/// <summary>
		/// Set new Header as the current selection
		/// </summary>
		public bool SelectNewTabOnCreate {
			get { return (bool)GetValue(SelectNewTabOnCreateProperty); }
			set { SetValue(SelectNewTabOnCreateProperty, value); }
		}

		public static readonly DependencyProperty SelectNewTabOnCreateProperty = DependencyProperty.Register("SelectNewTabOnCreate", typeof(bool), typeof(TabControl), new UIPropertyMetadata(true));

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

		public static readonly DependencyProperty AddNewTabToEndProperty = DependencyProperty.Register("AddNewTabToEnd", typeof(bool), typeof(TabControl), new UIPropertyMetadata(true));

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

		public static readonly DependencyProperty TabItemMinWidthProperty = DependencyProperty.Register("TabItemMinWidth", typeof(double), typeof(TabControl),
				new FrameworkPropertyMetadata(20.0, new PropertyChangedCallback(OnMinMaxChanged), CoerceMinWidth));

		private static object CoerceMinWidth(DependencyObject d, object value) {
			TabControl tc = (TabControl)d;
			double newValue = (double)value;

			if (newValue > tc.TabItemMaxWidth)
				return tc.TabItemMaxWidth;

			return (newValue > 0 ? newValue : 0);
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

		public static readonly DependencyProperty TabItemMinHeightProperty = DependencyProperty.Register("TabItemMinHeight", typeof(double), typeof(TabControl),
				new FrameworkPropertyMetadata(20.0, new PropertyChangedCallback(OnMinMaxChanged), CoerceMinHeight));

		private static object CoerceMinHeight(DependencyObject d, object value) {
			TabControl tc = (TabControl)d;
			double newValue = (double)value;

			if (newValue > tc.TabItemMaxHeight)
				return tc.TabItemMaxHeight;

			return (newValue > 0 ? newValue : 0);
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

		public static readonly DependencyProperty TabItemMaxWidthProperty = DependencyProperty.Register("TabItemMaxWidth", typeof(double), typeof(TabControl),
				new FrameworkPropertyMetadata(double.PositiveInfinity, new PropertyChangedCallback(OnMinMaxChanged), CoerceMaxWidth));

		private static object CoerceMaxWidth(DependencyObject d, object value) {
			TabControl tc = (TabControl)d;
			double newValue = (double)value;

			if (newValue < tc.TabItemMinWidth)
				return tc.TabItemMinWidth;

			return newValue;
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

		public static readonly DependencyProperty TabItemMaxHeightProperty = DependencyProperty.Register("TabItemMaxHeight", typeof(double), typeof(TabControl),
				new FrameworkPropertyMetadata(double.PositiveInfinity, new PropertyChangedCallback(OnMinMaxChanged), CoerceMaxHeight));

		private static object CoerceMaxHeight(DependencyObject d, object value) {
			TabControl tc = (TabControl)d;
			double newValue = (double)value;

			if (newValue < tc.TabItemMinHeight)
				return tc.TabItemMinHeight;

			return newValue;
		}

		/// <summary>
		/// OnMinMaxChanged callback responds to any of the Min/Max dependancy properties changing
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

			//            var tabsCount = tc.GetTabsCount();
			//            for (int i = 0; i < tabsCount; i++)
			//            {
			//                Header ti = tc.GetTabItem(i);
			//                if (ti != null)
			//                    ti.Dimension = null;
			//            }

			TabPanel tp = Helper.FindVirtualizingTabPanel(tc);
			if (tp != null)
				tp.InvalidateMeasure();
		}

		/// <summary>
		/// OnTabStripPlacementChanged property callback
		/// </summary>
		/// <remarks>
		///     We need to supplement the base implementation with this method as the base method does not work when
		///     we are using virtualization in the tabpanel, it only updates visible items
		/// </remarks>
		private static void OnTabStripPlacementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			TabControl tc = (TabControl)d;

			foreach (TabItem tabItem in tc.InternalChildren()) {
				if (tabItem != null) {
					tabItem.Dimension = null;
					tabItem.CoerceValue(System.Windows.Controls.TabItem.TabStripPlacementProperty);
				}
			}
		}

		#endregion Dependency properties

		#region Tab Stuff

		public Wpf.Controls.TabItem NewTab(ShellItem DefPath, bool IsNavigate) {
			//TODO: figure out what to do with Cloning a tab!
			DefPath.Thumbnail.CurrentSize = new Size(16, 16);
			DefPath.Thumbnail.FormatOption = BExplorer.Shell.Interop.ShellThumbnailFormatOption.IconOnly;
			SelectNewTabOnCreate = IsNavigate;
			var newt = new Wpf.Controls.TabItem() {
				ShellObject = DefPath,
				Header = DefPath.GetDisplayName(BExplorer.Shell.Interop.SIGDN.NORMALDISPLAY),
				Icon = DefPath.Thumbnail.BitmapSource,
				ToolTip = DefPath.ParsingName,
				AllowDrop = true
			};

			newt.DragEnter += new DragEventHandler(newt_DragEnter);
			newt.DragOver += new DragEventHandler(newt_DragOver);
			newt.PreviewMouseMove += new MouseEventHandler(newt_PreviewMouseMove);
			newt.Drop += new DragEventHandler(newt_Drop);

			Items.Add(newt);

			ConstructMoveToCopyToMenu();

			return newt;
		}

		public Wpf.Controls.TabItem NewTab(string Location, bool IsNavigate = false) {
			return NewTab(new ShellItem(Location), IsNavigate);
		}

		public void NewTab() {
			ShellItem DefPath;
			if (StartUpLocation.StartsWith("::") && !StartUpLocation.Contains(@"\"))
				DefPath = new ShellItem("shell:" + StartUpLocation);
			else
				try {
					DefPath = new ShellItem(StartUpLocation);
				}
				catch {
					DefPath = (ShellItem)KnownFolders.Libraries;
				}

			NewTab(DefPath, true);
		}

		/// <summary> Re-opens a previously closed tab using that tab's navigation log data then removes it from reopenabletabs. </summary>
		/// <param name="log"> The navigation log data from the previously closed tab. </param>
		public void ReOpenTab(NavigationLog log) {
			var Tab = NewTab(log.CurrentLocation, false);
			Tab.log.ImportData(log);
			reopenabletabs.Remove(log);
		}

		/*
		/// <summary>
		///     Add a new Header
		/// </summary>
		[Obsolete("Shouldn't we just use NewTab() ??")]
		public void AddTabItem() {
			var obj = new ShellItem(this.DefaultTabPath);
			obj.Thumbnail.CurrentSize = new Size(16, 16);
			obj.Thumbnail.FormatOption = BExplorer.Shell.Interop.ShellThumbnailFormatOption.IconOnly;
			obj.Thumbnail.RetrievalOption = BExplorer.Shell.Interop.ShellThumbnailRetrievalOption.Default;

			if (IsFixedSize)
				throw new InvalidOperationException("ItemsSource is Fixed Size");

			int i = this.SelectedIndex;

			// give an opertunity to cancel the adding of the tabitem
			CancelEventArgs c = new CancelEventArgs();
			if (TabItemAdding != null)
				TabItemAdding(this, c);

			if (c.Cancel)
				return;

			TabItem tabItem;

			// Using ItemsSource property
			if (ItemsSource != null) {
				IList list = (IList)ItemsSource;
				NewTabItemEventArgs n = new NewTabItemEventArgs();
				if (NewTabItem == null)
					throw new InvalidOperationException("You must implement the NewTabItem event to supply the item to be added to the tab control.");

				NewTabItem(this, n);
				if (n.Content == null)
					return;

				if (i == -1 || i == list.Count - 1 || AddNewTabToEnd)
					list.Add(n.Content);
				else
					list.Insert(++i, n.Content);

				tabItem = (TabItem)this.ItemContainerGenerator.ContainerFromItem(n.Content);
			}
			else {
				// Using Items Property
				tabItem = new TabItem {
					Header = obj.DisplayName,
					Icon = obj.Thumbnail.BitmapSource,
					ShellObject = obj,
				};

				if (i == -1 || i == this.Items.Count - 1 || AddNewTabToEnd)
					this.Items.Add(tabItem);
				else
					this.Items.Insert(++i, tabItem);
			}

			if (TabItemAdded != null)
				TabItemAdded(this, new TabItemEventArgs(tabItem));
		}
		*/

		/// <summary>
		/// Called by a child Header that wants to remove itself by clicking on the close button
		/// </summary>
		/// <param name="tabItem"></param>
		public void RemoveTabItem(TabItem tabItem) {
			if (IsFixedSize)
				throw new InvalidOperationException("ItemsSource is Fixed Size");

			// gives an opertunity to cancel the removal of the tabitem
			var c = new TabItemCancelEventArgs(tabItem);
			if (TabItemClosing != null)
				TabItemClosing(tabItem, c);

			if (c.Cancel)
				return;

			if (ItemsSource != null) {
				var list = ItemsSource as IList;
				object listItem = ItemContainerGenerator.ItemFromContainer(tabItem);
				if (listItem != null && list != null)
					list.Remove(listItem);
			}
			else
				this.Items.Remove(tabItem);

			if (TabItemClosed != null)
				TabItemClosed(this, new TabItemEventArgs(tabItem));
		}

		public void CloneTabItem(TabItem theTab) {
			int i = this.SelectedIndex;
			TabItem newt = new TabItem();
			newt.Header = theTab.Header;
			newt.Icon = theTab.Icon;
			newt.ShellObject = theTab.ShellObject;
			newt.ToolTip = theTab.ShellObject.ParsingName;
			newt.AllowDrop = true;
			newt.log.CurrentLocation = theTab.ShellObject;
			newt.SelectedItems = theTab.SelectedItems;
			newt.log.ImportData(theTab.log);
			if (i == -1 || i == this.Items.Count - 1 || AddNewTabToEnd)
				this.Items.Add(newt);
			else
				this.Items.Insert(++i, newt);

			ConstructMoveToCopyToMenu();
		}

		#endregion Tab Stuff

		static TabControl() {
			DefaultStyleKeyProperty.OverrideMetadata(typeof(TabControl), new FrameworkPropertyMetadata(typeof(TabControl)));
			TabStripPlacementProperty.AddOwner(typeof(TabControl), new FrameworkPropertyMetadata(Dock.Top, new PropertyChangedCallback(OnTabStripPlacementChanged)));
		}

		public TabControl() {
			Loaded +=
				delegate {
					SetAddNewButtonVisibility();
					SetTabItemsCloseButtonVisibility();
					IsUsingItemsSource = BindingOperations.IsDataBound(this, ItemsSourceProperty);

					if (IsUsingItemsSource && IsFixedSize)
						AllowAddNew = AllowDelete = false;
					this.MouseDoubleClick += TabControl_MouseDoubleClick;
				};
		}

		private void TabControl_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			this.NewTab();
		}

		/*
		 * Protected override methods
		 *
		 */

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
				ContextMenu cm = new ContextMenu { PlacementTarget = _toggleButton, Placement = PlacementMode.Bottom };

				// create a binding between the togglebutton's IsChecked Property
				// and the Context Menu's IsOpen Property
				Binding b = new Binding {
					Source = _toggleButton,
					Mode = BindingMode.TwoWay,
					Path = new PropertyPath(ToggleButton.IsCheckedProperty)
				};

				cm.SetBinding(ContextMenu.IsOpenProperty, b);

				_toggleButton.ContextMenu = cm;
				_toggleButton.Checked += DropdownButton_Checked;
			}

			ScrollViewer scrollViewer = this.Template.FindName("PART_ScrollViewer", this) as ScrollViewer;

			// set up event handlers for the RepeatButtons Click event
			RepeatButton repeatLeft = this.Template.FindName("PART_RepeatLeft", this) as RepeatButton;
			if (repeatLeft != null) {
				repeatLeft.Click += delegate {
					if (scrollViewer != null)
						scrollViewer.LineRight();
				};
			}

			RepeatButton repeatRight = this.Template.FindName("PART_RepeatRight", this) as RepeatButton;
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
			return new TabItem();
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

			if (e.Action == NotifyCollectionChangedAction.Add && SelectNewTabOnCreate) {
				TabItem tabItem = (TabItem)this.ItemContainerGenerator.ContainerFromItem(e.NewItems[e.NewItems.Count - 1]);
				SelectedItem = tabItem;

				TabPanel itemsHost = Helper.FindVirtualizingTabPanel(this);
				if (itemsHost != null)
					itemsHost.MakeVisible(tabItem, Rect.Empty);

				tabItem.Focus();
			}
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
			MenuItem mi = sender as MenuItem;
			if (mi == null) return;

			int index;
			// get the index of the Header from the manuitems Tag property
			bool b = int.TryParse(mi.Tag.ToString(), out index);

			if (b) {
				TabItem tabItem = GetTabItem(index);
				if (tabItem != null) {
					TabPanel itemsHost = Helper.FindVirtualizingTabPanel(this);
					if (itemsHost != null)
						itemsHost.MakeVisible(tabItem, Rect.Empty);

					tabItem.Focus();
				}
			}
		}

		public void CloseAllTabsButThis(TabItem tabItem) {
			foreach (TabItem tab in this.Items.OfType<TabItem>().ToArray()) {
				if (tab != tabItem) this.RemoveTabItem(tab);
			}
		}

		private void SetAddNewButtonVisibility() {
			if (this.Template == null)
				return;

			ButtonBase button = this.Template.FindName("PART_NewTabButton", this) as ButtonBase;
			if (button == null) return;

			if (IsFixedSize)
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

		internal IEnumerable GetItems() {
			return IsUsingItemsSource ? ItemsSource : Items;
		}

		internal int GetTabsCount() {
			if (BindingOperations.IsDataBound(this, ItemsSourceProperty)) {
				IList list = ItemsSource as IList;
				if (list != null)
					return list.Count;

				// ItemsSource is only an IEnumerable
				int i = 0;
				IEnumerator enumerator = ItemsSource.GetEnumerator();
				while (enumerator.MoveNext())
					i++;
				return i;
			}

			if (Items != null)
				return Items.Count;

			return 0;
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

		private IEnumerable InternalChildren() {
			IEnumerator enumerator = GetItems().GetEnumerator();
			while (enumerator.MoveNext()) {
				if (enumerator.Current is TabItem)
					yield return enumerator.Current;
				else
					yield return this.ItemContainerGenerator.ContainerFromItem(enumerator.Current) as TabItem;
			}
		}

	}
}