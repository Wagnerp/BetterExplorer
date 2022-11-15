﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace BExplorer.Shell.Interop {
  public static class Gdi32 {

    #region Constants
    public const int SRCCOPY = 0xCC0020;
    public const byte AC_SRC_OVER = 0x00;
    public const byte AC_SRC_ALPHA = 0x01;
    #endregion

    [StructLayout(LayoutKind.Sequential)]
    public struct BLENDFUNCTION {
      byte BlendOp;
      byte BlendFlags;
      byte SourceConstantAlpha;
      byte AlphaFormat;

      public BLENDFUNCTION(byte op, byte flags, byte alpha, byte format) {
        BlendOp = op;
        BlendFlags = flags;
        SourceConstantAlpha = alpha;
        AlphaFormat = format;
      }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RGBQUAD {
      public byte rgbBlue;
      public byte rgbGreen;
      public byte rgbRed;
      public byte rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAP {
      public Int32 bmType;
      public Int32 bmWidth;
      public Int32 bmHeight;
      public Int32 bmWidthBytes;
      public Int16 bmPlanes;
      public Int16 bmBitsPixel;
      public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER {
      public int biSize;
      public int biWidth;
      public int biHeight;
      public Int16 biPlanes;
      public Int16 biBitCount;
      public int biCompression;
      public int biSizeImage;
      public int biXPelsPerMeter;
      public int biYPelsPerMeter;
      public int biClrUsed;
      public int bitClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE {
      public int cx;
      public int cy;

      public SIZE(int cx, int cy) {
        this.cx = cx;
        this.cy = cy;
      }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DIBSECTION {
      public BITMAP dsBm;
      public BITMAPINFOHEADER dsBmih;
      public int dsBitField1;
      public int dsBitField2;
      public int dsBitField3;
      public IntPtr dshSection;
      public int dsOffset;
    }

    [DllImportAttribute("gdi32.dll")]
    public static extern int BitBlt(
      IntPtr hdcDest,     // handle to destination DC (device context)
      int nXDest,         // x-coord of destination upper-left corner
      int nYDest,         // y-coord of destination upper-left corner
      int nWidth,         // width of destination rectangle
      int nHeight,        // height of destination rectangle
      IntPtr hdcSrc,      // handle to source DC
      int nXSrc,          // x-coordinate of source upper-left corner
      int nYSrc,          // y-coordinate of source upper-left corner
      int dwRop  // raster operation code
      );

    [DllImport("gdi32.dll", EntryPoint = "GdiAlphaBlend")]
    public static extern bool AlphaBlend(IntPtr hdcDest, int nXOriginDest, int nYOriginDest,
       int nWidthDest, int nHeightDest,
       IntPtr hdcSrc, int nXOriginSrc, int nYOriginSrc, int nWidthSrc, int nHeightSrc,
       BLENDFUNCTION blendFunction);

    [DllImportAttribute("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImportAttribute("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImportAttribute("gdi32.dll")]
    public static extern void DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll", EntryPoint = "GetObject")]
    public static extern int GetObjectDIBSection(IntPtr hObject, int nCount, ref DIBSECTION lpObject);

    [DllImport("gdi32.dll", EntryPoint = "GetObject")]
    public static extern int GetObjectBitmap(IntPtr hObject, int nCount, [Out] IntPtr lpObject);
    [DllImport("gdi32.dll")]
    public static extern IntPtr GetStockObject(StockObjects fnObject);
    public enum StockObjects {
      WHITE_BRUSH = 0,
      LTGRAY_BRUSH = 1,
      GRAY_BRUSH = 2,
      DKGRAY_BRUSH = 3,
      BLACK_BRUSH = 4,
      NULL_BRUSH = 5,
      HOLLOW_BRUSH = NULL_BRUSH,
      WHITE_PEN = 6,
      BLACK_PEN = 7,
      NULL_PEN = 8,
      OEM_FIXED_FONT = 10,
      ANSI_FIXED_FONT = 11,
      ANSI_VAR_FONT = 12,
      SYSTEM_FONT = 13,
      DEVICE_DEFAULT_FONT = 14,
      DEFAULT_PALETTE = 15,
      SYSTEM_FIXED_FONT = 16,
      DEFAULT_GUI_FONT = 17,
      DC_BRUSH = 18,
      DC_PEN = 19,
    }

    public static void GetBitmapDimentions(IntPtr ipd, out int width, out int height) {
      // get the info about the HBITMAP inside the IPictureDisp
      var dibsection = new DIBSECTION();
      var res = GetObjectDIBSection(ipd, Marshal.SizeOf(dibsection), ref dibsection);
      width = dibsection.dsBm.bmWidth;
      height = dibsection.dsBm.bmHeight;
    }

    public static void ConvertPixelByPixel(IntPtr ipd, out int width, out int height) {
      // get the info about the HBITMAP inside the IPictureDisp
      DIBSECTION dibsection = new DIBSECTION();
      var res = GetObjectDIBSection(ipd, Marshal.SizeOf(dibsection), ref dibsection);
      width = dibsection.dsBm.bmWidth;
      height = dibsection.dsBm.bmHeight;
      unsafe {
        //Check is that 32bit bitmap
        if (dibsection.dsBmih.biBitCount == 32) {
          // get a pointer to the raw bits
          RGBQUAD* pBits = (RGBQUAD*)(void*)dibsection.dsBm.bmBits;

          // copy each pixel manually and premultiply the color values
          for (int x = 0; x < dibsection.dsBmih.biWidth; x++)
            for (int y = 0; y < dibsection.dsBmih.biHeight; y++) {
              int offset = y * dibsection.dsBmih.biWidth + x;
              if (pBits[offset].rgbReserved > 0 && (pBits[offset].rgbBlue > pBits[offset].rgbReserved || pBits[offset].rgbGreen > pBits[offset].rgbReserved || pBits[offset].rgbRed > pBits[offset].rgbReserved)) {
                pBits[offset].rgbBlue = (byte)((((int)pBits[offset].rgbBlue * (int)pBits[offset].rgbReserved + 1) * 257) >> 16);
                pBits[offset].rgbGreen = (byte)((((int)pBits[offset].rgbGreen * (int)pBits[offset].rgbReserved + 1) * 257) >> 16);
                pBits[offset].rgbRed = (byte)((((int)pBits[offset].rgbRed * (int)pBits[offset].rgbReserved + 1) * 257) >> 16);
              }
            }
        }
      }
    }

    [DllImport("user32.dll", EntryPoint = "GetDC", CharSet = CharSet.Auto)]
    public static extern IntPtr GetDeviceContext(IntPtr hWnd);

    [DllImport("gdi32", SetLastError = true, EntryPoint = "ExcludeClipRect", CharSet = CharSet.Auto)]
    public static extern int ExcludeClipRect(IntPtr hDC, int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("gdi32", EntryPoint = "CreateCompatibleBitmap")]
    public static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    public static void NativeDraw(IntPtr destDC, IntPtr hBitmap, int x, int y, int iconSize, Boolean isHidden = false) {
      IntPtr destCDC = CreateCompatibleDC(destDC);
      IntPtr oldSource = SelectObject(destCDC, hBitmap);
      AlphaBlend(destDC, x, y, iconSize, iconSize, destCDC, 0, 0, iconSize, iconSize, new BLENDFUNCTION(AC_SRC_OVER, 0, (byte)(isHidden ? 0x7f : 0xff), AC_SRC_ALPHA));
      SelectObject(destCDC, oldSource);
      DeleteObject(destCDC);
      DeleteObject(oldSource);
      DeleteObject(hBitmap);
    }

    public static void NativeDraw(IntPtr destDC, IntPtr hBitmap, int x, int y, int iconSizeWidth, int iconSizeHeight, Boolean isHidden = false) {
      IntPtr destCDC = CreateCompatibleDC(destDC);
      IntPtr oldSource = SelectObject(destCDC, hBitmap);
      AlphaBlend(destDC, x, y, iconSizeWidth, iconSizeHeight, destCDC, 0, 0, iconSizeWidth, iconSizeHeight, new BLENDFUNCTION(AC_SRC_OVER, 0, (byte)(isHidden ? 0x7f : 0xff), AC_SRC_ALPHA));
      SelectObject(destCDC, oldSource);
      DeleteObject(destCDC);
      DeleteObject(oldSource);
      DeleteObject(hBitmap);
    }

    public static void NativeDrawCrop(IntPtr destDC, IntPtr hBitmap, int x, int y, int xOrig, int yOrigin, int iconSizeWidth, int iconSizeHeight, Boolean isHidden = false) {
      IntPtr destCDC = CreateCompatibleDC(destDC);
      IntPtr oldSource = SelectObject(destCDC, hBitmap);
      AlphaBlend(destDC, x, y, iconSizeWidth, iconSizeHeight, destCDC, xOrig, yOrigin, iconSizeWidth, iconSizeHeight, new BLENDFUNCTION(AC_SRC_OVER, 0, (byte)(isHidden ? 0x7f : 0xff), AC_SRC_ALPHA));
      SelectObject(destCDC, oldSource);
      DeleteObject(destCDC);
      DeleteObject(oldSource);
      DeleteObject(hBitmap);
    }

    public static void NativeDraw(IntPtr destDC, IntPtr hBitmap, int x, int y, int iconSizeWidth, int iconSizeHeight, int iconSizeWidthDest, int iconSizeHeightDest, Boolean isHidden = false) {
      IntPtr destCDC = CreateCompatibleDC(destDC);
      IntPtr oldSource = SelectObject(destCDC, hBitmap);
      AlphaBlend(destDC, x, y, iconSizeWidthDest, iconSizeHeightDest, destCDC, 0, 0, iconSizeWidth, iconSizeHeight, new BLENDFUNCTION(AC_SRC_OVER, 0, (byte)(isHidden ? 0x7f : 0xff), AC_SRC_ALPHA));
      SelectObject(destCDC, oldSource);
      DeleteObject(destCDC);
      DeleteObject(oldSource);
      DeleteObject(hBitmap);
    }

    public static Bitmap RoundCorners(Bitmap StartImage, int cornerRadius, Brush backgroundColor, Pen borderColor) {
      if (cornerRadius == 0) {
        Bitmap roundedImage = new Bitmap(StartImage.Width, StartImage.Height);
        var r = new Rectangle(0, 0, StartImage.Width - 2, StartImage.Height - 2);
        using (Graphics g = Graphics.FromImage(roundedImage)) {
          g.SmoothingMode = SmoothingMode.AntiAlias;
          //g.CompositingQuality = CompositingQuality.HighQuality;
          g.InterpolationMode = InterpolationMode.NearestNeighbor;
          g.FillRectangle(backgroundColor, r);
          g.DrawRectangle(borderColor, r);
          borderColor.Dispose();
          backgroundColor.Dispose();
          return roundedImage;
        }
      } else {
        var d = cornerRadius * 2;
        Bitmap roundedImage = new Bitmap(StartImage.Width, StartImage.Height);
        var r = new Rectangle(0, 0, StartImage.Width - d, StartImage.Height - d);
        using (Graphics g = Graphics.FromImage(roundedImage)) {
          g.SmoothingMode = SmoothingMode.AntiAlias;
          //g.CompositingQuality = CompositingQuality.HighQuality;
          g.InterpolationMode = InterpolationMode.NearestNeighbor;
          System.Drawing.Drawing2D.GraphicsPath gp =
            new System.Drawing.Drawing2D.GraphicsPath();
          gp.AddArc(r.X, r.Y, d, d, 180, 90);
          gp.AddArc(r.X + r.Width - d, r.Y, d, d, 270, 90);
          gp.AddArc(r.X + r.Width - d, r.Y + r.Height - d, d, d, 0, 90);
          gp.AddArc(r.X, r.Y + r.Height - d, d, d, 90, 90);
          gp.AddLine(r.X, r.Y + r.Height - d, r.X, r.Y + d / 2);

          g.FillPath(backgroundColor, gp);
          g.DrawPath(borderColor, gp);
          borderColor.Dispose();
          backgroundColor.Dispose();
          return roundedImage;
        }
      }
    }

    [DllImport("gdi32.dll", EntryPoint = "GetTextExtentPoint32W")]
    public static extern int GetTextExtentPoint32(IntPtr hdc, [MarshalAs(UnmanagedType.LPWStr)] string str, int len, ref Size size);


    [DllImport("gdi32.dll")]
    public static extern int SetTextColor(IntPtr hdc, int color);

    [DllImport("gdi32.dll")]
    public static extern uint SetBkColor(IntPtr hdc, IntPtr crColor);

    [DllImport("gdi32.dll")]
    public static extern int SetBkMode(IntPtr hdc, int iBkMode);
    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateSolidBrush(uint crColor);

    public static Bitmap GetBitmapFromHBitmap(IntPtr hBitmap) {
      try {
        Bitmap bmp = Image.FromHbitmap(hBitmap);
        if (Image.GetPixelFormatSize(bmp.PixelFormat) < 32) {
          return bmp;
        }

        Rectangle bmBounds = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var bmpData = bmp.LockBits(bmBounds, ImageLockMode.ReadOnly, bmp.PixelFormat);
        if (IsAlphaBitmap(bmpData)) {
          var alpha = GetAlphaBitmapFromBitmapData(bmpData);
          bmp.UnlockBits(bmpData);
          bmp.Dispose();
          return alpha;
        }

        bmp.UnlockBits(bmpData);
        return bmp;
      } catch {
        return null;
      }
    }
    private static bool IsAlphaBitmap(BitmapData bmpData) {
      for (int y = 0; y <= bmpData.Height - 1; y++) {
        for (int x = 0; x <= bmpData.Width - 1; x++) {
          Color pixelColor = Color.FromArgb(
            Marshal.ReadInt32(bmpData.Scan0, (bmpData.Stride * y) + (4 * x)));

          if (pixelColor.A > 0 & pixelColor.A < 255) {
            return true;
          }
        }
      }

      return false;
    }
    private static Bitmap GetAlphaBitmapFromBitmapData(BitmapData bmpData) {
      using var tmp = new Bitmap(bmpData.Width, bmpData.Height, bmpData.Stride, PixelFormat.Format32bppArgb, bmpData.Scan0);
      Bitmap clone = new Bitmap(tmp.Width, tmp.Height, tmp.PixelFormat);
      using (Graphics gr = Graphics.FromImage(clone)) {
        gr.DrawImage(tmp, new Rectangle(0, 0, clone.Width, clone.Height));
      }
      return clone;
    }
    [Flags]
    public enum DCB {
      /// <summary>The bounding rectangle is empty.</summary>
      DCB_RESET = 0x0001,

      /// <summary>
      /// Adds the rectangle specified by the lprcBounds parameter to the bounding rectangle (using a rectangle union operation). Using
      /// both DCB_RESET and DCB_ACCUMULATE sets the bounding rectangle to the rectangle specified by the lprcBounds parameter.
      /// </summary>
      DCB_ACCUMULATE = 0x0002,

      /// <summary>Same as DCB_ACCUMULATE.</summary>
      DCB_DIRTY = DCB_ACCUMULATE,

      /// <summary>The bounding rectangle is not empty.</summary>
      DCB_SET = DCB_RESET | DCB_ACCUMULATE,

      /// <summary>Boundary accumulation is on.</summary>
      DCB_ENABLE = 0x0004,

      /// <summary>Boundary accumulation is off.</summary>
      DCB_DISABLE = 0x0008,
    }
    [DllImport("Gdi32", SetLastError = false, ExactSpelling = true)]
    public static extern DCB SetBoundsRect(IntPtr hdc, in User32.RECT lprect, DCB flags);
  }
}