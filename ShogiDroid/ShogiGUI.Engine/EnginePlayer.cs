using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;
using ShogiLib;

namespace ShogiGUI.Engine;

public class EnginePlayer : IPlayer
{
	private USIEngine engine_;

	private Thread th_;

	private object lockObj = new object();

	private ManualResetEvent mre;

	private SynchronizationContext syncContext;

	private EnginePlayerState state_;

	protected EngineOptions engineOptions_ = new EngineOptions();

	protected Dictionary<string, string> tempOptions_ = new Dictionary<string, string>();

	private bool cancel_;

	private ushort transactionCounter_;

	private int transactionNo_;

	private SPosition pos_;

	private bool is_go_req_;

	private GoRequest go_req_;

	private bool is_setoption_req_;

	private bool is_newgame_req_;

	private bool is_ready_req_;

	private MoveData ponder_;

	private string name_;

	private string author_;

	private int strength_ = -1;

	private PlayerColor color_ = PlayerColor.NoColor;

	private System.Timers.Timer timer_;

	private bool suppressTransportErrors_;

	private bool transportErrorReported_;

	public USIOptions Options => engine_.Options;

	public string Name => name_;

	public string Author => author_;

	public bool IsInitialized
	{
		get
		{
			if (state_ != EnginePlayerState.NONE)
			{
				return state_ != EnginePlayerState.INITIALIZING;
			}
			return false;
		}
	}

	public bool IsAlive => engine_ != null && engine_.IsAlive;

	public bool CanQueueGoRequest
	{
		get
		{
			lock (lockObj)
			{
				return engine_ != null
					&& engine_.IsAlive
					&& (state_ == EnginePlayerState.IDLE
						|| state_ == EnginePlayerState.GO
						|| state_ == EnginePlayerState.PONDER
						|| state_ == EnginePlayerState.STOP);
			}
		}
	}

	public virtual string WorkingDirectory => null;

	public event EventHandler<InitializedEventArgs> Initialized;

	/// <summary>
	/// usiok受信後、オプション送信前に発火する。
	/// ハンドラ内で SetTempOptionDeferred() を呼ぶことで、
	/// ユーザー保存オプションを自動設定値で上書きできる。
	/// </summary>
	public event EventHandler<InitializedEventArgs> OptionsApplying;

	public event EventHandler<ReadyOkEventArgs> ReadyOk;

	public event EventHandler<BestMoveEventArgs> BestMoveRecieved;

	public event EventHandler<CheckMateEventArgs> CheckMateRecieved;

	public event EventHandler<InfoEventArgs> InfoRecieved;

	public event EventHandler<StopEventArgs> Stopped;

	public event EventHandler<ReportErrorEventArgs> ReportError;

	public EnginePlayer(PlayerColor color)
	{
		color_ = color;
		syncContext = SynchronizationContext.Current;
	}

	public bool Init(string filename)
	{
		lock (lockObj)
		{
			if (state_ != EnginePlayerState.NONE)
			{
				return false;
			}
				engine_ = new USIEngine();
				cancel_ = false;
				suppressTransportErrors_ = false;
				transportErrorReported_ = false;
				if (!engine_.Initialize(filename, WorkingDirectory))
			{
				engine_ = null;
				return false;
			}
				if (!StartProtocol())
				{
					engine_.Terminate();
					engine_ = null;
					return false;
				}
				return true;
		}
	}

	public bool InitRemote(string host, int port)
	{
		lock (lockObj)
		{
			if (state_ != EnginePlayerState.NONE)
			{
				return false;
			}
				engine_ = new USIEngine();
				cancel_ = false;
				suppressTransportErrors_ = false;
				transportErrorReported_ = false;
				if (!engine_.InitializeRemote(host, port))
			{
				engine_ = null;
				return false;
			}
				if (!StartProtocol())
				{
					engine_.Terminate();
					engine_ = null;
					return false;
				}
				return true;
		}
	}

	public bool InitRemoteSsh(string host, int sshPort, string keyPath, string engineCommand)
	{
		lock (lockObj)
		{
			if (state_ != EnginePlayerState.NONE)
			{
				return false;
			}
				engine_ = new USIEngine();
				cancel_ = false;
				suppressTransportErrors_ = false;
				transportErrorReported_ = false;
				if (!engine_.InitializeRemoteSsh(host, sshPort, keyPath, engineCommand))
			{
				engine_ = null;
				return false;
			}
				if (!StartProtocol())
				{
					engine_.Terminate();
					engine_ = null;
					return false;
				}
				return true;
		}
	}

