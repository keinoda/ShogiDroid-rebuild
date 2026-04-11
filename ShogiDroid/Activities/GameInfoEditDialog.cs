using System;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace ShogiDroid;

/// <summary>
/// 棋譜情報（対局者名・棋戦・場所等）を編集するダイアログ
/// </summary>
public class GameInfoEditDialog : DialogFragment
{
	public EventHandler<EventArgs> OKClick;
	public EventHandler<EventArgs> CancelClick;

	public string BlackName { get; set; } = string.Empty;
	public string WhiteName { get; set; } = string.Empty;
	public string Event { get; set; } = string.Empty;
	public string Site { get; set; } = string.Empty;
	public string StartTime { get; set; } = string.Empty;
	public string EndTime { get; set; } = string.Empty;
	public string TimeLimit { get; set; } = string.Empty;
	public string Opening { get; set; } = string.Empty;

	public static GameInfoEditDialog NewInstance(
		string blackName,
		string whiteName,
		string ev,
		string site,
		string startTime,
		string endTime,
		string timeLimit,
		string opening)
	{
		return new GameInfoEditDialog
		{
			BlackName = blackName ?? string.Empty,
			WhiteName = whiteName ?? string.Empty,
			Event = ev ?? string.Empty,
			Site = site ?? string.Empty,
			StartTime = startTime ?? string.Empty,
			EndTime = endTime ?? string.Empty,
			TimeLimit = timeLimit ?? string.Empty,
			Opening = opening ?? string.Empty
		};
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		var builder = new AlertDialog.Builder(Activity);
		var dialog = builder.Create();
		var view = Activity.LayoutInflater.Inflate(Resource.Layout.gameinfoeditdialog, null);
		dialog.SetView(view);

		var blackEdit = view.FindViewById<EditText>(Resource.Id.game_info_black);
		var whiteEdit = view.FindViewById<EditText>(Resource.Id.game_info_white);
		var eventEdit = view.FindViewById<EditText>(Resource.Id.game_info_event);
		var siteEdit = view.FindViewById<EditText>(Resource.Id.game_info_site);
		var startTimeEdit = view.FindViewById<EditText>(Resource.Id.game_info_start_time);
		var endTimeEdit = view.FindViewById<EditText>(Resource.Id.game_info_end_time);
		var timeLimitEdit = view.FindViewById<EditText>(Resource.Id.game_info_time_limit);
		var openingEdit = view.FindViewById<EditText>(Resource.Id.game_info_opening);

		blackEdit.Text = BlackName;
		whiteEdit.Text = WhiteName;
		eventEdit.Text = Event;
		siteEdit.Text = Site;
		startTimeEdit.Text = StartTime;
		endTimeEdit.Text = EndTime;
		timeLimitEdit.Text = TimeLimit;
		openingEdit.Text = Opening;

		((Button)view.FindViewById(Resource.Id.DialogOKButton)).Click += (sender, e) =>
		{
			BlackName = blackEdit.Text ?? string.Empty;
			WhiteName = whiteEdit.Text ?? string.Empty;
			Event = eventEdit.Text ?? string.Empty;
			Site = siteEdit.Text ?? string.Empty;
			StartTime = startTimeEdit.Text ?? string.Empty;
			EndTime = endTimeEdit.Text ?? string.Empty;
			TimeLimit = timeLimitEdit.Text ?? string.Empty;
			Opening = openingEdit.Text ?? string.Empty;
			OKClick?.Invoke(sender, e);
			dialog.Dismiss();
		};
		((Button)view.FindViewById(Resource.Id.DialogCancelButton)).Click += (sender, e) =>
		{
			CancelClick?.Invoke(sender, e);
			dialog.Dismiss();
		};
		return dialog;
	}
}
