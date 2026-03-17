using System;
using ShogiLib;

namespace ShogiGUI.Engine;

public class InitializedEventArgs : EventArgs
{
	public PlayerColor Color { get; set; }

	public InitializedEventArgs(PlayerColor color)
	{
		Color = color;
	}
}
