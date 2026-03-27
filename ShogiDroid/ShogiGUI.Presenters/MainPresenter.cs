using System;
using System.Collections.Generic;
using System.IO;
using ShogiGUI.Engine;
using ShogiGUI.Events;
using ShogiGUI.Models;
using ShogiLib;

namespace ShogiGUI.Presenters;

public class MainPresenter : PresenterBase<IMainView>
{
	private string notationUrl = string.Empty;

	private bool reverse;

	private bool gameStartPopup;

	private UITimer timer = new UITimer();

	private bool autoplay;

	public bool Busy => Domain.Game.Busy;

	public GameMode GameMode => Domain.Game.GameMode;

	public ComputerState ComState => Domain.Game.ComState;

	public bool IsEngineModePlay => Domain.Game.IsEngineModePlay;

	public string NotationFileName => Domain.Game.NotationModel.FileName;

	public string NotationNewFileName => Domain.Game.NotationModel.GetFileName();

	public bool NotationModified => Domain.Game.NotationModel.State != NotationModel.ChangeState.Loaded;

	public string WEBNotationUrl => notationUrl;

	public SNotation Notation => Domain.Game.NotationModel.Notation;

	public bool Reverse
	{
		get
		{
			return reverse;
		}
		set
		{
			reverse = value;
			Settings.AppSettings.Reverse = value;
		}
	}

	public HintInfo HintInfo => Domain.Game.HintInfo;

	public ThreatmateInfo ThreatmateInfo => Domain.Game.ThreatmateInfo;

	public bool BothComputer => Domain.Game.BothComputer;

	public int BlackTime => Domain.Game.BlackTime.TotalElapsedTime;

	public int WhiteTime => Domain.Game.WhiteTime.TotalElapsedTime;

	public GameRemainTime BlackRemainTime => Domain.Game.BlackTime;

	public GameRemainTime WhiteRemainTime => Domain.Game.WhiteTime;

	public bool AutoPlay => autoplay;

	public MainPresenter(IMainView view)
		: base(view)
	{
	}

	public override void Initialize()
	{
		Domain.Init();
		Domain.Game.GameEventHandler += Game_GameEventHandler;
		Domain.Game.NotationModel.NotationChanged += NotationModel_NotationChanged;
		reverse = Settings.AppSettings.Reverse;
		UITimer uITimer = timer;
		uITimer.Tick = (EventHandler<EventArgs>)Delegate.Combine(uITimer.Tick, new EventHandler<EventArgs>(AutoPlayTick));
	}

	public override void Resume()
	{
	}

	public override void Pause()
	{
		Domain.Game.Stop(pause: true);
		if (Domain.Game.Notation.Count != 0)
		{
			Settings.AppSettings.FileName = Domain.Game.NotationModel.FileName;
			Domain.Game.NotationModel.SaveTemp();
		}
		AutoPlayStop();
		Settings.Save();
	}

	public override void Destory()
	{
		Domain.Game.Destory();
		Settings.AppSettings.FileName = Domain.Game.NotationModel.FileName;
		Domain.Game.NotationModel.SaveTemp();
		AutoPlayStop();
		timer.Dispose();
		Settings.Save();
	}

	public void UpdateSettings()
	{
		Domain.Game.UpdateThreatmateAnalysis();
	}

	public void MakeMove(MoveData moveData)
	{
		if (!Domain.Game.Busy && CanMakeMove())
		{
			Domain.Game.NotationModel.Continue(flag: false);
			Domain.Game.MakeMove(moveData, null, engine: false);
		}
	}

	public bool CanMakeMove()
	{
		bool result = true;
		if (GameMode == GameMode.Analyzer)
		{
			result = false;
		}
		return result;
	}

