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
	private bool statusPollInFlight_;
	private string apiKey_;
	private string lastKnownStatus_;

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
	public string LastKnownStatus => lastKnownStatus_;

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
			statusPollInFlight_ = false;
			lastKnownStatus_ = string.Empty;
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
		int instanceId;
		string apiKey;
		lock (lockObj_)
		{
			if (monitoredInstanceId_ <= 0 || suspended_ || statusPollInFlight_)
				return;

			statusPollInFlight_ = true;
			instanceId = monitoredInstanceId_;
			apiKey = apiKey_;
		}

		_ = CheckIdleAsync(instanceId, apiKey);
	}

	private async System.Threading.Tasks.Task CheckIdleAsync(int instanceId, string apiKey)
	{
		try
		{
			VastAiInstance instance = null;
			if (!string.IsNullOrEmpty(apiKey))
			{
				using var manager = new VastAiManager(apiKey);
				instance = await manager.GetInstanceAsync(instanceId);
			}

			bool shouldSuspend = false;
			TimeSpan idleTime;

			lock (lockObj_)
			{
				if (monitoredInstanceId_ != instanceId || suspended_)
				{
					return;
				}

				if (instance != null)
				{
					lastKnownStatus_ = instance.ActualStatus ?? string.Empty;
					AppDebug.Log.Info($"VastAiWatchdog: instance {instanceId} status={instance.ActualStatus}, intended={instance.IntendedStatus}");

					if (instance.IsStopped)
					{
						suspended_ = true;
						checkTimer_?.Dispose();
						checkTimer_ = null;
						Settings.EngineSettings.VastAiInstanceId = instanceId;
						Settings.Save();
						AppDebug.Log.Info($"VastAiWatchdog: instance {instanceId} is already stopped");
						return;
					}

					if (!instance.IsRunning && !instance.IsLoading)
					{
						AppDebug.Log.Info($"VastAiWatchdog: skip idle suspend because status is {instance.ActualStatus}");
						return;
					}
				}

				idleTime = GetIdleTime();
				AppDebug.Log.Info($"VastAiWatchdog: idle check — {idleTime.TotalMinutes:F0}min / {idleTimeoutMinutes_}min");
				shouldSuspend = idleTime.TotalMinutes >= idleTimeoutMinutes_;
			}

			if (!shouldSuspend)
			{
				return;
			}

			AppDebug.Log.Info($"VastAiWatchdog: idle timeout reached, suspending instance {instanceId}");
			await SuspendInstanceAsync(instanceId, apiKey);
		}
		catch (Exception ex)
		{
			AppDebug.Log.Error($"VastAiWatchdog: idle poll failed: {ex.Message}");
		}
		finally
		{
			lock (lockObj_)
			{
				statusPollInFlight_ = false;
			}
		}
	}

	private async System.Threading.Tasks.Task SuspendInstanceAsync(int instanceId, string apiKey)
	{
		try
		{
			using var manager = new VastAiManager(apiKey);
			await manager.StopInstanceAsync(instanceId);
			AppDebug.Log.Info($"VastAiWatchdog: instance {instanceId} auto-suspended");

			lock (lockObj_)
			{
				suspended_ = true;
				checkTimer_?.Dispose();
				checkTimer_ = null;
			}

			Settings.EngineSettings.VastAiInstanceId = instanceId; // keep ID for resume
			Settings.Save();

			InstanceAutoSuspended?.Invoke(instanceId);
		}
		catch (Exception ex)
		{
			AppDebug.Log.Error($"VastAiWatchdog: auto-suspend failed: {ex.Message}");
		}
	}

	private void StopMonitoringInternal()
	{
		checkTimer_?.Dispose();
		checkTimer_ = null;
		monitoredInstanceId_ = 0;
		suspended_ = false;
		statusPollInFlight_ = false;
		apiKey_ = string.Empty;
		lastKnownStatus_ = string.Empty;
	}

	public void Dispose()
	{
		StopMonitoringInternal();
	}
}
