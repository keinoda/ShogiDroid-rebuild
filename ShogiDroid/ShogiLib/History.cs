using System.Collections.Generic;
using System.Linq;

namespace ShogiLib;

public class History
{
	private const int RepCount = 4;

	private Dictionary<HashKey, HistoryItem> hashTable;

	private List<HashKey> moveList;

	public History()
	{
		hashTable = new Dictionary<HashKey, HistoryItem>();
		moveList = new List<HashKey>();
	}

	public void Clear()
	{
		hashTable.Clear();
		moveList.Clear();
	}

	public void Init(SNotation notation)
	{
		SPosition sPosition = (SPosition)notation.InitialPosition.Clone();
		Clear();
		foreach (MoveNode moveNode in notation.MoveNodes)
		{
			if (moveNode.MoveType.IsMove())
			{
				sPosition.Move(moveNode);
				if (moveNode.MoveType != MoveType.Pass)
				{
					Add(sPosition.HashKey, MoveCheck.IsCheck(sPosition, moveNode));
				}
			}
			if (moveNode == notation.MoveCurrent)
			{
				break;
			}
		}
	}

	public HistoryItem Add(HashKey hashkey, bool check)
	{
		HistoryItem historyItem;
		if (hashTable.ContainsKey(hashkey))
		{
			historyItem = hashTable[hashkey];
			historyItem.Count++;
			moveList.Add(hashkey);
		}
		else
		{
			moveList.Add(hashkey);
			historyItem = new HistoryItem(moveList.Count - 1, check);
			hashTable[hashkey] = historyItem;
		}
		return historyItem;
	}

	public bool IsRepetition()
	{
		if (moveList.Count == 0)
		{
			return false;
		}
		HashKey key = moveList.Last();
		return hashTable[key].Count >= 4;
	}

	public bool IsRepetitionCheck()
	{
		if (moveList.Count == 0)
		{
			return false;
		}
		HashKey key = moveList.Last();
		if (hashTable[key].Count >= 4 && hashTable[key].IsCheck)
		{
			for (int i = hashTable[key].No; i < moveList.Count; i += 2)
			{
				key = moveList[i];
				if (!hashTable[key].IsCheck)
				{
					return false;
				}
			}
			return true;
		}
		return false;
	}

	public bool IsRepetitionCheckOpp()
	{
		if (moveList.Count == 0)
		{
			return false;
		}
		HashKey key = moveList.Last();
		if (hashTable[key].Count >= 4 && !hashTable[key].IsCheck)
		{
			for (int i = hashTable[key].No + 1; i < moveList.Count; i += 2)
			{
				key = moveList[i];
				if (!hashTable[key].IsCheck)
				{
					return false;
				}
			}
			return true;
		}
		return false;
	}
}
