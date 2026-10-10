using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SimpleGrid.Model;
using SimpleGrid.Util;
using SimpleGrid.Win32;
using Peter;

namespace SimpleGrid;

public class FenceWindow : Form
{
	private const int L_itemWidth = 60;

	private const int L_itemPadding = 10;

	private const int L_textHeight = 22;

	private const int L_iconBox = 30;

	private const int L_titleOffset = 4;

	private const float L_titleFontPt = 8.5f;

	private const float L_iconFontPt = 7f;

	private int logicalTitleHeight;

	private int titleHeight;

	private Font titleFont;

	private Font iconFont;

	private float s = 1f;

	private int itemWidth;

	private int itemPadding;

	private int textHeight;

	private int iconBox;

	private int titleOffset;

	private bool allowClose;

	private string selectedItem;

	private string hoveringItem;

	private string contextMenuItem;

	private string dragCandidateItem;

	private Point dragStartPoint;

	private bool dragInProgress;

	private int dropInsertIndex = -1;

	private bool shouldUpdateSelection;

	private bool shouldRunDoubleClick;

	private bool hasSelectionUpdated;

	private bool hasHoverUpdated;

	private bool isMinified;

	private bool initialized;

	private int prevHeight;

	private int scrollHeight;

	private int scrollOffset;

	private TextBox titleEditBox;

	private int lastMouseInvalidateTick;

	private readonly List<RenderedItem> renderedItems = new List<RenderedItem>();

	private readonly ThrottledExecution throttledMove = new ThrottledExecution(TimeSpan.FromSeconds(4.0));

	private readonly ThrottledExecution throttledResize = new ThrottledExecution(TimeSpan.FromSeconds(4.0));

	private readonly ShellContextMenu shellContextMenu = new ShellContextMenu();

	private readonly ThumbnailProvider thumbnailProvider = new ThumbnailProvider();

	/// <summary>拖动对齐辅助线覆盖层。所有围栏共用同一个实例，避免多层叠加互相遮挡。</summary>
	private static readonly SnapOverlay snapOverlay = new SnapOverlay();

	private readonly FenceInfo fenceInfo;

	private static readonly int WM_TASKBARCREATED = RegisterWindowMessage("TaskbarCreated");

	/// <summary>拖动过程中持续发送，用于计算对齐吸附并显示辅助线。</summary>
	private const int WM_MOVING = 0x0216;

	private readonly object saveLock = new object();

	private IContainer components;

	private sealed class InternalDragItem
	{
		public FenceWindow Source { get; }

		public string Path { get; }

		public InternalDragItem(FenceWindow source, string path)
		{
			Source = source;
			Path = path;
		}
	}

	private sealed class RenderedItem
	{
		public int ModelIndex { get; }

		public FenceEntry Entry { get; }

		public RenderedItem(int modelIndex, FenceEntry entry)
		{
			ModelIndex = modelIndex;
			Entry = entry;
		}
	}

	private ContextMenuStrip appContextMenu;

	private ToolStripMenuItem lockedToolStripMenuItem;

	private ToolStripSeparator toolStripSeparator1;

	private ToolStripMenuItem exitToolStripMenuItem;

	private ToolStripMenuItem minifyToolStripMenuItem;

	private ToolStripMenuItem renameToolStripMenuItem;

	private ToolStripMenuItem deleteItemToolStripMenuItem;

	private ToolStripMenuItem openInExplorerToolStripMenuItem;

	private ToolStripMenuItem newFenceToolStripMenuItem;

	private ToolStripMenuItem titleSizeToolStripMenuItem;

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int RegisterWindowMessage(string lpString);

	private int S(int v)
	{
		return (int)Math.Round((float)v * s);
	}

	private float Sf(float v)
	{
		return v * s;
	}

	private void ComputeScale()
	{
		try
		{
			s = (float)base.DeviceDpi / 96f;
		}
		catch
		{
			s = 1f;
		}
		if (s < 0.5f)
		{
			s = 1f;
		}
		itemWidth = S(60);
		itemPadding = S(10);
		textHeight = S(22);
		iconBox = S(30);
		titleOffset = S(4);
	}

	private void ReloadFonts()
	{
		titleFont?.Dispose();
		iconFont?.Dispose();
		using (FontFamily family = ResolveUiFontFamily())
		{
			titleFont = new Font(family, 8.5f, GraphicsUnit.Point);
			iconFont = new Font(family, 7f, GraphicsUnit.Point);
		}
	}

