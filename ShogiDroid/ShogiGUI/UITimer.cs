using System;
using System.Threading;
using System.Timers;

namespace ShogiGUI;

public class UITimer : IDisposable
{
	private System.Timers.Timer timer = new System.Timers.Timer();

	private SynchronizationContext syncContext;

	public EventHandler<EventArgs> Tick;

	public int Interval
	{
		get
		{
			return (int)timer.Interval;
		}
		set
		{
			timer.Interval = value;
		}
	}

	public bool Enabled
	{
		get
		{
			return timer.Enabled;
		}
		set
		{
			timer.Enabled = value;
		}
	}

	public UITimer()
	{
		syncContext = SynchronizationContext.Current;
		timer.Elapsed += Timer_Elapsed;
		timer.AutoReset = false;
	}

	public void Start()
	{
		timer.Start();
	}

	public void Stop()
	{
		timer.Stop();
	}

	public void Dispose()
	{
		if (timer != null)
		{
			timer.Dispose();
		}
	}

	private void Timer_Elapsed(object sender, ElapsedEventArgs e)
	{
		syncContext.Post(delegate
		{
			if (Tick != null)
			{
				Tick(this, new EventArgs());
			}
		}, null);
	}
}
