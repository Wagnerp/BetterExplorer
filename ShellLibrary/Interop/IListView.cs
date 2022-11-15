﻿using System;
using System.Runtime.InteropServices;

namespace BExplorer.Shell.Interop {
  [ComImport]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid("E5B16AF2-3990-4681-A609-1F060CD14269")]
  public interface IListView  {
    void GetWindow(out IntPtr phwnd);

    void ContextSensitiveHelp([In, MarshalAs(UnmanagedType.Bool)] bool fEnterMode);

    void GetImageList(int index, out IntPtr iList);

    void SetImageList(int index, IntPtr iList, out IntPtr iListDest);

    void GetBackgroundColor(out IntPtr colorref);

    void SetBackgroundColor(IntPtr colorref);

    void GetTextColor(out IntPtr colorref);

    void SetTextColor(IntPtr colorref);

    void GetTextBackgroundColor(out IntPtr colorref);

    void SetTextBackgroundColor(IntPtr colorref);

    void GetHotLightColor(out IntPtr colorref);

    void SetHotLightColor(IntPtr colorref);

    void GetItemCount(out int count);

    void SetItemCount(int count, uint p);

    HResult GetItem(out IntPtr item);

    HResult SetItem(IntPtr item);

    HResult GetItemState(int iItem, LVIF mask, LVIS stateMask, out LVIS state);

    [PreserveSig]
    HResult SetItemState(int iItem, LVIF mask, LVIS stateMask, LVIS state);

    HResult GetItemText(int a, int b, out IntPtr c, int d);

    HResult SetItemText(int a, int b, IntPtr c);

    void GetBackgroundImage(out IntPtr bitmap);

    void SetBackgroundImage(IntPtr bitmap);

    void GetFocusedColumn(out int col);

    void SetSelectionFlags(ulong a, ulong b);

    HResult GetSelectedColumn(out int col);

    HResult SetSelectedColumn(int col);

    HResult GetView(out uint view);

    HResult SetView(uint view);

    HResult InsertItem(IntPtr item, out int index);

    HResult DeleteItem(int index);

    HResult DeleteAllItems();

    HResult UpdateItem(int index);

    HResult GetItemRect(LVITEMINDEX index, int a, out User32.RECT rect);

    HResult GetSubItemRect(LVITEMINDEX index, int a, int b, out User32.RECT rect);

    HResult HitTestSubItem(LVHITTESTINFO info);

    HResult GetIncrSearchString(IntPtr a, int b, out int c);

    HResult GetItemSpacing(bool a, out int b, out int c);

    HResult SetIconSpacing(int a, int b, out int c, out int d);

    HResult GetNextItem(LVITEMINDEX index, ulong flags, out LVITEMINDEX result);

    HResult FindItem(LVITEMINDEX index, IntPtr info, out LVITEMINDEX item);

    HResult GetSelectionMark(out LVITEMINDEX mark);

    HResult SetSelectionMark(LVITEMINDEX index, out LVITEMINDEX result);

    HResult GetItemPosition(LVITEMINDEX index, out POINT position);

    HResult SetItemPosition(int a, POINT p);

    HResult ScrollView(int a, int b);

    [PreserveSig]
    HResult EnsureItemVisible(LVITEMINDEX item, Boolean b);

    HResult EnsureSubItemVisible(LVITEMINDEX item, int a);

    HResult EditSubItem(LVITEMINDEX item, int a);

    HResult RedrawItems(int a, int b);

    HResult ArrangeItems(int a);

    HResult RecomputeItems(int a);

    HResult GetEditControl(out IntPtr handle);

    [PreserveSig]
    HResult EditLabel(LVITEMINDEX index, IntPtr a, out IntPtr handle);

    HResult EditGroupLabel(int a);

    HResult CancelEditLabel();

    HResult GetEditItem(out LVITEMINDEX item, out int a);

    HResult HitTest(ref LVHITTESTINFO result);

    HResult GetStringWidth(IntPtr a, out int b);

    HResult GetColumn(int a, out IntPtr col);

    HResult SetColumn(int a, ref IntPtr col);

    HResult GetColumnOrderArray(int a, out IntPtr b);

    HResult SetColumnOrderArray(int a, ref int[] b);

    HResult GetHeaderControl(out IntPtr header);

    HResult InsertColumn(int a, ref IntPtr b, out int c);

    HResult DeleteColumn(int a);

    HResult CreateDragImage(int a, POINT b, out IntPtr c);

    HResult GetViewRect(out User32.RECT rect);

    HResult GetClientRect(Boolean a, out User32.RECT b);

    HResult GetColumnWidth(int iSubitem, out int width);
    [PreserveSig]
    HResult SetColumnWidth(int a, int b);

    HResult GetCallbackMask(out long a);

    HResult SetCallbackMask(out long b);

    HResult GetTopIndex(out int a);

    HResult GetCountPerPage(out int a);

    HResult GetOrigin(out POINT p);

    HResult GetSelectedCount(out int a);

    HResult SortItems(bool a, IntPtr b, IntPtr c);

    HResult GetExtendedStyle(out IntPtr s);

    HResult SetExtendedStyle(long a, long b, out long c);

    HResult GetHoverTime(out uint a);

    HResult SetHoverTime(uint a, out uint b);

    HResult GetToolTip(out IntPtr a);

    HResult SetToolTip(IntPtr a, out IntPtr b);

    HResult GetHotItem(out LVITEMINDEX a);

    HResult SetHotItem(LVITEMINDEX a, out LVITEMINDEX b);

    HResult GetHotCursor(out IntPtr a);

    HResult SetHotCursor(IntPtr a, out IntPtr b);

    HResult ApproximateViewRect(int a, out int b, out int c);

    HResult SetRangeObject(int a, out IntPtr b);

    HResult GetWorkAreas(int a, out User32.RECT b);

    HResult SetWorkAreas(int a, ref User32.RECT b);

    HResult GetWorkAreaCount(out IntPtr a);

    HResult ResetEmptyText();

    HResult EnableGroupView(int a);

    HResult IsGroupViewEnabled(out Boolean result);

    HResult SortGroups(IntPtr a, IntPtr b);

    HResult GetGroupInfo(int a, int b, out IntPtr c);

    HResult SetGroupInfo(int a, int b, IntPtr c);

    HResult GetGroupRect(bool a, int b, int c, out User32.RECT d);

    HResult GetGroupState(int a, long b, out long c);

    HResult HasGroup(int a, out bool b);

    HResult InsertGroup(int index, LVGROUP2 collumn,out int position);

    HResult RemoveGroup(int index);

    HResult InsertGroupSorted(IntPtr a, out int b);

    HResult GetGroupMetrics(out IntPtr a);

    HResult SetGroupMetrics(IntPtr a);

    HResult RemoveAllGroups();

    HResult GetFocusedGroup(out int a);

    HResult GetGroupCount(out int a);

    HResult SetOwnerDataCallback(IntPtr callback);

    HResult GetTileViewInfo(out LVTILEVIEWINFO a);

    HResult SetTileViewInfo(ref LVTILEVIEWINFO a);

    HResult GetTileInfo(out IntPtr a);

    HResult SetTileInfo(IntPtr a);

    HResult GetInsertMark(out IntPtr a);

    HResult SetInsertMark(IntPtr a);

    HResult GetInsertMarkRect(out User32.RECT a);

    HResult GetInsertMarkColor(out IntPtr a);

    HResult SetInsertMarkColor(IntPtr a, out IntPtr b);

    HResult HitTestInsertMark(POINT a, out IntPtr b);

    HResult SetInfoTip(IntPtr a);

    HResult GetOutlineColor(out IntPtr a);

    HResult SetOutlineColor(IntPtr a, out IntPtr b);

    HResult GetFrozenItem(out int a);

    HResult SetFrozenItem(int a, int b);

    HResult GetFrozenSlot(out User32.RECT a);

    HResult SetFrozenSlot(int a, ref POINT p);

    HResult GetViewMargin(out User32.RECT a);

    HResult SetViewMargin(ref User32.RECT a);

    HResult SetKeyboardSelected(LVITEMINDEX index);

    HResult MapIndexToId(int a, out int b);

    HResult MapIdToIndex(int a, out int b);

    HResult IsItemVisible(LVITEMINDEX a, out bool b);
    HResult EnableAlphaShadow(bool b);

    HResult GetGroupSubsetCount(out int a);

    HResult SetGroupSubsetCount(int a);

    HResult GetVisibleSlotCount(out int a);

    HResult GetColumnMargin(ref User32.RECT a);

    HResult SetSubItemCallback(IntPtr a);

    HResult GetVisibleItemRange(out LVITEMINDEX a, out LVITEMINDEX b);

    HResult SetTypeAheadFlags(uint a, uint b);
  }
}
