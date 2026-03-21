using System;
using System.Collections.Generic;
using ShogiGUI.Engine;
using ShogiLib;

namespace ShogiGUI;

public class AnalyzeInfoList : SortedList<int, AnalyzeInfo>
{
	private bool notationFlag;

	private AnalyzeMoveInfo blackMoveInfo = new AnalyzeMoveInfo();

	private AnalyzeMoveInfo whiteMoveInfo = new AnalyzeMoveInfo();

	public bool NotationFlag => notationFlag;

	public AnalyzeMoveInfo BlackMoveInfo => blackMoveInfo;

	public AnalyzeMoveInfo WhiteMoveInfo => whiteMoveInfo;

	public void ClearAll()
	{
		notationFlag = false;
		Clear();
		blackMoveInfo.Init();
		whiteMoveInfo.Init();
	}

	public AnalyzeInfo Add(int number, MoveNode moveData)
	{
		AnalyzeInfo analyzeInfo = GetInfo(number);
		if (analyzeInfo != null)
		{
			analyzeInfo.MoveData = moveData;
		}
		else
		{
			analyzeInfo = new AnalyzeInfo(number, moveData);
			Add(number, analyzeInfo);
		}
		return analyzeInfo;
	}

	public AnalyzeInfo GetInfo(int number)
	{
		if (ContainsKey(number))
		{
			return base[number];
		}
		return null;
	}

	public void Load(SNotation notation)
	{
		notationFlag = true;
		Clear();
		foreach (MoveNode moveNode in notation.MoveNodes)
		{
			PvInfo pvInfo = null;
			PvInfo pvInfo2 = null;
			AnalyzeInfo analyzeInfo = null;
			foreach (string comment in moveNode.CommentList)
			{
				if (string.IsNullOrEmpty(comment) || comment[0] != '*')
				{
					continue;
				}
				PvInfo pvInfo3 = Parse(comment);
				if (pvInfo3.Kind == AnalyzeCommentKind.Analysis)
				{
					if (pvInfo3.Rank == 1)
					{
						pvInfo = pvInfo3;
					}
					else if (pvInfo == null)
					{
						pvInfo = pvInfo3;
					}
					else if (pvInfo.Rank >= pvInfo3.Rank)
					{
						pvInfo = pvInfo3;
					}
				}
				else if (pvInfo3.Kind == AnalyzeCommentKind.Candidate)
				{
					if (pvInfo3.Rank == 1)
					{
						pvInfo2 = pvInfo3;
					}
					else if (pvInfo == null)
					{
						pvInfo2 = pvInfo3;
					}
					else if (pvInfo.Rank >= pvInfo3.Rank)
					{
						pvInfo2 = pvInfo3;
					}
				}
			}
			if (pvInfo != null || pvInfo2 != null)
			{
				analyzeInfo = new AnalyzeInfo(moveNode.Number, moveNode);
				analyzeInfo.ThinkInfo = pvInfo;
				if (pvInfo2 != null)
				{
					analyzeInfo.Items.Add(pvInfo2);
				}
				else
				{
					analyzeInfo.Items.Add(pvInfo);
				}
				Add(moveNode.Number, analyzeInfo);

				// コメントから復元したScoreをMoveNodeに反映（評価グラフ用）
				// pvInfo = 最善の解析コメント（候補1）を優先
				PvInfo best = pvInfo ?? pvInfo2;
				if (best != null && best.HasEval)
				{
					moveNode.Score = best.Eval;
				}
			}
		}
		Total();
	}

	public void Total()
	{
		blackMoveInfo.Init();
		whiteMoveInfo.Init();
		foreach (AnalyzeInfo value in base.Values)
		{
			AnalyzeInfo info = GetInfo(value.Number - 1);
			if (value.MoveData == null || !value.MoveData.MoveType.IsMove())
			{
				continue;
			}
			AnalyzeMoveInfo analyzeMoveInfo = ((value.MoveData.Turn == PlayerColor.Black) ? blackMoveInfo : whiteMoveInfo);
			if (!value.MoveData.HasScore)
			{
				continue;
			}
			analyzeMoveInfo.Count++;
			if (value.MoveData.BestMove == MoveMatche.Best)
			{
				analyzeMoveInfo.Matches++;
			}
			if (info != null && info.MoveData != null && value.MoveData.HasScore && info.MoveData.HasScore)
			{
				int num = value.MoveData.Score - info.MoveData.Score;
				if (value.MoveData.Turn == PlayerColor.White)
				{
					num = -num;
				}
				if ((value.MoveData.Score >= -1500 || info.MoveData.Score >= -1500) && (value.MoveData.Score < 1500 || info.MoveData.Score < 1500))
				{
					if ((value.MoveData.Score >= -700 || info.MoveData.Score >= -700) && (value.MoveData.Score < 700 || info.MoveData.Score < 700))
					{
						analyzeMoveInfo.BadMoves700++;
						if (num < 0 && value.MoveData.BestMove != MoveMatche.Best)
						{
							analyzeMoveInfo.BadTotal700 += -num;
							analyzeMoveInfo.BadCount700++;
						}
					}
					analyzeMoveInfo.BadMoves1500++;
					if (num < 0 && value.MoveData.BestMove != MoveMatche.Best)
					{
						analyzeMoveInfo.BadTotal1500 += -num;
						analyzeMoveInfo.BadCount1500++;
					}
				}
			}
			if (info != null)
			{
				MoveEval moveEval = MoveEvalExtention.GetMoveEval(value.MoveData, info.MoveData);
				analyzeMoveInfo.Moves[(int)moveEval]++;
			}
		}
	}

	public static PvInfo Parse(string line)
	{
		AnalyzeComment analyzeComment = new AnalyzeComment();
		analyzeComment.Parse(line);
		PvInfo pvInfo = new PvInfo();
		pvInfo.Kind = analyzeComment.Kind;
		if (analyzeComment.Rank.HasValue)
		{
			pvInfo.Rank = analyzeComment.Rank.Value;
		}
		if (analyzeComment.Value.HasValue)
		{
			pvInfo.Score = analyzeComment.Value.Value;
		}
		if (analyzeComment.Mate.HasValue)
		{
			pvInfo.Mate = analyzeComment.Mate.Value;
		}
		if (analyzeComment.Time != string.Empty)
		{
			pvInfo.TimeMs = (int)AnalyzeCommentTokenizer.ParseTime(analyzeComment.Time);
		}
		if (analyzeComment.Depth != string.Empty)
		{
			string[] array = analyzeComment.Depth.Split(new char[2] { '/', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length >= 1)
			{
				pvInfo.Depth = (int)AnalyzeCommentTokenizer.ParseNum(array[0]);
			}
			if (array.Length >= 2)
			{
				pvInfo.SelDepth = (int)AnalyzeCommentTokenizer.ParseNum(array[1]);
			}
		}
		if (analyzeComment.Nodes != string.Empty)
		{
			pvInfo.Nodes = AnalyzeCommentTokenizer.ParseNum(analyzeComment.Nodes);
		}
		if (analyzeComment.Moves != null)
		{
			pvInfo.Message = analyzeComment.Moves;
		}
		return pvInfo;
	}
}
