using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace SimpleGrid;

/// <summary>
/// 全局视觉规范。配色与圆角语言参考 iTop Easy Desktop：
/// 深色半透明面板 + 蓝色强调色 + 轻量圆角 + 无边框标题栏。
/// 所有尺寸为 96 DPI 逻辑像素，实际绘制时由 FenceWindow 按缩放比换算。
/// </summary>
internal static class Theme
{
	/// <summary>面板底色（深灰蓝，比纯黑更有层次）。</summary>
	public static readonly Color PanelBackground = Color.FromArgb(28, 30, 36);

	/// <summary>标题栏底色，比面板略亮以形成层次。</summary>
	public static readonly Color TitleBackground = Color.FromArgb(44, 47, 56);

	/// <summary>强调色：选中、悬停、插入指示线统一使用。</summary>
	public static readonly Color Accent = Color.FromArgb(0, 150, 220);

	/// <summary>强调色浅色变体，用于 hover 背景。</summary>
	public static readonly Color AccentSoft = Color.FromArgb(38, 74, 96);

	/// <summary>面板/标题栏整体不透明度。</summary>
	public static byte PanelAlpha = 216;

	/// <summary>标题栏整体不透明度。</summary>
	public static byte TitleAlpha = 235;

	/// <summary>圆角半径（逻辑像素）。</summary>
	public const int CornerRadius = 10;

	/// <summary>外发光/描边宽度。</summary>
	public const int BorderWidth = 1;

	/// <summary>主文本色。</summary>
	public static readonly Color TextPrimary = Color.FromArgb(238, 240, 244);

	/// <summary>次要文本色（图标名）。</summary>
	public static readonly Color TextSecondary = Color.FromArgb(196, 202, 212);

	/// <summary>面板描边色。</summary>
	public static readonly Color PanelBorder = Color.FromArgb(64, 70, 82);

	/// <summary>
	/// 构建圆角矩形路径。WinForms 的 FillPath 抗锯齿在圆角处会有轻微锯齿，
	/// 这里配合 SmoothingMode.AntiAlias 使用即可。
	/// </summary>
	public static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
	{
		GraphicsPath path = new GraphicsPath();
		if (radius <= 0)
		{
			path.AddRectangle(bounds);
			return path;
		}
		// 半径不能超过边长的一半，否则 AddArc 会画出反折线
		int d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
		path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
		path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
		path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
		path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
		path.CloseFigure();
		return path;
	}

	/// <summary>用指定颜色与透明度填充圆角矩形。</summary>
	public static void FillRoundedRect(Graphics g, Rectangle bounds, int radius, Color color, byte alpha)
	{
		using (GraphicsPath path = CreateRoundedRect(bounds, radius))
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, color)))
		{
			g.FillPath(brush, path);
		}
	}

	/// <summary>描边圆角矩形。</summary>
	public static void DrawRoundedRect(Graphics g, Rectangle bounds, int radius, Color color, byte alpha, int width)
	{
		using (GraphicsPath path = CreateRoundedRect(bounds, radius))
		using (Pen pen = new Pen(Color.FromArgb(alpha, color), width))
		{
			g.DrawPath(pen, path);
		}
	}
}