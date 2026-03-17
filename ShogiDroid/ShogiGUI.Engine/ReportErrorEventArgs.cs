using System;
using ShogiLib;

namespace ShogiGUI.Engine;

public class ReportErrorEventArgs : EventArgs
{
	public PlayerColor Color { get; set; }

	public int TransactionNo { get; set; }

	public PlayerErrorId ErrorId { get; set; }

	public ReportErrorEventArgs(PlayerColor color, int transactionNo, PlayerErrorId err)
	{
		Color = color;
		TransactionNo = transactionNo;
		ErrorId = err;
	}
}
