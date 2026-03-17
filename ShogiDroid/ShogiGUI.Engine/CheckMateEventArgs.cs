using System;
using System.Collections.Generic;
using ShogiLib;

namespace ShogiGUI.Engine;

public class CheckMateEventArgs : EventArgs
{
	public List<MoveDataEx> Moves { get; set; }

	public bool IsMate { get; set; }

	public CheckMateEventArgs()
	{
		IsMate = false;
	}

	public CheckMateEventArgs(List<MoveDataEx> moves)
	{
		Moves = moves;
		IsMate = true;
	}
}
