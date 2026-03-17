using ShogiDroid;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Android.App;
using AppDebug;

namespace ShogiLib;

public class Csa
{
	private class Koma
	{
		public int Dan;

		public int Suji;

		public Piece Piece;
	}

	private class Komabako
	{
		private int[] komabako = new int[9] { 0, 18, 4, 4, 4, 4, 2, 2, 2 };

		public void Clear()
		{
			for (int i = 0; i < komabako.Length; i++)
			{
				komabako[i] = 0;
			}
		}

		public bool HasPiece(PieceType type)
		{
			return komabako[(uint)type] != 0;
		}

		public void AddPiece(PieceType type)
		{
			komabako[(uint)type]++;
		}

		public void DelPiece(PieceType type)
		{
			komabako[(uint)type]--;
		}

		public int Get(PieceType type)
		{
			return komabako[(uint)type];
		}

		public void Set(PieceType type, int num)
		{
			komabako[(uint)type] = num;
		}
	}

	private static readonly Dictionary<string, Piece> PieceTable = new Dictionary<string, Piece>
	{
		{
			"FU",
			Piece.BFU
		},
		{
			"KY",
			Piece.BKYO
		},
		{
			"KE",
			Piece.BKEI
		},
		{
			"GI",
			Piece.BGIN
		},
		{
			"KI",
			Piece.BKIN
		},
		{
			"KA",
			Piece.BKAK
		},
		{
			"HI",
			Piece.BHI
		},
		{
			"OU",
			Piece.BOU
		},
		{
			"TO",
			Piece.BTO
		},
		{
			"NY",
			Piece.BNKYO
		},
		{
			"NK",
			Piece.BNKEI
		},
		{
			"NG",
			Piece.BNGIN
		},
		{
			"UM",
			Piece.BUMA
		},
		{
			"RY",
			Piece.BRYU
		}
	};

	private static readonly Dictionary<string, MoveType> ResultTable = new Dictionary<string, MoveType>
	{
		{
			"%TORYO",
			MoveType.ResultFlag
		},
		{
			"%CHUDAN",
			MoveType.Stop
		},
		{
			"%SENNICHITE",
			MoveType.Repetition
		},
		{
			"%TIME_UP",
			MoveType.Timeout
		},
		{
			"%JISHOGI",
			MoveType.Draw
		},
		{
			"%KACHI",
			MoveType.WinNyugyoku
		},
		{
			"%HIKIWAKE",
			MoveType.Draw
		},
		{
			"%TSUMI",
			MoveType.Mate
		},
		{
			"%FUZUMI",
			MoveType.NonMate
		},
		{
			"%+ILLEGAL_ACTION",
			MoveType.WinFoul
		},
		{
			"%-ILLEGAL_ACTION",
			MoveType.WinFoul
		},
		{
			"%ILLEGAL_MOVE",
			MoveType.LoseFoul
		},
		{
			"%DRAW_SENNICHI",
			MoveType.Draw
		},
		{
			"%DRAW_PLY_LIMIT",
			MoveType.Draw
		},
		{
			"%DRAW_INVALID",
			MoveType.Draw
		},
		{
			"%SENTE_WIN_TORYO",
			MoveType.ResultFlag
		},
		{
			"%SENTE_WIN_CHECKMATE",
			MoveType.Mate
		},
		{
			"%SENTE_WIN_TIMEOUT",
			MoveType.Timeout
		},
		{
			"%SENTE_WIN_DISCONNECT",
			MoveType.Timeout
		},
		{
			"%SENTE_WIN_ENTERINGKING",
			MoveType.WinNyugyoku
		},
		{
			"%SENTE_WIN_OUTE_SENNICHI",
			MoveType.WinFoul
		},
		{
			"%GOTE_WIN_TORYO",
			MoveType.ResultFlag
		},
		{
			"%GOTE_WIN_CHECKMATE",
			MoveType.Mate
		},
		{
			"%GOTE_WIN_TIMEOUT",
			MoveType.Timeout
		},
		{
			"%GOTE_WIN_DISCONNECT",
			MoveType.Timeout
		},
		{
			"%GOTE_WIN_ENTERINGKING",
			MoveType.WinNyugyoku
		},
		{
			"%GOTE_WIN_OUTE_SENNICHI",
			MoveType.WinFoul
		},
		{
			"%SENNICHITE SUPERIOR",
			MoveType.RepeSup
		},
		{
			"%SENNICHITE INFERIOR",
			MoveType.RepeInf
		}
	};

