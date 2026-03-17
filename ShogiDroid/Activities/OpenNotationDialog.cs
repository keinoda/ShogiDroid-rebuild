using System;
using System.IO;
using System.Linq;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace ShogiDroid;

public class OpenNotationDialog : DialogFragment
{
	private string filename;

	private string[] file_list;

	private string path;

	public EventHandler<EventArgs> OKClick;

	public EventHandler<EventArgs> CancelClick;

	public string FileName => filename;

	public static OpenNotationDialog NewInstance(string path)
	{
		return new OpenNotationDialog
		{
			path = path
		};
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Activity);
		AlertDialog dialog = builder.Create();
		View view = base.Activity.LayoutInflater.Inflate(Resource.Layout.openfiledialog, null);
		dialog.SetView(view);
		view.FindViewById<TextView>(Resource.Id.OpenFileDialogPath).Text = path;
		view.FindViewById<Button>(Resource.Id.OpenDialogCancelButton).Click += delegate(object sender, EventArgs e)
		{
			if (CancelClick != null)
			{
				CancelClick(sender, e);
			}
			dialog.Dismiss();
		};
		file_list = LoadFileList(path);
		ListView listView = view.FindViewById<ListView>(Resource.Id.OpenFileDialogListView);
		listView.Adapter = new ArrayAdapter<string>(base.Activity, 17367043, file_list);
		listView.ItemClick += delegate(object sender, AdapterView.ItemClickEventArgs e)
		{
			filename = Path.Combine(path, file_list[e.Position]);
			if (OKClick != null)
			{
				OKClick(sender, e);
			}
			dialog.Dismiss();
		};
		return dialog;
	}

	private string[] LoadFileList(string path)
	{
		try
		{
			return (from filename in Directory.GetFiles(path, "*.*")
				select Path.GetFileName(filename)).ToArray();
		}
		catch
		{
			return new string[0];
		}
	}
}
