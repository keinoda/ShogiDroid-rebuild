using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using AppDebug;

namespace ShogiLib;

public class Kifu
{
	protected static readonly char[] ArabiaNumber = new char[10] { '０', '１', '２', '３', '４', '５', '６', '７', '８', '９' };

	protected static readonly char[] KanNumber = new char[11]
	{
		'零', '一', '二', '三', '四', '五', '六', '七', '八', '九',
		'十'
	};

	protected static readonly char[] KanjiPiece = new char[9] { '・', '歩', '香', '桂', '銀', '金', '角', '飛', '玉' };

	protected static readonly char[] KanjiPiecePromotion = new char[8] { '・', 'と', '杏', '圭', '全', '金', '馬', '龍' };

	protected static readonly string[] KanjiPiecePromotionStr = new string[8] { "・", "と", "成香", "成桂", "成銀", "金", "馬", "龍" };

	protected static readonly OrderedDictionary ResultHash = new OrderedDictionary
	{
		{
			"投了",
			MoveType.ResultFlag
		},
		{
			"中断",
			MoveType.Stop
		},
		{
			"千日手",
			MoveType.Repetition
		},
		{
			"優等局面",
			MoveType.RepeSup
		},
		{
			"劣等局面",
			MoveType.RepeInf
		},
		{
			"持将棋",
			MoveType.Draw
		},
		{
			"切れ負け",
			MoveType.Timeout
		},
		{
			"詰み",
			MoveType.Mate
		},
		{
			"不詰",
			MoveType.NonMate
		},
		{
			"反則負け",
			MoveType.LoseFoul
		},
		{
			"反則勝ち",
			MoveType.WinFoul
		},
		{
			"封じ手",
			MoveType.Stop
		},
		{
			"入玉宣言",
			MoveType.WinNyugyoku
		}
	};

	protected static readonly string[] HandicapString = new string[15]
	{
		"平手", "右香落ち", "香落ち", "角落ち", "飛車落ち", "飛香落ち", "二枚落ち", "三枚落ち", "四枚落ち", "左五枚落ち",
		"五枚落ち", "六枚落ち", "八枚落ち", "十枚落ち", "その他"
	};

	protected static readonly string[] TurnStrSengo = new string[2] { "先手", "後手" };

	protected static readonly string[] TurnStrSimokami = new string[2] { "下手", "上手" };

	protected int rankNum;

	protected string[] turnStr;

	protected int lineNo;

	protected bool moveError;

	protected bool readFirst;

	public Kifu(bool first = false)
	{
		Init();
		readFirst = first;
	}

	protected void Init()
	{
		rankNum = 0;
		turnStr = TurnStrSengo;
		lineNo = 0;
		moveError = false;
	}

	public virtual void Load(SNotation notation, string filename, Encoding encodeing)
	{
		Init();
		notation.Init();
		using (StreamReader sr = new StreamReader(filename, encodeing))
		{
			LoadFromReader(notation, sr);
		}
		notation.ChangeCurrent(0);
	}

	public virtual void Load(SNotation notation, Stream stream, Encoding encodeing)
	{
		Init();
		notation.Init();
		using (StreamReader sr = new StreamReader(stream, encodeing))
		{
			LoadFromReader(notation, sr);
		}
		notation.ChangeCurrent(0);
	}

	public virtual void FromString(SNotation notation, string str)
	{
		Init();
		notation.Init();
		using (StringReader sr = new StringReader(str))
		{
			LoadFromReader(notation, sr);
		}
		notation.ChangeCurrent(0);
	}

	private void LoadFromReader(SNotation notation, TextReader sr)
	{
		while (true)
		{
			string text = sr.ReadLine();
			if (text == null || (readFirst && text.StartsWith("変化")))
			{
				break;
			}
			lineNo++;
			text = text.TrimStart(' ', '\u3000');
			if (text == string.Empty || text[0] == '#')
			{
				continue;
			}
			if (text[0] == '*')
			{
				string text2 = text.Substring(1);
				if (text.Length >= 2 && text[1] == '*')
				{
					ParseComment(notation, text2);
				}
				notation.MoveCurrent.CommentAdd(text2);
			}
			else if (text[0] == '&')
			{
				string marker = text.Substring(1);
				notation.SetMarker(marker);
			}
			else
			{
				ParseLine(notation, text);
			}
		}
	}

	protected static bool IsMoveSeparator(char ch)
	{
		if (ch == ' ' || ch == '(' || ch == ')' || ch == '/' || ch == '\t')
		{
			return true;
		}
		return false;
	}

