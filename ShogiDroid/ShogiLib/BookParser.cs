using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
	/// <summary>
	/// sfenからキー部分を抽出（手数を除く）
	/// "lnsgkgsnl/... b - 1" → "lnsgkgsnl/... b -"
	/// </summary>
	public static string NormalizeSfenKey(string sfen)
	{
		int lastSpace = sfen.LastIndexOf(' ');
		if (lastSpace >= 0)
		{
			return sfen.Substring(0, lastSpace);
		}
		return sfen;
	}

	/// <summary>
	/// YaneuraOu db形式ファイルを読み込み
	/// </summary>
	public static Dictionary<string, List<BookMove>> LoadDb(string filename)
	{
		using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
		return LoadDb(stream);
	}

	/// <summary>
	/// YaneuraOu db形式ストリームを読み込み
	/// </summary>
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
				// "sfen board turn hands movenum" → キーは手数を除いた部分
				string sfenBody = line.Substring(5); // "sfen " を除く
				currentKey = NormalizeSfenKey(sfenBody);
				if (!book.ContainsKey(currentKey))
				{
					book[currentKey] = new List<BookMove>();
				}
			}
			else if (currentKey != null)
			{
				// 指し手行: "7g7f 3c3d 0 32 2" or "7g7f none 0 32 1"
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
	/// ShogiGUI sbk形式（バイナリ）を読み込み
	/// BookConv の SBook.Load() ロジック準拠
	/// </summary>
	public static Dictionary<string, List<BookMove>> LoadSbk(string filename)
	{
		using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
		return LoadSbk(stream);
	}

	/// <summary>
	/// ShogiGUI sbk形式ストリームを読み込み
	/// sbk形式: 各エントリが (sfen文字列 + 手のリスト) のバイナリ
	/// </summary>
	public static Dictionary<string, List<BookMove>> LoadSbk(Stream stream)
	{
		var book = new Dictionary<string, List<BookMove>>();

		using var reader = new BinaryReader(stream, Encoding.UTF8);
		try
		{
			while (reader.BaseStream.Position < reader.BaseStream.Length)
			{
				// sfen文字列長 (int32) + sfen文字列
				int sfenLen = reader.ReadInt32();
				if (sfenLen <= 0 || sfenLen > 1024)
				{
					break;
				}
				byte[] sfenBytes = reader.ReadBytes(sfenLen);
				string sfenStr = Encoding.UTF8.GetString(sfenBytes);
				string key = NormalizeSfenKey(sfenStr);

				// 手の数 (int32)
				int moveCount = reader.ReadInt32();
				var moves = new List<BookMove>();

				for (int i = 0; i < moveCount; i++)
				{
					// 各手: usi文字列長(int32) + usi文字列 + eval(int32) + depth(int32) + count(int32)
					int moveLen = reader.ReadInt32();
					byte[] moveBytes = reader.ReadBytes(moveLen);
					string usiMove = Encoding.UTF8.GetString(moveBytes);

					int eval = reader.ReadInt32();
					int depth = reader.ReadInt32();
					int count = reader.ReadInt32();

					moves.Add(new BookMove
					{
						UsiMove = usiMove,
						Response = "none",
						Eval = eval,
						Depth = depth,
						Count = count
					});
				}

				if (!book.ContainsKey(key))
				{
					book[key] = moves;
				}
				else
				{
					book[key].AddRange(moves);
				}
			}
		}
		catch (EndOfStreamException)
		{
			// ファイル末尾到達
		}

		return book;
	}
}
