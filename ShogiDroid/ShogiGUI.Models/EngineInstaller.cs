using System;
using System.IO;
using System.Threading;
using Android.App;
using Android.Media;
using Android.Net;
using Java.IO;
using ShogiDroid;
using ShogiGUI.Engine;
using ShogiGUI.Events;
using ShogiLib;

namespace ShogiGUI.Models;

public class EngineInstaller
{
	public delegate bool IsCanceled();

	private class Param
	{
		public EngineFileUri Zipfile;

		public string Folder;

		public Param(EngineFileUri zipfile, string folder)
		{
			Zipfile = zipfile;
			Folder = folder;
		}
	}

	private SynchronizationContext syncContext;

	private Thread th;

	private bool cancel;

	public event EventHandler<EngineInstallProgressEventArgs> EngineInstallProgress;

	public EngineInstaller()
	{
		syncContext = SynchronizationContext.Current;
	}

	public bool ExistInstallFolder(string path, string name)
	{
		return Directory.Exists(Path.Combine(path, name));
	}

	public void Install(EngineFileUri zipfile, string engine_folder)
	{
		cancel = false;
		th = new Thread(do_work);
		th.Start(new Param(zipfile, engine_folder));
	}

	public void Uninstall(string engine_folder)
	{
		try
		{
			new ExternalEnginePlayer(PlayerColor.Black, engine_folder).Uninstall();
			if (Directory.Exists(engine_folder))
			{
				Directory.Delete(engine_folder, recursive: true);
			}
		}
		catch
		{
		}
	}

	private void OnInstallProgress(EngineInstallProgressEventArgs e)
	{
		syncContext.Post(delegate
		{
			if (this.EngineInstallProgress != null)
			{
				this.EngineInstallProgress(this, e);
			}
		}, null);
	}

	public void InstallCancel(bool wait)
	{
		cancel = true;
		if (wait && th != null)
		{
			th.Join();
		}
	}

	private void do_work(object arg)
	{
		EngineInstallProgressEventArgs.InstallError installError = EngineInstallProgressEventArgs.InstallError.NoError;
		Param param = (Param)arg;
		bool flag = false;
		string text = param.Zipfile.Path;
		try
		{
			if (string.IsNullOrEmpty(text))
			{
				text = CopyFile(param.Zipfile.Uri);
				flag = true;
			}
			if (string.IsNullOrEmpty(text))
			{
				installError = EngineInstallProgressEventArgs.InstallError.Error;
			}
			else if (cancel)
			{
				installError = EngineInstallProgressEventArgs.InstallError.Cancel;
			}
			else
			{
				if (Directory.Exists(param.Folder))
				{
					Directory.Delete(param.Folder, recursive: true);
				}
				Directory.CreateDirectory(param.Folder);
				if (cancel)
				{
					installError = EngineInstallProgressEventArgs.InstallError.Cancel;
				}
				else
				{
					UnZip.ExtractToDirectory(text, param.Folder, delegate(object sender, UnzipEventArgs e)
					{
						OnInstallProgress(new EngineInstallProgressEventArgs(e.FileName, e.Progress));
					}, () => cancel);
					if (cancel)
					{
						installError = EngineInstallProgressEventArgs.InstallError.Cancel;
					}
				}
			}
		}
		catch (Exception)
		{
			installError = EngineInstallProgressEventArgs.InstallError.Error;
		}
		if (flag)
		{
			DeleteFile(text);
		}
		if (installError != EngineInstallProgressEventArgs.InstallError.NoError)
		{
			try
			{
				Directory.Delete(param.Folder, recursive: true);
			}
			catch
			{
			}
		}
		else
		{
			MediaScannerConnection.ScanFile(Application.Context, new string[1] { param.Folder }, null, new MediaScannerClient());
		}
		OnInstallProgress(new EngineInstallProgressEventArgs(installError));
	}

	private string CopyFile(Android.Net.Uri uri)
	{
		string result = string.Empty;
		byte[] array = new byte[16384];
		using (Java.IO.File file = Java.IO.File.CreateTempFile("temp", "zip", Application.Context.ExternalCacheDir))
		{
			result = file.Path;
			_ = string.Empty;
			using System.IO.Stream stream = Application.Context.ContentResolver.OpenInputStream(uri);
			using FileOutputStream fileOutputStream = new FileOutputStream(file);
			long fileSize = Util.GetFileSize(Application.Context, uri);
			long num = 0L;
			int num2 = 0;
			int num3;
			while ((num3 = stream.Read(array, 0, array.Length)) > 0)
			{
				fileOutputStream.Write(array, 0, num3);
				if (cancel)
				{
					break;
				}
				num += num3;
				int num4 = num2;
				num2 = (int)((fileSize > 0) ? (num * 100 / fileSize) : (num / 524288 % 100));
				if (num4 != num2)
				{
					OnInstallProgress(new EngineInstallProgressEventArgs("Copying files", num2));
				}
			}
		}
		return result;
	}

	private void DeleteFile(string path)
	{
		try
		{
			if (!string.IsNullOrEmpty(path))
			{
				System.IO.File.Delete(path);
			}
		}
		catch
		{
		}
	}
}
