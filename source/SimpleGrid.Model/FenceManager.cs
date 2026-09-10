using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using SimpleGrid.Util;

namespace SimpleGrid.Model;

public class FenceManager
{
	private const string MetaFileName = "__fence_metadata.xml";
	private const string IndexFileName = "fences.index";

	private readonly string basePath;

	private readonly string itemsPath;

	private readonly object importLock = new object();

	public static FenceManager Instance { get; } = new FenceManager();

	public FenceManager()
	{
		basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimpleGrid");
		EnsureDirectoryExists(basePath);
		itemsPath = Path.Combine(basePath, "Items");
		MigrateLegacyDataIfNeeded();
		EnsureDirectoryExists(itemsPath);
	}

	private void MigrateLegacyDataIfNeeded()
	{
		foreach (string directory in Directory.GetDirectories(basePath))
		{
			if (File.Exists(Path.Combine(directory, MetaFileName)))
				return;
		}

		string legacyBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoFences");
		if (!Directory.Exists(legacyBasePath))
			return;

		string legacyItemsPath = Path.Combine(legacyBasePath, "Items");
		if (!Directory.Exists(itemsPath) && Directory.Exists(legacyItemsPath))
			CopyDirectory(legacyItemsPath, itemsPath);

		foreach (string legacyDirectory in Directory.GetDirectories(legacyBasePath))
		{
			string legacyMetadataPath = Path.Combine(legacyDirectory, MetaFileName);
			if (!File.Exists(legacyMetadataPath))
				continue;

			string destinationDirectory = Path.Combine(basePath, Path.GetFileName(legacyDirectory));
			if (Directory.Exists(destinationDirectory))
				continue;
			CopyDirectory(legacyDirectory, destinationDirectory);
			string destinationMetadataPath = Path.Combine(destinationDirectory, MetaFileName);
			string metadata = File.ReadAllText(destinationMetadataPath).Replace(legacyItemsPath, itemsPath);
			File.WriteAllText(destinationMetadataPath, metadata);
		}

		string[] migratedDirectories = Directory.GetDirectories(basePath)
			.Where(path => File.Exists(Path.Combine(path, MetaFileName)))
			.ToArray();
		if (migratedDirectories.Length > 0)
			WriteFenceIndex(migratedDirectories);
	}

	public int LoadFences()
	{
		int loadedCount = 0;
		string[] fenceDirectories = GetFenceDirectories();
		foreach (string item in fenceDirectories)
		{
			try
			{
				string path = Path.Combine(item, MetaFileName);
				if (!File.Exists(path))
					continue;
				XmlSerializer xmlSerializer = new XmlSerializer(typeof(FenceInfo));
				FenceInfo fenceInfo;
				using (StreamReader streamReader = new StreamReader(path))
				{
					fenceInfo = xmlSerializer.Deserialize(streamReader) as FenceInfo;
				}
				if (fenceInfo != null)
				{
					new FenceWindow(fenceInfo).Show();
					loadedCount++;
				}
			}
			catch (Exception ex)
			{
				DiagLog.Log("无法载入围栏数据 '" + item + "': " + ex.Message);
			}
		}
		return loadedCount;
	}

	public void CreateFence(string name)
	{
		FenceInfo fenceInfo = new FenceInfo(Guid.NewGuid())
		{
			Name = name,
			PosX = 100,
			PosY = 250,
			Height = 300,
			Width = 300
		};
		UpdateFence(fenceInfo);
		new FenceWindow(fenceInfo).Show();
	}

	public void RemoveFence(FenceInfo info)
	{
		Directory.Delete(GetFolderPath(info), recursive: true);
		WriteFenceIndex(GetFenceDirectories().Where(path => !path.Equals(GetFolderPath(info), StringComparison.OrdinalIgnoreCase)));
	}

