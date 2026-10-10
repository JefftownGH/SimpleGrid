using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace SimpleGrid.Util;

/// <summary>
/// 开机自启管理。写入 HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run。
/// 用 HKCU 而非 HKLM，无需管理员权限，普通用户即可开关。
/// </summary>
internal static class StartupManager
{
	private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
	private const string ValueName = "SimpleGrid";

	/// <summary>
	/// 是否已设置开机自启。
	/// 额外比对注册表里记录的程序路径与当前 exe 路径：
	/// 用户换了安装目录后，残留的注册表项会让托盘勾选状态与实际情况不符。
	/// </summary>
	public static bool IsStartupEnabled()
	{
		try
		{
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false))
			{
				if (key?.GetValue(ValueName) is string recorded)
				{
					string current = GetApplicationPath();
					if (!string.IsNullOrEmpty(current)
						&& !string.IsNullOrEmpty(recorded)
						&& !PathsEqual(recorded, current))
					{
						// 路径对不上：视为未启用，等待用户重新勾选以刷新注册表
						return false;
					}
					return true;
				}
			}
		}
		catch (Exception ex)
		{
			DiagLog.Log("读取开机自启设置失败: " + ex.Message);
		}
		return false;
	}

	public static void EnableStartup()
	{
		try
		{
			string path = GetApplicationPath();
			if (string.IsNullOrEmpty(path))
				return;
			using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
			{
				// 前后加引号，路径含空格时也能正确解析
				key.SetValue(ValueName, "\"" + path + "\"", RegistryValueKind.String);
			}
			DiagLog.Log("已启用开机自启: " + path);
		}
		catch (Exception ex)
		{
			DiagLog.Log("启用开机自启失败: " + ex.Message);
		}
	}

	public static void DisableStartup()
	{
		try
		{
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
			{
				key?.DeleteValue(ValueName, throwOnMissingValue: false);
			}
			DiagLog.Log("已关闭开机自启");
		}
		catch (Exception ex)
		{
			DiagLog.Log("关闭开机自启失败: " + ex.Message);
		}
	}

	/// <summary>按目标状态切换开关。</summary>
	public static void SetStartup(bool enabled)
	{
		if (enabled)
			EnableStartup();
		else
			DisableStartup();
	}

	/// <summary>
	/// 取得当前程序的可执行文件路径。按可靠性降级：
	/// Environment.ProcessPath → 进程主模块 → 程序集目录推断 → 程序集位置。
	/// </summary>
	private static string GetApplicationPath()
	{
		try
		{
			string processPath = Environment.ProcessPath;
			if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
				return processPath;
		}
		catch
		{
		}

		try
		{
			string modulePath = Process.GetCurrentProcess().MainModule?.FileName;
			// framework-dependent 部署下主模块可能是 dotnet.exe，此时不可用
			if (!string.IsNullOrEmpty(modulePath)
				&& !string.Equals(Path.GetFileNameWithoutExtension(modulePath), "dotnet", StringComparison.OrdinalIgnoreCase)
				&& File.Exists(modulePath))
			{
				return modulePath;
			}
		}
		catch
		{
		}

		try
		{
			string assembly = Assembly.GetExecutingAssembly().Location;
			if (!string.IsNullOrEmpty(assembly))
			{
				string dir = Path.GetDirectoryName(assembly);
				string exeName = Path.GetFileNameWithoutExtension(assembly) + ".exe";
				string candidate = Path.Combine(dir ?? string.Empty, exeName);
				if (File.Exists(candidate))
					return candidate;
			}
		}
		catch
		{
		}

		return null;
	}

	private static bool PathsEqual(string a, string b)
	{
		string na = a.Trim().Trim('"');
		string nb = b.Trim().Trim('"');
		return string.Equals(
			Path.GetFullPath(na).TrimEnd('\\'),
			Path.GetFullPath(nb).TrimEnd('\\'),
			StringComparison.OrdinalIgnoreCase);
	}
}