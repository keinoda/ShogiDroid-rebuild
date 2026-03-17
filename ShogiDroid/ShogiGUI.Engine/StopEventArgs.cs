using System;
using ShogiLib;

namespace ShogiGUI.Engine;

public class StopEventArgs : EventArgs
{
	public PlayerColor Color { get; set; }

	public int TransactionNo { get; set; }

	public StopEventArgs(PlayerColor color, int transactionNo)
	{
		Color = color;
		TransactionNo = transactionNo;
	}
}
