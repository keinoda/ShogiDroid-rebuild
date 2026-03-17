using System;
using ShogiLib;

namespace ShogiGUI.Engine;

public class InfoEventArgs : EventArgs
{
	public PlayerColor Color { get; set; }

	public int TransactionNo { get; set; }

	public PvInfo PvInfo { get; set; }

	public InfoEventArgs(PlayerColor color, int transactionNo, PvInfo pvinfo)
	{
		Color = color;
		TransactionNo = transactionNo;
		PvInfo = pvinfo;
	}
}
