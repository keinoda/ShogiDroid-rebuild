using System;
using Android.Graphics;

namespace ShogiDroid.Controls.ShogiBoard;

public class PieceUpdateEventArgs : EventArgs
{
	public Point Pos;

	public PieceUpdateEventArgs(Point pos)
	{
		Pos = pos;
	}
}
