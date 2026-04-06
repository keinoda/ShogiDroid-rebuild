using System;
using System.Collections.Generic;
using ShogiLib;

namespace ShogiGUI.Engine;

/// <summary>
/// dlshogi ONNX モデルの Policy 推論をバックグラウンドで実行し、推定選択率を取得する。
/// ThreatmateAnalyzer と同じパターン。
/// </summary>
public class PolicyAnalyzer : IDisposable
{
	private readonly object lockObj = new object();

	private PolicyEnginePlayer enginePlayer;

	private SNotation pendingNotation;

	private bool engineReady;

	private int currentTransactionNo = -1;

	private PolicyInfo currentInfo = PolicyInfo.None();

	// MultiPV の info を蓄積するバッファ
	private Dictionary<int, PolicyMoveInfo> pendingMoves = new Dictionary<int, PolicyMoveInfo>();

	public PolicyInfo CurrentInfo
	{
		get { lock (lockObj) return currentInfo; }
	}

	public event EventHandler Updated;

	public void Analyze(SNotation notation)
	{
		if (notation == null)
		{
			Clear();
			return;
		}

		lock (lockObj)
		{
			pendingNotation = notation;
			pendingMoves.Clear();
			SetCurrentInfo(new PolicyInfo { State = PolicyState.Analyzing });

			if (!EnsureEngine())
			{
				SetCurrentInfo(new PolicyInfo { State = PolicyState.Error });
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
			currentTransactionNo = -1;
			pendingMoves.Clear();
			SetCurrentInfo(PolicyInfo.None());
			enginePlayer?.Stop();
		}
	}

	public void Dispose()
	{
		lock (lockObj)
		{
			pendingNotation = null;
			currentTransactionNo = -1;
			DisposeEngine();
		}
	}

	private bool EnsureEngine()
	{
		if (enginePlayer != null)
		{
			return true;
		}

		var player = new PolicyEnginePlayer(PlayerColor.Black);
		if (!player.CopyFiles())
		{
			return false;
		}
		player.LoadSettings();
		player.Initialized += EnginePlayer_Initialized;
		player.ReadyOk += EnginePlayer_ReadyOk;
		player.InfoReceived += EnginePlayer_InfoReceived;
		player.BestMoveReceived += EnginePlayer_BestMoveReceived;
		player.ReportError += EnginePlayer_ReportError;

		if (!player.Init(player.EnginePath))
		{
			player.Initialized -= EnginePlayer_Initialized;
			player.ReadyOk -= EnginePlayer_ReadyOk;
			player.InfoReceived -= EnginePlayer_InfoReceived;
			player.BestMoveReceived -= EnginePlayer_BestMoveReceived;
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
		pendingMoves.Clear();
		enginePlayer.GameStart();
		// depth 1, movetime 500ms（Policy推論は20ms程度なので十分な余裕）
		currentTransactionNo = enginePlayer.Analyze(pendingNotation, new AnalyzeTimeSettings(500, 0, 1));
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

	private void EnginePlayer_InfoReceived(object sender, InfoEventArgs e)
	{
		lock (lockObj)
		{
			if (e.TransactionNo != currentTransactionNo || e.PvInfo == null)
			{
				return;
			}

			// MultiPV の各行を蓄積
			int rank = e.PvInfo.Rank;
			if (rank <= 0) rank = 1;

			// score cp の値は選択率の0.1%単位（例: cp 358 = 35.8%）
			// USIパーサーが後手番で符号反転するため、絶対値を使用
			double selectionRate = System.Math.Abs(e.PvInfo.Score) / 10.0;

			string moveUSI = e.PvInfo.GetFirstMove(ShogiLib.MoveStyle.Kif);

			pendingMoves[rank] = new PolicyMoveInfo
			{
				Rank = rank,
				MoveUSI = moveUSI,
				SelectionRate = selectionRate,
				Score = e.PvInfo.Score
			};
		}
	}

	private void EnginePlayer_BestMoveReceived(object sender, BestMoveEventArgs e)
	{
		lock (lockObj)
		{
			if (e.TransactionNo != currentTransactionNo)
			{
				return;
			}

			// 蓄積された MultiPV 結果をまとめる
			var moves = new List<PolicyMoveInfo>(pendingMoves.Values);
			moves.Sort((a, b) => b.SelectionRate.CompareTo(a.SelectionRate));

			SetCurrentInfo(new PolicyInfo
			{
				State = PolicyState.Done,
				Moves = moves
			});
		}
	}

	private void EnginePlayer_ReportError(object sender, ReportErrorEventArgs e)
	{
		lock (lockObj)
		{
			AppDebug.Log.Error($"PolicyAnalyzer: エンジンエラー: {e.ErrorId}");
			DisposeEngine();
			SetCurrentInfo(new PolicyInfo { State = PolicyState.Error });
		}
	}

	private void DisposeEngine()
	{
		if (enginePlayer != null)
		{
			enginePlayer.Initialized -= EnginePlayer_Initialized;
			enginePlayer.ReadyOk -= EnginePlayer_ReadyOk;
			enginePlayer.InfoReceived -= EnginePlayer_InfoReceived;
			enginePlayer.BestMoveReceived -= EnginePlayer_BestMoveReceived;
			enginePlayer.ReportError -= EnginePlayer_ReportError;
			try { enginePlayer.Stop(); } catch { }
			try { enginePlayer.Terminate(); } catch { }
			enginePlayer = null;
		}
	}

	private void SetCurrentInfo(PolicyInfo info)
	{
		currentInfo = info ?? PolicyInfo.None();
		Updated?.Invoke(this, EventArgs.Empty);
	}
}
