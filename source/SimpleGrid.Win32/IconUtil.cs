using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace SimpleGrid.Win32;

public static class IconUtil
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct SHSTOCKICONINFO
	{
		public uint cbSize;

		public IntPtr hIcon;

		public int iSysIconIndex;

		public int iIcon;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
		public string szPath;
	}

	private static Icon folderIcon;

	private const uint SHSIID_FOLDER = 3u;

	private const uint SHGSI_ICON = 256u;

	private const uint SHGSI_LARGEICON = 0u;

	private const uint SHGSI_SMALLICON = 1u;

	public static Icon FolderLarge => folderIcon ?? (folderIcon = GetStockIcon(3u, 0u));

	private static Icon GetStockIcon(uint type, uint size)
	{
		SHSTOCKICONINFO psii = default(SHSTOCKICONINFO);
		psii.cbSize = (uint)Marshal.SizeOf(psii);
		SHGetStockIconInfo(type, 0x100 | size, ref psii);
		Icon result = (Icon)Icon.FromHandle(psii.hIcon).Clone();
		DestroyIcon(psii.hIcon);
		return result;
	}

	[DllImport("shell32.dll")]
	public static extern int SHGetStockIconInfo(uint siid, uint uFlags, ref SHSTOCKICONINFO psii);

	[DllImport("user32.dll")]
	public static extern bool DestroyIcon(IntPtr handle);
}
