using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace ShogiLib;

public class EnglishNotation
{
	protected static readonly char[] PieceChar = new char[9] { ' ', 'P', 'L', 'N', 'S', 'G', 'B', 'R', 'K' };

	protected static readonly string[] PiecePromotionStr = new string[9] { " ", "+P", "+L", "+N", "+S", "G", "H", "D", "K" };

	protected static readonly char[] KanNumber = new char[11]
	{
		'零', '一', '二', '三', '四', '五', '六', '七', '八', '九',
		'十'
	};

	private const int Width = 10;

	private const int MoveNum = 8;

	protected static readonly Dictionary<char, Piece> PieceHash = new Dictionary<char, Piece>
	{
		{
			'P',
			Piece.BFU
		},
		{
			'L',
			Piece.BKYO
		},
		{
			'N',
			Piece.BKEI
		},
		{
			'S',
			Piece.BGIN
		},
		{
			'G',
			Piece.BKIN
		},
		{
			'B',
			Piece.BKAK
		},
		{
			'R',
			Piece.BHI
		},
		{
			'K',
			Piece.BOU
		},
		{
			'H',
			Piece.BUMA
		},
		{
			'D',
			Piece.BRYU
		}
	};

	protected static readonly Dictionary<MoveType, string> ResultHash = new Dictionary<MoveType, string>
	{
		{
			MoveType.ResultFlag,
			"Resign"
		},
		{
			MoveType.Stop,
			"Stop"
		},
		{
			MoveType.Repetition,
			"Repetition"
		},
		{
			MoveType.Draw,
			"Draw"
		},
		{
			MoveType.Timeout,
			"Timeout"
		},
		{
			MoveType.Mate,
			"Mate"
		},
		{
			MoveType.NonMate,
			"NoMate"
		},
		{
			MoveType.LoseFoul,
			"Lose Foul"
		},
		{
			MoveType.WinFoul,
			"Win Foul"
		},
		{
			MoveType.WinNyugyoku,
			"Win entering king"
		},
		{
			MoveType.RepeSup,
			"Superior"
		},
		{
			MoveType.RepeInf,
			"Inferior"
		}
	};

	private int writeMoveCount;

	private int spaceCount;

	private void Init()
	{
		writeMoveCount = 0;
		spaceCount = 0;
	}

	public string ToString(SNotation notation, RankStyle rankStyle)
	{
		string empty = string.Empty;
		Init();
		using StringWriter stringWriter = new StringWriter();
		SaveToWriter(notation, stringWriter, rankStyle);
		return stringWriter.ToString();
	}

	public virtual string ToBodString(SNotation notation, RankStyle rankStyle)
	{
		string empty = string.Empty;
		Init();
		using StringWriter stringWriter = new StringWriter();
		WriteBoard(notation.Position, notation.Handicap, stringWriter, rankStyle);
		return stringWriter.ToString();
	}

	private void SaveToWriter(SNotation notation, TextWriter wr, RankStyle rankStyle)
	{
		foreach (DictionaryEntry kifuInfo in notation.KifuInfos)
		{
			if (!(kifuInfo.Key.ToString() == "場所") && !(kifuInfo.Key.ToString() == "持ち時間"))
			{
				wr.WriteLine("{0}:{1}", kifuInfo.Key, kifuInfo.Value);
			}
		}
		if (notation.IsOutputInitialPosition)
		{
			WriteBoard(notation.InitialPosition, notation.Handicap, wr, rankStyle);
		}
		else if (notation.Handicap != Handicap.HIRATE)
		{
			wr.WriteLine("Handicap:{0}", notation.Handicap.ToKifuString());
		}
		wr.WriteLine("Black:{0}", notation.BlackName);
		wr.WriteLine("White:{0}", notation.WhiteName);
		foreach (DictionaryEntry kifuInfo2 in notation.KifuInfos)
		{
			if (kifuInfo2.Key.ToString() == "場所" || kifuInfo2.Key.ToString() == "持ち時間")
			{
				wr.WriteLine("{0}:{1}", kifuInfo2.Key, kifuInfo2.Value);
			}
		}
		_ = notation.MoveCurrent;
		SNotation sNotation = new SNotation(notation);
		sNotation.First();
		WriteMove(sNotation, sNotation.MoveCurrent, wr, rankStyle);
	}

