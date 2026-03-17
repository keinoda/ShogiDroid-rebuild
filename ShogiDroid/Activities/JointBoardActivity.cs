using System;
using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using Android.Widget;
using ShogiDroid.Controls.ShogiBoard;
using ShogiGUI;
using ShogiGUI.Events;
using ShogiGUI.Presenters;

namespace ShogiDroid;

[Activity(Label = "JointBoardActivity", ConfigurationChanges = (ConfigChanges.Orientation | ConfigChanges.ScreenSize), Theme = "@style/Theme.AppCompat.Light")]
public class JointBoardActivity : Activity, IJointBoardView
{
	private JointBoardPresenter presenter;

	private ShogiBoard shogiBoard;

	private ImageButton prevButton;

	private ImageButton nextButton;

	private ImageButton reverseButton;

	private Button retButton;

	private SystemUiFlags uiFlags = SystemUiFlags.Fullscreen | SystemUiFlags.HideNavigation | SystemUiFlags.ImmersiveSticky | SystemUiFlags.LayoutHideNavigation;

	private bool reverse;

	protected override void OnCreate(Bundle savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		reverse = Intent.GetBooleanExtra("reverse", defaultValue: false);
		int intExtra = Intent.GetIntExtra("pvnum", 1);
		int intExtra2 = Intent.GetIntExtra("dispMode", 0);
		RequestWindowFeature(WindowFeatures.NoTitle);
		presenter = new JointBoardPresenter(this);
		presenter.Initialize();
		presenter.LoadPv(intExtra, (PVDispMode)intExtra2);
		InitUI();
	}

	private void InitUI()
	{
		UpdateWindowSettings();
		SetContentView(Resource.Layout.jointboard);
		shogiBoard = FindViewById<ShogiBoard>(Resource.Id.shogiboard);
		prevButton = FindViewById<ImageButton>(Resource.Id.prev_button);
		prevButton.Click += PrevButton_Click;
		nextButton = FindViewById<ImageButton>(Resource.Id.next_button);
		nextButton.Click += NextButton_Click;
		reverseButton = FindViewById<ImageButton>(Resource.Id.reverse_button);
		reverseButton.Click += ReverseButton_Click;
		retButton = FindViewById<Button>(Resource.Id.RetButton);
		retButton.Click += RetButton_Click;
		shogiBoard.Notation = presenter.Notation;
		shogiBoard.MoveStyle = Settings.AppSettings.MoveStyle;
		shogiBoard.AnimaSpeed = Settings.AppSettings.AnimationSpeed;
		shogiBoard.Reverse = reverse;
	}

	protected override void OnPause()
	{
		base.OnPause();
		shogiBoard.AnimationStop();
	}

	private void RetButton_Click(object sender, EventArgs e)
	{
		SetResult(Result.Ok);
		Finish();
	}

	private void ReverseButton_Click(object sender, EventArgs e)
	{
		reverse = !reverse;
		shogiBoard.Reverse = reverse;
	}

	private void PrevButton_Click(object sender, EventArgs e)
	{
		presenter.Prev();
	}

	private void NextButton_Click(object sender, EventArgs e)
	{
		presenter.Next();
	}

	public override void OnConfigurationChanged(Configuration newConfig)
	{
		base.OnConfigurationChanged(newConfig);
		InitUI();
	}

	public override void OnWindowFocusChanged(bool hasFocus)
	{
		base.OnWindowFocusChanged(hasFocus);
		if (hasFocus)
		{
			UpdateWindowSettings();
		}
	}

	public void UpdateNotation(NotationEventId id)
	{
		shogiBoard.UpdateNotation(id);
	}

	private void UpdateWindowSettings()
	{
		if (Settings.AppSettings.DispToolbar)
		{
			uiFlags = SystemUiFlags.ImmersiveSticky;
		}
		else
		{
			uiFlags = SystemUiFlags.Fullscreen | SystemUiFlags.HideNavigation | SystemUiFlags.ImmersiveSticky | SystemUiFlags.LayoutHideNavigation;
		}
		if (Settings.AppSettings.DispToolbar)
		{
			Window.ClearFlags(WindowManagerFlags.Fullscreen);
		}
		else
		{
			Window.Attributes.Flags |= WindowManagerFlags.Fullscreen;
		}
		Window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiFlags;
	}
}
