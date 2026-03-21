using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProtoBuf;

namespace ShogiLib;

public class BookMove
{
	public string UsiMove { get; set; }
	public string Response { get; set; }
	public int Eval { get; set; }
	public int Depth { get; set; }
	public int Count { get; set; }
}

public static class BookParser
{
	public static string NormalizeSfenKey(string sfen)
	{
		int lastSpace = sfen.LastIndexOf(' ');
		if (lastSpace >= 0)
		{
			return sfen.Substring(0, lastSpace);
		}
		return sfen;
	}

	public static Dictionary<string, List<BookMove>> LoadDb(string filename)
	{
		using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
		return LoadDb(stream);
	}

	public static Dictionary<string, List<BookMove>> LoadDb(Stream stream)
	{
		var book = new Dictionary<string, List<BookMove>>();
		string currentKey = null;

		using var reader = new StreamReader(stream, Encoding.UTF8);
		string line;
		while ((line = reader.ReadLine()) != null)
		{
			line = line.Trim();
			if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
			{
				continue;
			}

			if (line.StartsWith("sfen "))
			{
				string sfenBody = line.Substring(5);
				currentKey = NormalizeSfenKey(sfenBody);
				if (!book.ContainsKey(currentKey))
				{
					book[currentKey] = new List<BookMove>();
				}
			}
			else if (currentKey != null)
			{
				var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length >= 1)
				{
					var move = new BookMove
					{
						UsiMove = parts[0],
						Response = parts.Length >= 2 ? parts[1] : "none",
						Eval = parts.Length >= 3 && int.TryParse(parts[2], out int eval) ? eval : 0,
						Depth = parts.Length >= 4 && int.TryParse(parts[3], out int depth) ? depth : 0,
						Count = parts.Length >= 5 && int.TryParse(parts[4], out int count) ? count : 0
					};
					book[currentKey].Add(move);
				}
			}
		}

