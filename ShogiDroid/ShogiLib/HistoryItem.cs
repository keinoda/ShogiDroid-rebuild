namespace ShogiLib;

public class HistoryItem
{
	public int No;

	public int Count;

	public bool IsCheck;

	public HistoryItem(int no, bool check)
	{
		No = no;
		Count = 1;
		IsCheck = check;
	}
}
