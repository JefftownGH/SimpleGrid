using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SimpleGrid.Util;

/// <summary>
/// 围栏自动排布。给定屏幕可用区域和围栏数量，算出每个围栏的位置与尺寸，
/// 让它们铺满区域且尽量接近屏幕宽高比。
///
/// 移植自 BestNoFences 的 FormLayoutCalculator，保留其「余数补齐」与
/// 「单元格宽高比最接近屏幕宽高比」两个关键点：
///   - 不补齐会在网格右侧/底部留下裂缝；
///   - 只按数量平均分会得到细长的格子，与屏幕形状不匹配。
/// </summary>
internal static class FormLayoutCalculator
{
	/// <summary>屏幕与围栏之间保留的边距（逻辑像素）。</summary>
	private const int Margin = 40;

	/// <summary>围栏之间的间隔。</summary>
	private const int Gap = 16;

	/// <summary>单个围栏允许的最小尺寸，避免数量多时被压到不可用。</summary>
	private const int MinSize = 160;

	/// <summary>
	/// 按围栏数量计算网格排布。
	/// </summary>
	/// <param name="formCount">围栏数量。</param>
	/// <param name="area">目标区域（通常是主屏工作区）。</param>
	/// <param name="preferMoreRows">平局时是否优先多行。靠左/靠右布局偏扁，传 true。</param>
	/// <returns>数量为 formCount 的位置/尺寸数组，顺序与传入一致。</returns>
	public static Rectangle[] CalculateLayout(int formCount, Rectangle area, bool preferMoreRows)
	{
		if (formCount <= 0)
			return Array.Empty<Rectangle>();

		Rectangle usable = InflateArea(area);
		if (formCount == 1)
			return new[] { usable };

		int rows = 1;
		int columns = formCount;

		// 遍历所有可行的行列组合，挑单元格宽高比最贴近屏幕宽高比的一个。
		double screenRatio = usable.Width / (double)usable.Height;
		double bestScore = double.MaxValue;
		for (int r = 1; r <= formCount; r++)
		{
			int c = (int)Math.Ceiling(formCount / (double)r);
			double cellW = (usable.Width - Gap * (c - 1)) / (double)c;
			double cellH = (usable.Height - Gap * (r - 1)) / (double)r;
			if (cellW < MinSize || cellH < MinSize)
				break; // 再往下只会更小，不可能更优

			double score = Math.Abs(cellW / cellH - screenRatio);
			if (preferMoreRows && r == c)
				score -= 0.01; // 平局时偏好多行
			if (score < bestScore)
			{
				bestScore = score;
				rows = r;
				columns = c;
			}
		}

		int cellWidth = (usable.Width - Gap * (columns - 1)) / columns;
		int cellHeight = (usable.Height - Gap * (rows - 1)) / rows;
		if (cellWidth < MinSize)
			cellWidth = MinSize;
		if (cellHeight < MinSize)
			cellHeight = MinSize;

		// 余数补齐：最后一个格子吃掉除不尽的余量，保证整体铺满不留缝。
		int lastColumnWidth = usable.Width - cellWidth * (columns - 1) - Gap * (columns - 1);
		int lastRowHeight = usable.Height - cellHeight * (rows - 1) - Gap * (rows - 1);

		Rectangle[] result = new Rectangle[formCount];
		for (int i = 0; i < formCount; i++)
		{
			int row = i / columns;
			int column = i % columns;
			int w = column == columns - 1 ? lastColumnWidth : cellWidth;
			int h = row == rows - 1 ? lastRowHeight : cellHeight;
			result[i] = new Rectangle(
				usable.X + column * (cellWidth + Gap),
				usable.Y + row * (cellHeight + Gap),
				w, h);
		}
		return result;
	}

	/// <summary>
	/// 在指定屏幕区域内，按围栏数量自动排布。
	/// </summary>
	public static Rectangle[] CalculateLayoutOnPrimaryScreen(int formCount, bool preferMoreRows = false)
	{
		Rectangle bounds = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
		return CalculateLayout(formCount, bounds, preferMoreRows);
	}

	private static Rectangle InflateArea(Rectangle area)
	{
		int marginX = Math.Min(Margin, Math.Max(0, area.Width / 10));
		int marginY = Math.Min(Margin, Math.Max(0, area.Height / 10));
		return Rectangle.Inflate(area, -marginX, -marginY);
	}
}

/// <summary>
/// 屏幕区域的几种预设朝向，对应托盘菜单里的「靠左 / 居中 / 靠右 / 自动」。
/// </summary>
internal enum FenceScreenRegion
{
	Auto,
	Left,
	Center,
	Right,
	TopRight
}

/// <summary>
/// 按区域类型算出目标矩形，再交给 <see cref="FormLayoutCalculator"/> 做网格排布。
/// </summary>
internal static class ScreenRegionGenerator
{
	public static Rectangle[] GenerateLayout(FenceScreenRegion region, int formCount)
	{
		Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);

		// 靠左/靠右/右上等横向分栏需要更扁的网格，Auto 则允许接近屏幕比例。
		bool preferMoreRows = region != FenceScreenRegion.Auto;

		switch (region)
		{
			case FenceScreenRegion.Left:
			{
				Rectangle half = new Rectangle(work.X, work.Y, work.Width / 2, work.Height);
				return FormLayoutCalculator.CalculateLayout(formCount, half, preferMoreRows);
			}
			case FenceScreenRegion.Right:
			{
				Rectangle half = new Rectangle(work.X + work.Width / 2, work.Y, work.Width - work.Width / 2, work.Height);
				return FormLayoutCalculator.CalculateLayout(formCount, half, preferMoreRows);
			}
			case FenceScreenRegion.TopRight:
			{
				Rectangle corner = new Rectangle(work.X + work.Width / 2, work.Y, work.Width - work.Width / 2, work.Height / 2);
				return FormLayoutCalculator.CalculateLayout(formCount, corner, preferMoreRows);
			}
			default:
				return FormLayoutCalculator.CalculateLayout(formCount, work, preferMoreRows);
		}
	}
}