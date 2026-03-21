using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using ShogiDroid;
using ShogiLib;

namespace ShogiGUI.Engine;

/// <summary>
/// リモートサーバーで並列棋譜解析を実行
/// </summary>
public class ParallelAnalyzer
{
	/// <summary>
	/// 1手分の解析結果
	/// </summary>
	public class MoveResult
	{
		public int Index;
		public string MoveUsi;
		public string BestMove;
		public int? Score;
		public int? Mate;
		public int? Depth;
		public int? SelDepth;
		public long? Nodes;
		public string InfoLine;
		public string PvString;
	}

	/// <summary>
	/// 進捗通知
	/// </summary>
	public event Action<string> Progress;

	/// <summary>
	/// Androidリソースから解析スクリプトを読み込み
	/// </summary>
	private static string LoadAnalyzeScript()
	{
		var context = Android.App.Application.Context;
		using var stream = context.Resources.OpenRawResource(Resource.Raw.parallel_analyze);
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}

	/// <summary>
	/// リモートサーバーで並列解析を実行
	/// </summary>
	public async Task<List<MoveResult>> ExecuteAsync(
		string host, int sshPort, string keyPath,
		string engineCommand, SNotation notation,
		int workers, long nodesPerMove,
		int threadsPerWorker, int hashPerWorker,
		List<string> extraSetOptions,
		CancellationToken ct = default)
	{
		// 棋譜をUSIコマンドに変換
		string usiCmd = BuildUsiPositionCommand(notation);
		if (string.IsNullOrEmpty(usiCmd))
			throw new InvalidOperationException("棋譜の変換に失敗しました");

		// エンジンパスと作業ディレクトリを解析
		ParseEngineCommand(engineCommand, out string enginePath, out string engineCwd);

		Report($"並列解析開始: {workers}並列, {threadsPerWorker}スレッド/ワーカー, Hash={hashPerWorker}MB, {nodesPerMove}ノード/手");

		return await Task.Run(() =>
		{
			using var keyFile = new PrivateKeyFile(keyPath);
			using var client = new SshClient(host, sshPort, "root", keyFile);
			client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
			client.Connect();

			// スクリプトをアップロード
			string scriptContent = LoadAnalyzeScript();
			using (var sftp = new SftpClient(host, sshPort, "root", keyFile))
			{
				sftp.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
				sftp.Connect();
				using var stream = new MemoryStream(Encoding.UTF8.GetBytes(scriptContent));
				sftp.UploadFile(stream, "/tmp/shogi_parallel_analyze.py", true);
				sftp.Disconnect();
			}
			Report("解析スクリプトをアップロード完了");

			// 実行コマンド構築
			string setoptionsArg = "";
			if (extraSetOptions != null && extraSetOptions.Count > 0)
				setoptionsArg = $" --setoptions \"{string.Join(";", extraSetOptions)}\"";

			string remoteCmd =
				$"python3 /tmp/shogi_parallel_analyze.py " +
				$"--cmd \"{usiCmd}\" " +
				$"--engine \"{enginePath}\" " +
				$"--engine_cwd \"{engineCwd}\" " +
				$"--move_nodes {nodesPerMove} " +
				$"--workers {workers} " +
				$"--threads_per_worker {threadsPerWorker} " +
				$"--hash_per_worker {hashPerWorker}" +
				setoptionsArg;

			AppDebug.Log.Info($"ParallelAnalyzer: executing: {remoteCmd}");
			Report("リモートで解析実行中...");

			var cmd = client.CreateCommand(remoteCmd);
			cmd.CommandTimeout = TimeSpan.FromMinutes(30);
			var asyncResult = cmd.BeginExecute();

			// stderrから進捗を読み取り
			var stderrReader = new StreamReader(cmd.ExtendedOutputStream);
			var outputBuilder = new StringBuilder();

			// stdoutとstderrを並列で読む
			var stderrTask = Task.Run(() =>
			{
				string line;
				while ((line = stderrReader.ReadLine()) != null)
				{
					if (ct.IsCancellationRequested) break;
					if (line.StartsWith("PROGRESS "))
						Report($"解析中... {line.Substring(9)}");
				}
			});

			cmd.EndExecute(asyncResult);
			string output = cmd.Result ?? "";
			stderrTask.Wait(5000);

			client.Disconnect();

			Report("結果を解析中...");
			return ParseResults(output, notation);
		}, ct);
	}

