using System;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using ShogiGUI;
using ShogiLib;

namespace ShogiDroid;

public class GameStartDialog : DialogFragment
{
	public EventHandler<EventArgs> OKClick;

	public EventHandler<EventArgs> CancelClick;

	private RadioGroup gameStartDialogBlackRadio;

	private RadioGroup gameStartDialogWhiteRadio;

	private Spinner gameStartDialogHandicapSpinner;

	private RadioGroup gameStartDialogPositionRadio;

	private Spinner gameStartDialogTimeSpinner;

	private Spinner gameStartDialogCountdownSpinner;

	private Spinner gameStartDialogIncrementSpinner;

	private bool suppressSpinnerEvent;

	public static GameStartDialog NewInstance()
	{
		return new GameStartDialog();
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Activity);
		AlertDialog dialog = builder.Create();
		View view = base.Activity.LayoutInflater.Inflate(Resource.Layout.gamestartdialog, null);
		dialog.SetView(view);
		gameStartDialogBlackRadio = view.FindViewById<RadioGroup>(Resource.Id.GameStartDialogBlackRadio);
		gameStartDialogWhiteRadio = view.FindViewById<RadioGroup>(Resource.Id.GameStartDialogWhiteRadio);
		gameStartDialogHandicapSpinner = view.FindViewById<Spinner>(Resource.Id.GameStartDialogHandicapSpinner);
		gameStartDialogTimeSpinner = view.FindViewById<Spinner>(Resource.Id.GameStartDialogTime);
		gameStartDialogCountdownSpinner = view.FindViewById<Spinner>(Resource.Id.GameStartDialogCountdown);
		gameStartDialogIncrementSpinner = view.FindViewById<Spinner>(Resource.Id.GameStartDialogIncrement);
		gameStartDialogPositionRadio = view.FindViewById<RadioGroup>(Resource.Id.GameStartDialogPositionRadio);

		gameStartDialogCountdownSpinner.ItemSelected += CountdownSpinner_ItemSelected;
		gameStartDialogIncrementSpinner.ItemSelected += IncrementSpinner_ItemSelected;

		((Button)view.FindViewById(Resource.Id.DialogOKButton)).Click += delegate(object sender, EventArgs e)
		{
			saveSettings();
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
		loadSettings();
		return dialog;
	}

	private void CountdownSpinner_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
	{
		if (suppressSpinnerEvent)
			return;
		if (e.Position > 0)
		{
			suppressSpinnerEvent = true;
			gameStartDialogIncrementSpinner.SetSelection(0);
			suppressSpinnerEvent = false;
		}
	}

	private void IncrementSpinner_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
	{
		if (suppressSpinnerEvent)
			return;
		if (e.Position > 0)
		{
			suppressSpinnerEvent = true;
			gameStartDialogCountdownSpinner.SetSelection(0);
			suppressSpinnerEvent = false;
		}
	}

	private void loadSettings()
	{
		if (Settings.AppSettings.BlackNo == 0)
		{
			gameStartDialogBlackRadio.Check(Resource.Id.GameStartDialogBlackRadioPlayer);
		}
		else
		{
			gameStartDialogBlackRadio.Check(Resource.Id.GameStartDialogBlackRadioComputer);
		}
		if (Settings.AppSettings.WhiteNo == 0)
		{
			gameStartDialogWhiteRadio.Check(Resource.Id.GameStartDialogWhiteRadioPlayer);
		}
		else
		{
			gameStartDialogWhiteRadio.Check(Resource.Id.GameStartDialogWhiteRadioComputer);
		}
		gameStartDialogHandicapSpinner.SetSelection((int)Settings.AppSettings.Handicap);
		if (Settings.AppSettings.StartPosition == GameStartPosition.InitialPosition)
		{
			gameStartDialogPositionRadio.Check(Resource.Id.GameStartDialogNewGameInitialPositionRadio);
		}
		else
		{
			gameStartDialogPositionRadio.Check(Resource.Id.GameStartDialogNewGameThisPositionRadio);
		}
		suppressSpinnerEvent = true;
		gameStartDialogTimeSpinner.SetSelection(GetIndex(Resource.Array.SettingsTime_Values, Settings.EngineSettings.Time));
		gameStartDialogCountdownSpinner.SetSelection(GetIndex(Resource.Array.SettingsCountdown_Values, Settings.EngineSettings.Countdown));
		gameStartDialogIncrementSpinner.SetSelection(GetIndex(Resource.Array.SettingsIncrement_Values, Settings.EngineSettings.Increment));
		suppressSpinnerEvent = false;
	}

	private int GetIndex(int id, int value)
	{
		int num = Array.FindIndex(base.Resources.GetStringArray(id), (string x) => x == value.ToString());
		if (num < 0)
		{
			return 0;
		}
		return num;
	}

	private int GetValue(int id, int index)
	{
		return int.Parse(base.Resources.GetStringArray(id)[index]);
	}

	private void saveSettings()
	{
		if (gameStartDialogBlackRadio.CheckedRadioButtonId == Resource.Id.GameStartDialogBlackRadioPlayer)
		{
			Settings.AppSettings.BlackNo = 0;
		}
		else
		{
			Settings.AppSettings.BlackNo = 1;
		}
		if (gameStartDialogWhiteRadio.CheckedRadioButtonId == Resource.Id.GameStartDialogWhiteRadioPlayer)
		{
			Settings.AppSettings.WhiteNo = 0;
		}
		else
		{
			Settings.AppSettings.WhiteNo = 1;
		}
		Settings.AppSettings.Handicap = (Handicap)gameStartDialogHandicapSpinner.SelectedItemPosition;
		Settings.EngineSettings.Time = GetValue(Resource.Array.SettingsTime_Values, gameStartDialogTimeSpinner.SelectedItemPosition);
		Settings.EngineSettings.Countdown = GetValue(Resource.Array.SettingsCountdown_Values, gameStartDialogCountdownSpinner.SelectedItemPosition);
		Settings.EngineSettings.Increment = GetValue(Resource.Array.SettingsIncrement_Values, gameStartDialogIncrementSpinner.SelectedItemPosition);
		switch (gameStartDialogPositionRadio.CheckedRadioButtonId)
		{
		case Resource.Id.GameStartDialogNewGameInitialPositionRadio:
			Settings.AppSettings.StartPosition = GameStartPosition.InitialPosition;
			Settings.AppSettings.StartMode = GameStartMode.NewGame;
			break;
		case Resource.Id.GameStartDialogNewGameThisPositionRadio:
			Settings.AppSettings.StartPosition = GameStartPosition.NowPosition;
			Settings.AppSettings.StartMode = GameStartMode.NewGame;
			break;
		}
	}
}
