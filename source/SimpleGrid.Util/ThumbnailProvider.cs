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
	private sealed class ThumbnailState
	{
		public Icon Icon;
	}

	private static readonly string[] SupportedExtensions = new string[7] { ".bmp", ".gif", ".jpg", ".jpeg", ".png", ".tiff", ".tif" };
	private static readonly SemaphoreSlim LoadGate = new SemaphoreSlim(2);
	private readonly object cacheLock = new object();
	private readonly IDictionary<string, ThumbnailState> iconCache = new Dictionary<string, ThumbnailState>(StringComparer.OrdinalIgnoreCase);
	private bool disposed;

	public event EventHandler IconThumbnailLoaded;

	public bool IsSupported(string path)
	{
		return SupportedExtensions.Any((string ext) => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
	}

	public Icon GenerateThumbnail(string path)
	{
		lock (cacheLock)
		{
			if (disposed)
				return (Icon)SystemIcons.Application.Clone();
			if (!iconCache.TryGetValue(path, out ThumbnailState state))
			{
				state = new ThumbnailState { Icon = (Icon)SystemIcons.Application.Clone() };
				iconCache[path] = state;
				SubmitGeneratorTask(path, state);
			}
			return (Icon)state.Icon.Clone();
		}
	}

	private void SubmitGeneratorTask(string path, ThumbnailState state)
	{
		Task.Run(async delegate
		{
			await LoadGate.WaitAsync().ConfigureAwait(false);
			Icon loadedIcon = null;
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

				EventHandler handler = null;
				lock (cacheLock)
				{
					if (!disposed && iconCache.TryGetValue(path, out ThumbnailState current) && ReferenceEquals(current, state))
					{
						Icon oldIcon = state.Icon;
						state.Icon = loadedIcon;
						loadedIcon = null;
						oldIcon?.Dispose();
						handler = IconThumbnailLoaded;
					}
				}
				handler?.Invoke(this, EventArgs.Empty);
			}
			catch
			{
			}
			finally
			{
				loadedIcon?.Dispose();
				LoadGate.Release();
			}
		});
	}

	public void Dispose()
	{
		lock (cacheLock)
		{
			if (disposed)
				return;
			disposed = true;
			foreach (ThumbnailState state in iconCache.Values)
				state.Icon?.Dispose();
			iconCache.Clear();
			IconThumbnailLoaded = null;
		}
	}

	[DllImport("user32.dll")]
	private static extern bool DestroyIcon(IntPtr handle);
}
