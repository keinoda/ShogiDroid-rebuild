namespace ShogiGUI;

public class AnalyzeSettings
{
	public int Time = 60000;

	public int AnalyzeTime = 5000;

	public bool AnalyzeDepthEnable;

	public int AnalyzeDepth = 15;

	public GameStartPosition AnalyzePositon;

	public bool Reverse;

	public int GetAnalyzeDepth()
	{
		if (!AnalyzeDepthEnable)
		{
			return -1;
		}
		return AnalyzeDepth;
	}
}
