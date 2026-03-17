using System;
using System.Timers;
using ShogiLib;
using Timer = System.Timers.Timer;

namespace ShogiGUI.Engine;

public class GameTimer : IDisposable
{
	private const int MarginTime = 300;

	public GameTime BlackTime;

	public GameTime WhiteTime;

	private GameRemainTime blackRemainTime;

	private GameRemainTime whiteRemainTime;

	private PlayerColor turn;

	private bool started;

	private long startTime;

	private bool startTick;

	private Timer timer;

	private Timer updateTimer;

	public GameRemainTime BlackRemainTime => blackRemainTime;

	public GameRemainTime WhiteRemainTime => whiteRemainTime;

	public event EventHandler<EventArgs> Timeout;

	public event EventHandler<EventArgs> UpdateTime;

	public GameTimer(EventHandler<EventArgs> timeot)
	{
		BlackTime.Time = 600000;
		BlackTime.RemainTime = 600000;
		WhiteTime.Time = 600000;
		BlackTime.RemainTime = 600000;
		BlackTime.Byoyomi = 30000;
		WhiteTime.Byoyomi = 30000;
		BlackTime.ElapsedTime = 0;
		WhiteTime.ElapsedTime = 0;
		turn = PlayerColor.Black;
		started = false;
		startTime = 0L;
		timer = new Timer();
		timer.Elapsed += timer_Tick;
		Timeout += timeot;
		updateTimer = new Timer();
		updateTimer.Elapsed += UpdateTimer_Elapsed;
	}

	public GameRemainTime GetRemainTime(PlayerColor color)
	{
		if (color != PlayerColor.Black)
		{
			return whiteRemainTime;
		}
		return blackRemainTime;
	}

	public void SetTime(PlayerColor color, int timeMs, int byoyomiMs)
	{
		if (color == PlayerColor.Black)
		{
			BlackTime.Time = timeMs;
			BlackTime.RemainTime = BlackTime.Time;
			BlackTime.Byoyomi = byoyomiMs;
			BlackTime.ElapsedTime = 0;
		}
		else
		{
			WhiteTime.Time = timeMs;
			WhiteTime.RemainTime = WhiteTime.Time;
			WhiteTime.Byoyomi = byoyomiMs;
			WhiteTime.ElapsedTime = 0;
		}
	}

	public void SetRestartTime(int blackTimeSec, int whiteTimeSec)
	{
		int num = blackTimeSec * 1000;
		int num2 = whiteTimeSec * 1000;
		BlackTime.ElapsedTime = num;
		WhiteTime.ElapsedTime = num2;
		if (BlackTime.Time <= num)
		{
			BlackTime.RemainTime = 0;
			if (BlackTime.Byoyomi == 0 && BlackTime.Time != 0)
			{
				BlackTime.RemainTime = 60000;
			}
		}
		else
		{
			BlackTime.RemainTime = BlackTime.Time - num;
		}
		if (WhiteTime.Time <= num2)
		{
			WhiteTime.RemainTime = 0;
			if (WhiteTime.Byoyomi == 0 && WhiteTime.Time != 0)
			{
				WhiteTime.RemainTime = 60000;
			}
		}
		else
		{
			WhiteTime.RemainTime = WhiteTime.Time - num2;
		}
	}

	public void Start(PlayerColor turn)
	{
		_ = started;
		started = true;
		this.turn = turn;
		startTime = DateTime.Now.Ticks;
		startTick = true;
		int num = ((this.turn == PlayerColor.Black) ? (BlackTime.RemainTime + BlackTime.Byoyomi) : (WhiteTime.RemainTime + WhiteTime.Byoyomi));
		if (num != 0)
		{
			timer.Interval = num + 300;
			timer.Start();
		}
		StartUpdateTimer();
	}

	public int Stop()
	{
		started = false;
		startTick = false;
		timer.Stop();
		updateTimer.Stop();
		return CalcTime(DateTime.Now.Ticks);
	}

