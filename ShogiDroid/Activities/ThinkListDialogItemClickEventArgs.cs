using System;

namespace ShogiDroid;

public class ThinkListDialogItemClickEventArgs : EventArgs
{
	public int Position { get; set; }

	public string Moves { get; set; }

	public ThinkListDialogItemClickEventArgs(int pos, string moves)
	{
		Position = pos;
		Moves = moves;
	}
}
