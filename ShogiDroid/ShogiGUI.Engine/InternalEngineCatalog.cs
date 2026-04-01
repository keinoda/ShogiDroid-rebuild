namespace ShogiGUI.Engine;

public static class InternalEngineCatalog
{
	public static readonly string[] EngineBaseNames = { "shinden3", "AobaNNUE" };

	public static int Count => EngineBaseNames.Length;

	public static int DefaultEngineNo => 1;

	public static string DefaultEngineName => GetEngineName(DefaultEngineNo);

	public static bool IsInternalEngineNo(int engineNo)
	{
		return engineNo >= 1 && engineNo <= Count;
	}

	public static bool IsInternalEngineName(string engineName)
	{
		foreach (string name in EngineBaseNames)
		{
			if (name == engineName)
			{
				return true;
			}
		}
		return false;
	}

	public static string GetEngineName(int engineNo)
	{
		return IsInternalEngineNo(engineNo) ? EngineBaseNames[engineNo - 1] : string.Empty;
	}
}
