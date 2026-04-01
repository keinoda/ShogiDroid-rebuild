using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using ShogiGUI;

namespace ShogiDroid;

[Activity(Label = "設定", Theme = "@style/AppTheme")]
public class SettingsHomeActivity : ThemedActivity
{
	private static readonly (string Section, string Label, string Description)[] Sections = {
		(SettingActivity.SectionAnalyze, "解析設定", "局面や棋譜を見ながら使う解析まわりの設定です。"),
		(SettingActivity.SectionDisplay, "表示設定", "矢印、PV、表示スタイルなど見え方を調整します。"),
		(SettingActivity.SectionControls, "操作・カスタマイズ", "駒音やショートカットなど操作まわりを整えます。"),
		(SettingActivity.SectionUser, "データ・ユーザー", "ユーザー名、保存、バックアップを管理します。"),
		(SettingActivity.SectionGame, "対局設定", "持ち時間など対局前に使う設定をまとめています。"),
	};

	protected override void OnCreate(Bundle savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		UpdateWindowSettings();

		var root = new FrameLayout(this);
		root.SetBackgroundResource(Resource.Drawable.window_background);

		var scroll = new ScrollView(this);
		scroll.FillViewport = true;
		var layout = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical };
		layout.SetPadding(Dp(20), Dp(20), Dp(20), Dp(24));

		var hero = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical };
		hero.SetBackgroundResource(Resource.Drawable.surface_panel_bg);
		hero.SetPadding(Dp(24), Dp(24), Dp(24), Dp(24));
		hero.LayoutParameters = new LinearLayout.LayoutParams(
			ViewGroup.LayoutParams.MatchParent,
			ViewGroup.LayoutParams.WrapContent)
		{
			BottomMargin = Dp(16)
		};

		var heroTitle = new TextView(this) { Text = "設定" };
		heroTitle.SetTextSize(Android.Util.ComplexUnitType.Sp, 28);
		heroTitle.SetTypeface(null, TypefaceStyle.Bold);
		heroTitle.SetTextColor(ColorUtils.Get(this, Resource.Color.primary_text));
		hero.AddView(heroTitle);

		var heroBody = new TextView(this)
		{
			Text = GetString(Resource.String.SettingsHomeLead_Text)
		};
		heroBody.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		heroBody.SetTextColor(ColorUtils.Get(this, Resource.Color.secondary_text));
		heroBody.SetPadding(0, Dp(6), 0, 0);
		hero.AddView(heroBody);
		layout.AddView(hero);

		// 内蔵エンジン非表示トグル
		layout.AddView(CreateHideInternalEngineToggle());

		foreach (var (section, label, description) in Sections)
		{
			var item = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical };
			item.SetBackgroundResource(Resource.Drawable.surface_clickable_bg);
			item.SetPadding(Dp(20), Dp(18), Dp(20), Dp(18));
			item.Clickable = true;
			item.Focusable = true;
			item.LayoutParameters = new LinearLayout.LayoutParams(
				ViewGroup.LayoutParams.MatchParent,
				ViewGroup.LayoutParams.WrapContent)
			{
				BottomMargin = Dp(12)
			};

			var title = new TextView(this) { Text = label };
			title.SetTextSize(Android.Util.ComplexUnitType.Sp, 17);
			title.SetTypeface(null, TypefaceStyle.Bold);
			title.SetTextColor(ColorUtils.Get(this, Resource.Color.primary_text));
			item.AddView(title);

			var body = new TextView(this) { Text = description };
			body.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
			body.SetTextColor(ColorUtils.Get(this, Resource.Color.secondary_text));
			body.SetPadding(0, Dp(6), 0, 0);
			item.AddView(body);

			string sec = section; // closure capture
			item.Click += (s, e) =>
			{
				var intent = new Intent(this, typeof(SettingActivity));
				intent.PutExtra(SettingActivity.ExtraSection, sec);
				StartActivity(intent);
			};
			layout.AddView(item);
		}

		scroll.AddView(layout);
		root.AddView(scroll);
		SetContentView(root);
		FontUtil.ApplyFont(root);
	}

	private View CreateHideInternalEngineToggle()
	{
		var row = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Horizontal };
		row.SetBackgroundResource(Resource.Drawable.surface_clickable_bg);
		row.SetPadding(Dp(20), Dp(14), Dp(20), Dp(14));
		row.SetGravity(GravityFlags.CenterVertical);
		row.LayoutParameters = new LinearLayout.LayoutParams(
			ViewGroup.LayoutParams.MatchParent,
			ViewGroup.LayoutParams.WrapContent)
		{
			BottomMargin = Dp(12)
		};

		var label = new TextView(this) { Text = "内蔵エンジンを非表示" };
		label.SetTextSize(Android.Util.ComplexUnitType.Sp, 15);
		label.SetTextColor(ColorUtils.Get(this, Resource.Color.primary_text));
		label.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
		row.AddView(label);

		var toggle = new Android.Widget.Switch(this);
		toggle.Checked = Settings.AppSettings.HideInternalEngine;
		toggle.CheckedChange += (s, e) =>
		{
			Settings.AppSettings.HideInternalEngine = e.IsChecked;
			Settings.Save();
		};
		row.AddView(toggle);

		return row;
	}

	private int Dp(int dp) => (int)(dp * Resources.DisplayMetrics.Density + 0.5f);

	private void UpdateWindowSettings()
	{
		if (Settings.AppSettings.DispToolbar)
		{
			Window.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.Visible;
			Window.ClearFlags(WindowManagerFlags.Fullscreen);
		}
		else
		{
			Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
				SystemUiFlags.ImmersiveSticky |
				SystemUiFlags.HideNavigation |
				SystemUiFlags.Fullscreen);
			Window.Attributes.Flags |= WindowManagerFlags.Fullscreen;
		}
		AndroidX.Core.View.WindowCompat.SetDecorFitsSystemWindows(Window, Settings.AppSettings.DispToolbar);
	}
}
