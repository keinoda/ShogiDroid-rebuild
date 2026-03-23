using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;
using ShogiGUI;
using ShogiGUI.Engine;
using ShogiGUI.Presenters;
using Math = System.Math;

namespace ShogiDroid;

[Activity(Label = "@string/EngineSettingsTitle_Text", ConfigurationChanges = (ConfigChanges.Orientation | ConfigChanges.ScreenSize), Theme = "@style/AppTheme")]
public class EngineSettingsWrapperActivity : ThemedActivity, IEngineOptions
{
	private EngineOptionsPresenter presenter;
	private LinearLayout container;
	private Button okButton;
	private Button cancelButton;
	private Button allOptionsButton;
	private TextView engineNameText;
	private List<USIOption> editedOptions = new List<USIOption>();
	private SystemUiFlags uiFlags = SystemUiFlags.Fullscreen;

	// Option definitions with alias support for version differences.
	// Aliases: first match in the engine's reported options wins.
	private static readonly OptionDef[] OptionDefinitions = new OptionDef[]
	{
		// === 基本設定 ===
		new OptionDef(new[] { "USI_Hash" }, "基本設定",
			"ハッシュサイズ(MB)",
			"探索の高速化に使うメモリ量。推奨: 端末メモリの1/4～1/2"),
		new OptionDef(new[] { "Threads" }, "基本設定",
			"探索スレッド数",
			"CPUコア数に合わせます。推奨: コア数と同じ"),
		new OptionDef(new[] { "USI_Ponder" }, "基本設定",
			"先読み(Ponder)",
			"相手の手番中も思考を続けます。推奨: 対局時ON"),
		new OptionDef(new[] { "MultiPV" }, "基本設定",
			"候補手の数",
			"推奨: 対局時は1、検討時のみ増やす(2以上で棋力低下)"),

		// === ニューラルネット設定 (deep系: ふかうら王等) ===
		new OptionDef(new[] { "DNN_Model", "DNN_Model1" }, "ニューラルネット設定",
			"NNモデルファイル",
			"推論に使うモデルファイル名(onnx等)"),
		new OptionDef(new[] { "DNN_Batch_Size", "DNN_Batch_Size1" }, "ニューラルネット設定",
			"推論バッチサイズ",
			"GPU推論の並列数。大きいほど高速だが遅延が増加する"),
		new OptionDef(new[] { "UCT_Threads", "UCT_Threads1" }, "ニューラルネット設定",
			"MCTS探索スレッド数",
			"GPU毎の探索スレッド数。推奨: 2～3"),
		new OptionDef(new[] { "UCT_NodeLimit" }, "ニューラルネット設定",
			"MCTS最大ノード数",
			"探索木の最大ノード数。1ノード≒2KBのメモリを使用。\n推奨: 50000000(50M≒100GB)。長時間解析時は増やす"),

		// === 定跡設定 ===
		new OptionDef(new[] { "USI_OwnBook" }, "定跡設定",
			"定跡を使用",
			"エンジン内蔵の定跡DBを使って序盤を指します。推奨: ON\n※評価値付き定跡使用時、NarrowBookはOFF推奨(全オプションで設定)"),

		// === 対局設定 ===
		new OptionDef(new[] { "ResignValue" }, "対局設定",
			"投了する評価値",
			"この評価値以下で投了。推奨: 対局時は低め(例:500)、棋譜解析時は99999(投了しない)"),
		new OptionDef(new[] { "Resign_Threshold" }, "対局設定",
			"投了する勝率(‰)",
			"この勝率以下で投了(deep系)。0で投了しません"),
		new OptionDef(new[] { "EnteringKingRule" }, "対局設定",
			"入玉ルール",
			"入玉宣言勝ちのルール。CSARule27が一般的"),
		new OptionDef(new[] { "ConsiderationMode" }, "対局設定",
			"検討モード",
			"推奨: 検討時ON(不完全な読み筋の出力を抑制)、対局時OFF"),
	};

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
		SetContentView(Resource.Layout.engine_settings_wrapper);
		container = FindViewById<LinearLayout>(Resource.Id.engine_settings_container);
		okButton = FindViewById<Button>(Resource.Id.OKButton);
		cancelButton = FindViewById<Button>(Resource.Id.CancelButton);
		allOptionsButton = FindViewById<Button>(Resource.Id.engine_settings_all_options);
		engineNameText = FindViewById<TextView>(Resource.Id.engine_settings_engine_name);

