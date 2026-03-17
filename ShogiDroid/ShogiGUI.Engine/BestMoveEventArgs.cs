using System;
using ShogiLib;

namespace ShogiGUI.Engine;

public class BestMoveEventArgs : EventArgs
{
	public PlayerColor Color { get; set; }

	public int TransactionNo { get; set; }

	public MoveData BestMove { get; set; }

	public MoveData Ponder { get; set; }

	public BestMoveEventArgs(PlayerColor color, int transactionNo, MoveData bestmove, MoveData ponder)
	{
		Color = color;
		TransactionNo = transactionNo;
		BestMove = bestmove;
		Ponder = ponder;
	}
}
