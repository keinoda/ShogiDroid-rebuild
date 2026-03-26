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
		(SettingActivity.SectionGame, "対局設定", "持ち時間、手番、開始局面などを調整します。"),
		(SettingActivity.SectionAnalyze, "解析設定", "解析時間、深さ、評価表示の挙動をまとめています。"),
		(SettingActivity.SectionDisplay, "表示設定", "テーマ、盤面表示、グラフ関連の見え方を切り替えます。"),
		(SettingActivity.SectionControls, "操作・カスタマイズ", "ショートカットや操作ボタンの好みを反映します。"),
		(SettingActivity.SectionUser, "データ・ユーザー", "ユーザー名や保存データ周りを管理します。"),
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
			Text = "対局、解析、表示まわりをセクション別に整理しました。"
		};
		heroBody.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		heroBody.SetTextColor(ColorUtils.Get(this, Resource.Color.secondary_text));
		heroBody.SetPadding(0, Dp(6), 0, 0);
		hero.AddView(heroBody);
		layout.AddView(hero);

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
