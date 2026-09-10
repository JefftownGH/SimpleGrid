using System;

namespace Peter;

internal static class ShellHelper
{
	public static uint HiWord(IntPtr ptr)
	{
		if (((int)ptr & int.MinValue) == int.MinValue)
		{
			return (uint)(int)ptr >> 16;
		}
		return (uint)(((int)ptr >>> 16) & 0xFFFF);
	}

	public static uint LoWord(IntPtr ptr)
	{
		return (uint)((int)ptr & 0xFFFF);
	}
}