	private bool StartProtocol()
	{
			mre = new ManualResetEvent(initialState: false);
			th_ = new Thread(receive_thread);
			th_.Start();
			mre.Reset();
			state_ = EnginePlayerState.INITIALIZING;
			if (!send_cmd("usi"))
			{
				return false;
			}
			timer_ = new System.Timers.Timer();
			timer_.Elapsed += InitTimeout;
			timer_.Interval = 60000.0;
			timer_.Start();
		return true;
	}

	public void Terminate()
	{
		lock (lockObj)
		{
			if (state_ != EnginePlayerState.NONE && engine_ != null)
			{
				suppressTransportErrors_ = true;
				transportErrorReported_ = true;
				cancel_ = true;
				if (state_ == EnginePlayerState.GO || state_ == EnginePlayerState.PONDER)
				{
					state_ = EnginePlayerState.STOP;
					is_go_req_ = false;
					ponder_ = null;
					send_cmd("stop", reportFailure: false);
				}
				state_ = EnginePlayerState.TERMINATING;
				timer_.Stop();
				send_cmd("quit", reportFailure: false);
				engine_.Terminate();
				if (th_ != null)
				{
					th_.Join();
					th_ = null;
				}
				if (mre != null)
				{
					mre.Dispose();
					mre = null;
				}
				if (timer_ != null)
				{
					timer_.Dispose();
					timer_ = null;
				}
				state_ = EnginePlayerState.NONE;
			}
		}
	}

	public bool Wait(int timeoutms)
	{
		return mre.WaitOne(timeoutms);
	}

	public void Ready()
	{
		lock (lockObj)
		{
			if (engine_ == null || !engine_.IsAlive)
			{
				ReportTransportError(PlayerErrorId.EngineDisconnected);
				return;
			}
			if (state_ == EnginePlayerState.INISIALIZED || state_ == EnginePlayerState.IDLE)
			{
				send_isready();
			}
			else
			{
				is_ready_req_ = true;
			}
		}
	}

	private void send_isready()
	{
		is_ready_req_ = false;
		state_ = EnginePlayerState.WAIT_READY;
		mre.Reset();
		send_cmd("isready");
		timer_.Stop();
		timer_.Elapsed -= InitTimeout;
		timer_.Elapsed += ReadyTimeout;
		timer_.Interval = 30000.0;
		timer_.Start();
	}

	public void GameStart()
	{
		lock (lockObj)
		{
			if (state_ == EnginePlayerState.IDLE)
			{
				is_newgame_req_ = false;
				send_cmd("usinewgame");
			}
			else
			{
				is_newgame_req_ = true;
			}
		}
	}

	public void GameOver(PlayerColor wincolor)
	{
		Stop();
		lock (lockObj)
		{
			string text = ((wincolor == PlayerColor.NoColor) ? "draw" : ((wincolor != color_) ? "lose" : "win"));
			send_cmd("gameover " + text);
		}
	}

	private static bool HasPassInCurrentLine(SNotation notation)
	{
		foreach (MoveNode moveNode in notation.MoveNodes)
		{
			if (moveNode.MoveType == MoveType.Pass)
			{
				return true;
			}
			if (moveNode == notation.MoveCurrent)
			{
				break;
			}
		}
		return false;
	}

	private static void SetGoRequestPosition(GoRequest goRequest, SNotation notation, MoveData extraMove = null)
	{
		if (HasPassInCurrentLine(notation))
		{
			goRequest.Sfen = notation.Position.PositionToString(notation.MoveCurrent.Number + 1);
			goRequest.Moves = string.Empty;
		}
		else
		{
			if (notation.IsOutputInitialPosition || notation.Handicap != Handicap.HIRATE)
			{
				goRequest.Sfen = notation.InitialPosition.PositionToString(1);
			}
			goRequest.Moves = notation.MovesToString();
		}

		if (extraMove != null && extraMove.MoveType.IsMove())
		{
			goRequest.Moves = string.IsNullOrEmpty(goRequest.Moves)
				? extraMove.MoveToString()
				: goRequest.Moves + " " + extraMove.MoveToString();
		}
	}

