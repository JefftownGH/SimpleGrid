using System;
using System.Collections.Generic;
using System.Drawing;

namespace SimpleGrid.Util;

/// <summary>
/// 围栏移动时的边缘对齐计算。
///
/// 设计目标：拖动围栏时，靠近其它围栏或屏幕工作区边缘时自动吸附，并给出可见的辅助线，
/// 让用户知道当前对齐到了哪里。吸附阈值默认 8 像素（逻辑像素），
/// 与 Windows 自身的窗口吸附手感接近。
/// </summary>
internal sealed class SnapGuide
{
	/// <summary>辅助线方向。必须显式区分，不能靠 X/Y 是否为 0 判断——
	/// 屏幕左边缘的竖直线正好落在 X=0。</summary>
	public bool IsVertical;

	/// <summary>竖直线的 X 坐标（屏幕物理坐标）。</summary>
	public int X;

	/// <summary>水平线的 Y 坐标（屏幕物理坐标）。</summary>
	public int Y;

	/// <summary>该辅助线是否属于屏幕边缘（边缘线用更醒目的颜色）。</summary>
	public bool IsScreenEdge;
}

/// <summary>
/// 一次吸附计算的结果：需要施加的偏移，以及要绘制的辅助线。
/// </summary>
internal sealed class SnapResult
{
	/// <summary>X 方向需要修正的像素（正数向右）。</summary>
	public int DeltaX;

	/// <summary>Y 方向需要修正的像素（正数向下）。</summary>
	public int DeltaY;

	/// <summary>要绘制的辅助线。</summary>
	public readonly List<SnapGuide> Guides = new List<SnapGuide>();

	/// <summary>是否发生了吸附。</summary>
	public bool HasSnap => DeltaX != 0 || DeltaY != 0;
}

/// <summary>
/// 对齐吸附引擎。围栏在构造时注册、销毁时注销，计算时排除自身。
/// 线程安全：拖动计算发生在 UI 线程，注册注销也可能来自 UI 线程，
/// 这里用锁保证容器一致，避免枚举期间被修改。
/// </summary>
internal static class SnapManager
{
	/// <summary>吸附阈值（屏幕物理像素）。</summary>
	public const int SnapThreshold = 8;

	private static readonly List<SnapTarget> targets = new List<SnapTarget>();

	private static readonly object sync = new object();

	/// <summary>注册一个可参与对齐的窗口。</summary>
	public static void Register(IntPtr handle, Func<Rectangle> boundsProvider)
	{
		if (handle == IntPtr.Zero || boundsProvider == null)
			return;
		lock (sync)
		{
			UnregisterInternal(handle);
			targets.Add(new SnapTarget(handle, boundsProvider));
		}
	}

	public static void Unregister(IntPtr handle)
	{
		if (handle == IntPtr.Zero)
			return;
		lock (sync)
		{
			UnregisterInternal(handle);
		}
	}

	private static void UnregisterInternal(IntPtr handle)
	{
		for (int i = targets.Count - 1; i >= 0; i--)
		{
			if (targets[i].Handle == handle)
				targets.RemoveAt(i);
		}
	}

