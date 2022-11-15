﻿// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using WPFUI.Common;

// ReSharper disable InconsistentNaming

namespace WPFUI.Win32 {
  /// <summary>
  /// This header is used by multiple technologies.
  /// </summary>
  internal static class User32 {
    /// <summary>
    /// DWM window accent state.
    /// </summary>
    public enum ACCENT_STATE {
      ACCENT_DISABLED = 0,
      ACCENT_ENABLE_GRADIENT = 1,
      ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
      ACCENT_ENABLE_BLURBEHIND = 3,
      ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
      ACCENT_INVALID_STATE = 5
    }

    /// <summary>
    /// DWM window attributes.
    /// </summary>
    public enum WINCOMPATTR {
      WCA_UNDEFINED = 0,
      WCA_NCRENDERING_ENABLED = 1,
      WCA_NCRENDERING_POLICY = 2,
      WCA_TRANSITIONS_FORCEDISABLED = 3,
      WCA_ALLOW_NCPAINT = 4,
      WCA_CAPTION_BUTTON_BOUNDS = 5,
      WCA_NONCLIENT_RTL_LAYOUT = 6,
      WCA_FORCE_ICONIC_REPRESENTATION = 7,
      WCA_EXTENDED_FRAME_BOUNDS = 8,
      WCA_HAS_ICONIC_BITMAP = 9,
      WCA_THEME_ATTRIBUTES = 10,
      WCA_NCRENDERING_EXILED = 11,
      WCA_NCADORNMENTINFO = 12,
      WCA_EXCLUDED_FROM_LIVEPREVIEW = 13,
      WCA_VIDEO_OVERLAY_ACTIVE = 14,
      WCA_FORCE_ACTIVEWINDOW_APPEARANCE = 15,
      WCA_DISALLOW_PEEK = 16,
      WCA_CLOAK = 17,
      WCA_CLOAKED = 18,
      WCA_ACCENT_POLICY = 19,
      WCA_FREEZE_REPRESENTATION = 20,
      WCA_EVER_UNCLOAKED = 21,
      WCA_VISUAL_OWNER = 22,
      WCA_HOLOGRAPHIC = 23,
      WCA_EXCLUDED_FROM_DDA = 24,
      WCA_PASSIVEUPDATEMODE = 25,
      WCA_USEDARKMODECOLORS = 26,
      WCA_CORNER_STYLE = 27,
      WCA_PART_COLOR = 28,
      WCA_DISABLE_MOVESIZE_FEEDBACK = 29,
      WCA_LAST = 30
    }

    /// <summary>
    /// The following are the window styles. After the window has been created, these styles cannot be modified, except as noted.
    /// </summary>
    public enum WS {
      /// <summary>
      /// The window has a maximize button. Cannot be combined with the WS_EX_CONTEXTHELP style. The WS_SYSMENU style must also be specified.
      /// </summary>
      MAXIMIZEBOX = 0x10000,

      /// <summary>
      /// The window has a minimize button. Cannot be combined with the WS_EX_CONTEXTHELP style. The WS_SYSMENU style must also be specified.
      /// </summary>
      MINIMIZEBOX = 0x20000,

      /// <summary>
      /// The window is an overlapped window. Same as the WS_TILEDWINDOW style.
      /// </summary>
      SIZEBOX = 0x40000,

      /// <summary>
      /// The window has a window menu on its title bar. The WS_CAPTION style must also be specified.
      /// </summary>
      SYSMENU = 0x80000
    }

    /// <summary>
    /// Window message values, WM_*
    /// </summary>
    public enum WM {
#pragma warning disable CS1591
      NULL = 0x0000,
      CREATE = 0x0001,
      DESTROY = 0x0002,
      MOVE = 0x0003,
      SIZE = 0x0005,
      ACTIVATE = 0x0006,
      SETFOCUS = 0x0007,
      KILLFOCUS = 0x0008,
      ENABLE = 0x000A,
      SETREDRAW = 0x000B,
      SETTEXT = 0x000C,
      GETTEXT = 0x000D,
      GETTEXTLENGTH = 0x000E,
      PAINT = 0x000F,
      CLOSE = 0x0010,
      QUERYENDSESSION = 0x0011,
      QUIT = 0x0012,
      QUERYOPEN = 0x0013,
      ERASEBKGND = 0x0014,
      SYSCOLORCHANGE = 0x0015,
      SHOWWINDOW = 0x0018,
      CTLCOLOR = 0x0019,
      WININICHANGE = 0x001A,
      SETTINGCHANGE = 0x001A,
      ACTIVATEAPP = 0x001C,
      SETCURSOR = 0x0020,
      MOUSEACTIVATE = 0x0021,
      CHILDACTIVATE = 0x0022,
      QUEUESYNC = 0x0023,
      GETMINMAXINFO = 0x0024,

