using System;
using System.Collections.Generic;
using System.Drawing;

namespace SimpleGrid.Util;

/// <summary>
/// 带容量上限的 LRU 缓存。淘汰时会对值调用 Dispose()。
///
/// 图标/缩略图都是 GDI 对象，只增不减会持续泄漏句柄（README 已知限制之一）。
/// 淘汰时释放是这里最关键的一点：普通 Dictionary 即使加了计数也不会释放资源。
/// </summary>
internal sealed class LruCache<TKey, TValue> : IDisposable where TValue : class, IDisposable
{
	private readonly int capacity;

	private readonly Dictionary<TKey, LinkedListNode<Entry>> map;

	private readonly LinkedList<Entry> order = new LinkedList<Entry>();

	private readonly object sync = new object();

	private bool disposed;

	public LruCache(int capacity)
	{
		if (capacity < 1)
			capacity = 1;
		this.capacity = capacity;
		map = new Dictionary<TKey, LinkedListNode<Entry>>(capacity);
	}

	public int Count
	{
		get
		{
			lock (sync)
			{
				return map.Count;
			}
		}
	}

	/// <summary>取缓存项，命中时刷新为最近使用。未命中返回 null。</summary>
	public TValue TryGet(TKey key)
	{
		lock (sync)
		{
			if (disposed || !map.TryGetValue(key, out LinkedListNode<Entry> node))
				return null;
			order.Remove(node);
			order.AddFirst(node);
			return node.Value.Item;
		}
	}

	/// <summary>
	/// 取缓存项，未命中则用 factory 生成并写入。写入后若超出容量，淘汰最久未使用的项
	/// 并 Dispose 其值。
	/// </summary>
	public TValue GetOrAdd(TKey key, Func<TValue> factory)
	{
		lock (sync)
		{
			if (disposed)
				return factory();
			if (map.TryGetValue(key, out LinkedListNode<Entry> existing))
			{
				order.Remove(existing);
				order.AddFirst(existing);
				return existing.Value.Item;
			}

			TValue created = factory();
			LinkedListNode<Entry> node = order.AddFirst(new Entry(key, created));
			map[key] = node;

			while (map.Count > capacity)
			{
				LinkedListNode<Entry> last = order.Last;
				if (last == null)
					break;
				order.RemoveLast();
				map.Remove(last.Value.Key);
				SafeDispose(last.Value.Item);
			}
			return created;
		}
	}

	/// <summary>替换指定键的值，并释放旧值。用于后台加载完成后回填真实图标。</summary>
	public void Replace(TKey key, TValue newValue)
	{
		TValue old = null;
		lock (sync)
		{
			if (disposed)
			{
				old = newValue;
				newValue = null;
			}
			else if (map.TryGetValue(key, out LinkedListNode<Entry> node))
			{
				old = node.Value.Item;
				node.Value.Item = newValue;
				order.Remove(node);
				order.AddFirst(node);
			}
			else
			{
				LinkedListNode<Entry> added = order.AddFirst(new Entry(key, newValue));
				map[key] = added;
				while (map.Count > capacity)
				{
					LinkedListNode<Entry> last = order.Last;
					if (last == null)
						break;
					order.RemoveLast();
					map.Remove(last.Value.Key);
					SafeDispose(last.Value.Item);
				}
			}
		}
		SafeDispose(old);
	}

	public bool Remove(TKey key)
	{
		TValue removed = null;
		lock (sync)
		{
			if (map.TryGetValue(key, out LinkedListNode<Entry> node))
			{
				removed = node.Value.Item;
				order.Remove(node);
				map.Remove(key);
			}
		}
		SafeDispose(removed);
		return removed != null;
	}

	public void Clear()
	{
		List<TValue> values;
		lock (sync)
		{
			values = new List<TValue>(map.Count);
			foreach (Entry entry in order)
				values.Add(entry.Item);
			order.Clear();
			map.Clear();
		}
		foreach (TValue value in values)
			SafeDispose(value);
	}

	public void Dispose()
	{
		Clear();
		lock (sync)
		{
			disposed = true;
		}
	}

	private static void SafeDispose(TValue value)
	{
		try
		{
			value?.Dispose();
		}
		catch
		{
			// 释放失败不应影响调用方
		}
	}

	// 注意：成员刻意命名为 Item 而不是 Value。LinkedListNode<Entry>.Value 本身就是
	// Entry 类型，若内部再叫 Value 会与之遮蔽，访问链极具迷惑性。
	private sealed class Entry
	{
		public TKey Key { get; }

		public TValue Item { get; set; }

		public Entry(TKey key, TValue item)
		{
			Key = key;
			Item = item;
		}
	}
}