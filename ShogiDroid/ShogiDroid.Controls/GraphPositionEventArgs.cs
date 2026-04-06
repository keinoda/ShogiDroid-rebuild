using System;

namespace ShogiDroid.Controls;

public class GraphPositionEventArgs : EventArgs
{
	public int Number { get; set; }

	public GraphPositionEventArgs(int number)
	{
		Number = number;
	}
}