	protected static bool IsMoveSeparatorEnd(char ch)
	{
		if (ch == ' ' || ch == '/' || ch == '\t')
		{
			return true;
		}
		return false;
	}

	protected static bool IsSankaku(char ch)
	{
		return ch == '▲' || ch == '△' || ch == '▽' || ch == '▼';
	}

	protected static List<string> SplitKifMove(string line)
	{
		List<string> list = new List<string>();
		int i = 0;
		while (i < line.Length)
		{
			for (; i < line.Length && IsMoveSeparator(line[i]); i++)
			{
			}
			int num = 0;
			int j;
			for (j = i; j < line.Length && ((line[j] == ' ' && j != 0 && line[j - 1] == '同') || !IsMoveSeparatorEnd(line[j])) && (num == 0 || !IsSankaku(line[j])); j++)
			{
				num++;
			}
			if (num == 0)
			{
				break;
			}
			list.Add(line.Substring(i, num));
			i = j;
		}
		return list;
	}

	protected bool IsMove(string key)
	{
		if (!string.IsNullOrEmpty(key))
		{
			if (int.TryParse(key, out var _))
			{
				return true;
			}
			if (IsSankaku(key[0]))
			{
				return true;
			}
		}
		return false;
	}

	protected void ParseLine(SNotation notation, string line)
	{
		bool flag = false;
		string empty = string.Empty;
		int num = line.IndexOfAny(new char[2] { '：', ':' });
		string text;
		if (num == -1)
		{
			text = line;
		}
		else
		{
			text = line.Substring(0, num);
			flag = true;
		}
		int num2 = line.IndexOfAny(new char[1] { ' ' });
		empty = ((num2 == -1) ? line : line.Substring(0, num2));
		switch (text)
		{
		case "先手番":
		case "下手番":
		case "Black to Move":
			notation.Position.Turn = PlayerColor.Black;
			return;
		case "後手番":
		case "上手番":
		case "White to Move":
			notation.Position.Turn = PlayerColor.White;
			return;
		}
		switch (line)
		{
		case "9  8  7  6  5  4  3  2  1":
			return;
		case "+---------------------------+":
			return;
		}
		if (line[0] == '|')
		{
			if (rankNum == 0)
			{
				notation.IsOutputInitialPosition = true;
			}
			if (rankNum < 9)
			{
				ParseBoard(notation.Position, line, rankNum);
				notation.InitHashKey();
				rankNum++;
			}
			return;
		}
		if (line.IndexOf("手数----指手---") >= 0)
		{
			return;
		}
		if (line.IndexOf("まで") == 0)
		{
			if (notation.MoveCurrent.MoveType.IsResult())
			{
				return;
			}
			MoveType moveType = MoveTypeFromMadeStr(line);
			if (moveType.IsResult())
			{
				MoveDataEx moveDataEx = new MoveDataEx();
				moveDataEx.MoveType = moveType;
				if (!notation.AddMove(moveDataEx))
				{
					throw new NotationException("指し手エラー", line, lineNo);
				}
			}
		}
		else if (IsMove(empty))
		{
			if (rankNum > 0 && rankNum < 9)
			{
				throw new NotationException("KIF解析エラー", line, lineNo);
			}
			if (!moveError)
			{
				ParseKif(notation, line);
			}
		}
		else if (flag)
		{
			string text2 = line.Substring(num + 1);
			switch (text)
			{
			case "手合割":
			case "Handicap":
			{
				text2 = text2.TrimStart(' ', '\u3000');
				text2 = text2.TrimEnd(' ', '\u3000');
				Handicap handicap = HandicapFromStr(text2);
				notation.Handicap = handicap;
				break;
			}
			case "先手":
			case "下手":
			case "Black":
				notation.BlackName = text2;
				break;
			case "後手":
			case "上手":
			case "White":
				notation.WhiteName = text2;
				break;
			case "先手の持駒":
			case "下手の持駒":
			case "White in hand":
				ParseHand(PlayerColor.Black, notation.Position, text2);
				break;
			case "後手の持駒":
			case "上手の持駒":
			case "Black in hand":
				ParseHand(PlayerColor.White, notation.Position, text2);
				break;
			case "変化":
			case "variation":
			{
				int num3 = ParseNum(text2);
				if (num3 == 0)
				{
					throw new NotationException("棋譜解析エラー", line, lineNo);
				}
				notation.StartBranch(num3);
				moveError = false;
				break;
			}
			default:
				if (text2 != string.Empty)
				{
					notation.AddKifuInfo(text, text2);
				}
				break;
			}
		}
		else
		{
			Log.Warning("不明な文字列:" + line);
		}
	}

