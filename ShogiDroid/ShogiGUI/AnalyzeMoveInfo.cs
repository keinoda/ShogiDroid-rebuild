namespace ShogiGUI;

public class AnalyzeMoveInfo
{
	private int[] moves = new int[9];

	public int Count { get; set; }

	public int Matches { get; set; }

	public int BadMoves700 { get; set; }

	public int BadMoves1500 { get; set; }

	public int BadCount700 { get; set; }

	public int BadCount1500 { get; set; }

	public int BadTotal700 { get; set; }

	public int BadTotal1500 { get; set; }

	public int[] Moves => moves;

	public void Init()
	{
		Count = 0;
		Matches = 0;
		for (int i = 0; i < moves.Length; i++)
		{
			moves[i] = 0;
		}
		BadCount700 = 0;
		BadCount1500 = 0;
		BadTotal700 = 0;
		BadTotal1500 = 0;
		BadMoves700 = 0;
		BadMoves1500 = 0;
	}
}
