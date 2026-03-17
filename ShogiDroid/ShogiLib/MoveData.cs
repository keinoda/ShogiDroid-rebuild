namespace ShogiLib;

public class MoveData
{
	public int ToSquare { get; set; }

	public int FromSquare { get; set; }

	public MoveType MoveType { get; set; }

	public Piece Piece { get; set; }

	public Piece CapturePiece { get; set; }

	public PlayerColor Turn
	{
		get
		{
			if ((Piece & Piece.WhiteFlag) == 0)
			{
				return PlayerColor.Black;
			}
			return PlayerColor.White;
		}
	}

	public MoveData()
	{
		Init();
	}

	public MoveData(MoveData moveData)
	{
		Copy(this, moveData);
	}

	public MoveData(MoveType moveType)
	{
		MoveType = moveType;
		ToSquare = 0;
		FromSquare = 0;
		Piece = Piece.NoPiece;
		CapturePiece = Piece.NoPiece;
	}

	public MoveData(MoveType moveType, int from, int to, Piece piece, Piece capture)
	{
		MoveType = moveType;
		ToSquare = to;
		FromSquare = from;
		Piece = piece;
		CapturePiece = capture;
	}

	private void Init()
	{
		ToSquare = 0;
		FromSquare = 0;
		MoveType = MoveType.NoMove;
		Piece = Piece.NoPiece;
		CapturePiece = Piece.NoPiece;
	}

	public virtual void Initialize()
	{
		Init();
	}

	public static void Copy(MoveData dest, MoveData src)
	{
		dest.ToSquare = src.ToSquare;
		dest.FromSquare = src.FromSquare;
		dest.MoveType = src.MoveType;
		dest.Piece = src.Piece;
		dest.CapturePiece = src.CapturePiece;
	}

	public bool Equals(MoveData movedata)
	{
		bool result = false;
		if (movedata == null)
		{
			return false;
		}
		if (MoveType.HasFlag(MoveType.DropFlag) && movedata.MoveType.HasFlag(MoveType.DropFlag))
		{
			if (ToSquare == movedata.ToSquare && Piece == movedata.Piece)
			{
				result = true;
			}
		}
		else if (MoveType.HasFlag(MoveType.MoveFlag) && movedata.MoveType.HasFlag(MoveType.MoveFlag))
		{
			if (FromSquare == movedata.FromSquare && ToSquare == movedata.ToSquare && (MoveType & MoveType.MoveMask) == (movedata.MoveType & MoveType.MoveMask))
			{
				result = true;
			}
		}
		else if (MoveType == movedata.MoveType)
		{
			result = true;
		}
		return result;
	}
}
