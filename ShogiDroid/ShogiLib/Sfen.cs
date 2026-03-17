using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AppDebug;

namespace ShogiLib;

public static class Sfen
{
	private static readonly Dictionary<char, PieceType> CharToPieceHashtable = new Dictionary<char, PieceType>
	{
		{
			'K',
			PieceType.OU
		},
		{
			'R',
			PieceType.HI
		},
		{
			'B',
			PieceType.KAK
		},
		{
			'G',
			PieceType.KIN
		},
		{
			'S',
			PieceType.GIN
		},
		{
			'N',
			PieceType.KEI
		},
		{
			'L',
			PieceType.KYO
		},
		{
			'P',
			PieceType.FU
		}
	};

	public static bool IsSfen(string str)
	{
		bool result = false;
		if (string.IsNullOrEmpty(str))
		{
			return false;
		}
		if (str.StartsWith("position") || str.StartsWith("sfen") || str.StartsWith("startpos") || str.StartsWith("moves"))
		{
			result = true;
		}
		else if (str.Length < 100)
		{
			result = true;
			string text = "KRBGSNLPkrgbsnlp0123456789wb/-+ ";
			foreach (char value in str)
			{
				if (text.IndexOf(value) < 0)
				{
					result = false;
					break;
				}
			}
		}
		return result;
	}

	public static void Load(SNotation notation, string filename)
	{
		notation.Init();
		using StreamReader streamReader = new StreamReader(filename);
		string str = streamReader.ReadLine();
		ReadNotation(notation, str);
	}

	public static void Load(SNotation notation, Stream stream)
	{
		notation.Init();
		using StreamReader streamReader = new StreamReader(stream);
		string str = streamReader.ReadLine();
		ReadNotation(notation, str);
	}

	public static void LoadNotation(SNotation notation, string sfen)
	{
		notation.Init();
		ReadNotation(notation, sfen);
	}

	public static void LoadPosition(SNotation notation, string sfen)
	{
		notation.Init();
		ReadPosition(notation, sfen);
		notation.InitHashKey();
		notation.DecisionHandicap();
	}

	public static void LoadPositin(SPosition positio, string sfen)
	{
		positio.Init();
		ReadPosition(positio, sfen);
	}

	public static void LoadPosition(SPosition position, string sfen)
	{
		position.Init();
		ReadPosition(position, sfen);
		position.InitHashKey();
	}

	public static SPosition loadPosition(string sfen)
	{
		SPosition sPosition = new SPosition();
		ReadPosition(sPosition, sfen);
		sPosition.InitHashKey();
		return sPosition;
	}

	public static void Save(SNotation notation, string filename)
	{
		using StreamWriter wr = new StreamWriter(filename, append: false, Encoding.GetEncoding(932));
		WriteNotation(notation, wr);
	}

	public static string NotationToString(SNotation notation)
	{
		string empty = string.Empty;
		using StringWriter stringWriter = new StringWriter();
		WriteNotation(notation, stringWriter);
		return stringWriter.ToString();
	}

	public static string PositionToString(this SPosition position, int num)
	{
		string empty = string.Empty;
		using StringWriter stringWriter = new StringWriter();
		WritePosition(position, stringWriter, num);
		return stringWriter.ToString();
	}

	public static string MovesToString(this SNotation notation)
	{
		string empty = string.Empty;
		using StringWriter stringWriter = new StringWriter();
		WriteMovesToCurrent(notation, stringWriter);
		return stringWriter.ToString();
	}

	public static string MovesToString(this SNotation notation, MoveData movedata)
	{
		string empty = string.Empty;
		using StringWriter stringWriter = new StringWriter();
		WriteMovesToCurrent(notation, stringWriter);
		stringWriter.Write(" ");
		WriteMove(movedata, stringWriter);
		return stringWriter.ToString();
	}

	public static string MoveToString(this MoveData moveData)
	{
		string empty = string.Empty;
		using StringWriter stringWriter = new StringWriter();
		WriteMove(moveData, stringWriter);
		return stringWriter.ToString();
	}

	private static int ReadPosition(SNotation notation, string sfen)
	{
		return ReadPosition(notation.Position, sfen);
	}

