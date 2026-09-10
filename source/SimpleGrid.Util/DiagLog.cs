using System;
using System.IO;
using System.Text;
using System.Threading;

namespace SimpleGrid.Util;

internal static class DiagLog
{
	private static readonly object gate = new object();

	private static string cachedPath;

	private static string LogPath
	{
		get
		{
			if (cachedPath == null)
			{
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimpleGrid");
				try
				{
					Directory.CreateDirectory(text);
				}
				catch
				{
				}
				cachedPath = Path.Combine(text, "app_diag.log");
			}
			return cachedPath;
		}
	}

	public static void Log(string msg)
	{
		try
		{
			lock (gate)
			{
				string contents = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [tid" + Thread.CurrentThread.ManagedThreadId + "] " + (msg ?? string.Empty) + Environment.NewLine;
				File.AppendAllText(LogPath, contents, Encoding.UTF8);
			}
		}
		catch
		{
		}
	}
}