	private int lineNo;

	private Komabako komabako;

	private int blackRemain;

	private int whiteRemain;

	private static string gameText = Application.Context.GetString(Resource.String.Game_Text);

	private static string valueText = Application.Context.GetString(Resource.String.Value_Text);

	private static string movesText = Application.Context.GetString(Resource.String.Moves_Text);

	public Csa()
	{
		lineNo = 0;
		komabako = new Komabako();
	}

	public static bool IsCsa(string str)
	{
		bool result = true;
		int num = 0;
		using (StringReader stringReader = new StringReader(str))
		{
			string text;
			while ((text = stringReader.ReadLine()) != null)
			{
				if (!string.IsNullOrEmpty(text))
				{
					if (text[0] == '*')
					{
						result = false;
						break;
					}
					if (text[0] == '\'' || text[0] == '$' || text[0] == 'P' || text[0] == 'N')
					{
						num++;
					}
				}
			}
		}
		if (num < 2)
		{
			result = false;
		}
		return result;
	}

	public void Load(SNotation notation, string filename)
	{
		using StreamReader tr = new StreamReader(filename, Encoding.GetEncoding(932));
		Load(notation, tr);
	}

	public void Load(SNotation notation, Stream stream)
	{
		using StreamReader tr = new StreamReader(stream, Encoding.GetEncoding(932));
		Load(notation, tr);
	}

	public void LoadFromString(SNotation notation, string str)
	{
		using StringReader tr = new StringReader(str);
		Load(notation, tr);
	}

	public void LoadCsa1(SNotation notation, string str, int result)
	{
		char c = '+';
		for (int i = 0; i + 6 <= str.Length; i += 6)
		{
			string line = c + str.Substring(i, 6);
			ParseMove(notation, line);
			c = ((c == '+') ? '-' : '+');
		}
		if (result == 1 || result == 2)
		{
			notation.AddMove(new MoveDataEx(MoveType.ResultFlag));
		}
		notation.InitHashKey();
		notation.ChangeCurrent(0);
	}

	private void Load(SNotation notation, TextReader tr)
	{
		notation.Init();
		notation.InitialPosition.BoardClear();
		LoadFromReader(notation, tr);
		notation.InitHashKey();
		notation.DecisionHandicap();
		notation.ChangeCurrent(0);
	}

