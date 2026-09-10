using System;
using System.Runtime.InteropServices;

namespace SimpleGrid.Win32;

public class BlurUtil
{
	[DllImport("user32.dll")]
	public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

	public static void EnableBlur(IntPtr hwnd)
	{
		AccentPolicy structure = new AccentPolicy
		{
			AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND
		};
		int num = Marshal.SizeOf(structure);
		IntPtr intPtr = Marshal.AllocHGlobal(num);
		Marshal.StructureToPtr(structure, intPtr, fDeleteOld: false);
		WindowCompositionAttributeData data = new WindowCompositionAttributeData
		{
			Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
			SizeOfData = num,
			Data = intPtr
		};
		SetWindowCompositionAttribute(hwnd, ref data);
		Marshal.FreeHGlobal(intPtr);
	}
}