	/// <summary>
	/// 依据候选矩形计算吸附偏移。
	/// </summary>
	/// <param name="moving">当前被拖动围栏的矩形（屏幕物理坐标）。</param>
	/// <param name="exclude">要排除的窗口句柄（通常是自身）。</param>
	/// <param name="workArea">屏幕工作区，用于吸附到屏幕边缘。</param>
	public static SnapResult Calculate(Rectangle moving, IntPtr exclude, Rectangle workArea)
	{
		SnapResult result = new SnapResult();
		List<SnapTarget> snapshot = Snapshot();

		List<int> dxCandidates = new List<int>();
		List<int> dyCandidates = new List<int>();

		// ---- X 方向：与其它围栏及屏幕工作区的左/中/右对齐 ----
		XAxis axis = BuildXAxis(moving, snapshot, exclude, workArea);
		result.DeltaX = PickBest(axis.Candidates);
		if (result.DeltaX != 0)
		{
			// 吸附后 moving 的 Left 会落在某个参照边上，该边就是竖直辅助线
			result.Guides.Add(new SnapGuide
			{
				IsVertical = true,
				X = moving.Left + result.DeltaX,
				IsScreenEdge = axis.ScreenEdges.Contains(moving.Left + result.DeltaX)
			});
		}

		// ---- Y 方向：与其它围栏及屏幕工作区的上/中/下对齐 ----
		Axis yAxis = BuildYAxis(moving, snapshot, exclude, workArea);
		result.DeltaY = PickBest(yAxis.Candidates);
		if (result.DeltaY != 0)
		{
			result.Guides.Add(new SnapGuide
			{
				IsVertical = false,
				Y = moving.Top + result.DeltaY,
				IsScreenEdge = yAxis.ScreenEdges.Contains(moving.Top + result.DeltaY)
			});
		}

		return result;
	}

	/// <summary>X 轴候选集。</summary>
	private struct XAxis
	{
		public List<int> Candidates;
		public HashSet<int> ScreenEdges;
	}

	/// <summary>Y 轴候选集。</summary>
	private struct Axis
	{
		public List<int> Candidates;
		public HashSet<int> ScreenEdges;
	}

	private static XAxis BuildXAxis(Rectangle moving, List<SnapTarget> snapshot, IntPtr exclude, Rectangle workArea)
	{
		XAxis axis = new XAxis
		{
			Candidates = new List<int>(),
			ScreenEdges = new HashSet<int>()
		};
		int near = moving.Left;
		int far = moving.Right;
		int center = moving.X + moving.Width / 2;

		// 屏幕工作区左右边缘
		AddCandidate(axis, near, workArea.Left, true);
		AddCandidate(axis, far, workArea.Right, true);

		foreach (SnapTarget target in EnumerateOthers(snapshot, exclude))
		{
			Rectangle other = SafeBounds(target);
			if (other.Width <= 0 || other.Height <= 0)
				continue;
			int oNear = other.Left;
			int oFar = other.Right;
			int oCenter = other.X + other.Width / 2;
			AddCandidate(axis, near, oNear, false);
			AddCandidate(axis, far, oFar, false);
			AddCandidate(axis, far, oNear, false);
			AddCandidate(axis, near, oFar, false);
			AddCandidate(axis, center, oCenter, false);
		}
		return axis;
	}

	private static Axis BuildYAxis(Rectangle moving, List<SnapTarget> snapshot, IntPtr exclude, Rectangle workArea)
	{
		Axis axis = new Axis
		{
			Candidates = new List<int>(),
			ScreenEdges = new HashSet<int>()
		};
		int near = moving.Top;
		int far = moving.Bottom;
		int center = moving.Y + moving.Height / 2;

		AddCandidate(axis, near, workArea.Top, true);
		AddCandidate(axis, far, workArea.Bottom, true);

		foreach (SnapTarget target in EnumerateOthers(snapshot, exclude))
		{
			Rectangle other = SafeBounds(target);
			if (other.Width <= 0 || other.Height <= 0)
				continue;
			int oNear = other.Top;
			int oFar = other.Bottom;
			int oCenter = other.Y + other.Height / 2;
			AddCandidate(axis, near, oNear, false);
			AddCandidate(axis, far, oFar, false);
			AddCandidate(axis, far, oNear, false);
			AddCandidate(axis, near, oFar, false);
			AddCandidate(axis, center, oCenter, false);
		}
		return axis;
	}

	private static IEnumerable<SnapTarget> EnumerateOthers(List<SnapTarget> snapshot, IntPtr exclude)
	{
		foreach (SnapTarget target in snapshot)
		{
			if (target.Handle != exclude)
				yield return target;
		}
	}

