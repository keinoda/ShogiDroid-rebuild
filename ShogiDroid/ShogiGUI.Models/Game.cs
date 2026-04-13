using ShogiDroid;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Android.App;
using ShogiGUI.Engine;
using ShogiGUI.Events;
using ShogiLib;

namespace ShogiGUI.Models;

public class Game
{
	private enum EngineMode
	{
		None,
		Play,
		Hint,
		Mate,
		Analyze
	}

	private GameMode gameMode;

	private const int DefaultRemotePort = 28597;

	private History history = new History();

	private NotationModel notationModel = new NotationModel();

	private EnginePlayer enginePlayer;

	private IPlayer blackPlayer;

	private IPlayer whitePlayer;

	private IPlayer dummyPlayer;

	private GameParam gameParam;

	private SynchronizationContext syncContext;

	private GameTimer gameTimer;

	private bool cancel;

	private bool busy;

	private bool bothcomputer;

	private ComputerState comState;

	private PvInfos pvinfos = new PvInfos();

	private HintInfo hint_info = new HintInfo();

	private AnalyzeInfoList analyzeInfoList = new AnalyzeInfoList();

	private ThreatmateAnalyzer threatmateAnalyzer = new ThreatmateAnalyzer();

	private PolicyAnalyzer policyAnalyzer = new PolicyAnalyzer();

	private int analyzeStopNumber_ = -1;

	private EngineMode engineMode;

	private static string gameText = Application.Context.GetString(Resource.String.Game_Text);

	private static string analysisText = Application.Context.GetString(Resource.String.Analysis_Text);

	private static string analysis_comment_Text = Application.Context.GetString(Resource.String.Analysis_comment_Text);

	public GameMode GameMode
	{
		get
		{
			return gameMode;
		}
		set
		{
			gameMode = value;
		}
	}

	public NotationModel NotationModel
	{
		get
		{
			return notationModel;
		}
		set
		{
			notationModel = value;
		}
	}

	public SNotation Notation => notationModel.Notation;

	private IPlayer CurrentPlayer
	{
		get
		{
			if (Notation.Position.Turn != PlayerColor.Black)
			{
				return whitePlayer;
			}
			return blackPlayer;
		}
	}

	public ComputerState ComState => comState;

	public bool IsEngineModePlay => engineMode == EngineMode.Play;

	public PvInfos PvInfos => pvinfos;

	public bool Busy => busy;

	public bool ShouldKeepRemoteAnalysisRunningOnPause()
	{
		if (Settings.EngineSettings.EngineNo != RemoteEnginePlayer.RemoteEngineNo || enginePlayer == null)
		{
			return false;
		}

		if (gameMode != GameMode.Analyzer && gameMode != GameMode.Consider)
		{
			return false;
		}

		return busy || comState.IsThinking();
	}

	public HintInfo HintInfo => hint_info;

	public GameRemainTime BlackTime => gameTimer.BlackRemainTime;

	public GameRemainTime WhiteTime => gameTimer.WhiteRemainTime;

	public bool BothComputer => bothcomputer;

	public EnginePlayer EnginePlayer => enginePlayer;

	public ThreatmateInfo ThreatmateInfo => threatmateAnalyzer.CurrentInfo;

	public PolicyInfo PolicyInfo => policyAnalyzer.CurrentInfo;

	public event EventHandler<GameEventArgs> GameEventHandler;

	public Game()
	{
		syncContext = SynchronizationContext.Current;
		dummyPlayer = new HumanPlayer(PlayerColor.Black);
		blackPlayer = dummyPlayer;
		whitePlayer = dummyPlayer;
		gameTimer = new GameTimer(GameTimer_Timeout);
		gameTimer.UpdateTime += GameTimer_UpdateTime;
		notationModel.NotationChanged += NotationModel_NotationChanged;
		threatmateAnalyzer.Updated += ThreatmateAnalyzer_Updated;
		policyAnalyzer.Updated += PolicyAnalyzer_Updated;
	}

	protected virtual void OnGameEvent(GameEventArgs e)
	{
		// vast.ai / GCP のクラウドエンジン使用中のみ Watchdog にイベントを転送
		if (Settings.EngineSettings.EngineNo == RemoteEnginePlayer.RemoteEngineNo
			&& (Settings.EngineSettings.CloudProvider == "vastai"
				|| Settings.EngineSettings.CloudProvider == "gcp"))
		{
			CloudInstanceWatchdog.Instance.OnGameEvent(e.EventId);
		}

		if (this.GameEventHandler != null)
		{
			this.GameEventHandler(this, e);
		}
	}

	public void Destroy()
	{
		threatmateAnalyzer.Dispose();
		policyAnalyzer.Dispose();
		EngineTerminate();
	}

	public void EngineWakeup()
	{
		if (enginePlayer == null)
		{
			if (!initEnginePlayer(Settings.EngineSettings.EngineNo))
			{
				EngineTerminate();
				OnGameEvent(new GameEventArgs(GameEventId.InitializeError));
				return;
			}
			busy = true;
			cancel = false;
			comState = ComputerState.None;
			engineMode = EngineMode.None;
			OnGameEvent(new GameEventArgs(GameEventId.InitializeStart));
		}
	}

	public void EngineTerminate()
	{
		if (enginePlayer != null)
		{
			cancel = true;
			enginePlayer.Stop();
			enginePlayer.Terminate();
			enginePlayer = null;
			comState = ComputerState.None;
			busy = false;
			// エンジン切替時に Watchdog 監視を停止（ローカルエンジンへの切替で課金が続くのを防止）
			CloudInstanceWatchdog.Instance.StopMonitoring();
			OnGameEvent(new GameEventArgs(GameEventId.AnalyzeEnd));
		}
	}

