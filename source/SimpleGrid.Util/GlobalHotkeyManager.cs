using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SimpleGrid.Util;

/// <summary>
/// 全局热键注册与派发。用一个隐藏的 NativeWindow 接收 WM_HOTKEY，
/// 因此即使主程序没有焦点也能响应（区别于 Form.ProcessCmdKey 的窗体内快捷键）。
/// </summary>
internal static class GlobalHotkeyManager
{
	private const int WM_HOTKEY = 0x0312;

	private const uint MOD_ALT = 0x0001;
	private const uint MOD_CONTROL = 0x0002;
	private const uint MOD_SHIFT = 0x0004;
	private const uint MOD_WIN = 0x0008;

	/// <summary>MOD_NOREPEAT：长按不重复触发。</summary>
	private const uint MOD_NOREPEAT = 0x4000;

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

	/// <summary>无窗体消息接收器。必须保持存活，否则热键随之失效。</summary>
	private sealed class HotkeyWindow : NativeWindow
	{
		public HotkeyWindow()
		{
			CreateHandle(new CreateParams());
		}

		protected override void WndProc(ref Message m)
		{
			if (m.Msg == WM_HOTKEY)
			{
				GlobalHotkeyManager.OnHotkeyPressed(m.WParam.ToInt32());
				return;
			}
			base.WndProc(ref m);
		}
	}

	private static readonly Dictionary<int, Action> handlers = new Dictionary<int, Action>();

	private static HotkeyWindow window;

	private static int nextId = 0xA000;

	private static bool initialized;

	/// <summary>
	/// 按快捷键字符串注册全局热键，例如 "Ctrl+Alt+S"。
	/// BestNoFences 这里是硬编码 Keys.H/Keys.S、不读配置，这里改为真正解析字符串。
	/// </summary>
	/// <returns>注册成功返回热键 id，失败返回 -1（已被其他程序占用是常见原因）。</returns>
	public static int Register(string shortcut, Action action)
	{
		if (string.IsNullOrWhiteSpace(shortcut) || action == null)
			return -1;
		if (!TryParseShortcut(shortcut, out uint modifiers, out Keys key))
			return -1;

		try
		{
			EnsureInitialized();
			int id = nextId++;
			if (!RegisterHotKey(window.Handle, id, modifiers | MOD_NOREPEAT, (uint)key))
			{
				DiagLog.Log("注册全局热键失败(" + shortcut + ")，可能已被其他程序占用");
				return -1;
			}
			handlers[id] = action;
			DiagLog.Log("已注册全局热键: " + shortcut);
			return id;
		}
		catch (Exception ex)
		{
			DiagLog.Log("注册全局热键异常(" + shortcut + "): " + ex.Message);
			return -1;
		}
	}

	public static void Unregister(int id)
	{
		if (id < 0)
			return;
		try
		{
			handlers.Remove(id);
			if (window?.Handle != IntPtr.Zero)
				UnregisterHotKey(window.Handle, id);
		}
		catch
		{
		}
	}

	/// <summary>注销全部热键并释放消息窗口。退出前调用。</summary>
	public static void Dispose()
	{
		try
		{
			foreach (int id in new List<int>(handlers.Keys))
			{
				if (window?.Handle != IntPtr.Zero)
					UnregisterHotKey(window.Handle, id);
			}
			handlers.Clear();
			window?.DestroyHandle();
			window = null;
			initialized = false;
		}
		catch
		{
		}
	}

	private static void EnsureInitialized()
	{
		if (!initialized || window == null)
		{
			window = new HotkeyWindow();
			initialized = true;
		}
	}

	private static void OnHotkeyPressed(int id)
	{
		if (!handlers.TryGetValue(id, out Action action))
			return;
		try
		{
			action();
		}
		catch (Exception ex)
		{
			// 热键回调里的异常不能冒泡，否则会拖垮消息循环
			DiagLog.Log("全局热键处理异常: " + ex.Message);
		}
	}

	/// <summary>
	/// 解析 "Ctrl+Alt+S" 形式为修饰键位掩码与主键。
	/// BestNoFences 声明了 8 个可配置热键却从未读取，这里让配置真正生效。
	/// </summary>
	public static bool TryParseShortcut(string shortcut, out uint modifiers, out Keys key)
	{
		modifiers = 0;
		key = Keys.None;

		if (string.IsNullOrWhiteSpace(shortcut))
			return false;

		string[] parts = shortcut.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0)
			return false;

		Keys mainKey = Keys.None;
		foreach (string raw in parts)
		{
			string part = raw.Trim();
			if (part.Length == 0)
				continue;

			switch (part.ToUpperInvariant())
			{
				case "CTRL":
				case "CONTROL":
					modifiers |= MOD_CONTROL;
					continue;
				case "ALT":
					modifiers |= MOD_ALT;
					continue;
				case "SHIFT":
					modifiers |= MOD_SHIFT;
					continue;
				case "WIN":
				case "WINDOWS":
					modifiers |= MOD_WIN;
					continue;
			}

			// 修饰键之外的那一个就是主键；单字符走 Keys 枚举转换，F5 等走 Enum.TryParse
			if (part.Length == 1)
				mainKey = (Keys)char.ToUpperInvariant(part[0]);
			else if (!Enum.TryParse(part, ignoreCase: true, out mainKey))
				return false;
		}

		if (mainKey == Keys.None || mainKey == Keys.ControlKey || mainKey == Keys.Menu
			|| mainKey == Keys.ShiftKey || mainKey == Keys.LWin || mainKey == Keys.RWin)
			return false;

		key = mainKey;
		return true;
	}
}