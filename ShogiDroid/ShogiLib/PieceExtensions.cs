namespace ShogiLib;

public static class PieceExtensions
{
	private const int ColorShift = 4;

	public static PieceType TypeOf(this Piece piece)
	{
		PieceType pieceType = (PieceType)(piece & Piece.BHI);
		if (pieceType == PieceType.NoPieceType)
		{
			pieceType = (PieceType)(piece & Piece.BOU);
		}
		return pieceType;
	}

	public static int ToHandIndex(this Piece piece)
	{
		return (int)(piece & Piece.BHI);
	}

	public static PlayerColor ColorOf(this Piece piece)
	{
		if (piece == Piece.NoPiece)
		{
			return PlayerColor.NoColor;
		}
		return (PlayerColor)((int)(piece & Piece.WhiteFlag) >> 4);
	}

	public static Piece MakePiece(PieceType pt, PlayerColor color)
	{
		return (Piece)((uint)pt | (uint)((int)color << 4));
	}

	public static Piece PieceFlagFromColor(PlayerColor color)
	{
		return (Piece)((int)color << 4);
	}

	public static bool IsPromoted(this Piece piece)
	{
		return (int)(piece & Piece.BRYU) > 8;
	}

	public static bool IsWhite(this Piece piece)
	{
		return (piece & Piece.WhiteFlag) != 0;
	}

	public static Piece Opp(this Piece piece)
	{
		if (piece != Piece.NoPiece)
		{
			piece = ((piece.ColorOf() != PlayerColor.Black) ? (piece & (Piece)239) : (piece | Piece.WhiteFlag));
		}
		return piece;
	}
}
