using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using ShogiGUI.Engine;

namespace ShogiDroid;

public class EngineSelectDialog : DialogFragment
{
	private const string RemoteEngineLabel = "リモートエンジン";

	private int engineNo;

	private string enginename;

	private AlertDialog dialog;

	private string[] file_list;

	private string path;

	public EventHandler<EventArgs> OKClick;

	public EventHandler<EventArgs> CancelClick;

	public int EngineNo
	{
		get
		{
			return engineNo;
		}
		set
		{
			engineNo = value;
		}
	}

	public string EngineName
	{
		get
		{
			return enginename;
		}
		set
		{
			enginename = value;
		}
	}

	public static EngineSelectDialog NewInstance(string path, int engineNo, string name)
	{
		return new EngineSelectDialog
		{
			path = path,
			enginename = name,
			engineNo = engineNo
		};
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Activity);
		string title = GetString(Resource.String.Menu_EngineSelect_Text);
		List<string> list = new List<string>();
		builder.SetTitle(title);
		builder.SetNegativeButton(Resource.String.DialogCancel_Text, delegate(object sender, DialogClickEventArgs e)
		{
			if (CancelClick != null)
			{
				CancelClick(sender, e);
			}
		});
		list.Add(InternalEnginePlayer.EngineBaseName);
		file_list = LoadFileList(path);
		list.AddRange(file_list);
		list.Add(RemoteEngineLabel);
		int num = 0;
		if (engineNo == RemoteEnginePlayer.RemoteEngineNo)
		{
			num = list.Count - 1;
		}
		else if (engineNo != 1)
		{
			num = Array.IndexOf(file_list, enginename);
			if (num >= 0)
			{
				num++;
			}
		}
		builder.SetSingleChoiceItems(list.ToArray(), num, ListClicked);
		dialog = builder.Create();
		return dialog;
	}

	private void ListClicked(object sender, DialogClickEventArgs e)
	{
		int remoteIndex = 1 + file_list.Length;
		if (e.Which == 0)
		{
			enginename = InternalEnginePlayer.EngineBaseName;
			engineNo = 1;
		}
		else if (e.Which == remoteIndex)
		{
			enginename = RemoteEngineLabel;
			engineNo = RemoteEnginePlayer.RemoteEngineNo;
		}
		else
		{
			enginename = file_list[e.Which - 1];
			engineNo = e.Which + 1;
		}
		if (OKClick != null)
		{
			OKClick(sender, e);
		}
		dialog.Dismiss();
	}

	private string[] LoadFileList(string path)
	{
		try
		{
			AppDebug.Log.Info($"EngineSelectDialog: scanning {path}");
			var result = (from filename in Directory.GetDirectories(path, "*.*")
				select Path.GetFileName(filename) into name
				where name != InternalEnginePlayer.EngineBaseName
				select name).ToArray();
			AppDebug.Log.Info($"EngineSelectDialog: found {result.Length} engines: {string.Join(", ", result)}");
			return result;
		}
		catch (Exception e)
		{
			AppDebug.Log.ErrorException(e, $"EngineSelectDialog: failed to scan {path}");
			return new string[0];
		}
	}
}