	public void GameStart(GameParam param)
	{
		cancel = false;
		notationModel.Initialize(param.StartPosition, param.StartMode, param.BlackName, param.WhiteName, param.Handicap);
		history.Init(notationModel.Notation);
		gameParam = new GameParam(param);
		gameMode = GameMode.Play;
		if (gameParam.BlackNo == 0)
		{
			gameTimer.SetTime(PlayerColor.Black, 0, 0);
		}
		else
		{
			gameTimer.SetTime(PlayerColor.Black, param.Time, param.Countdown, param.Increment);
		}
		if (gameParam.WhiteNo == 0)
		{
			gameTimer.SetTime(PlayerColor.White, 0, 0);
		}
		else
		{
			gameTimer.SetTime(PlayerColor.White, param.Time, param.Countdown, param.Increment);
		}
		bothcomputer = gameParam.BlackNo != 0 && gameParam.WhiteNo != 0;
		if (gameParam.StartMode == GameStartMode.Continued)
		{
			gameTimer.SetRestartTime(Notation.TotalTime(PlayerColor.Black), Notation.TotalTime(PlayerColor.White));
		}
		if (param.BlackNo != 0 || param.WhiteNo != 0)
		{
			bool flag = false;
			if (enginePlayer == null)
			{
				flag = true;
				if (!initEnginePlayer(Settings.EngineSettings.EngineNo))
				{
					GameTerminate();
					OnGameEvent(new GameEventArgs(GameEventId.InitializeError));
					return;
				}
			}
			pvinfos.Clear();
			hint_info.Clear();
			OnGameEvent(new GameEventArgs(GameEventId.Info));
			engineMode = EngineMode.Play;
			busy = true;
			IPlayer player2;
			if (param.BlackNo != 0)
			{
				IPlayer player = enginePlayer;
				player2 = player;
			}
			else
			{
				player2 = dummyPlayer;
			}
			blackPlayer = player2;
			IPlayer player3;
			if (param.WhiteNo != 0)
			{
				IPlayer player = enginePlayer;
				player3 = player;
			}
			else
			{
				player3 = dummyPlayer;
			}
			whitePlayer = player3;
			if (flag)
			{
				OnGameEvent(new GameEventArgs(GameEventId.InitializeStart));
			}
			else
			{
				enginePlayer.Ready();
			}
		}
		else
		{
			pvinfos.Clear();
			hint_info.Clear();
			OnGameEvent(new GameEventArgs(GameEventId.Info));
			blackPlayer = dummyPlayer;
			whitePlayer = dummyPlayer;
			engineMode = EngineMode.Play;
			blackPlayer.GameStart();
			whitePlayer.GameStart();
			CurrentPlayer.Go(Notation, gameTimer);
			gameTimer.Start(Notation.Position.Turn);
			OnGameEvent(new GameEventArgs(GameEventId.GameStart));
		}
	}

	public void GameRestart()
	{
		if (gameMode == GameMode.Play && enginePlayer != null)
		{
			if (comState.IsThinking())
			{
				enginePlayer.Stop();
			}
			if (CurrentPlayer == enginePlayer)
			{
				comState = ComputerState.Thinking;
			}
			engineMode = EngineMode.Play;
			enginePlayer.Ready();
		}
	}

	public void Hint()
	{
		cancel = false;
		if (comState == ComputerState.Analyzing)
		{
			enginePlayer.Stop();
			return;
		}
		if (enginePlayer == null)
		{
			if (!initEnginePlayer(Settings.EngineSettings.EngineNo))
			{
				OnGameEvent(new GameEventArgs(GameEventId.InitializeError));
				return;
			}
			busy = true;
			pvinfos.Clear();
			hint_info.Clear();
			OnGameEvent(new GameEventArgs(GameEventId.Info));
			engineMode = EngineMode.Hint;
			comState = ComputerState.Stop;
			OnGameEvent(new GameEventArgs(GameEventId.InitializeStart));
			return;
		}
		if (comState != ComputerState.Stop)
		{
			enginePlayer.Stop();
		}
		pvinfos.Clear();
		hint_info.Clear();
		OnGameEvent(new GameEventArgs(GameEventId.Info));
		comState = ComputerState.Stop;
		engineMode = EngineMode.Hint;
		enginePlayer.Ready();
		busy = true;
		OnGameEvent(new GameEventArgs(GameEventId.InitializeStart));
	}

	public void Mate()
	{
		cancel = false;
		if (comState == ComputerState.Mating)
		{
			enginePlayer.Stop();
			return;
		}
		if (enginePlayer == null)
		{
			if (!initEnginePlayer(Settings.EngineSettings.EngineNo))
			{
				OnGameEvent(new GameEventArgs(GameEventId.InitializeError));
				return;
			}
			busy = true;
			comState = ComputerState.Stop;
			engineMode = EngineMode.Mate;
			OnGameEvent(new GameEventArgs(GameEventId.InitializeStart));
			return;
		}
		if (comState != ComputerState.Stop)
		{
			enginePlayer.Stop();
		}
		comState = ComputerState.Stop;
		engineMode = EngineMode.Mate;
		enginePlayer.Ready();
		busy = true;
		OnGameEvent(new GameEventArgs(GameEventId.InitializeStart));
	}

