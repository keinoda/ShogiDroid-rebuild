using System;

namespace ShogiDroid.Controls.ShogiBoard;

public class PieceMoveEventArgs : EventArgs
{
	public PieceMoveData MoveData;

	public PieceMoveEventArgs(PieceMoveData moveData)
	{
		MoveData = moveData;
	}
}