	public int Go(SNotation notation, GameTimer time_info)
	{
		lock (lockObj)
		{
			if (ponder_ != null && state_ == EnginePlayerState.PONDER && ponder_.Equals(notation.MoveCurrent))
			{
				ponder_ = null;
				state_ = EnginePlayerState.GO;
				send_cmd("ponderhit");
				return -1;
			}
			ponder_ = null;
			if (state_ != EnginePlayerState.IDLE && state_ != EnginePlayerState.GO && state_ != EnginePlayerState.PONDER && state_ != EnginePlayerState.STOP)
			{
				return -1;
			}
			color_ = notation.Position.Turn;
			GameTime gameTime = ((color_ == PlayerColor.Black) ? time_info.BlackTime : time_info.WhiteTime);
			int byoyomi = gameTime.Byoyomi;
			if (gameTime.Byoyomi == 0 && gameTime.Time == 0)
			{
				byoyomi = 10000;
			}
			GoRequest goRequest = new GoRequest(time_info.BlackTime.RemainTime, time_info.WhiteTime.RemainTime, byoyomi);
			goRequest.Binc = time_info.BlackTime.Increment;
			goRequest.Winc = time_info.WhiteTime.Increment;
			SetGoRequestPosition(goRequest, notation);
			goRequest.Pos = (SPosition)notation.Position.Clone();
			goRequest.TransactionNo = ++transactionCounter_;
			if (state_ != EnginePlayerState.IDLE)
			{
				go_req_ = goRequest;
				is_go_req_ = true;
				if (state_ == EnginePlayerState.GO || state_ == EnginePlayerState.PONDER)
				{
					state_ = EnginePlayerState.STOP;
					send_cmd("stop");
				}
			}
			else
			{
				is_go_req_ = false;
				state_ = EnginePlayerState.GO;
				if (!ExecGoReeust(goRequest))
				{
					state_ = EnginePlayerState.IDLE;
					return -1;
				}
			}
		}
		return transactionCounter_;
	}

	private bool ExecGoReeust(GoRequest req)
	{
		mre.Reset();
		pos_ = req.Pos;
		string text = "position ";
		text += ((req.Sfen == string.Empty) ? "startpos" : ("sfen " + req.Sfen));
		if (!string.IsNullOrEmpty(req.Moves))
		{
			text = text + " moves " + req.Moves;
		}
		if (!send_cmd(text))
		{
			return false;
		}
		if (req.ReqType == GoRequest.Type.NORMAL)
		{
			if (req.Binc > 0 || req.Winc > 0)
			{
				text = $"go btime {req.Btime} wtime {req.Wtime} binc {req.Binc} winc {req.Winc}";
			}
			else
			{
				text = $"go btime {req.Btime} wtime {req.Wtime} byoyomi {req.Byoyomi}";
			}
			if (!send_cmd(text))
			{
				return false;
			}
		}
		else if (req.ReqType == GoRequest.Type.PONDER)
		{
			if (req.Binc > 0 || req.Winc > 0)
			{
				text = $"go ponder btime {req.Btime} wtime {req.Wtime} binc {req.Binc} winc {req.Winc}";
			}
			else
			{
				text = $"go ponder btime {req.Btime} wtime {req.Wtime} byoyomi {req.Byoyomi}";
			}
			if (!send_cmd(text))
			{
				return false;
			}
		}
		else if (req.ReqType == GoRequest.Type.MOVETIME)
		{
			text = "go";
			if (req.Time >= 0)
			{
				text = ((req.Time != 0L) ? (text + " byoyomi " + req.Time) : (text + " infinite "));
			}
			if (req.Nodes >= 0)
			{
				text = text + " nodes " + req.Nodes;
			}
			if (req.Depth >= 0)
			{
				text = text + " depth " + req.Depth;
			}
			if (!send_cmd(text))
			{
				return false;
			}
		}
		else if (req.ReqType == GoRequest.Type.MATE)
		{
			text = "go mate " + ((req.Time == 0L) ? "infinite" : req.Time.ToString());
			if (!send_cmd(text))
			{
				return false;
			}
		}
		else
		{
			if (!send_cmd("go infinite"))
			{
				return false;
			}
		}
		transactionNo_ = req.TransactionNo;
		return true;
	}

	public int Ponder(SNotation notation, GameTimer time_info)
	{
		lock (lockObj)
		{
			if (ponder_ == null || !ponder_.MoveType.IsMove())
			{
				return -1;
			}
			if (state_ != EnginePlayerState.IDLE)
			{
				return -1;
			}
			GameTime gameTime = ((color_ == PlayerColor.Black) ? time_info.BlackTime : time_info.WhiteTime);
			int byoyomi = gameTime.Byoyomi;
			if (gameTime.Byoyomi == 0 && gameTime.Time == 0)
			{
				byoyomi = 10000;
			}
			GoRequest goRequest = new GoRequest(time_info.BlackTime.RemainTime, time_info.WhiteTime.RemainTime, byoyomi)
			{
				ReqType = GoRequest.Type.PONDER,
				Binc = time_info.BlackTime.Increment,
				Winc = time_info.WhiteTime.Increment
			};
			SetGoRequestPosition(goRequest, notation, ponder_);
			goRequest.Pos = (SPosition)notation.Position.Clone();
			goRequest.Pos.Move(ponder_);
			goRequest.TransactionNo = ++transactionCounter_;
			state_ = EnginePlayerState.PONDER;
			if (!ExecGoReeust(goRequest))
			{
				state_ = EnginePlayerState.IDLE;
				return -1;
			}
		}
		return transactionCounter_;
	}

