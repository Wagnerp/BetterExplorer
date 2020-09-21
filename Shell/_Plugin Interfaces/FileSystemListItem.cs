﻿using System.Runtime.CompilerServices;
using System.IO;
using BExplorer.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ThumbnailGenerator;
using Size = System.Drawing.Size;
using System.Text;
using System.Linq;
using System.Windows.Interop;

namespace BExplorer.Shell._Plugin_Interfaces {
  using System.Drawing;
  using System.Windows.Media;

  /// <summary>
  /// A representation of items on a standard physical/local file system
  /// </summary>
  public class FileSystemListItem : IListItemEx {

    #region Private Members

    /// <summary>The real item that is a wrapper for</summary>
    private ShellItem _Item { get; set; }

    #endregion Private Members

    #region IListItemEx Members

    public IntPtr ParentPIDL { get; set; }
    public IntPtr EnumPIDL { get; set; }

    /// <summary>The COM interface for this item</summary>
    public IShellItem ComInterface => this.ParentPIDL == IntPtr.Zero
      ? Shell32.SHCreateItemFromIDList(this.EnumPIDL, typeof(IShellItem).GUID)
      : Shell32.SHCreateItemWithParent(this.ParentPIDL, null, this.EnumPIDL, typeof(IShellItem).GUID);

    /// <summary>The text that represents the display name</summary>
    public String DisplayName {
      get { return this.GetDisplayName(SIGDN.NORMALDISPLAY); }
    }

    /// <summary>Does the current item need to be refreshed in the ShellListView</summary>
    public Boolean IsNeedRefreshing { get; set; }

    /// <summary>Assigned values but never used</summary>
    public Boolean IsInvalid { get; set; }

    public Boolean IsProcessed { get; set; }

    /// <summary>Changes how the item gets loaded</summary>
    public Boolean IsOnlyLowQuality { get; set; }

    public Boolean IsThumbnailLoaded { get; set; }

    public Boolean IsInitialised { get; set; }

    public Int32 OverlayIconIndex { get; set; }

    private IExtractIconPWFlags _IconType = IExtractIconPWFlags.GIL_PERCLASS;

    public IExtractIconPWFlags IconType {
      get { return this.GetIconType(); }
      set { this._IconType = value; }
    }

    public IExtractIconPWFlags GetIconType() {
      if (this.IsFolder) {
        IExtractIcon iextract = null;
        IShellFolder ishellfolder = null;
        StringBuilder str = null;
        IntPtr result;

        try {
          var guid = new Guid("000214fa-0000-0000-c000-000000000046");
          UInt32 res = 0;
          ishellfolder = this.Parent.GetIShellFolder();
          var pidls = new IntPtr[1] { Shell32.ILFindLastID(this.PIDL) };

          ishellfolder.GetUIObjectOf(IntPtr.Zero, 1, pidls, ref guid, res, out result);

          if (result == IntPtr.Zero) {
            pidls = null;
            return IExtractIconPWFlags.GIL_PERCLASS;
          }
          iextract = (IExtractIcon)Marshal.GetTypedObjectForIUnknown(result, typeof(IExtractIcon));
          str = new StringBuilder(512);
          var index = -1;
          IExtractIconPWFlags flags;
          iextract.GetIconLocation(IExtractIconUFlags.GIL_ASYNC, str, 512, out index, out flags);
          Marshal.ReleaseComObject(ishellfolder);
          Marshal.ReleaseComObject(iextract);
          return flags;
        } catch (Exception) {
          return 0;
        }
      } else {
        var value = this.GetPropertyValue(SystemProperties.PerceivedType, typeof(PerceivedType))?.Value;
        if (value != null) {
          var perceivedType = (PerceivedType)value;
          if (perceivedType == PerceivedType.Application) {
            return IExtractIconPWFlags.GIL_PERINSTANCE;
          } else {
            return IExtractIconPWFlags.GIL_PERCLASS;
          }
        }
      }
      return IExtractIconPWFlags.GIL_PERCLASS;
      //if (this.Extension.ToLowerInvariant() == ".exe" || this.Extension.ToLowerInvariant() == ".com" || this.Extension.ToLowerInvariant() == ".bat" || this.Extension.ToLowerInvariant() == ".msi" || this.Extension.ToLowerInvariant() == ".jar") {
      //  return IExtractIconPWFlags.GIL_PERINSTANCE;
      //}
      ////return IExtractIconPWFlags.GIL_PERCLASS;
      //if (this.Parent == null) {
      //  return 0;
      //}

      ////if (this.IsFolder) {
      //  IExtractIcon iextract = null;
      //  IShellFolder ishellfolder = null;
      //  StringBuilder str = null;
      //  IntPtr result;

      //  try {
      //    var guid = new Guid("000214fa-0000-0000-c000-000000000046");
      //    uint res = 0;
      //    ishellfolder = this.Parent.GetIShellFolder();
      //    var pidls = new IntPtr[1] { Shell32.ILFindLastID(this.PIDL) };

      //    ishellfolder.GetUIObjectOf(IntPtr.Zero, 1, pidls, ref guid, res, out result);

      //    if (result == IntPtr.Zero) {
      //      pidls = null;
      //      return IExtractIconPWFlags.GIL_PERCLASS;
      //    }
      //    iextract = (IExtractIcon)Marshal.GetTypedObjectForIUnknown(result, typeof(IExtractIcon));
      //    str = new StringBuilder(512);
      //    var index = -1;
      //    IExtractIconPWFlags flags;
      //    iextract.GetIconLocation(IExtractIconUFlags.GIL_ASYNC, str, 512, out index, out flags);
      //    return flags;
      //  } catch (Exception) {
      //    return 0;
      //  }
      ////} else {
      ////  return IExtractIconPWFlags.GIL_PERCLASS;
      ////}
    }

