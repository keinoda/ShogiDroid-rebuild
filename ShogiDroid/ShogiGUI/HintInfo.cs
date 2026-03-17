using System.Collections.Generic;
using ShogiGUI.Engine;
using ShogiLib;

namespace ShogiGUI;

public class HintInfo
{
	private const int HintInfoMax = 3;

	private HashKey hashKey;

	private MoveData moveCurrent;

	private List<MoveData>[] moveArray;

	public MoveData MoveCurrent => moveCurrent;

	public List<MoveData>[] MoveArray => moveArray;

	public HintInfo()
	{
		moveArray = new List<MoveData>[3];
		for (int i = 0; i < 3; i++)
		{
			moveArray[i] = new List<MoveData>();
		}
	}

	public void Clear()
	{
		moveCurrent = null;
		for (int i = 0; i < 3; i++)
		{
			moveArray[i].Clear();
		}
	}

	public bool IsEqual(HashKey key, MoveData movedata)
	{
		if (hashKey.Equals(key))
		{
			return moveCurrent.Equals(movedata);
		}
		return false;
	}

	public void UpdateInfo(PvInfo info, SPosition pos, MoveData movedata)
	{
		if (info != null && info.PvMoves != null)
		{
			int num = info.Rank - 1;
			if (num < 0)
			{
				num = 0;
			}
			if (num < 3 && info.PvMoves.Count != 0)
			{
				hashKey = pos.HashKey;
				moveCurrent = new MoveData(movedata);
				moveArray[num] = new List<MoveData>(info.PvMoves);
			}
		}
	}
}
