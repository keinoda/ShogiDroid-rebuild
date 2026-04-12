using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShogiGUI.Events;

namespace ShogiGUI.Engine;

/// <summary>
/// クラウドインスタンスのアイドル自動終了を管理するシングルトン。
/// vast.ai / GCP のいずれかを扱える汎用 Watchdog（AWS は対象外）。
/// 解析終了後に一定時間（デフォルト5分）新しい解析が開始されなければ
/// インスタンスを停止する。
/// </summary>
public sealed class CloudInstanceWatchdog : IDisposable
{
	private static CloudInstanceWatchdog instance_;
	private static readonly object lockObj_ = new object();

	/// <summary>解析終了後、自動終了までのデフォルト待機時間（分）</summary>
	public const int DefaultIdleMinutes = 5;

	private Timer shutdownTimer_;
	private int idleTimeoutMinutes_;
	private bool isAnalyzing_;
	private bool shutdownInFlight_;
	private CloudWatchdogConfig config_;

	/// <summary>
	/// 最後に接続した Vast.ai インスタンスの設定を一時保存する。
	/// 自動終了前に設定を保持し、次回接続時に復元可能にする。
	/// GCP 用の再接続情報保持は未対応。
	/// </summary>
	public int LastInstanceId { get; private set; }
	public string LastSshHost { get; private set; } = string.Empty;
	public int LastSshPort { get; private set; }
	public string LastEngineCommand { get; private set; } = string.Empty;

	public static CloudInstanceWatchdog Instance
	{
		get
		{
			if (instance_ == null)
			{
				lock (lockObj_)
				{
					instance_ ??= new CloudInstanceWatchdog();
				}
			}
			return instance_;
		}
	}

	public bool IsMonitoring => config_ != null;
	public bool IsShutdownPending => shutdownTimer_ != null;
	public int IdleTimeoutMinutes => idleTimeoutMinutes_;
	public string CurrentProvider => config_?.Provider ?? string.Empty;

	/// <summary>全インスタンスが自動終了されたときに発火する。</summary>
	public event Action InstanceAutoStopped;

	private CloudInstanceWatchdog()
	{
		idleTimeoutMinutes_ = DefaultIdleMinutes;
	}

	/// <summary>
	/// 監視を開始する。リモートエンジン接続時に呼び出す。
	/// config.IsValid() が false の場合は何もしない。
	/// </summary>
	public void StartMonitoring(CloudWatchdogConfig config, int idleTimeoutMinutes = DefaultIdleMinutes)
	{
		if (config == null || !config.IsValid())
		{
			AppDebug.Log.Info($"CloudInstanceWatchdog: 監視開始をスキップ (provider={config?.Provider ?? "null"}, 設定不備)");
			return;
		}

		lock (lockObj_)
		{
			config_ = config;
			idleTimeoutMinutes_ = idleTimeoutMinutes;
			if (config.Provider == "vastai")
			{
				LastInstanceId = config.VastAiInstanceId;
			}
			isAnalyzing_ = false;
			shutdownInFlight_ = false;
			CancelShutdownTimer();

			AppDebug.Log.Info($"CloudInstanceWatchdog: 監視開始 provider={config.Provider}, timeout={idleTimeoutMinutes}分");
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
			config_ = null;
			isAnalyzing_ = false;
			shutdownInFlight_ = false;
			AppDebug.Log.Info("CloudInstanceWatchdog: 監視停止");
		}
	}

