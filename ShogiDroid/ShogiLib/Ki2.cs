using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ShogiLib;

public class Ki2 : Kifu
{
	private int writeMoveCount;

	private int spaceCount;

	public Ki2()
	{
		writeMoveCount = 0;
		spaceCount = 0;
	}

	private new void Init()
	{
		base.Init();
		writeMoveCount = 0;
		spaceCount = 0;
	}

	public override void Save(SNotation notation, string filename, Encoding encode)
	{
		Init();
		using StreamWriter streamWriter = new StreamWriter(filename, append: false, encode);
		streamWriter.NewLine = "\r\n";
		SaveToWriter(notation, streamWriter);
	}

	public override string ToString(SNotation notation, bool ki2 = false)
	{
		string empty = string.Empty;
		Init();
		using StringWriter stringWriter = new StringWriter();
		stringWriter.NewLine = "\r\n";
		SaveToWriter(notation, stringWriter);
		return stringWriter.ToString();
	}

	private void SaveToWriter(SNotation notation, TextWriter wr)
	{
		if (!notation.Handicap.IsSenGo())
		{
			turnStr = Kifu.TurnStrSimokami;
		}
		foreach (DictionaryEntry kifuInfo in notation.KifuInfos)
		{
			if (!(kifuInfo.Key.ToString() == "場所") && !(kifuInfo.Key.ToString() == "持ち時間"))
			{
				wr.WriteLine("{0}：{1}", kifuInfo.Key, kifuInfo.Value);
			}
		}
		if (notation.IsOutputInitialPosition)
		{
			WriteBoard(notation.InitialPosition, notation.Handicap, wr);
		}
		else if (notation.Handicap != Handicap.HIRATE)
		{
			wr.WriteLine("手合割：{0}", notation.Handicap.ToKifuString());
		}
		wr.WriteLine("{0}：{1}", turnStr[0], notation.BlackName);
		wr.WriteLine("{0}：{1}", turnStr[1], notation.WhiteName);
		foreach (DictionaryEntry kifuInfo2 in notation.KifuInfos)
		{
			if (kifuInfo2.Key.ToString() == "場所" || kifuInfo2.Key.ToString() == "持ち時間")
			{
				wr.WriteLine("{0}：{1}", kifuInfo2.Key, kifuInfo2.Value);
			}
		}
		_ = notation.MoveCurrent;
		SNotation sNotation = new SNotation(notation);
		sNotation.First();
		WriteMove(sNotation, sNotation.MoveCurrent, wr);
	}

	protected override void WriteLastMove(SNotation notation, TextWriter wr)
	{
		if (notation.MoveCurrent != notation.MoveFirst)
		{
			wr.WriteLine("手数＝{0}  {1}{2} まで", notation.MoveCurrent.Number, notation.MoveCurrent.Turn.ToKifChar(), GetLastMoveString(notation.MoveCurrent));
		}
	}

	protected string GetLastMoveString(MoveDataEx move)
	{
		string text = GetMoveString(move);
		if (text.StartsWith("同"))
		{
			text = text.Replace("\u3000", string.Empty);
			int num = move.ToSquare.DanOf();
			int num2 = move.ToSquare.SujiOf();
			text = $"{Kifu.ArabiaNumber[num2]}{Kifu.KanNumber[num]}" + text;
		}
		return text;
	}

	private void WriteMove(SNotation notation, MoveNode move_info, TextWriter wr)
	{
		if (move_info.Number != 0 && !move_info.MoveType.IsResult())
		{
			if (writeMoveCount >= 6)
			{
				wr.WriteLine(string.Empty);
				writeMoveCount = 0;
			}
			if (writeMoveCount != 0 && spaceCount != 0)
			{
				for (int i = 0; i < spaceCount; i++)
				{
					wr.Write("  ");
				}
			}
			string moveString = GetMoveString(notation.Position, move_info);
			if (moveString != string.Empty)
			{
				wr.Write("{0}{1}", notation.Position.Turn.ToKifChar(), moveString);
			}
			writeMoveCount++;
			spaceCount = 6 - moveString.Length;
		}
		if (move_info.Marker != null)
		{
			if (writeMoveCount != 0)
			{
				wr.WriteLine(string.Empty);
				writeMoveCount = 0;
			}
			wr.WriteLine("&{0}", move_info.Marker);
		}
		if (move_info.CommentList.Count != 0)
		{
			if (writeMoveCount != 0)
			{
				wr.WriteLine(string.Empty);
				writeMoveCount = 0;
			}
			for (int j = 0; j < move_info.CommentList.Count; j++)
			{
				wr.WriteLine("*{0}", move_info.CommentList[j]);
			}
		}
		if (move_info.Children.Count == 0)
		{
			if (move_info.MoveType.IsResult())
			{
				if (writeMoveCount != 0)
				{
					wr.WriteLine(string.Empty);
					writeMoveCount = 0;
				}
				PlayerColor turn = move_info.Turn;
				turn = move_info.Turn.Opp();
				switch (move_info.MoveType)
				{
				case MoveType.ResultFlag:
					wr.WriteLine("まで{0}手で{1}の勝ち", move_info.Number - 1, turnStr[(int)turn]);
					break;
				case MoveType.Timeout:
					wr.WriteLine("まで{0}手で時間切れにより{1}の勝ち", move_info.Number - 1, turnStr[(int)turn]);
					break;
				case MoveType.LoseFoul:
				case MoveType.WinFoul:
					wr.WriteLine("まで{0}手で{1}の{2}", move_info.Number - 1, turnStr[(int)move_info.Turn], Kifu.StrFromMovetype(move_info.MoveType));
					break;
				default:
					wr.WriteLine("まで{0}手で{1}", move_info.Number - 1, Kifu.StrFromMovetype(move_info.MoveType));
					break;
				}
			}
			wr.WriteLine(string.Empty);
			return;
		}
		notation.MoveChild(move_info);
		for (int k = 0; k < move_info.Children.Count; k++)
		{
			if (k > 0)
			{
				if (writeMoveCount != 0)
				{
					wr.WriteLine(string.Empty);
					writeMoveCount = 0;
				}
				wr.WriteLine("変化：{0}手", move_info.Children[k].Number);
			}
			WriteMove(notation, move_info.Children[k], wr);
		}
		notation.MoveParent();
	}

