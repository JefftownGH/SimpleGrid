using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleGrid.Util;

public sealed class ThumbnailProvider : IDisposable
{
	private static readonly string[] SupportedExtensions = new string[7] { ".bmp", ".gif", ".jpg", ".jpeg", ".png", ".tiff", ".tif" };
	private static readonly SemaphoreSlim LoadGate = new SemaphoreSlim(2);

	/// <summary>缩略图缓存上限。原先无上限，Icon 长期累积不释放。</summary>
	private const int MaxCacheEntries = 256;

	private readonly object cacheLock = new object();

	/// <summary>
	/// 缩略图缓存。用 LruCache 限制容量，淘汰时自动 Dispose 图标，
	/// 避免长期使用后 GDI 句柄耗尽。
	/// </summary>
	private readonly LruCache<string, Icon> iconCache = new LruCache<string, Icon>(MaxCacheEntries);

	private bool disposed;

	public event EventHandler IconThumbnailLoaded;

	public bool IsSupported(string path)
	{
		return SupportedExtensions.Any((string ext) => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
	}

	public Icon GenerateThumbnail(string path)
	{
		Icon cached = iconCache.TryGet(path);
		if (cached != null)
			return (Icon)cached.Clone();

		if (disposed)
			return (Icon)SystemIcons.Application.Clone();

		// 先用系统图标占位，保证 UI 当帧可绘制；真实缩略图后台生成后回填
		Icon placeholder = (Icon)SystemIcons.Application.Clone();
		iconCache.Replace(path, placeholder);
		SubmitGeneratorTask(path);
		return (Icon)placeholder.Clone();
	}

	private void SubmitGeneratorTask(string path)
	{
		Task.Run(async delegate
		{
			await LoadGate.WaitAsync().ConfigureAwait(false);
			Icon loadedIcon = null;
			bool applied = false;
			try
			{
				using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
				using Image image = Image.FromStream(stream);
				using Bitmap thumbnail = (Bitmap)image.GetThumbnailImage(256, 256, () => false, IntPtr.Zero);
				IntPtr handle = thumbnail.GetHicon();
				try
				{
					loadedIcon = (Icon)Icon.FromHandle(handle).Clone();
				}
				finally
				{
					DestroyIcon(handle);
				}

				lock (cacheLock)
				{
					// 已 Dispose 时不写入，直接丢弃；Replace 会释放旧占位图标
					if (!disposed)
					{
						iconCache.Replace(path, loadedIcon);
						loadedIcon = null;
						applied = true;
					}
				}
			}
			catch
			{
			}
			finally
			{
				loadedIcon?.Dispose();
				LoadGate.Release();
			}

			if (applied)
				IconThumbnailLoaded?.Invoke(this, EventArgs.Empty);
		});
	}

	public void Dispose()
	{
		lock (cacheLock)
		{
			if (disposed)
				return;
			disposed = true;
			// LruCache.Clear 会 Dispose 每个缓存图标
			iconCache.Clear();
			IconThumbnailLoaded = null;
		}
	}

	[DllImport("user32.dll")]
	private static extern bool DestroyIcon(IntPtr handle);
}
