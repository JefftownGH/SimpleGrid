using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SimpleGrid.Util;

/// <summary>
/// 覆盖整个虚拟屏幕的透明图层，用于绘制围栏拖动时的对齐辅助线。
///
/// 围栏是彼此独立的顶层窗口，无法直接画到别的窗口上，因此用一个
/// 置顶、不激活、点击穿透的覆盖层来显示提示线。
/// </summary>
internal sealed class SnapOverlay : IDisposable
{
	private const int WS_EX_TRANSPARENT = 0x00000020;
	private const int WS_EX_LAYERED = 0x00080000;
	private const int WS_EX_NOACTIVATE = 0x08000000;
	private const int WS_EX_TOOLWINDOW = 0x00000080;

	private const int WS_POPUP = unchecked((int)0x80000000);
	private const int SWP_SHOWWINDOW = 0x0040;
	private const int SWP_NOACTIVATE = 0x0010;

	private Form overlay;
	private readonly List<SnapGuide> activeGuides = new List<SnapGuide>();

	private static readonly Color GuideColor = Color.FromArgb(0, 150, 220);
	private static readonly Color ScreenEdgeColor = Color.FromArgb(255, 152, 0);

	public void Show(IList<SnapGuide> guides)
	{
		if (guides == null || guides.Count == 0)
		{
			Hide();
			return;
		}

		activeGuides.Clear();
		activeGuides.AddRange(guides);
		EnsureOverlay();
		overlay.Refresh();
	}

	public void Hide()
	{
		if (overlay != null && overlay.IsHandleCreated)
			overlay.Hide();
	}

	public void Dispose()
	{
		if (overlay != null)
		{
			overlay.Dispose();
			overlay = null;
		}
	}

	private void EnsureOverlay()
	{
		if (overlay != null && !overlay.IsDisposed)
		{
			if (!overlay.Visible)
			{
				ShowNative(overlay.Handle);
			}
			return;
		}

		Rectangle area = SystemInformation.VirtualScreen;
		overlay = new Form
		{
			FormBorderStyle = FormBorderStyle.None,
			ShowInTaskbar = false,
			TopMost = true,
			StartPosition = FormStartPosition.Manual,
			Bounds = area,
			// 全透明背景：既不遮挡桌面，也保证自身不被画出边框
			BackColor = Color.Black
		};

		// 用 Win32 直接设置扩展样式并显示：Form 的 Show() 会走正常的激活流程，
		// 而覆盖层必须既不抢焦点也不拦截鼠标。
		CreateParams cp = new CreateParams();
		cp.ExStyle = WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
		cp.Caption = "SimpleGridSnapOverlay";
		overlay.CreateControl();
		ShowNative(overlay.Handle, area);
		overlay.Paint += Overlay_Paint;
		overlay.Refresh();
	}

	private void ShowNative(IntPtr handle)
	{
		ShowNative(handle, new Rectangle(
			SystemInformation.VirtualScreen.X,
			SystemInformation.VirtualScreen.Y,
			SystemInformation.VirtualScreen.Width,
			SystemInformation.VirtualScreen.Height));
	}

	private void ShowNative(IntPtr handle, Rectangle area)
	{
		SimpleGrid.Win32.WindowUtil.SetWindowPos(
			handle,
			IntPtr.Zero, // HWND_TOPMOST
			area.X, area.Y, area.Width, area.Height,
			SWP_SHOWWINDOW | SWP_NOACTIVATE);
	}

	private void Overlay_Paint(object sender, PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		Rectangle area = SystemInformation.VirtualScreen;

		foreach (SnapGuide guide in activeGuides)
		{
			Color color = guide.IsScreenEdge ? ScreenEdgeColor : GuideColor;
			using (Pen outer = new Pen(Color.FromArgb(90, Color.Black), 3f))
			using (Pen inner = new Pen(color, 1.5f))
			{
				if (guide.IsVertical)
				{
					// 竖直辅助线：贯穿整个虚拟屏幕高度
					g.DrawLine(outer, guide.X, area.Top, guide.X, area.Bottom);
					g.DrawLine(inner, guide.X, area.Top, guide.X, area.Bottom);
				}
				else
				{
					// 水平辅助线：贯穿整个虚拟屏幕宽度
					g.DrawLine(outer, area.Left, guide.Y, area.Right, guide.Y);
					g.DrawLine(inner, area.Left, guide.Y, area.Right, guide.Y);
				}
			}
		}
	}
}