	public void AnalyzerStart()
	{
		cancel = false;
		analyzeStopNumber_ = -1;
		if (comState.IsThinking())
		{
			enginePlayer.Stop();
		}
		if (Settings.AnalyzeSettings.Reverse)
		{
			// 逆順: 終局から開始、NowPositionの場合は現在位置を記録して終局へ
			if (Settings.AnalyzeSettings.AnalyzePositon == GameStartPosition.InitialPosition)
			{
				NotationModel.Last();
			}
			else
			{
				analyzeStopNumber_ = Notation.MoveCurrent.Number;
				NotationModel.Last();
			}
			// 投了等の結果ノードにいる場合、実際の最終指し手まで戻る
			while (Notation.MoveCurrent.MoveType.HasFlag(MoveType.ResultFlag)
				|| !Notation.MoveCurrent.MoveType.IsMove())
			{
				if (!NotationModel.Prev()) break;
			}
		}
		else
		{
			if (Settings.AnalyzeSettings.AnalyzePositon == GameStartPosition.InitialPosition)
				NotationModel.First();
			// NowPosition+Forward: 現在位置から開始（何もしない）
		}
		analyzeInfoList.Clear();
		pvinfos.Clear();
		hint_info.Clear();
		if (enginePlayer == null)
		{
			if (!initEnginePlayer(Settings.EngineSettings.EngineNo))
			{
				OnGameEvent(new GameEventArgs(GameEventId.InitializeError));
				return;
			}
			busy = true;
			comState = ComputerState.Stop;
			engineMode = EngineMode.Analyze;
			gameMode = GameMode.Analyzer;
			OnGameEvent(new GameEventArgs(GameEventId.InitializeStart));
		}
		else
		{
			busy = true;
			comState = ComputerState.Stop;
			engineMode = EngineMode.Analyze;
			gameMode = GameMode.Analyzer;
			enginePlayer.Ready();
			OnGameEvent(new GameEventArgs(GameEventId.InitializeStart));
		}
	}

	/// <summary>
	/// 並列解析の進捗通知
	/// </summary>
	public event Action<string> ParallelAnalyzeProgress;

	/// <summary>
	/// リモートサーバーで並列棋譜解析を実行
	/// </summary>
	public async System.Threading.Tasks.Task ParallelAnalyzeAsync(int workers, long nodesPerMove, CancellationToken ct)
	{
		int threadsPerWorker = Settings.AnalyzeSettings.ParallelThreadsPerWorker;
		int hashPerWorker = Settings.AnalyzeSettings.ParallelHashPerWorker;

		gameMode = GameMode.Analyzer;
		busy = true;

		try
		{
			var results = await ParallelAnalysisTaskRunner.ExecuteAsync(
				Notation,
				workers,
				nodesPerMove,
				threadsPerWorker,
				hashPerWorker,
				msg => ParallelAnalyzeProgress?.Invoke(msg),
				ct);
			ApplyParallelResults(results);
			ParallelAnalyzeProgress?.Invoke($"解析完了: {results.Count}手");
		}
		catch (Exception ex)
		{
			AppDebug.Log.ErrorException(ex, "ParallelAnalyze failed");
			ParallelAnalyzeProgress?.Invoke($"解析エラー: {ex.Message}");
			throw;
		}
		finally
		{
			gameMode = GameMode.Input;
			busy = false;
			OnGameEvent(new GameEventArgs(GameEventId.NotationAnalyzeEnd));
		}
	}

	/// <summary>
	/// 並列解析結果をNotationに反映
	/// </summary>
	private void ApplyParallelResults(List<ParallelAnalyzer.MoveResult> results)
	{
		ParallelAnalysisTaskRunner.ApplyResults(Notation, results, Settings.AppSettings.MoveStyle);

		// 棋譜変更を通知
		NotationModel.OnNotationChangedPublic(new ShogiGUI.Events.NotationEventArgs(ShogiGUI.Events.NotationEventId.OBJECT_CHANGED));
	}

	private void AnalyzeEnd()
	{
		if (gameMode == GameMode.Analyzer)
		{
			enginePlayer.Stop();
			gameMode = GameMode.Input;
			OnGameEvent(new GameEventArgs(GameEventId.NotationAnalyzeEnd));
		}
	}

	public void ConsiderStart()
	{
		cancel = false;
		pvinfos.Clear();
		hint_info.Clear();
		if (enginePlayer == null)
		{
			if (!initEnginePlayer(Settings.EngineSettings.EngineNo))
			{
				OnGameEvent(new GameEventArgs(GameEventId.InitializeError));
				return;
			}
			busy = true;
			comState = ComputerState.Stop;
			engineMode = EngineMode.Hint;
			gameMode = GameMode.Consider;
			OnGameEvent(new GameEventArgs(GameEventId.InitializeStart));
			return;
		}
		gameMode = GameMode.Consider;
		comState = ComputerState.Stop;
		engineMode = EngineMode.Hint;
		if (enginePlayer.CanQueueGoRequest)
		{
			StartAnalyzeCommand(new AnalyzeTimeSettings(Settings.AnalyzeSettings.Time, -1L, -1L));
			return;
		}
		busy = true;
		enginePlayer.Ready();
		OnGameEvent(new GameEventArgs(GameEventId.InitializeStart));
	}

	private void ConsiderEnd()
	{
		if (gameMode == GameMode.Consider)
		{
			if (enginePlayer != null)
			{
				enginePlayer.Stop();
			}
			gameMode = GameMode.Input;
			OnGameEvent(new GameEventArgs(GameEventId.AnalyzeEnd));
		}
	}

	private void GameTerminate()
	{
		if (blackPlayer != dummyPlayer)
		{
			blackPlayer = dummyPlayer;
		}
		if (whitePlayer != dummyPlayer)
		{
			whitePlayer = dummyPlayer;
		}
		gameMode = GameMode.Input;
		busy = false;
		OnGameEvent(new GameEventArgs(GameEventId.GameEnd));
	}