	public void MoveNow()
	{
		lock (lockObj)
		{
			if (state_ == EnginePlayerState.GO)
			{
				send_cmd("stop");
			}
		}
	}

	public void Stop()
	{
		lock (lockObj)
		{
			transactionCounter_++;
			if (state_ == EnginePlayerState.GO || state_ == EnginePlayerState.PONDER)
			{
				state_ = EnginePlayerState.STOP;
				is_go_req_ = false;
				ponder_ = null;
				send_cmd("stop");
			}
		}
	}

	public bool IsThinkingOrStopping()
	{
		if (state_ != EnginePlayerState.GO && state_ != EnginePlayerState.PONDER)
		{
			return state_ == EnginePlayerState.STOP;
		}
		return true;
	}

	public int Analyze(SNotation notation, AnalyzeTimeSettings settings)
	{
		lock (lockObj)
		{
			ponder_ = null;
			if (state_ != EnginePlayerState.IDLE && state_ != EnginePlayerState.GO && state_ != EnginePlayerState.PONDER && state_ != EnginePlayerState.STOP)
			{
				return -1;
			}
			GoRequest goRequest = new GoRequest(settings);
			SetGoRequestPosition(goRequest, notation);
			goRequest.Pos = (SPosition)notation.Position.Clone();
			goRequest.TransactionNo = ++transactionCounter_;
			if (state_ != EnginePlayerState.IDLE)
			{
				go_req_ = goRequest;
				is_go_req_ = true;
				if (state_ == EnginePlayerState.GO || state_ == EnginePlayerState.PONDER)
				{
					state_ = EnginePlayerState.STOP;
					send_cmd("stop");
				}
			}
			else
			{
				is_go_req_ = false;
				state_ = EnginePlayerState.GO;
				if (!ExecGoReeust(goRequest))
				{
					state_ = EnginePlayerState.IDLE;
					return -1;
				}
			}
		}
		return transactionCounter_;
	}

	public int Mate(SNotation notation, int timeMs)
	{
		lock (lockObj)
		{
			ponder_ = null;
			if (state_ != EnginePlayerState.IDLE && state_ != EnginePlayerState.GO && state_ != EnginePlayerState.PONDER && state_ != EnginePlayerState.STOP)
			{
				return -1;
			}
			GoRequest goRequest = new GoRequest(GoRequest.Type.MATE, timeMs);
			goRequest.Sfen = notation.Position.PositionToString(notation.MoveCurrent.Number + 1);
			goRequest.Moves = string.Empty;
			goRequest.Pos = (SPosition)notation.Position.Clone();
			goRequest.TransactionNo = ++transactionCounter_;
			if (state_ != EnginePlayerState.IDLE)
			{
				go_req_ = goRequest;
				is_go_req_ = true;
				if (state_ == EnginePlayerState.GO || state_ == EnginePlayerState.PONDER)
				{
					state_ = EnginePlayerState.STOP;
					send_cmd("stop");
				}
			}
			else
			{
				is_go_req_ = false;
				state_ = EnginePlayerState.GO;
				if (!ExecGoReeust(goRequest))
				{
					state_ = EnginePlayerState.IDLE;
					return -1;
				}
			}
		}
		return transactionCounter_;
	}

	public bool SetOption(string key, bool value, bool temp = false)
	{
		bool result = false;
		lock (lockObj)
		{
			if (!temp)
			{
				engineOptions_.SetOption(key, value);
			}
			if (IsInitialized)
			{
				result = engine_.SetOption(key, value);
			}
			else
			{
				tempOptions_[key] = value.ToString();
			}
			send_change_options();
			return result;
		}
	}

	public bool SetOption(string key, int value, bool temp = false)
	{
		bool result = false;
		lock (lockObj)
		{
			if (!temp)
			{
				engineOptions_.SetOption(key, value);
			}
			if (IsInitialized)
			{
				result = engine_.SetOption(key, value);
			}
			else
			{
				tempOptions_[key] = value.ToString();
			}
			send_change_options();
			return result;
		}
	}

	public bool SetOption(string key, string value, bool temp = false)
	{
		bool result = false;
		lock (lockObj)
		{
			if (!temp)
			{
				engineOptions_.SetOption(key, value);
			}
			if (IsInitialized)
			{
				result = engine_.SetOption(key, value);
			}
			else
			{
				tempOptions_[key] = value;
			}
			send_change_options();
			return result;
		}
	}

