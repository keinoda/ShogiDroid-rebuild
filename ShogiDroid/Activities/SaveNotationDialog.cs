using System;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace ShogiDroid;

public class SaveNotationDialog : DialogFragment
{
	public EventHandler<EventArgs> OKClick;

	public EventHandler<EventArgs> CancelClick;

	private TextView filenameTextView;

	public string FileName
	{
		get
		{
			if (filenameTextView == null)
			{
				return string.Empty;
			}
			return filenameTextView.Text;
		}
	}

	public static SaveNotationDialog NewInstance(string path, string filename, string blackName, string whiteName)
	{
		SaveNotationDialog saveNotationDialog = new SaveNotationDialog();
		Bundle bundle = new Bundle();
		bundle.PutString("path", path);
		bundle.PutString("filename", filename);
		bundle.PutString("blackName", blackName);
		bundle.PutString("whiteName", whiteName);
		saveNotationDialog.Arguments = bundle;
		return saveNotationDialog;
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Activity);
		AlertDialog dialog = builder.Create();
		View view = base.Activity.LayoutInflater.Inflate(Resource.Layout.savenotationdialog, null);
		dialog.SetView(view);
		TextView textView = view.FindViewById<TextView>(Resource.Id.SaveDialogFolderName);
		textView.Text = base.Arguments.GetString("path");
		(filenameTextView = view.FindViewById<TextView>(Resource.Id.SaveDialogFileName)).Text = base.Arguments.GetString("filename");
		textView = view.FindViewById<TextView>(Resource.Id.SaveDialogBlackName);
		textView.Text = base.Arguments.GetString("blackName");
		textView = view.FindViewById<TextView>(Resource.Id.SaveDialogWhiteName);
		textView.Text = base.Arguments.GetString("whiteName");
		((Button)view.FindViewById(Resource.Id.SaveDialogOKButton)).Click += delegate(object sender, EventArgs e)
		{
			if (OKClick != null)
			{
				OKClick(sender, e);
			}
			dialog.Dismiss();
		};
		((Button)view.FindViewById(Resource.Id.SaveDialogCancelButton)).Click += delegate(object sender, EventArgs e)
		{
			if (CancelClick != null)
			{
				CancelClick(sender, e);
			}
			dialog.Dismiss();
		};
		return dialog;
	}
}