	private bool initEnginePlayer(int playerNo)
	{
		AppDebug.Log.Info($"initEnginePlayer: playerNo={playerNo}");
		if (playerNo != 0)
		{
			if (playerNo == RemoteEnginePlayer.RemoteEngineNo)
			{
				string host = Settings.EngineSettings.RemoteHost;
				AppDebug.Log.Info($"initEnginePlayer: remote engine host={host}");
				if (string.IsNullOrEmpty(host))
				{
					AppDebug.Log.Error("initEnginePlayer: RemoteHost is empty");
					return false;
				}

				// SSH接続を優先、設定がなければTCPフォールバック
				string sshKeyPath = Settings.EngineSettings.VastAiSshKeyPath;
				int sshPort = Settings.EngineSettings.VastAiSshPort;
				string engineCmd = Settings.EngineSettings.VastAiSshEngineCommand;
				bool useSsh = !string.IsNullOrEmpty(sshKeyPath) && sshPort > 0 && !string.IsNullOrEmpty(engineCmd);

				RemoteEnginePlayer remoteEnginePlayer;
				if (useSsh)
				{
					AppDebug.Log.Info($"initEnginePlayer: SSH mode sshPort={sshPort}, cmd={engineCmd}");
					remoteEnginePlayer = new RemoteEnginePlayer(PlayerColor.Black, host, sshPort, sshKeyPath, engineCmd);
				}
				else
				{
					int port = DefaultRemotePort;
					int.TryParse(Settings.EngineSettings.RemotePort, out port);
					AppDebug.Log.Info($"initEnginePlayer: TCP mode port={port}");
					remoteEnginePlayer = new RemoteEnginePlayer(PlayerColor.Black, host, port);
				}

				remoteEnginePlayer.CopyFiles();
				remoteEnginePlayer.LoadSettings();

				// マシンIDが一致しない場合でもユーザー設定（MultiPV, FV_SCALE等）は保持する。
				// Threads/Hash等のリソース設定は OptionsApplying → ApplyVastAiAutoOptions で
				// tempOptions_ 経由で確実に上書きされるため、全クリアは不要。
				if (Settings.EngineSettings.VastAiMachineId > 0
					&& Settings.EngineSettings.VastAiOptionsMachineId != Settings.EngineSettings.VastAiMachineId)
				{
					AppDebug.Log.Info($"initEnginePlayer: マシンID不一致 (saved={Settings.EngineSettings.VastAiOptionsMachineId}, current={Settings.EngineSettings.VastAiMachineId}) → リソース設定は自動補正で上書き");
				}

				enginePlayer = remoteEnginePlayer;
				enginePlayer.OptionsApplying += Player_OptionsApplying;
				enginePlayer.Initialized += Player_Initialized;
				enginePlayer.ReadyOk += Player_ReadyOk;
				enginePlayer.BestMoveReceived += Player_BestMoveReceived;
				enginePlayer.CheckMateReceived += Player_CheckMateReceived;
				enginePlayer.Stopped += Player_Stopped;
				enginePlayer.InfoReceived += Player_InfoReceived;
				enginePlayer.ReportError += Player_ReportError;

				bool connected;
				if (useSsh)
				{
					connected = remoteEnginePlayer.InitSsh();
				}
				else
				{
					int port = DefaultRemotePort;
					int.TryParse(Settings.EngineSettings.RemotePort, out port);
					connected = enginePlayer.InitRemote(host, port);
				}

				if (!connected)
				{
					AppDebug.Log.Error($"initEnginePlayer: remote connection failed for {host}");
					enginePlayer.Terminate();
					enginePlayer = null;
					return false;
				}
				AppDebug.Log.Info("initEnginePlayer: remote engine connected successfully");

				string provider = Settings.EngineSettings.CloudProvider;
				if (provider == "vastai")
				{
					var config = CloudWatchdogConfig.ForVastAi(
						Settings.EngineSettings.VastAiInstanceId,
						Settings.EngineSettings.VastAiApiKey);
					if (config.IsValid())
					{
						CloudInstanceWatchdog.Instance.StartMonitoring(config);
						CloudInstanceWatchdog.Instance.SaveLastConnectionInfo(
							Settings.EngineSettings.VastAiInstanceId,
							host,
							Settings.EngineSettings.VastAiSshPort,
							Settings.EngineSettings.VastAiSshEngineCommand);
					}
				}
				else if (provider == "gcp")
				{
					var config = CloudWatchdogConfig.ForGcp(
						Settings.EngineSettings.GcpServiceAccountKeyPath,
						Settings.EngineSettings.GcpZone,
						Settings.EngineSettings.GcpInstanceName);
					if (config.IsValid())
					{
						CloudInstanceWatchdog.Instance.StartMonitoring(config);
					}
				}
			}
			else
			{
				string enginePath;
				if (InternalEngineCatalog.IsInternalEngineNo(playerNo))
				{
					InternalEnginePlayer internalEnginePlayer = new InternalEnginePlayer(PlayerColor.Black, InternalEngineCatalog.GetEngineName(playerNo));
					if (!internalEnginePlayer.CopyFiles())
					{
						AppDebug.Log.Error("initEnginePlayer: InternalEngine CopyFiles failed");
						return false;
					}
					internalEnginePlayer.LoadSettings();
					enginePlayer = internalEnginePlayer;
					enginePath = internalEnginePlayer.EnginePath;
				}
				else
				{
					string externalFile = Settings.EngineSettings.GetExternalEngineFile();
					AppDebug.Log.Info($"initEnginePlayer: external engine file={externalFile}");
					ExternalEnginePlayer externalEnginePlayer = new ExternalEnginePlayer(PlayerColor.Black, externalFile);
					if (!externalEnginePlayer.CopyFiles())
					{
						AppDebug.Log.Error("initEnginePlayer: ExternalEngine CopyFiles failed");
						return false;
					}
					externalEnginePlayer.LoadSettings();
					enginePlayer = externalEnginePlayer;
					enginePath = externalEnginePlayer.EnginePath;
				}
				AppDebug.Log.Info($"initEnginePlayer: launching engine at {enginePath}");
				enginePlayer.Initialized += Player_Initialized;
				enginePlayer.ReadyOk += Player_ReadyOk;
				enginePlayer.BestMoveReceived += Player_BestMoveReceived;
				enginePlayer.CheckMateReceived += Player_CheckMateReceived;
				enginePlayer.Stopped += Player_Stopped;
				enginePlayer.InfoReceived += Player_InfoReceived;
				enginePlayer.ReportError += Player_ReportError;
				if (!enginePlayer.Init(enginePath))
				{
					AppDebug.Log.Error($"initEnginePlayer: Init failed for {enginePath}");
					enginePlayer.Terminate();
					enginePlayer = null;
					return false;
				}
				AppDebug.Log.Info("initEnginePlayer: engine started successfully");
			}
		}
		return true;
	}

