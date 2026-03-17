using System;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace ShogiDroid;

public class CommentEditDialog : DialogFragment
{
	public EventHandler<EventArgs> OKClick;

	public EventHandler<EventArgs> CancelClick;

	private EditText commentEditText;

	private string comment;

	public string Comment
	{
		get
		{
			return comment;
		}
		set
		{
			comment = value;
		}
	}

	public static CommentEditDialog NewInstance(string comment)
	{
		return new CommentEditDialog
		{
			comment = comment
		};
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Activity);
		AlertDialog dialog = builder.Create();
		View view = base.Activity.LayoutInflater.Inflate(Resource.Layout.commenteditdialog, null);
		dialog.SetView(view);
		commentEditText = view.FindViewById<EditText>(Resource.Id.comments);
		commentEditText.Text = comment;
		((Button)view.FindViewById(Resource.Id.DialogOKButton)).Click += delegate(object sender, EventArgs e)
		{
			comment = commentEditText.Text;
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