	// 按优先级挑选一款系统一定存在的字体。原先硬编码 "Microsoft YaHei UI"，
	// 在未安装中文字体的 Windows 上会抛 ArgumentException，且该调用发生在
	// FenceWindow 构造函数里 —— 缺字体等于程序完全无法启动。
	private static FontFamily ResolveUiFontFamily()
	{
		string[] candidates = { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI", "Tahoma", "Arial" };
		foreach (string name in candidates)
		{
			try
			{
				return new FontFamily(name);
			}
			catch (ArgumentException)
			{
				// 该字体未安装，试下一个。
			}
		}
		// 理论不可达：Windows 字体目录下总有可用字体。
		return SystemFonts.DefaultFont.FontFamily;
	}

	public FenceWindow(FenceInfo fenceInfo)
	{
		this.fenceInfo = fenceInfo ?? throw new ArgumentNullException(nameof(fenceInfo));
		InitializeComponent();
		TrayController.Register(this);
		DropShadow.ApplyShadows(this);
		BlurUtil.EnableBlur(base.Handle);
		WindowUtil.HideFromAltTab(base.Handle);
		DesktopUtil.GlueToDesktop(base.Handle);
		ComputeScale();
		logicalTitleHeight = ((fenceInfo.TitleHeight < 16 || fenceInfo.TitleHeight > 100) ? 35 : fenceInfo.TitleHeight);
		titleHeight = S(logicalTitleHeight);
		base.MouseWheel += FenceWindow_MouseWheel;
		base.MouseCaptureChanged += FenceWindow_MouseCaptureChanged;
		thumbnailProvider.IconThumbnailLoaded += ThumbnailProvider_IconThumbnailLoaded;
		HiResIcons.IconLoaded += HiResIcons_IconLoaded;
		ReloadFonts();
		AllowDrop = true;
		Text = fenceInfo.Name;
		base.Location = new Point(S(fenceInfo.PosX), S(fenceInfo.PosY));
		base.Width = S(fenceInfo.Width);
		base.Height = S(fenceInfo.Height);
		// 必须等真实宽高确定后再生成圆角 Region：InitializeComponent 阶段用的是窗体默认尺寸，
		// 若那时就裁剪，后续设置的实际尺寸会被裁掉一大块。
		UpdateRoundedRegion();
		prevHeight = base.Height;
		// 参与拖动对齐：提供当前屏幕矩形，供 SnapManager 计算吸附目标
		SnapManager.Register(base.Handle, GetScreenBounds);
		lockedToolStripMenuItem.Checked = fenceInfo.Locked;
		minifyToolStripMenuItem.Checked = fenceInfo.CanMinify;
		Minify();
		initialized = true;
	}

	protected override void WndProc(ref Message m)
	{
		// WM_DPICHANGED：换显示器或改缩放后重算缩放比，并按新 DPI 重新落地位置尺寸
		if (m.Msg == 736)
		{
			base.WndProc(ref m);
			ComputeScale();
			titleHeight = S(logicalTitleHeight);
			ReloadFonts();
			// lParam 里是系统建议的新矩形，按它重定位，避免围栏停在屏幕外
			if (m.LParam.ToInt64() != 0)
			{
				Rectangle suggested = (Rectangle)Marshal.PtrToStructure(m.LParam, typeof(Rectangle));
				base.Location = new Point(suggested.X, suggested.Y);
				base.Width = Math.Max(S(160), suggested.Width);
				base.Height = Math.Max(S(120), suggested.Height);
			}
			UpdateRoundedRegion();
			fenceInfo.PosX = (int)Math.Round((float)base.Location.X / s);
			fenceInfo.PosY = (int)Math.Round((float)base.Location.Y / s);
			fenceInfo.Width = (int)Math.Round((float)base.Width / s);
			fenceInfo.Height = (int)Math.Round((float)(isMinified ? prevHeight : base.Height) / s);
			Save();
			Invalidate(invalidateChildren: true);
			return;
		}
		// WM_DISPLAYCHANGE：分辨率变化后标记需要重新适配，由托盘/定时器统一重排
		if (m.Msg == 0x007E)
		{
			base.WndProc(ref m);
			fenceInfo.NeedsRelayout = true;
			return;
		}
		if (m.Msg == 131)
		{
			m.Result = IntPtr.Zero;
			return;
		}
		Rectangle rectangle = new Rectangle(base.Location, base.Size);
		if (m.Msg == 674 && !rectangle.IntersectsWith(new Rectangle(Control.MousePosition, new Size(1, 1))))
		{
			Minify();
		}
		if (m.Msg == 274)
		{
			int num = m.WParam.ToInt32() & 0xFFF0;
			if (num == 61488 || num == 61472)
			{
				m.Result = IntPtr.Zero;
				return;
			}
		}
		if (m.Msg == 163)
		{
			long num = m.LParam.ToInt64();
			if (PointToClient(new Point(unchecked((short)num), unchecked((short)(num >> 16)))).Y < titleHeight && !lockedToolStripMenuItem.Checked)
			{
				BeginRenameTitle();
				return;
			}
		}
		// WM_MOVING：拖动过程中每移动一次都会发送。
		// lParam 指向一个 RECT，写回它即可修正窗口位置（比 WM_MOVE 更可靠，
		// 因为它发生在移动循环内部，不会与用户输入打架）。
		if (m.Msg == WM_MOVING)
		{
			base.WndProc(ref m);
			HandleMoving(m.LParam);
			return;
		}
		if (m.Msg == WM_TASKBARCREATED)
		{
			base.WndProc(ref m);
			WindowUtil.SetWindowPos(base.Handle, WindowUtil.HWND_BOTTOM, 0, 0, 0, 0, 19u);
			return;
		}
		if (m.Msg == 7)
		{
			WindowUtil.SetWindowPos(base.Handle, WindowUtil.HWND_BOTTOM, 0, 0, 0, 0, 19u);
			return;
		}
		base.WndProc(ref m);
		// WM_NCHITTEST：在标题栏内返回 HTCAPTION，系统才会进入标准的窗口拖动流程
		// （进而触发 WM_MOVING，辅助线吸附才有输入来源）。
		// 原实现在 WM_MOUSEMOVE 里改写 m.Result，依赖光标已经在移动时才生效，
		// 按下即拖的正常操作反而可能进不了移动循环。
		if (Control.MouseButtons != MouseButtons.Right && !lockedToolStripMenuItem.Checked && m.Msg == 132)
		{
			Point point = PointToClient(new Point(m.LParam.ToInt32()));
			int num2 = S(8);
			if ((int)m.Result == 1 && point.Y < titleHeight)
			{
				m.Result = (IntPtr)2;
				FenceWindow_MouseEnter(null, null);
			}
			if (point.Y < titleHeight)
			{
				// 标题栏：先让出图标区域，其余部分交给系统拖动
				if (!IsPointOverIcon(point))
				{
					m.Result = (IntPtr)2;
					FenceWindow_MouseEnter(null, null);
					return;
				}
			}
			if (point.X < num2 && point.Y < num2)
			{
				m.Result = new IntPtr(13);
			}
			else if (point.X > base.Width - num2 && point.Y < num2)
			{
				m.Result = new IntPtr(14);
			}
			else if (point.X < num2 && point.Y > base.Height - num2)
			{
				m.Result = new IntPtr(16);
			}
			else if (point.X > base.Width - num2 && point.Y > base.Height - num2)
			{
				m.Result = new IntPtr(17);
			}
			else if (point.Y > base.Height - num2)
			{
				m.Result = new IntPtr(15);
			}
			else if (point.X < num2)
			{
				m.Result = new IntPtr(10);
			}
			else if (point.X > base.Width - num2)
			{
				m.Result = new IntPtr(11);
			}
		}
	}

	private void exitToolStripMenuItem_Click(object sender, EventArgs e)
	{
		if (MessageBox.Show(this, "确定要删除这个围栏吗？\n围栏内的项目记录将一并移除。", "删除围栏", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
		{
			FenceManager.Instance.RemoveFence(fenceInfo);
			allowClose = true;
			Close();
		}
	}

	// ---- 内联双击标题改名 ----

	private void BeginRenameTitle()
	{
		if (titleEditBox != null)
			return;
		string oldName = Text;
		titleEditBox = new TextBox
		{
			Text = oldName,
			Font = titleFont,
			TextAlign = HorizontalAlignment.Center,
			BorderStyle = BorderStyle.None,
			BackColor = Theme.TitleBackground,
			ForeColor = Theme.TextPrimary,
			Bounds = new Rectangle(0, 0, base.Width, titleHeight),
			Multiline = false,
			TabStop = false
		};
		titleEditBox.KeyDown += TitleEditBox_KeyDown;
		titleEditBox.LostFocus += TitleEditBox_LostFocus;
		base.Controls.Add(titleEditBox);
		titleEditBox.BringToFront();
		base.Activate();
		titleEditBox.Focus();
		titleEditBox.SelectAll();
		Invalidate();
		DiagLog.Log("开始重命名标题: '" + oldName + "' focused=" + titleEditBox.Focused + " foreground=" + base.Focused);
	}

	private void TitleEditBox_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Enter)
		{
			SaveTitleEdit();
			e.Handled = true;
			e.SuppressKeyPress = true;
		}
		else if (e.KeyCode == Keys.Escape)
		{
			CancelTitleEdit();
			e.Handled = true;
			e.SuppressKeyPress = true;
		}
	}

	private void TitleEditBox_LostFocus(object sender, EventArgs e)
	{
		// 延到当前事件之后再收起，避免在 LostFocus 调用栈内销毁自身控件。
		base.BeginInvoke(delegate { SaveTitleEdit(); });
	}

	private void SaveTitleEdit()
	{
		TextBox box = titleEditBox;
		if (box == null)
			return;
		titleEditBox = null;
		string newName = box.Text?.Trim();
		string oldName = Text;
		if (!string.IsNullOrEmpty(newName) && newName != oldName)
		{
			Text = newName;
			fenceInfo.Name = newName;
			Save();
			DiagLog.Log("标题已保存: '" + oldName + "' -> '" + newName + "'");
		}
		base.Controls.Remove(box);
		box.Dispose();
		Invalidate();
	}

	private void CancelTitleEdit()
	{
		TextBox box = titleEditBox;
		if (box == null)
			return;
		titleEditBox = null;
		base.Controls.Remove(box);
		box.Dispose();
		Invalidate();
	}

	private void deleteItemToolStripMenuItem_Click(object sender, EventArgs e)
	{
		string itemToDelete = contextMenuItem;
		contextMenuItem = null;
		hoveringItem = null;
		selectedItem = null;
		dragCandidateItem = null;
		if (string.IsNullOrEmpty(itemToDelete))
			return;

		if (!RemoveItem(fenceInfo.Files, itemToDelete))
			return;

		Save();
		Invalidate();
	}

	private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
	{
		deleteItemToolStripMenuItem.Visible = contextMenuItem != null;
		openInExplorerToolStripMenuItem.Visible = contextMenuItem != null;
	}

	private void openInExplorerToolStripMenuItem_Click(object sender, EventArgs e)
	{
		string itemPath = contextMenuItem;
		contextMenuItem = null;
		if (string.IsNullOrEmpty(itemPath))
			return;

		try
		{
			Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + itemPath + "\"")
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			DiagLog.Log("无法在资源管理器中定位 '" + itemPath + "': " + ex.Message);
		}
	}