	public void MakeMove(MoveData moveData, MoveData ponder, bool engine)
	{
		int time = gameTimer.ChnageTurn();
		AnalyzeStop();
		AddMove(moveData, time);
		OnGameEvent(new GameEventArgs(GameEventId.Moved, engine));
		bool flag = false;
		if (gameMode == GameMode.Play)
		{
			history.Add(Notation.Position.HashKey, MoveCheck.IsCheck(Notation.Position));
			if (history.IsRepetitionCheck())
			{
				GameOver(new MoveData(MoveType.WinFoul));
				flag = true;
			}
			else if (history.IsRepetitionCheckOpp())
			{
				GameOver(new MoveData(MoveType.LoseFoul));
				flag = true;
			}
			else if (history.IsRepetition())
			{
				GameOver(new MoveData(MoveType.Repetition));
				flag = true;
			}
		}
		if (!flag)
		{
			if (engine && Settings.AppSettings.AnimationSpeed != AnimeSpeed.Off)
			{
				OnGameEvent(new GameEventArgs(GameEventId.Delay));
			}
			else
			{
				TakeTurn();
			}
		}
	}

	private void AddMove(MoveData moveData, int time)
	{
		MoveDataEx moveDataEx = new MoveDataEx(moveData);
		moveDataEx.Time = time;
		if (gameMode == GameMode.Play && !IsHumanPlayer(Notation.Position.Turn) && pvinfos.ContainsKey(1))
		{
			moveDataEx.CommentAdd("*" + gameText + " " + pvinfos[1].ToString());
			moveDataEx.Eval = pvinfos[1].Eval;
		}
		if (gameMode == GameMode.Play)
		{
			NotationModel.AddMove(moveDataEx, MoveAddMode.INSERT, changeChildCurrent: true);
		}
		else
		{
			NotationModel.AddMove(moveDataEx, MoveAddMode.MERGE, changeChildCurrent: true);
		}
	}

	private void GameOver(MoveData moveData)
	{
		if (gameMode == GameMode.Play)
		{
			int time = gameTimer.Stop();
			AddMove(moveData, time);
			blackPlayer.GameOver(notationModel.Notation.WinColor);
			if (whitePlayer != blackPlayer)
			{
				whitePlayer.GameOver(notationModel.Notation.WinColor);
			}
			if (Settings.AppSettings.AutoSave)
			{
				string fileName = notationModel.FileName;
				if (fileName == string.Empty)
				{
					fileName = notationModel.GetFileName();
				}
				try
				{
					notationModel.Save(Path.Combine(LocalFile.KifPath, fileName));
				}
				catch
				{
				}
			}
			GameTerminate();
			OnGameEvent(new GameEventArgs(GameEventId.GameOver));
		}
		else
		{
			AddMove(moveData, 0);
		}
	}

	public void TakeTurn()
	{
		if (gameMode != GameMode.Play)
		{
			return;
		}
		if (engineMode != EngineMode.Play)
		{
			GameRestart();
			return;
		}
		if (CurrentPlayer == enginePlayer)
		{
			comState = ComputerState.Thinking;
		}
		int num = CurrentPlayer.Go(Notation, gameTimer);
		if (num >= 0 && CurrentPlayer == enginePlayer)
		{
			pvinfos.Clear();
			OnGameEvent(new GameEventArgs(GameEventId.Info));
		}
		if (!bothcomputer && IsPonderEnabled() && CurrentPlayer != enginePlayer)
		{
			num = ((Notation.Position.Turn != PlayerColor.Black) ? blackPlayer.Ponder(Notation, gameTimer) : whitePlayer.Ponder(Notation, gameTimer));
			if (num >= 0)
			{
				pvinfos.Clear();
				OnGameEvent(new GameEventArgs(GameEventId.Info));
				comState = ComputerState.Ponder;
			}
		}
		gameTimer.TakeTurn();
		OnGameEvent(new GameEventArgs(GameEventId.TakeTurn));
	}

	public void Resign()
	{
		if (enginePlayer != null)
		{
			enginePlayer.Stop();
			comState = ComputerState.Stop;
		}
		GameOver(new MoveData(MoveType.ResultFlag));
	}

	public void MoveNow()
	{
		CurrentPlayer.MoveNow();
	}

	public void Stop(bool pause)
	{
		if (enginePlayer != null)
		{
			enginePlayer.Stop();
			comState = ComputerState.Stop;
		}
		switch (gameMode)
		{
		case GameMode.Play:
			GameOver(new MoveData(MoveType.Stop));
			break;
		case GameMode.Consider:
			if (pause)
			{
				AnalyzeStop();
			}
			else
			{
				ConsiderEnd();
			}
			break;
		case GameMode.Analyzer:
			AnalyzeEnd();
			break;
		case GameMode.Input:
			break;
		}
	}

	public void AnalyzeStop()
	{
		if ((ComState == ComputerState.Analyzing || ComState == ComputerState.Mating) && enginePlayer != null)
		{
			enginePlayer.Stop();
		}
	}

	public void Matta()
	{
		if (gameMode == GameMode.Play)
		{
			gameTimer.Stop();
			if (enginePlayer != null)
			{
				enginePlayer.Stop();
			}
			if (IsHumanPlayer(Notation.Position.Turn))
			{
				NotationModel.Matta();
			}
			else
			{
				NotationModel.InputCancel();
			}
			gameTimer.SetRestartTime(Notation.TotalTime(PlayerColor.Black), Notation.TotalTime(PlayerColor.White));
			history.Init(Notation);
			CurrentPlayer.Go(Notation, gameTimer);
			gameTimer.Start(Notation.Position.Turn);
		}
	}

