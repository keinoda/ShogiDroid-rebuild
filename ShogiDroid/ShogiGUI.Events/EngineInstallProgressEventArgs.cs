using System;

namespace ShogiGUI.Events;

public class EngineInstallProgressEventArgs : EventArgs
{
	public enum InstallError
	{
		NoError,
		Cancel,
		Error
	}

	private int progress;

	private InstallError error;

	private string filename;

	public int Progress => progress;

	public string FileName => filename;

	public InstallError Error => error;

	public EngineInstallProgressEventArgs()
	{
		progress = 0;
		error = InstallError.NoError;
		filename = string.Empty;
	}

	public EngineInstallProgressEventArgs(string filename, int progress)
	{
		this.filename = filename;
		this.progress = progress;
		error = InstallError.NoError;
	}

	public EngineInstallProgressEventArgs(InstallError error)
	{
		filename = string.Empty;
		progress = 1000;
		this.error = error;
	}
}