	public int ChnageTurn()
	{
		if (!started)
		{
			return 0;
		}
		timer.Stop();
		long ticks = DateTime.Now.Ticks;
		int result = CalcTime(ticks);
		startTick = false;
		UpdateRemain();
		return result;
	}

	public void TakeTurn()
	{
		if (started)
		{
			turn = turn.Opp();
			int num = ((turn == PlayerColor.Black) ? (BlackTime.RemainTime + BlackTime.Byoyomi) : (WhiteTime.RemainTime + WhiteTime.Byoyomi));
			startTime = DateTime.Now.Ticks;
			startTick = true;
			if (num != 0)
			{
				timer.Interval = num + 300;
				timer.Start();
			}
			StartUpdateTimer();
		}
	}

	private GameRemainTime RemainTime(PlayerColor color, GameTime gameTime)
	{
		long num = 0L;
		if (turn == color && startTick)
		{
			num = (DateTime.Now.Ticks - startTime) / 10000;
		}
		GameRemainTime result = default(GameRemainTime);
		if (gameTime.Time == 0 && gameTime.Byoyomi == 0)
		{
			result.Time = gameTime.ElapsedTime + (int)num;
			result.Byoyomi = 0;
		}
		else if (gameTime.RemainTime < num)
		{
			num -= gameTime.RemainTime;
			result.Time = 0;
			if (gameTime.Byoyomi < num)
			{
				result.Byoyomi = 0;
			}
			else
			{
				result.Byoyomi = gameTime.Byoyomi - (int)num;
			}
		}
		else
		{
			result.Time = gameTime.RemainTime - (int)num;
			result.Byoyomi = gameTime.Byoyomi;
		}
		result.ElapsedTime = (int)num;
		result.HaveTime = gameTime.Time;
		result.HaveByoyomi = gameTime.Byoyomi;
		result.TotalElapsedTime = gameTime.ElapsedTime + (int)num;
		return result;
	}

	private int CalcTime(long now_time)
	{
		long num = (now_time - startTime) / 10000;
		int elapsedTime;
		if (turn == PlayerColor.Black)
		{
			if (BlackTime.RemainTime < num)
			{
				BlackTime.RemainTime = 0;
			}
			else
			{
				BlackTime.RemainTime -= (int)num;
			}
			elapsedTime = BlackTime.ElapsedTime;
			BlackTime.ElapsedTime += (int)num;
			return BlackTime.ElapsedTime / 1000 - elapsedTime / 1000;
		}
		if (WhiteTime.RemainTime < num)
		{
			WhiteTime.RemainTime = 0;
		}
		else
		{
			WhiteTime.RemainTime -= (int)num;
		}
		elapsedTime = WhiteTime.ElapsedTime;
		WhiteTime.ElapsedTime += (int)num;
		return WhiteTime.ElapsedTime / 1000 - elapsedTime / 1000;
	}

	public void StartUpdateTimer()
	{
		if (started)
		{
			int num = 1000 - ((turn != PlayerColor.Black) ? WhiteRemainTime : BlackRemainTime).ElapsedTime % 1000;
			if (num == 0)
			{
				num = 1000;
			}
			updateTimer.Interval = num;
			updateTimer.Start();
		}
	}

	public void UpdateRemain()
	{
		blackRemainTime = RemainTime(PlayerColor.Black, BlackTime);
		whiteRemainTime = RemainTime(PlayerColor.White, WhiteTime);
	}

	private void timer_Tick(object sender, EventArgs e)
	{
		timer.Enabled = false;
		if (this.Timeout != null)
		{
			this.Timeout(sender, new EventArgs());
		}
	}

	private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
	{
		if (this.UpdateTime != null)
		{
			this.UpdateTime(sender, new EventArgs());
		}
	}

	public void Dispose()
	{
		if (timer != null)
		{
			timer.Dispose();
		}
		if (updateTimer != null)
		{
			updateTimer.Dispose();
		}
	}
}