	/// <summary>
	/// アクティビティを記録する。解析中のシャットダウンタイマーをキャンセルする。
	/// </summary>
	public void RecordActivity()
	{
		lock (lockObj_)
		{
			if (shutdownTimer_ != null)
			{
				CancelShutdownTimer();
				AppDebug.Log.Info("CloudInstanceWatchdog: アクティビティ検知、シャットダウンタイマーをキャンセル");
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
			if (config_ == null)
				return;

			switch (eventId)
			{
				// 解析開始: シャットダウンタイマーをキャンセル
				case GameEventId.AnalyzeStart:
				case GameEventId.GameStart:
				case GameEventId.MateStart:
					isAnalyzing_ = true;
					CancelShutdownTimer();
					AppDebug.Log.Info($"CloudInstanceWatchdog: 解析開始検知 ({eventId})、タイマーキャンセル");
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
					AppDebug.Log.Info($"CloudInstanceWatchdog: 解析終了検知 ({eventId})、{idleTimeoutMinutes_}分後にシャットダウン予定");
					break;
			}
		}
	}

	/// <summary>
	/// 最後に接続した Vast.ai インスタンスの情報を保存する。
	/// 自動終了前に呼び出し、次回の再接続に備える（Vast.ai 限定）。
	/// </summary>
	public void SaveLastConnectionInfo(int instanceId, string sshHost, int sshPort, string engineCommand)
	{
		LastInstanceId = instanceId;
		LastSshHost = sshHost ?? string.Empty;
		LastSshPort = sshPort;
		LastEngineCommand = engineCommand ?? string.Empty;
		AppDebug.Log.Info($"CloudInstanceWatchdog: 接続情報を保存 instance={instanceId}, host={sshHost}:{sshPort}");
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
		CloudWatchdogConfig snapshot;
		lock (lockObj_)
		{
			if (isAnalyzing_ || shutdownInFlight_ || config_ == null)
			{
				CancelShutdownTimer();
				return;
			}
			shutdownInFlight_ = true;
			snapshot = config_;
			CancelShutdownTimer();
		}

		_ = ShutdownAsync(snapshot);
	}

	private async Task ShutdownAsync(CloudWatchdogConfig config)
	{
		try
		{
			AppDebug.Log.Info($"CloudInstanceWatchdog: アイドルタイムアウト、インスタンスを一時停止します (provider={config.Provider})");

			switch (config.Provider)
			{
				case "vastai":
					await StopVastAiAsync(config);
					break;
				case "gcp":
					await StopGcpAsync(config);
					break;
				default:
					AppDebug.Log.Error($"CloudInstanceWatchdog: 未対応プロバイダ ({config.Provider})");
					break;
			}

			lock (lockObj_)
			{
				shutdownInFlight_ = false;
			}

			InstanceAutoStopped?.Invoke();
		}
		catch (Exception ex)
		{
			AppDebug.Log.Error($"CloudInstanceWatchdog: 自動一時停止に失敗: {ex.Message}");
			lock (lockObj_)
			{
				shutdownInFlight_ = false;
			}
		}
	}

	private async Task StopVastAiAsync(CloudWatchdogConfig config)
	{
		// 現在のエンジン設定を保存（次回復元用）
		Settings.EngineSettings.VastAiInstanceId = LastInstanceId;
		Settings.Save();

		using var manager = new VastAiManager(config.VastAiApiKey);
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
					AppDebug.Log.Info($"CloudInstanceWatchdog: vast.ai インスタンス #{inst.Id} を一時停止しました");
				}
				catch (Exception ex)
				{
					AppDebug.Log.Error($"CloudInstanceWatchdog: vast.ai インスタンス #{inst.Id} の一時停止に失敗: {ex.Message}");
				}
			}
		}

		AppDebug.Log.Info($"CloudInstanceWatchdog: {stopped}個の vast.ai インスタンスを一時停止しました");
	}

	private async Task StopGcpAsync(CloudWatchdogConfig config)
	{
		using var manager = new GcpSpotManager(config.GcpServiceAccountKeyPath);
		await manager.StopInstanceAsync(config.GcpZone, config.GcpInstanceName);
		AppDebug.Log.Info($"CloudInstanceWatchdog: GCP インスタンス {config.GcpInstanceName} ({config.GcpZone}) を停止しました");
	}

	private void StopMonitoringInternal()
	{
		CancelShutdownTimer();
		config_ = null;
		isAnalyzing_ = false;
		shutdownInFlight_ = false;
	}

	public void Dispose()
	{
		StopMonitoringInternal();
	}
}

/// <summary>
/// CloudInstanceWatchdog に渡す接続設定。Provider 毎に必要なフィールドが異なる。
/// IsValid() は必要フィールドが揃っているかのみ検査する（疎通確認は行わない）。
/// </summary>
public sealed record CloudWatchdogConfig(
	string Provider,
	int VastAiInstanceId,
	string VastAiApiKey,
	string GcpServiceAccountKeyPath,
	string GcpZone,
	string GcpInstanceName)
{
	public bool IsValid() => Provider switch
	{
		"vastai" => VastAiInstanceId > 0 && !string.IsNullOrEmpty(VastAiApiKey),
		"gcp" => !string.IsNullOrEmpty(GcpServiceAccountKeyPath)
				 && !string.IsNullOrEmpty(GcpZone)
				 && !string.IsNullOrEmpty(GcpInstanceName),
		_ => false,
	};

	public static CloudWatchdogConfig ForVastAi(int instanceId, string apiKey) =>
		new(
			Provider: "vastai",
			VastAiInstanceId: instanceId,
			VastAiApiKey: apiKey ?? string.Empty,
			GcpServiceAccountKeyPath: string.Empty,
			GcpZone: string.Empty,
			GcpInstanceName: string.Empty);

	public static CloudWatchdogConfig ForGcp(string serviceAccountKeyPath, string zone, string instanceName) =>
		new(
			Provider: "gcp",
			VastAiInstanceId: 0,
			VastAiApiKey: string.Empty,
			GcpServiceAccountKeyPath: serviceAccountKeyPath ?? string.Empty,
			GcpZone: zone ?? string.Empty,
			GcpInstanceName: instanceName ?? string.Empty);
}