      MEASUREITEM = 0x002C,

      WINDOWPOSCHANGING = 0x0046,
      WINDOWPOSCHANGED = 0x0047,

      CONTEXTMENU = 0x007B,
      STYLECHANGING = 0x007C,
      STYLECHANGED = 0x007D,
      DISPLAYCHANGE = 0x007E,
      GETICON = 0x007F,
      SETICON = 0x0080,
      NCCREATE = 0x0081,
      NCDESTROY = 0x0082,
      NCCALCSIZE = 0x0083,
      NCHITTEST = 0x0084,
      NCPAINT = 0x0085,
      NCACTIVATE = 0x0086,
      GETDLGCODE = 0x0087,
      SYNCPAINT = 0x0088,
      NCMOUSEMOVE = 0x00A0,
      NCLBUTTONDOWN = 0x00A1,
      NCLBUTTONUP = 0x00A2,
      NCLBUTTONDBLCLK = 0x00A3,
      NCRBUTTONDOWN = 0x00A4,
      NCRBUTTONUP = 0x00A5,
      NCRBUTTONDBLCLK = 0x00A6,
      NCMBUTTONDOWN = 0x00A7,
      NCMBUTTONUP = 0x00A8,
      NCMBUTTONDBLCLK = 0x00A9,

      SYSKEYDOWN = 0x0104,
      SYSKEYUP = 0x0105,
      SYSCHAR = 0x0106,
      SYSDEADCHAR = 0x0107,
      COMMAND = 0x0111,
      SYSCOMMAND = 0x0112,

      MOUSEMOVE = 0x0200,
      LBUTTONDOWN = 0x0201,
      LBUTTONUP = 0x0202,
      LBUTTONDBLCLK = 0x0203,
      RBUTTONDOWN = 0x0204,
      RBUTTONUP = 0x0205,
      RBUTTONDBLCLK = 0x0206,
      MBUTTONDOWN = 0x0207,
      MBUTTONUP = 0x0208,
      MBUTTONDBLCLK = 0x0209,
      MOUSEWHEEL = 0x020A,
      XBUTTONDOWN = 0x020B,
      XBUTTONUP = 0x020C,
      XBUTTONDBLCLK = 0x020D,
      MOUSEHWHEEL = 0x020E,
      PARENTNOTIFY = 0x0210,

      CAPTURECHANGED = 0x0215,
      POWERBROADCAST = 0x0218,
      DEVICECHANGE = 0x0219,

      ENTERSIZEMOVE = 0x0231,
      EXITSIZEMOVE = 0x0232,

      IME_SETCONTEXT = 0x0281,
      IME_NOTIFY = 0x0282,
      IME_CONTROL = 0x0283,
      IME_COMPOSITIONFULL = 0x0284,
      IME_SELECT = 0x0285,
      IME_CHAR = 0x0286,
      IME_REQUEST = 0x0288,
      IME_KEYDOWN = 0x0290,
      IME_KEYUP = 0x0291,

      NCMOUSELEAVE = 0x02A2,

      TABLET_DEFBASE = 0x02C0,
      //WM_TABLET_MAXOFFSET = 0x20,

      TABLET_ADDED = TABLET_DEFBASE + 8,
      TABLET_DELETED = TABLET_DEFBASE + 9,
      TABLET_FLICK = TABLET_DEFBASE + 11,
      TABLET_QUERYSYSTEMGESTURESTATUS = TABLET_DEFBASE + 12,

      CUT = 0x0300,
      COPY = 0x0301,
      PASTE = 0x0302,
      CLEAR = 0x0303,
      UNDO = 0x0304,
      RENDERFORMAT = 0x0305,
      RENDERALLFORMATS = 0x0306,
      DESTROYCLIPBOARD = 0x0307,
      DRAWCLIPBOARD = 0x0308,
      PAINTCLIPBOARD = 0x0309,
      VSCROLLCLIPBOARD = 0x030A,
      SIZECLIPBOARD = 0x030B,
      ASKCBFORMATNAME = 0x030C,
      CHANGECBCHAIN = 0x030D,
      HSCROLLCLIPBOARD = 0x030E,
      QUERYNEWPALETTE = 0x030F,
      PALETTEISCHANGING = 0x0310,
      PALETTECHANGED = 0x0311,
      HOTKEY = 0x0312,
      PRINT = 0x0317,
      PRINTCLIENT = 0x0318,
      APPCOMMAND = 0x0319,
      THEMECHANGED = 0x031A,

