using System.Collections.Generic;
using System.Linq;

namespace ShogiLib;

public static class SNotationUtility
{
	public static void AddBranches(this SNotation notation, List<MoveDataEx> moveDataList, MoveNode move_node, MoveAddMode add_mode)
	{
		if (moveDataList == null || moveDataList.Count == 0)
		{
			return;
		}
		bool flag = false;
		notation.ChangeCurrent(move_node);
		if (moveDataList[0].Turn == move_node.Turn)
		{
			notation.Prev(1);
			flag = true;
		}
		bool flag2 = true;
		for (int i = 0; i < moveDataList.Count; i++)
		{
			MoveData moveData = moveDataList[i];
			if (!MoveCheck.IsValid(notation.Position, moveData))
			{
				break;
			}
			if (add_mode == MoveAddMode.ADD_MERGE)
			{
				if (flag2 && notation.MoveCurrent.FindChildIndex(moveData) == 0)
				{
					notation.AddMove(new MoveDataEx(moveData), MoveAddMode.ADD, changeChildCurrent: false);
				}
				else
				{
					notation.AddMove(new MoveDataEx(moveData), MoveAddMode.MERGE, changeChildCurrent: false);
				}
			}
			else
			{
				notation.AddMove(new MoveDataEx(moveData), add_mode, changeChildCurrent: false);
			}
			flag2 = false;
		}
		if (flag)
		{
			notation.Next(1);
		}
	}

	public static void AddBranches(this SNotation notation, MoveData ponder, List<MoveDataEx> moveDataList, MoveNode move_node, MoveAddMode add_mode)
	{
		if (moveDataList == null || moveDataList.Count == 0)
		{
			return;
		}
		MoveNode moveCurrent = notation.MoveCurrent;
		bool flag = true;
		notation.ChangeCurrent(move_node);
		if (ponder != null && !move_node.Equals(ponder))
		{
			if (add_mode == MoveAddMode.ADD_MERGE)
			{
				if (notation.MoveCurrent.FindChildIndex(ponder) == 0)
				{
					notation.AddMove(new MoveDataEx(ponder), MoveAddMode.ADD, changeChildCurrent: false);
				}
				else
				{
					notation.AddMove(new MoveDataEx(ponder), MoveAddMode.MERGE, changeChildCurrent: false);
				}
			}
			else
			{
				notation.AddMove(new MoveDataEx(ponder), add_mode, changeChildCurrent: false);
			}
			flag = false;
		}
		for (int i = 0; i < moveDataList.Count; i++)
		{
			MoveData moveData = moveDataList[i];
			if (!MoveCheck.IsValid(notation.Position, moveData))
			{
				break;
			}
			if (add_mode == MoveAddMode.ADD_MERGE)
			{
				if (flag && notation.MoveCurrent.FindChildIndex(moveData) == 0)
				{
					notation.AddMove(new MoveDataEx(moveData), MoveAddMode.ADD, changeChildCurrent: false);
				}
				else
				{
					notation.AddMove(new MoveDataEx(moveData), MoveAddMode.MERGE, changeChildCurrent: false);
				}
			}
			else
			{
				notation.AddMove(new MoveDataEx(moveData), add_mode, changeChildCurrent: false);
			}
			flag = false;
		}
		notation.ChangeCurrent(moveCurrent);
	}

	public static void AddBranches(this SNotation notation, string moves, MoveNode move_node, MoveAddMode add_mode)
	{
		Kifu kifu = new Kifu();
		notation.ChangeCurrent(move_node);
		if (Kifu.GetPlayerColor(Kifu.GetFirstMove(moves)) == move_node.Turn)
		{
			notation.Prev(1);
		}
		kifu.ParseMoves(notation, add_mode, moves);
		notation.ChangeCurrent(move_node);
	}

	public static void WebMerge(this SNotation n1, SNotation n2, bool force)
	{
		MoveNode moveNode = n1.MoveFirst;
		MoveNode moveNode2 = null;
		for (MoveNode moveNode3 = n2.MoveFirst; moveNode3 != null; moveNode3 = moveNode3.ChildCurrent)
		{
			if ((moveNode3.Number <= n1.WebLoad || force) && moveNode != null)
			{
				if (moveNode.Equals(moveNode3))
				{
					moveNode.CommentList = moveNode3.CommentList;
				}
				else
				{
					moveNode2.InsertChild(0, new MoveNode((MoveDataEx)moveNode3), changeChildCurrent: false);
					moveNode = moveNode2.Children[0];
				}
			}
			else
			{
				moveNode2.InsertChild(0, new MoveNode((MoveDataEx)moveNode3), changeChildCurrent: false);
				moveNode = moveNode2.Children[0];
			}
			moveNode2 = moveNode;
			moveNode = ((moveNode.Children.Count == 0) ? null : moveNode.Children[0]);
		}
		n1.BlackName = n2.BlackName;
		n1.WhiteName = n2.WhiteName;
		n1.WinColor = n2.WinColor;
		n1.KifuInfos = DeepCopyHelper.DeepCopy(n2.KifuInfos);
		if (n1.WebLoad < n2.Count)
		{
			n1.WebLoad = n2.Count;
		}
	}

	public static int AddEngine(this SNotation notation, string engineName)
	{
		int num = -1;
		foreach (KeyValuePair<int, string> engine in notation.Engines)
		{
			if (engine.Value == engineName)
			{
				return engine.Key;
			}
			if (engine.Key > num)
			{
				num = engine.Key;
			}
		}
		int num2 = num + 1;
		notation.Engines.Add(num2, engineName);
		notation.MoveFirst.CommentAdd($"*Engines {num2} {engineName}");
		return num2;
	}

	public static int MaxEngineNo(this SNotation notation)
	{
		return notation.Engines.Keys.Max();
	}
}