	private void FenceWindow_DragEnter(object sender, DragEventArgs e)
	{
		if (CanAcceptInternalDrag(e.Data))
		{
			e.Effect = DragDropEffects.Move;
			UpdateDropInsertIndex(e.X, e.Y);
		}
		else if (e.Data.GetDataPresent(DataFormats.FileDrop) && !lockedToolStripMenuItem.Checked)
		{
			e.Effect = DragDropEffects.Move;
			UpdateDropInsertIndex(e.X, e.Y);
		}
	}

	private void FenceWindow_DragOver(object sender, DragEventArgs e)
	{
		if (CanAcceptInternalDrag(e.Data) || (e.Data.GetDataPresent(DataFormats.FileDrop) && !lockedToolStripMenuItem.Checked))
		{
			e.Effect = DragDropEffects.Move;
			UpdateDropInsertIndex(e.X, e.Y);
		}
		else
		{
			e.Effect = DragDropEffects.None;
			ClearDropInsertIndex();
		}
	}

	private void FenceWindow_DragLeave(object sender, EventArgs e)
	{
		ClearDropInsertIndex();
	}

	private void FenceWindow_DragDrop(object sender, DragEventArgs e)
	{
		int insertIndex = GetDropInsertIndex(PointToClient(new Point(e.X, e.Y)));
		InternalDragItem internalItem = e.Data.GetData(typeof(InternalDragItem)) as InternalDragItem;
		if (internalItem != null && CanAcceptInternalDrag(e.Data))
		{
			MoveFenceItem(internalItem.Source, this, internalItem.Path, insertIndex);
			ClearDropInsertIndex();
			return;
		}

		string[] array = e.Data.GetData(DataFormats.FileDrop) as string[];
		if (array == null)
		{
			ClearDropInsertIndex();
			return;
		}
		foreach (string text in array)
		{
			if (fenceInfo.Files.Contains(text) || !ItemExists(text))
				continue;
			string importedPath = FenceManager.Instance.ImportItem(text);
			if (!string.IsNullOrEmpty(importedPath) && !fenceInfo.Files.Contains(importedPath))
			{
				fenceInfo.Files.Insert(Math.Min(insertIndex, fenceInfo.Files.Count), importedPath);
				insertIndex++;
			}
		}
		Save();
		ClearDropInsertIndex();
		Invalidate();
	}

	private void FenceWindow_Resize(object sender, EventArgs e)
	{
		if (!initialized)
			return;
		UpdateRoundedRegion();
		throttledResize.Run(delegate
		{
			fenceInfo.Width = (int)Math.Round((float)base.Width / s);
			fenceInfo.Height = (int)Math.Round((float)(isMinified ? prevHeight : base.Height) / s);
			Save();
		});
		Invalidate();
	}

	// 用 Region 抠掉四角，让圆角外的部分真正透明（配合 TransparencyKey）。
	private void UpdateRoundedRegion()
	{
		Region previous = base.Region;
		base.Region = new Region(CreateRoundedRegion(base.Width, base.Height, S(Theme.CornerRadius)));
		previous?.Dispose();
	}