	/// <summary>
	/// SNotation全体をUSI positionコマンドに変換
	/// </summary>
	private string BuildUsiPositionCommand(SNotation notation)
	{
		// 全手を取得するために最終局面まで移動して手順を取得
		var saved = notation.MoveCurrent;

		// 全手の指し手をSFEN形式で取得
		notation.Last();
		string sfen;
		if (notation.IsOutputInitialPosition || notation.Handicap != Handicap.HIRATE)
			sfen = "position sfen " + notation.InitialPosition.PositionToString(1) + " moves " + notation.MovesToString();
		else
			sfen = "position startpos moves " + notation.MovesToString();

		// 元の位置に戻す
		notation.First();
		while (notation.MoveCurrent != saved && notation.Next(1)) { }

		return sfen;
	}

	/// <summary>
	/// エンジンコマンド ("cd /workspace/X && exec ./Y") からパスと作業ディレクトリを抽出
	/// </summary>
	private void ParseEngineCommand(string cmd, out string enginePath, out string engineCwd)
	{
		// "cd /workspace/Suisho10 && exec ./Suisho10-YaneuraOu-tournament-avx2"
		var match = Regex.Match(cmd, @"cd\s+(\S+)\s+&&\s+exec\s+(\S+)");
		if (match.Success)
		{
			engineCwd = match.Groups[1].Value;
			string exe = match.Groups[2].Value;
			if (exe.StartsWith("./"))
				enginePath = engineCwd + "/" + exe.Substring(2);
			else
				enginePath = exe;
		}
		else
		{
			// フォールバック: コマンド全体をパスとして扱う
			enginePath = cmd.Trim();
			engineCwd = Path.GetDirectoryName(enginePath) ?? "/workspace";
		}
	}

	/// <summary>
	/// スクリプト出力をパースしてMoveResultリストに変換
	/// </summary>
	private List<MoveResult> ParseResults(string output, SNotation notation)
	{
		var results = new List<MoveResult>();
		var lines = output.Split('\n');

		MoveResult current = null;
		foreach (string rawLine in lines)
		{
			string line = rawLine.Trim();
			if (line.StartsWith("RESULT "))
			{
				current = new MoveResult();
				// RESULT 1 move=7g7f bestmove=8c8d
				var m = Regex.Match(line, @"RESULT\s+(\d+)\s+move=(\S+)\s+bestmove=(\S+)");
				if (m.Success)
				{
					current.Index = int.Parse(m.Groups[1].Value);
					current.MoveUsi = m.Groups[2].Value;
					current.BestMove = m.Groups[3].Value;
					results.Add(current);
				}
			}
			else if (line.StartsWith("INFO ") && current != null)
			{
				// INFO 1 info depth 20 seldepth 25 score cp 45 nodes 10000000 pv 7g7f ...
				current.InfoLine = line;
				ParseInfoLine(line, current);
			}
		}

		return results;
	}

	/// <summary>
	/// USI info行からスコア等を抽出
	/// </summary>
	private void ParseInfoLine(string line, MoveResult result)
	{
		// depth
		var m = Regex.Match(line, @"\bdepth\s+(\d+)");
		if (m.Success) result.Depth = int.Parse(m.Groups[1].Value);

		// seldepth
		m = Regex.Match(line, @"\bseldepth\s+(\d+)");
		if (m.Success) result.SelDepth = int.Parse(m.Groups[1].Value);

		// score cp / score mate
		m = Regex.Match(line, @"\bscore\s+cp\s+([+-]?\d+)");
		if (m.Success)
			result.Score = int.Parse(m.Groups[1].Value);
		else
		{
			m = Regex.Match(line, @"\bscore\s+mate\s+([+-]?\d+)");
			if (m.Success) result.Mate = int.Parse(m.Groups[1].Value);
		}

		// nodes
		m = Regex.Match(line, @"\bnodes\s+(\d+)");
		if (m.Success) result.Nodes = long.Parse(m.Groups[1].Value);

		// pv
		m = Regex.Match(line, @"\bpv\s+(.+)$");
		if (m.Success) result.PvString = m.Groups[1].Value.Trim();
	}

	private void Report(string msg)
	{
		AppDebug.Log.Info($"ParallelAnalyzer: {msg}");
		Progress?.Invoke(msg);
	}
}