	protected void ParseBoard(SPosition pos, string line, int rank)
	{
		int i = 1;
		for (int j = 0; j < 9; j++)
		{
			PlayerColor playerColor = PlayerColor.Black;
			Piece piece = Piece.NoPiece;
			for (; i < line.Length && char.IsWhiteSpace(line[i]); i++)
			{
			}
			if (i < line.Length)
			{
				if (line[i] == 'v' || line[i] == 'V' || line[i] == '-' || line[i] == 'w')
				{
					playerColor = PlayerColor.White;
					i++;
				}
				else if (line[i] == '+' || line[i] == '^' || line[i] == 'b')
				{
					i++;
				}
				piece = PieceFromStr(line, ref i);
				if (playerColor == PlayerColor.White)
				{
					piece |= Piece.WhiteFlag;
				}
				pos.SetPiece(j, rank, piece);
				continue;
			}
			break;
		}
	}

	protected void ParseKif(SNotation notation, string line)
	{
		List<string> list = SplitKifMove(line);
		if (list.Count < 2 || !int.TryParse(list[0], out var result))
		{
			if (IsSankaku(list[0][0]))
			{
				ParseKi2(notation, list);
			}
			else
			{
				Log.Warning("不明な文字列:" + line);
			}
			return;
		}
		MoveDataEx moveDataEx = new MoveDataEx();
		moveDataEx.Number = result;
		string str = list[1];
		int num = 2;
		if (!ParseMove(str, notation, moveDataEx))
		{
			throw new NotationException("KIF解析エラー", line, lineNo);
		}
		if (list.Count > num)
		{
			moveDataEx.Time = TimeFromStr(list[num]);
		}
		bool flag = false;
		if (moveDataEx.MoveType.IsMove() && !MoveCheck.IsValid(notation.Position, moveDataEx))
		{
			flag = true;
		}
		if (moveDataEx.Turn != PlayerColor.NoColor && notation.Position.Turn != moveDataEx.Turn)
		{
			notation.AddMove(new MoveDataEx(MoveType.WinFoul));
			moveError = true;
			return;
		}
		moveDataEx.Iregal = flag;
		if (!notation.AddMove(moveDataEx))
		{
			throw new NotationException("指し手エラー", line, lineNo);
		}
		if (flag)
		{
			notation.AddMove(new MoveDataEx(MoveType.WinFoul));
			moveError = true;
		}
	}

	protected void ParseKi2(SNotation notation, List<string> string_list)
	{
		foreach (string item in string_list)
		{
			if (item.Length == 0)
			{
				continue;
			}
			MoveDataEx moveDataEx = new MoveDataEx();
			if (!ParseMove(item, notation, moveDataEx))
			{
				throw new NotationException("KIF解析エラー", item, lineNo);
			}
			if (moveDataEx.Turn != PlayerColor.NoColor && notation.Position.Turn != moveDataEx.Turn)
			{
				notation.AddMove(new MoveDataEx(MoveType.WinFoul));
				moveError = true;
				break;
			}
			bool flag = false;
			if (moveDataEx.MoveType.IsMove())
			{
				if (!MoveCheck.IsValid(notation.Position, moveDataEx))
				{
					flag = true;
				}
				moveDataEx.Iregal = flag;
			}
			if (!notation.AddMove(moveDataEx))
			{
				moveError = true;
				throw new NotationException("指し手エラー", item, lineNo);
			}
			if (flag)
			{
				notation.AddMove(new MoveDataEx(MoveType.WinFoul));
				moveError = true;
				break;
			}
		}
	}

	protected bool ParseHand(PlayerColor turn, SPosition pos, string str)
	{
		if (str == "nothing")
		{
			return true;
		}
		for (int i = 0; i < str.Length; i++)
		{
			char c = str[i];
			if (c == ' ' || c == '\u3000')
			{
				continue;
			}
			Piece piece = PieceFromChar(c);
			if (piece == Piece.NoPiece)
			{
				return false;
			}
			int num = 1;
			if (i + 1 < str.Length)
			{
				num = ParseKanjiNum(str, ref i);
				if (num == 0)
				{
					num = 1;
				}
			}
			if (turn == PlayerColor.White)
			{
				pos.SetWhiteHand(piece.ToHandIndex(), num);
			}
			else
			{
				pos.SetBlackHand(piece.ToHandIndex(), num);
			}
		}
		return true;
	}

	protected static int ParseKanjiNum(string str, ref int index)
	{
		int num = 0;
		for (int i = index + 1; i < str.Length; index = i, i++)
		{
			char c = str[i];
			switch (c)
			{
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
			{
				int num3 = c - 48;
				num = num * 10 + num3;
				continue;
			}
			default:
			{
				int num2 = NumFromKanji(c);
				if (num2 != 0)
				{
					num += num2;
					continue;
				}
				break;
			}
			case ' ':
			case '\u3000':
				continue;
			}
			break;
		}
		return num;
	}

