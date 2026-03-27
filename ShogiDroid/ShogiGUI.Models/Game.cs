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

	public HintInfo HintInfo => hint_info;

	public GameRemainTime BlackTime => gameTimer.BlackRemainTime;

	public GameRemainTime WhiteTime => gameTimer.WhiteRemainTime;

	public bool BothComputer => bothcomputer;

	public EnginePlayer EnginePlayer => enginePlayer;

	public ThreatmateInfo ThreatmateInfo => threatmateAnalyzer.CurrentInfo;

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
	}

	protected virtual void OnGameEvent(GameEventArgs e)
	{
		// Notify watchdog of engine activity for auto-suspend
		VastAiWatchdog.Instance.OnGameEvent(e.EventId);

		if (this.GameEventHandler != null)
		{
			this.GameEventHandler(this, e);
		}
	}

	public void Destory()
	{
		threatmateAnalyzer.Dispose();
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
			comState = ComputerState.Analyzing;
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
		if (engineMode != EngineMode.Hint)
		{
			comState = ComputerState.Analyzing;
			engineMode = EngineMode.Hint;
			enginePlayer.Ready();
			busy = true;
		}
		else
		{
			comState = ComputerState.Analyzing;
			engineMode = EngineMode.Hint;
			enginePlayer.Analyze(Notation, new AnalyzeTimeSettings(Settings.AnalyzeSettings.Time, -1L, -1L));
			OnGameEvent(new GameEventArgs(GameEventId.AnalyzeStart));
		}
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
			comState = ComputerState.Mating;
			engineMode = EngineMode.Mate;
			OnGameEvent(new GameEventArgs(GameEventId.InitializeStart));
			return;
		}
		if (comState != ComputerState.Stop)
		{
			enginePlayer.Stop();
		}
		comState = ComputerState.Mating;
		if (engineMode != EngineMode.Mate)
		{
			engineMode = EngineMode.Mate;
			enginePlayer.Ready();
			busy = true;
		}
		else
		{
			enginePlayer.Mate(Notation, 1000);
			OnGameEvent(new GameEventArgs(GameEventId.MateStart));
		}
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
			comState = ComputerState.Analyzing;
			engineMode = EngineMode.Analyze;
			gameMode = GameMode.Analyzer;
			OnGameEvent(new GameEventArgs(GameEventId.InitializeStart));
		}
		else
		{
			busy = true;
			comState = ComputerState.Analyzing;
			engineMode = EngineMode.Analyze;
			gameMode = GameMode.Analyzer;
			enginePlayer.Ready();
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
		string host = Settings.EngineSettings.RemoteHost;
		int sshPort = Settings.EngineSettings.VastAiSshPort;
		string keyPath = Settings.EngineSettings.VastAiSshKeyPath;
		string engineCmd = Settings.EngineSettings.VastAiSshEngineCommand;

		if (string.IsNullOrEmpty(host) || sshPort <= 0 || string.IsNullOrEmpty(keyPath) || string.IsNullOrEmpty(engineCmd))
			throw new InvalidOperationException("SSH接続設定が不完全です");

		int threadsPerWorker = Settings.AnalyzeSettings.ParallelThreadsPerWorker;
		int hashPerWorker = Settings.AnalyzeSettings.ParallelHashPerWorker;

		// 保存済みエンジンオプションを読み込む（FV_SCALE等）
		var extraSetOptions = new List<string>();
		string settingsFile = System.IO.Path.Combine(EngineFile.EngineFolder, "remote_engine", "remote_engine.xml");
		if (System.IO.File.Exists(settingsFile))
		{
			var savedOptions = EngineOptions.Load(settingsFile);
			foreach (var opt in savedOptions.OptionList)
			{
				string key = opt.Key;
				// Threads/Hashは並列用の値で上書きするのでスキップ
				if (key == "Threads" || key == "USI_Hash" || key == "Hash" || key == "MultiPV")
					continue;
				extraSetOptions.Add($"setoption name {key} value {opt.Value}");
			}
		}
		// 並列解析は各局面の最善手評価だけを使うため、MultiPVは必ず1に固定する。
		extraSetOptions.Add("setoption name MultiPV value 1");

		var analyzer = new ParallelAnalyzer();
		analyzer.Progress += (msg) => ParallelAnalyzeProgress?.Invoke(msg);

		gameMode = GameMode.Analyzer;
		busy = true;

		try
		{
			var results = await analyzer.ExecuteAsync(host, sshPort, keyPath, engineCmd, Notation,
				workers, nodesPerMove, threadsPerWorker, hashPerWorker, extraSetOptions, ct);
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
		string analysisText = Android.App.Application.Context.GetString(Resource.String.Analysis_Text);
		var moveStyle = Settings.AppSettings.MoveStyle;

		// 各手の局面を再構築するために初期局面からリプレイ
		SPosition replayPos = (SPosition)Notation.InitialPosition.Clone();

		int moveIndex = 0;
		foreach (MoveNode moveNode in Notation.MoveNodes)
		{
			if (!moveNode.MoveType.IsMove()) continue;

			// 指し手を適用して局面を進める
			replayPos.Move(moveNode);
			moveIndex++;

			var result = results.Find(r => r.Index == moveIndex);
			if (result == null) continue;

			// 評価値をセット（先手目線に統一）
			// エンジンは手番側視点で報告。moveNode.Turnは指した側なので、
			// 指した後は相手の手番。先手が指した後→後手視点→反転が必要
			if (result.Score.HasValue)
			{
				int score = result.Score.Value;
				if (moveNode.Turn == PlayerColor.Black)
					score = -score;
				moveNode.Score = score;
			}
			else if (result.Mate.HasValue)
			{
				int mate = result.Mate.Value;
				int mateScore = (mate > 0 ? 1 : -1) * (32000 - System.Math.Abs(mate));
				if (moveNode.Turn == PlayerColor.Black)
					mateScore = -mateScore;
				moveNode.Score = mateScore;
			}

			// 最善手判定
			if (result.BestMove == result.MoveUsi)
				moveNode.BestMove = MoveMatche.Best;
			else
				moveNode.BestMove = MoveMatche.None;

			// 先手目線の評価値文字列（コメント用）
			int senteScore = moveNode.HasScore ? moveNode.Score : 0;
			string evalStr = result.Mate.HasValue
				? PvInfo.ValueToString(senteScore > 0 ? 1 : -1, System.Math.Abs(result.Mate.Value), 0)
				: senteScore.ToString();

			string depthStr = result.Depth.HasValue
				? $"{result.Depth.Value}" + (result.SelDepth.HasValue ? $"/{result.SelDepth.Value}" : "")
				: "";
			string nodesStr = result.Nodes.HasValue ? PvInfo.NodesToString(result.Nodes.Value) : "";

			// 読み筋をUSI→KIF形式に変換
			string pvKif = ConvertPvToKif(replayPos, result.PvString, moveStyle);

			string bestMark = (moveNode.BestMove == MoveMatche.Best) ? " ○" : "";
			string comment = $"*{analysisText}{bestMark} 評価値 {evalStr}";
			if (!string.IsNullOrEmpty(depthStr)) comment += $" 深さ {depthStr}";
			if (!string.IsNullOrEmpty(nodesStr)) comment += $" ノード数 {nodesStr}";
			if (!string.IsNullOrEmpty(pvKif)) comment += $" 読み筋 {pvKif}";

			moveNode.CommentAdd(comment);
		}

		// 棋譜変更を通知
		NotationModel.OnNotationChangedPublic(new ShogiGUI.Events.NotationEventArgs(ShogiGUI.Events.NotationEventId.OBJECT_CHANGED));
	}

	/// <summary>
	/// USI形式の読み筋をKIF形式に変換
	/// </summary>
	private string ConvertPvToKif(SPosition basePos, string pvString, MoveStyle style)
	{
		if (string.IsNullOrEmpty(pvString)) return string.Empty;

		var pos = (SPosition)basePos.Clone();
		string[] usiMoves = pvString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var sb = new System.Text.StringBuilder();

		foreach (string usiMove in usiMoves)
		{
			MoveDataEx moveData = Sfen.ParseMove(pos, usiMove);
			if (moveData.MoveType == MoveType.NoMove || !MoveCheck.IsValid(pos, moveData))
				break;

			string turnMark = (pos.Turn == PlayerColor.Black) ? "▲" : "△";
			sb.Append($" {turnMark}{moveData.ToString(style)}");
			pos.Move(moveData);
		}

		return sb.ToString().TrimStart();
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
		if (comState.IsThinking())
		{
			enginePlayer.Stop();
		}
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
			comState = ComputerState.Analyzing;
			engineMode = EngineMode.Hint;
			gameMode = GameMode.Consider;
			OnGameEvent(new GameEventArgs(GameEventId.InitializeStart));
			return;
		}
		gameMode = GameMode.Consider;
		if (engineMode != EngineMode.Hint)
		{
			busy = true;
			comState = ComputerState.Analyzing;
			engineMode = EngineMode.Hint;
			enginePlayer.Ready();
		}
		else
		{
			comState = ComputerState.Analyzing;
			engineMode = EngineMode.Hint;
			enginePlayer.Analyze(Notation, new AnalyzeTimeSettings(Settings.AnalyzeSettings.Time, -1L, -1L));
			OnGameEvent(new GameEventArgs(GameEventId.AnalyzeStart));
		}
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
					int port = 28597;
					int.TryParse(Settings.EngineSettings.RemotePort, out port);
					AppDebug.Log.Info($"initEnginePlayer: TCP mode port={port}");
					remoteEnginePlayer = new RemoteEnginePlayer(PlayerColor.Black, host, port);
				}

				remoteEnginePlayer.CopyFiles();
				remoteEnginePlayer.LoadSettings();
				enginePlayer = remoteEnginePlayer;
				enginePlayer.Initialized += Player_Initialized;
				enginePlayer.ReadyOk += Player_ReadyOk;
				enginePlayer.BestMoveRecieved += Player_BestMoveRecieved;
				enginePlayer.CheckMateRecieved += Player_CheckMateRecieved;
				enginePlayer.Stopped += Player_Stopped;
				enginePlayer.InfoRecieved += Player_InfoRecieved;
				enginePlayer.ReportError += Player_ReportError;

				bool connected;
				if (useSsh)
				{
					connected = remoteEnginePlayer.InitSsh();
				}
				else
				{
					int port = 28597;
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
			}
			else
			{
				string enginePath;
				if (playerNo == 1)
				{
					InternalEnginePlayer internalEnginePlayer = new InternalEnginePlayer(PlayerColor.Black);
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
				enginePlayer.BestMoveRecieved += Player_BestMoveRecieved;
				enginePlayer.CheckMateRecieved += Player_CheckMateRecieved;
				enginePlayer.Stopped += Player_Stopped;
				enginePlayer.InfoRecieved += Player_InfoRecieved;
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
			comState = ComputerState.Analyzing;
			enginePlayer.Analyze(Notation, new AnalyzeTimeSettings(Settings.AnalyzeSettings.AnalyzeTime, -1L, Settings.AnalyzeSettings.GetAnalyzeDepth()));
			return;
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
		bool result = false;
		switch (color)
		{
		case PlayerColor.Black:
			result = gameParam.BlackNo == 0;
			break;
		case PlayerColor.White:
			result = gameParam.WhiteNo == 0;
			break;
		}
		return result;
	}

	private void Player_BestMoveRecieved(object sender, BestMoveEventArgs e)
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

	private void Player_CheckMateRecieved(object sender, CheckMateEventArgs e)
	{
		if (comState == ComputerState.Mating)
		{
			comState = ComputerState.Stop;
			OnGameEvent(new GameEventArgs(GameEventId.MateEnd));
		}
	}

	private void Player_InfoRecieved(object sender, InfoEventArgs e)
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
		if (e.ErrorId == PlayerErrorId.InitializeTimeout)
		{
			EngineTerminate();
			GameTerminate();
			OnGameEvent(new GameEventArgs(GameEventId.InitializeError));
		}
	}

	private void Player_Stopped(object sender, StopEventArgs e)
	{
		if (comState == ComputerState.Analyzing)
		{
			comState = ComputerState.Stop;
			OnGameEvent(new GameEventArgs(GameEventId.AnalyzeEnd));
		}
	}

	private void Player_Initialized(object sender, InitializedEventArgs e)
	{
		if (!cancel)
		{
			// vast.aiインスタンスのスペックに基づいてThreads/Hashを自動設定
			ApplyVastAiAutoOptions();

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
				enginePlayer.SetOption("Threads", cores, temp: true);
				AppDebug.Log.Info($"VastAi auto-option: Threads={cores}");
			}

			// Hash: DEEPでは1024MBで十分
			enginePlayer.SetOption("USI_Hash", 1024, temp: true);
			enginePlayer.SetOption("Hash", 1024, temp: true);
			AppDebug.Log.Info("VastAi auto-option: Hash=1024MB (DEEP)");

			// UCT_NodeLimit: MCTSツリーのノード上限
			if (ramMb > 0 && opts.ContainsKey("UCT_NodeLimit"))
			{
				long availableBytes = (long)(ramMb - 1024) * 1024L * 1024L;
				if (availableBytes < 0) availableBytes = (long)ramMb * 512L * 1024L;
				long nodeLimit = availableBytes / 200;
				int nodeLimitInt = (int)System.Math.Min(nodeLimit, 50000000L);
				enginePlayer.SetOption("UCT_NodeLimit", nodeLimitInt, temp: true);
				AppDebug.Log.Info($"VastAi auto-option: UCT_NodeLimit={nodeLimitInt}");
			}
		}
		else
		{
			AppDebug.Log.Info($"VastAi auto-option: NNUEエンジン検出 (cores={cores}, RAM={ramMb}MB)");

			if (cores > 0)
			{
				enginePlayer.SetOption("Threads", cores, temp: true);
				AppDebug.Log.Info($"VastAi auto-option: Threads={cores}");
			}

			if (ramMb > 0)
			{
				// RAMの70%、上限32768MB
				int hashMb = System.Math.Min((int)(ramMb * 0.7), 32768);
				enginePlayer.SetOption("USI_Hash", hashMb, temp: true);
				enginePlayer.SetOption("Hash", hashMb, temp: true);
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
			comState = ComputerState.Analyzing;
			enginePlayer.Analyze(Notation, new AnalyzeTimeSettings(Settings.AnalyzeSettings.Time, -1L, -1L));
			OnGameEvent(new GameEventArgs(GameEventId.AnalyzeStart));
		}
		else if (engineMode == EngineMode.Mate)
		{
			comState = ComputerState.Mating;
			enginePlayer.GameStart();
			enginePlayer.Mate(Notation, 1000);
			OnGameEvent(new GameEventArgs(GameEventId.MateStart));
		}
		else if (engineMode == EngineMode.Analyze)
		{
			enginePlayer.GameStart();
			comState = ComputerState.Analyzing;
			enginePlayer.Analyze(Notation, new AnalyzeTimeSettings(Settings.AnalyzeSettings.AnalyzeTime, -1L, Settings.AnalyzeSettings.GetAnalyzeDepth()));
			OnGameEvent(new GameEventArgs(GameEventId.AnalyzeStart));
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
		}
	}
}
