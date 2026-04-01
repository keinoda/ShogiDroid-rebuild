using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using ShogiGUI.Engine;

namespace ShogiDroid;

public class ExternalEngineSelectDialog : DialogFragment
{
	private AlertDialog dialog;

	private string[] file_list;

	private string path;

	private int engineNo;

	private string engineName = string.Empty;

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
			return engineName;
		}
		set
		{
			engineName = value;
		}
	}

	public static ExternalEngineSelectDialog NewInstance(string path)
	{
		return new ExternalEngineSelectDialog
		{
			path = path
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
		file_list = LoadFileList(path);
		list.AddRange(file_list);
		builder.SetItems(list.ToArray(), ListClicked);
		dialog = builder.Create();
		return dialog;
	}

	private void ListClicked(object sender, DialogClickEventArgs e)
	{
		engineName = file_list[e.Which];
		engineNo = e.Which + InternalEngineCatalog.Count + 1;
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
			return (from filename in Directory.GetDirectories(path, "*.*")
				select Path.GetFileName(filename) into name
				where !InternalEngineCatalog.IsInternalEngineName(name)
				select name).ToArray();
		}
		catch
		{
			return new string[0];
		}
	}
}