	protected void ParseComment(SNotation notation, string line)
	{
		AnalyzeComment analyzeComment = new AnalyzeComment();
		analyzeComment.Parse(line);
		if (analyzeComment.Kind == AnalyzeCommentKind.Analysis || analyzeComment.Kind == AnalyzeCommentKind.Consider)
		{
			if (!notation.MoveCurrent.HasScore || analyzeComment.Rank <= 1 || !analyzeComment.Rank.HasValue)
			{
				if (analyzeComment.Value.HasValue)
				{
					notation.MoveCurrent.Score = analyzeComment.Value.Value;
				}
				notation.MoveCurrent.BestMove = analyzeComment.BestMove;
			}
		}
		else if (analyzeComment.Kind == AnalyzeCommentKind.Game)
		{
			if (analyzeComment.Value.HasValue)
			{
				notation.MoveCurrent.Eval = analyzeComment.Value.Value;
			}
		}
		else if (analyzeComment.Kind == AnalyzeCommentKind.EngineList && analyzeComment.EngineNo != -1 && !string.IsNullOrEmpty(analyzeComment.EngineName))
		{
			notation.Engines.Add(analyzeComment.EngineNo, analyzeComment.EngineName);
		}
	}

	public virtual void Save(SNotation notation, string filename, Encoding encode)
	{
		Init();
		using StreamWriter streamWriter = new StreamWriter(filename, append: false, encode);
		streamWriter.NewLine = "\r\n";
		if (encode == Encoding.UTF8)
		{
			streamWriter.WriteLine("#KIF version=2.0 encoding=UTF-8");
		}
		SaveToWriter(notation, streamWriter);
	}

	public virtual void Save(SNotation notation, Stream stream, Encoding encode)
	{
		Init();
		using StreamWriter streamWriter = new StreamWriter(stream, encode);
		streamWriter.NewLine = "\r\n";
		if (encode == Encoding.UTF8)
		{
			streamWriter.WriteLine("#KIF version=2.0 encoding=UTF-8");
		}
		SaveToWriter(notation, streamWriter);
	}

	public virtual string ToString(SNotation notation, bool ki2 = false)
	{
		string empty = string.Empty;
		Init();
		using StringWriter stringWriter = new StringWriter();
		stringWriter.NewLine = "\r\n";
		SaveToWriter(notation, stringWriter);
		return stringWriter.ToString();
	}

	public virtual string ToBodString(SNotation notation)
	{
		string empty = string.Empty;
		Init();
		using StringWriter stringWriter = new StringWriter();
		stringWriter.NewLine = "\r\n";
		WriteBoard(notation.Position, notation.Handicap, stringWriter);
		WriteLastMove(notation, stringWriter);
		return stringWriter.ToString();
	}

	private void SaveToWriter(SNotation notation, TextWriter wr)
	{
		if (!notation.Handicap.IsSenGo())
		{
			turnStr = TurnStrSimokami;
		}
		foreach (DictionaryEntry kifuInfo in notation.KifuInfos)
		{
			wr.WriteLine("{0}：{1}", kifuInfo.Key, kifuInfo.Value);
		}
		if (notation.IsOutputInitialPosition)
		{
			WriteBoard(notation.InitialPosition, notation.Handicap, wr);
		}
		else
		{
			wr.WriteLine("手合割：{0}", notation.Handicap.ToKifuString());
		}
		wr.WriteLine("{0}：{1}", turnStr[0], notation.BlackName);
		wr.WriteLine("{0}：{1}", turnStr[1], notation.WhiteName);
		wr.WriteLine("手数----指手---------消費時間--");
		WriteMove(notation.MoveFirst, wr);
	}