	public void GameStart(bool continued)
	{
		if (Domain.Game.Busy || !CanGameStart())
		{
			return;
		}
		GameParam gameParam = new GameParam();
		gameParam.BlackNo = Settings.AppSettings.BlackNo;
		gameParam.WhiteNo = Settings.AppSettings.WhiteNo;
		gameParam.BlackName = GetPlayerName(Settings.AppSettings.BlackNo);
		gameParam.WhiteName = GetPlayerName(Settings.AppSettings.WhiteNo);
		gameParam.Time = Settings.EngineSettings.Time;
		gameParam.Countdown = Settings.EngineSettings.Countdown;
		gameParam.Increment = Settings.EngineSettings.Increment;
		if (continued)
		{
			gameParam.StartPosition = GameStartPosition.NowPosition;
			gameParam.StartMode = GameStartMode.Continued;
		}
		else
		{
			gameParam.StartPosition = Settings.AppSettings.StartPosition;
			gameParam.StartMode = Settings.AppSettings.StartMode;
		}
		gameParam.Handicap = Settings.AppSettings.Handicap;
		gameStartPopup = true;
		Domain.Game.GameStart(gameParam);
		view.UpdateState();
		view.SetPlayer(Settings.AppSettings.BlackNo == 0, Settings.AppSettings.WhiteNo == 0);
		if (gameParam.BlackNo != 0 && gameParam.WhiteNo == 0)
		{
			if (!reverse)
			{
				reverse = true;
				view.UpdateReverse();
			}
		}
		else if (gameParam.BlackNo == 0 && gameParam.WhiteNo != 0 && reverse)
		{
			reverse = false;
			view.UpdateReverse();
		}
	}

	public bool CanGameStart()
	{
		bool result = true;
		if (GameMode != GameMode.Input || Domain.Game.Busy)
		{
			result = false;
		}
		return result;
	}

	private string GetPlayerName(int playerNo)
	{
		if (playerNo == 0)
		{
			return Settings.AppSettings.PlayerName;
		}
		if (Settings.EngineSettings.EngineNo == 1)
		{
			return InternalEnginePlayer.EngineBaseName;
		}
		return Settings.EngineSettings.EngineName;
	}

	public void TakeTurn()
	{
		Domain.Game.TakeTurn();
	}

	public void MoveNow()
	{
		if (!Domain.Game.Busy)
		{
			Domain.Game.MoveNow();
		}
	}

	public void Resign()
	{
		if (!Domain.Game.Busy && CanMakeMove())
		{
			Domain.Game.NotationModel.Continue(flag: false);
			Domain.Game.Resign();
			view.UpdateState();
		}
	}

	public void Stop()
	{
		Domain.Game.Stop(pause: false);
		view.UpdateState();
	}

	public void Pass()
	{
		if (!Domain.Game.Busy || CanPass())
		{
			Domain.Game.NotationModel.Continue(flag: false);
			Domain.Game.MakeMove(new MoveData(MoveType.Pass), null, engine: false);
		}
	}

	public bool CanPass()
	{
		bool result = true;
		if (GameMode == GameMode.Play || GameMode == GameMode.Analyzer)
		{
			result = false;
		}
		return result;
	}

	public void InputCancel()
	{
		if (!Domain.Game.Busy)
		{
			if (Domain.Game.GameMode == GameMode.Play)
			{
				Domain.Game.Matta();
			}
			else
			{
				Domain.Game.NotationModel.InputCancel();
			}
		}
	}

	public bool CanInputCancel()
	{
		if (GameMode == GameMode.Analyzer)
		{
			return false;
		}
		return true;
	}

	public void Prev()
	{
		if (!Busy && CanNext())
		{
			if (Domain.Game.GameMode == GameMode.Play)
			{
				InputCancel();
			}
			else
			{
				CaptureConsiderEval();
				Domain.Game.NotationModel.Prev();
			}
		}
	}

	public void Next()
	{
		if (!Busy && CanNext())
		{
			if (Domain.Game.GameMode == GameMode.Play)
			{
				MoveNow();
			}
			else
			{
				CaptureConsiderEval();
				Domain.Game.NotationModel.Next();
			}
		}
	}

	/// <summary>
	/// 検討モード中に手を進める前に、現在の解析結果をMoveNodeに保存する。
	/// これにより評価値グラフにリアルタイムで反映される。
	/// </summary>
	private void CaptureConsiderEval()
	{
		if (Domain.Game.GameMode != GameMode.Consider) return;
		var pvinfos = Domain.Game.PvInfos;
		if (pvinfos == null || !pvinfos.ContainsKey(1)) return;

		var best = pvinfos[1];
		var current = Domain.Game.Notation.MoveCurrent;
		if (current == null || current.Number == 0) return;

		if (best.HasScore)
		{
			current.Score = best.Eval;
			current.Eval = best.Eval;
		}
	}

	public bool CanNext()
	{
		bool result = true;
		if (GameMode == GameMode.Analyzer)
		{
			result = false;
		}
		return result;
	}

	public void Jump(int number)
	{
		if (!Busy && CanMove())
		{
			Domain.Game.NotationModel.Jump(number);
		}
	}

