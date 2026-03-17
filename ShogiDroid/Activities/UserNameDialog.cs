using System;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace ShogiDroid;

public class UserNameDialog : DialogFragment
{
	private string userName = string.Empty;

	private EditText userNamelEditText;

	public EventHandler<EventArgs> OKClick;

	public EventHandler<EventArgs> CancelClick;

	public string UserName
	{
		get
		{
			return userName;
		}
		set
		{
			userName = value;
		}
	}

	public static UserNameDialog NewInstance()
	{
		UserNameDialog result = new UserNameDialog();
		new Bundle();
		return result;
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Activity);
		AlertDialog dialog = builder.Create();
		View view = base.Activity.LayoutInflater.Inflate(Resource.Layout.usernamedialog, null);
		dialog.SetView(view);
		(userNamelEditText = view.FindViewById<EditText>(Resource.Id.UserName)).Text = userName;
		((Button)view.FindViewById(Resource.Id.DialogOKButton)).Click += delegate(object sender, EventArgs e)
		{
			userName = userNamelEditText.Text;
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
