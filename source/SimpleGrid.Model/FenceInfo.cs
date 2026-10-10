using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SimpleGrid.Model;

public class FenceInfo
{
	public Guid Id { get; set; }

	public string Name { get; set; }

	public int PosX { get; set; }

	public int PosY { get; set; }

	public int Width { get; set; }

	public int Height { get; set; }

	public bool Locked { get; set; }

	public bool CanMinify { get; set; }

	public int TitleHeight { get; set; } = 35;

	/// <summary>
	/// 创建该围栏时的屏幕分辨率。用于分辨率/显示器变化后判断是否需要重新适配，
	/// 以及自动布局时按当前屏幕重新计算尺寸。旧版本 XML 没有此字段，反序列化为 0，
	/// 由 <see cref="EnsureScreenMetrics"/> 补齐。
	/// </summary>
	public int ScreenX { get; set; }

	public int ScreenY { get; set; }

	/// <summary>屏幕分辨率与围栏位置不匹配时（如换了显示器），标记为待重新适配。</summary>
	public bool NeedsRelayout { get; set; }

	public List<string> Files { get; set; } = new List<string>();

	/// <summary>
	/// 旧数据兼容：ScreenX/ScreenY 缺失或为 0 时，用当前主屏分辨率补齐并标记需要重排。
	/// </summary>
	public void EnsureScreenMetrics()
	{
		if (ScreenX > 0 && ScreenY > 0)
			return;
		ScreenX = Screen.PrimaryScreen?.Bounds.Width ?? 1920;
		ScreenY = Screen.PrimaryScreen?.Bounds.Height ?? 1080;
		NeedsRelayout = true;
	}

	public FenceInfo()
	{
	}

	public FenceInfo(Guid id)
	{
		Id = id;
	}
}