      DWMCOMPOSITIONCHANGED = 0x031E,
      DWMNCRENDERINGCHANGED = 0x031F,
      DWMCOLORIZATIONCOLORCHANGED = 0x0320,
      DWMWINDOWMAXIMIZEDCHANGE = 0x0321,

      GETTITLEBARINFOEX = 0x033F,
      #region Windows 7
      DWMSENDICONICTHUMBNAIL = 0x0323,
      DWMSENDICONICLIVEPREVIEWBITMAP = 0x0326,
      #endregion

      USER = 0x0400,

      // This is the hard-coded message value used by Microsoft for Shell_NotifyIcon.
      // It's relatively safe to reuse.
      TRAYMOUSEMESSAGE = 0x800, //WM_USER + 1024
      APP = 0x8000
#pragma warning restore CS1591
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT {
      /// <summary>
      /// x coordinate of point.
      /// </summary>
      public int x;
      /// <summary>
      /// y coordinate of point.
      /// </summary>
      public int y;

      /// <summary>
      /// Construct a point of coordinates (x,y).
      /// </summary>
      public POINT(int x, int y) {
        this.x = x;
        this.y = y;
      }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO {
      public POINT ptReserved;
      public POINT ptMaxSize;
      public POINT ptMaxPosition;
      public POINT ptMinTrackSize;
      public POINT ptMaxTrackSize;
    };


    /// <summary>
    /// DWM window accent policy.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ACCENT_POLICY {
      public ACCENT_STATE AccentState;
      public uint AccentFlags;
      public uint GradientColor;
      public uint AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINCOMPATTRDATA {
      public WINCOMPATTR Attribute;
      public IntPtr Data;
      public int SizeOfData;
    }

    public enum MonitorOptions : uint {
      MONITOR_DEFAULTTONULL,
      MONITOR_DEFAULTTOPRIMARY,
      MONITOR_DEFAULTTONEAREST,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO {
      public int cbSize;
      public SnapLayout.RECT rcMonitor;
      public SnapLayout.RECT rcWork;
      [CLSCompliant(false)]
      public uint dwFlags;
    }


    [DllImport("user32.dll")]
    [CLSCompliant(false)]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, MonitorOptions dwFlags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfo", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool _GetMonitorInfo([In] IntPtr hMonitor, ref MONITORINFO lpmi);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static MONITORINFO GetMonitorInfo([In] IntPtr hMonitor) {
      var mi = new MONITORINFO {
        cbSize = Marshal.SizeOf(typeof(MONITORINFO))
      };
      if (!_GetMonitorInfo(hMonitor, ref mi)) {
        return mi;
      }

      return mi;
    }

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool _GetMonitorInfoW([In] IntPtr hMonitor, ref MONITORINFO lpmi);

    public static MONITORINFO GetMonitorInfoW([In] IntPtr hMonitor) {
      var mi = new MONITORINFO {
        cbSize = Marshal.SizeOf(typeof(MONITORINFO))
      };
      if (!_GetMonitorInfoW(hMonitor, ref mi)) {
        return mi;
      }

      return mi;
    }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(ref Win32Point pt);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "DefWindowProcW")]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Win32Point {
      public Int32 X;
      public Int32 Y;
    };
    public static Point GetMousePosition() {
      var w32Mouse = new Win32Point();
      GetCursorPos(ref w32Mouse);

      return new Point(w32Mouse.X, w32Mouse.Y);
    }
    /// <summary>
    /// Sets various information regarding DWM window attributes.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int SetWindowCompositionAttribute(IntPtr hWnd, ref WINCOMPATTRDATA data);

    /// <summary>
    /// Brings the thread that created the specified window into the foreground and activates the window.
    /// Keyboard input is directed to the window, and various visual cues are changed for the user.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool SetForegroundWindow(HandleRef hWnd);

    /// <summary>
    /// Retrieves information about the specified window.
    /// The function also retrieves the 32-bit (DWORD) value at the specified offset into the extra window memory.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    /// <summary>
    /// Changes an attribute of the specified window.
    /// The function also sets the 32-bit (long) value at the specified offset into the extra window memory.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);

