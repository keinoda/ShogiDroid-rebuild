using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShogiGUI.Events;

namespace ShogiGUI.Engine;

/// <summary>
/// vast.ai インスタンスのアイドル自動終了を管理するシングルトン。
/// 解析終了後に一定時間（デフォルト5分）新しい解析が開始されなければ
/// 全インスタンスを Destroy し、最後の接続設定を保持する。
/// </summary>
public sealed class VastAiWatchdog : IDisposable
{
	private static VastAiWatchdog instance_;
	private static readonly object lockObj_ = new object();

	/// <summary>解析終了後、自動終了までのデフォルト待機時間（分）</summary>
	public const int DefaultIdleMinutes = 5;

	private Timer shutdownTimer_;
	private int idleTimeoutMinutes_;
	private bool isAnalyzing_;
	private bool shutdownInFlight_;
	private string apiKey_;

	/// <summary>
	/// 最後に接続したインスタンスの設定を一時保存するフラグ。
	/// 自動終了前に設定を保持し、次回接続時に復元可能にする。
	/// </summary>
	public int LastInstanceId { get; private set; }
	public string LastSshHost { get; private set; } = string.Empty;
	public int LastSshPort { get; private set; }
	public string LastEngineCommand { get; private set; } = string.Empty;

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

	public bool IsMonitoring => !string.IsNullOrEmpty(apiKey_);
	public bool IsShutdownPending => shutdownTimer_ != null;
	public int IdleTimeoutMinutes => idleTimeoutMinutes_;

	/// <summary>全インスタンスが自動終了されたときに発火する。</summary>
	public event Action InstanceAutoStopped;

	private VastAiWatchdog()
	{
		idleTimeoutMinutes_ = DefaultIdleMinutes;
	}

	/// <summary>
	/// 監視を開始する。リモートエンジン接続時に呼び出す。
	/// </summary>
	public void StartMonitoring(int instanceId, string apiKey, int idleTimeoutMinutes = DefaultIdleMinutes)
	{
		lock (lockObj_)
		{
			apiKey_ = apiKey;
			idleTimeoutMinutes_ = idleTimeoutMinutes;
			LastInstanceId = instanceId;
			isAnalyzing_ = false;
			shutdownInFlight_ = false;
			CancelShutdownTimer();

			AppDebug.Log.Info($"VastAiWatchdog: 監視開始 instance={instanceId}, timeout={idleTimeoutMinutes}分");
		}
	}

	/// <summary>
	/// 監視を停止する。エンジン切断時や手動停止時に呼び出す。
	/// </summary>
	public void StopMonitoring()
	{
		lock (lockObj_)
		{
			CancelShutdownTimer();
			apiKey_ = string.Empty;
			isAnalyzing_ = false;
			shutdownInFlight_ = false;
			AppDebug.Log.Info("VastAiWatchdog: 監視停止");
		}
	}

	/// <summary>
	/// アクティビティを記録する（互換性のため残す）。
	/// 解析中のフラグをリセットし、シャットダウンタイマーをキャンセルする。
	/// </summary>
	public void RecordActivity()
	{
		lock (lockObj_)
		{
			if (shutdownTimer_ != null)
			{
				CancelShutdownTimer();
				AppDebug.Log.Info("VastAiWatchdog: アクティビティ検知、シャットダウンタイマーをキャンセル");
			}
		}
	}

	/// <summary>
	/// ゲームイベントを処理し、解析の開始・終了を追跡する。
	/// </summary>
	public void OnGameEvent(GameEventId eventId)
	{
		lock (lockObj_)
		{
			if (string.IsNullOrEmpty(apiKey_))
				return;

			switch (eventId)
			{
				// 解析開始: シャットダウンタイマーをキャンセル
				case GameEventId.AnalyzeStart:
				case GameEventId.GameStart:
				case GameEventId.MateStart:
					isAnalyzing_ = true;
					CancelShutdownTimer();
					AppDebug.Log.Info($"VastAiWatchdog: 解析開始検知 ({eventId})、タイマーキャンセル");
					break;

				// 解析中のアクティビティ: 何もしない（解析中フラグは維持）
				case GameEventId.Info:
				case GameEventId.Moved:
					break;

				// 解析終了: シャットダウンタイマーを起動
				case GameEventId.AnalyzeEnd:
				case GameEventId.NotationAnalyzeEnd:
				case GameEventId.MateEnd:
				case GameEventId.GameEnd:
				case GameEventId.GameOver:
					isAnalyzing_ = false;
					StartShutdownTimer();
					AppDebug.Log.Info($"VastAiWatchdog: 解析終了検知 ({eventId})、{idleTimeoutMinutes_}分後にシャットダウン予定");
					break;
			}
		}
	}

