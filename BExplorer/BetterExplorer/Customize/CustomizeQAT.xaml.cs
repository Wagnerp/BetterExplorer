﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Fluent;

namespace BetterExplorer {

  /// <summary> Interaction logic for CustomizeQAT.xaml </summary>
  public partial class CustomizeQAT : Window {
    public MainWindow MainForm;

    #region Helpers

    private RibbonItemListDisplay GetRibbonItemListDisplay(IRibbonControl item) {
      var rils = new RibbonItemListDisplay() {
        SourceControl = item,
        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
        Header = (item.Header as string),
        ItemName = (item as FrameworkElement).Name
      };

      if (item.Icon != null) {
        if (item.Icon is String)
          rils.Icon = new BitmapImage(new Uri(@"/BetterExplorer;component/" + item.Icon.ToString(), UriKind.Relative));
        else
          rils.Icon = (item.Icon as Image).Source;
      }

      if (item is Fluent.DropDownButton || item is Fluent.SplitButton || item is Fluent.InRibbonGallery) {
        rils.ShowMenuArrow = true;
      } else if (item is Fluent.CheckBox) {
        rils.ShowCheck = true;
      }

      return rils;
    }

    private void RefreshQATDialog(Ribbon ribbon) {
      List<IRibbonControl> NonQATButtons = (from Tab in ribbon.Tabs
                                            from Group in Tab.Groups
                                            from Item in Group.Items.OfType<IRibbonControl>()
                                            where !(ribbon.IsInQuickAccessToolBar(Item as UIElement))
                                            orderby Item.Header
                                            select Item).ToList();

      foreach (IRibbonControl item in NonQATButtons) {
        AllControls.Items.Add(GetRibbonItemListDisplay(item));
      }

      #region DO NOT DELETE (yet) [From: Aaron Campf]
      var AllMenuItems = MainForm.TheRibbon.QuickAccessItems.Select(x => x.Target).ToList();
      var Controls = (from control in MainForm.TheRibbon.QuickAccessItems
                      select control as Control into newControl
                      where !AllMenuItems.Any(x => x.Uid == newControl.Uid)
                      select newControl).ToList();
      #endregion

      AllMenuItems.AddRange(Controls);
      //Here add visible elements since we want to show in that dialog only visible elements into the QAT.
      //Maybe have to find a way to show all elements even not visible and do some handling to display them properly
      foreach (var item in MainForm.TheRibbon.QuickAccessItems.Cast<IRibbonControl>()) {
        QATControls.Items.Add(GetRibbonItemListDisplay(item));
      }
    }

    private void AddToList(RibbonItemListDisplay source, bool qatlist = true) {
      if (qatlist) {
        this.QATControls.Items.Add(GetRibbonItemListDisplay(source.SourceControl));
      } else {
        this.AllControls.Items.Add(GetRibbonItemListDisplay(source.SourceControl));
      }
    }

    private void CheckAgainstList() {
      var QATControlsNames = this.QATControls.Items.OfType<RibbonItemListDisplay>().Select(_ => _.ItemName).ToArray();
      foreach (var item in this.AllControls.Items.OfType<RibbonItemListDisplay>().Where(_ => QATControlsNames.Contains(_.ItemName)).ToArray()) {
        this.AllControls.Items.Remove(item);
      }
    }

    #endregion Helpers

    #region Buttons

    private void btnAdd_Click(object sender, RoutedEventArgs e) {
      int sel = AllControls.SelectedIndex;
      RibbonItemListDisplay item = AllControls.SelectedValue as RibbonItemListDisplay;
      AllControls.Items.Remove(item);
      AddToList(item, true);

      CheckAgainstList();
      if (sel != 0) {
        AllControls.SelectedIndex = sel - 1;
      } else if (AllControls.Items.Count != 0) {
        AllControls.SelectedIndex = 0;
      } else {
        btnRemove.IsEnabled = true;
        btnAdd.IsEnabled = false;
      }
    }

    private void btnRemove_Click(object sender, RoutedEventArgs e) {
      int sel = QATControls.SelectedIndex;
      QATControls.Items.Remove(QATControls.SelectedValue as RibbonItemListDisplay);
      this.AllControls.Items.Clear();
      foreach (IRibbonControl thing in from Tab in MainForm.TheRibbon.Tabs from Group in Tab.Groups from Item in Group.Items.OfType<IRibbonControl>() select Item) {
        this.AllControls.Items.Add(GetRibbonItemListDisplay(thing));
      }

      CheckAgainstList();
      if (sel != 0) {
        QATControls.SelectedIndex = sel - 1;
      } else if (QATControls.Items.Count != 0) {
        QATControls.SelectedIndex = 0;
      } else {
        btnRemove.IsEnabled = false;
        btnAdd.IsEnabled = true;
      }
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e) {
      this.Close();
    }

    private void btnMoveUp_Click(object sender, RoutedEventArgs e) {
      if (QATControls.SelectedIndex != 0) {
        int sel = QATControls.SelectedIndex;
        Object oItem = QATControls.Items.GetItemAt(sel);
        QATControls.Items.RemoveAt(sel);
        QATControls.Items.Insert(sel - 1, oItem);
        QATControls.SelectedIndex = QATControls.Items.IndexOf(oItem);
        QATControls.ScrollIntoView(QATControls.SelectedItem);
      }
    }

    private void btnMoveDown_Click(object sender, RoutedEventArgs e) {
      if (QATControls.SelectedIndex != QATControls.Items.Count - 1) {
        int sel = QATControls.SelectedIndex;
        Object oItem = QATControls.Items.GetItemAt(sel);
        QATControls.Items.RemoveAt(sel);
        QATControls.Items.Insert(sel + 1, oItem);
        QATControls.SelectedIndex = QATControls.Items.IndexOf(oItem);
        QATControls.ScrollIntoView(QATControls.SelectedItem);
      }
    }

    private void btnApply_Click(object sender, RoutedEventArgs e) {
      MainForm.TheRibbon.ClearQuickAccessToolBar();
      Dictionary<string, IRibbonControl> items = (from Tab in MainForm.TheRibbon.Tabs
                                                  from Group in Tab.Groups
                                                  from Item in Group.Items.OfType<IRibbonControl>()
                                                  select Item).
                            ToDictionary(_ => (_ as FrameworkElement).Name, _ => _);

      foreach (string item in from Item in this.QATControls.Items.Cast<RibbonItemListDisplay>() select Item.ItemName) {
        IRibbonControl ctrl;
        if (items.TryGetValue(item, out ctrl)) {
          MainForm.TheRibbon.AddToQuickAccessToolBar(ctrl as UIElement);
        }
      }
    }

    private void btnOkay_Click(object sender, RoutedEventArgs e) {
      btnApply_Click(sender, e);
      this.Close();
    }

    #endregion Buttons

    private CustomizeQAT() {
      InitializeComponent();
    }

    public static void Open(MainWindow mainWindow, Ribbon ribbon) {
      var qal = new CustomizeQAT();
      qal.Owner = mainWindow;
      qal.MainForm = mainWindow;
      qal.RefreshQATDialog(ribbon);
      qal.ShowDialog();
    }
  }
}