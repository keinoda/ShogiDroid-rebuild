using System.Collections.Generic;
using System.IO;
using ShogiGUI.Events;
using ShogiGUI.Models;

namespace ShogiGUI.Presenters;

public class EngineInstallPresenter : PresenterBase<IEngineInstallView>
{
	private List<string> enginelist;

	private EngineInstaller installer;

	private string install_folder;

	public List<string> EngineList => enginelist;

	public EngineInstallPresenter(IEngineInstallView view)
		: base(view)
	{
		enginelist = new List<string>();
		installer = new EngineInstaller();
		installer.EngineInstallProgress += Installer_EngineInstallProgress;
	}

	public override void Initialize()
	{
	}

	public override void Resume()
	{
	}

	public override void Pause()
	{
		installer.InstallCancel(wait: true);
	}

	public override void Destory()
	{
	}

	public void EngineSelect(int no)
	{
		switch (no)
		{
		case 0:
			view.SelectLocalFile();
			break;
		case 1:
			view.DownloadWebPage();
			break;
		case 2:
			view.ShowSelectUninstallEngine();
			break;
		}
	}

	public bool ExistInstallFolder(string engine_name)
	{
		return installer.ExistInstallFolder(Settings.EngineSettings.GetExternalEngineFolder(), engine_name);
	}

	public void Install(string engine_name, EngineFileUri zipfile)
	{
		install_folder = Path.Combine(Settings.EngineSettings.GetExternalEngineFolder(), engine_name);
		installer.Install(zipfile, install_folder);
	}

	public void InstallCancel()
	{
		installer.InstallCancel(wait: false);
	}

	public string GetLicense()
	{
		string[] array = new string[6] { "Copying.txt", "Copylight.txt", "Copylight.html", "copying.txt", "copylight.txt", "copylight.html" };
		foreach (string path in array)
		{
			string text = Path.Combine(install_folder, path);
			if (File.Exists(text))
			{
				return text;
			}
		}
		return string.Empty;
	}

	public string GetReadMe()
	{
		string[] array = new string[4] { "Readme.html", "Readme.txt", "readme.html", "readme.txt" };
		foreach (string path in array)
		{
			string text = Path.Combine(install_folder, path);
			if (File.Exists(text))
			{
				return text;
			}
		}
		return string.Empty;
	}

	public void Uninstall(int engineNo, string enginename)
	{
		string engine_folder = Path.Combine(Settings.EngineSettings.GetExternalEngineFolder(), enginename);
		installer.Uninstall(engine_folder);
		if (Settings.EngineSettings.EngineNo == engineNo)
		{
			Settings.EngineSettings.EngineNo = 1;
			Settings.EngineSettings.EngineName = string.Empty;
			Domain.Game.EngineTerminate();
		}
	}

	private void Installer_EngineInstallProgress(object sender, EngineInstallProgressEventArgs e)
	{
		if (e.Progress == 1000)
		{
			view.InstallComplete(e.Error);
		}
		else
		{
			view.InstallProgress(e.FileName, e.Progress);
		}
	}
}