	private static int ReadPosition(SPosition pos, string sfen, int index = 0)
	{
		int num = 0;
		int num2 = 0;
		if (sfen != string.Empty)
		{
			pos.BoardClear();
		}
		while (index < sfen.Length)
		{
			int num3 = sfen[index];
			index++;
			switch (num3)
			{
			case 47:
				num = 0;
				num2++;
				if (num2 < 9)
				{
					continue;
				}
				break;
			case 48:
			case 49:
			case 50:
			case 51:
			case 52:
			case 53:
			case 54:
			case 55:
			case 56:
			case 57:
				num += num3 - 48;
				continue;
			default:
			{
				Piece piece = Piece.NoPiece;
				if (num3 == 43)
				{
					piece |= Piece.BOU;
					if (index >= sfen.Length)
					{
						break;
					}
					num3 = sfen[index];
					index++;
					if (num3 == 32)
					{
						break;
					}
				}
				if (num3 >= 97 && num3 <= 122)
				{
					piece |= Piece.WhiteFlag;
					num3 = char.ToUpper((char)num3);
				}
				if (CharToPieceHashtable.TryGetValue((char)num3, out var value))
				{
					piece = (Piece)((uint)piece | (uint)value);
				}
				else
				{
					Log.Warning("parse error");
					piece = Piece.NoPiece;
				}
				if (piece.TypeOf() != PieceType.NoPieceType && num < 9)
				{
					pos.SetPiece(num, num2, piece);
					num++;
				}
				continue;
			}
			case 32:
				break;
			}
			break;
		}
		while (index < sfen.Length)
		{
			int num3 = sfen[index];
			index++;
			switch (num3)
			{
			case 119:
				pos.Turn = PlayerColor.White;
				continue;
			case 98:
				pos.Turn = PlayerColor.Black;
				continue;
			default:
				continue;
			case 32:
				break;
			}
			break;
		}
		while (index < sfen.Length)
		{
			int num3 = sfen[index];
			index++;
			if (num3 == 32)
			{
				break;
			}
			int num4 = 1;
			if (num3 >= 48 && num3 <= 57)
			{
				num4 = num3 - 48;
				if (index >= sfen.Length)
				{
					break;
				}
				num3 = sfen[index];
				index++;
				if (num3 == 32)
				{
					break;
				}
				if (num3 >= 48 && num3 <= 57)
				{
					num4 = num4 * 10 + (num3 - 48);
					if (index >= sfen.Length)
					{
						break;
					}
					num3 = sfen[index];
					index++;
					if (num3 == 32)
					{
						break;
					}
				}
			}
			if (CharToPieceHashtable.TryGetValue(char.ToUpper((char)num3), out var value2))
			{
				if (char.IsUpper((char)num3))
				{
					pos.SetBlackHand(value2, num4);
				}
				else
				{
					pos.SetWhiteHand(value2, num4);
				}
			}
		}
		while (index < sfen.Length)
		{
			int num3 = sfen[index];
			index++;
			if (num3 == 32)
			{
				break;
			}
		}
		return index;
	}

	private static void ReadNotation(SNotation notation, string str)
	{
		Tokenizer tokenizer = new Tokenizer(str);
		string text = tokenizer.Token();
		if (text == "position")
		{
			text = tokenizer.Token();
		}
		switch (text)
		{
		case "sfen":
			ReadPosition(notation, tokenizer.TokenPosition());
			notation.InitHashKey();
			notation.DecisionHandicap();
			break;
		case "moves":
			tokenizer.Push(text);
			break;
		default:
			ReadPosition(notation, str);
			notation.InitHashKey();
			notation.DecisionHandicap();
			return;
		case "startpos":
			break;
		}
		text = tokenizer.Token();
		if (!(text == "moves"))
		{
			return;
		}
		while ((text = tokenizer.Token()) != string.Empty)
		{
			MoveDataEx moveDataEx = ParseMove(notation.Position, text);
			if (moveDataEx == null || !notation.AddMove(moveDataEx))
			{
				break;
			}
		}
	}

