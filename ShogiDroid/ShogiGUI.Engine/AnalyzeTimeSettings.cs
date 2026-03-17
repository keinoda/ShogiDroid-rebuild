namespace ShogiGUI.Engine;

public class AnalyzeTimeSettings
{
	public long Time;

	public long Nodes;

	public long Depth;

	public AnalyzeTimeSettings(long time, long nodes, long depth)
	{
		Time = time;
		Nodes = nodes;
		Depth = depth;
	}
}
