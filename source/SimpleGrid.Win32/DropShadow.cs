using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SimpleGrid.Win32;

public class DropShadow
{
	[EditorBrowsable(EditorBrowsableState.Never)]
	public struct MARGINS
	{
		public int leftWidth;

		public int rightWidth;

		public int topHeight;

		public int bottomHeight;
	}

	private const int WM_NCHITTEST = 132;

	private const int WS_MINIMIZEBOX = 131072;

	private const int HTCLIENT = 1;

	private const int HTCAPTION = 2;

	private const int CS_DBLCLKS = 8;

	private const int CS_DROPSHADOW = 131072;

	private const int WM_NCPAINT = 133;

	private const int WM_ACTIVATEAPP = 28;

	[DllImport("dwmapi.dll")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

	[DllImport("dwmapi.dll")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

	[DllImport("dwmapi.dll")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int DwmIsCompositionEnabled(ref int pfEnabled);

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static bool IsCompositionEnabled()
	{
		if (Environment.OSVersion.Version.Major < 6)
		{
			return false;
		}
		DwmIsCompositionEnabled(out var enabled);
		return enabled;
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmIsCompositionEnabled(out bool enabled);

	[DllImport("Gdi32.dll")]
	private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

	private bool CheckIfAeroIsEnabled()
	{
		if (Environment.OSVersion.Version.Major >= 6)
		{
			int pfEnabled = 0;
			DwmIsCompositionEnabled(ref pfEnabled);
			return pfEnabled == 1;
		}
		return false;
	}

	public static void ApplyShadows(Form form)
	{
		int attrValue = 2;
		DwmSetWindowAttribute(form.Handle, 2, ref attrValue, 4);
		MARGINS pMarInset = new MARGINS
		{
			bottomHeight = 0,
			leftWidth = 0,
			rightWidth = 0,
			topHeight = 1
		};
		DwmExtendFrameIntoClientArea(form.Handle, ref pMarInset);
	}
}