		okButton.Click += OkButton_Click;
		cancelButton.Click += CancelButton_Click;
		allOptionsButton.Click += AllOptionsButton_Click;

		if (Settings.EngineSettings.EngineNo == 1)
		{
			engineNameText.Text = InternalEnginePlayer.EngineBaseName;
		}
		else
		{
			engineNameText.Text = Settings.EngineSettings.EngineName;
		}

		if (presenter.EnginePlayer != null && presenter.EnginePlayer.IsInitialized)
		{
			BuildOptionViews();
		}
		else
		{
			okButton.Enabled = false;
		}
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

	private void AllOptionsButton_Click(object sender, EventArgs e)
	{
		StartActivityForResult(new Intent(this, typeof(EngineOptionsActivity)), 200);
	}

	protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
	{
		base.OnActivityResult(requestCode, resultCode, data);
		if (requestCode == 200)
		{
			SetResult(resultCode);
			Finish();
		}
	}

	private void StoreSettings()
	{
		foreach (USIOption option in editedOptions)
		{
			if (option.HasChanged())
			{
				presenter.EnginePlayer.SetOption(option.Name, option.ValueToString());
			}
		}
		presenter.EnginePlayer.SaveSettings();
	}

	public void InitializeEnd()
	{
		RunOnUiThread(() =>
		{
			okButton.Enabled = true;
			engineNameText.Text = presenter.EnginePlayer.Name;
			BuildOptionViews();
		});
	}

	public void InitializeError()
	{
		RunOnUiThread(() =>
		{
			Toast.MakeText(this, "エンジンの初期化に失敗しました", ToastLength.Long).Show();
			SetResult(Result.Canceled);
			Finish();
		});
	}

	/// <summary>
	/// Resolves the first matching alias from the engine's live options.
	/// Returns null if no alias matches.
	/// </summary>
	private string ResolveOptionName(string[] aliases, USIOptions engineOptions)
	{
		foreach (string alias in aliases)
		{
			if (engineOptions.ContainsKey(alias))
				return alias;
		}
		return null;
	}

	private void BuildOptionViews()
	{
		container.RemoveAllViews();
		editedOptions.Clear();

		USIOptions engineOptions = presenter.EnginePlayer.Options;
		string currentCategory = null;

		foreach (OptionDef def in OptionDefinitions)
		{
			string resolvedName = ResolveOptionName(def.Aliases, engineOptions);
			if (resolvedName == null)
				continue;

			USIOption opt = engineOptions[resolvedName].Clone();
			editedOptions.Add(opt);

			if (currentCategory != def.Category)
			{
				currentCategory = def.Category;
				AddCategoryHeader(def.Category);
			}

			AddOptionView(opt, def);
		}

		if (editedOptions.Count == 0)
		{
			var noOptions = new TextView(this)
			{
				Text = "このエンジンには対応するオプションがありません"
			};
			noOptions.SetPadding(16, 32, 16, 32);
			container.AddView(noOptions);
		}
	}

	private void AddCategoryHeader(string category)
	{
		var header = new TextView(this);
		header.Text = category;
		header.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
		header.SetTypeface(null, TypefaceStyle.Bold);
		header.SetTextColor(ColorUtils.Get(this, Resource.Color.title_background));
		var lp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent,
			LinearLayout.LayoutParams.WrapContent);
		lp.TopMargin = DpToPx(16);
		lp.BottomMargin = DpToPx(4);
		header.LayoutParameters = lp;
		container.AddView(header);

