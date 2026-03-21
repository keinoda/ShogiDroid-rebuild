using System;
using System.Collections.Generic;
using System.Linq;

namespace ShogiLib;

/// <summary>
/// 定跡辞書のHashKeyベース変換と、現在局面への定跡手適用
/// </summary>
public static class BookExpander
{
	/// <summary>
	/// 進捗通知コールバック (展開済みノード数)
	/// </summary>
	public static Action<int> OnProgress;
	/// <summary>
	/// sfen文字列キーの定跡辞書を HashKey ベースに変換する
	/// </summary>
	public static Dictionary<HashKey, List<BookMove>> BuildHashBook(
		Dictionary<string, List<BookMove>> book)
	{
		var hashBook = new Dictionary<HashKey, List<BookMove>>();
		var tempPos = new SPosition();

		foreach (var entry in book)
		{
			string sfenWithNum = entry.Key + " 1";
			tempPos.Init();
			try
			{
				Sfen.LoadPosition(tempPos, sfenWithNum);
				hashBook[tempPos.HashKey] = entry.Value;
			}
			catch { }
		}

		return hashBook;
	}

	/// <summary>
	/// 現在局面に定跡の候補手を分岐として追加する
	/// 既に同じ手がある場合はMERGEされる
	/// </summary>
	public static bool ApplyBookAtPosition(
		SNotation notation,
		Dictionary<HashKey, List<BookMove>> hashBook)
	{
		if (hashBook == null || hashBook.Count == 0)
		{
			return false;
		}

		HashKey posKey = notation.Position.HashKey;
		if (!hashBook.TryGetValue(posKey, out var bookMoves))
		{
			return false;
		}

		// 現在ノードの位置を記憶
		var currentNode = notation.MoveCurrent;

		// 出現回数昇順で追加（最後=最多がChildCurrentになる）
		var sorted = bookMoves.OrderBy(m => m.Count).ToList();
		bool anyAdded = false;

		foreach (var bm in sorted)
		{
			MoveDataEx moveData = Sfen.ParseMove(notation.Position, bm.UsiMove);
			if (moveData == null || moveData.MoveType == MoveType.NoMove)
			{
				continue;
			}

			moveData.Score = bm.Eval;
			if (bm.Count > 0 || bm.Depth > 0)
			{
				moveData.CommentList = new List<string>();
				moveData.CommentList.Add($"出現回数: {bm.Count}  評価値: {bm.Eval}  深さ: {bm.Depth}");
			}

			bool added = notation.AddMove(moveData, MoveAddMode.MERGE, changeChildCurrent: false);
			if (added)
			{
				anyAdded = true;
				// AddMoveで進んだので戻る
				notation.MoveParent();
			}
		}

		return anyAdded;
	}
}
