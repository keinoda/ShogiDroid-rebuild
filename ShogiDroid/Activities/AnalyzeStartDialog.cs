using System;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using ShogiGUI;

namespace ShogiDroid;

public class AnalyzeStartDialog : DialogFragment
{
	public EventHandler<EventArgs> OKClick;

	public EventHandler<EventArgs> CancelClick;

	private Spinner timeSpinner;

	private CheckBox depthCheckBox;

	private EditText depthEditText;

	private RadioGroup rangeRadio;

	private CheckBox reverseCheckBox;

	private static readonly int[] TimeArray = new int[8] { 1000, 2000, 3000, 5000, 10000, 15000, 20000, 30000 };

	public static AnalyzeStartDialog NewInstance()
	{
		return new AnalyzeStartDialog();
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Activity);
		AlertDialog dialog = builder.Create();
		View view = base.Activity.LayoutInflater.Inflate(Resource.Layout.analyzestartdialog, null);
		dialog.SetView(view);
		timeSpinner = view.FindViewById<Spinner>(Resource.Id.AnalyzeStartDialogTimeSpinner);
		depthCheckBox = view.FindViewById<CheckBox>(Resource.Id.AnalyzeStartDialogDepthCheckBox);
		depthEditText = view.FindViewById<EditText>(Resource.Id.AnalyzeStartDialogDepthEditText);
		rangeRadio = view.FindViewById<RadioGroup>(Resource.Id.AnalyzeStartDialogRange);
		reverseCheckBox = view.FindViewById<CheckBox>(Resource.Id.AnalyzeStartDialogReverse);
		// 逆順時はラジオボタンのラベルを変更
		reverseCheckBox.Click += delegate
		{
			var nowRadio = view.FindViewById<RadioButton>(Resource.Id.AnalyzeStartDialogRangeNow);
			nowRadio.Text = reverseCheckBox.Checked
				? "現在の局面まで解析"
				: Activity.GetString(Resource.String.AnalyzeRangeNow_Text);
		};
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
		depthEditText.Enabled = Settings.AnalyzeSettings.AnalyzeDepthEnable;
		depthCheckBox.Click += delegate
		{
			depthEditText.Enabled = depthCheckBox.Checked;
		};
		return dialog;
	}

	private void loadSettings()
	{
		int num = Array.FindIndex(TimeArray, (int val) => val == Settings.AnalyzeSettings.AnalyzeTime);
		if (num < 0)
		{
			num = 0;
		}
		timeSpinner.SetSelection(num);
		depthCheckBox.Checked = Settings.AnalyzeSettings.AnalyzeDepthEnable;
		depthEditText.Text = Settings.AnalyzeSettings.AnalyzeDepth.ToString();
		if (Settings.AnalyzeSettings.AnalyzePositon == GameStartPosition.InitialPosition)
		{
			rangeRadio.Check(Resource.Id.AnalyzeStartDialogRangeAll);
		}
		else
		{
			rangeRadio.Check(Resource.Id.AnalyzeStartDialogRangeNow);
		}
		reverseCheckBox.Checked = Settings.AnalyzeSettings.Reverse;
		// 初期表示時もラベルを反映
		if (Settings.AnalyzeSettings.Reverse)
		{
			var nowRadio = Activity.FindViewById<RadioButton>(Resource.Id.AnalyzeStartDialogRangeNow);
			if (nowRadio != null) nowRadio.Text = "現在の局面まで解析";
		}
	}

	private void saveSettings()
	{
		Settings.AnalyzeSettings.AnalyzeTime = TimeArray[timeSpinner.SelectedItemPosition];
		Settings.AnalyzeSettings.AnalyzeDepthEnable = depthCheckBox.Checked;
		if (int.TryParse(depthEditText.Text, out var result))
		{
			if (result < 0)
			{
				result = 0;
			}
			Settings.AnalyzeSettings.AnalyzeDepth = result;
		}
		if (rangeRadio.CheckedRadioButtonId == Resource.Id.AnalyzeStartDialogRangeAll)
		{
			Settings.AnalyzeSettings.AnalyzePositon = GameStartPosition.InitialPosition;
		}
		else
		{
			Settings.AnalyzeSettings.AnalyzePositon = GameStartPosition.NowPosition;
		}
		Settings.AnalyzeSettings.Reverse = reverseCheckBox.Checked;
	}
}
