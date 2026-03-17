using Android.Graphics;

namespace ShogiDroid.Controls.ShogiBoard;

public class Rectangle
{
	public int X { get; set; }

	public int Y { get; set; }

	public int Width { get; set; }

	public int Height { get; set; }

	public int Top => Y;

	public int Bottom => Y + Height - 1;

	public int Left => X;

	public int Right => X + Width - 1;

	public Rect Rect => new Rect(X, Y, X + Width - 1, Y + Height - 1);

	public Rectangle(int x, int y, int width, int height)
	{
		X = x;
		Y = y;
		Width = width;
		Height = height;
	}
}