	public static string GetMoveString(SPosition position, MoveData move_data)
	{
		string empty = string.Empty;
		if (move_data.MoveType == MoveType.Pass)
		{
			return empty + "パス";
		}
		if (move_data.MoveType.IsMove())
		{
			string empty2 = string.Empty;
			string empty3 = string.Empty;
			if (move_data.MoveType.HasFlag(MoveType.Same))
			{
				empty2 = "同";
			}
			else
			{
				int num = move_data.ToSquare.DanOf();
				int num2 = move_data.ToSquare.SujiOf();
				empty2 = $"{Kifu.ArabiaNumber[num2]}{Kifu.KanNumber[num]}";
			}
			empty3 = Kifu.GetPieceStr(move_data.Piece);
			string text = string.Empty;
			if (!move_data.MoveType.HasFlag(MoveType.DropFlag))
			{
				text = GetActionString(position, move_data);
			}
			if (move_data.MoveType.HasFlag(MoveType.MoveMask))
			{
				text += "成";
			}
			else if (move_data.MoveType.HasFlag(MoveType.DropFlag))
			{
				if (CanSamePlaceMove(position, move_data))
				{
					text += "打";
				}
			}
			else if (IsNotPromotion(move_data))
			{
				text += "不成";
			}
			empty += empty2;
			if (empty2.Length == 1 && empty3.Length == 1 && text == string.Empty)
			{
				empty += "\u3000";
			}
			return empty + empty3 + text;
		}
		if (move_data.MoveType.IsResult())
		{
			return Kifu.StrFromMovetype(move_data.MoveType);
		}
		return string.Empty;
	}

	public static string GetMoveString(MoveDataEx move_data)
	{
		string empty = string.Empty;
		if (move_data.MoveType == MoveType.Pass)
		{
			return empty + "パス";
		}
		if (move_data.MoveType.IsMove())
		{
			string empty2 = string.Empty;
			string empty3 = string.Empty;
			if (move_data.MoveType.HasFlag(MoveType.Same))
			{
				empty2 = "同";
			}
			else
			{
				int num = move_data.ToSquare.DanOf();
				int num2 = move_data.ToSquare.SujiOf();
				empty2 = $"{Kifu.ArabiaNumber[num2]}{Kifu.KanNumber[num]}";
			}
			empty3 = Kifu.GetPieceStr(move_data.Piece);
			string text = move_data.Action.DisplayName();
			if (move_data.MoveType.HasFlag(MoveType.MoveMask))
			{
				text += "成";
			}
			else if (move_data.MoveType.HasFlag(MoveType.Unpromotion))
			{
				text += "不成";
			}
			empty += empty2;
			if (empty2.Length == 1 && empty3.Length == 1 && text == string.Empty)
			{
				empty += "\u3000";
			}
			return empty + empty3 + text;
		}
		if (move_data.MoveType.IsResult())
		{
			return Kifu.StrFromMovetype(move_data.MoveType);
		}
		return string.Empty;
	}