	/// <summary>
	/// 最後に接続したインスタンスの設定を保存する。
	/// 自動終了前に呼び出し、次回の再接続に備える。
	/// </summary>
	public void SaveLastConnectionInfo(int instanceId, string sshHost, int sshPort, string engineCommand)
	{
		LastInstanceId = instanceId;
		LastSshHost = sshHost ?? string.Empty;
		LastSshPort = sshPort;
		LastEngineCommand = engineCommand ?? string.Empty;
		AppDebug.Log.Info($"VastAiWatchdog: 接続情報を保存 instance={instanceId}, host={sshHost}:{sshPort}");
	}

	private void StartShutdownTimer()
	{
		CancelShutdownTimer();
		int delayMs = idleTimeoutMinutes_ * 60 * 1000;
		shutdownTimer_ = new Timer(OnShutdownTimerFired, null, delayMs, Timeout.Infinite);
	}

	private void CancelShutdownTimer()
	{
		shutdownTimer_?.Dispose();
		shutdownTimer_ = null;
	}

	private void OnShutdownTimerFired(object state)
	{
		string apiKey;
		lock (lockObj_)
		{
			// 解析中ならスキップ
			if (isAnalyzing_ || shutdownInFlight_ || string.IsNullOrEmpty(apiKey_))
			{
				CancelShutdownTimer();
				return;
			}
			shutdownInFlight_ = true;
			apiKey = apiKey_;
			CancelShutdownTimer();
		}

		_ = StopAllInstancesAsync(apiKey);
	}

	private async Task StopAllInstancesAsync(string apiKey)
	{
		try
		{
			AppDebug.Log.Info("VastAiWatchdog: アイドルタイムアウト、全インスタンスを一時停止します");

			// 現在のエンジン設定を保存（次回復元用）
			Settings.EngineSettings.VastAiInstanceId = LastInstanceId;
			Settings.Save();

			using var manager = new VastAiManager(apiKey);
			List<VastAiInstance> instances = await manager.ListInstancesAsync();

			int stopped = 0;
			foreach (var inst in instances)
			{
				if ((inst.IsShogiInstance || inst.Id == LastInstanceId) && inst.IsRunning)
				{
					try
					{
						await manager.StopInstanceAsync(inst.Id);
						stopped++;
						AppDebug.Log.Info($"VastAiWatchdog: インスタンス #{inst.Id} を一時停止しました");
					}
					catch (Exception ex)
					{
						AppDebug.Log.Error($"VastAiWatchdog: インスタンス #{inst.Id} の一時停止に失敗: {ex.Message}");
					}
				}
			}

			AppDebug.Log.Info($"VastAiWatchdog: {stopped}個のインスタンスを一時停止しました");

			lock (lockObj_)
			{
				shutdownInFlight_ = false;
			}

			InstanceAutoStopped?.Invoke();
		}
		catch (Exception ex)
		{
			AppDebug.Log.Error($"VastAiWatchdog: 自動一時停止に失敗: {ex.Message}");
			lock (lockObj_)
			{
				shutdownInFlight_ = false;
			}
		}
	}

	private void StopMonitoringInternal()
	{
		CancelShutdownTimer();
		apiKey_ = string.Empty;
		isAnalyzing_ = false;
		shutdownInFlight_ = false;
	}

	public void Dispose()
	{
		StopMonitoringInternal();
	}
}