	public void First()
	{
		if (!Busy && CanMove())
		{
			Domain.Game.NotationModel.First();
		}
	}

	public void Last()
	{
		if (!Busy && CanMove())
		{
			Domain.Game.NotationModel.Last();
		}
	}

	public bool CanMove()
	{
		bool result = true;
		if (GameMode == GameMode.Play || GameMode == GameMode.Analyzer)
		{
			result = false;
		}
		return result;
	}

	public void Hint()
	{
		if (GameMode == GameMode.Analyzer)
		{
			Domain.Game.Stop(pause: false);
		}
		else if (!Busy && ComState != ComputerState.Thinking)
		{
			Domain.Game.Hint();
		}
	}

	public void Mate()
	{
		if (!Busy)
		{
			Domain.Game.Mate();
		}
	}

	public void CreateFolders()
	{
		LocalFile.CreateFolders();
	}

	public void SaveNotation(string folder, string filename)
	{
		try
		{
			Domain.Game.NotationModel.Save(Path.Combine(folder, filename));
		}
		catch (Exception ex)
		{
			view.MessageError(ex.Message);
		}
	}

	public void InitNotation()
	{
		if (!Busy && CanLoadNotaton())
		{
			Domain.Game.NotationModel.Initialize();
		}
	}

	public bool LoadNotation(string filename)
	{
		if (Busy || !CanLoadNotaton())
		{
			return false;
		}
		bool result = false;
		try
		{
			Domain.Game.NotationModel.Load(filename);
			result = true;
		}
		catch (Exception ex)
		{
			view.MessageError(ex.Message);
		}
		return result;
	}

	public Dictionary<string, List<BookMove>> ParseBookFile(string filename)
	{
		if (Busy || !CanLoadNotaton())
		{
			return null;
		}
		try
		{
			return Domain.Game.NotationModel.ParseBookFile(filename);
		}
		catch (Exception ex)
		{
			view.MessageError(ex.Message);
			return null;
		}
	}

	public void StartBookBrowse(Dictionary<HashKey, List<BookMove>> hashBook)
	{
		Domain.Game.NotationModel.StartBookBrowse(hashBook);
	}


	public bool CanLoadNotaton()
	{
		bool result = true;
		if (GameMode == GameMode.Play || GameMode == GameMode.Analyzer)
		{
			result = false;
		}
		return result;
	}

	public bool LoadTempNotation()
	{
		bool result = false;
		try
		{
			Domain.Game.NotationModel.LoadTemp(Settings.AppSettings.FileName);
			result = true;
		}
		catch (Exception ex)
		{
			view.MessageError(ex.Message);
		}
		return result;
	}

	public string NotaitonToString()
	{
		return new Kifu().ToString(Domain.Game.Notation);
	}

	public void PasteNotation(string str)
	{
		try
		{
			Domain.Game.NotationModel.LoadFromString(str);
		}
		catch (NotationException ex)
		{
			view.MessageError(ex.Message);
		}
		catch (Exception ex2)
		{
			view.MessageError(ex2.Message);
		}
	}

	public bool CanPaste()
	{
		bool result = true;
		if (Domain.Game.Busy || GameMode == GameMode.Play || GameMode == GameMode.Analyzer)
		{
			result = false;
		}
		return result;
	}

