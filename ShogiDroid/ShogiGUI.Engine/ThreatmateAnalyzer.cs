using System;
using ShogiLib;

namespace ShogiGUI.Engine;

public class ThreatmateAnalyzer : IDisposable
{
	private readonly object lockObj = new object();

	private KomoringEnginePlayer enginePlayer;

	private SNotation pendingNotation;

	private PlayerColor pendingAttacker = PlayerColor.NoColor;

	private bool engineReady;

	private int currentTransactionNo = -1;

	private ThreatmateInfo currentInfo = ThreatmateInfo.None();

	public ThreatmateInfo CurrentInfo => currentInfo.Clone();

	public event EventHandler Updated;

	public void Analyze(SNotation notation)
	{
		if (notation == null || !HasBothKings(notation.Position) || MoveCheck.IsCheck(notation.Position))
		{
			Clear();
			return;
		}

		SNotation threatNotation = CreateThreatmateNotation(notation);
		lock (lockObj)
		{
			pendingNotation = threatNotation;
			pendingAttacker = threatNotation.Position.Turn;
			SetCurrentInfo(new ThreatmateInfo
			{
				State = ThreatmateState.Analyzing,
				Attacker = pendingAttacker
			});

			if (!EnsureEngine())
			{
				SetCurrentInfo(new ThreatmateInfo
				{
					State = ThreatmateState.Unknown,
					Attacker = pendingAttacker
				});
				return;
			}

			if (engineReady)
			{
				SubmitPending();
			}
		}
	}

	public void Clear()
	{
		lock (lockObj)
		{
			pendingNotation = null;
			pendingAttacker = PlayerColor.NoColor;
			currentTransactionNo = -1;
			SetCurrentInfo(ThreatmateInfo.None());
			enginePlayer?.Stop();
		}
	}

	public void Dispose()
	{
		lock (lockObj)
		{
			pendingNotation = null;
			currentTransactionNo = -1;
			if (enginePlayer != null)
			{
				enginePlayer.Initialized -= EnginePlayer_Initialized;
				enginePlayer.ReadyOk -= EnginePlayer_ReadyOk;
				enginePlayer.InfoRecieved -= EnginePlayer_InfoRecieved;
				enginePlayer.CheckMateRecieved -= EnginePlayer_CheckMateRecieved;
				enginePlayer.ReportError -= EnginePlayer_ReportError;
				enginePlayer.Stop();
				enginePlayer.Terminate();
				enginePlayer = null;
			}
		}
	}

	private bool EnsureEngine()
	{
		if (enginePlayer != null)
		{
			return true;
		}

		var player = new KomoringEnginePlayer(PlayerColor.Black);
		if (!player.CopyFiles())
		{
			return false;
		}
		player.LoadSettings();
		player.Initialized += EnginePlayer_Initialized;
		player.ReadyOk += EnginePlayer_ReadyOk;
		player.InfoRecieved += EnginePlayer_InfoRecieved;
		player.CheckMateRecieved += EnginePlayer_CheckMateRecieved;
		player.ReportError += EnginePlayer_ReportError;

		if (!player.Init(player.EnginePath))
		{
			player.Initialized -= EnginePlayer_Initialized;
			player.ReadyOk -= EnginePlayer_ReadyOk;
			player.InfoRecieved -= EnginePlayer_InfoRecieved;
			player.CheckMateRecieved -= EnginePlayer_CheckMateRecieved;
			player.ReportError -= EnginePlayer_ReportError;
			player.Terminate();
			return false;
		}

		enginePlayer = player;
		engineReady = false;
		return true;
	}

	private void SubmitPending()
	{
		if (enginePlayer == null || pendingNotation == null)
		{
			return;
		}
		enginePlayer.GameStart();
		currentTransactionNo = enginePlayer.Mate(pendingNotation, 0);
	}

	private void EnginePlayer_Initialized(object sender, InitializedEventArgs e)
	{
		lock (lockObj)
		{
			engineReady = false;
			enginePlayer?.Ready();
		}
	}

	private void EnginePlayer_ReadyOk(object sender, ReadyOkEventArgs e)
	{
		lock (lockObj)
		{
			engineReady = true;
			SubmitPending();
		}
	}

