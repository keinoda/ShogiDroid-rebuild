namespace ShogiLib;

public struct MoveCoord
{
	public int Rank;

	public int File;

	public MoveCoord(int rank, int file)
	{
		Rank = rank;
		File = file;
	}
}
