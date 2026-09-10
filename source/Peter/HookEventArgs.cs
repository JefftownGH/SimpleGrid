using System;

namespace Peter;

public class HookEventArgs : EventArgs
{
	public int HookCode;

	public IntPtr wParam;

	public IntPtr lParam;
}
