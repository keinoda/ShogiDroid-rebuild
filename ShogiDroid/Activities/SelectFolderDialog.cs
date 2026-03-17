using System;
using System.IO;
using System.Linq;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace ShogiDroid;

public class SelectFolderDialog : DialogFragment
{
	private string path;

	private string[] file_list;

	public EventHandler<EventArgs> OKClick;

	public EventHandler<EventArgs> CancelClick;

	public string FolderPath => path;

	public static SelectFolderDialog NewInstance(string path)
	{
		return new SelectFolderDialog
		{
			path = path
		};
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Activity);
		AlertDialog dialog = builder.Create();
		View view = base.Activity.LayoutInflater.Inflate(Resource.Layout.selectfolder, null);
		dialog.SetView(view);
		TextView textview = view.FindViewById<TextView>(Resource.Id.SelectFolderDialogPath);
		textview.Text = path;
		view.FindViewById<Button>(Resource.Id.DialogOKButton).Click += delegate(object sender, EventArgs e)
		{
			if (OKClick != null)
			{
				OKClick(sender, e);
			}
			dialog.Dismiss();
		};
		view.FindViewById<Button>(Resource.Id.DialogCancelButton).Click += delegate(object sender, EventArgs e)
		{
			if (CancelClick != null)
			{
				CancelClick(sender, e);
			}
			dialog.Dismiss();
		};
		file_list = LoadFileList(path);
		ListView listview = view.FindViewById<ListView>(Resource.Id.SelectFolderDialogListView);
		listview.Adapter = new ArrayAdapter<string>(base.Activity, 17367043, file_list);
		listview.ItemClick += delegate(object sender, AdapterView.ItemClickEventArgs e)
		{
			path = Path.Combine(path, file_list[e.Position]);
			textview.Text = path;
			file_list = LoadFileList(path);
			listview.Adapter = new ArrayAdapter<string>(base.Activity, 17367043, file_list);
		};
		view.FindViewById<Button>(Resource.Id.SelectFolderDialogUp).Click += delegate
		{
			path = Path.GetDirectoryName(path);
			textview.Text = path;
			file_list = LoadFileList(path);
			listview.Adapter = new ArrayAdapter<string>(base.Activity, 17367043, file_list);
		};
		return dialog;
	}

	private string[] LoadFileList(string path)
	{
		try
		{
			return (from filename in Directory.GetDirectories(path, "*")
				select Path.GetFileName(filename)).ToArray();
		}
		catch
		{
			return new string[0];
		}
	}
}
