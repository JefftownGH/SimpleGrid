using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleGrid.Win32;

public static class HiResIcons
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct SHFILEINFO
	{
		public IntPtr hIcon;

		public int iIcon;

		public uint dwAttributes;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
		public string szDisplayName;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
		public string szTypeName;
	}

	[ComImport]
	[Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IImageList
	{
		[PreserveSig]
		int GetImageCount();

		[PreserveSig]
		IntPtr GetImageRect(int i, IntPtr prc);

		[PreserveSig]
		IntPtr GetImageInfo(int i, IntPtr pImageInfo);

		[PreserveSig]
		IntPtr SetImageInfo(int i, ref Guid riid, IntPtr pv, uint dwFlags);

		[PreserveSig]
		int GetIcon(int i, uint flags, out IntPtr picon);
	}

	private const uint SHGFI_SYSICONINDEX = 16384u;

	private const uint SHGFI_LARGEICON = 0u;

	private const uint FILE_ATTRIBUTE_DIRECTORY = 16u;

	private const uint SHIL_EXTRALARGE = 2u;

	private const uint SHIL_JUMBO = 4u;

	private const uint ILD_TRANSPARENT = 1u;

	private const uint ILD_PRESERVEALPHA = 64u;

	private static readonly Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

	private static readonly Dictionary<string, Dictionary<int, Bitmap>> Cache = new Dictionary<string, Dictionary<int, Bitmap>>(StringComparer.OrdinalIgnoreCase);

	private static readonly object CacheLock = new object();
	private static readonly HashSet<string> Pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	private static readonly SemaphoreSlim LoadGate = new SemaphoreSlim(2);

	public static event EventHandler IconLoaded;

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

	[DllImport("shell32.dll", EntryPoint = "#727")]
	private static extern int SHGetImageList(uint iImageList, ref Guid riid, out IImageList ppv);

	[DllImport("user32.dll")]
	private static extern bool DestroyIcon(IntPtr handle);

	public static Bitmap Get(string path, bool isFolder, int pixelSize)
	{
		if (pixelSize < 8)
		{
			pixelSize = 8;
		}
		string requestKey = path + "\0" + pixelSize;
		Bitmap result;
		bool shouldQueue = false;
		lock (CacheLock)
		{
			if (!Cache.TryGetValue(path, out var value))
			{
				value = new Dictionary<int, Bitmap>();
				Cache[path] = value;
			}
			if (value.TryGetValue(pixelSize, out var value2) && value2 != null)
			{
				return value2;
			}
			using Bitmap placeholder = SystemIcons.Application.ToBitmap();
			result = ScaleBitmap(placeholder, pixelSize);
			value[pixelSize] = result;
			shouldQueue = Pending.Add(requestKey);
		}
		if (shouldQueue)
			QueueBuild(path, isFolder, pixelSize, requestKey);
		return result;
	}

	private static void QueueBuild(string path, bool isFolder, int pixelSize, string requestKey)
	{
		Task.Run(async delegate
		{
			await LoadGate.WaitAsync().ConfigureAwait(false);
			try
			{
				Bitmap loaded = Build(path, isFolder, pixelSize);
				lock (CacheLock)
				{
					Bitmap oldBitmap = Cache[path][pixelSize];
					Cache[path][pixelSize] = loaded;
					oldBitmap?.Dispose();
					Pending.Remove(requestKey);
				}
				IconLoaded?.Invoke(null, EventArgs.Empty);
			}
			catch
			{
				lock (CacheLock)
					Pending.Remove(requestKey);
			}
			finally
			{
				LoadGate.Release();
			}
		});
	}

	private static Bitmap Build(string path, bool isFolder, int pixelSize)
	{
		Bitmap bitmap = TryFromImageList(path, isFolder, 4u);
		if (bitmap == null || bitmap.Width < 24)
		{
			bitmap?.Dispose();
			bitmap = TryFromImageList(path, isFolder, 2u);
		}
		if (bitmap == null || bitmap.Width < 16)
		{
			bitmap?.Dispose();
			try
			{
				using Icon icon = Icon.ExtractAssociatedIcon(path);
				using Bitmap src = icon.ToBitmap();
				return ScaleBitmap(src, pixelSize);
			}
			catch
			{
				return new Bitmap(pixelSize, pixelSize);
			}
		}
		try
		{
			return ScaleBitmap(bitmap, pixelSize);
		}
		finally
		{
			bitmap.Dispose();
		}
	}

	private static Bitmap TryFromImageList(string path, bool isFolder, uint shil)
	{
		IImageList ppv = null;
		IntPtr picon = IntPtr.Zero;
		try
		{
			Guid riid = IID_IImageList;
			if (SHGetImageList(shil, ref riid, out ppv) != 0 || ppv == null)
			{
				return null;
			}
			SHFILEINFO psfi = default(SHFILEINFO);
			uint dwFileAttributes = (isFolder ? 16u : 0u);
			uint uFlags = 16384u;
			SHGetFileInfo(path, dwFileAttributes, ref psfi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), uFlags);
			if (psfi.iIcon < 0)
			{
				return null;
			}
			if (ppv.GetIcon(psfi.iIcon, 65u, out picon) != 0 || picon == IntPtr.Zero)
			{
				return null;
			}
			using Icon icon = Icon.FromHandle(picon);
			return icon.ToBitmap();
		}
		catch
		{
			return null;
		}
		finally
		{
			if (picon != IntPtr.Zero)
			{
				DestroyIcon(picon);
			}
			if (ppv != null)
			{
				Marshal.ReleaseComObject(ppv);
			}
		}
	}

	public static Bitmap ScaleBitmap(Image src, int size)
	{
		if (size < 8)
		{
			size = 8;
		}
		Bitmap bitmap = new Bitmap(size, size);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.SmoothingMode = SmoothingMode.HighQuality;
		graphics.CompositingQuality = CompositingQuality.HighQuality;
		float num = Math.Min((float)size / (float)src.Width, (float)size / (float)src.Height);
		int num2 = Math.Max(1, (int)Math.Round((float)src.Width * num));
		int num3 = Math.Max(1, (int)Math.Round((float)src.Height * num));
		int x = (size - num2) / 2;
		int y = (size - num3) / 2;
		graphics.Clear(Color.Transparent);
		graphics.DrawImage(src, x, y, num2, num3);
		return bitmap;
	}
}
