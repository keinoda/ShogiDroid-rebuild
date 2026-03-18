using System;
using System.Threading;
using ShogiGUI.Events;

namespace ShogiGUI.Engine;

/// <summary>
/// Monitors vast.ai instance idle time and auto-suspends after a configured period.
/// Singleton — lives for the app lifetime.
/// </summary>
public sealed class VastAiWatchdog : IDisposable
{
	private static VastAiWatchdog instance_;
	private static readonly object lockObj_ = new object();

	/// <summary>Default idle timeout: 60 minutes.</summary>
	public const int DefaultIdleMinutes = 60;

	private Timer checkTimer_;
	private DateTime lastActivityTime_;
	private int idleTimeoutMinutes_;
	private int monitoredInstanceId_;
	private bool suspended_;
	private string apiKey_;

	public static VastAiWatchdog Instance
	{
		get
		{
			if (instance_ == null)
			{
				lock (lockObj_)
				{
					instance_ ??= new VastAiWatchdog();
				}
			}
			return instance_;
		}
	}

	public bool IsMonitoring => monitoredInstanceId_ > 0 && checkTimer_ != null;
	public int MonitoredInstanceId => monitoredInstanceId_;
	public DateTime LastActivityTime => lastActivityTime_;
	public int IdleTimeoutMinutes => idleTimeoutMinutes_;

	/// <summary>Raised when an instance is auto-suspended.</summary>
	public event Action<int> InstanceAutoSuspended;

	private VastAiWatchdog()
	{
		idleTimeoutMinutes_ = DefaultIdleMinutes;
	}

	/// <summary>
	/// Start monitoring a vast.ai instance. Call when a remote engine backed by vast.ai connects.
	/// </summary>
	public void StartMonitoring(int instanceId, string apiKey, int idleTimeoutMinutes = DefaultIdleMinutes)
	{
		lock (lockObj_)
		{
			StopMonitoringInternal();

			monitoredInstanceId_ = instanceId;
			apiKey_ = apiKey;
			idleTimeoutMinutes_ = idleTimeoutMinutes;
			suspended_ = false;
			lastActivityTime_ = DateTime.UtcNow;

			// Check every 5 minutes
			checkTimer_ = new Timer(CheckIdle, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

			AppDebug.Log.Info($"VastAiWatchdog: started monitoring instance {instanceId}, timeout={idleTimeoutMinutes}min");
		}
	}

	/// <summary>
	/// Stop monitoring. Call when engine is terminated or instance is manually stopped.
	/// </summary>
	public void StopMonitoring()
	{
		lock (lockObj_)
		{
			StopMonitoringInternal();
			AppDebug.Log.Info("VastAiWatchdog: monitoring stopped");
		}
	}

	/// <summary>
	/// Record engine activity. Resets the idle timer.
	/// Call on: analysis start, go, info received, bestmove, etc.
	/// </summary>
	public void RecordActivity()
	{
		lastActivityTime_ = DateTime.UtcNow;
	}

	/// <summary>
	/// Handle a game event to automatically track engine activity.
	/// </summary>
	public void OnGameEvent(GameEventId eventId)
	{
		switch (eventId)
		{
			case GameEventId.AnalyzeStart:
			case GameEventId.GameStart:
			case GameEventId.MateStart:
			case GameEventId.Info:
			case GameEventId.Moved:
				RecordActivity();
				break;
		}
	}

	public TimeSpan GetIdleTime()
	{
		return DateTime.UtcNow - lastActivityTime_;
	}

	public TimeSpan GetRemainingTime()
	{
		var remaining = TimeSpan.FromMinutes(idleTimeoutMinutes_) - GetIdleTime();
		return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
	}

	private void CheckIdle(object state)
	{
		lock (lockObj_)
		{
			if (monitoredInstanceId_ <= 0 || suspended_)
				return;

			var idleTime = GetIdleTime();
			AppDebug.Log.Info($"VastAiWatchdog: idle check — {idleTime.TotalMinutes:F0}min / {idleTimeoutMinutes_}min");

			if (idleTime.TotalMinutes >= idleTimeoutMinutes_)
			{
				AppDebug.Log.Info($"VastAiWatchdog: idle timeout reached, suspending instance {monitoredInstanceId_}");
				SuspendInstanceAsync();
			}
		}
	}

	private async void SuspendInstanceAsync()
	{
		int instanceId = monitoredInstanceId_;
		suspended_ = true;

		try
		{
			using var manager = new VastAiManager(apiKey_);
			await manager.StopInstanceAsync(instanceId);
			AppDebug.Log.Info($"VastAiWatchdog: instance {instanceId} auto-suspended");

			Settings.EngineSettings.VastAiInstanceId = instanceId; // keep ID for resume
			Settings.Save();

			InstanceAutoSuspended?.Invoke(instanceId);
		}
		catch (Exception ex)
		{
			AppDebug.Log.Error($"VastAiWatchdog: auto-suspend failed: {ex.Message}");
			suspended_ = false; // retry next check
		}
	}

	private void StopMonitoringInternal()
	{
		checkTimer_?.Dispose();
		checkTimer_ = null;
		monitoredInstanceId_ = 0;
		suspended_ = false;
	}

	public void Dispose()
	{
		StopMonitoringInternal();
	}
}