	protected void WriteBoard(SPosition pos, Handicap handicap, TextWriter wr)
	{
		string arg = StrFromHand(pos.WhiteHand);
		if (handicap.IsSenGo())
		{
			wr.WriteLine("後手の持駒：{0}", arg);
		}
		else
		{
			wr.WriteLine("上手の持駒：{0}", arg);
		}
		wr.WriteLine("  ９ ８ ７ ６ ５ ４ ３ ２ １");
		wr.WriteLine("+---------------------------+");
		for (int i = 0; i < 9; i++)
		{
			wr.Write("|");
			for (int j = 0; j < 9; j++)
			{
				Piece piece = pos.GetPiece(j, i);
				if (piece.ColorOf() == PlayerColor.White)
				{
					wr.Write("v");
				}
				else
				{
					wr.Write(" ");
				}
				wr.Write(GetPieceChar(piece));
			}
			wr.WriteLine("|{0}", KanNumber[i + 1]);
		}
		wr.WriteLine("+---------------------------+");
		arg = StrFromHand(pos.BlackHand);
		if (handicap.IsSenGo())
		{
			wr.WriteLine("先手の持駒：{0}", arg);
		}
		else
		{
			wr.WriteLine("下手の持駒：{0}", arg);
		}
		if (pos.Turn == PlayerColor.White)
		{
			if (handicap.IsSenGo())
			{
				wr.WriteLine("後手番");
			}
			else
			{
				wr.WriteLine("下手番");
			}
		}
	}

	protected virtual void WriteLastMove(SNotation notation, TextWriter wr)
	{
		if (notation.MoveCurrent != notation.MoveFirst)
		{
			wr.WriteLine("手数＝{0}  {1}{2} まで", notation.MoveCurrent.Number, notation.MoveCurrent.Turn.ToKifChar(), GetMoveString(notation.MoveCurrent));
		}
	}