	public static MoveDataEx ParseMove(SPosition position, string move)
	{
		switch (move)
		{
		case "resign":
			return new MoveDataEx(MoveType.ResultFlag);
		case "win":
			return new MoveDataEx(MoveType.WinNyugyoku);
		case "draw":
			return new MoveDataEx(MoveType.Draw);
		case "pass":
		case "0000":
			return new MoveDataEx(MoveType.Pass);
		case "rep_draw":
			return new MoveDataEx(MoveType.Repetition);
		case "rep_sup":
			return new MoveDataEx(MoveType.RepeSup);
		case "rep_inf":
			return new MoveDataEx(MoveType.RepeInf);
		case "win_foul":
			return new MoveDataEx(MoveType.WinFoul);
		case "timeout":
			return new MoveDataEx(MoveType.Timeout);
		case "mate":
			return new MoveDataEx(MoveType.Mate);
		case "break":
			return new MoveDataEx(MoveType.Stop);
		default:
		{
			if (move.Length < 4)
			{
				return null;
			}
			MoveDataEx moveDataEx = new MoveDataEx();
			if (move[1] == '*')
			{
				moveDataEx.MoveType = MoveType.DropFlag;
				if (CharToPieceHashtable.TryGetValue(move[0], out var value))
				{
					moveDataEx.Piece = (Piece)((uint)value | (uint)PieceExtensions.PieceFlagFromColor(position.Turn));
				}
				else
				{
					moveDataEx.Piece = Piece.NoPiece;
				}
				int num = FileFromChar(move[2]);
				int num2 = RankFromChar(move[3]);
				if (num < 0 || num2 < 0)
				{
					moveDataEx.MoveType = MoveType.NoMove;
					return moveDataEx;
				}
				moveDataEx.ToSquare = Square.Make(num, num2);
			}
			else
			{
				moveDataEx.MoveType = MoveType.MoveFlag;
				int file = FileFromChar(move[0]);
				int rank = RankFromChar(move[1]);
				moveDataEx.FromSquare = Square.Make(file, rank);
				file = FileFromChar(move[2]);
				rank = RankFromChar(move[3]);
				if (file < 0 || rank < 0)
				{
					moveDataEx.MoveType = MoveType.NoMove;
					return moveDataEx;
				}
				moveDataEx.ToSquare = Square.Make(file, rank);
				moveDataEx.Piece = position.GetPiece(moveDataEx.FromSquare);
				if (move.Length >= 5 && move[4] == '+')
				{
					moveDataEx.MoveType = MoveType.MoveMask;
				}
			}
			if (moveDataEx.MoveType.IsMoveWithoutPass())
			{
				if (position.MoveLast.MoveType.IsMove() && moveDataEx.ToSquare == position.MoveLast.ToSquare)
				{
					moveDataEx.MoveType |= MoveType.Same;
				}
				if (position.GetPiece(moveDataEx.ToSquare) != Piece.NoPiece)
				{
					moveDataEx.MoveType |= MoveType.Capture;
					moveDataEx.CapturePiece = position.GetPiece(moveDataEx.ToSquare);
				}
			}
			moveDataEx.Action = moveDataEx.GetAction(position);
			return moveDataEx;
		}
		}
	}

	private static int FileFromChar(char ch)
	{
		int result = -1;
		if (ch >= '1' && ch <= '9')
		{
			result = (ch - 48).ToFile();
		}
		return result;
	}

	private static int RankFromChar(char ch)
	{
		int result = -1;
		if (ch >= 'a' && ch <= 'i')
		{
			result = (ch - 97 + 1).ToRank();
		}
		return result;
	}

	private static void WriteNotation(SNotation notation, TextWriter wr)
	{
		wr.Write("position ");
		if (notation.Handicap != Handicap.HIRATE || notation.IsOutputInitialPosition)
		{
			wr.Write("sfen ");
			WritePosition(notation.InitialPosition, wr, 1);
		}
		else
		{
			wr.Write("startpos");
		}
		wr.Write(" moves ");
		WriteMoves(notation, wr);
	}

	private static char CharFromPieceType(PieceType pt)
	{
		char c = CharToPieceHashtable.FirstOrDefault((KeyValuePair<char, PieceType> x) => x.Value == pt).Key;
		if (c == '\0')
		{
			c = ' ';
		}
		return c;
	}

	public static PieceType PieceTypeFromChar(char ch)
	{
		PieceType result = PieceType.NoPieceType;
		if (CharToPieceHashtable.ContainsKey(ch))
		{
			result = CharToPieceHashtable[ch];
		}
		return result;
	}

