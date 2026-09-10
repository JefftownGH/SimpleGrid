using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SimpleGrid.Util;
using SimpleGrid.Win32;

namespace SimpleGrid.Model;

public class FenceEntry
{
	public string Path { get; }

	public EntryType Type { get; }

	public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

	private FenceEntry(string path, EntryType type)
	{
		Path = path;
		Type = type;
	}

	public static FenceEntry FromPath(string path)
	{
		if (File.Exists(path))
		{
			return new FenceEntry(path, EntryType.File);
		}
		if (Directory.Exists(path))
		{
			return new FenceEntry(path, EntryType.Folder);
		}
		return null;
	}

	public Icon ExtractIcon(ThumbnailProvider thumbnailProvider)
	{
		if (Type == EntryType.File)
		{
			if (thumbnailProvider.IsSupported(Path))
			{
				return thumbnailProvider.GenerateThumbnail(Path);
			}
			return Icon.ExtractAssociatedIcon(Path);
		}
		return IconUtil.FolderLarge;
	}

	public void Open()
	{
		Task.Run(delegate
		{
			try
			{
				if (Type == EntryType.File)
				{
					OpenFile(Path);
				}
				else if (Type == EntryType.Folder)
				{
					Process.Start("explorer.exe", Path);
				}
			}
			catch (Exception arg)
			{
				Console.WriteLine($"Failed to start: {arg}");
			}
		});
	}

	private static void OpenFile(string path)
	{
		string targetPath;
		string arguments;
		string workingDirectory;
		if (string.Equals(System.IO.Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase)
			&& TryResolveShortcut(path, out targetPath, out arguments, out workingDirectory)
			&& File.Exists(targetPath)
			&& string.Equals(System.IO.Path.GetExtension(targetPath), ".exe", StringComparison.OrdinalIgnoreCase))
		{
			var startInfo = new ProcessStartInfo(targetPath, arguments ?? string.Empty)
			{
				UseShellExecute = false,
				WorkingDirectory = Directory.Exists(workingDirectory)
					? workingDirectory
					: System.IO.Path.GetDirectoryName(targetPath)
			};
			Process.Start(startInfo);
			return;
		}

		Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
	}

	private static bool TryResolveShortcut(string shortcutPath, out string targetPath, out string arguments, out string workingDirectory)
	{
		targetPath = string.Empty;
		arguments = string.Empty;
		workingDirectory = string.Empty;
		object shell = null;
		object shortcut = null;
		try
		{
			System.Type shellType = System.Type.GetTypeFromProgID("WScript.Shell");
			if (shellType == null)
				return false;
			shell = Activator.CreateInstance(shellType);
			shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
			System.Type shortcutType = shortcut.GetType();
			targetPath = Convert.ToString(shortcutType.InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null));
			arguments = Convert.ToString(shortcutType.InvokeMember("Arguments", BindingFlags.GetProperty, null, shortcut, null));
			workingDirectory = Convert.ToString(shortcutType.InvokeMember("WorkingDirectory", BindingFlags.GetProperty, null, shortcut, null));
			return !string.IsNullOrWhiteSpace(targetPath);
		}
		catch
		{
			return false;
		}
		finally
		{
			if (shortcut != null && Marshal.IsComObject(shortcut))
				Marshal.FinalReleaseComObject(shortcut);
			if (shell != null && Marshal.IsComObject(shell))
				Marshal.FinalReleaseComObject(shell);
		}
	}
}
