using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using ShogiGUI;

namespace ShogiDroid;

[Activity(Label = "設定", Theme = "@style/AppTheme")]
public class SettingsHomeActivity : Activity
{
	private static readonly (string Section, string Label)[] Sections = {
		(SettingActivity.SectionGame, "対局設定"),
		(SettingActivity.SectionAnalyze, "解析設定"),
		(SettingActivity.SectionDisplay, "表示設定"),
		(SettingActivity.SectionControls, "操作・カスタマイズ"),
		(SettingActivity.SectionUser, "データ・ユーザー"),
	};

	protected override void OnCreate(Bundle savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		UpdateWindowSettings();

		var scroll = new ScrollView(this);
		var layout = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical };
		layout.SetPadding(0, 0, 0, 0);

		foreach (var (section, label) in Sections)
		{
			var item = new TextView(this) { Text = label };
			item.SetPadding(Dp(24), Dp(16), Dp(24), Dp(16));
			item.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
			// テーマの標準テキスト色
			var tv = new Android.Util.TypedValue();
			Theme.ResolveAttribute(Android.Resource.Attribute.TextColorPrimary, tv, true);
			item.SetTextColor(Resources.GetColorStateList(tv.ResourceId, Theme));
			item.Clickable = true;
			// rippleエフェクト
			var outValue = new Android.Util.TypedValue();
			Theme.ResolveAttribute(Android.Resource.Attribute.SelectableItemBackground, outValue, true);
			item.SetBackgroundResource(outValue.ResourceId);

			string sec = section; // closure capture
			item.Click += (s, e) =>
			{
				var intent = new Intent(this, typeof(SettingActivity));
				intent.PutExtra(SettingActivity.ExtraSection, sec);
				StartActivity(intent);
			};
			layout.AddView(item);

			// 区切り線
			var div = new View(this);
			div.SetBackgroundColor(Android.Graphics.Color.Argb(30, 128, 128, 128));
			div.LayoutParameters = new LinearLayout.LayoutParams(
				LinearLayout.LayoutParams.MatchParent, 1);
			layout.AddView(div);
		}

		scroll.AddView(layout);
		SetContentView(scroll);
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