		var divider = new View(this);
		divider.SetBackgroundColor(ColorUtils.Get(this, Resource.Color.title_background));
		divider.LayoutParameters = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, DpToPx(2));
		container.AddView(divider);
	}

	private void AddOptionView(USIOption opt, OptionDef def)
	{
		var card = new LinearLayout(this)
		{
			Orientation = Android.Widget.Orientation.Vertical
		};
		var cardLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent,
			LinearLayout.LayoutParams.WrapContent);
		cardLp.TopMargin = DpToPx(8);
		card.LayoutParameters = cardLp;
		card.SetPadding(DpToPx(8), DpToPx(8), DpToPx(8), DpToPx(8));
		card.SetBackgroundColor(ColorUtils.Get(this, Resource.Color.card_background));

		switch (opt.Type)
		{
			case USIOptionType.CHECK:
				BuildCheckOption(card, (USIOptionCheck)opt, def);
				break;
			case USIOptionType.SPIN:
				BuildSpinOption(card, (USIOptionSpin)opt, def);
				break;
			case USIOptionType.COMBO:
				BuildComboOption(card, (USIOptionCombo)opt, def);
				break;
			case USIOptionType.STRING:
			case USIOptionType.FILENAME:
				BuildStringOption(card, (USIOptionString)opt, def);
				break;
		}

		container.AddView(card);
	}

	private void BuildCheckOption(LinearLayout card, USIOptionCheck opt, OptionDef def)
	{
		var row = new LinearLayout(this)
		{
			Orientation = Android.Widget.Orientation.Horizontal
		};
		row.SetGravity(GravityFlags.CenterVertical);
		row.LayoutParameters = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent,
			LinearLayout.LayoutParams.WrapContent);

		var labelLayout = new LinearLayout(this)
		{
			Orientation = Android.Widget.Orientation.Vertical
		};
		var labelLp = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1f);
		labelLayout.LayoutParameters = labelLp;

		var title = new TextView(this) { Text = def.DisplayName };
		title.SetTextSize(Android.Util.ComplexUnitType.Sp, 15);
		title.SetTypeface(null, TypefaceStyle.Bold);
		labelLayout.AddView(title);

		var desc = new TextView(this) { Text = def.Description };
		desc.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		desc.SetTextColor(Color.Gray);
		labelLayout.AddView(desc);

		row.AddView(labelLayout);

		var toggle = new Switch(this) { Checked = opt.Value };
		toggle.CheckedChange += (s, e) =>
		{
			opt.SetValue(e.IsChecked);
		};
		row.AddView(toggle);

		card.AddView(row);
	}

	private void BuildSpinOption(LinearLayout card, USIOptionSpin opt, OptionDef def)
	{
		var title = new TextView(this) { Text = def.DisplayName };
		title.SetTextSize(Android.Util.ComplexUnitType.Sp, 15);
		title.SetTypeface(null, TypefaceStyle.Bold);
		card.AddView(title);

		var desc = new TextView(this) { Text = def.Description };
		desc.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		desc.SetTextColor(Color.Gray);
		card.AddView(desc);

		var valueRow = new LinearLayout(this)
		{
			Orientation = Android.Widget.Orientation.Horizontal
		};
		valueRow.SetGravity(GravityFlags.CenterVertical);
		var rowLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent,
			LinearLayout.LayoutParams.WrapContent);
		rowLp.TopMargin = DpToPx(4);
		valueRow.LayoutParameters = rowLp;

		var editText = new EditText(this);
		editText.InputType = InputTypes.ClassNumber | InputTypes.NumberFlagSigned;
		editText.Text = opt.ValueToString();
		var etLp = new LinearLayout.LayoutParams(DpToPx(100), LinearLayout.LayoutParams.WrapContent);
		editText.LayoutParameters = etLp;
		editText.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		if (opt.Value != opt.DefaultValue)
		{
			editText.SetTextColor(ColorUtils.Get(this, Resource.Color.option_change_text_color));
		}

		editText.FocusChange += (s, e) =>
		{
			if (!e.HasFocus)
			{
				if (int.TryParse(editText.Text, out int result))
				{
					result = Math.Min(opt.Max, Math.Max(opt.Min, result));
					opt.SetValue(result);
					editText.Text = result.ToString();
					if (opt.Value != opt.DefaultValue)
						editText.SetTextColor(ColorUtils.Get(this, Resource.Color.option_change_text_color));
					else
						editText.SetTextColor(ColorUtils.Get(this, Resource.Color.option_normal_text_color));
				}
			}
		};
		valueRow.AddView(editText);

		var rangeText = new TextView(this)
		{
			Text = $"  ({opt.Min} ～ {opt.Max}  初期値: {opt.DefaultValue})"
		};
		rangeText.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		rangeText.SetTextColor(Color.Gray);
		rangeText.SetPadding(DpToPx(8), 0, 0, 0);
		valueRow.AddView(rangeText);

		card.AddView(valueRow);
	}

	private void BuildComboOption(LinearLayout card, USIOptionCombo opt, OptionDef def)
	{
		var title = new TextView(this) { Text = def.DisplayName };
		title.SetTextSize(Android.Util.ComplexUnitType.Sp, 15);
		title.SetTypeface(null, TypefaceStyle.Bold);
		card.AddView(title);

		var desc = new TextView(this) { Text = def.Description };
		desc.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		desc.SetTextColor(Color.Gray);
		card.AddView(desc);

		var spinner = new Spinner(this);
		var adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, opt.ComboValues);
		adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
		spinner.Adapter = adapter;

		int selectedIndex = opt.ComboValues.IndexOf(opt.Value);
		if (selectedIndex >= 0)
			spinner.SetSelection(selectedIndex);

		spinner.ItemSelected += (s, e) =>
		{
			opt.SetValue(opt.ComboValues[e.Position]);
		};

		var spinnerLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent,
			LinearLayout.LayoutParams.WrapContent);
		spinnerLp.TopMargin = DpToPx(4);
		spinner.LayoutParameters = spinnerLp;
		card.AddView(spinner);

		var defaultText = new TextView(this)
		{
			Text = $"初期値: {opt.DefaultValue}"
		};
		defaultText.SetTextSize(Android.Util.ComplexUnitType.Sp, 11);
		defaultText.SetTextColor(Color.Gray);
		card.AddView(defaultText);
	}

	private void BuildStringOption(LinearLayout card, USIOptionString opt, OptionDef def)
	{
		var title = new TextView(this) { Text = def.DisplayName };
		title.SetTextSize(Android.Util.ComplexUnitType.Sp, 15);
		title.SetTypeface(null, TypefaceStyle.Bold);
		card.AddView(title);

		var desc = new TextView(this) { Text = def.Description };
		desc.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		desc.SetTextColor(Color.Gray);
		card.AddView(desc);

		var editText = new EditText(this);
		editText.Text = opt.Value;
		editText.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		var etLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent,
			LinearLayout.LayoutParams.WrapContent);
		etLp.TopMargin = DpToPx(4);
		editText.LayoutParameters = etLp;

		if (opt.Value != opt.DefaultValue)
		{
			editText.SetTextColor(ColorUtils.Get(this, Resource.Color.option_change_text_color));
		}

		editText.FocusChange += (s, e) =>
		{
			if (!e.HasFocus)
			{
				opt.SetValue(editText.Text);
				if (opt.Value != opt.DefaultValue)
					editText.SetTextColor(ColorUtils.Get(this, Resource.Color.option_change_text_color));
				else
					editText.SetTextColor(ColorUtils.Get(this, Resource.Color.option_normal_text_color));
			}
		};
		card.AddView(editText);

		var defaultText = new TextView(this)
		{
			Text = $"初期値: {opt.DefaultValue}"
		};
		defaultText.SetTextSize(Android.Util.ComplexUnitType.Sp, 11);
		defaultText.SetTextColor(Color.Gray);
		card.AddView(defaultText);
	}

	private int DpToPx(int dp)
	{
		return (int)(dp * Resources.DisplayMetrics.Density + 0.5f);
	}

	private void UpdateWindowSettings()
	{
		if (Settings.AppSettings.DispToolbar)
		{
			uiFlags = SystemUiFlags.ImmersiveSticky;
			Window.ClearFlags(WindowManagerFlags.Fullscreen);
		}
		else
		{
			uiFlags = SystemUiFlags.Fullscreen;
			Window.Attributes.Flags |= WindowManagerFlags.Fullscreen;
		}
		Window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiFlags;
	}

	private class OptionDef
	{
		public string[] Aliases { get; }
		public string Category { get; }
		public string DisplayName { get; }
		public string Description { get; }

		public OptionDef(string[] aliases, string category, string displayName, string description)
		{
			Aliases = aliases;
			Category = category;
			DisplayName = displayName;
			Description = description;
		}
	}
}