		return book;
	}

	/// <summary>
	/// ShogiGUI sbk形式を読み込み（Protocol Buffers + BookConv互換デコード）
	/// </summary>
	public static Dictionary<string, List<BookMove>> LoadSbk(string filename)
	{
		using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
		return LoadSbk(stream);
	}

	/// <summary>
	/// 定跡辞書をYaneuraOu db形式でファイルに書き出す
	/// </summary>
	public static void SaveDb(Dictionary<string, List<BookMove>> book, string filename)
	{
		using var writer = new StreamWriter(filename, false, Encoding.UTF8);
		writer.WriteLine("#YANEURAOU-DB2016 1.00");
		foreach (var entry in book)
		{
			writer.WriteLine($"sfen {entry.Key} 1");
			foreach (var move in entry.Value)
			{
				writer.WriteLine($"{move.UsiMove} {move.Response} {move.Eval} {move.Depth} {move.Count}");
			}
		}
	}

	public static Dictionary<string, List<BookMove>> LoadSbk(Stream stream)
	{
		var sbook = Serializer.Deserialize<SbkBook>(stream);
		if (sbook == null || sbook.BookStates == null || sbook.BookStates.Count == 0)
		{
			return new Dictionary<string, List<BookMove>>();
		}

		// NextStateId → SbkBookState のインデックスマップ
		var stateById = new Dictionary<int, SbkBookState>();
		foreach (var state in sbook.BookStates)
		{
			stateById[state.Id] = state;
		}

		// sbkはツリー構造（初期局面のみPosition有、以降はNextStateIdで辿る）
		// DFSでツリーを辿りながらSPositionで局面を再現し、db辞書に変換する
		var book = new Dictionary<string, List<BookMove>>();
		var visitedStates = new HashSet<int>();

		// 初期局面を見つける（Position が設定されているか、Id=0）
		SbkBookState rootState = null;
		foreach (var state in sbook.BookStates)
		{
			if (!string.IsNullOrEmpty(state.Position))
			{
				rootState = state;
				break;
			}
		}
		if (rootState == null)
		{
			rootState = sbook.BookStates[0];
		}

		// 初期局面のSPositionを構築
		var pos = new SPosition();
		if (!string.IsNullOrEmpty(rootState.Position))
		{
			Sfen.LoadPosition(pos, rootState.Position);
		}
		else
		{
			pos.InitHashKey();
		}

		SbkDFS(rootState, pos, stateById, book, visitedStates);

		return book;
	}

	/// <summary>
	/// sbkツリーをDFSで辿り、各局面のsfenと候補手をdb辞書に追加
	/// </summary>
	private static void SbkDFS(
		SbkBookState state,
		SPosition pos,
		Dictionary<int, SbkBookState> stateById,
		Dictionary<string, List<BookMove>> book,
		HashSet<int> visitedStates)
	{
		if (visitedStates.Contains(state.Id))
		{
			return;
		}
		visitedStates.Add(state.Id);

		if (state.Moves == null || state.Moves.Count == 0)
		{
			return;
		}

		// 現在局面のsfenキーを生成
		string sfenKey = NormalizeSfenKey(pos.PositionToString(1));

		// この局面の候補手を収集
		var moves = new List<BookMove>();
		foreach (var sbkMove in state.Moves)
		{
			string usiMove = SbkMoveToUsi(sbkMove.Move);
			if (string.IsNullOrEmpty(usiMove))
			{
				continue;
			}

			int eval = 0;
			int depth = 0;
			if (state.Evals != null && state.Evals.Count > 0)
			{
				eval = state.Evals[0].EvalutionValue;
				depth = state.Evals[0].Depth;
			}

			moves.Add(new BookMove
			{
				UsiMove = usiMove,
				Response = "none",
				Eval = eval,
				Depth = depth,
				Count = sbkMove.Weight > 0 ? sbkMove.Weight : 1
			});
		}

		if (moves.Count > 0)
		{
			if (!book.ContainsKey(sfenKey))
			{
				book[sfenKey] = moves;
			}
			else
			{
				book[sfenKey].AddRange(moves);
			}
		}

		// 各手について次の局面に再帰
		foreach (var sbkMove in state.Moves)
		{
			if (sbkMove.NextStateId == 0 || !stateById.TryGetValue(sbkMove.NextStateId, out var nextState))
			{
				continue;
			}

			string usiMove = SbkMoveToUsi(sbkMove.Move);
			if (string.IsNullOrEmpty(usiMove))
			{
				continue;
			}

			// USI文字列から手を適用
			var moveData = Sfen.ParseMove(pos, usiMove);
			if (moveData == null || moveData.MoveType == MoveType.NoMove)
			{
				continue;
			}

			// CapturePiece設定（UnMoveに必要）
			if (moveData.MoveType.HasFlag(MoveType.MoveFlag) && !moveData.MoveType.HasFlag(MoveType.DropFlag))
			{
				moveData.CapturePiece = pos.GetPiece(moveData.ToSquare);
			}

			pos.Move(moveData);
			SbkDFS(nextState, pos, stateById, book, visitedStates);
			pos.UnMove(moveData, null);
		}
	}

	/// <summary>
	/// sbkの32ビット手エンコードをUSI文字列に変換
	/// BookConv準拠のビットレイアウト:
	///   bits  0-3 : 移動元段 (rank, 0=持ち駒)
	///   bits  4-7 : 移動元筋 (file, 0=持ち駒)
	///   bits  8-11: 移動先段 (rank)
	///   bits 12-15: 移動先筋 (file)
	///   bit  19   : 成りフラグ
	///   bits 24-29: 駒種
	///   bit  31   : 手番 (0=先手, 1=後手)
	/// </summary>
	private static string SbkMoveToUsi(int move)
	{
		int fromRank = move & 0xF;
		int fromFile = (move >> 4) & 0xF;
		int toRank = (move >> 8) & 0xF;
		int toFile = (move >> 12) & 0xF;
		bool promote = ((move >> 19) & 1) != 0;
		int pieceType = (move >> 24) & 0x3F;

		if (toFile < 1 || toFile > 9 || toRank < 1 || toRank > 9)
		{
			return null;
		}

		char toFileChar = (char)('0' + toFile);
		char toRankChar = (char)('a' + toRank - 1);

		if (fromFile == 0 && fromRank == 0)
		{
			// 駒打ち
			char pieceChar = PieceTypeToChar(pieceType);
			if (pieceChar == '\0')
			{
				return null;
			}
			return $"{pieceChar}*{toFileChar}{toRankChar}";
		}
		else
		{
			if (fromFile < 1 || fromFile > 9 || fromRank < 1 || fromRank > 9)
			{
				return null;
			}
			char fromFileChar = (char)('0' + fromFile);
			char fromRankChar = (char)('a' + fromRank - 1);
			string prom = promote ? "+" : "";
			return $"{fromFileChar}{fromRankChar}{toFileChar}{toRankChar}{prom}";
		}
	}

	/// <summary>
	/// sbkの駒種番号をUSIの駒文字に変換（打ち駒用）
	/// BookConvの駒種定義に準拠
	/// </summary>
	private static char PieceTypeToChar(int pieceType)
	{
		// 先手駒: 1=歩,2=香,3=桂,4=銀,5=金,6=角,7=飛,8=王
		// 後手駒: bit31で判定されるが、打ち駒は色なし
		int pt = pieceType & 0xF; // 下位4ビットが駒種
		switch (pt)
		{
			case 1: return 'P';
			case 2: return 'L';
			case 3: return 'N';
			case 4: return 'S';
			case 5: return 'G';
			case 6: return 'B';
			case 7: return 'R';
			default: return '\0';
		}
	}
}