    public IntPtr ILPidl => Shell32.ILFindLastID(this.PIDL);

    public IntPtr PIDL {
      get {
        var comObject = this.ComInterface;
        var result = comObject == null ? IntPtr.Zero : Shell32.SHGetIDListFromObject(comObject);
        Marshal.FinalReleaseComObject(comObject);
        return result;
      }
    }

    public IntPtr AbsolutePidl {
      get {
        UInt32 attr;
        IntPtr pidl;
        Shell32.SHParseDisplayName(this.ParsingName, IntPtr.Zero, out pidl, 0, out attr);
        return pidl;
      }
    }

    /// <summary>Index of the ShieldedIcon</summary>
    public Int32 ShieldedIconIndex { get; set; }

    /// <summary>Is this item's icon loaded yet?</summary>
    public Boolean IsIconLoaded { get; set; }

    public String ParsingName {
      get { return this.GetDisplayName(SIGDN.DESKTOPABSOLUTEPARSING); }
    }

    /// <summary>The file system extension for this item</summary>
    public String Extension => Path.GetExtension(this.ParsingName);

    /// <summary>The file system path</summary>
    public String FileSystemPath => this.GetDisplayName(SIGDN.FILESYSPATH);

    /// <summary>
    /// Returns true if folder can be browsed
    /// </summary>
    public Boolean IsBrowsable => this.COM_Attribute_Check(SFGAO.BROWSABLE);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Boolean COM_Attribute_Check(SFGAO Check) {
      var comObject = this.ComInterface;
      SFGAO sfgao;
      comObject.GetAttributes(Check, out sfgao);
      Marshal.FinalReleaseComObject(comObject);
      this.Dispose();
      return (sfgao & Check) != 0;
    }

    /// <summary>Gets a value indicating whether this is a folder </summary>
    public Boolean IsFolder {
      get {
        var comObject = this.ComInterface;
        SFGAO sfgao;
        comObject.GetAttributes(SFGAO.FOLDER, out sfgao);
        SFGAO sfgao2;
        comObject.GetAttributes(SFGAO.STREAM, out sfgao2);
        Marshal.FinalReleaseComObject(comObject);
        return sfgao != 0 && sfgao2 == 0;
      }
    }

    /// <summary>Does this have folders?</summary>
    public Boolean HasSubFolders => this.COM_Attribute_Check(SFGAO.HASSUBFOLDER);

    /// <summary>Is this item normally hidden?</summary>
    public Boolean IsHidden => this.COM_Attribute_Check(SFGAO.HIDDEN);

    public Boolean IsFileSystem => this.COM_Attribute_Check(SFGAO.FILESYSTEM);

    public Boolean IsNetworkPath => Shell32.PathIsNetworkPath(this.ParsingName);