	private void EnginePlayer_InfoRecieved(object sender, InfoEventArgs e)
	{
		lock (lockObj)
		{
			if (e.TransactionNo != currentTransactionNo || e.PvInfo == null || !e.PvInfo.RawMatePly.HasValue)
			{
				return;
			}
			int rawMate = e.PvInfo.RawMatePly.Value;
			if (rawMate == -9999)
			{
				SetCurrentInfo(new ThreatmateInfo
				{
					State = ThreatmateState.NoThreatmate,
					Attacker = pendingAttacker,
					Moves = new System.Collections.Generic.List<MoveDataEx>()
				});
			}
			else if (rawMate > 0)
			{
				SetCurrentInfo(new ThreatmateInfo
				{
					State = ThreatmateState.Threatmate,
					Attacker = pendingAttacker,
					MatePly = rawMate,
					Moves = ThreatmateInfo.CloneMoves(e.PvInfo.PvMoves)
				});
			}
		}
	}

	private void EnginePlayer_CheckMateRecieved(object sender, CheckMateEventArgs e)
	{
		lock (lockObj)
		{
			if (e.TransactionNo != currentTransactionNo)
			{
				return;
			}
			switch (e.Kind)
			{
			case CheckMateResultKind.Mate:
				SetCurrentInfo(new ThreatmateInfo
				{
					State = ThreatmateState.Threatmate,
					Attacker = pendingAttacker,
					MatePly = e.Moves?.Count ?? 0,
					Moves = ThreatmateInfo.CloneMoves(e.Moves)
				});
				break;
			case CheckMateResultKind.NoMate:
				SetCurrentInfo(new ThreatmateInfo
				{
					State = ThreatmateState.NoThreatmate,
					Attacker = pendingAttacker,
					Moves = new System.Collections.Generic.List<MoveDataEx>()
				});
				break;
			case CheckMateResultKind.Timeout:
			case CheckMateResultKind.None:
			case CheckMateResultKind.NotImplemented:
				SetCurrentInfo(new ThreatmateInfo
				{
					State = ThreatmateState.Unknown,
					Attacker = pendingAttacker
				});
				break;
			}
		}
	}

	private void EnginePlayer_ReportError(object sender, ReportErrorEventArgs e)
	{
		lock (lockObj)
		{
			SetCurrentInfo(new ThreatmateInfo
			{
				State = ThreatmateState.Unknown,
				Attacker = pendingAttacker
			});
		}
	}

	private void SetCurrentInfo(ThreatmateInfo info)
	{
		ThreatmateInfo next = info ?? ThreatmateInfo.None();
		if (currentInfo.State == next.State
			&& currentInfo.Attacker == next.Attacker
			&& currentInfo.MatePly == next.MatePly
			&& AreMovesEqual(currentInfo.Moves, next.Moves))
		{
			return;
		}
		currentInfo = next;
		Updated?.Invoke(this, EventArgs.Empty);
	}

	private static bool AreMovesEqual(System.Collections.Generic.IList<MoveDataEx> left, System.Collections.Generic.IList<MoveDataEx> right)
	{
		int leftCount = left?.Count ?? 0;
		int rightCount = right?.Count ?? 0;
		if (leftCount != rightCount)
		{
			return false;
		}
		for (int i = 0; i < leftCount; i++)
		{
			if (!left[i].Equals(right[i]))
			{
				return false;
			}
		}
		return true;
	}

	private static bool HasBothKings(SPosition position)
	{
		bool blackKing = false;
		bool whiteKing = false;
		foreach (Piece piece in position.Board)
		{
			if (piece == Piece.BOU)
			{
				blackKing = true;
			}
			else if (piece == Piece.WOU)
			{
				whiteKing = true;
			}
		}
		return blackKing && whiteKing;
	}

	private static SNotation CreateThreatmateNotation(SNotation notation)
	{
		SPosition position = (SPosition)notation.Position.Clone();
		position.Turn = position.Turn.Opp();

		SNotation threatNotation = new SNotation();
		threatNotation.SetInitialPosition(position);
		threatNotation.Handicap = Handicap.OTHER;
		return threatNotation;
	}
}