	private void AnalyzeReciveBestMove(MoveData bestmove)
	{
		PvInfo pvInfo = pvinfos[1];
		if (pvInfo.HasEval)
		{
			Notation.MoveCurrent.Score = pvInfo.Eval;
		}
		MoveNode moveCurrent = Notation.MoveCurrent;
		bool flag = false;
		MoveNode moveNode;
		if (Settings.AnalyzeSettings.Reverse)
		{
			moveNode = Notation.MoveCurrent.ChildCurrent;
			flag = NotationModel.Prev();
		}
		else
		{
			flag = NotationModel.Next();
			moveNode = Notation.MoveCurrent;
		}
		if (moveNode != null && moveNode.MoveType.IsMove() && pvInfo.HasEval)
		{
			if (moveNode.Turn == PlayerColor.Black)
			{
				if (moveNode.Equals(bestmove))
				{
					moveNode.BestMove = MoveMatche.Best;
				}
				else
				{
					moveNode.BestMove = MoveMatche.None;
				}
			}
			else if (moveNode.Equals(bestmove))
			{
				moveNode.BestMove = MoveMatche.Best;
			}
			else
			{
				moveNode.BestMove = MoveMatche.None;
			}
		}
		if (pvInfo.HasEval)
		{
			AnalyzeInfo analyzeInfo = analyzeInfoList.Add(moveCurrent.Number, moveCurrent);
			analyzeInfo.ThinkInfo = pvinfos[1];
			for (int i = 1; i <= 3; i++)
			{
				if (pvinfos.ContainsKey(i))
				{
					PvInfo item = pvinfos[i];
					analyzeInfo.Items.Add(item);
				}
			}
			analyzeInfo = analyzeInfoList.GetInfo(moveCurrent.Number + 1);
			if (analyzeInfo == null)
			{
				analyzeInfo = analyzeInfoList.Add(moveCurrent.Number + 1, null);
			}
			AnalyzeInfo analyzeInfo2 = null;
			analyzeInfo2 = ((!Settings.AnalyzeSettings.Reverse) ? analyzeInfoList.GetInfo(moveCurrent.Number) : analyzeInfoList.GetInfo(moveCurrent.Number + 1));
			if (analyzeInfo2 != null)
			{
				PvInfo thinkInfo = analyzeInfo2.ThinkInfo;
				if (thinkInfo != null)
				{
					if (analyzeInfo2.MoveData.BestMove == MoveMatche.Best)
					{
						analyzeInfo2.MoveData.CommentAdd($"*{analysisText} ○ {thinkInfo.ToString(Settings.AppSettings.MoveStyle)}");
					}
					else
					{
						analyzeInfo2.MoveData.CommentAdd($"*{analysisText} {thinkInfo.ToString(Settings.AppSettings.MoveStyle)}");
					}
					for (int j = 1; j < analyzeInfo2.Items.Count; j++)
					{
						thinkInfo = analyzeInfo2.Items[j];
						analyzeInfo2.MoveData.CommentAdd($"*{analysisText} {thinkInfo.ToString(Settings.AppSettings.MoveStyle)}");
					}
				}
			}
		}
		// 逆順+現在位置まで: 目標局面に到達したら停止
		if (flag && Settings.AnalyzeSettings.Reverse && analyzeStopNumber_ >= 0
			&& Notation.MoveCurrent.Number <= analyzeStopNumber_)
		{
			flag = false;
		}

		if (flag)
		{
			pvinfos.Clear();
			hint_info.Clear();
			if (StartAnalyzeCommand(new AnalyzeTimeSettings(Settings.AnalyzeSettings.AnalyzeTime, -1L, Settings.AnalyzeSettings.GetAnalyzeDepth())))
			{
				return;
			}
		}
		analyzeStopNumber_ = -1;
		AnalyzeEnd();
		if (Settings.AnalyzeSettings.Reverse)
		{
			AnalyzeInfo info = analyzeInfoList.GetInfo(moveCurrent.Number);
			if (info != null)
			{
				PvInfo thinkInfo2 = info.ThinkInfo;
				if (thinkInfo2 != null)
				{
					if (info.MoveData.BestMove == MoveMatche.Best)
					{
						info.MoveData.CommentAdd($"*{analysisText} ○ {thinkInfo2.ToString(Settings.AppSettings.MoveStyle)}");
					}
					else
					{
						info.MoveData.CommentAdd($"*{analysisText} {thinkInfo2.ToString(Settings.AppSettings.MoveStyle)}");
					}
					for (int k = 1; k < info.Items.Count; k++)
					{
						thinkInfo2 = info.Items[k];
						info.MoveData.CommentAdd($"*{analysisText} {thinkInfo2.ToString(Settings.AppSettings.MoveStyle)}");
					}
				}
			}
		}
		analyzeInfoList.Total();
		string comment = string.Format(analysis_comment_Text, (analyzeInfoList.BlackMoveInfo.Count != 0) ? (analyzeInfoList.BlackMoveInfo.Matches * 100 / analyzeInfoList.BlackMoveInfo.Count) : 0, analyzeInfoList.BlackMoveInfo.Matches, analyzeInfoList.BlackMoveInfo.Count, (analyzeInfoList.WhiteMoveInfo.Count != 0) ? (analyzeInfoList.WhiteMoveInfo.Matches * 100 / analyzeInfoList.WhiteMoveInfo.Count) : 0, analyzeInfoList.WhiteMoveInfo.Matches, analyzeInfoList.WhiteMoveInfo.Count);
		NotationModel.AddComment(comment);
	}

	private bool IsPonderEnabled()
	{
		if (enginePlayer == null) return false;
		var options = enginePlayer.Options;
		if (options.ContainsKey("Ponder") && options["Ponder"] is USIOptionCheck ponder)
			return ponder.Value;
		if (options.ContainsKey("USI_Ponder") && options["USI_Ponder"] is USIOptionCheck usiPonder)
			return usiPonder.Value;
		return false;
	}

