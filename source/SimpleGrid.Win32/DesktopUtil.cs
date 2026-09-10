using System;
using System.Runtime.InteropServices;

namespace SimpleGrid.Win32;

public class DesktopUtil
{
	private const int GWL_STYLE = -16;

	private const int WS_MAXIMIZEBOX = 65536;

	private const int WS_MINIMIZEBOX = 131072;

	[DllImport("User32.dll", EntryPoint = "GetWindowLong")]
	private static extern int GetWindowLongPtr(IntPtr hWnd, int nIndex);

	[DllImport("User32.dll", EntryPoint = "SetWindowLong")]
	private static extern int SetWindowLongPtr(IntPtr hWnd, int nIndex, int dwNewLong);

	public static void GlueToDesktop(IntPtr handle)
	{
		PreventMinimize(handle);
	}

	public static void PreventMinimize(IntPtr handle)
	{
		int windowLongPtr = GetWindowLongPtr(handle, -16);
		SetWindowLongPtr(handle, -16, windowLongPtr & -65537 & -131073);
	}
}
