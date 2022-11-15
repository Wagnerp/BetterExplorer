﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BExplorer.Shell.Interop
{
	[ComImportAttribute()]
	[GuidAttribute("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
	[InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
	interface IShellItemImageFactory
	{
		[PreserveSig]
		HResult GetImage(
		[In, MarshalAs(UnmanagedType.Struct)] Size size,
		[In] SIIGBF flags,
		[Out] out IntPtr phbm);
	}



	[ComImport,
	Guid(InterfaceGuids.ISharedBitmap),
	InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface ISharedBitmap
	{
		void GetSharedBitmap([Out] out IntPtr phbm);
		void GetSize([Out] out Size pSize);
		void GetFormat([Out] out ThumbnailAlphaType pat);
		void InitializeBitmap([In] IntPtr hbm, [In] ThumbnailAlphaType wtsAT);
		void Detach([Out] out IntPtr phbm);
	}
}