	/// <summary>
	/// tempOptions_ にオプションを追加する（即時送信しない）。
	/// OptionsApplying イベントハンドラ内で使用し、
	/// update_options() でユーザー保存オプションの後に適用される。
	/// </summary>
	public void SetTempOptionDeferred(string key, int value)
	{
		lock (lockObj)
		{
			tempOptions_[key] = value.ToString();
		}
	}

	/// <summary>
	/// 保存済みエンジンオプションをクリアする。
	/// マシンが変わった場合に、前回のオプションが不適切な値を送信するのを防ぐ。
	/// </summary>
	public void ClearEngineOptions()
	{
		lock (lockObj)
		{
			engineOptions_ = new EngineOptions();
		}
	}

	public void SetOptions(Dictionary<string, string> opt_name_value, bool temp = false)
	{
		lock (lockObj)
		{
			foreach (KeyValuePair<string, string> item in opt_name_value)
			{
				if (!temp)
				{
					engineOptions_.SetOption(item.Key, item.Value);
				}
				engine_.SetOption(item.Key, item.Value);
			}
			send_change_options();
		}
	}

	public void ResetOption(string key)
	{
		lock (lockObj)
		{
			if (!IsInitialized)
			{
				if (tempOptions_.ContainsKey(key))
				{
					tempOptions_.Remove(key);
				}
				return;
			}
			EngineOption option = engineOptions_.GetOption(key);
			if (option != null)
			{
				engine_.SetOption(key, option.Value.ToString());
			}
			else if (engine_.HasOption(key))
			{
				engine_.Options[key].Reset();
			}
			send_change_options();
		}
	}

	private void send_change_options()
	{
		if (state_ == EnginePlayerState.IDLE || state_ == EnginePlayerState.INISIALIZED)
		{
			send_options();
		}
		else
		{
			is_setoption_req_ = true;
		}
	}

	public void SetStrength(int strength)
	{
		lock (lockObj)
		{
			if (state_ == EnginePlayerState.IDLE || state_ == EnginePlayerState.INISIALIZED)
			{
				setStrengthInner(strength);
			}
			else
			{
				strength_ = strength;
			}
		}
	}

	private void setStrengthInner(int strength)
	{
		send_skill_level("Skill Level", strength);
		send_skill_level("SkillLevel", strength);
		send_skill_level("USI_SkillLevel", strength);
		strength_ = -1;
	}

	private void send_skill_level(string key, int strength)
	{
		if (engine_.Options.ContainsKey(key) && engine_.Options[key].Type == USIOptionType.SPIN)
		{
			USIOptionSpin uSIOptionSpin = (USIOptionSpin)engine_.Options[key];
			int value = (uSIOptionSpin.Max - uSIOptionSpin.Min) * strength / 100 + uSIOptionSpin.Min;
			engine_.SetOption(key, value);
			send_options();
		}
	}

	private void update_options()
	{
		lock (lockObj)
		{
			foreach (EngineOption option in engineOptions_.OptionList)
			{
				engine_.SetOption(option.Key, option.Value.ToString());
			}
			foreach (KeyValuePair<string, string> item in tempOptions_)
			{
				engine_.SetOption(item.Key, item.Value);
			}
			send_change_options();
		}
	}

	public virtual bool CopyFiles()
	{
		return false;
	}

	public virtual void LoadSettings()
	{
	}

	public virtual void SaveSettings()
	{
	}

	private void send_options()
	{
		foreach (KeyValuePair<string, USIOption> option in engine_.Options)
		{
			if (option.Value.HasChanged())
			{
				string cmd = ((option.Value.Type != USIOptionType.BUTTON) ? ("setoption name " + option.Key + " value " + option.Value.ValueToString()) : ("setoption name " + option.Key));
				send_cmd(cmd);
				option.Value.ClearChanged();
			}
		}
	}

	private bool send_cmd(string cmd, bool reportFailure = true)
	{
		AppDebug.Log.Info($"USI>> {cmd}");
		bool result = engine_ != null && engine_.WriteLine(cmd);
		if (!result && reportFailure)
		{
			ReportTransportError(PlayerErrorId.EngineDisconnected);
		}
		return result;
	}

	private void receive_thread()
	{
		while (!cancel_)
		{
			string str;
			var err = engine_.ReadLine(out str, -1);
			switch (err)
			{
			case StringQueue.Error.OK:
				if (!cancel_)
				{
					// info string は大量に来るため除外
					if (str != null && !str.StartsWith("info "))
						AppDebug.Log.Info($"USI<< {str}");
					receive_command(str);
				}
				break;
			case StringQueue.Error.TIMEOUT:
				break;
			default:
				AppDebug.Log.Error($"USI: receive_thread error={err}, cancel={cancel_}");
				if (!cancel_)
				{
					ReportTransportError(PlayerErrorId.EngineDisconnected);
				}
				return;
			}
		}
	}