	protected void WriteBoard(SPosition pos, Handicap handicap, TextWriter wr, RankStyle rankStyle)
	{
		string arg = StrFromHand(pos.WhiteHand);
		wr.WriteLine("White in hand：{0}", arg);
		wr.WriteLine("  9  8  7  6  5  4  3  2  1");
		wr.WriteLine("+---------------------------+");
		for (int i = 0; i < 9; i++)
		{
			wr.Write("|");
			for (int j = 0; j < 9; j++)
			{
				Piece piece = pos.GetPiece(j, i);
				if (piece == Piece.NoPiece)
				{
					wr.Write(" * ");
					continue;
				}
				if (piece.ColorOf() == PlayerColor.White)
				{
					wr.Write("w");
				}
				else
				{
					wr.Write("b");
				}
				string text = GetPieceStr(piece);
				if (text.Length == 1)
				{
					text += " ";
				}
				wr.Write(text);
			}
			wr.WriteLine("|{0}", RankStr(rankStyle, i + 1));
		}
		wr.WriteLine("+---------------------------+");
		arg = StrFromHand(pos.BlackHand);
		wr.WriteLine("Black in hand：{0}", arg);
		if (pos.Turn == PlayerColor.White)
		{
			wr.WriteLine("White to Move");
		}
	}

