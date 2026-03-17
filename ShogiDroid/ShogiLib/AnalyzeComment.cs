using System;

namespace ShogiLib;

public class AnalyzeComment
{
	private string orgString;

	private int lineno;

	private int? rank;

	private AnalyzeCommentKind kind;

	private string time;

	private string depth;

	private string nodes;

	private string eval;

	private string moves;

	private int? value;

	private int? mate;

	private MoveMatche bestmove;

	private int engineNo;

	public bool Mark;

	public int LineNo
	{
		get
		{
			return lineno;
		}
		set
		{
			lineno = value;
		}
	}

	public AnalyzeCommentKind Kind
	{
		get
		{
			return kind;
		}
		set
		{
			kind = value;
		}
	}

	public string Time
	{
		get
		{
			return time;
		}
		set
		{
			time = value;
		}
	}

	public string Depth
	{
		get
		{
			return depth;
		}
		set
		{
			depth = value;
		}
	}

	public string Nodes
	{
		get
		{
			return nodes;
		}
		set
		{
			nodes = value;
		}
	}

	public string Eval
	{
		get
		{
			return eval;
		}
		set
		{
			eval = value;
		}
	}

	public string Moves
	{
		get
		{
			return moves;
		}
		set
		{
			moves = value;
		}
	}

	public int? Rank => rank;

	public int? Value => value;

	public string OriginalString => orgString;

	public int? Mate => mate;

	public MoveMatche BestMove => bestmove;

	public int EngineNo
	{
		get
		{
			return engineNo;
		}
		set
		{
			engineNo = value;
		}
	}

	public string EngineName { get; set; }

	public AnalyzeComment()
	{
		Clear();
	}

	public void Clear()
	{
		Mark = false;
		lineno = 0;
		rank = null;
		value = null;
		kind = AnalyzeCommentKind.None;
		time = string.Empty;
		depth = string.Empty;
		nodes = string.Empty;
		eval = string.Empty;
		moves = string.Empty;
		mate = null;
		bestmove = MoveMatche.None;
		engineNo = -1;
	}

	public void Parse(string line)
	{
		line.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		orgString = line;
		AnalyzeCommentTokenizer analyzeCommentTokenizer = new AnalyzeCommentTokenizer(line);
		while (true)
		{
			string text = analyzeCommentTokenizer.Token();
			if (string.IsNullOrEmpty(text))
			{
				break;
			}
			switch (text)
			{
			case "*解析":
			case "解析":
			case "*Analysis":
			case "Analysis":
				kind = AnalyzeCommentKind.Analysis;
				continue;
			case "*対局":
			case "対局":
			case "*Game":
			case "Game":
				kind = AnalyzeCommentKind.Game;
				continue;
			case "*検討":
			case "検討":
			case "*Consider":
			case "Consider":
				kind = AnalyzeCommentKind.Consider;
				continue;
			case "*詰み探索":
			case "詰み探索":
			case "*Mate":
			case "Mate":
				kind = AnalyzeCommentKind.Mate;
				eval = analyzeCommentTokenizer.Token();
				value = (int)AnalyzeCommentTokenizer.ParseNum(eval);
				continue;
			case "*候補手":
			case "候補手":
			case "*Candidate":
			case "Candidate":
				kind = AnalyzeCommentKind.Candidate;
				continue;
			case "*Engines":
			case "Engines":
			{
				kind = AnalyzeCommentKind.EngineList;
				text = analyzeCommentTokenizer.Token();
				if (int.TryParse(text, out var result))
				{
					engineNo = result;
				}
				EngineName = analyzeCommentTokenizer.Last();
				return;
			}
			case "*":
			{
				text = analyzeCommentTokenizer.Token();
				if (Kifu.ParseNum(text, out var _))
				{
					kind = AnalyzeCommentKind.Game;
					eval = text;
					moves = analyzeCommentTokenizer.Last();
					return;
				}
				analyzeCommentTokenizer.Push(text);
				continue;
			}
			}
			if (text.StartsWith("候補") || text.StartsWith("Rank"))
			{
				if (text.Length >= 3)
				{
					text = text.Substring(2);
					if (int.TryParse(text, out var result2))
					{
						rank = result2;
					}
				}
				continue;
			}
			switch (text)
			{
			case "時間":
			case "Time":
				time = analyzeCommentTokenizer.Token();
				break;
			case "深さ":
			case "Depth":
				depth = analyzeCommentTokenizer.Token();
				break;
			case "ノード数":
			case "Nodes":
				nodes = analyzeCommentTokenizer.Token();
				break;
			case "評価値":
			case "Value":
				eval = analyzeCommentTokenizer.Token();
				if (eval == "+詰" || eval == "-詰" || eval == "+Mate" || eval == "-Mate")
				{
					text = analyzeCommentTokenizer.Token();
					if (int.TryParse(text, out var _))
					{
						eval = eval + " " + text;
					}
					else
					{
						analyzeCommentTokenizer.Push(text);
						text = string.Empty;
					}
					parsemate(eval, text);
				}
				else
				{
					value = (int)AnalyzeCommentTokenizer.ParseNum(eval);
				}
				break;
			case "EngineNo":
			{
				text = analyzeCommentTokenizer.Token();
				if (int.TryParse(text, out var result6))
				{
					engineNo = result6;
				}
				break;
			}
			case "読み筋":
			case "文字列":
			case "Moves":
			case "String":
				moves = analyzeCommentTokenizer.Last();
				return;
			case "+詰":
			case "-詰":
			case "+Mate":
			case "-Mate":
			{
				eval = text;
				text = analyzeCommentTokenizer.Token();
				if (int.TryParse(text, out var _))
				{
					eval = eval + " " + text;
				}
				parsemate(eval, text);
				break;
			}
			case "○":
				bestmove = MoveMatche.Best;
				break;
			case "△":
				bestmove = MoveMatche.Better;
				break;
			default:
			{
				if (int.TryParse(text, out var result3))
				{
					engineNo = result3;
				}
				break;
			}
			}
		}
	}

	private void parsemate(string str, string matenum)
	{
		mate = (int)AnalyzeCommentTokenizer.ParseNum(matenum);
		if (str[0] == '-')
		{
			value = -32000 + mate;
		}
		else
		{
			value = 32000 - mate + 1;
		}
	}
}
