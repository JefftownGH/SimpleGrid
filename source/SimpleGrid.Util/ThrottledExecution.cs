using System;
using System.Windows.Forms;

namespace SimpleGrid.Util;

public sealed class ThrottledExecution : IDisposable
{
	private readonly TimeSpan delay;
	private readonly Timer timer;
	private DateTime lastExecution = DateTime.MinValue;
	private Action pendingAction;
	private bool disposed;

	public ThrottledExecution(TimeSpan delay)
	{
		this.delay = delay;
		timer = new Timer();
		timer.Tick += Timer_Tick;
	}

	public void Run(Action action)
	{
		if (disposed || action == null)
			return;

		TimeSpan remaining = delay - (DateTime.UtcNow - lastExecution);
		if (remaining <= TimeSpan.Zero)
		{
			pendingAction = null;
			timer.Stop();
			Execute(action);
			return;
		}

		pendingAction = action;
		timer.Stop();
		timer.Interval = Math.Max(1, Math.Min(int.MaxValue, (int)Math.Ceiling(remaining.TotalMilliseconds)));
		timer.Start();
	}

	private void Timer_Tick(object sender, EventArgs e)
	{
		timer.Stop();
		Action action = pendingAction;
		pendingAction = null;
		if (action != null)
			Execute(action);
	}

	private void Execute(Action action)
	{
		lastExecution = DateTime.UtcNow;
		action();
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		pendingAction = null;
		timer.Stop();
		timer.Tick -= Timer_Tick;
		timer.Dispose();
	}
}