	private static void WritePosition(SPosition position, TextWriter wr, int movenumber)
	{
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < 9; i++)
		{
			if (i != 0)
			{
				wr.Write('/');
			}
			int num3 = 0;
			while (num3 < 9)
			{
				Piece piece = position.GetPiece(num);
				if (piece == Piece.NoPiece)
				{
					num2++;
				}
				else
				{
					if (num2 != 0)
					{
						wr.Write(num2);
						num2 = 0;
					}
					if (piece.IsPromoted())
					{
						wr.Write('+');
					}
					char c = CharFromPieceType(piece.TypeOf());
					if (piece.HasFlag(Piece.WhiteFlag))
					{
						c = char.ToLower(c);
					}
					wr.Write(c);
				}
				num3++;
				num++;
			}
			if (num2 != 0)
			{
				wr.Write(num2);
				num2 = 0;
			}
		}
		if (position.Turn == PlayerColor.White)
		{
			wr.Write(" w ");
		}
		else
		{
			wr.Write(" b ");
		}
		int num4 = 0;
		PieceType pieceType = PieceType.HI;
		while ((int)pieceType > 0)
		{
			int blackHand = position.GetBlackHand(pieceType);
			if (blackHand != 0)
			{
				if (blackHand > 1)
				{
					wr.Write(blackHand);
				}
				wr.Write(CharFromPieceType(pieceType));
				num4++;
			}
			pieceType--;
		}
		PieceType pieceType2 = PieceType.HI;
		while ((int)pieceType2 > 0)
		{
			int whiteHand = position.GetWhiteHand(pieceType2);
			if (whiteHand != 0)
			{
				if (whiteHand > 1)
				{
					wr.Write(whiteHand);
				}
				char c2 = CharFromPieceType(pieceType2);
				c2 = char.ToLower(c2);
				wr.Write(c2);
				num4++;
			}
			pieceType2--;
		}
		if (num4 == 0)
		{
			wr.Write("-");
		}
		if (movenumber != 0)
		{
			wr.Write(" {0}", movenumber);
		}
	}

	private static void WriteMoves(SNotation notation, TextWriter wr)
	{
		bool flag = true;
		foreach (MoveNode moveNode in notation.MoveNodes)
		{
			if (moveNode.MoveType.IsMove() || moveNode.MoveType.IsResult())
			{
				if (flag)
				{
					flag = false;
				}
				else
				{
					wr.Write(" ");
				}
				WriteMove(moveNode, wr);
			}
		}
	}

	private static void WriteMovesToCurrent(SNotation notation, TextWriter wr)
	{
		bool flag = true;
		foreach (MoveNode moveNode in notation.MoveNodes)
		{
			if (moveNode.MoveType.IsMove())
			{
				if (flag)
				{
					flag = false;
				}
				else
				{
					wr.Write(" ");
				}
				WriteMove(moveNode, wr);
			}
			if (moveNode == notation.MoveCurrent)
			{
				break;
			}
		}
	}

	private static void WriteMove(MoveData move_data, TextWriter wr)
	{
		if (move_data.MoveType.IsResult())
		{
			switch (move_data.MoveType)
			{
			case MoveType.ResultFlag:
			case MoveType.LoseFoul:
			case MoveType.LoseNyugyoku:
				wr.Write("resign");
				break;
			case MoveType.Timeout:
				wr.Write("timeout");
				break;
			case MoveType.Repetition:
				wr.Write("rep_draw");
				break;
			case MoveType.Draw:
				wr.Write("draw");
				break;
			case MoveType.WinFoul:
				wr.Write("win_foul");
				break;
			case MoveType.WinNyugyoku:
				wr.Write("win");
				break;
			case MoveType.RepeInf:
				wr.Write("rep_sup");
				break;
			case MoveType.RepeSup:
				wr.Write("rep_inf");
				break;
			case MoveType.Mate:
				wr.Write("mate");
				break;
			case MoveType.Stop:
				wr.Write("break");
				break;
			case MoveType.NonMate:
				break;
			}
		}
		else if (move_data.MoveType == MoveType.Pass)
		{
			wr.Write("pass");
		}
		else if (move_data.MoveType.HasFlag(MoveType.DropFlag))
		{
			wr.Write("{0}*{1}{2}", CharFromPieceType(move_data.Piece.TypeOf()), (char)(49 + move_data.ToSquare.SujiOf() - 1), (char)(97 + move_data.ToSquare.DanOf() - 1));
		}
		else if (move_data.MoveType.HasFlag(MoveType.MoveFlag))
		{
			wr.Write("{0}{1}{2}{3}", (char)(49 + move_data.FromSquare.SujiOf() - 1), (char)(97 + move_data.FromSquare.DanOf() - 1), (char)(49 + move_data.ToSquare.SujiOf() - 1), (char)(97 + move_data.ToSquare.DanOf() - 1));
			if (move_data.MoveType.HasFlag(MoveType.MoveMask))
			{
				wr.Write("+");
			}
		}
	}

	public static string FSenReaderWEB(SNotation notation, bool title, bool name)
	{
		string text = "http://sfenreader.appspot.com/sfen?";
		text += "sfen=";
		text += Uri.EscapeDataString(notation.Position.PositionToString(notation.MoveCurrent.Number));
		if (notation.MoveCurrent.MoveType.IsMove())
		{
			text = text + "&lm=" + notation.MoveCurrent.ToSquare.SujiOf() + notation.MoveCurrent.ToSquare.DanOf();
		}
		if (title && notation.KifuInfos.Contains("棋戦"))
		{
			text = text + "&title=" + Uri.EscapeDataString((string)notation.KifuInfos["棋戦"]);
		}
		if (name)
		{
			text = text + "&sname=" + Uri.EscapeDataString(notation.BlackName);
			text = text + "&gname=" + Uri.EscapeDataString(notation.WhiteName);
		}
		return text;
	}
}
