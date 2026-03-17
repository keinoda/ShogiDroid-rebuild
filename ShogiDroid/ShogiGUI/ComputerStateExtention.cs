namespace ShogiGUI;

public static class ComputerStateExtention
{
	public static bool IsThinking(this ComputerState state)
	{
		if (state == ComputerState.None || state == ComputerState.Stop)
		{
			return false;
		}
		return true;
	}
}