    /// <summary>
    /// Sends the specified message to a window or windows.
    /// The SendMessage function calls the window procedure for the specified window and does not return until the window procedure has processed the message.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Places (posts) a message in the message queue associated with the thread that created the specified window and returns without waiting for the thread to process the message.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool PostMessage(HandleRef hWnd, WM msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Calls the default window procedure to provide default processing for any window messages that an application does not process.
    /// This function ensures that every message is processed.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Defines a new window message that is guaranteed to be unique throughout the system. The message value can be used when sending or posting messages.
    /// </summary>
    /// <param name="lpString">The message to be registered.</param>
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int RegisterWindowMessage(string lpString);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);
    [Flags]
    public enum SetWindowPosFlags : uint {
      /// <summary>If the calling thread and the thread that owns the window are attached to different input queues,
      /// the system posts the request to the thread that owns the window. This prevents the calling thread from
      /// blocking its execution while other threads process the request.</summary>
      /// <remarks>SWP_ASYNCWINDOWPOS</remarks>
      AsynchronousWindowPosition = 0x4000,
      /// <summary>Prevents generation of the WM_SYNCPAINT message.</summary>
      /// <remarks>SWP_DEFERERASE</remarks>
      DeferErase = 0x2000,
      /// <summary>Draws a frame (defined in the window's class description) around the window.</summary>
      /// <remarks>SWP_DRAWFRAME</remarks>
      DrawFrame = 0x0020,
      /// <summary>Applies new frame styles set using the SetWindowLong function. Sends a WM_NCCALCSIZE message to
      /// the window, even if the window's size is not being changed. If this flag is not specified, WM_NCCALCSIZE
      /// is sent only when the window's size is being changed.</summary>
      /// <remarks>SWP_FRAMECHANGED</remarks>
      FrameChanged = 0x0020,
      /// <summary>Hides the window.</summary>
      /// <remarks>SWP_HIDEWINDOW</remarks>
      HideWindow = 0x0080,
      /// <summary>Does not activate the window. If this flag is not set, the window is activated and moved to the
      /// top of either the topmost or non-topmost group (depending on the setting of the hWndInsertAfter
      /// parameter).</summary>
      /// <remarks>SWP_NOACTIVATE</remarks>
      DoNotActivate = 0x0010,
      /// <summary>Discards the entire contents of the client area. If this flag is not specified, the valid
      /// contents of the client area are saved and copied back into the client area after the window is sized or
      /// repositioned.</summary>
      /// <remarks>SWP_NOCOPYBITS</remarks>
      DoNotCopyBits = 0x0100,
      /// <summary>Retains the current position (ignores X and Y parameters).</summary>
      /// <remarks>SWP_NOMOVE</remarks>
      IgnoreMove = 0x0002,
      /// <summary>Does not change the owner window's position in the Z order.</summary>
      /// <remarks>SWP_NOOWNERZORDER</remarks>
      DoNotChangeOwnerZOrder = 0x0200,
      /// <summary>Does not redraw changes. If this flag is set, no repainting of any kind occurs. This applies to
      /// the client area, the nonclient area (including the title bar and scroll bars), and any part of the parent
      /// window uncovered as a result of the window being moved. When this flag is set, the application must
      /// explicitly invalidate or redraw any parts of the window and parent window that need redrawing.</summary>
      /// <remarks>SWP_NOREDRAW</remarks>
      DoNotRedraw = 0x0008,
      /// <summary>Same as the SWP_NOOWNERZORDER flag.</summary>
      /// <remarks>SWP_NOREPOSITION</remarks>
      DoNotReposition = 0x0200,
      /// <summary>Prevents the window from receiving the WM_WINDOWPOSCHANGING message.</summary>
      /// <remarks>SWP_NOSENDCHANGING</remarks>
      DoNotSendChangingEvent = 0x0400,
      /// <summary>Retains the current size (ignores the cx and cy parameters).</summary>
      /// <remarks>SWP_NOSIZE</remarks>
      IgnoreResize = 0x0001,
      /// <summary>Retains the current Z order (ignores the hWndInsertAfter parameter).</summary>
      /// <remarks>SWP_NOZORDER</remarks>
      IgnoreZOrder = 0x0004,
      /// <summary>Displays the window.</summary>
      /// <remarks>SWP_SHOWWINDOW</remarks>
      ShowWindow = 0x0040,
    }
  }
}