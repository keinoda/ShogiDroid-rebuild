using System;
using System.Collections.Generic;

namespace ShogiLib;

public class MoveDataEx : MoveData
{
	private List<string> commentList;

	private int? score;

	private int? eval;

	private MoveMatche? bestmove;

	private int commentCount;

	private int thinkInfoCount;

	private string marker;

	public int Number { get; set; }

	public int Time { get; set; }

	public int TotalTime { get; set; }

	public MoveAction Action { get; set; }

	public bool Iregal { get; set; }

	public string Marker
	{
		get
		{
			return marker;
		}
		set
		{
			marker = value;
		}
	}

	public int Score
	{
		get
		{
			return score.GetValueOrDefault();
		}
		set
		{
			score = value;
		}
	}

	public int Eval
	{
		get
		{
			return eval.GetValueOrDefault();
		}
		set
		{
			eval = value;
		}
	}

	public MoveMatche BestMove
	{
		get
		{
			return bestmove.GetValueOrDefault();
		}
		set
		{
			bestmove = value;
		}
	}

	public bool HasScore => score.HasValue;

	public bool HasEval => eval.HasValue;

	public bool HasBestMove => bestmove.HasValue;

	public string Comment
	{
		get
		{
			string text = string.Empty;
			for (int i = 0; i < commentList.Count; i++)
			{
				text = text + commentList[i] + Environment.NewLine;
			}
			return text;
		}
	}

	public List<string> CommentList
	{
		get
		{
			return commentList;
		}
		set
		{
			commentList = DeepCopyHelper.DeepCopy(value);
			UpdateCommentCount();
		}
	}

	public int CommentCount => commentCount;

	public int ThinkInfoCount => thinkInfoCount;

	public MoveDataEx()
	{
		commentList = new List<string>();
		Init();
	}

	public MoveDataEx(MoveType result)
	{
		commentList = new List<string>();
		Init();
		base.MoveType = result;
	}

	public MoveDataEx(MoveData moveData)
		: base(moveData)
	{
		commentList = new List<string>();
		Init();
	}

	public MoveDataEx(MoveDataEx moveData)
		: base(moveData)
	{
		Number = moveData.Number;
		Time = moveData.Time;
		TotalTime = moveData.TotalTime;
		score = moveData.score;
		eval = moveData.eval;
		bestmove = moveData.bestmove;
		Action = moveData.Action;
		commentCount = moveData.commentCount;
		thinkInfoCount = moveData.thinkInfoCount;
		commentList = DeepCopyHelper.DeepCopy(moveData.commentList);
		marker = moveData.Marker;
		Iregal = moveData.Iregal;
	}

	private void Init()
	{
		Time = 0;
		TotalTime = 0;
		score = null;
		eval = null;
		bestmove = null;
		thinkInfoCount = 0;
		commentCount = 0;
		Action = MoveAction.None;
		marker = null;
		commentList.Clear();
		Iregal = false;
	}

	public override void Initialize()
	{
		base.Initialize();
		Init();
	}

	public void UpdateCommentCount()
	{
		commentCount = 0;
		thinkInfoCount = 0;
		foreach (string comment in commentList)
		{
			if (comment.Length != 0 && comment[0] == '*')
			{
				thinkInfoCount++;
			}
			else
			{
				commentCount++;
			}
		}
	}

	public void CommentAdd(string str)
	{
		commentList.Add(str);
		if (str.Length != 0 && str[0] == '*')
		{
			thinkInfoCount++;
		}
		else
		{
			commentCount++;
		}
	}
}
