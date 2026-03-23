using System;
using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using Android.Widget;
using ShogiDroid.Controls;
using ShogiGUI;
using ShogiGUI.Engine;
using ShogiGUI.Presenters;

namespace ShogiDroid;

[Activity(Label = "@string/EngineOptionsTitle_Text", ConfigurationChanges = (ConfigChanges.Orientation | ConfigChanges.ScreenSize), Theme = "@style/AppTheme")]
public class EngineOptionsActivity : ThemedActivity, IEngineOptions
{
	private EngineOptionsPresenter presenter;

	private TextView engineName;

	private TextView engineId;

	private TextView engineAuthor;

	private ListView optionList;

	private Button okbutton;

	private Button cancelButton;

	private EngineOptionAdapter optionAdapter;

	private SystemUiFlags uiFlags = SystemUiFlags.Fullscreen;

	protected override void OnCreate(Bundle bundle)
	{
		base.OnCreate(bundle);
		RequestWindowFeature(WindowFeatures.NoTitle);
		presenter = new EngineOptionsPresenter(this);
		presenter.Initialize();
		InitUI();
	}

	private void InitUI()
	{
		UpdateWindowSettings();
		SetContentView(Resource.Layout.engineoptions);
		okbutton = FindViewById<Button>(Resource.Id.OKButton);
		cancelButton = FindViewById<Button>(Resource.Id.CancelButton);
		okbutton.Click += OkButton_Click;
		cancelButton.Click += CancelButton_Click;
		engineName = FindViewById<TextView>(Resource.Id.engine_name);
		engineId = FindViewById<TextView>(Resource.Id.engine_id);
		engineAuthor = FindViewById<TextView>(Resource.Id.engine_author);
		optionList = FindViewById<ListView>(Resource.Id.engine_option_list);
		if (Settings.EngineSettings.EngineNo == 1)
		{
			engineName.Text = InternalEnginePlayer.EngineBaseName;
		}
		else
		{
			engineName.Text = Settings.EngineSettings.EngineName;
		}
		if (presenter.EnginePlayer != null && presenter.EnginePlayer.IsInitialized)
		{
			UpdateControls();
			return;
		}
		okbutton.Enabled = false;
		optionList.Enabled = false;
	}

	protected override void OnResume()
	{
		base.OnResume();
		presenter.Resume();
	}

	protected override void OnPause()
	{
		base.OnPause();
		presenter.Pause();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		presenter.Destory();
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

	private void CancelButton_Click(object sender, EventArgs e)
	{
		SetResult(Result.Canceled);
		Finish();
	}

	private void OkButton_Click(object sender, EventArgs e)
	{
		StoreSettings();
		SetResult(Result.Ok);
		Finish();
	}

	private void UpdateControls()
	{
		okbutton.Enabled = true;
		optionList.Enabled = true;
		engineId.Text = presenter.EnginePlayer.Name;
		engineAuthor.Text = presenter.EnginePlayer.Author;
		optionAdapter = new EngineOptionAdapter(this, presenter.EnginePlayer.Options);
		optionAdapter.ButtonClick += OptionAdapter_ButtonClick;
		optionList.Adapter = optionAdapter;
	}

	private void StoreSettings()
	{
		foreach (USIOption option in optionAdapter.OptionList)
		{
			if (option.HasChanged())
			{
				presenter.EnginePlayer.SetOption(option.Name, option.ValueToString());
			}
		}
		presenter.EnginePlayer.SaveSettings();
	}

	private void OptionAdapter_ButtonClick(object sender, OptionButtonEventArgs e)
	{
		presenter.SendButton(e.Name);
	}

	public void InitializeEnd()
	{
		UpdateControls();
	}

	public void InitializeError()
	{
		RunOnUiThread(() =>
		{
			Android.Widget.Toast.MakeText(this, "エンジンの初期化に失敗しました", Android.Widget.ToastLength.Long).Show();
			SetResult(Result.Canceled);
			Finish();
		});
	}

	private void UpdateWindowSettings()
	{
		if (Settings.AppSettings.DispToolbar)
		{
			uiFlags = SystemUiFlags.ImmersiveSticky;
		}
		else
		{
			uiFlags = SystemUiFlags.Fullscreen;
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
