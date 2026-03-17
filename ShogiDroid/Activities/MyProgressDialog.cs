using System;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace ShogiDroid;

public class MyProgressDialog : DialogFragment
{
	public EventHandler<EventArgs> CancelClick;

	private int progress;

	private TextView titleText;

	private TextView percentText;

	private TextView messageText;

	private ProgressBar progressBar;

	public int Progress
	{
		get
		{
			return progress;
		}
		set
		{
			progressBar.Progress = value;
			progress = value;
			percentText.Text = value + "%";
		}
	}

	public static MyProgressDialog NewInstance(int titleId)
	{
		MyProgressDialog myProgressDialog = new MyProgressDialog();
		Bundle bundle = new Bundle();
		bundle.PutInt("IconId", 16843252);
		bundle.PutInt("TitleId", titleId);
		myProgressDialog.Cancelable = false;
		myProgressDialog.Arguments = bundle;
		return myProgressDialog;
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Activity);
		AlertDialog dialog = builder.Create();
		View view = base.Activity.LayoutInflater.Inflate(Resource.Layout.progressdialog, null);
		dialog.SetView(view);
		int resId = base.Arguments.GetInt("TitleId");
		titleText = view.FindViewById<TextView>(Resource.Id.ProgressTitle);
		titleText.Text = Application.Context.GetString(resId);
		progressBar = view.FindViewById<ProgressBar>(Resource.Id.ProgressBar);
		progressBar.Max = 100;
		messageText = view.FindViewById<TextView>(Resource.Id.Message);
		percentText = view.FindViewById<TextView>(Resource.Id.ProgressPercent);
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

	public void SetMessage(string message)
	{
		messageText.Text = message;
	}
}