// ============================================================
// ShogiGUI sbk形式の Protocol Buffers 定義
// BookConv の book.proto / book.cs 準拠
// ============================================================

[ProtoContract]
public class SbkBook
{
	[ProtoMember(1)]
	public string Author { get; set; } = "";

	[ProtoMember(2)]
	public string Description { get; set; } = "";

	[ProtoMember(3)]
	public List<SbkBookState> BookStates { get; set; } = new List<SbkBookState>();
}

[ProtoContract]
public class SbkBookState
{
	[ProtoMember(1)]
	public int Id { get; set; }

	[ProtoMember(2)]
	public ulong BoardKey { get; set; }

	[ProtoMember(3)]
	public uint HandKey { get; set; }

	[ProtoMember(4)]
	public int Games { get; set; }

	[ProtoMember(5)]
	public int WonBlack { get; set; }

	[ProtoMember(6)]
	public int WonWhite { get; set; }

	[ProtoMember(7)]
	public string Position { get; set; } = "";

	[ProtoMember(8)]
	public string Comment { get; set; } = "";

	[ProtoMember(9)]
	public List<SbkBookMove> Moves { get; set; } = new List<SbkBookMove>();

	[ProtoMember(10)]
	public List<SbkBookEval> Evals { get; set; } = new List<SbkBookEval>();
}

[ProtoContract]
public class SbkBookMove
{
	[ProtoMember(1)]
	public int Move { get; set; }

	[ProtoMember(2)]
	public int Evalution { get; set; }

	[ProtoMember(3)]
	public int Weight { get; set; }

	[ProtoMember(4)]
	public int NextStateId { get; set; }
}

[ProtoContract]
public class SbkBookEval
{
	[ProtoMember(1)]
	public int EvalutionValue { get; set; }

	[ProtoMember(2)]
	public int Depth { get; set; }

	[ProtoMember(3)]
	public int SelDepth { get; set; }

	[ProtoMember(4)]
	public long Nodes { get; set; }

	[ProtoMember(5)]
	public string Variation { get; set; } = "";

	[ProtoMember(6)]
	public string EngineName { get; set; } = "";
}
