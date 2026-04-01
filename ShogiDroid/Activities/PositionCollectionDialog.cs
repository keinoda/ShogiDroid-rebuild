using System;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace ShogiDroid;

public class PositionCollectionDialog : DialogFragment
{
	private int totalCount;

	private string sourceName = string.Empty;

	public int SelectedIndex { get; private set; } = -1;

	public EventHandler<EventArgs> OKClick;

	public EventHandler<EventArgs> CancelClick;

	public static PositionCollectionDialog NewInstance(string sourceName, int totalCount)
	{
		return new PositionCollectionDialog
		{
			sourceName = sourceName ?? string.Empty,
			totalCount = totalCount
		};
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Activity);
		AlertDialog dialog = builder.Create();
		View view = base.Activity.LayoutInflater.Inflate(Resource.Layout.positioncollectiondialog, null);
		dialog.SetView(view);

		view.FindViewById<TextView>(Resource.Id.PositionCollectionDialogTitle).Text =
			GetString(Resource.String.PositionCollectionDialogTitle_Text);
		view.FindViewById<TextView>(Resource.Id.PositionCollectionDialogMessage).Text =
			string.Format(
				GetString(Resource.String.PositionCollectionDialogMessage_Text),
				sourceName,
				totalCount);

		EditText indexInput = view.FindViewById<EditText>(Resource.Id.PositionCollectionDialogIndex);
		indexInput.Hint = string.Format(GetString(Resource.String.PositionCollectionDialogIndexHint_Text), totalCount);
		indexInput.Text = "1";

		view.FindViewById<Button>(Resource.Id.PositionCollectionDialogCancelButton).Click += delegate(object sender, EventArgs e)
		{
			if (CancelClick != null)
			{
				CancelClick(sender, e);
			}
			dialog.Dismiss();
		};

		view.FindViewById<Button>(Resource.Id.PositionCollectionDialogRandomButton).Click += delegate(object sender, EventArgs e)
		{
			SelectedIndex = System.Random.Shared.Next(totalCount);
			if (OKClick != null)
			{
				OKClick(sender, e);
			}
			dialog.Dismiss();
		};

		view.FindViewById<Button>(Resource.Id.PositionCollectionDialogOpenButton).Click += delegate(object sender, EventArgs e)
		{
			if (!int.TryParse(indexInput.Text, out var index) || index < 1 || index > totalCount)
			{
				Toast.MakeText(
					base.Activity,
					string.Format(GetString(Resource.String.PositionCollectionInputError_Text), totalCount),
					ToastLength.Short).Show();
				return;
			}

			SelectedIndex = index - 1;
			if (OKClick != null)
			{
				OKClick(sender, e);
			}
			dialog.Dismiss();
		};

		return dialog;
	}
}