	private static Rectangle SafeBounds(SnapTarget target)
	{
		try
		{
			return target.BoundsProvider();
		}
		catch
		{
			return Rectangle.Empty;
		}
	}

	private static void AddCandidate(XAxis axis, int movingEdge, int targetEdge, bool isScreenEdge)
	{
		int delta = targetEdge - movingEdge;
		if (Math.Abs(delta) > SnapThreshold)
			return;
		axis.Candidates.Add(delta);
		if (isScreenEdge)
			axis.ScreenEdges.Add(targetEdge);
	}

	private static void AddCandidate(Axis axis, int movingEdge, int targetEdge, bool isScreenEdge)
	{
		int delta = targetEdge - movingEdge;
		if (Math.Abs(delta) > SnapThreshold)
			return;
		axis.Candidates.Add(delta);
		if (isScreenEdge)
			axis.ScreenEdges.Add(targetEdge);
	}

	/// <summary>
	/// 收集单个轴向的候选位移，并记录每条对齐线落在哪个坐标。
	/// </summary>
	private static void AddAxisCandidates(int near, int far, int center,
		List<SnapTarget> snapshot, IntPtr exclude, Rectangle workArea,
		List<int> candidates, Dictionary<int, bool> guides, bool isVertical)
	{
		// 屏幕工作区边缘：靠齐最左/最上时吸附
		int workNear = isVertical ? workArea.Left : workArea.Top;
		int workFar = isVertical ? workArea.Right : workArea.Bottom;
		AddCandidate(candidates, guides, near, workNear, true);
		AddCandidate(candidates, guides, far, workFar, true);

		foreach (SnapTarget target in snapshot)
		{
			if (target.Handle == exclude)
				continue;
			Rectangle other;
			try
			{
				other = target.BoundsProvider();
			}
			catch
			{
				continue;
			}
			if (other.Width <= 0 || other.Height <= 0)
				continue;

			int oNear = isVertical ? other.Left : other.Top;
			int oFar = isVertical ? other.Right : other.Bottom;
			int oCenter = isVertical
				? other.X + other.Width / 2
				: other.Y + other.Height / 2;

			AddCandidate(candidates, guides, near, oNear, false);
			AddCandidate(candidates, guides, far, oFar, false);
			AddCandidate(candidates, guides, far, oNear, false);
			AddCandidate(candidates, guides, near, oFar, false);
			AddCandidate(candidates, guides, center, oCenter, false);
		}
	}

	private static void AddCandidate(List<int> candidates, Dictionary<int, bool> guides,
		int movingEdge, int targetEdge, bool isScreenEdge)
	{
		int delta = targetEdge - movingEdge;
		if (Math.Abs(delta) > SnapThreshold)
			return;
		candidates.Add(delta);
		// 该候选吸附后，moving 的 near 边会落在 targetEdge 上
		guides[targetEdge] = isScreenEdge;
	}

	/// <summary>选择绝对值最小的候选位移；距离相同时优先取 0（不动）。</summary>
	private static int PickBest(List<int> candidates)
	{
		int best = 0;
		int bestAbs = int.MaxValue;
		foreach (int c in candidates)
		{
			int abs = Math.Abs(c);
			if (abs < bestAbs)
			{
				bestAbs = abs;
				best = c;
			}
		}
		// 已经在同一位置（delta=0）时不产生偏移
		return bestAbs == 0 ? 0 : best;
	}

	private static List<SnapTarget> Snapshot()
	{
		lock (sync)
		{
			return new List<SnapTarget>(targets);
		}
	}

	private sealed class SnapTarget
	{
		public IntPtr Handle { get; }

		public Func<Rectangle> BoundsProvider { get; }

		public SnapTarget(IntPtr handle, Func<Rectangle> boundsProvider)
		{
			Handle = handle;
			BoundsProvider = boundsProvider;
		}
	}
}