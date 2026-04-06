using System;

namespace ShogiLib;

public class AnalyzeComment
{
	private string orgString;

	private int? rank;

	private int? value;

	private int? mate;

	private MoveMatche bestmove;

	public bool Mark;

	public int LineNo { get; set; }

	public AnalyzeCommentKind Kind { get; set; }

	public string Time { get; set; }

	public string Depth { get; set; }

	public string Nodes { get; set; }

	public string Eval { get; set; }

	public string Moves { get; set; }

	public int? Rank => rank;

	public int? Value => value;

	public string OriginalString => orgString;

	public int? Mate => mate;

	public MoveMatche BestMove => bestmove;

	public int EngineNo { get; set; }

	public string EngineName { get; set; }

	public AnalyzeComment()
	{
		Clear();
	}

	public void Clear()
	{
		Mark = false;
		LineNo = 0;
		rank = null;
		value = null;
		Kind = AnalyzeCommentKind.None;
		Time = string.Empty;
		Depth = string.Empty;
		Nodes = string.Empty;
		Eval = string.Empty;
		Moves = string.Empty;
		mate = null;
		bestmove = MoveMatche.None;
		EngineNo = -1;
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
				Kind = AnalyzeCommentKind.Analysis;
				continue;
			case "*対局":
			case "対局":
			case "*Game":
			case "Game":
				Kind = AnalyzeCommentKind.Game;
				continue;
			case "*検討":
			case "検討":
			case "*Consider":
			case "Consider":
				Kind = AnalyzeCommentKind.Consider;
				continue;
			case "*詰み探索":
			case "詰み探索":
			case "*Mate":
			case "Mate":
				Kind = AnalyzeCommentKind.Mate;
				Eval = analyzeCommentTokenizer.Token();
				value = (int)AnalyzeCommentTokenizer.ParseNum(Eval);
				continue;
			case "*候補手":
			case "候補手":
			case "*Candidate":
			case "Candidate":
				Kind = AnalyzeCommentKind.Candidate;
				continue;
			case "*Engines":
			case "Engines":
			{
				Kind = AnalyzeCommentKind.EngineList;
				text = analyzeCommentTokenizer.Token();
				if (int.TryParse(text, out var result))
				{
					EngineNo = result;
				}
				EngineName = analyzeCommentTokenizer.Last();
				return;
			}
			case "*":
			{
				text = analyzeCommentTokenizer.Token();
				if (Kifu.ParseNum(text, out var _))
				{
					Kind = AnalyzeCommentKind.Game;
					Eval = text;
					Moves = analyzeCommentTokenizer.Last();
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
				Time = analyzeCommentTokenizer.Token();
				break;
			case "深さ":
			case "Depth":
				Depth = analyzeCommentTokenizer.Token();
				break;
			case "ノード数":
			case "Nodes":
				Nodes = analyzeCommentTokenizer.Token();
				break;
			case "評価値":
			case "Value":
				Eval = analyzeCommentTokenizer.Token();
				if (Eval == "+詰" || Eval == "-詰" || Eval == "+Mate" || Eval == "-Mate")
				{
					text = analyzeCommentTokenizer.Token();
					if (int.TryParse(text, out var _))
					{
						Eval = Eval + " " + text;
					}
					else
					{
						analyzeCommentTokenizer.Push(text);
						text = string.Empty;
					}
					parsemate(Eval, text);
				}
				else
				{
					value = (int)AnalyzeCommentTokenizer.ParseNum(Eval);
				}
				break;
			case "EngineNo":
			{
				text = analyzeCommentTokenizer.Token();
				if (int.TryParse(text, out var result6))
				{
					EngineNo = result6;
				}
				break;
			}
			case "読み筋":
			case "文字列":
			case "Moves":
			case "String":
				Moves = analyzeCommentTokenizer.Last();
				return;
			case "+詰":
			case "-詰":
			case "+Mate":
			case "-Mate":
			{
				Eval = text;
				text = analyzeCommentTokenizer.Token();
				if (int.TryParse(text, out var _))
				{
					Eval = Eval + " " + text;
				}
				parsemate(Eval, text);
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
					EngineNo = result3;
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