	private void receive_command(string str)
	{
		lock (lockObj)
		{
			switch (state_)
			{
			case EnginePlayerState.INITIALIZING:
				if (str == "usiok")
				{
					state_ = EnginePlayerState.INISIALIZED;
					timer_.Stop();
					// 自動オプション（Threads/Hash等）をtempOptions_にセットする機会を与える
					OptionsApplying?.Invoke(this, new InitializedEventArgs(color_));
					update_options();
					OnInitialized(new InitializedEventArgs(color_));
					handleInitialized();
				}
				else
				{
					parse_option(str);
				}
				break;
			case EnginePlayerState.WAIT_READY:
				if (str == "readyok")
				{
					timer_.Stop();
					timer_.Elapsed -= ReadyTimeout;
					state_ = EnginePlayerState.IDLE;
					OnReadyOk(new ReadyOkEventArgs(color_));
					handleIdleState();
				}
				break;
			case EnginePlayerState.GO:
			case EnginePlayerState.PONDER:
				switch (new USITokenizer(str).GetToken())
				{
				case "bestmove":
					if (state_ == EnginePlayerState.GO)
					{
						parse_bestmove(str);
					}
					state_ = EnginePlayerState.IDLE;
					handleIdleState();
					break;
				case "checkmate":
					parse_checkmate(str);
					state_ = EnginePlayerState.IDLE;
					handleIdleState();
					break;
				case "info":
					parse_info(str);
					break;
				}
				break;
			case EnginePlayerState.STOP:
			{
				string token = new USITokenizer(str).GetToken();
				if (token == "bestmove" || token == "checkmate")
				{
					if (!is_go_req_)
					{
						OnStopped(new StopEventArgs(color_, transactionNo_));
					}
					state_ = EnginePlayerState.IDLE;
					handleIdleState();
				}
				break;
			}
			case EnginePlayerState.NONE:
			case EnginePlayerState.INISIALIZED:
			case EnginePlayerState.IDLE:
			case EnginePlayerState.TERMINATING:
				break;
			}
		}
	}

	private void parse_option(string str)
	{
		USITokenizer uSITokenizer = new USITokenizer(str);
		string token = uSITokenizer.GetToken();
		if (token == "id")
		{
			string token2 = uSITokenizer.GetToken();
			if (token2 == "name")
			{
				name_ = uSITokenizer.GetTokenLast();
			}
			else if (token2 == "author")
			{
				author_ = uSITokenizer.GetTokenLast();
			}
		}
		else if (token == "option")
		{
			engine_.AddOption(str);
		}
	}

	private void parse_bestmove(string str)
	{
		USITokenizer uSITokenizer = new USITokenizer(str);
		if (uSITokenizer.GetToken() == "bestmove")
		{
			string token = uSITokenizer.GetToken();
			string text = string.Empty;
			if (uSITokenizer.GetToken() == "ponder")
			{
				text = uSITokenizer.GetToken();
			}
			MoveData moveData = Sfen.ParseMove(pos_, token);
			if (text == string.Empty || text == "(none)" || !moveData.MoveType.IsMove() || !MoveCheck.IsValid(pos_, moveData))
			{
				ponder_ = new MoveData();
			}
			else
			{
				MoveData moveLast = pos_.MoveLast;
				pos_.Move(moveData);
				ponder_ = new MoveData(Sfen.ParseMove(pos_, text));
				pos_.UnMove(moveData, moveLast);
			}
			OnBestMoveRecieved(new BestMoveEventArgs(color_, transactionNo_, moveData, ponder_));
		}
	}

	private void parse_checkmate(string str)
	{
		USITokenizer uSITokenizer = new USITokenizer(str);
		if (!(uSITokenizer.GetToken() == "checkmate"))
		{
			return;
		}
		string token = uSITokenizer.GetToken();
		List<MoveDataEx> list = new List<MoveDataEx>();
		switch (token)
		{
		default:
		{
			SPosition sPosition = (SPosition)pos_.Clone();
			do
			{
				MoveDataEx moveDataEx = Sfen.ParseMove(sPosition, token);
				if (moveDataEx.MoveType == MoveType.NoMove || !MoveCheck.IsValid(sPosition, moveDataEx))
				{
					break;
				}
				list.Add(moveDataEx);
				sPosition.Move(moveDataEx);
			}
			while ((token = uSITokenizer.GetToken()) != string.Empty);
			break;
		}
		case "none":
			OnCheckMateRecieved(new CheckMateEventArgs(color_, transactionNo_, CheckMateResultKind.None));
			return;
		case "notimplemented":
			OnCheckMateRecieved(new CheckMateEventArgs(color_, transactionNo_, CheckMateResultKind.NotImplemented));
			return;
		case "timeout":
			OnCheckMateRecieved(new CheckMateEventArgs(color_, transactionNo_, CheckMateResultKind.Timeout));
			return;
		case "nomate":
			OnCheckMateRecieved(new CheckMateEventArgs(color_, transactionNo_, CheckMateResultKind.NoMate));
			return;
		}
		if (list.Count == 0)
		{
			OnCheckMateRecieved(new CheckMateEventArgs(color_, transactionNo_, CheckMateResultKind.None));
		}
		else
		{
			OnCheckMateRecieved(new CheckMateEventArgs(color_, transactionNo_, list));
		}
	}