	public bool LoadNotationFromWeb(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return false;
		}
		bool result = false;
		try
		{
			string str = WebKifuFile.LoadKifu(url);
			Domain.Game.NotationModel.LoadFromString(str);
			notationUrl = url;
			result = true;
		}
		catch (Exception ex)
		{
			view.MessageError(ex.Message);
		}
		return result;
	}

	public bool LoadNotationFromString(string subject, string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		string fileName = string.Empty;
		if (string.IsNullOrEmpty(subject))
		{
			fileName = subject.ReplaceInvalidFileNameChars();
		}
		bool result = false;
		try
		{
			Domain.Game.NotationModel.LoadFromString(text);
			Domain.Game.NotationModel.FileName = fileName;
			result = true;
		}
		catch (Exception ex)
		{
			view.MessageError(ex.Message);
		}
		return result;
	}

	public void ChangeBranch(int number, int child)
	{
		Domain.Game.NotationModel.ChangeBranch(number, child);
	}

	public void NextChild(int child)
	{
		Domain.Game.NotationModel.NextChild(child);
	}

	public SNotation LoadPv(int pvnum, PVDispMode dispMode)
	{
		PvInfo pvInfo = Domain.Game.PvInfos.GetPvInfo(pvnum, dispMode);
		if (pvInfo == null)
		{
			return null;
		}
		if (pvInfo.PvMoves.Count == 0)
		{
			return null;
		}
		SPosition sPosition = (SPosition)Domain.Game.Notation.Position.Clone();
		if (Domain.Game.Notation.Position.Turn != pvInfo.PvMoves[0].Turn)
		{
			sPosition.UnMove(Domain.Game.Notation.MoveCurrent, Domain.Game.Notation.MovePrev);
		}
		SNotation sNotation = new SNotation();
		NotationModel.SetMoves(sNotation, sPosition, null, pvInfo.PvMoves);
		return sNotation;
	}

	public SNotation LoadMoves(string moves)
	{
		if (string.IsNullOrEmpty(moves))
		{
			return null;
		}
		PlayerColor playerColor = Kifu.GetPlayerColor(Kifu.GetFirstMove(moves));
		SPosition sPosition = (SPosition)Domain.Game.Notation.Position.Clone();
		if (Domain.Game.Notation.Position.Turn != playerColor)
		{
			sPosition.UnMove(Domain.Game.Notation.MoveCurrent, Domain.Game.Notation.MovePrev);
		}
		SNotation sNotation = new SNotation();
		NotationModel.SetMoves(sNotation, sPosition, moves);
		return sNotation;
	}

	public SNotation LoadThreatmate()
	{
		ThreatmateInfo threatmateInfo = Domain.Game.ThreatmateInfo;
		if (threatmateInfo == null || threatmateInfo.State != ThreatmateState.Threatmate || !threatmateInfo.HasMoves)
		{
			return null;
		}
		SPosition sPosition = (SPosition)Domain.Game.Notation.Position.Clone();
		sPosition.Turn = sPosition.Turn.Opp();
		SNotation sNotation = new SNotation();
		NotationModel.SetMoves(sNotation, sPosition, null, threatmateInfo.Moves);
		return sNotation;
	}

	private void ShowGameOver(MoveType moveType)
	{
		if (moveType == MoveType.Stop)
		{
			view.Message(MainViewMessageId.GameStop);
		}
		else if (moveType.IsResult())
		{
			view.Message(MainViewMessageId.GameOver);
		}
	}

	public void SelectEngine(int engineNo, string engineName)
	{
		if (engineNo != Settings.EngineSettings.EngineNo || (engineNo != 1 && engineName != Settings.EngineSettings.EngineName))
		{
			Settings.EngineSettings.EngineNo = engineNo;
			Settings.EngineSettings.EngineName = engineName;
			Domain.Game.EngineTerminate();
		}
	}

	/// <summary>
	/// Unconditionally terminate current engine so next use picks up new settings (host/port etc).
	/// </summary>
	public void ForceEngineReconnect()
	{
		Domain.Game.EngineTerminate();
	}

	public void EngineWakeup()
	{
		Domain.Game.EngineWakeup();
	}

	public void EngineTerminate()
	{
		Domain.Game.Stop(pause: false);
		Domain.Game.EngineTerminate();
		view.Message(MainViewMessageId.EngineTerminated);
		view.UpdateState();
	}

	public void SetExternalEnginePath(string folder)
	{
		if (LocalFile.EnginePath == folder)
		{
			Settings.EngineSettings.EngineFolder = string.Empty;
		}
		else
		{
			Settings.EngineSettings.EngineFolder = folder;
		}
	}

	public bool CheckPvInfo(int pvnum, PVDispMode dispMode)
	{
		PvInfo pvInfo = Domain.Game.PvInfos.GetPvInfo(pvnum, dispMode);
		if (pvInfo == null)
		{
			return false;
		}
		bool flag = Notation.Position.HashKey.Equals(pvInfo.HashKey);
		if (!flag && Notation.MoveCurrent.Parent != null)
		{
			flag = Notation.MoveCurrent.Parent.Key.Equals(pvInfo.HashKey);
		}
		return flag;
	}

	public void AddBranch(int pvnum, PVDispMode dispmode)
	{
		PvInfo pvInfo = Domain.Game.PvInfos.GetPvInfo(pvnum, dispmode);
		if (pvInfo != null)
		{
			Domain.Game.NotationModel.AddBranch(null, pvInfo.PvMoves);
		}
	}

	public void AddBranch(SNotation branchNotation)
	{
		Domain.Game.NotationModel.AddBranch(branchNotation);
	}

	public void AddComment(int pvnum, PVDispMode dispmode)
	{
		PvInfo pvInfo = Domain.Game.PvInfos.GetPvInfo(pvnum, dispmode);
		if (pvInfo != null)
		{
			string comment = "*検討 " + pvInfo.ToString();
			Domain.Game.NotationModel.AddComment(comment);
		}
	}

	public void SetComment(string str)
	{
		Domain.Game.NotationModel.SetComment(str);
	}

	public bool CanCommentEdit()
	{
		bool result = true;
		if (GameMode != GameMode.Input && GameMode != GameMode.Consider)
		{
			result = false;
		}
		return result;
	}

	public void AnalyzerStart()
	{
		if (!Busy && Domain.Game.GameMode == GameMode.Input)
		{
			Domain.Game.AnalyzerStart();
		}
	}

	public bool CanAnalyzerStart()
	{
		bool result = true;
		if (Domain.Game.GameMode != GameMode.Input || Domain.Game.Busy)
		{
			result = false;
		}
		return result;
	}

	public void AnalyzeStop()
	{
		Domain.Game.AnalyzeStop();
	}

	public bool CanEditBoard()
	{
		bool result = true;
		if (GameMode == GameMode.Play || GameMode == GameMode.Analyzer)
		{
			result = false;
		}
		return result;
	}

	public bool CanManageEngine()
	{
		bool result = true;
		if (GameMode == GameMode.Play || GameMode == GameMode.Analyzer)
		{
			result = false;
		}
		return result;
	}

	public void ConsiderStart()
	{
		if (!Busy && CanConsiderStart())
		{
			Domain.Game.ConsiderStart();
		}
	}

	public bool CanConsiderStart()
	{
		bool result = true;
		if (Domain.Game.GameMode != GameMode.Input || Domain.Game.Busy)
		{
			result = false;
		}
		return result;
	}

	public void AutoPlayStart()
	{
		if (!Busy && CanAutoPlay())
		{
			autoplay = true;
			timer.Interval = Settings.AppSettings.PlayInterval;
			timer.Start();
			view.AutoPlayState(autoplay);
		}
	}

	public void AutoPlayStop()
	{
		if (autoplay)
		{
			timer.Stop();
			autoplay = false;
			view.AutoPlayState(autoplay);
		}
	}

	public void AutoPlayNext()
	{
		if (autoplay)
		{
			if (Domain.Game.NotationModel.Next())
			{
				timer.Start();
			}
			else
			{
				AutoPlayStop();
			}
		}
	}

	public bool CanAutoPlay()
	{
		bool result = false;
		if (GameMode == GameMode.Input || GameMode == GameMode.Consider)
		{
			result = true;
		}
		return result;
	}

	public void AutoPlayTick(object sender, EventArgs e)
	{
		AutoPlayNext();
	}

	private void NotationModel_NotationChanged(object sender, NotationEventArgs e)
	{
		view.UpdateNotation(e.EventId);
	}

	private void Game_GameEventHandler(object sender, GameEventArgs e)
	{
		switch (e.EventId)
		{
		case GameEventId.InitializeStart:
			view.Message(MainViewMessageId.Initializing);
			break;
		case GameEventId.InitializeEnd:
			view.OnEngineInitialized();
			break;
		case GameEventId.InitializeError:
			view.Message(MainViewMessageId.InitializeError);
			break;
		case GameEventId.GameStart:
			if (gameStartPopup)
			{
				gameStartPopup = false;
				view.Message(MainViewMessageId.GameStart);
			}
			break;
		case GameEventId.GameOver:
			ShowGameOver(Domain.Game.Notation.MoveCurrent.MoveType);
			break;
		case GameEventId.GameEnd:
			view.SetPlayer(black: true, white: true);
			break;
		case GameEventId.Info:
			view.UpdateInfo(Domain.Game.PvInfos);
			break;
		case GameEventId.Moved:
			view.Moved(e.Engine);
			break;
		case GameEventId.UpdateTime:
			view.UpdateTime();
			break;
		case GameEventId.NotationAnalyzeEnd:
			Settings.AppSettings.NotationAnalyzeCount++;
			if (Settings.AppSettings.NotationAnalyzeCount >= 5)
			{
				Settings.AppSettings.NotationAnalyzeCount = 0;
				view.ShowInterstitial();
			}
			break;
		}
		if (e.EventId != GameEventId.Info && e.EventId != GameEventId.UpdateTime)
		{
			view.UpdateState();
		}
	}
}
