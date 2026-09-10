using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Peter;

public class LocalWindowsHook
{
	public delegate int HookProc(int code, IntPtr wParam, IntPtr lParam);

	public delegate void HookEventHandler(object sender, HookEventArgs e);

	protected IntPtr m_hhook = IntPtr.Zero;

	protected HookProc m_filterFunc;

	protected HookType m_hookType;

	public event HookEventHandler HookInvoked;

	protected void OnHookInvoked(HookEventArgs e)
	{
		if (HookInvoked != null)
		{
			HookInvoked(this, e);
		}
	}

	public LocalWindowsHook(HookType hook)
	{
		m_hookType = hook;
		m_filterFunc = CoreHookProc;
	}

	public LocalWindowsHook(HookType hook, HookProc func)
	{
		m_hookType = hook;
		m_filterFunc = func;
	}

	protected int CoreHookProc(int code, IntPtr wParam, IntPtr lParam)
	{
		if (code < 0)
		{
			return CallNextHookEx(m_hhook, code, wParam, lParam);
		}
		HookEventArgs e = new HookEventArgs();
		e.HookCode = code;
		e.wParam = wParam;
		e.lParam = lParam;
		OnHookInvoked(e);
		return CallNextHookEx(m_hhook, code, wParam, lParam);
	}

	public void Install()
	{
		m_hhook = SetWindowsHookEx(m_hookType, m_filterFunc, IntPtr.Zero, Thread.CurrentThread.ManagedThreadId);
	}

	public void Uninstall()
	{
		UnhookWindowsHookEx(m_hhook);
	}

	[DllImport("user32.dll")]
	protected static extern IntPtr SetWindowsHookEx(HookType code, HookProc func, IntPtr hInstance, int threadID);

	[DllImport("user32.dll")]
	protected static extern int UnhookWindowsHookEx(IntPtr hhook);

	[DllImport("user32.dll")]
	protected static extern int CallNextHookEx(IntPtr hhook, int code, IntPtr wParam, IntPtr lParam);
}
