using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SimpleGrid.Win32;

public class WindowUtil
{
	[Flags]
	public enum ExtendedWindowStyles
	{
		WS_EX_TOOLWINDOW = 0x80
	}

	public enum GetWindowLongFields
	{
		GWL_EXSTYLE = -20
	}

	public const int WM_NCHITTEST = 132;

	public const int HTCLIENT = 1;

	public const int HTCAPTION = 2;

	public const int HTLEFT = 10;

	public const int HTRIGHT = 11;

	public const int HTTOP = 12;

	public const int HTTOPLEFT = 13;

	public const int HTTOPRIGHT = 14;

	public const int HTBOTTOM = 15;

	public const int HTBOTTOMLEFT = 16;

	public const int HTBOTTOMRIGHT = 17;

	public const int WM_SYSCOMMAND = 274;

	public const int SC_MAXIMIZE = 61488;

	public const int SC_MINIMIZE = 61472;

	public const uint SWP_NOSIZE = 1u;

	public const uint SWP_NOMOVE = 2u;

	public const uint SWP_NOACTIVATE = 16u;

	public const uint SWP_NOZORDER = 4u;

	public const int WM_ACTIVATEAPP = 28;

	public const int WM_ACTIVATE = 6;

	public const int WM_SETFOCUS = 7;

	public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

	public const int WM_WINDOWPOSCHANGING = 70;

	[DllImport("user32.dll")]
	public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	[DllImport("user32.dll")]
	public static extern IntPtr DeferWindowPos(IntPtr hWinPosInfo, IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

	[DllImport("user32.dll")]
	public static extern IntPtr BeginDeferWindowPos(int nNumWindows);

	[DllImport("user32.dll")]
	public static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);

	[DllImport("user32.dll")]
	public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

	[DllImport("user32.dll")]
	public static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

	public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
	{
		int num = 0;
		IntPtr zero = IntPtr.Zero;
		SetLastError(0);
		if (IntPtr.Size == 4)
		{
			int value = IntSetWindowLong(hWnd, nIndex, IntPtrToInt32(dwNewLong));
			num = Marshal.GetLastWin32Error();
			zero = new IntPtr(value);
		}
		else
		{
			zero = IntSetWindowLongPtr(hWnd, nIndex, dwNewLong);
			num = Marshal.GetLastWin32Error();
		}
		if (zero == IntPtr.Zero && num != 0)
		{
			throw new Win32Exception(num);
		}
		return zero;
	}

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
	private static extern IntPtr IntSetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
	private static extern int IntSetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

	private static int IntPtrToInt32(IntPtr intPtr)
	{
		return (int)intPtr.ToInt64();
	}

	[DllImport("kernel32.dll")]
	public static extern void SetLastError(int dwErrorCode);

	[DllImport("uxtheme.dll", CharSet = CharSet.Unicode, EntryPoint = "#135", SetLastError = true)]
	public static extern int SetPreferredAppMode(int preferredAppMode);

	public static void HideFromAltTab(IntPtr Handle)
	{
		SetWindowPos(Handle, HWND_BOTTOM, 0, 0, 0, 0, 19u);
		int num = (int)GetWindowLong(Handle, -20);
		num |= 0x80;
		SetWindowLong(Handle, -20, (IntPtr)num);
	}
}