	private void WriteMove(SNotation notation, MoveNode move_info, TextWriter wr, RankStyle rankStyle)
	{
		if (move_info.Number != 0)
		{
			if (writeMoveCount >= 8)
			{
				wr.WriteLine(string.Empty);
				writeMoveCount = 0;
			}
			if (writeMoveCount != 0 && spaceCount != 0)
			{
				for (int i = 0; i < spaceCount; i++)
				{
					wr.Write(" ");
				}
			}
			string text = MoveString(move_info, notation.Position, rankStyle);
			if (text != string.Empty)
			{
				wr.Write("{0}{1}", (notation.Position.Turn == PlayerColor.Black) ? "▲" : "△", text);
			}
			writeMoveCount++;
			if (text.Length >= 10)
			{
				spaceCount = 1;
			}
			else
			{
				spaceCount = 10 - text.Length;
			}
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
				wr.WriteLine("variation:{0}", move_info.Children[k].Number);
			}
			WriteMove(notation, move_info.Children[k], wr, rankStyle);
		}
		notation.MoveParent();
	}

	public static string MoveString(MoveData move_data, SPosition pos, RankStyle rankStyle = RankStyle.Number)
	{
		string text = string.Empty;
		if (move_data.MoveType == MoveType.Pass)
		{
			text = "pass";
		}
		else if (move_data.MoveType.IsMove())
		{
			text = GetPieceStr(move_data.Piece);
			if (!move_data.MoveType.HasFlag(MoveType.DropFlag) && pos != null && move_data.GetAction(pos) != MoveAction.None)
			{
				int dan = move_data.FromSquare.DanOf();
				int num = move_data.FromSquare.SujiOf();
				text += $"({num}{RankStr(rankStyle, dan)})";
			}
			text = (move_data.MoveType.HasFlag(MoveType.DropFlag) ? (text + "*") : ((!move_data.MoveType.HasFlag(MoveType.Capture)) ? (text + "-") : (text + "x")));
			if (!move_data.MoveType.HasFlag(MoveType.Same))
			{
				int dan = move_data.ToSquare.DanOf();
				int num = move_data.ToSquare.SujiOf();
				text += $"{num}{RankStr(rankStyle, dan)}";
			}
			if (move_data.MoveType.HasFlag(MoveType.MoveMask))
			{
				text += "+";
			}
			else if (move_data.IsNotPromotion())
			{
				text += "=";
			}
		}
		else if (move_data.MoveType.IsResult())
		{
			if (ResultHash.ContainsKey(move_data.MoveType))
			{
				text = ResultHash[move_data.MoveType];
			}
		}
		else
		{
			text = string.Empty;
		}
		return text;
	}

	public static string MoveString(MoveDataEx move_data, RankStyle rankStyle = RankStyle.Number)
	{
		string text = string.Empty;
		if (move_data.MoveType == MoveType.Pass)
		{
			text = "pass";
		}
		else if (move_data.MoveType.IsMove())
		{
			text = GetPieceStr(move_data.Piece);
			if (!move_data.MoveType.HasFlag(MoveType.DropFlag) && move_data.Action != MoveAction.None)
			{
				int dan = move_data.FromSquare.DanOf();
				int num = move_data.FromSquare.SujiOf();
				text += $"({num}{RankStr(rankStyle, dan)})";
			}
			text = (move_data.MoveType.HasFlag(MoveType.DropFlag) ? (text + "*") : ((!move_data.MoveType.HasFlag(MoveType.Capture)) ? (text + "-") : (text + "x")));
			if (!move_data.MoveType.HasFlag(MoveType.Same))
			{
				int dan = move_data.ToSquare.DanOf();
				int num = move_data.ToSquare.SujiOf();
				text += $"{num}{RankStr(rankStyle, dan)}";
			}
			if (move_data.MoveType.HasFlag(MoveType.MoveMask))
			{
				text += "+";
			}
			else if (move_data.MoveType.HasFlag(MoveType.Unpromotion))
			{
				text += "=";
			}
		}
		else if (move_data.MoveType.IsResult())
		{
			if (ResultHash.ContainsKey(move_data.MoveType))
			{
				text = ResultHash[move_data.MoveType];
			}
		}
		else
		{
			text = string.Empty;
		}
		return text;
	}

	public static string GetPieceStr(Piece piece)
	{
		int num = (int)piece.TypeOf();
		if (piece.IsPromoted())
		{
			return PiecePromotionStr[num];
		}
		return PieceChar[num].ToString();
	}

	protected static string StrFromHand(int[] hand)
	{
		string text = string.Empty;
		int num = 0;
		foreach (int num2 in hand)
		{
			num += num2;
		}
		if (num == 0)
		{
			return "nothing";
		}
		for (int num3 = 8; num3 >= 0; num3--)
		{
			int num4 = hand[num3];
			if (num4 != 0)
			{
				text += PieceChar[num3];
				if (num4 > 1)
				{
					text += num4;
				}
				text += " ";
			}
		}
		return text;
	}

	public static bool IsEnglishMove(string str)
	{
		if (str[0] == '+' || IsPieceChar(str[0]))
		{
			return true;
		}
		return false;
	}

	public static bool IsPieceChar(char piece)
	{
		return PieceHash.ContainsKey(piece);
	}

	public static Piece PieceFromChar(char ch)
	{
		Piece result = Piece.NoPiece;
		if (PieceHash.ContainsKey(ch))
		{
			result = PieceHash[ch];
		}
		return result;
	}

	public static int numberFromChar(char ch)
	{
		if (ch >= '0' && ch <= '9')
		{
			return ch - 48;
		}
		if (ch >= 'a' && ch <= 'i')
		{
			return ch - 97 + 1;
		}
		return Array.IndexOf(KanNumber, ch);
	}

	public static char GetAlphaetRank(int rank)
	{
		return (char)(97 + (rank - 1));
	}

	public static string RankStr(RankStyle style, int dan)
	{
		return style switch
		{
			RankStyle.Alphabet => GetAlphaetRank(dan).ToString(), 
			RankStyle.ChineseNumerals => KanNumber[dan].ToString(), 
			_ => dan.ToString(), 
		};
	}

	public static MoveType ResultMoveTypeFromStr(string str)
	{
		MoveType result = MoveType.NoMove;
		foreach (KeyValuePair<MoveType, string> item in ResultHash)
		{
			if (item.Value == str)
			{
				return item.Key;
			}
		}
		return result;
	}

	public static bool Parse(SNotation notation, MoveData move_data, string move_str)
	{
		int num = 0;
		if (move_str.Length < 2)
		{
			return false;
		}
		bool flag = false;
		if (move_str[0] == '+')
		{
			flag = true;
			num++;
		}
		Piece piece = PieceFromChar(move_str[num]);
		if (piece == Piece.NoPiece)
		{
			return false;
		}
		move_data.Piece |= piece;
		num++;
		if (flag)
		{
			move_data.Piece |= Piece.BOU;
		}
		move_data.MoveType = MoveType.MoveFlag;
		bool flag2 = false;
		bool flag3 = false;
		if (num < move_str.Length && move_str[num] == '(')
		{
			if (num + 4 > move_str.Length)
			{
				return false;
			}
			int num2 = numberFromChar(move_str[num + 1]);
			int num3 = numberFromChar(move_str[num + 2]);
			if (num2 <= 0 || num3 <= 0)
			{
				return false;
			}
			flag3 = true;
			move_data.FromSquare = Square.Make(num2.ToFile(), num3.ToRank());
			num += 4;
		}
		if (num < move_str.Length)
		{
			if (move_str[num] == '*')
			{
				move_data.MoveType |= MoveType.DropFlag;
			}
			else if (move_str[num] == 'x')
			{
				flag2 = true;
			}
			num++;
		}
		if (num + 2 > move_str.Length)
		{
			if (flag2)
			{
				if (notation.MoveCurrent.MoveType.IsMoveWithoutPass())
				{
					move_data.ToSquare = notation.MoveCurrent.ToSquare;
				}
				else
				{
					move_data.ToSquare = notation.Position.MoveLast.ToSquare;
				}
			}
		}
		else
		{
			int num4 = numberFromChar(move_str[num]);
			int num5 = numberFromChar(move_str[num + 1]);
			if (num4 <= 0 || num5 <= 0)
			{
				return false;
			}
			move_data.ToSquare = Square.Make(num4.ToFile(), num5.ToRank());
			num += 2;
		}
		if (num < move_str.Length)
		{
			if (move_str[num] == '+')
			{
				move_data.MoveType |= MoveType.MoveMask;
			}
			num++;
		}
		if (!move_data.MoveType.HasFlag(MoveType.DropFlag) && !flag3 && !NotationUtility.GetMoveFromPosition(notation.Position, move_data, MoveAbsPos.NONE, MoveOperation.NONE))
		{
			return false;
		}
		return true;
	}
}
