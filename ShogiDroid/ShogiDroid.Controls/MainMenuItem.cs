namespace ShogiDroid.Controls;

public class MainMenuItem
{
	public long Id;

	public int TextId;

	public bool Enable;

	public MainMenuItem()
	{
	}

	public MainMenuItem(MainMenuItem item)
	{
		Id = item.Id;
		TextId = item.TextId;
		Enable = item.Enable;
	}

	public MainMenuItem(long id, int textId)
	{
		Id = id;
		TextId = textId;
		Enable = true;
	}
}
