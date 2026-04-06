using System;
using Android.Graphics;
using ShogiGUI;
using ShogiLib;

namespace ShogiDroid.Controls.ShogiBoard;

public class PieceMoveAnimation
{
	private PieceMoveData moveData;

	private PieceMoveState moveState;

	private int frame = 6;

	private int count;

	private Point src;

	private Point dest;

	private Point now;

	private int squareWidth = 64;

	private int minFrame = 6;

	private int maxFrame = 15;

	private int squareSpeed = 3;

	private PieceMoveQueue queue;

	private PiecePositionFromMoveData fromPos;

	private PiecePositionFromMoveData toPos;

	public PieceMoveState MoveSate => moveState;

	public MoveData MoveData => moveData;

	public Point NowPoint => now;

	public event EventHandler<PieceMoveEventArgs> MoveStartEvent;

	public event EventHandler<PieceMoveEventArgs> MoveEndEvent;

	public event EventHandler<PieceUpdateEventArgs> PieceUpdateEvent;

	public PieceMoveAnimation(int width, PiecePositionFromMoveData fromPos, PiecePositionFromMoveData toPos)
	{
		queue = new PieceMoveQueue();
		this.fromPos = fromPos;
		this.toPos = toPos;
		squareWidth = width;
		Init();
	}

	public void Init()
	{
		moveState = PieceMoveState.Stop;
		count = 0;
		queue.Clear();
	}

	public void Add(PieceMoveData moveData)
	{
		if (IsAnimation())
		{
			queue.Add(moveData);
			return;
		}
		MoveNext(moveData);
		Animation();
	}

	public void MoveNext(PieceMoveData moveData)
	{
		moveState = PieceMoveState.Play;
		this.moveData = moveData;
		if (moveData.Dir == MoveDataDir.Next)
		{
			src = fromPos(moveData);
			dest = toPos(moveData);
		}
		else
		{
			dest = fromPos(moveData);
			src = toPos(moveData);
		}
		now = src;
		count = 0;
		int val = Math.Abs(src.X - dest.X);
		int val2 = Math.Abs(src.Y - dest.Y);
		int num = Math.Max(val, val2) * squareSpeed / squareWidth;
		if (num < minFrame)
		{
			num = minFrame;
		}
		else if (num > maxFrame)
		{
			num = maxFrame;
		}
		frame = num;
		OnMoveStart(moveData);
	}

	public void Stop()
	{
		Init();
	}

	public bool IsAnimation()
	{
		return moveState != PieceMoveState.Stop;
	}

	public bool IsMoving(int square, Piece piece)
	{
		if (moveState == PieceMoveState.Stop)
		{
			return false;
		}
		if (moveData.MoveType == MoveType.Pass || moveData.MoveType.HasFlag(MoveType.DropFlag))
		{
			return false;
		}
		if (moveState == PieceMoveState.Play && square != moveData.FromSquare)
		{
			return false;
		}
		return true;
	}

	public bool IsMoving(Piece piece)
	{
		if (moveState == PieceMoveState.Stop)
		{
			return false;
		}
		if (moveData.MoveType == MoveType.Pass || !moveData.MoveType.HasFlag(MoveType.DropFlag))
		{
			return false;
		}
		if (moveState == PieceMoveState.Play && moveData.Piece != piece)
		{
			return false;
		}
		return true;
	}

	public void Animation()
	{
		if (moveState == PieceMoveState.Stop)
		{
			return;
		}
		count++;
		if (count >= frame)
		{
			if (queue.IsEmpty())
			{
				moveState = PieceMoveState.Stop;
				OnMoveEnd(moveData);
				OnPieceUpdate(now);
				return;
			}
			PieceMoveData pieceMoveData = moveData;
			moveData = queue.Get();
			OnMoveEnd(pieceMoveData);
			OnPieceUpdate(now);
			MoveNext(moveData);
		}
		else
		{
			OnPieceUpdate(now);
		}
		now.X = src.X + (dest.X - src.X) * count / frame;
		now.Y = src.Y + (dest.Y - src.Y) * count / frame;
		OnPieceUpdate(now);
	}

	public void SetAnimaSpeed(AnimeSpeed speed)
	{
		switch (speed)
		{
		case AnimeSpeed.Slow:
			minFrame = 15;
			maxFrame = 35;
			squareSpeed = 8;
			break;
		case AnimeSpeed.Fast:
			minFrame = 3;
			maxFrame = 7;
			squareSpeed = 2;
			break;
		default:
			minFrame = 6;
			maxFrame = 15;
			squareSpeed = 4;
			break;
		}
	}

	public void Resize(int width)
	{
		squareWidth = width;
		if (IsAnimation())
		{
			if (moveData.Dir == MoveDataDir.Next)
			{
				src = fromPos(moveData);
				dest = toPos(moveData);
			}
			else
			{
				dest = fromPos(moveData);
				src = toPos(moveData);
			}
			now.X = src.X + (dest.X - src.X) * count / frame;
			now.Y = src.Y + (dest.Y - src.Y) * count / frame;
		}
	}

	private void OnMoveStart(PieceMoveData moveData)
	{
		if (this.MoveStartEvent != null)
		{
			this.MoveStartEvent(this, new PieceMoveEventArgs(moveData));
		}
	}

	private void OnMoveEnd(PieceMoveData moveData)
	{
		if (this.MoveEndEvent != null)
		{
			this.MoveEndEvent(this, new PieceMoveEventArgs(moveData));
		}
	}

	private void OnPieceUpdate(Point pos)
	{
		if (this.PieceUpdateEvent != null)
		{
			this.PieceUpdateEvent(this, new PieceUpdateEventArgs(pos));
		}
	}
}
