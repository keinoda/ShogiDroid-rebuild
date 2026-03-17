using System;
using ShogiLib;

namespace ShogiGUI.Engine;

public class ReadyOkEventArgs : EventArgs
{
	public PlayerColor Color { get; set; }

	public ReadyOkEventArgs(PlayerColor color)
	{
		Color = color;
	}
}