	private void parse_info(string str)
	{
		USITokenizer uSITokenizer = new USITokenizer(str);
		if (!(uSITokenizer.GetToken() == "info"))
		{
			return;
		}
		PvInfo pvInfo = new PvInfo();
		string token;
		while ((token = uSITokenizer.GetToken()) != string.Empty)
		{
			switch (token)
			{
			case "depth":
			{
				USIString.ParseNum(uSITokenizer.GetToken(), out int out_num5);
				pvInfo.Depth = out_num5;
				continue;
			}
			case "seldepth":
			{
				USIString.ParseNum(uSITokenizer.GetToken(), out int out_num7);
				pvInfo.SelDepth = out_num7;
				continue;
			}
			case "time":
			{
				USIString.ParseNum(uSITokenizer.GetToken(), out int out_num6);
				pvInfo.TimeMs = out_num6;
				continue;
			}
			case "nodes":
			{
				USIString.ParseNum(uSITokenizer.GetToken(), out long out_num8);
				pvInfo.Nodes = out_num8;
				continue;
			}
			case "pv":
			{
				List<MoveDataEx> list = new List<MoveDataEx>();
				SPosition sPosition = (SPosition)pos_.Clone();
				if (ponder_ != null)
				{
					list.Add(new MoveDataEx(sPosition.MoveLast));
				}
				string token2;
				while ((token2 = uSITokenizer.GetToken()) != string.Empty)
				{
					MoveDataEx moveDataEx = Sfen.ParseMove(sPosition, token2);
					if (moveDataEx.MoveType == MoveType.NoMove)
					{
						uSITokenizer.Back(token2);
						break;
					}
					if (!MoveCheck.IsValid(sPosition, moveDataEx))
					{
						break;
					}
					list.Add(moveDataEx);
					sPosition.Move(moveDataEx);
				}
				pvInfo.PvMoves = list;
				continue;
			}
			case "score":
				token = uSITokenizer.GetToken();
				if (token == "cp")
				{
					USIString.ParseNum(uSITokenizer.GetToken(), out int out_num3);
					if (pos_.Turn == PlayerColor.White)
					{
						out_num3 = -out_num3;
					}
					pvInfo.Score = out_num3;
				}
				else if (token == "mate")
				{
					int rawMateFromEngine = 0;
					int rawMate = 0;
					int mateSign = 0;
					string text = uSITokenizer.GetToken();
					if (text == "+")
					{
						mateSign = 1;
					}
					else if (text == "-")
					{
						mateSign = -1;
					}
					else
					{
						string mateText = text;
						if (!string.IsNullOrEmpty(mateText) && mateText[0] == '+')
						{
							mateText = mateText.Substring(1);
						}
						if (USIString.ParseNum(mateText, out rawMate))
						{
							rawMateFromEngine = rawMate;
							mateSign = ((rawMate > 0) ? 1 : (-1));
						}
					}
					pvInfo.RawMatePly = rawMateFromEngine;
					if (pos_.Turn == PlayerColor.White)
					{
						mateSign = -mateSign;
						rawMate = -rawMate;
					}
					pvInfo.Mate = mateSign;
					if (rawMate != 0)
					{
						pvInfo.MatePly = rawMate;
						pvInfo.Score = System.Math.Abs(rawMate);
					}
				}
				continue;
			case "lowerbound":
				pvInfo.Bounds = 1;
				continue;
			case "upperbound":
				pvInfo.Bounds = -1;
				continue;
			case "currmove":
				Sfen.ParseMove(pos_, uSITokenizer.GetToken());
				continue;
			case "nps":
			{
				USIString.ParseNum(uSITokenizer.GetToken(), out int out_num2);
				pvInfo.NPS = out_num2;
				continue;
			}
			case "hashfull":
			{
				USIString.ParseNum(uSITokenizer.GetToken(), out int hf);
				pvInfo.HashFull = hf;
				continue;
			}
			case "multipv":
			{
				USIString.ParseNum(uSITokenizer.GetToken(), out int out_num);
				pvInfo.Rank = out_num;
				continue;
			}
			case "string":
				break;
			default:
				continue;
			}
			string tokenLast = uSITokenizer.GetTokenLast();
			if (tokenLast != string.Empty)
			{
				pvInfo.Message = tokenLast;
			}
			break;
		}
		HashKey hashKey = pos_.HashKey;
		if (ponder_ != null)
		{
			hashKey.UnMoveHash(pos_.MoveLast);
		}
		pvInfo.HashKey = hashKey;
		OnInfoRecieved(new InfoEventArgs(color_, transactionNo_, pvInfo));
	}

