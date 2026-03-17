using System;

namespace ShogiGUI;

public class UnzipEventArgs : EventArgs
{
	public int Progress { get; set; }

	public string FileName { get; set; }

	public UnzipEventArgs(string filename, int progress)
	{
		Progress = progress;
		FileName = filename;
	}
}