	private bool IsHumanPlayer(PlayerColor color)
	{
		switch (color)
		{
		case PlayerColor.Black:
			return gameParam.BlackNo == 0;
		case PlayerColor.White:
			return gameParam.WhiteNo == 0;
		}
		return false;
	}

	private void Player_BestMoveReceived(object sender, BestMoveEventArgs e)
	{
		ComputerState num = comState;
		comState = ComputerState.Stop;
		if (num == ComputerState.Analyzing)
		{
			MoveDataEx moveDataEx = new MoveDataEx(e.BestMove);
			moveDataEx.Piece = Notation.Position.GetPiece(moveDataEx.FromSquare);
			moveDataEx.Action = moveDataEx.GetAction(Notation.Position);
			if (!pvinfos.ContainsKey(1))
			{
				PvInfo pvInfo = new PvInfo(1);
				pvInfo.PvMoves = new List<MoveDataEx> { moveDataEx };
				if (moveDataEx.MoveType == MoveType.ResultFlag)
				{
					pvInfo.Mate = ((Notation.Position.Turn == PlayerColor.White) ? 1 : (-1));
				}
				pvinfos.Add(pvInfo);
				hint_info.UpdateInfo(pvinfos[1], Notation.Position, Notation.Position.MoveLast);
				OnGameEvent(new GameEventArgs(GameEventId.Info));
			}
			if (gameMode == GameMode.Analyzer)
			{
				AnalyzeReciveBestMove(moveDataEx);
			}
			else
			{
				OnGameEvent(new GameEventArgs(GameEventId.AnalyzeEnd));
			}
		}
		else if (GameMode == GameMode.Play)
		{
			if (e.BestMove.MoveType.HasFlag(MoveType.ResultFlag))
			{
				_ = e.BestMove.MoveType;
				_ = 137;
				GameOver(e.BestMove);
			}
			else
			{
				MakeMove(e.BestMove, e.Ponder, engine: true);
			}
		}
	}

	private void Player_CheckMateReceived(object sender, CheckMateEventArgs e)
	{
		if (comState == ComputerState.Mating)
		{
			comState = ComputerState.Stop;
			OnGameEvent(new GameEventArgs(GameEventId.MateEnd));
		}
	}

	private void Player_InfoReceived(object sender, InfoEventArgs e)
	{
		pvinfos.Add(e.PvInfo);
		if (comState == ComputerState.Analyzing)
		{
			hint_info.UpdateInfo(e.PvInfo, Notation.Position, Notation.Position.MoveLast);
		}
		OnGameEvent(new GameEventArgs(GameEventId.Info));
	}

	private void Player_ReportError(object sender, ReportErrorEventArgs e)
	{
		HandleEngineFailure(e.ErrorId);
	}

	private void Player_Stopped(object sender, StopEventArgs e)
	{
		if (comState == ComputerState.Analyzing)
		{
			comState = ComputerState.Stop;
			OnGameEvent(new GameEventArgs(GameEventId.AnalyzeEnd));
		}
	}

	/// <summary>
	/// usiok受信後、オプション送信前に呼ばれる。
	/// クラウドインスタンスのスペックに基づく自動オプションを tempOptions_ にセットする。
	/// </summary>
	private void Player_OptionsApplying(object sender, InitializedEventArgs e)
	{
		if (!cancel)
		{
			ApplyVastAiAutoOptions();
		}
	}

	private void Player_Initialized(object sender, InitializedEventArgs e)
	{
		if (!cancel)
		{
			if (engineMode == EngineMode.None)
			{
				busy = false;
				OnGameEvent(new GameEventArgs(GameEventId.InitializeEnd));
			}
			else
			{
				enginePlayer.Ready();
			}
		}
	}

	/// <summary>
	/// クラウドインスタンスのスペックに基づいてThreads/Hash等を自動設定する。
	/// OptionsApplying イベントから呼ばれ、SetTempOptionDeferred で tempOptions_ にセットする。
	/// update_options() でユーザー保存オプションの後に適用されるため、確実に上書きされる。
	/// </summary>
	private void ApplyVastAiAutoOptions()
	{
		if (Settings.EngineSettings.EngineNo != RemoteEnginePlayer.RemoteEngineNo) return;

		int cores = Settings.EngineSettings.VastAiCpuCores;
		int ramMb = Settings.EngineSettings.VastAiRamMb;
		int gpuRamMb = Settings.EngineSettings.VastAiGpuRamMb;
		if (cores <= 0 && ramMb <= 0) return;

		// USIオプションにDL系の設定があればDEEPエンジンと判定
		var opts = enginePlayer.Options;
		bool isDeep = opts.ContainsKey("UCT_NodeLimit") ||
					  opts.ContainsKey("DNN_Batch_Size") ||
					  opts.ContainsKey("GPU_Id");

		if (isDeep)
		{
			AppDebug.Log.Info($"VastAi auto-option: DEEPエンジン検出 (cores={cores}, RAM={ramMb}MB, VRAM={gpuRamMb}MB)");

			if (cores > 0)
			{
				enginePlayer.SetTempOptionDeferred("Threads", cores);
				AppDebug.Log.Info($"VastAi auto-option: Threads={cores}");
			}

			// Hash: DEEPでは1024MBで十分
			enginePlayer.SetTempOptionDeferred("USI_Hash", 1024);
			enginePlayer.SetTempOptionDeferred("Hash", 1024);
			AppDebug.Log.Info("VastAi auto-option: Hash=1024MB (DEEP)");

			// UCT_NodeLimit: MCTSツリーのノード上限
			if (ramMb > 0 && opts.ContainsKey("UCT_NodeLimit"))
			{
				long availableBytes = (long)(ramMb - 1024) * 1024L * 1024L;
				if (availableBytes < 0) availableBytes = (long)ramMb * 512L * 1024L;
				long nodeLimit = availableBytes / 200;
				int nodeLimitInt = (int)System.Math.Min(nodeLimit, 50000000L);
				enginePlayer.SetTempOptionDeferred("UCT_NodeLimit", nodeLimitInt);
				AppDebug.Log.Info($"VastAi auto-option: UCT_NodeLimit={nodeLimitInt}");
			}
		}
		else
		{
			AppDebug.Log.Info($"VastAi auto-option: NNUEエンジン検出 (cores={cores}, RAM={ramMb}MB)");

			if (cores > 0)
			{
				enginePlayer.SetTempOptionDeferred("Threads", cores);
				AppDebug.Log.Info($"VastAi auto-option: Threads={cores}");
			}

			if (cores > 0)
			{
				// 4コアにつき1024MB、上限65536MB（64GB）
				int hashMb = System.Math.Min(cores / 4 * 1024, 65536);
				enginePlayer.SetTempOptionDeferred("USI_Hash", hashMb);
				enginePlayer.SetTempOptionDeferred("Hash", hashMb);
				AppDebug.Log.Info($"VastAi auto-option: Hash={hashMb}MB");
			}
		}
	}

