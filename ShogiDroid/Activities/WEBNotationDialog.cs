using System;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace ShogiDroid;

public class WEBNotationDialog : DialogFragment
{
	private string url;

	private EditText urlEditText;

	public EventHandler<EventArgs> OKClick;

	public EventHandler<EventArgs> CancelClick;

	public string Url
	{
		get
		{
			return url;
		}
		set
		{
			url = value;
		}
	}

	public static WEBNotationDialog NewInstance()
	{
		WEBNotationDialog result = new WEBNotationDialog();
		new Bundle();
		return result;
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Activity);
		AlertDialog dialog = builder.Create();
		View view = base.Activity.LayoutInflater.Inflate(Resource.Layout.webnotationdialog, null);
		dialog.SetView(view);
		(urlEditText = view.FindViewById<EditText>(Resource.Id.WEBNotationDialogURL)).Text = url;
		((Button)view.FindViewById(Resource.Id.DialogOKButton)).Click += delegate(object sender, EventArgs e)
		{
			url = urlEditText.Text;
			if (OKClick != null)
			{
				OKClick(sender, e);
			}
			dialog.Dismiss();
		};
		((Button)view.FindViewById(Resource.Id.DialogCancelButton)).Click += delegate(object sender, EventArgs e)
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
