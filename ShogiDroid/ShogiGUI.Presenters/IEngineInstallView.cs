using ShogiGUI.Events;

namespace ShogiGUI.Presenters;

public interface IEngineInstallView
{
	void SelectLocalFile();

	void DownloadWebPage();

	void InstallProgress(string filename, int progress);

	void InstallComplete(EngineInstallProgressEventArgs.InstallError error);

	void ShowSelectUninstallEngine();
}
