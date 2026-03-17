using System;

namespace ShogiDroid.Controls;

public class GraphPositoinEventArgs : EventArgs
{
	public int Number;

	public GraphPositoinEventArgs(int number)
	{
		Number = number;
	}
}