	private void LoadFromReader(SNotation notation, TextReader tr)
	{
		string text;
		while ((text = tr.ReadLine()) != null)
		{
			lineNo++;
			if (text.StartsWith("'*"))
			{
				string text2 = CommentFromComment(notation, text.Substring(3));
				if (string.IsNullOrEmpty(text2))
				{
					notation.MoveCurrent.CommentAdd(text.Substring(2));
				}
				else
				{
					notation.MoveCurrent.CommentAdd(text2);
				}
			}
			else if (!text.StartsWith("'") && !string.IsNullOrEmpty(text))
			{
				string[] array = text.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string line in array)
				{
					Parse(notation, line);
				}
			}
		}
	}

	private void Parse(SNotation notation, string line)
	{
		if (line.StartsWith("V"))
		{
			return;
		}
		if (line.StartsWith("N"))
		{
			ParseName(notation, line);
		}
		else if (line.StartsWith("$"))
		{
			ParseInfo(notation, line);
		}
		else if (line.StartsWith("P"))
		{
			ParsePosition(notation, line);
			notation.InitHashKey();
		}
		else if (line.StartsWith("+") || line.StartsWith("-"))
		{
			if (line.Length == 1)
			{
				notation.InitialPosition.Turn = ((line[0] != '+') ? PlayerColor.White : PlayerColor.Black);
			}
			else
			{
				ParseMove(notation, line);
			}
		}
		else if (line.StartsWith("T"))
		{
			ParseTime(notation, line);
		}
		else if (line.StartsWith("L"))
		{
			ParseRemain(notation, line);
		}
		else if (line.StartsWith("%"))
		{
			ParseResult(notation, line);
		}
	}

	private void ParseName(SNotation notation, string line)
	{
		if (line.Length < 2)
		{
			Log.Warning("CSAの名前が変？ " + lineNo + " " + line);
			return;
		}
		bool flag = false;
		if (line[1] == '+')
		{
			flag = true;
		}
		else
		{
			if (line[1] != '-')
			{
				Log.Warning("CSAの名前が変？ " + lineNo + " " + line);
				return;
			}
			flag = false;
		}
		string text = string.Empty;
		if (line.Length > 2)
		{
			text = line.Substring(2);
		}
		if (flag)
		{
			notation.BlackName = text;
		}
		else
		{
			notation.WhiteName = text;
		}
	}

	private void ParseInfo(SNotation notation, string line)
	{
		int num = line.IndexOfAny(new char[2] { '：', ':' });
		string text;
		string text2;
		if (num == -1)
		{
			text = line;
			text2 = string.Empty;
		}
		else
		{
			text = line.Substring(0, num);
			text2 = ((line.Length <= num + 1) ? string.Empty : line.Substring(num + 1));
		}
		switch (text)
		{
		case "$EVENT":
			text = "棋戦";
			break;
		case "$SITE":
			text = "場所";
			break;
		case "$START_TIME":
			text = "開始日時";
			break;
		case "$END_TIME":
			text = "終了日時";
			break;
		case "$TIME_LIMIT":
		{
			text = "持ち時間";
			int time = 0;
			int time2 = 0;
			int num2 = parse_time(text2, 0, out time);
			if (num2 < text2.Length && text2[num2] == '+')
			{
				parse_time(text2, num2 + 1, out time2);
			}
			blackRemain = time * 60;
			whiteRemain = time * 60;
			if (time == 0 && time2 == 0)
			{
				break;
			}
			text2 = string.Empty;
			if (time != 0)
			{
				if (time >= 60)
				{
					int num3 = time / 60;
					text2 = text2 + num3 + "時間";
					time -= num3 * 60;
				}
				if (time != 0)
				{
					text2 = text2 + time + "分";
				}
			}
			text2 = ((time2 == 0) ? (text2 + "切れ負け") : (text2 + "秒読み" + time2 + "秒"));
			break;
		}
		case "$OPENING":
			text = "戦型";
			break;
		}
		notation.KifuInfos.Add(text, text2);
	}

	private void ParsePosition(SNotation notation, string line)
	{
		if (line.Length < 2)
		{
			Log.Warning("ポジションエラー?" + lineNo + " " + line);
		}
		else if (line[1] == 'I')
		{
			notation.InitialPosition.Init();
			komabako.Clear();
			Koma koma = new Koma();
			for (int i = 2; i < line.Length; i += 4)
			{
				if (!ParseKomaPos(line, i, koma))
				{
					continue;
				}
				if (koma.Dan == 0 || koma.Suji == 0)
				{
					Log.Warning("ポジションエラー?" + lineNo + " " + line);
					continue;
				}
				int file = koma.Suji.ToFile();
				int rank = koma.Dan.ToRank();
				Piece piece = notation.InitialPosition.GetPiece(file, rank);
				if (piece == Piece.NoPiece || piece.TypeOf() != koma.Piece.TypeOf())
				{
					Log.Warning("ポジションエラー?" + lineNo + " " + line);
					continue;
				}
				notation.InitialPosition.SetPiece(file, rank, Piece.NoPiece);
				komabako.AddPiece(piece.TypeOf());
			}
		}
		else if (line[1] == 'S')
		{
			string sfen = line.Substring(2);
			Sfen.LoadPosition(notation.InitialPosition, sfen);
		}
		else if (line[1] >= '1' && line[1] <= '9')
		{
			int rank2 = line[1] - 49;
			int j = 0;
			for (int k = 2; k + 3 <= line.Length && j < 9; k += 3, j++)
			{
				bool flag = false;
				if (line[k] == '+')
				{
					flag = true;
				}
				else
				{
					if (line[k] != '-')
					{
						continue;
					}
					flag = false;
				}
				Piece piece2 = ParseKoma(line, k + 1);
				if (piece2 == Piece.NoPiece)
				{
					continue;
				}
				if (komabako.HasPiece(piece2.TypeOf()))
				{
					if (!flag)
					{
						piece2 |= Piece.WhiteFlag;
					}
					notation.InitialPosition.SetPiece(j, rank2, piece2);
					komabako.DelPiece(piece2.TypeOf());
				}
				else
				{
					Log.Warning("駒箱になくね？" + lineNo + " " + line);
				}
			}
		}
		else
		{
			if (line[1] != '+' && line[1] != '-')
			{
				return;
			}
			bool flag2 = false;
			if (line[1] == '+')
			{
				flag2 = true;
			}
			Koma koma2 = new Koma();
			for (int l = 2; l + 4 <= line.Length; l += 4)
			{
				if (!ParseKomaPos(line, l, koma2))
				{
					continue;
				}
				if (koma2.Dan == 0 || koma2.Suji == 0)
				{
					if (line[l + 2] == 'A' && line[l + 3] == 'L')
					{
						PieceType pieceType = PieceType.FU;
						while ((int)pieceType < 8)
						{
							int num = komabako.Get(pieceType);
							if (num != 0)
							{
								komabako.Set(pieceType, 0);
								if (flag2)
								{
									num += notation.InitialPosition.GetBlackHand(pieceType);
									notation.InitialPosition.SetBlackHand(pieceType, num);
								}
								else
								{
									num += notation.InitialPosition.GetWhiteHand(pieceType);
									notation.InitialPosition.SetWhiteHand(pieceType, num);
								}
							}
							pieceType++;
						}
						continue;
					}
					if (koma2.Piece != Piece.NoPiece)
					{
						if (flag2)
						{
							int blackHand = notation.InitialPosition.GetBlackHand(koma2.Piece.ToHandIndex());
							notation.InitialPosition.SetBlackHand(koma2.Piece.ToHandIndex(), blackHand + 1);
						}
						else
						{
							int blackHand = notation.InitialPosition.GetWhiteHand(koma2.Piece.ToHandIndex());
							notation.InitialPosition.SetWhiteHand(koma2.Piece.ToHandIndex(), blackHand + 1);
						}
					}
					komabako.DelPiece(koma2.Piece.TypeOf());
					continue;
				}
				int file2 = koma2.Suji.ToFile();
				int rank3 = koma2.Dan.ToRank();
				Piece piece3 = notation.InitialPosition.GetPiece(file2, rank3);
				if (piece3 != Piece.NoPiece || piece3.TypeOf() != koma2.Piece.TypeOf())
				{
					Log.Warning("ポジションエラー?" + lineNo + " " + line);
					continue;
				}
				if (!flag2)
				{
					piece3 |= Piece.WhiteFlag;
				}
				notation.InitialPosition.SetPiece(file2, rank3, piece3);
				komabako.DelPiece(piece3.TypeOf());
			}
		}
	}

	private void ParseMove(SNotation notation, string line)
	{
		Koma koma = new Koma();
		if (line.Length < 7)
		{
			return;
		}
		PlayerColor playerColor = ((line[0] != '+') ? PlayerColor.White : PlayerColor.Black);
		if (playerColor != notation.Position.Turn)
		{
			Log.Warning("指し手エラー?" + lineNo + " " + line);
			return;
		}
		if (!ParsePos(line, 1, koma))
		{
			Log.Warning("指し手エラー?" + lineNo + " " + line);
			return;
		}
		MoveDataEx moveDataEx = new MoveDataEx();
		if (koma.Dan == 0 && koma.Suji == 0)
		{
			moveDataEx.MoveType = MoveType.DropFlag;
		}
		else
		{
			moveDataEx.MoveType = MoveType.MoveFlag;
			moveDataEx.FromSquare = Square.Make(koma.Suji.ToFile(), koma.Dan.ToRank());
		}
		if (!ParseKomaPos(line, 3, koma))
		{
			Log.Warning("指し手エラー?" + lineNo + " " + line);
			return;
		}
		if (koma.Piece == Piece.NoPiece)
		{
			Log.Warning("指し手エラー?" + lineNo + " " + line);
			return;
		}
		moveDataEx.ToSquare = Square.Make(koma.Suji.ToFile(), koma.Dan.ToRank());
		moveDataEx.Piece = koma.Piece | PieceExtensions.PieceFlagFromColor(notation.Position.Turn);
		if (koma.Piece.IsPromoted() && moveDataEx.MoveType.HasFlag(MoveType.MoveFlag))
		{
			Piece piece = notation.Position.GetPiece(moveDataEx.FromSquare);
			if (piece.ColorOf() == playerColor && !piece.IsPromoted() && piece.TypeOf() == moveDataEx.Piece.TypeOf())
			{
				moveDataEx.Piece &= (Piece)247;
				moveDataEx.MoveType |= MoveType.MoveMask;
			}
		}
		if (notation.AddMove(moveDataEx))
		{
			return;
		}
		throw new NotationException("指し手エラー", line, lineNo);
	}

	private void ParseTime(SNotation notation, string line)
	{
		if (int.TryParse(line.Substring(1), out var result))
		{
			notation.MoveCurrent.Time = result;
			notation.MoveCurrent.TotalTime += result;
		}
	}

	private void ParseRemain(SNotation notation, string line)
	{
		if (int.TryParse(line.Substring(1), out var result))
		{
			int num;
			if (notation.MoveCurrent.Turn == PlayerColor.Black)
			{
				num = blackRemain - result;
				blackRemain = result;
			}
			else
			{
				num = whiteRemain - result;
				whiteRemain = result;
			}
			if (num >= 0)
			{
				notation.MoveCurrent.Time = num;
				notation.MoveCurrent.TotalTime += num;
			}
		}
	}

	private void ParseResult(SNotation notation, string line)
	{
		if (ResultTable.ContainsKey(line))
		{
			MoveDataEx moveData = new MoveDataEx(ResultTable[line]);
			if (!notation.AddMove(moveData))
			{
				throw new NotationException("指し手エラー", line, lineNo);
			}
		}
		else
		{
			Log.Warning("Resultエラー " + line + " " + lineNo);
		}
	}

	public static MoveData ParseResult(string line)
	{
		if (ResultTable.ContainsKey(line))
		{
			return new MoveData(ResultTable[line]);
		}
		return new MoveData(MoveType.Stop);
	}

	private static bool ParseKomaPos(string line, int pos, Koma koma)
	{
		if (pos + 4 > line.Length)
		{
			return false;
		}
		if (!ParsePos(line, pos, koma))
		{
			return false;
		}
		koma.Piece = ParseKoma(line, pos + 2);
		return true;
	}

	private static bool ParsePos(string line, int pos, Koma koma)
	{
		if (pos + 2 > line.Length)
		{
			return false;
		}
		if (line[pos] >= '0' && line[pos] <= '9')
		{
			koma.Suji = line[pos] - 48;
			if (line[pos + 1] >= '0' && line[pos + 1] <= '9')
			{
				koma.Dan = line[pos + 1] - 48;
				return true;
			}
			return false;
		}
		return false;
	}

	private static Piece ParseKoma(string line, int pos)
	{
		Piece result = Piece.NoPiece;
		if (pos + 2 > line.Length)
		{
			return Piece.NoPiece;
		}
		string key = line.Substring(pos, 2);
		if (PieceTable.ContainsKey(key))
		{
			result = PieceTable[key];
		}
		return result;
	}

	private string CommentFromComment(SNotation notation, string line)
	{
		string text = "*" + gameText;
		CsaCommentTokenizer csaCommentTokenizer = new CsaCommentTokenizer(line);
		string text2 = csaCommentTokenizer.Token();
		if (text2 != string.Empty)
		{
			if (!CsaCommentTokenizer.ParseNum(text2, out var outnum))
			{
				return null;
			}
			notation.MoveCurrent.Eval = outnum;
			text += $" {valueText} {outnum}";
		}
		SPosition sPosition = (SPosition)notation.Position.Clone();
		text = text + " " + movesText;
		while ((text2 = csaCommentTokenizer.Token()) != string.Empty && text2.Length >= 4)
		{
			MoveData moveData = ParseMove(sPosition, text2);
			text += " ";
			text += ((moveData.Turn == PlayerColor.Black) ? "▲" : "△");
			text += Kifu.GetMoveString(moveData);
			if (moveData.MoveType.IsMove() && MoveCheck.IsValid(sPosition, moveData))
			{
				sPosition.Move(moveData);
			}
		}
		return text;
	}

	public static MoveData ParseMove(SPosition position, string str)
	{
		MoveData moveData = new MoveData();
		Koma koma = new Koma();
		if (str.Length < 7)
		{
			return moveData;
		}
		PlayerColor playerColor = ((str[0] != '+') ? PlayerColor.White : PlayerColor.Black);
		if (!ParsePos(str, 1, koma))
		{
			return moveData;
		}
		if (koma.Dan == 0 && koma.Suji == 0)
		{
			moveData.MoveType = MoveType.DropFlag;
		}
		else
		{
			moveData.MoveType = MoveType.MoveFlag;
			moveData.FromSquare = Square.Make(koma.Suji.ToFile(), koma.Dan.ToRank());
		}
		if (!ParseKomaPos(str, 3, koma))
		{
			return moveData;
		}
		if (koma.Piece == Piece.NoPiece)
		{
			return moveData;
		}
		moveData.ToSquare = Square.Make(koma.Suji.ToFile(), koma.Dan.ToRank());
		moveData.Piece = koma.Piece | PieceExtensions.PieceFlagFromColor(playerColor);
		if (position.GetPiece(moveData.ToSquare) != Piece.NoPiece)
		{
			moveData.MoveType |= MoveType.Capture;
			moveData.CapturePiece = position.GetPiece(moveData.ToSquare);
		}
		if (koma.Piece.IsPromoted() && moveData.MoveType.HasFlag(MoveType.MoveFlag))
		{
			Piece piece = position.GetPiece(moveData.FromSquare);
			if (piece.ColorOf() == playerColor && !piece.IsPromoted() && piece.TypeOf() == moveData.Piece.TypeOf())
			{
				moveData.Piece &= (Piece)247;
				moveData.MoveType |= MoveType.MoveMask;
			}
		}
		return moveData;
	}

	public static string ToMoveString(MoveData moveData)
	{
		string result = string.Empty;
		if (moveData.MoveType.IsMoveWithoutPass())
		{
			Piece piece = moveData.Piece;
			if (moveData.MoveType.HasFlag(MoveType.MoveMask))
			{
				piece |= Piece.BOU;
			}
			result = ((!moveData.MoveType.HasFlag(MoveType.DropFlag)) ? $"{ToTurnChar(moveData.Turn)}{moveData.FromSquare.SujiOf()}{moveData.FromSquare.DanOf()}{moveData.ToSquare.SujiOf()}{moveData.ToSquare.DanOf()}{ToPieceString(piece)}" : $"{ToTurnChar(moveData.Turn)}00{moveData.ToSquare.SujiOf()}{moveData.ToSquare.DanOf()}{ToPieceString(piece)}");
		}
		else if (moveData.MoveType.IsResult())
		{
			result = ToResultString(moveData.MoveType);
		}
		return result;
	}

	protected static char ToTurnChar(PlayerColor color)
	{
		if (color != PlayerColor.Black)
		{
			return '-';
		}
		return '+';
	}

	protected static string ToPieceString(Piece piece)
	{
		if (piece.IsWhite())
		{
			piece = piece.Opp();
		}
		foreach (KeyValuePair<string, Piece> item in PieceTable)
		{
			if (item.Value == piece)
			{
				return item.Key;
			}
		}
		return string.Empty;
	}

	protected static string ToResultString(MoveType result)
	{
		foreach (KeyValuePair<string, MoveType> item in ResultTable)
		{
			if (item.Value == result)
			{
				return item.Key;
			}
		}
		return string.Empty;
	}

	private static int parse_time(string str, int start, out int time)
	{
		time = 0;
		int i;
		for (i = start; i < str.Length; i++)
		{
			int num = str[i];
			if (num >= 48 && num <= 57)
			{
				time *= 10;
				time += num - 48;
				continue;
			}
			if (num != 58)
			{
				break;
			}
			time *= 60;
		}
		return i;
	}
}