	private void WriteMove(MoveNode move_info, TextWriter wr)
	{
		if (move_info.Number != 0)
		{
			wr.WriteLine("{0,4} {1,-11}    ({2})", move_info.Number, GetMoveString(move_info), GetMoveTimeString(move_info));
		}
		if (move_info.Marker != null)
		{
			wr.WriteLine("&{0}", move_info.Marker);
		}
		if (move_info.CommentList.Count != 0)
		{
			for (int i = 0; i < move_info.CommentList.Count; i++)
			{
				wr.WriteLine("*{0}", move_info.CommentList[i]);
			}
		}
		if (move_info.Children.Count == 0)
		{
			if (move_info.MoveType.IsResult())
			{
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
					wr.WriteLine("まで{0}手で{1}の{2}", move_info.Number - 1, turnStr[(int)move_info.Turn], StrFromMovetype(move_info.MoveType));
					break;
				default:
					wr.WriteLine("まで{0}手で{1}", move_info.Number - 1, StrFromMovetype(move_info.MoveType));
					break;
				}
			}
			wr.WriteLine(string.Empty);
			return;
		}
		for (int j = 0; j < move_info.Children.Count; j++)
		{
			if (j > 0)
			{
				wr.WriteLine("変化：{0}手", move_info.Children[j].Number);
			}
			WriteMove(move_info.Children[j], wr);
		}
	}

	protected static int DanFromChar(char ch)
	{
		if (ch >= '1' && ch <= '9')
		{
			return ch - 48;
		}
		return NumFromKanji(ch);
	}

	protected static int NumFromKanji(char ch)
	{
		for (int i = 0; i < KanNumber.Length; i++)
		{
			if (KanNumber[i] == ch)
			{
				return i;
			}
		}
		return 0;
	}

	protected static int SujiFromChar(char ch)
	{
		if (ch >= '1' && ch <= '9')
		{
			return ch - 48;
		}
		for (int i = 0; i < ArabiaNumber.Length; i++)
		{
			if (ArabiaNumber[i] == ch)
			{
				return i;
			}
		}
		return 0;
	}

	protected static int TimeFromStr(string str)
	{
		int num = 0;
		int num2 = 0;
		int i;
		for (i = 0; i < str.Length; i++)
		{
			if (str[i] >= '0' && str[i] <= '9')
			{
				num *= 10;
				num += str[i] - 48;
				continue;
			}
			if (str[i] == ':')
			{
				i++;
				break;
			}
			return 0;
		}
		for (; i < str.Length; i++)
		{
			if (str[i] >= '0' && str[i] <= '9')
			{
				num2 *= 10;
				num2 += str[i] - 48;
				continue;
			}
			return 0;
		}
		return num * 60 + num2;
	}

	protected static Piece PieceFromChar(char ch)
	{
		Piece result = Piece.NoPiece;
		switch (ch)
		{
		case '王':
			return Piece.BOU;
		case '竜':
			return Piece.BRYU;
		default:
		{
			for (int i = 0; i < KanjiPiece.Length; i++)
			{
				if (KanjiPiece[i] == ch)
				{
					return (Piece)i;
				}
			}
			for (int i = 0; i < KanjiPiecePromotion.Length; i++)
			{
				if (KanjiPiecePromotion[i] == ch)
				{
					result = (Piece)((byte)i | 8);
					break;
				}
			}
			return result;
		}
		}
	}

	protected static Piece PieceFromStr(string str, ref int pos)
	{
		Piece piece = Piece.NoPiece;
		if (str.Length <= pos)
		{
			return Piece.NoPiece;
		}
		char c = str[pos];
		switch (c)
		{
		case '王':
			pos++;
			return Piece.BOU;
		case '竜':
			pos++;
			return Piece.BRYU;
		case '*':
			pos++;
			return Piece.NoPiece;
		default:
		{
			if ((piece = EnglishNotation.PieceFromChar(c)) != Piece.NoPiece)
			{
				pos++;
				return piece;
			}
			for (int i = 0; i < KanjiPiece.Length; i++)
			{
				if (KanjiPiece[i] == c)
				{
					piece = (Piece)i;
					pos++;
					return piece;
				}
			}
			for (int i = 0; i < KanjiPiecePromotion.Length; i++)
			{
				if (KanjiPiecePromotion[i] == c)
				{
					piece = (Piece)((byte)i | 8);
					pos++;
					break;
				}
			}
			if (c == '成' && str.Length >= 2)
			{
				if (str[pos + 1] == '銀')
				{
					pos += 2;
					piece = Piece.BNGIN;
				}
				else if (str[pos + 1] == '桂')
				{
					pos += 2;
					piece = Piece.BNKEI;
				}
				else if (str[pos + 1] == '香')
				{
					pos += 2;
					piece = Piece.BNKYO;
				}
			}
			return piece;
		}
		}
	}

	public static char GetPieceChar(Piece piece)
	{
		int num = (int)piece.TypeOf();
		if (piece.IsPromoted())
		{
			return KanjiPiecePromotion[num];
		}
		return KanjiPiece[num];
	}

	public static string GetPieceStr(Piece piece)
	{
		int num = (int)piece.TypeOf();
		if (piece.IsPromoted())
		{
			return KanjiPiecePromotionStr[num];
		}
		return KanjiPiece[num].ToString();
	}

	protected static Handicap HandicapFromStr(string str)
	{
		if (HandicapExtension.HandicapHash.TryGetValue(str, out var result))
		{
			return result;
		}
		Log.Warning("ハンディキャップ?" + str);
		return Handicap.OTHER;
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
			return "なし";
		}
		for (int num3 = 8; num3 >= 0; num3--)
		{
			int num4 = hand[num3];
			if (num4 != 0)
			{
				text += KanjiPiece[num3];
				if (num4 > 1)
				{
					if (num4 > 10)
					{
						text += KanNumber[10];
						num4 %= 10;
					}
					if (num4 > 0)
					{
						text += KanNumber[num4];
					}
				}
				text += " ";
			}
		}
		return text;
	}

	protected static MoveType ResultMoveTypeFromStr(string str)
	{
		MoveType moveType = MoveType.NoMove;
		object obj = ResultHash[str];
		if (obj == null)
		{
			return EnglishNotation.ResultMoveTypeFromStr(str);
		}
		return (MoveType)obj;
	}

	public static string StrFromMovetype(MoveType move_type)
	{
		foreach (DictionaryEntry item in ResultHash)
		{
			if ((MoveType)item.Value == move_type)
			{
				return (string)item.Key;
			}
		}
		return string.Empty;
	}

	protected MoveType MoveTypeFromMadeStr(string str)
	{
		MoveType result = MoveType.NoMove;
		if (str.Contains("不詰"))
		{
			result = MoveType.NonMate;
		}
		else if (str.Contains("詰"))
		{
			result = MoveType.Mate;
		}
		else if (str.Contains("反則負け"))
		{
			result = MoveType.LoseFoul;
		}
		else if (str.Contains("反則勝ち"))
		{
			result = MoveType.LoseFoul;
		}
		else if (str.Contains("中断"))
		{
			result = MoveType.Stop;
		}
		else if (str.Contains("千日手"))
		{
			result = MoveType.Repetition;
		}
		else if (str.Contains("持将棋"))
		{
			result = MoveType.Draw;
		}
		else if (str.Contains("時間切れ"))
		{
			result = MoveType.Timeout;
		}
		else if (str.Contains("勝ち"))
		{
			result = MoveType.ResultFlag;
		}
		else if (str.Contains("封じ手"))
		{
			result = MoveType.Stop;
		}
		else if (str.Contains("入玉宣言"))
		{
			result = MoveType.WinNyugyoku;
		}
		return result;
	}

	public static string GetMoveString(MoveData move_data)
	{
		string empty = string.Empty;
		if (move_data.MoveType == MoveType.Pass)
		{
			empty = "パス";
		}
		else if (!move_data.MoveType.IsMove())
		{
			empty = ((!move_data.MoveType.IsResult()) ? string.Empty : StrFromMovetype(move_data.MoveType));
		}
		else
		{
			if (move_data.MoveType.HasFlag(MoveType.Same))
			{
				empty = "同\u3000";
			}
			else
			{
				int num = move_data.ToSquare.DanOf();
				int num2 = move_data.ToSquare.SujiOf();
				empty = $"{ArabiaNumber[num2]}{KanNumber[num]}";
			}
			empty += GetPieceStr(move_data.Piece);
			if (move_data.MoveType.HasFlag(MoveType.MoveMask))
			{
				empty += "成";
			}
			if (move_data.MoveType.HasFlag(MoveType.DropFlag))
			{
				empty += "打";
			}
			else if (move_data.MoveType.HasFlag(MoveType.MoveFlag))
			{
				int num = move_data.FromSquare.DanOf();
				int num2 = move_data.FromSquare.SujiOf();
				empty += $"({num2}{num})";
			}
		}
		return empty;
	}

	public static string GetMoveTimeString(MoveDataEx info)
	{
		_ = string.Empty;
		int num = info.TotalTime / 3600;
		int num2 = info.TotalTime - num * 3600;
		return $"{info.Time / 60,2}:{info.Time % 60:00}/{num:00}:{num2 / 60:00}:{num2 % 60:00}";
	}

	public static string GetStateString(MoveDataEx move)
	{
		string result = string.Empty;
		int num = move.Score;
		if (num == 0)
		{
			num = move.Eval;
		}
		if (move.HasEval || move.HasScore)
		{
			result = ((num > 1500) ? "先手勝勢" : ((num > 800) ? "先手優勢" : ((num > 300) ? "先手有利" : ((num < -1500) ? "後手勝勢" : ((num < -800) ? "後手優勢" : ((num >= -300) ? "互角" : "後手有利"))))));
		}
		return result;
	}

	public static void DebugPrintBoard(SPosition pos)
	{
		StrFromHand(pos.WhiteHand);
		for (int i = 0; i < 9; i++)
		{
			for (int j = 0; j < 9; j++)
			{
				pos.GetPiece(j, i).ColorOf();
				_ = 1;
			}
		}
		StrFromHand(pos.BlackHand);
		_ = pos.Turn;
		_ = 1;
	}

	public void ParseMoves(SNotation notation, MoveAddMode add_mode, string line)
	{
		List<string> list = SplitKifMove(line);
		bool flag = true;
		notation.Continue(remove_stop: false);
		foreach (string item in list)
		{
			MoveDataEx moveDataEx = new MoveDataEx();
			if (!ParseMove(item, notation, moveDataEx) || !MoveCheck.IsValid(notation.Position, moveDataEx))
			{
				break;
			}
			if (add_mode == MoveAddMode.ADD_MERGE)
			{
				if (flag && notation.MoveCurrent.FindChildIndex(moveDataEx) == 0)
				{
					notation.AddMove(moveDataEx, MoveAddMode.ADD, changeChildCurrent: false);
				}
				else
				{
					notation.AddMove(moveDataEx, MoveAddMode.MERGE, changeChildCurrent: false);
				}
			}
			else
			{
				notation.AddMove(moveDataEx, add_mode, changeChildCurrent: false);
			}
			flag = false;
		}
	}

	public static bool ParseMove(string str, SNotation notation, MoveData move_data)
	{
		PlayerColor color = notation.Position.Turn;
		int num = 0;
		if (str.Length > 0 && IsSankaku(str[0]))
		{
			if (str[0] == '▲' || str[0] == '▼')
			{
				num++;
				color = PlayerColor.Black;
			}
			else if (str[0] == '△' || str[0] == '▽')
			{
				num++;
				color = PlayerColor.White;
			}
			_ = notation.Position.Turn;
		}
		string text = str.Substring(num);
		MoveType moveType = ResultMoveTypeFromStr(text);
		if (moveType.IsResult())
		{
			move_data.MoveType = moveType;
			move_data.Piece |= PieceExtensions.PieceFlagFromColor(color);
		}
		else if (text == "パス" || text == "pass")
		{
			move_data.MoveType = MoveType.Pass;
			move_data.Piece |= PieceExtensions.PieceFlagFromColor(color);
		}
		else if (EnglishNotation.IsEnglishMove(text))
		{
			move_data.Piece |= PieceExtensions.PieceFlagFromColor(color);
			if (!EnglishNotation.Parse(notation, move_data, text))
			{
				return false;
			}
		}
		else
		{
			if (str.Length < num + 2)
			{
				return false;
			}
			if (str[num] == '同')
			{
				if (notation.MoveCurrent.MoveType.IsMoveWithoutPass())
				{
					move_data.ToSquare = notation.MoveCurrent.ToSquare;
				}
				else
				{
					move_data.ToSquare = notation.Position.MoveLast.ToSquare;
				}
				num++;
				for (int i = num; i < str.Length && (str[i] == '\u3000' || str[i] == ' '); i++)
				{
					num++;
				}
			}
			else
			{
				int dan = DanFromChar(str[num + 1]);
				int suji = SujiFromChar(str[num]);
				move_data.ToSquare = Square.Make(suji.ToFile(), dan.ToRank());
				num += 2;
			}
			move_data.Piece = PieceFromStr(str, ref num);
			if (move_data.Piece == Piece.NoPiece)
			{
				return false;
			}
			move_data.Piece |= PieceExtensions.PieceFlagFromColor(color);
			move_data.MoveType = MoveType.MoveFlag;
			MoveAbsPos moveAbsPos = MoveAbsPos.NONE;
			MoveOperation moveOperation = MoveOperation.NONE;
			if (num < str.Length)
			{
				if (str[num] == '右')
				{
					moveAbsPos = MoveAbsPos.RIGHT;
					num++;
				}
				else if (str[num] == '左')
				{
					moveAbsPos = MoveAbsPos.LEFT;
					num++;
				}
				else if (str[num] == '直')
				{
					moveAbsPos = MoveAbsPos.CENTER;
					num++;
				}
			}
			if (num < str.Length)
			{
				if (str[num] == '上' || str[num] == '行')
				{
					moveOperation = MoveOperation.UE;
					num++;
				}
				else if (str[num] == '寄')
				{
					moveOperation = MoveOperation.YORI;
					num++;
				}
				else if (str[num] == '引')
				{
					moveOperation = MoveOperation.HIKI;
					num++;
				}
			}
			if (num < str.Length)
			{
				if (str[num] == '成')
				{
					move_data.MoveType |= MoveType.MoveMask;
					num++;
				}
				else if (str[num] == '打')
				{
					move_data.MoveType = MoveType.DropFlag;
					num++;
				}
			}
			bool flag = false;
			if (!move_data.MoveType.HasFlag(MoveType.DropFlag) && num + 3 <= str.Length && str[num] == '(' && int.TryParse(str.Substring(num + 1, 2), out var result))
			{
				int dan2 = result % 10;
				int suji2 = result / 10;
				move_data.FromSquare = Square.Make(suji2.ToFile(), dan2.ToRank());
				flag = true;
			}
			if (move_data.MoveType.HasFlag(MoveType.MoveFlag) && !flag)
			{
				if (!NotationUtility.GetMoveFromPosition(notation.Position, move_data, moveAbsPos, moveOperation))
				{
					if (moveAbsPos != MoveAbsPos.NONE || moveOperation != MoveOperation.NONE || !notation.Position.IsHand(notation.Position.Turn, move_data.Piece.ToHandIndex()))
					{
						return false;
					}
					move_data.MoveType = MoveType.DropFlag;
				}
			}
			else if (moveOperation != MoveOperation.NONE || moveAbsPos != MoveAbsPos.NONE)
			{
				return false;
			}
		}
		return true;
	}

	public static string GetFirstMove(string line)
	{
		List<string> list = SplitKifMove(line);
		if (list.Count == 0)
		{
			return string.Empty;
		}
		return list[0];
	}

	public static PlayerColor GetPlayerColor(string move)
	{
		PlayerColor result = PlayerColor.NoColor;
		if (move.StartsWith("▲") || move.StartsWith("▼"))
		{
			result = PlayerColor.Black;
		}
		else if (move.StartsWith("△") || move.StartsWith("▽"))
		{
			result = PlayerColor.White;
		}
		return result;
	}

	public static int ParseNum(string str)
	{
		int num = 0;
		ParseNum(str, out num);
		return num;
	}

	public static bool ParseNum(string str, out int num)
	{
		int i = 0;
		bool flag = false;
		bool result = false;
		num = 0;
		if (str.Length >= 1 && str[0] == '-')
		{
			flag = true;
			i++;
		}
		for (; i < str.Length; i++)
		{
			char c = str[i];
			if (c >= '0' && c <= '9')
			{
				num *= 10;
				num += c - 48;
				result = true;
				continue;
			}
			switch (c)
			{
			case 'K':
			case 'k':
				num *= 1000;
				break;
			case 'M':
			case 'm':
				num = num * 1000 * 1000;
				break;
			}
			break;
		}
		if (flag)
		{
			num = -num;
		}
		return result;
	}
}
