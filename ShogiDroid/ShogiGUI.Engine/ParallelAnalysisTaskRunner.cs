using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ShogiDroid;
using ShogiLib;

namespace ShogiGUI.Engine;

public static class ParallelAnalysisTaskRunner
{
	public static async Task<List<ParallelAnalyzer.MoveResult>> ExecuteAsync(
		SNotation notation,
		int workers,
		long nodesPerMove,
		int threadsPerWorker,
		int hashPerWorker,
		Action<string> progress = null,
		CancellationToken ct = default)
	{
		string host = Settings.EngineSettings.RemoteHost;
		int sshPort = Settings.EngineSettings.VastAiSshPort;
		string keyPath = Settings.EngineSettings.VastAiSshKeyPath;
		string engineCmd = Settings.EngineSettings.VastAiSshEngineCommand;

		if (string.IsNullOrEmpty(host) || sshPort <= 0 || string.IsNullOrEmpty(keyPath) || string.IsNullOrEmpty(engineCmd))
		{
			throw new InvalidOperationException("SSH接続設定が不完全です");
		}

		var analyzer = new ParallelAnalyzer();
		void HandleProgress(string message)
		{
			CloudInstanceWatchdog.Instance.RecordActivity();
			progress?.Invoke(message);
		}
		analyzer.Progress += HandleProgress;
		CloudInstanceWatchdog.Instance.RecordActivity();

		return await analyzer.ExecuteAsync(
			host,
			sshPort,
			keyPath,
			engineCmd,
			notation,
			workers,
			nodesPerMove,
			threadsPerWorker,
			hashPerWorker,
			BuildExtraSetOptions(),
			ct);
	}

	public static void ApplyResults(SNotation notation, List<ParallelAnalyzer.MoveResult> results, MoveStyle moveStyle)
	{
		if (notation == null || results == null)
		{
			return;
		}

		string analysisText = Android.App.Application.Context.GetString(Resource.String.Analysis_Text);
		SPosition replayPos = (SPosition)notation.InitialPosition.Clone();
		int moveIndex = 0;

		foreach (MoveNode moveNode in notation.MoveNodes)
		{
			if (!moveNode.MoveType.IsMove())
			{
				continue;
			}

			replayPos.Move(moveNode);
			moveIndex++;

			var result = results.Find(r => r.Index == moveIndex);
			if (result == null)
			{
				continue;
			}

			if (result.Score.HasValue)
			{
				int score = result.Score.Value;
				if (moveNode.Turn == PlayerColor.Black)
				{
					score = -score;
				}
				moveNode.Score = score;
			}
			else if (result.Mate.HasValue)
			{
				int mate = result.Mate.Value;
				int mateScore = (mate > 0 ? 1 : -1) * (32000 - System.Math.Abs(mate));
				if (moveNode.Turn == PlayerColor.Black)
				{
					mateScore = -mateScore;
				}
				moveNode.Score = mateScore;
			}

			moveNode.BestMove = result.BestMove == result.MoveUsi ? MoveMatche.Best : MoveMatche.None;

			int senteScore = moveNode.HasScore ? moveNode.Score : 0;
			string evalStr = result.Mate.HasValue
				? PvInfo.ValueToString(senteScore > 0 ? 1 : -1, System.Math.Abs(result.Mate.Value), 0)
				: senteScore.ToString();

			string depthStr = result.Depth.HasValue
				? $"{result.Depth.Value}" + (result.SelDepth.HasValue ? $"/{result.SelDepth.Value}" : "")
				: "";
			string nodesStr = result.Nodes.HasValue ? PvInfo.NodesToString(result.Nodes.Value) : "";
			string pvKif = ConvertPvToKif(replayPos, result.PvString, moveStyle);

			string bestMark = moveNode.BestMove == MoveMatche.Best ? " ○" : "";
			string comment = $"*{analysisText}{bestMark} 評価値 {evalStr}";
			if (!string.IsNullOrEmpty(depthStr)) comment += $" 深さ {depthStr}";
			if (!string.IsNullOrEmpty(nodesStr)) comment += $" ノード数 {nodesStr}";
			if (!string.IsNullOrEmpty(pvKif)) comment += $" 読み筋 {pvKif}";

			moveNode.CommentAdd(comment);
		}
	}

	private static List<string> BuildExtraSetOptions()
	{
		var extraSetOptions = new List<string>();
		string settingsFile = Path.Combine(EngineFile.EngineFolder, "remote_engine", "remote_engine.xml");
		if (System.IO.File.Exists(settingsFile))
		{
			var savedOptions = EngineOptions.Load(settingsFile);
			foreach (var opt in savedOptions.OptionList)
			{
				string key = opt.Key;
				if (key == "Threads" || key == "USI_Hash" || key == "Hash" || key == "MultiPV")
				{
					continue;
				}
				extraSetOptions.Add($"setoption name {key} value {opt.Value}");
			}
		}

		extraSetOptions.Add("setoption name MultiPV value 1");
		return extraSetOptions;
	}

	private static string ConvertPvToKif(SPosition basePos, string pvString, MoveStyle style)
	{
		if (string.IsNullOrEmpty(pvString))
		{
			return string.Empty;
		}

		var pos = (SPosition)basePos.Clone();
		string[] usiMoves = pvString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var sb = new System.Text.StringBuilder();

		foreach (string usiMove in usiMoves)
		{
			MoveDataEx moveData = Sfen.ParseMove(pos, usiMove);
			if (moveData.MoveType == MoveType.NoMove || !MoveCheck.IsValid(pos, moveData))
			{
				break;
			}

			string turnMark = pos.Turn == PlayerColor.Black ? "▲" : "△";
			sb.Append($" {turnMark}{moveData.ToString(style)}");
			pos.Move(moveData);
		}

		return sb.ToString().TrimStart();
	}
}