	public void UpdateFence(FenceInfo fenceInfo)
	{
		string folderPath = GetFolderPath(fenceInfo);
		EnsureDirectoryExists(folderPath);
		string path = Path.Combine(folderPath, MetaFileName);
		string tempPath = path + ".tmp";
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(FenceInfo));
		using (StreamWriter streamWriter = new StreamWriter(tempPath))
		{
			xmlSerializer.Serialize(streamWriter, fenceInfo);
			streamWriter.Flush();
		}
		if (File.Exists(path))
			File.Replace(tempPath, path, null);
		else
			File.Move(tempPath, path);
		EnsureFenceIndexed(folderPath);
	}

	private string[] GetFenceDirectories()
	{
		string indexPath = Path.Combine(basePath, IndexFileName);
		if (File.Exists(indexPath))
		{
			return File.ReadAllLines(indexPath)
				.Select(line => line.Trim())
				.Where(line => Guid.TryParse(line, out _))
				.Select(line => Path.Combine(basePath, line))
				.Where(Directory.Exists)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}
		return Directory.GetDirectories(basePath)
			.Where(path => File.Exists(Path.Combine(path, MetaFileName)))
			.ToArray();
	}

	private void EnsureFenceIndexed(string folderPath)
	{
		string[] directories = GetFenceDirectories();
		if (!directories.Contains(folderPath, StringComparer.OrdinalIgnoreCase))
			WriteFenceIndex(directories.Concat(new[] { folderPath }));
	}

	private void WriteFenceIndex(System.Collections.Generic.IEnumerable<string> directories)
	{
		string indexPath = Path.Combine(basePath, IndexFileName);
		string tempPath = indexPath + ".tmp";
		File.WriteAllLines(tempPath, directories.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase));
		if (File.Exists(indexPath))
			File.Replace(tempPath, indexPath, null);
		else
			File.Move(tempPath, indexPath);
	}

	public string ImportItem(string sourcePath)
	{
		if (string.IsNullOrWhiteSpace(sourcePath))
			return null;

		string fullSourcePath;
		try
		{
			fullSourcePath = Path.GetFullPath(sourcePath);
		}
		catch
		{
			return null;
		}

		if (IsManagedItem(fullSourcePath))
			return File.Exists(fullSourcePath) || Directory.Exists(fullSourcePath) ? fullSourcePath : null;
		if (!File.Exists(fullSourcePath) && !Directory.Exists(fullSourcePath))
			return null;

		lock (importLock)
		{
			string containerPath = Path.Combine(itemsPath, Guid.NewGuid().ToString("N"));
			string name = Path.GetFileName(fullSourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			string destinationPath = Path.Combine(containerPath, name);
			try
			{
				Directory.CreateDirectory(containerPath);
				if (File.Exists(fullSourcePath))
					File.Copy(fullSourcePath, destinationPath, overwrite: false);
				else
					CopyDirectory(fullSourcePath, destinationPath);
				return destinationPath;
			}
			catch (Exception ex)
			{
				DiagLog.Log("无法将项目归档到 SimpleGrid '" + fullSourcePath + "': " + ex.Message);
				try
				{
					if (Directory.Exists(containerPath))
						Directory.Delete(containerPath, recursive: true);
				}
				catch
				{
				}
				return null;
			}
		}
	}

	private bool IsManagedItem(string path)
	{
		string root = Path.GetFullPath(itemsPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
	}

	private static void CopyDirectory(string sourcePath, string destinationPath)
	{
		Directory.CreateDirectory(destinationPath);
		foreach (string filePath in Directory.EnumerateFiles(sourcePath))
			File.Copy(filePath, Path.Combine(destinationPath, Path.GetFileName(filePath)), overwrite: false);
		foreach (string directoryPath in Directory.EnumerateDirectories(sourcePath))
			CopyDirectory(directoryPath, Path.Combine(destinationPath, Path.GetFileName(directoryPath)));
	}

	private void EnsureDirectoryExists(string dir)
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(dir);
		if (!directoryInfo.Exists)
		{
			directoryInfo.Create();
		}
	}

	private string GetFolderPath(FenceInfo fenceInfo)
	{
		return Path.Combine(basePath, fenceInfo.Id.ToString());
	}
}