	public static string GetActionString(SPosition pos, MoveData move_data)
	{
		string result = string.Empty;
		if (!move_data.MoveType.IsMove())
		{
			return string.Empty;
		}
		if (!move_data.Piece.IsPromoted() && (move_data.Piece.TypeOf() == PieceType.FU || move_data.Piece.TypeOf() == PieceType.KYO || move_data.Piece.TypeOf() == PieceType.OU))
		{
			return string.Empty;
		}
		MoveData moveData = new MoveData(move_data);
		List<int> list = new List<int>();
		for (int i = 0; i < 81; i++)
		{
			if (i != move_data.FromSquare && pos.GetPiece(i) == move_data.Piece)
			{
				moveData.FromSquare = i;
				if (MoveCheck.IsValidLight(pos, moveData))
				{
					list.Add(i);
				}
			}
		}
		if (list.Count != 0)
		{
			if (move_data.FromSquare.RankOf() == move_data.ToSquare.RankOf())
			{
				result = (IsRankEmpty(list, move_data.FromSquare) ? "寄" : (IsRightMost(list, move_data.FromSquare) ? ((!EqualFile(list, move_data.FromSquare)) ? ((pos.Turn == PlayerColor.Black) ? "右" : "左") : ((pos.Turn == PlayerColor.Black) ? "右寄" : "左寄")) : ((!EqualFile(list, move_data.FromSquare)) ? ((pos.Turn == PlayerColor.Black) ? "左" : "右") : ((pos.Turn == PlayerColor.Black) ? "左寄" : "右寄"))));
			}
			else if (move_data.FromSquare.RankOf() > move_data.ToSquare.RankOf())
			{
				result = (IsRankGrater(list, move_data.ToSquare) ? ((pos.Turn == PlayerColor.Black) ? "上" : "引") : ((move_data.FromSquare.FileOf() == move_data.ToSquare.FileOf()) ? ((move_data.Piece.TypeOf() != PieceType.HI && move_data.Piece.TypeOf() != PieceType.KAK) ? ((pos.Turn == PlayerColor.Black) ? "直" : "引") : ((list[0].FileOf() >= move_data.FromSquare.FileOf()) ? ((pos.Turn == PlayerColor.Black) ? "左" : "右") : ((pos.Turn == PlayerColor.Black) ? "右" : "左"))) : (IsRightMost(list, move_data.FromSquare) ? ((!EqualFile(list, move_data.FromSquare)) ? ((pos.Turn == PlayerColor.Black) ? "右" : "左") : ((pos.Turn == PlayerColor.Black) ? "右上" : "左引")) : ((!EqualFile(list, move_data.FromSquare)) ? ((pos.Turn == PlayerColor.Black) ? "左" : "右") : ((pos.Turn == PlayerColor.Black) ? "左上" : "右引")))));
			}
			else if (move_data.FromSquare.RankOf() < move_data.ToSquare.RankOf())
			{
				result = (IsRankSmall(list, move_data.ToSquare) ? ((pos.Turn == PlayerColor.Black) ? "引" : "上") : ((move_data.FromSquare.FileOf() == move_data.ToSquare.FileOf()) ? ((move_data.Piece.TypeOf() != PieceType.HI && move_data.Piece.TypeOf() != PieceType.KAK) ? ((pos.Turn == PlayerColor.Black) ? "引" : "直") : ((list[0].FileOf() >= move_data.FromSquare.FileOf()) ? ((pos.Turn == PlayerColor.Black) ? "左" : "右") : ((pos.Turn == PlayerColor.Black) ? "右" : "左"))) : (IsRightMost(list, move_data.FromSquare) ? ((!EqualFile(list, move_data.FromSquare)) ? ((pos.Turn == PlayerColor.Black) ? "右" : "左") : ((pos.Turn == PlayerColor.Black) ? "右引" : "左上")) : ((!EqualFile(list, move_data.FromSquare)) ? ((pos.Turn == PlayerColor.Black) ? "左" : "右") : ((pos.Turn == PlayerColor.Black) ? "左引" : "右上")))));
			}
		}
		return result;
	}

	private static bool CanSamePlaceMove(SPosition pos, MoveData move_data)
	{
		MoveData moveData = new MoveData(move_data);
		moveData.MoveType &= (MoveType)191;
		moveData.MoveType |= MoveType.MoveFlag;
		for (int i = 0; i < 81; i++)
		{
			if (pos.GetPiece(i) == move_data.Piece)
			{
				moveData.FromSquare = i;
				if (MoveCheck.IsValidLight(pos, moveData))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool IsRankSmall(List<int> squareCollection, int sq)
	{
		return squareCollection.All(item => item.RankOf() >= sq.RankOf());
	}

	private static bool IsRankGrater(List<int> squareCollection, int sq)
	{
		return squareCollection.All(item => item.RankOf() <= sq.RankOf());
	}

	private static bool IsRankEmpty(List<int> squareCollection, int sq)
	{
		return squareCollection.All(item => item.RankOf() != sq.RankOf());
	}

	private static bool IsRightMost(List<int> squareCollection, int sq)
	{
		return squareCollection.Any(item => item.FileOf() < sq.FileOf());
	}

	private static bool EqualFile(List<int> squareCollection, int sq)
	{
		return squareCollection.Any(item => item.FileOf() == sq.FileOf());
	}

	private static bool IsLeftMost(List<int> squareCollection, int sq)
	{
		return squareCollection.Any(item => item.FileOf() > sq.FileOf());
	}

	public static bool IsNotPromotion(MoveData moveData)
	{
		if (moveData.MoveType.HasFlag(MoveType.MoveMask) || moveData.MoveType.HasFlag(MoveType.DropFlag))
		{
			return false;
		}
		return MoveCheck.CanPromote(moveData);
	}
}
