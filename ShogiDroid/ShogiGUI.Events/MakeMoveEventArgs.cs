using System;
using ShogiLib;

namespace ShogiGUI.Events;

public class MakeMoveEventArgs : EventArgs
{
	public MoveData MoveData { get; set; }

	public MakeMoveEventArgs(MoveData moveData)
	{
		MoveData = moveData;
	}
}
