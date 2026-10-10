using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SimpleGrid.Model;
using SimpleGrid.Util;
using SimpleGrid.Win32;

namespace SimpleGrid;

/// <summary>
/// 常驻托盘控制器。提供"退出程序但不删除围栏"的出口，以及全部围栏的批量显示/隐藏。
/// 围栏窗体在构造时自动登记，因此不需要 FenceManager 反向回调 UI。
/// </summary>
internal static class TrayController
{
	private static NotifyIcon trayIcon;

	private static readonly List<FenceWindow> windows = new List<FenceWindow>();

	private static bool fencesVisible = true;

	/// <summary>程序正在经托盘退出；围栏窗体据此放行 FormClosing，避免被 OnFormClosing 拦截导致退出失效。</summary>
	public static bool ShuttingDown { get; private set; }

	public static void Initialize()
	{
		if (trayIcon != null)
			return;

		trayIcon = new NotifyIcon
		{
			Icon = CreateTrayIcon(),
			Text = "SimpleGrid",
			Visible = true
		};

		ToolStripMenuItem addFence = new ToolStripMenuItem("新建围栏");
		addFence.Click += delegate
		{
			FenceManager.Instance.CreateFence("新建围栏");
			ShowAll();
		};

		// 围栏排布：移植 BestNoFences 的自动布局能力
		ToolStripMenuItem layoutRoot = new ToolStripMenuItem("围栏排布");
		ToolStripMenuItem autoLayout = new ToolStripMenuItem("自动铺满屏幕");
		autoLayout.Click += delegate { ApplyLayout(FenceScreenRegion.Auto); };
		ToolStripMenuItem leftLayout = new ToolStripMenuItem("靠左排列");
		leftLayout.Click += delegate { ApplyLayout(FenceScreenRegion.Left); };
		ToolStripMenuItem centerLayout = new ToolStripMenuItem("居中排列");
		centerLayout.Click += delegate { ApplyLayout(FenceScreenRegion.Center); };
		ToolStripMenuItem rightLayout = new ToolStripMenuItem("靠右排列");
		rightLayout.Click += delegate { ApplyLayout(FenceScreenRegion.Right); };
		ToolStripMenuItem topRightLayout = new ToolStripMenuItem("靠右上排列");
		topRightLayout.Click += delegate { ApplyLayout(FenceScreenRegion.TopRight); };
		layoutRoot.DropDownItems.AddRange(new ToolStripItem[]
		{
			autoLayout, leftLayout, centerLayout, rightLayout, topRightLayout
		});

		ToolStripMenuItem showAll = new ToolStripMenuItem("显示全部围栏");
		showAll.Click += delegate { ShowAll(); };

		ToolStripMenuItem hideAll = new ToolStripMenuItem("隐藏全部围栏");
		hideAll.Click += delegate { HideAll(); };

		ToolStripMenuItem toggleAll = new ToolStripMenuItem("显示 / 隐藏全部围栏");
		toggleAll.Click += delegate { ToggleAll(); };

		// 开机自启：勾选状态与注册表实际值同步
		ToolStripMenuItem startupItem = new ToolStripMenuItem("开机自动启动")
		{
			CheckOnClick = true
		};
		try
		{
			startupItem.Checked = StartupManager.IsStartupEnabled();
		}
		catch
		{
			startupItem.Enabled = false;
		}
		startupItem.Click += delegate
		{
			bool target = startupItem.Checked;
			StartupManager.SetStartup(target);
			// 写完再回读校验，失败则把勾选状态回滚，避免菜单与实际不一致
			if (StartupManager.IsStartupEnabled() != target)
			{
				startupItem.Checked = !target;
				MessageBox.Show("无法修改开机启动设置，请检查注册表权限。", "开机启动",
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		};

		ToolStripMenuItem exit = new ToolStripMenuItem("退出");
		exit.Click += delegate { ExitApplication(); };

		ContextMenuStrip menu = new ContextMenuStrip();
		menu.Items.AddRange(new ToolStripItem[]
		{
			addFence,
			new ToolStripSeparator(),
			layoutRoot,
			new ToolStripSeparator(),
			showAll,
			hideAll,
			toggleAll,
			new ToolStripSeparator(),
			startupItem,
			new ToolStripSeparator(),
			exit
		});

		trayIcon.ContextMenuStrip = menu;
		trayIcon.DoubleClick += delegate { ToggleAll(); };

		RegisterGlobalHotkeys();
		Application.ApplicationExit += delegate { DisposeTrayIcon(); };
		DiagLog.Log("托盘已创建");
	}

	/// <summary>
	/// 注册全局热键。快捷键字符串在这里配置，便于用户按习惯修改。
	/// 选中 Ctrl+Shift+S / Ctrl+Shift+H 是因为 Ctrl+Alt+S、Ctrl+Alt+H 常被输入法
	/// 等常驻程序占用（RegisterHotKey 会失败）。MOD_NOREPEAT 已避免长按连发。
	/// </summary>
	private static void RegisterGlobalHotkeys()
	{
		GlobalHotkeyManager.Register("Ctrl+Shift+S", ShowAll);
		GlobalHotkeyManager.Register("Ctrl+Shift+H", HideAll);
	}

	/// <summary>
	/// 按指定区域重新排布全部围栏。
	/// </summary>
	public static void ApplyLayout(FenceScreenRegion region)
	{
		try
		{
			List<FenceWindow> alive = new List<FenceWindow>();
			foreach (FenceWindow window in windows)
			{
				if (!window.IsDisposed)
					alive.Add(window);
			}
			if (alive.Count == 0)
			{
				ShowAll();
				return;
			}

			Rectangle[] layout = ScreenRegionGenerator.GenerateLayout(region, alive.Count);
			for (int i = 0; i < alive.Count; i++)
			{
				alive[i].EnsureExpanded();
				alive[i].ApplyLayout(layout[i]);
			}
			ShowAll();
			DiagLog.Log("已按 " + region + " 重新排布 " + alive.Count + " 个围栏");
		}
		catch (Exception ex)
		{
			DiagLog.Log("重新排布围栏失败: " + ex.Message);
			MessageBox.Show("重新排布围栏时出错，详情见 app_diag.log。", "围栏排布",
				MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
	}

	/// <summary>
	/// 检查是否有围栏因分辨率变化需要重新适配，有则自动居中重排。
	/// </summary>
	public static void RelayoutIfNeeded()
	{
		try
		{
			int currentWidth = Screen.PrimaryScreen?.Bounds.Width ?? 0;
			int currentHeight = Screen.PrimaryScreen?.Bounds.Height ?? 0;
			if (currentWidth <= 0 || currentHeight <= 0)
				return;

			bool needRelayout = false;
			foreach (FenceWindow window in windows)
			{
				if (window.IsDisposed)
					continue;
				if (window.NeedsRelayout || window.FenceScreenWidth != currentWidth || window.FenceScreenHeight != currentHeight)
				{
					needRelayout = true;
					break;
				}
			}
			if (needRelayout)
				ApplyLayout(FenceScreenRegion.Center);
		}
		catch (Exception ex)
		{
			DiagLog.Log("分辨率自适应检查失败: " + ex.Message);
		}
	}

	public static void Register(FenceWindow window)
	{
		if (window == null || windows.Contains(window))
			return;
		windows.Add(window);
		if (!fencesVisible)
			window.Hide();
	}

	public static void Unregister(FenceWindow window)
	{
		if (window == null || !windows.Remove(window))
			return;
		if (window.Visible)
			fencesVisible = true;
	}

	public static void ShowAll()
	{
		fencesVisible = true;
		foreach (FenceWindow window in windows)
		{
			if (!window.IsDisposed)
				window.Show();
		}
	}

	public static void HideAll()
	{
		fencesVisible = false;
		foreach (FenceWindow window in windows)
		{
			if (!window.IsDisposed)
				window.Hide();
		}
	}

	public static void ToggleAll()
	{
		if (fencesVisible)
			HideAll();
		else
			ShowAll();
	}

	/// <summary>
	/// 退出程序。与围栏右键的 Remove Fence 不同：这里不删除任何围栏目录，只隐藏窗口并结束进程。
	/// </summary>
	public static void ExitApplication()
	{
		if (ShuttingDown)
			return;
		if (MessageBox.Show("确定要退出 SimpleGrid 吗？\n围栏布局会被保留。", "退出", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			return;

		ShuttingDown = true;
		DiagLog.Log("托盘退出：隐藏全部围栏后结束进程");
		HideAll();
		Application.Exit();
	}

	private static void DisposeTrayIcon()
	{
		if (trayIcon == null)
			return;
		NotifyIcon icon = trayIcon;
		trayIcon = null;
		icon.Visible = false;
		icon.Dispose();
	}

	// 运行时绘制 2x2 网格徽标。不用 ExtractAssociatedIcon：框架依赖部署下常返回 null；
	// 也不需要内嵌二进制 .ico 资源。
	private static Icon CreateTrayIcon()
	{
		const int size = 32;
		const int pad = 5;
		const int gap = 4;
		const int cell = (size - pad * 2 - gap) / 2;
		IntPtr handle = IntPtr.Zero;
		try
		{
			using (Bitmap bitmap = new Bitmap(size, size))
			using (Graphics g = Graphics.FromImage(bitmap))
			{
				g.Clear(Color.Transparent);
				g.SmoothingMode = SmoothingMode.AntiAlias;
				using (SolidBrush fill = new SolidBrush(Theme.Accent))
				{
					for (int row = 0; row < 2; row++)
						for (int col = 0; col < 2; col++)
							FillRounded(g, fill, pad + col * (cell + gap), pad + row * (cell + gap), cell, cell, 4);
				}
				handle = bitmap.GetHicon();
				using (Icon managed = Icon.FromHandle(handle))
					return (Icon)managed.Clone();
			}
		}
		catch
		{
			if (handle != IntPtr.Zero)
				DestroyIcon(handle);
			return SystemIcons.Application;
		}
	}

	private static void FillRounded(Graphics g, Brush brush, int x, int y, int w, int h, int radius)
	{
		int d = radius * 2;
		using (GraphicsPath path = new GraphicsPath())
		{
			path.AddArc(x, y, d, d, 180, 90);
			path.AddArc(x + w - d, y, d, d, 270, 90);
			path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
			path.AddArc(x, y + h - d, d, d, 90, 90);
			path.CloseFigure();
			g.FillPath(brush, path);
		}
	}

	private static extern bool DestroyIcon(IntPtr handle);
}
