namespace ShogiLib;

public class PieceBox
{
	private int[] box = new int[9];

	public int[] Box => box;

	private void init_box()
	{
		box[1] = 18;
		box[2] = 4;
		box[3] = 4;
		box[4] = 4;
		box[5] = 4;
		box[6] = 2;
		box[7] = 2;
		box[8] = 2;
	}

	public void Init(SPosition pos)
	{
		init_box();
		foreach (Piece item in pos.Board)
		{
			if (item != Piece.NoPiece)
			{
				box[(uint)item.TypeOf()]--;
			}
		}
		PieceType pieceType = PieceType.FU;
		while ((int)pieceType <= 7)
		{
			int blackHand = pos.GetBlackHand((int)pieceType);
			if (blackHand > 0)
			{
				box[(uint)pieceType] -= blackHand;
			}
			blackHand = pos.GetWhiteHand((int)pieceType);
			if (blackHand > 0)
			{
				box[(uint)pieceType] -= blackHand;
			}
			pieceType++;
		}
	}

	public bool IsValid()
	{
		bool result = true;
		int[] array = box;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] < 0)
			{
				result = false;
				break;
			}
		}
		return result;
	}
}