    /// <summary>Is current item represent a system drive?</summary>
    public Boolean IsDrive {
      get {
        try {
          return Directory.GetLogicalDrives().Contains(this.ParsingName) && Kernel32.GetDriveType(this.ParsingName) != DriveType.Network;
        } catch {
          return false;
        }
      }
    }

    public Boolean IsShared => this.COM_Attribute_Check(SFGAO.SHARE);

    public Boolean IsSlow => this.COM_Attribute_Check(SFGAO.ISSLOW);

    /// <summary>Is the parent a search folder?</summary>
    public Boolean IsParentSearchFolder { get; set; }

    public Int32 GroupIndex { get; set; }

    public Int32 RCWThread { get; set; }

    public IShellFolder IFolder { get; set; }

    private void Initialize_Helper(IntPtr folder, IntPtr lvHandle, Int32 index) {
      this.ParentPIDL = IntPtr.Zero;
      this.EnumPIDL = folder;
      //this.ParentHandle = lvHandle;
      this.OverlayIconIndex = -1;
      this.ShieldedIconIndex = -1;
    }

    private void Initialize_Helper2(IntPtr parent, IntPtr pidl, IntPtr lvHandle, Int32 index) {
      this.ParentPIDL = parent;
      this.EnumPIDL = pidl;
      //this.ParentHandle = lvHandle;
      this.OverlayIconIndex = -1;
      this.ShieldedIconIndex = -1;
    }

    public void Initialize(IntPtr lvHandle, IntPtr pidl, Int32 index) {
      this.Initialize_Helper(pidl, lvHandle, index);
    }

    public void InitializeWithParent(IntPtr parent, IntPtr lvHandle, IntPtr pidl, Int32 index) {
      this.Initialize_Helper2(parent, pidl, lvHandle, index);
    }

    public void InitializeWithShellItem(ShellSearchFolder item, IntPtr lvHandle, Int32 index) {
      this.Initialize_Helper(item.Pidl, lvHandle, index);
      this.searchFolder = item;
    }

    public ShellSearchFolder searchFolder { get; set; }

    public Dictionary<PROPERTYKEY, Object> ColumnValues { get; set; }

    public Int32 ItemIndex { get; set; }

    public IntPtr ParentHandle { get; set; }

    public static IListItemEx InitializeWithIShellItem(IntPtr lvHandle, IShellItem item) {
      var fsItem = new FileSystemListItem();
      fsItem.Initialize(lvHandle, new ShellItem(item).Pidl, 0);
      return fsItem;
    }

    public void Initialize(IntPtr lvHandle, String path, Int32 index) {
      var shellItem = Shell32.SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItem).GUID);
      this.ParentPIDL = IntPtr.Zero;
      this.EnumPIDL = Shell32.SHGetIDListFromObject(shellItem);
      Marshal.ReleaseComObject(shellItem);
      //this.ParentHandle = lvHandle;

