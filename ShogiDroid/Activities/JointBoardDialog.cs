using Orientation = Android.Content.Res.Orientation;
using System;
using Android.App;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using Android.Widget;
using ShogiDroid.Controls.ShogiBoard;
using ShogiGUI;
using ShogiGUI.Events;
using ShogiLib;

namespace ShogiDroid;

public class JointBoardDialog : DialogFragment
{
	private ShogiBoard shogiBoard;

	private ImageButton prevButton;

	private ImageButton nextButton;

	private ImageButton reverseButton;

	private SNotation notation;

	public static JointBoardDialog NewInstance(bool reverse, SNotation notation)
	{
		JointBoardDialog jointBoardDialog = new JointBoardDialog();
		Bundle bundle = new Bundle();
		bundle.PutBoolean("reverse", reverse);
		jointBoardDialog.Arguments = bundle;
		jointBoardDialog.notation = notation;
		return jointBoardDialog;
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		bool boolean = base.Arguments.GetBoolean("reverse", defaultValue: false);
		Dialog dialog = new Dialog(base.Activity);
		View view = base.Activity.LayoutInflater.Inflate(Resource.Layout.jointboarddialog, null);
		dialog.RequestWindowFeature(1);
		dialog.SetContentView(view);
		shogiBoard = view.FindViewById<ShogiBoard>(Resource.Id.shogiboard);
		shogiBoard.MakeMoveEvent += ShogiBoard_MakeMoveEvent;
		prevButton = view.FindViewById<ImageButton>(Resource.Id.prev_button);
		prevButton.Click += PrevButton_Click;
		nextButton = view.FindViewById<ImageButton>(Resource.Id.next_button);
		nextButton.Click += NextButton_Click;
		reverseButton = view.FindViewById<ImageButton>(Resource.Id.reverse_button);
		reverseButton.Click += ReverseButton_Click;
		view.FindViewById<Button>(Resource.Id.RetButton).Click += delegate
		{
			dialog.Dismiss();
		};
		shogiBoard.Notation = notation;
		shogiBoard.MoveStyle = Settings.AppSettings.MoveStyle;
		shogiBoard.AnimaSpeed = Settings.AppSettings.AnimationSpeed;
		shogiBoard.Reverse = boolean;
		shogiBoard.NextMoveDisp = Settings.AppSettings.ShowNextArrow;
		return dialog;
	}

	public override void OnPause()
	{
		base.OnPause();
		if (shogiBoard != null)
		{
			shogiBoard.AnimationStop();
		}
	}

	public override void OnActivityCreated(Bundle savedInstanceState)
	{
		base.OnActivityCreated(savedInstanceState);
		UpdateSize();
	}

	public override void OnConfigurationChanged(Configuration newConfig)
	{
		base.OnConfigurationChanged(newConfig);
		UpdateSize();
	}

	private void UpdateSize()
	{
		Dialog dialog = Dialog;
		WindowManagerLayoutParams attributes = dialog.Window.Attributes;
		if (base.Resources.Configuration.Orientation == Orientation.Landscape)
		{
			attributes.Gravity = GravityFlags.Right;
			attributes.Width = base.Resources.DisplayMetrics.WidthPixels * 5 / 10;
			attributes.Height = -1;
		}
		else
		{
			attributes.Gravity = GravityFlags.Center;
			attributes.Width = -1;
			attributes.Height = base.Resources.DisplayMetrics.HeightPixels * 8 / 10;
		}
		dialog.Window.Attributes = attributes;
	}

	private void ShogiBoard_MakeMoveEvent(object sender, MakeMoveEventArgs e)
	{
		if (Settings.AppSettings.PlaySE)
		{
			PlaySE.Play(SeNo.KOMA);
		}
		notation.AddMove(new MoveDataEx(e.MoveData));
		UpdateNotation(NotationEventId.MAKE_MOVE);
	}

	private void ReverseButton_Click(object sender, EventArgs e)
	{
		shogiBoard.Reverse = !shogiBoard.Reverse;
	}

	private void PrevButton_Click(object sender, EventArgs e)
	{
		notation.Prev(1);
		UpdateNotation(NotationEventId.PREV);
	}

	private void NextButton_Click(object sender, EventArgs e)
	{
		notation.Next(1);
		UpdateNotation(NotationEventId.NEXT);
	}

	private void UpdateNotation(NotationEventId id)
	{
		shogiBoard.UpdateNotation(id);
	}
}
