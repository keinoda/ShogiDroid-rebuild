using System;
using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using Android.Widget;
using ShogiDroid.Controls.ShogiBoard;
using ShogiGUI;
using ShogiGUI.Presenters;

namespace ShogiDroid;

[Activity(Label = "EditBoard", ConfigurationChanges = (ConfigChanges.Orientation | ConfigChanges.ScreenSize), Theme = "@style/Theme.AppCompat.Light")]
public class EditBoardActivity : Activity, IEditBoardView
{
	private EditBoardPresenter presenter;

	private ShogiBoard shogiBoard;

	private Button evenButton;

	private Button mateButton;

	private Button mirrorButton;

	private Button turnButton;

	private Button okbutton;

	private Button cancelButton;

	private ImageButton reverseButton;

	private SystemUiFlags uiFlags = SystemUiFlags.Fullscreen | SystemUiFlags.HideNavigation | SystemUiFlags.ImmersiveSticky | SystemUiFlags.LayoutHideNavigation;

	protected override void OnCreate(Bundle bundle)
	{
		base.OnCreate(bundle);
		RequestWindowFeature(WindowFeatures.NoTitle);
		presenter = new EditBoardPresenter(this);
		presenter.Initialize();
		InitUI();
	}

	private void InitUI()
	{
		UpdateWindowSettings();
		SetContentView(Resource.Layout.editboard);
		shogiBoard = FindViewById<ShogiBoard>(Resource.Id.shogiboard);
		evenButton = FindViewById<Button>(Resource.Id.EditBoardEvenButton);
		mateButton = FindViewById<Button>(Resource.Id.EditBoardMateButton);
		mirrorButton = FindViewById<Button>(Resource.Id.EditBoardMirrorButton);
		turnButton = FindViewById<Button>(Resource.Id.EditBoardChangeTurnButton);
		okbutton = FindViewById<Button>(Resource.Id.OKButton);
		cancelButton = FindViewById<Button>(Resource.Id.CancelButton);
		reverseButton = FindViewById<ImageButton>(Resource.Id.reverse_button);
		evenButton.Click += EvenButton_Click;
		mateButton.Click += MateButton_Click;
		mirrorButton.Click += MirrorButton_Click;
		turnButton.Click += TurnButton_Click;
		okbutton.Click += OkButton_Click;
		cancelButton.Click += CancelButton_Click;
		shogiBoard.Notation = presenter.Notation;
		shogiBoard.MoveStyle = Settings.AppSettings.MoveStyle;
		reverseButton.Click += ReverseButton_Click;
	}

	private void ReverseButton_Click(object sender, EventArgs e)
	{
		shogiBoard.Reverse = !shogiBoard.Reverse;
	}

	private void CancelButton_Click(object sender, EventArgs e)
	{
		SetResult(Result.Canceled);
		Finish();
	}

	private void OkButton_Click(object sender, EventArgs e)
	{
		presenter.BoardEditEnd();
		SetResult(Result.Ok);
		Finish();
	}

	private void TurnButton_Click(object sender, EventArgs e)
	{
		presenter.ChangeTurn();
		shogiBoard.Notation = presenter.Notation;
	}

	private void MirrorButton_Click(object sender, EventArgs e)
	{
		presenter.Mirror();
		shogiBoard.Notation = presenter.Notation;
	}

	private void MateButton_Click(object sender, EventArgs e)
	{
		presenter.InitPositionMate();
		shogiBoard.Notation = presenter.Notation;
	}

	private void EvenButton_Click(object sender, EventArgs e)
	{
		presenter.InitPositionEven();
		shogiBoard.Notation = presenter.Notation;
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