      this.OverlayIconIndex = -1;
      this.ShieldedIconIndex = -1;
    }

    public void Initialize(IntPtr lvHandle, String path) {
      throw new NotImplementedException();
    }

    public void Initialize(IntPtr lvHandle, IntPtr pidl) {
      throw new NotImplementedException();
    }

    public FileSystemListItem() {
      this.GroupIndex = -1;
      this.ItemIndex = -1;
      this.IconIndex = -1;
      this.ColumnValues = new Dictionary<PROPERTYKEY, Object>();
    }

    public HResult NavigationStatus { get; set; }

    public Size IconSize { get; set; }

    public IEnumerable<IntPtr> GetItemsForCount(Boolean isEnumHidden) {
      var folder = this.GetIShellFolder();
      if (folder == null) {
        yield return IntPtr.Zero;
      }

      HResult navRes;
      var flags = SHCONTF.FOLDERS | SHCONTF.FASTITEMS | SHCONTF.NONFOLDERS | SHCONTF.ENABLE_ASYNC | SHCONTF.INIT_ON_FIRST_NEXT;
      if (isEnumHidden) {
        flags = SHCONTF.FOLDERS | SHCONTF.INCLUDEHIDDEN | SHCONTF.INCLUDESUPERHIDDEN | SHCONTF.FASTITEMS | SHCONTF.NONFOLDERS | SHCONTF.ENABLE_ASYNC | SHCONTF.INIT_ON_FIRST_NEXT;
      }
      var enumId = ShellItem.GetIEnumIDList(folder, flags, out navRes);
      this.NavigationStatus = navRes;
      UInt32 count;
      IntPtr pidl;

      if (enumId == null) {
        yield break;
      }

      var result = enumId.Next(1, out pidl, out count);
      while (result == HResult.S_OK) {
        yield return pidl;
        Shell32.ILFree(pidl);
        result = enumId.Next(1, out pidl, out count);
      }

      if (folder != null) {
        Marshal.ReleaseComObject(folder);
      }
      if (result != HResult.S_FALSE) {
        //Marshal.ThrowExceptionForHR((int)result);
      }
      yield break;
    }

    public IEnumerable<IListItemEx> GetContents(Boolean isEnumHidden) {
      var folder = this.GetIShellFolder();
      if (folder == null) {
        yield return null;
      }

      HResult navRes;
      var flags = SHCONTF.FOLDERS | SHCONTF.NONFOLDERS | SHCONTF.CHECKING_FOR_CHILDREN | SHCONTF.ENABLE_ASYNC;
      if (isEnumHidden) {
        flags = SHCONTF.FOLDERS | SHCONTF.INCLUDEHIDDEN | SHCONTF.INCLUDESUPERHIDDEN | SHCONTF.NONFOLDERS | SHCONTF.CHECKING_FOR_CHILDREN | SHCONTF.ENABLE_ASYNC;
      }

      var enumId = ShellItem.GetIEnumIDList(folder, flags, out navRes);
      this.NavigationStatus = navRes;
      UInt32 count;
      IntPtr pidl;
      if (enumId == null) {
        yield break;
      }

      var result = enumId.Next(1, out pidl, out count);
      var i = 0;
      while (result == HResult.S_OK) {
        //var sb = new StringBuilder(512);
        //Shell32.SHGetPathFromIDList(pidl, sb);
        //var ext = Path.GetExtension(sb.ToString());
        var fsi = new FileSystemListItem();
        try {
          fsi.InitializeWithParent(this.PIDL, this.ParentHandle, pidl, i++);
        } catch {
          continue;
        }
        fsi.IsParentSearchFolder = this.IsSearchFolder;
        fsi.Dispose();
        yield return fsi;
        //Shell32.ILFree(pidl);
        result = enumId.Next(1, out pidl, out count);
      }

      if (folder != null) {
        Marshal.ReleaseComObject(folder);
      }

      if (result != HResult.S_FALSE) {
        //Marshal.ThrowExceptionForHR((int)result);
      }

      //parentItem.Dispose();
      yield break;
    }

    public IEnumerator<IListItemEx> GetEnumerator() {
      var folder = this.GetIShellFolder();
      if (folder == null) {
        yield return null;
      }

      HResult navRes;
      var flags = SHCONTF.FOLDERS | SHCONTF.INCLUDEHIDDEN | SHCONTF.INCLUDESUPERHIDDEN | SHCONTF.FASTITEMS | SHCONTF.NONFOLDERS | SHCONTF.ENABLE_ASYNC | SHCONTF.INIT_ON_FIRST_NEXT;
      var enumId = ShellItem.GetIEnumIDList(folder, flags, out navRes);
      this.NavigationStatus = navRes;
      UInt32 count;
      IntPtr pidl;

      if (enumId == null) {
        yield break;
      }

      var result = enumId.Next(1, out pidl, out count);
      var i = 0;
      while (result == HResult.S_OK) {
        var fsi = new FileSystemListItem();
        try {
          fsi.InitializeWithParent(this.PIDL, this.ParentHandle, pidl, i++);
        } catch {
          continue;
        }

        fsi.IsParentSearchFolder = this.IsSearchFolder;
        fsi.Dispose();
        yield return fsi;
        result = enumId.Next(1, out pidl, out count);
      }

      if (folder != null) {
        Marshal.ReleaseComObject(folder);
      }

      if (result != HResult.S_FALSE) {
        //Marshal.ThrowExceptionForHR((int)result);
      }
      yield break;
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();

    public PropVariant GetPropertyValue(PROPERTYKEY pkey, Type type) {
      var pvar = new PropVariant();
      var comObject = this.ComInterface;
      var isi2 = (IShellItem2)comObject;
      if (isi2 == null) {
        return PropVariant.FromObject(null);
      }

      isi2.GetProperty(ref pkey, pvar);
      Marshal.ReleaseComObject(comObject);
      this.Dispose();
      return pvar;
    }

    public System.Drawing.Bitmap Thumbnail(Int32 size, ShellThumbnailFormatOption format, ShellThumbnailRetrievalOption source) => this._Item?.GetShellThumbnail(size, format, source);

    public BitmapSource ThumbnailBitmapSource => this.ThumbnailSource(16, ShellThumbnailFormatOption.IconOnly, ShellThumbnailRetrievalOption.Default);

    public BitmapSource ThumbnailSource(Int32 size, ShellThumbnailFormatOption format, ShellThumbnailRetrievalOption source) {
      var hBitmap = this.GetHBitmap(size, format == ShellThumbnailFormatOption.ThumbnailOnly, source == ShellThumbnailRetrievalOption.Default, format == ShellThumbnailFormatOption.Default);

      // return a System.Media.Imaging.BitmapSource
      // Use interop to create a BitmapSource from hBitmap.
      if (hBitmap == IntPtr.Zero) {
        return null;
      }

      var returnValue = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()).Clone();
      returnValue.Freeze();
      // delete HBitmap to avoid memory leaks
      Gdi32.DeleteObject(hBitmap);
      this.Dispose();
      return returnValue;
    }

    /// <summary>Is the current item a search folder?</summary>
    public Boolean IsSearchFolder {
      get {
        try {
          return (!this.ParsingName.StartsWith("::") && !this.IsFileSystem && !this.ParsingName.StartsWith(@"\\") && !this.ParsingName.Contains(":\\")) || this.ParsingName.EndsWith(".search-ms");
        } catch {
          return false;
        }
      }
    }

    /// <summary>The logical parent for this item</summary>
    public IListItemEx Parent {
      get {
        IShellItem item;
        var comObject = this.ComInterface;
        var result = comObject.GetParent(out item);
        Marshal.ReleaseComObject(comObject);
        if (result == HResult.S_OK) {
          var parent = new FileSystemListItem();
          parent.Initialize(this.ParentHandle, Shell32.SHGetIDListFromObject(item), 0);
          return parent;
        } else if (result == HResult.MK_E_NOOBJECT) {
          return null;
        } else {
          Marshal.ThrowExceptionForHR((Int32)result);
          return null;
        }
      }
    }

    public IShellFolder GetIShellFolder() {
      IntPtr res;
      try {
        var comObject = this.ComInterface;
        comObject.BindToHandler(IntPtr.Zero, BHID.SFObject, typeof(IShellFolder).GUID, out res);
        var iShellFolder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(res, typeof(IShellFolder));
        Marshal.ReleaseComObject(comObject);
        return iShellFolder;
      } catch {
        return null;
      }
    }

    public Boolean IsLink => this.COM_Attribute_Check(SFGAO.LINK);

    public String ToolTipText {
      get {
        IntPtr result;
        IQueryInfo queryInfo;
        IntPtr infoTipPtr;
        String infoTip;

        try {
          var relativePidl = this.ILPidl;
          this.Parent.GetIShellFolder().GetUIObjectOf(IntPtr.Zero, 1, new IntPtr[] { relativePidl }, typeof(IQueryInfo).GUID, 0, out result);
        } catch (Exception) {
          return string.Empty;
        }
        if (result == IntPtr.Zero) {
          return string.Empty;
        }

        queryInfo = (IQueryInfo)Marshal.GetTypedObjectForIUnknown(result, typeof(IQueryInfo));
        queryInfo.GetInfoTip(0x00000001 | 0x00000008, out infoTipPtr);
        infoTip = Marshal.PtrToStringUni(infoTipPtr);
        Ole32.CoTaskMemFree(infoTipPtr);
        return infoTip;
      }
    }

    /// <summary>Returns drive information</summary>
    public DriveInfo GetDriveInfo() => this.IsDrive || this.IsNetworkPath ? new DriveInfo(this.ParsingName) : null;

    /// <summary>Gets the item's BitmapSource</summary>
    public BitmapSource BitmapSource {
      get { return this.ThumbnailSource(48, ShellThumbnailFormatOption.Default, ShellThumbnailRetrievalOption.Default); }
    }

    public HResult ExtractAndDrawThumbnail(IntPtr hdc, UInt32 iconSize, out WTS_CACHEFLAGS flags, User32.RECT iconBounds, out Boolean retrieved, Boolean isHidden, Boolean isRefresh = false) {
      IThumbnailCache thumbCache = null;

      if (this.ComInterface != null) {

        var IID_IUnknown = new Guid("00000000-0000-0000-C000-000000000046");
        var CLSID_LocalThumbnailCache = new Guid("50EF4544-AC9F-4A8E-B21B-8A26180DB13F");

        IntPtr cachePointer;
        Ole32.CoCreateInstance(ref CLSID_LocalThumbnailCache, IntPtr.Zero, Ole32.CLSCTX.INPROC, ref IID_IUnknown, out cachePointer);

        thumbCache = (IThumbnailCache)Marshal.GetObjectForIUnknown(cachePointer);
      }

      var res = HResult.S_OK;
      ISharedBitmap bmp = null;
      flags = WTS_CACHEFLAGS.WTS_DEFAULT;
      var thumbId = default(WTS_THUMBNAILID);
      try {
        retrieved = false;
        res = thumbCache.GetThumbnail(this._Item.ComInterface, iconSize,
          isRefresh ? (WTS_FLAGS.WTS_FORCEEXTRACTION | WTS_FLAGS.WTS_SCALETOREQUESTEDSIZE) : (WTS_FLAGS.WTS_INCACHEONLY | WTS_FLAGS.WTS_SCALETOREQUESTEDSIZE), out bmp, flags, thumbId);
        var hBitmap = IntPtr.Zero;
        if (bmp != null) {
          bmp.GetSharedBitmap(out hBitmap);
          retrieved = true;

          Int32 width;
          Int32 height;
          Gdi32.ConvertPixelByPixel(hBitmap, out width, out height);
          Gdi32.NativeDraw(hdc, hBitmap, iconBounds.Left + (iconBounds.Right - iconBounds.Left - width) / 2, iconBounds.Top + (iconBounds.Bottom - iconBounds.Top - height) / 2, width, height, isHidden);
          Gdi32.DeleteObject(hBitmap);
        }
      } finally {
        if (bmp != null) {
          Marshal.ReleaseComObject(bmp);
        }
      }
      return res;
    }

    public IntPtr GetHBitmap(Int32 iconSize, Boolean isThumbnail, Boolean isForce = false, Boolean isBoth = false) {
      var options = ThumbnailOptions.None;
      if (isThumbnail) {
        options = ThumbnailOptions.ThumbnailOnly;
        if (!isForce) {
          options |= ThumbnailOptions.InCacheOnly;
        }
      } else {
        if (!isBoth) {
          options |= ThumbnailOptions.IconOnly;
        }
      }
      return WindowsThumbnailProvider.GetThumbnail(this.PIDL, iconSize, iconSize, options);
    }

    public static FileSystemListItem ToFileSystemItem(IntPtr parentHandle, String path) {
      var fsItem = new FileSystemListItem();
      fsItem.Initialize(parentHandle, path, 0);
      return fsItem;
    }

    public static FileSystemListItem ToFileSystemItem(IntPtr parentHandle, IntPtr pidl) {
      var fsItem = new FileSystemListItem();
      fsItem.Initialize(parentHandle, pidl, 0);
      return fsItem;
    }

    public static FileSystemListItem ToFileSystemItem(IntPtr parentHandle, ShellSearchFolder folder) {
      var fsItem = new FileSystemListItem();
      fsItem.InitializeWithShellItem(folder, parentHandle, 0);
      return fsItem;
    }

    public String GetDisplayName(SIGDN type) {
      try {
        var comInterface = this.ComInterface;
        var resultPtr = comInterface.GetDisplayName(type);
        var result = Marshal.PtrToStringUni(resultPtr);
        Marshal.FreeCoTaskMem(resultPtr);
        Marshal.FinalReleaseComObject(comInterface);
        this.Dispose();
        return result;
      } catch {
        return "Search.search-ms";
      }
    }

    public IExtractIconPWFlags GetShield() {
      IExtractIcon iextract = null;
      IShellFolder ishellfolder = null;
      StringBuilder str = null;
      IntPtr result;

      try {
        var guid = new Guid("000214fa-0000-0000-c000-000000000046");
        UInt32 res = 0;
        ishellfolder = this.Parent.GetIShellFolder();
        var pidls = new IntPtr[1];
        pidls[0] = Shell32.ILFindLastID(this.PIDL);
        ishellfolder.GetUIObjectOf(IntPtr.Zero, 1, pidls, ref guid, res, out result);
        iextract = (IExtractIcon)Marshal.GetTypedObjectForIUnknown(result, typeof(IExtractIcon));
        str = new StringBuilder(512);
        var index = -1;
        IExtractIconPWFlags flags;
        iextract.GetIconLocation(IExtractIconUFlags.GIL_CHECKSHIELD, str, 512, out index, out flags);
        pidls = null;
        return flags;
      } catch {
        return 0;
      }
    }

    public Int32 GetSystemImageListIndex(IntPtr pidl, ShellIconType type, ShellIconFlags flags) {
      var options = SHGetFileInfoOptions.Icon | SHGetFileInfoOptions.SysIconIndex | SHGetFileInfoOptions.OverlayIndex | SHGetFileInfoOptions.Pidl | SHGetFileInfoOptions.AddOverlays | (SHGetFileInfoOptions)type | (SHGetFileInfoOptions)flags;
      var shfi = new SHFILEINFO();
      var shfiSize = Marshal.SizeOf(shfi.GetType());
      IntPtr retVal = Win32Api.SHGetFileInfo(pidl, FileAttributes.None, ref shfi, shfiSize, options);
      if (shfi.hIcon != IntPtr.Zero) {
        Win32Api.DestroyIcon(shfi.hIcon);
      }

      if (retVal.Equals(IntPtr.Zero)) {
        return 0;
      } else {
        return shfi.iIcon;
      }
    }

    public Boolean RefreshThumb(Int32 iconSize, out WTS_CACHEFLAGS flags) {
      flags = WTS_CACHEFLAGS.WTS_DEFAULT;
      return true;
      //ISharedBitmap bmp = null;
      //WTS_CACHEFLAGS cacheFlags = WTS_CACHEFLAGS.WTS_DEFAULT;
      //WTS_THUMBNAILID thumbId = new WTS_THUMBNAILID();
      //Boolean result = false;
      //try {
      //  if (ThumbnailCache.GetThumbnail(this.shellItemNative, iconSize, WTS_FLAGS.WTS_FORCEEXTRACTION | WTS_FLAGS.WTS_SCALETOREQUESTEDSIZE, out bmp, cacheFlags, thumbId) != HResult.WTS_E_FAILEDEXTRACTION) {
      //    result = true;
      //  }
      //} finally {
      //  if (bmp != null)
      //    Marshal.ReleaseComObject(bmp);
      //}
      //flags = cacheFlags;
      //return result;
    }

    public Int32 IconIndex { get; set; }

    public Int32 GetUniqueID() => this.ParsingName.GetHashCode();

    public Int32[] cColumns { get; set; }

    public Boolean IsRCWSet { get; set; }

    public IListItemEx Clone(Boolean isHardCloning = false) {
      if (isHardCloning) {
        var newObj = ToFileSystemItem(this.ParentHandle, this.ParsingName.ToShellParsingName());
        this.Dispose();
        return newObj;
      }
      return ToFileSystemItem(this.ParentHandle, this.PIDL);
    }

    #endregion IListItemEx Members

    #region IEquatable<IListItemEx> Members

    public Boolean Equals(IListItemEx other) => other == null ? false : other.ParsingName.Equals(this.ParsingName, StringComparison.InvariantCultureIgnoreCase);


    #endregion IEquatable<IListItemEx> Members

    #region IEqualityComparer<IListItemEx> Members

    public Boolean Equals(IListItemEx x, IListItemEx y) => x.Equals(y);

    public Int32 GetHashCode(IListItemEx obj) => 0;


    #endregion IEqualityComparer<IListItemEx> Members

    #region IDisposable Members

    public void Dispose() {
      this._Item?.Dispose();
      Marshal.ReleaseComObject(this.ComInterface);
    }

    #endregion IDisposable Members
  }
}