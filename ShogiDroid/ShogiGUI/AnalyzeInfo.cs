using System.Collections.Generic;
using ShogiGUI.Engine;
using ShogiLib;

namespace ShogiGUI;

public class AnalyzeInfo
{
	private static int transactionNo;

	public int TransactionNo;

	public int Number;

	public MoveNode MoveData;

	public PvInfo ThinkInfo;

	private List<PvInfo> items;

	public List<PvInfo> Items => items;

	public AnalyzeInfo(int number, MoveNode move_data)
	{
		Number = number;
		MoveData = move_data;
		items = new List<PvInfo>();
		TransactionNo = transactionNo++;
		ThinkInfo = null;
	}

	public void Clear()
	{
		items.Clear();
	}
}