	private void handleInitialized()
	{
		lock (lockObj)
		{
			if (strength_ != -1)
			{
				setStrengthInner(strength_);
			}
			if (is_setoption_req_)
			{
				is_setoption_req_ = false;
				send_options();
			}
			if (is_ready_req_)
			{
				send_isready();
			}
		}
	}

	private void handleIdleState()
	{
		lock (lockObj)
		{
			if (strength_ != -1)
			{
				setStrengthInner(strength_);
			}
			if (is_setoption_req_)
			{
				is_setoption_req_ = false;
				send_options();
			}
			if (is_ready_req_)
			{
				send_isready();
				return;
			}
			if (is_newgame_req_)
			{
				is_newgame_req_ = false;
				send_cmd("usinewgame");
			}
			if (is_go_req_)
			{
				is_go_req_ = false;
				state_ = EnginePlayerState.GO;
				if (!ExecGoReeust(go_req_))
				{
					state_ = EnginePlayerState.IDLE;
				}
			}
		}
	}

	private void InitTimeout(object sender, ElapsedEventArgs e)
	{
		if (state_ == EnginePlayerState.INITIALIZING)
		{
			OnReportError(new ReportErrorEventArgs(color_, -1, PlayerErrorId.InitializeTimeout));
		}
	}

	private void ReadyTimeout(object sender, ElapsedEventArgs e)
	{
		timer_.Stop();
		timer_.Elapsed -= ReadyTimeout;
		if (state_ == EnginePlayerState.WAIT_READY)
		{
			AppDebug.Log.Error("EnginePlayer: readyok timeout, engine may be stuck");
			OnReportError(new ReportErrorEventArgs(color_, -1, PlayerErrorId.InitializeTimeout));
		}
	}

	protected virtual void OnInitialized(InitializedEventArgs e)
	{
		mre.Set();
		syncContext.Post(delegate
		{
			if (this.Initialized != null)
			{
				this.Initialized(this, e);
			}
		}, null);
	}

	protected virtual void OnReadyOk(ReadyOkEventArgs e)
	{
		mre.Set();
		syncContext.Post(delegate
		{
			if (this.ReadyOk != null)
			{
				this.ReadyOk(this, e);
			}
		}, null);
	}

	protected virtual void OnBestMoveRecieved(BestMoveEventArgs e)
	{
		mre.Set();
		syncContext.Post(delegate
		{
			if (transactionCounter_ != e.TransactionNo)
			{
				if (this.Stopped != null)
				{
					this.Stopped(this, new StopEventArgs(e.Color, e.TransactionNo));
				}
			}
			else if (this.BestMoveRecieved != null)
			{
				this.BestMoveRecieved(this, e);
			}
		}, null);
	}

	protected virtual void OnCheckMateRecieved(CheckMateEventArgs e)
	{
		mre.Set();
		syncContext.Post(delegate
		{
			if (transactionCounter_ == e.TransactionNo && this.CheckMateRecieved != null)
			{
				this.CheckMateRecieved(this, e);
			}
		}, null);
	}

	protected virtual void OnInfoRecieved(InfoEventArgs e)
	{
		syncContext.Post(delegate
		{
			if (transactionCounter_ == e.TransactionNo && this.InfoRecieved != null)
			{
				this.InfoRecieved(this, e);
			}
		}, null);
	}

	protected virtual void OnStopped(StopEventArgs e)
	{
		mre.Set();
		syncContext.Post(delegate
		{
			if (this.Stopped != null)
			{
				this.Stopped(this, e);
			}
		}, null);
	}

	private void ReportTransportError(PlayerErrorId errorId)
	{
		if (suppressTransportErrors_ || transportErrorReported_)
		{
			return;
		}
		transportErrorReported_ = true;
		timer_?.Stop();
		OnReportError(new ReportErrorEventArgs(color_, transactionNo_, errorId));
	}

	protected virtual void OnReportError(ReportErrorEventArgs e)
	{
		syncContext.Post(delegate
		{
			if (this.ReportError != null)
			{
				this.ReportError(this, e);
			}
		}, null);
	}
}