	private static GraphicsPath CreateRoundedRegion(int width, int height, int radius)
	{
		Rectangle bounds = new Rectangle(0, 0, Math.Max(1, width), Math.Max(1, height));
		return Theme.CreateRoundedRect(bounds, Math.Min(radius, Math.Min(width, height) / 2));
	}

	private void FenceWindow_MouseMove(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && dragCandidateItem != null && !dragInProgress && !lockedToolStripMenuItem.Checked)
		{
			Size dragSize = SystemInformation.DragSize;
			Rectangle dragBounds = new Rectangle(dragStartPoint.X - dragSize.Width / 2, dragStartPoint.Y - dragSize.Height / 2, dragSize.Width, dragSize.Height);
			if (!dragBounds.Contains(e.Location))
			{
				string path = dragCandidateItem;
				dragCandidateItem = null;
				dragInProgress = true;
				try
				{
					DataObject data = new DataObject();
					data.SetData(typeof(InternalDragItem), new InternalDragItem(this, path));
					DoDragDrop(data, DragDropEffects.Move);
				}
				finally
				{
					dragInProgress = false;
					ClearDropInsertIndex();
				}
				return;
			}
		}
		int now = Environment.TickCount;
		if (unchecked(now - lastMouseInvalidateTick) >= 32)
		{
			lastMouseInvalidateTick = now;
			Invalidate();
		}
	}

	private void FenceWindow_MouseEnter(object sender, EventArgs e)
	{
		if (minifyToolStripMenuItem.Checked && isMinified)
		{
			isMinified = false;
			base.Height = prevHeight;
			UpdateRoundedRegion();
		}
	}

	private void FenceWindow_MouseLeave(object sender, EventArgs e)
	{
		Minify();
		selectedItem = null;
		Refresh();
	}

	private void Minify()
	{
		if (minifyToolStripMenuItem.Checked && !isMinified)
		{
			isMinified = true;
			prevHeight = base.Height;
			base.Height = titleHeight;
			// 折叠会改变窗体高度，圆角 Region 需同步重算
			UpdateRoundedRegion();
			Refresh();
		}
	}

	private void minifyToolStripMenuItem_Click(object sender, EventArgs e)
	{
		if (isMinified)
		{
			base.Height = prevHeight;
			isMinified = false;
		}
		fenceInfo.CanMinify = minifyToolStripMenuItem.Checked;
		Save();
	}

	private void FenceWindow_Click(object sender, EventArgs e)
	{
		shouldUpdateSelection = true;
		Refresh();
	}

	protected override void OnDoubleClick(EventArgs e)
	{
		base.OnDoubleClick(e);
	}

	private void FenceWindow_DoubleClick(object sender, EventArgs e)
	{
		MouseEventArgs args = e as MouseEventArgs;
		if (args != null && args.Y < titleHeight && !lockedToolStripMenuItem.Checked)
		{
			BeginRenameTitle();
			return;
		}
		string item = hoveringItem;
		if (!string.IsNullOrEmpty(item))
		{
			FenceEntry entry = FenceEntry.FromPath(item);
			entry?.Open();
		}
		shouldRunDoubleClick = false;
		Invalidate();
	}

	private void FenceWindow_Paint(object sender, PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;

		// 圆角面板：整窗按圆角裁剪，四角露出桌面而不是硬直角。
		// DropShadow 已在 Win32 层加了投影，这里只负责表面。
		Rectangle panelBounds = new Rectangle(0, 0, base.Width, base.Height);
		int corner = S(Theme.CornerRadius);
		GraphicsState panelState = graphics.Save();
		{
			using (GraphicsPath panelPath = Theme.CreateRoundedRect(panelBounds, corner))
				graphics.SetClip(panelPath, CombineMode.Intersect);
			Theme.FillRoundedRect(graphics, panelBounds, corner, Theme.PanelBackground, Theme.PanelAlpha);

			// 标题栏：底部与内容区之间加一条极淡的分隔线，形成层次。
			Rectangle titleBounds = new Rectangle(0, 0, base.Width, titleHeight);
			Theme.FillRoundedRect(graphics, titleBounds, corner, Theme.TitleBackground, Theme.TitleAlpha);
			// 用方形补丁把标题栏下缘两角的圆角补平，避免出现缺口。
			using (SolidBrush titlePatch = new SolidBrush(Color.FromArgb(Theme.TitleAlpha, Theme.TitleBackground)))
				graphics.FillRectangle(titlePatch, 0, titleHeight - S(2), base.Width, S(2));

			// 标题文字：左侧留出拖拽把手的小圆点，视觉上表明标题栏可拖动。
			using (SolidBrush gripBrush = new SolidBrush(Color.FromArgb(120, Theme.TextSecondary)))
			{
				int gripY = titleHeight / 2;
				for (int i = -1; i <= 1; i++)
					graphics.FillEllipse(gripBrush, S(8), gripY + i * S(3) - S(1), S(2), S(2));
			}
			if (titleEditBox == null)
			{
				Rectangle textRect = new Rectangle(S(16), titleOffset, base.Width - S(32), titleHeight - titleOffset);
				TextRenderer.DrawText(graphics, Text, titleFont, textRect, Theme.TextPrimary,
					TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter
					| TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			}
		}
		graphics.Restore(panelState);

		// 面板描边画在最上层，避免内容贴边时被裁掉
		Theme.DrawRoundedRect(graphics, Rectangle.Inflate(panelBounds, -1, -1), corner, Theme.PanelBorder, 255, S(Theme.BorderWidth));

		int num = itemPadding;
		int num2 = itemPadding;
		scrollHeight = 0;
		RefreshRenderedItems();
		GraphicsState contentState = graphics.Save();
		graphics.SetClip(new Rectangle(S(1), titleHeight, base.Width - S(2), Math.Max(0, base.Height - titleHeight - S(1))));
		foreach (RenderedItem item in renderedItems)
		{
			RenderEntry(graphics, item.Entry, num, num2 + titleHeight - scrollOffset);
			int num3 = num2 + ItemPhysicalHeight();
			if (num3 > scrollHeight)
			{
				scrollHeight = num3;
			}
			num += itemWidth + itemPadding;
			if (num + itemWidth > base.ClientSize.Width)
			{
				num = itemPadding;
				num2 += ItemPhysicalHeight() + itemPadding;
			}
		}
		scrollHeight -= base.ClientRectangle.Height - titleHeight;
		if (scrollHeight > 0)
		{
			int num4 = base.Height - titleHeight - scrollHeight;
			// 细滚动条：轨道半透明，滑块用强调色，端部做圆角
			int trackW = S(4);
			int trackX = base.Width - trackW - S(2);
			using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
				graphics.FillRectangle(trackBrush, trackX, titleHeight, trackW, Math.Max(0, base.Height - titleHeight));
			int thumbH = Math.Max(S(24), (int)((float)(base.Height - titleHeight) * (base.Height - titleHeight)
				/ (float)((base.Height - titleHeight) + scrollHeight)));
			Theme.FillRoundedRect(graphics, new Rectangle(trackX, titleHeight + scrollOffset, trackW, thumbH),
				trackW / 2, Theme.Accent, 190);
		}
		DrawDropIndicator(graphics);
		graphics.Restore(contentState);
		if (shouldUpdateSelection && !hasSelectionUpdated)
		{
			selectedItem = null;
		}
		if (!hasHoverUpdated)
		{
			hoveringItem = null;
		}
		shouldRunDoubleClick = false;
		shouldUpdateSelection = false;
		hasSelectionUpdated = false;
		hasHoverUpdated = false;
	}

	private void RefreshRenderedItems()
	{
		renderedItems.Clear();
		for (int index = 0; index < fenceInfo.Files.Count; index++)
		{
			FenceEntry entry = FenceEntry.FromPath(fenceInfo.Files[index]);
			if (entry != null)
				renderedItems.Add(new RenderedItem(index, entry));
		}
	}

	private int ItemPhysicalHeight()
	{
		return iconBox + itemPadding + textHeight;
	}

	private void RenderEntry(Graphics g, FenceEntry entry, int x, int y)
	{
		string name = entry.Name;
		Bitmap bitmap = null;
		bool ownsBitmap = false;
		try
		{
			if (entry.Type == EntryType.File && thumbnailProvider.IsSupported(entry.Path))
			{
				using Icon icon = thumbnailProvider.GenerateThumbnail(entry.Path);
				if (icon != null)
				{
					using Bitmap iconBitmap = icon.ToBitmap();
					bitmap = HiResIcons.ScaleBitmap(iconBitmap, iconBox);
					ownsBitmap = true;
				}
			}
			if (bitmap == null)
			{
				bitmap = HiResIcons.Get(entry.Path, entry.Type == EntryType.Folder, iconBox);
			}
		}
		catch
		{
			bitmap = null;
		}
		Rectangle bounds = new Rectangle(x - S(2), y + iconBox + S(4), itemWidth, textHeight);
		Rectangle rect = new Rectangle(x - S(2), y - S(2), itemWidth + S(4), iconBox + textHeight + S(8));
		Point point = PointToClient(Control.MousePosition);
		bool flag = point.X >= rect.X && point.Y >= rect.Y && point.X < rect.Right && point.Y < rect.Bottom;
		if (flag)
		{
			hoveringItem = entry.Path;
			hasHoverUpdated = true;
		}
		if (flag && shouldUpdateSelection)
		{
			selectedItem = entry.Path;
			shouldUpdateSelection = false;
			hasSelectionUpdated = true;
		}
		// 悬停/选中统一用圆角卡片：hover 用强调色浅底，选中用强调色描边+更实底
		bool isSelected = selectedItem == entry.Path;
		int cardRadius = S(6);
		if (isSelected)
		{
			Theme.FillRoundedRect(g, rect, cardRadius, Theme.Accent, 150);
			Theme.DrawRoundedRect(g, rect, cardRadius, Theme.Accent, 255, S(1));
		}
		else if (flag)
		{
			Theme.FillRoundedRect(g, rect, cardRadius, Theme.AccentSoft, 140);
		}
		if (bitmap != null)
		{
			g.DrawImage(bitmap, x + (itemWidth - iconBox) / 2, y, iconBox, iconBox);
		}
		TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
		TextRenderer.DrawText(g, name, iconFont, bounds, Theme.TextSecondary, flags);
		if (ownsBitmap)
			bitmap?.Dispose();
	}

	private void renameToolStripMenuItem_Click(object sender, EventArgs e)
	{
		EditDialog editDialog = new EditDialog(Text);
		if (editDialog.ShowDialog(this) == DialogResult.OK)
		{
			Text = editDialog.NewName;
			fenceInfo.Name = Text;
			Refresh();
			Save();
		}
	}

	private void newFenceToolStripMenuItem_Click(object sender, EventArgs e)
	{
		FenceManager.Instance.CreateFence("新建围栏");
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		bool appExiting = TrayController.ShuttingDown;
		DiagLog.Log("OnFormClosing: fence='" + ((fenceInfo != null) ? fenceInfo.Name : "?") + "' reason=" + e.CloseReason.ToString() + " allowClose=" + allowClose + " appExiting=" + appExiting);
		if (!allowClose && !appExiting && e.CloseReason != CloseReason.WindowsShutDown)
		{
			DiagLog.Log("  -> 已拦截外部关闭(" + e.CloseReason.ToString() + ")，窗口保持");
			e.Cancel = true;
		}
		else
		{
			DiagLog.Log("  -> 放行关闭");
			base.OnFormClosing(e);
		}
	}

	private void FenceWindow_FormClosed(object sender, FormClosedEventArgs e)
	{
		TrayController.Unregister(this);
		SnapManager.Unregister(base.Handle);
		// 最后一个围栏关闭时收起辅助线覆盖层
		snapOverlay.Hide();
		DiagLog.Log("FormClosed: '" + ((fenceInfo != null) ? fenceInfo.Name : "?") + "' 剩余OpenForms=" + Application.OpenForms.Count + " 退出中=" + TrayController.ShuttingDown);
		// 不再因"窗口全部关闭"而结束进程：托盘常驻，围栏可用托盘重新显示或新建。
		// 移除围栏(Remove Fence)只影响单个围栏；Application.Exit() 现仅由 TrayController.ExitApplication 触发。
	}

	private void Save()
	{
		lock (saveLock)
		{
			FenceManager.Instance.UpdateFence(fenceInfo);
		}
	}

	/// <summary>
	/// 把围栏移动/缩放到指定屏幕矩形（物理像素），并持久化。
	/// 供托盘的自动排布与分辨率变化后的重新适配使用。
	/// </summary>
	public void ApplyLayout(Rectangle bounds)
	{
		base.Location = new Point(bounds.X, bounds.Y);
		base.Width = Math.Max(S(160), bounds.Width);
		base.Height = Math.Max(S(120), bounds.Height);
		// 折叠状态下不要把高度撑开，保持折叠语义
		if (isMinified)
			base.Height = titleHeight;

		fenceInfo.PosX = (int)Math.Round((float)base.Location.X / s);
		fenceInfo.PosY = (int)Math.Round((float)base.Location.Y / s);
		fenceInfo.Width = (int)Math.Round((float)base.Width / s);
		fenceInfo.Height = (int)Math.Round((float)(isMinified ? prevHeight : base.Height) / s);
		fenceInfo.ScreenX = Screen.PrimaryScreen?.Bounds.Width ?? fenceInfo.ScreenX;
		fenceInfo.ScreenY = Screen.PrimaryScreen?.Bounds.Height ?? fenceInfo.ScreenY;
		fenceInfo.NeedsRelayout = false;

		UpdateRoundedRegion();
		Save();
		Invalidate();
	}

	/// <summary>解除折叠，使围栏回到完整高度（自动排布前需要）。</summary>
	public void EnsureExpanded()
	{
		if (!isMinified)
			return;
		isMinified = false;
		base.Height = prevHeight;
		UpdateRoundedRegion();
	}

	/// <summary>该围栏记录的创建时屏幕宽度，供分辨率自适应判断使用。</summary>
	public int FenceScreenWidth => fenceInfo.ScreenX;

	/// <summary>当前围栏在屏幕上的矩形（含边框），用于对齐计算。</summary>
	private Rectangle GetScreenBounds()
	{
		return base.Bounds;
	}

	/// <summary>
	/// 处理拖动中的对齐吸附：计算需要的偏移，写回 RECT 并显示辅助线。
	/// </summary>
	private void HandleMoving(IntPtr rectPointer)
	{
		if (rectPointer == IntPtr.Zero || lockedToolStripMenuItem.Checked)
		{
			snapOverlay.Hide();
			return;
		}

		try
		{
		Rectangle moving = (Rectangle)Marshal.PtrToStructure(rectPointer, typeof(Rectangle));
			SnapResult snap = SnapManager.Calculate(moving, base.Handle, Screen.FromHandle(base.Handle).WorkingArea);
			if (!snap.HasSnap)
			{
				snapOverlay.Hide();
				return;
			}

			// 修正 RECT：X/Y 加上吸附偏移，并把结果写回驱动移动循环
			Rectangle snapped = Rectangle.FromLTRB(
				moving.Left + snap.DeltaX,
				moving.Top + snap.DeltaY,
				moving.Right + snap.DeltaX,
				moving.Bottom + snap.DeltaY);

			Marshal.StructureToPtr(snapped, rectPointer, false);

			// 辅助线用屏幕坐标；覆盖层与围栏都位于桌面坐标系，可直接绘制
			snapOverlay.Show(snap.Guides);
		}
		catch (Exception ex)
		{
			DiagLog.Log("对齐吸附计算失败: " + ex.Message);
			snapOverlay.Hide();
		}
	}

	/// <summary>拖动结束（鼠标释放）后隐藏辅助线。</summary>
	private void FenceWindow_MouseCaptureChanged(object sender, EventArgs e)
	{
		if (!base.Capture)
			snapOverlay.Hide();
	}

	/// <summary>该围栏记录的创建时屏幕高度。</summary>
	public int FenceScreenHeight => fenceInfo.ScreenY;

	/// <summary>是否因分辨率变化而需要重新适配。</summary>
	public bool NeedsRelayout => fenceInfo.NeedsRelayout;

	private void FenceWindow_LocationChanged(object sender, EventArgs e)
	{
		if (!initialized)
			return;
		throttledMove.Run(delegate
		{
			fenceInfo.PosX = (int)Math.Round((float)base.Location.X / s);
			fenceInfo.PosY = (int)Math.Round((float)base.Location.Y / s);
			Save();
		});
	}

	private void lockedToolStripMenuItem_Click(object sender, EventArgs e)
	{
		fenceInfo.Locked = lockedToolStripMenuItem.Checked;
		Save();
	}

	private void FenceWindow_Load(object sender, EventArgs e)
	{
	}

	private void titleSizeToolStripMenuItem_Click(object sender, EventArgs e)
	{
		HeightDialog heightDialog = new HeightDialog(fenceInfo.TitleHeight);
		if (heightDialog.ShowDialog(this) == DialogResult.OK)
		{
			fenceInfo.TitleHeight = heightDialog.TitleHeight;
			logicalTitleHeight = heightDialog.TitleHeight;
			titleHeight = S(logicalTitleHeight);
			ReloadFonts();
			Minify();
			if (isMinified)
			{
				base.Height = titleHeight;
			}
			UpdateRoundedRegion();
			Refresh();
			Save();
		}
	}

	private void FenceWindow_MouseClick(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Right)
		{
			contextMenuItem = HitTestItem(e.Location);
			if (contextMenuItem != null && Control.ModifierKeys.HasFlag(Keys.Shift))
			{
				shellContextMenu.ShowContextMenu(new FileInfo[1]
				{
					new FileInfo(contextMenuItem)
				}, Control.MousePosition);
				contextMenuItem = null;
			}
			else
			{
				appContextMenu.Show(this, e.Location);
			}
		}
	}

	private void FenceWindow_MouseDown(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && !lockedToolStripMenuItem.Checked)
		{
			dragCandidateItem = HitTestItem(e.Location);
			dragStartPoint = e.Location;
		}
	}

	private void FenceWindow_MouseUp(object sender, MouseEventArgs e)
	{
		dragCandidateItem = null;
	}

	/// <summary>该点是否落在某个图标上。标题栏里也要留出图标区，让用户可以拖动图标。</summary>
	private bool IsPointOverIcon(Point point)
	{
		// 图标网格从标题栏下方开始，标题栏内理论上没有图标；
		// 这里仍做一次判定，保证拖动标题与拖动图标的边界清晰。
		if (point.Y < titleHeight)
			return false;
		return HitTestItem(point) != null;
	}

	private string HitTestItem(Point point)
	{
		int columns = GetColumnCount();
		int rowHeight = ItemPhysicalHeight() + itemPadding;
		for (int index = 0; index < renderedItems.Count; index++)
		{
			int column = index % columns;
			int row = index / columns;
			int x = itemPadding + column * (itemWidth + itemPadding);
			int y = titleHeight + itemPadding + row * rowHeight - scrollOffset;
			Rectangle bounds = new Rectangle(x - S(2), y - S(2), itemWidth + S(4), iconBox + textHeight + S(8));
			if (bounds.Contains(point))
				return renderedItems[index].Entry.Path;
		}
		return null;
	}

	private int GetColumnCount()
	{
		return CalculateColumnCount(base.ClientSize.Width, itemWidth, itemPadding);
	}

	internal static int CalculateColumnCount(int clientWidth, int width, int padding)
	{
		if (clientWidth <= padding + width)
			return 1;
		return 1 + (clientWidth - padding - width) / Math.Max(1, width + padding);
	}

	private int GetDropInsertIndex(Point point)
	{
		int columns = GetColumnCount();
		int cellWidth = itemWidth + itemPadding;
		int rowHeight = ItemPhysicalHeight() + itemPadding;
		int contentX = Math.Max(0, point.X - itemPadding);
		int contentY = Math.Max(0, point.Y - titleHeight + scrollOffset - itemPadding);
		int column = Math.Min(columns - 1, contentX / Math.Max(1, cellWidth));
		int row = contentY / Math.Max(1, rowHeight);
		int visualIndex = row * columns + column;
		if (contentX % Math.Max(1, cellWidth) > itemWidth / 2)
			visualIndex++;
		visualIndex = Math.Max(0, Math.Min(visualIndex, renderedItems.Count));
		return VisualToModelInsertIndex(visualIndex);
	}

	private int VisualToModelInsertIndex(int visualIndex)
	{
		return visualIndex < renderedItems.Count ? renderedItems[visualIndex].ModelIndex : fenceInfo.Files.Count;
	}

	private int ModelToVisualInsertIndex(int modelIndex)
	{
		int visualIndex = 0;
		while (visualIndex < renderedItems.Count && renderedItems[visualIndex].ModelIndex < modelIndex)
			visualIndex++;
		return visualIndex;
	}

	private void UpdateDropInsertIndex(int screenX, int screenY)
	{
		int index = GetDropInsertIndex(PointToClient(new Point(screenX, screenY)));
		if (dropInsertIndex != index)
		{
			dropInsertIndex = index;
			Invalidate();
		}
	}

	private void ClearDropInsertIndex()
	{
		if (dropInsertIndex >= 0)
		{
			dropInsertIndex = -1;
			Invalidate();
		}
	}

	private void DrawDropIndicator(Graphics graphics)
	{
		if (dropInsertIndex < 0 || lockedToolStripMenuItem.Checked)
			return;
		int columns = GetColumnCount();
		int visualIndex = ModelToVisualInsertIndex(dropInsertIndex);
		int column = visualIndex % columns;
		int row = visualIndex / columns;
		int x = itemPadding + column * (itemWidth + itemPadding) - S(3);
		int y = titleHeight + itemPadding + row * (ItemPhysicalHeight() + itemPadding) - scrollOffset;
		// 插入指示线用强调色并带端点圆头，与整体视觉统一
		using Pen pen = new Pen(Color.FromArgb(240, Theme.Accent), Math.Max(2, S(2)))
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round
		};
		graphics.DrawLine(pen, x, y, x, y + ItemPhysicalHeight());
	}

	private bool CanAcceptInternalDrag(IDataObject data)
	{
		if (lockedToolStripMenuItem.Checked || data == null || !data.GetDataPresent(typeof(InternalDragItem)))
			return false;
		InternalDragItem item = data.GetData(typeof(InternalDragItem)) as InternalDragItem;
		return item != null && item.Source != null && !item.Source.lockedToolStripMenuItem.Checked && item.Source.fenceInfo.Files.Contains(item.Path);
	}

	private static void MoveFenceItem(FenceWindow source, FenceWindow target, string path, int insertIndex)
	{
		if (!MoveItem(source.fenceInfo.Files, target.fenceInfo.Files, path, insertIndex))
			return;
		target.selectedItem = path;
		if (source == target)
		{
			source.Save();
			source.Invalidate();
			return;
		}
		source.Save();
		target.Save();
		source.Invalidate();
		target.Invalidate();
	}

	private static bool MoveItem(IList<string> sourceItems, IList<string> targetItems, string path, int insertIndex)
	{
		int sourceIndex = sourceItems.IndexOf(path);
		if (sourceIndex < 0)
			return false;

		if (ReferenceEquals(sourceItems, targetItems))
		{
			sourceItems.RemoveAt(sourceIndex);
			if (insertIndex > sourceIndex)
				insertIndex--;
			insertIndex = Math.Max(0, Math.Min(insertIndex, sourceItems.Count));
			sourceItems.Insert(insertIndex, path);
			return true;
		}

		sourceItems.RemoveAt(sourceIndex);
		int existingIndex = targetItems.IndexOf(path);
		if (existingIndex >= 0)
		{
			targetItems.RemoveAt(existingIndex);
			if (insertIndex > existingIndex)
				insertIndex--;
		}
		insertIndex = Math.Max(0, Math.Min(insertIndex, targetItems.Count));
		targetItems.Insert(insertIndex, path);
		return true;
	}

	internal static bool RemoveItem(IList<string> items, string path)
	{
		if (items == null || string.IsNullOrEmpty(path))
			return false;

		bool removed = false;
		for (int index = items.Count - 1; index >= 0; index--)
		{
			if (!string.Equals(items[index], path, StringComparison.OrdinalIgnoreCase))
				continue;
			items.RemoveAt(index);
			removed = true;
		}
		return removed;
	}

	private void FenceWindow_MouseWheel(object sender, MouseEventArgs e)
	{
		if (scrollHeight >= 1)
		{
			int num = S(14);
			scrollOffset -= Math.Sign(e.Delta) * num;
			if (scrollOffset < 0)
			{
				scrollOffset = 0;
			}
			if (scrollOffset > scrollHeight)
			{
				scrollOffset = scrollHeight;
			}
			Invalidate();
		}
	}

	private void ThumbnailProvider_IconThumbnailLoaded(object sender, EventArgs e)
	{
		InvalidateFromWorker();
	}

	private void HiResIcons_IconLoaded(object sender, EventArgs e)
	{
		InvalidateFromWorker();
	}

	private void InvalidateFromWorker()
	{
		if (IsDisposed || !IsHandleCreated)
			return;
		try
		{
			BeginInvoke((Action)(() => Invalidate()));
		}
		catch (InvalidOperationException)
		{
		}
	}

	private bool ItemExists(string path)
	{
		if (!File.Exists(path))
		{
			return Directory.Exists(path);
		}
		return true;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			HiResIcons.IconLoaded -= HiResIcons_IconLoaded;
			thumbnailProvider.IconThumbnailLoaded -= ThumbnailProvider_IconThumbnailLoaded;
			thumbnailProvider.Dispose();
			throttledMove.Dispose();
			throttledResize.Dispose();
			titleFont?.Dispose();
			iconFont?.Dispose();
		}
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.components = new System.ComponentModel.Container();
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SimpleGrid.FenceWindow));
		this.appContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
		this.deleteItemToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.openInExplorerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.lockedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.minifyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.renameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.titleSizeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
		this.newFenceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.appContextMenu.SuspendLayout();
		base.SuspendLayout();
		resources.ApplyResources(this.appContextMenu, "appContextMenu");
		this.appContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[9] { this.deleteItemToolStripMenuItem, this.openInExplorerToolStripMenuItem, this.lockedToolStripMenuItem, this.minifyToolStripMenuItem, this.renameToolStripMenuItem, this.titleSizeToolStripMenuItem, this.toolStripSeparator1, this.newFenceToolStripMenuItem, this.exitToolStripMenuItem });
		this.appContextMenu.Name = "contextMenuStrip1";
		this.appContextMenu.RenderMode = System.Windows.Forms.ToolStripRenderMode.Professional;
		this.appContextMenu.Opening += new System.ComponentModel.CancelEventHandler(contextMenuStrip1_Opening);
		resources.ApplyResources(this.deleteItemToolStripMenuItem, "deleteItemToolStripMenuItem");
		this.deleteItemToolStripMenuItem.Name = "deleteItemToolStripMenuItem";
		this.deleteItemToolStripMenuItem.Click += new System.EventHandler(deleteItemToolStripMenuItem_Click);
		this.openInExplorerToolStripMenuItem.Name = "openInExplorerToolStripMenuItem";
		this.openInExplorerToolStripMenuItem.Text = "在资源管理器中打开";
		this.openInExplorerToolStripMenuItem.Click += new System.EventHandler(openInExplorerToolStripMenuItem_Click);
		resources.ApplyResources(this.lockedToolStripMenuItem, "lockedToolStripMenuItem");
		this.lockedToolStripMenuItem.CheckOnClick = true;
		this.lockedToolStripMenuItem.Name = "lockedToolStripMenuItem";
		this.lockedToolStripMenuItem.Click += new System.EventHandler(lockedToolStripMenuItem_Click);
		resources.ApplyResources(this.minifyToolStripMenuItem, "minifyToolStripMenuItem");
		this.minifyToolStripMenuItem.CheckOnClick = true;
		this.minifyToolStripMenuItem.Name = "minifyToolStripMenuItem";
		this.minifyToolStripMenuItem.Click += new System.EventHandler(minifyToolStripMenuItem_Click);
		resources.ApplyResources(this.renameToolStripMenuItem, "renameToolStripMenuItem");
		this.renameToolStripMenuItem.Name = "renameToolStripMenuItem";
		this.renameToolStripMenuItem.Click += new System.EventHandler(renameToolStripMenuItem_Click);
		resources.ApplyResources(this.titleSizeToolStripMenuItem, "titleSizeToolStripMenuItem");
		this.titleSizeToolStripMenuItem.Name = "titleSizeToolStripMenuItem";
		this.titleSizeToolStripMenuItem.Click += new System.EventHandler(titleSizeToolStripMenuItem_Click);
		resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
		this.toolStripSeparator1.Name = "toolStripSeparator1";
		resources.ApplyResources(this.newFenceToolStripMenuItem, "newFenceToolStripMenuItem");
		this.newFenceToolStripMenuItem.Name = "newFenceToolStripMenuItem";
		this.newFenceToolStripMenuItem.Click += new System.EventHandler(newFenceToolStripMenuItem_Click);
		resources.ApplyResources(this.exitToolStripMenuItem, "exitToolStripMenuItem");
		this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
		this.exitToolStripMenuItem.Click += new System.EventHandler(exitToolStripMenuItem_Click);
		resources.ApplyResources(this, "$this");
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		// 圆角由 Region 裁出真实透明的四角，因此不需要 TransparencyKey；
		// 使用 TransparencyKey 会连阴影里的黑色像素一起抠掉，反而破坏投影。
		this.BackColor = Theme.PanelBackground;
		this.DoubleBuffered = true;
		this.ForeColor = System.Drawing.SystemColors.ControlText;
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.MinimizeBox = false;
		base.Name = "FenceWindow";
		base.ShowIcon = false;
		base.ShowInTaskbar = false;
		base.FormClosed += new System.Windows.Forms.FormClosedEventHandler(FenceWindow_FormClosed);
		base.Load += new System.EventHandler(FenceWindow_Load);
		base.LocationChanged += new System.EventHandler(FenceWindow_LocationChanged);
		base.Click += new System.EventHandler(FenceWindow_Click);
		base.DragDrop += new System.Windows.Forms.DragEventHandler(FenceWindow_DragDrop);
		base.DragEnter += new System.Windows.Forms.DragEventHandler(FenceWindow_DragEnter);
		base.DragOver += new System.Windows.Forms.DragEventHandler(FenceWindow_DragOver);
		base.DragLeave += new System.EventHandler(FenceWindow_DragLeave);
		base.Paint += new System.Windows.Forms.PaintEventHandler(FenceWindow_Paint);
		base.DoubleClick += new System.EventHandler(FenceWindow_DoubleClick);
		base.MouseClick += new System.Windows.Forms.MouseEventHandler(FenceWindow_MouseClick);
		base.MouseEnter += new System.EventHandler(FenceWindow_MouseEnter);
		base.MouseLeave += new System.EventHandler(FenceWindow_MouseLeave);
		base.MouseMove += new System.Windows.Forms.MouseEventHandler(FenceWindow_MouseMove);
		base.MouseDown += new System.Windows.Forms.MouseEventHandler(FenceWindow_MouseDown);
		base.MouseUp += new System.Windows.Forms.MouseEventHandler(FenceWindow_MouseUp);
		base.Resize += new System.EventHandler(FenceWindow_Resize);
		this.appContextMenu.ResumeLayout(false);
		base.ResumeLayout(false);
	}
}