	private void Player_ReadyOk(object sender, ReadyOkEventArgs e)
	{
		busy = false;
		OnGameEvent(new GameEventArgs(GameEventId.InitializeEnd));
		if (engineMode == EngineMode.Hint)
		{
			enginePlayer.GameStart();
			StartAnalyzeCommand(new AnalyzeTimeSettings(Settings.AnalyzeSettings.Time, -1L, -1L));
		}
		else if (engineMode == EngineMode.Mate)
		{
			enginePlayer.GameStart();
			StartMateCommand(1000);
		}
		else if (engineMode == EngineMode.Analyze)
		{
			enginePlayer.GameStart();
			StartAnalyzeCommand(new AnalyzeTimeSettings(Settings.AnalyzeSettings.AnalyzeTime, -1L, Settings.AnalyzeSettings.GetAnalyzeDepth()));
		}
		else if (engineMode == EngineMode.Play)
		{
			if (CurrentPlayer == enginePlayer)
			{
				comState = ComputerState.Thinking;
			}
			blackPlayer.GameStart();
			whitePlayer.GameStart();
			CurrentPlayer.Go(Notation, gameTimer);
			gameTimer.Start(Notation.Position.Turn);
			OnGameEvent(new GameEventArgs(GameEventId.GameStart));
		}
	}

	private void GameTimer_Timeout(object sender, EventArgs e)
	{
		syncContext.Post(delegate
		{
		}, null);
	}

	private bool StartAnalyzeCommand(AnalyzeTimeSettings settings)
	{
		if (enginePlayer == null)
		{
			return false;
		}

		int transactionNo = enginePlayer.Analyze(Notation, settings);
		if (transactionNo < 0)
		{
			HandleEngineFailure(PlayerErrorId.EngineDisconnected);
			return false;
		}

		comState = ComputerState.Analyzing;
		OnGameEvent(new GameEventArgs(GameEventId.AnalyzeStart));
		return true;
	}

	private bool StartMateCommand(int timeMs)
	{
		if (enginePlayer == null)
		{
			return false;
		}

		int transactionNo = enginePlayer.Mate(Notation, timeMs);
		if (transactionNo < 0)
		{
			HandleEngineFailure(PlayerErrorId.EngineDisconnected);
			return false;
		}

		comState = ComputerState.Mating;
		OnGameEvent(new GameEventArgs(GameEventId.MateStart));
		return true;
	}

	private void HandleEngineFailure(PlayerErrorId errorId)
	{
		AppDebug.Log.Error($"Game: engine failure detected ({errorId})");

		bool wasPlayMode = gameMode == GameMode.Play;
		bool shouldReturnToInput = gameMode == GameMode.Analyzer || gameMode == GameMode.Consider;

		EngineTerminate();

		if (shouldReturnToInput)
		{
			gameMode = GameMode.Input;
		}

		if (wasPlayMode)
		{
			GameTerminate();
		}

		OnGameEvent(new GameEventArgs(GameEventId.InitializeError));
	}

	private void GameTimer_UpdateTime(object sender, EventArgs e)
	{
		syncContext.Post(delegate
		{
			gameTimer.UpdateRemain();
			gameTimer.StartUpdateTimer();
			OnGameEvent(new GameEventArgs(GameEventId.UpdateTime));
		}, null);
	}

	private void ThreatmateAnalyzer_Updated(object sender, EventArgs e)
	{
		OnGameEvent(new GameEventArgs(GameEventId.ThreatmateUpdated));
	}

	private void PolicyAnalyzer_Updated(object sender, EventArgs e)
	{
		OnGameEvent(new GameEventArgs(GameEventId.PolicyUpdated));
	}

	private void RefreshThreatmateAnalysis()
	{
		if (!Settings.AppSettings.AutoThreatmateAnalysis || Notation == null || Notation.MoveCurrent.MoveType.IsResult())
		{
			threatmateAnalyzer.Clear();
			return;
		}
		threatmateAnalyzer.Analyze(Notation);
	}

	public void UpdateThreatmateAnalysis()
	{
		RefreshThreatmateAnalysis();
	}

	private void RefreshPolicyAnalysis()
	{
		if (!Settings.AppSettings.AutoPolicyAnalysis || Notation == null || Notation.MoveCurrent.MoveType.IsResult())
		{
			policyAnalyzer.Clear();
			return;
		}
		policyAnalyzer.Analyze(Notation);
	}

	private void NotationModel_NotationChanged(object sender, NotationEventArgs e)
	{
		if (e.EventId != NotationEventId.COMMENT)
		{
			if (gameMode == GameMode.Consider && !busy)
			{
				ConsiderStart();
			}
			else if (comState == ComputerState.Analyzing || comState == ComputerState.Mating)
			{
				AnalyzeStop();
			}
			RefreshThreatmateAnalysis();
			RefreshPolicyAnalysis();
		}
	}
}
