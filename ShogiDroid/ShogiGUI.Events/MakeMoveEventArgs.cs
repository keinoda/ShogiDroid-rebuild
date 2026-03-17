using System;
using ShogiLib;

namespace ShogiGUI.Events;

public class MakeMoveEventArgs : EventArgs
{
	public MoveData MoveData;

	public MakeMoveEventArgs(MoveData moveData)
	{
		MoveData = moveData;
	}
}
