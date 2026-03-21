namespace ShogiGUI;

public class AnalyzeSettings
{
	public int Time = 60000;

	public int AnalyzeTime = 5000;

	public bool AnalyzeDepthEnable;

	public int AnalyzeDepth = 15;

	public GameStartPosition AnalyzePositon;

	public bool Reverse;

	// 並列解析設定
	public int ParallelWorkers = 8;
	public int ParallelNodesMillions = 10;
	public int ParallelThreadsPerWorker = 4;
	public int ParallelHashPerWorker = 2048;

	public int GetAnalyzeDepth()
	{
		if (!AnalyzeDepthEnable)
		{
			return -1;
		}
		return AnalyzeDepth;
	}
}
