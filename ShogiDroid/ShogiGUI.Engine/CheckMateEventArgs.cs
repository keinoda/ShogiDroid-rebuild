using System;
using System.Collections.Generic;
using ShogiLib;

namespace ShogiGUI.Engine;

public enum CheckMateResultKind
{
	Mate,
	NoMate,
	Timeout,
	None,
	NotImplemented
}

public class CheckMateEventArgs : EventArgs
{
	public List<MoveDataEx> Moves { get; set; }

	public bool IsMate { get; set; }

	public PlayerColor Color { get; set; }

	public int TransactionNo { get; set; }

	public CheckMateResultKind Kind { get; set; }

	public CheckMateEventArgs(PlayerColor color, int transactionNo, CheckMateResultKind kind)
	{
		Color = color;
		TransactionNo = transactionNo;
		Kind = kind;
		IsMate = kind == CheckMateResultKind.Mate;
	}

	public CheckMateEventArgs(PlayerColor color, int transactionNo, List<MoveDataEx> moves)
	{
		Color = color;
		TransactionNo = transactionNo;
		Moves = moves;
		Kind = CheckMateResultKind.Mate;
		IsMate = true;
	}
}
