using System;
using System.Threading;
using System.Windows.Forms;
using SimpleGrid.Model;
using SimpleGrid.Util;
using SimpleGrid.Win32;

namespace SimpleGrid;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		DiagLog.Log("=== Main 进程启动 ===");
		WindowUtil.SetPreferredAppMode(1);
		Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
		Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
		{
			DiagLog.Log("ThreadException(已吞掉保活): " + e.Exception);
		};
		AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
		{
			DiagLog.Log("UnhandledException IsTerminating=" + e.IsTerminating + " : " + e.ExceptionObject);
		};
		Application.ApplicationExit += delegate
		{
			DiagLog.Log("Application.ApplicationExit 触发（消息循环即将结束）");
		};
		AppDomain.CurrentDomain.ProcessExit += delegate
		{
			DiagLog.Log("AppDomain.ProcessExit 触发（进程真正结束）");
		};
		bool createdNew;
		using (new Mutex(initiallyOwned: true, "SimpleGrid", out createdNew))
		{
			if (createdNew)
			{
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(defaultValue: false);
				int loadedFenceCount = FenceManager.Instance.LoadFences();
				if (loadedFenceCount == 0)
				{
					FenceManager.Instance.CreateFence("First fence");
				}
				Application.Run();
				DiagLog.Log("Application.Run() 返回（正常走完消息循环）");
			}
			else
			{
				DiagLog.Log("已有实例在运行(Mutex)，本次重复启动直接退出");
			}
		}
	}
}
