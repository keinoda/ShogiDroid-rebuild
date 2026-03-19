using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using ShogiGUI;
using ShogiGUI.Engine;

namespace ShogiDroid;

[Activity(Label = "リモートエンジン (vast.ai)", ConfigurationChanges = (Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize), Theme = "@style/AppTheme")]
public class VastAiActivity : Activity
{
	public const string ExtraHost = "vast_ai_host";
	public const string ExtraPort = "vast_ai_port";
	public const string ExtraInstanceId = "vast_ai_instance_id";
	private const int PortNNUE = 6000;
	private const int PortDEEP = 6001;

	private VastAiManager vastAi_;
	private CancellationTokenSource cts_;

	// UI
	private LinearLayout rootLayout_;
	private TextView statusText_;
	private ProgressBar progressBar_;
	private LinearLayout existingInstancesContainer_;
	private LinearLayout offerListContainer_;
	private LinearLayout connectButtonsContainer_;
	private Button searchButton_;
	private EditText apiKeyEdit_;
	private EditText dockerImageEdit_;
	private EditText onStartCmdEdit_;
	private EditText manualHostEdit_;
	private EditText manualPortEdit_;

	// Search criteria UI
	private EditText gpuNamesEdit_;
	private EditText minCpuCoresEdit_;
	private EditText maxDphEdit_;
	private EditText minGpuRamEdit_;
	private EditText minDiskSpaceEdit_;
	private EditText minReliabilityEdit_;
	private EditText minInetDownEdit_;
	private EditText numGpusEdit_;
	private Spinner sortFieldSpinner_;
	private CheckBox sortAscCheck_;

	protected override void OnCreate(Bundle savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		cts_ = new CancellationTokenSource();
		BuildUI();
		LoadSettings();
		LoadExistingInstancesAsync();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		cts_?.Cancel();
		cts_?.Dispose();
		vastAi_?.Dispose();
	}

	private void BuildUI()
	{
		UpdateWindowSettings();

		var scroll = new ScrollView(this);
		scroll.SetPadding(DpToPx(16), DpToPx(16), DpToPx(16), DpToPx(16));

		rootLayout_ = new LinearLayout(this) { Orientation = Orientation.Vertical };

		// === 既存インスタンス ===
		AddSectionHeader(rootLayout_, "既存インスタンス");

		var refreshBtn = new Button(this) { Text = "更新" };
		refreshBtn.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		var refreshLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.WrapContent, LinearLayout.LayoutParams.WrapContent);
		refreshLp.BottomMargin = DpToPx(4);
		refreshBtn.LayoutParameters = refreshLp;
		refreshBtn.Click += (s, e) => LoadExistingInstancesAsync();
		rootLayout_.AddView(refreshBtn);

		existingInstancesContainer_ = new LinearLayout(this) { Orientation = Orientation.Vertical };
		rootLayout_.AddView(existingInstancesContainer_);

		// === Progress / Status ===
		progressBar_ = new ProgressBar(this, null, Android.Resource.Attribute.ProgressBarStyleHorizontal);
		progressBar_.Indeterminate = true;
		progressBar_.Visibility = ViewStates.Gone;
		var progressLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		progressLp.TopMargin = DpToPx(4);
		progressBar_.LayoutParameters = progressLp;
		rootLayout_.AddView(progressBar_);

		statusText_ = new TextView(this) { Text = "" };
		statusText_.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
		var statusLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		statusLp.TopMargin = DpToPx(4);
		statusText_.LayoutParameters = statusLp;
		rootLayout_.AddView(statusText_);

		// === 接続ボタン (動的) ===
		connectButtonsContainer_ = new LinearLayout(this) { Orientation = Orientation.Vertical };
		connectButtonsContainer_.Visibility = ViewStates.Gone;
		rootLayout_.AddView(connectButtonsContainer_);

		// === 新規作成 ===
		AddSectionHeader(rootLayout_, "新規インスタンス作成");

		// API設定
		apiKeyEdit_ = AddEditField(rootLayout_, "APIキー", "vast.ai API Key");
		apiKeyEdit_.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationPassword;

		dockerImageEdit_ = AddEditField(rootLayout_, "Dockerイメージ", "keinoda/shogi:v9.0");

		onStartCmdEdit_ = AddEditField(rootLayout_, "起動コマンド", "onstart command");
		onStartCmdEdit_.SetMaxLines(3);
		onStartCmdEdit_.SetSingleLine(false);

		// === 検索条件 ===
		AddSectionHeader(rootLayout_, "検索条件");

		gpuNamesEdit_ = AddEditField(rootLayout_, "GPU名 (カンマ区切り)", "例: RTX 4090, RTX 4090 D");

		// 2列レイアウトで検索条件を並べる
		var row1 = MakeTwoColumnRow();
		minCpuCoresEdit_ = AddCompactEditField(row1, "最小CPUコア数", "32", true);
		maxDphEdit_ = AddCompactEditField(row1, "最大単価 ($/h)", "0.5", false);
		rootLayout_.AddView(row1);

		var row2 = MakeTwoColumnRow();
		minGpuRamEdit_ = AddCompactEditField(row2, "最小GPU RAM (GB)", "0", true);
		minDiskSpaceEdit_ = AddCompactEditField(row2, "最小ディスク (GB)", "0", true);
		rootLayout_.AddView(row2);

		var row3 = MakeTwoColumnRow();
		minReliabilityEdit_ = AddCompactEditField(row3, "最小信頼性 (%)", "0", false);
		minInetDownEdit_ = AddCompactEditField(row3, "最小DL速度 (Mbps)", "0", false);
		rootLayout_.AddView(row3);

		var row4 = MakeTwoColumnRow();
		numGpusEdit_ = AddCompactEditField(row4, "GPU数 (0=指定なし)", "0", true);
		// ソート方向
		var sortAscContainer = new LinearLayout(this) { Orientation = Orientation.Vertical };
		sortAscContainer.LayoutParameters = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1f);
		sortAscContainer.SetPadding(DpToPx(4), 0, DpToPx(4), 0);
		var sortAscLabel = new TextView(this) { Text = "ソート順" };
		sortAscLabel.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		sortAscLabel.SetTypeface(null, TypefaceStyle.Bold);
		sortAscContainer.AddView(sortAscLabel);
		sortAscCheck_ = new CheckBox(this) { Text = "昇順 (安い順)", Checked = true };
		sortAscCheck_.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		sortAscContainer.AddView(sortAscCheck_);
		row4.AddView(sortAscContainer);
		rootLayout_.AddView(row4);

		// ソートフィールド
		var sortRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
		sortRow.SetGravity(GravityFlags.CenterVertical);
		var sortRowLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		sortRowLp.TopMargin = DpToPx(4);
		sortRow.LayoutParameters = sortRowLp;

		var sortLabel = new TextView(this) { Text = "ソート基準: " };
		sortLabel.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
		sortLabel.SetTypeface(null, TypefaceStyle.Bold);
		sortRow.AddView(sortLabel);

		sortFieldSpinner_ = new Spinner(this);
		var sortItems = new[] { "時間単価", "DLパフォーマンス", "CPUコア数", "信頼性", "DL速度" };
		var sortAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, sortItems);
		sortAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
		sortFieldSpinner_.Adapter = sortAdapter;
		sortFieldSpinner_.LayoutParameters = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1f);
		sortRow.AddView(sortFieldSpinner_);
		rootLayout_.AddView(sortRow);

		searchButton_ = new Button(this) { Text = "オファー検索 (interruptible)" };
		searchButton_.Click += (s, e) => SearchOffersAsync();
		var searchLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		searchLp.TopMargin = DpToPx(8);
		searchButton_.LayoutParameters = searchLp;
		rootLayout_.AddView(searchButton_);

		// === 検索結果 ===
		offerListContainer_ = new LinearLayout(this) { Orientation = Orientation.Vertical };
		rootLayout_.AddView(offerListContainer_);

		// === 手動接続 ===
		AddSectionHeader(rootLayout_, "手動接続");

		manualHostEdit_ = AddEditField(rootLayout_, "ホスト (IPアドレス)", "例: 192.168.1.100");
		manualHostEdit_.Text = Settings.EngineSettings.RemoteHost;

		manualPortEdit_ = AddEditField(rootLayout_, "ポート", "6000");
		manualPortEdit_.InputType = Android.Text.InputTypes.ClassNumber;
		manualPortEdit_.Text = string.IsNullOrEmpty(Settings.EngineSettings.RemotePort) ? "6000" : Settings.EngineSettings.RemotePort;

		var manualConnectBtn = new Button(this) { Text = "手動接続" };
		var manualBtnLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		manualBtnLp.TopMargin = DpToPx(8);
		manualConnectBtn.LayoutParameters = manualBtnLp;
		manualConnectBtn.Click += (s, e) => ConnectManual();
		rootLayout_.AddView(manualConnectBtn);

		scroll.AddView(rootLayout_);
		SetContentView(scroll);
	}

	private static readonly string[] SortFieldValues = { "dph_total", "dlperf", "cpu_cores_effective", "reliability2", "inet_down" };

	private void LoadSettings()
	{
		apiKeyEdit_.Text = Settings.EngineSettings.VastAiApiKey;
		dockerImageEdit_.Text = Settings.EngineSettings.VastAiDockerImage;
		onStartCmdEdit_.Text = Settings.EngineSettings.VastAiOnStartCmd;

		// Search criteria
		gpuNamesEdit_.Text = Settings.EngineSettings.VastAiGpuNames;
		minCpuCoresEdit_.Text = Settings.EngineSettings.VastAiMinCpuCores.ToString();
		maxDphEdit_.Text = Settings.EngineSettings.VastAiMaxDph.ToString("G");
		minGpuRamEdit_.Text = Settings.EngineSettings.VastAiMinGpuRam.ToString();
		minDiskSpaceEdit_.Text = Settings.EngineSettings.VastAiMinDiskSpace.ToString();
		minReliabilityEdit_.Text = Settings.EngineSettings.VastAiMinReliability.ToString("G");
		minInetDownEdit_.Text = Settings.EngineSettings.VastAiMinInetDown.ToString("G");
		numGpusEdit_.Text = Settings.EngineSettings.VastAiNumGpus.ToString();
		sortAscCheck_.Checked = Settings.EngineSettings.VastAiSortAsc;

		int sortIndex = Array.IndexOf(SortFieldValues, Settings.EngineSettings.VastAiSortField);
		sortFieldSpinner_.SetSelection(sortIndex >= 0 ? sortIndex : 0);
	}

	private void SaveSettings()
	{
		Settings.EngineSettings.VastAiApiKey = apiKeyEdit_.Text?.Trim() ?? "";
		Settings.EngineSettings.VastAiDockerImage = dockerImageEdit_.Text?.Trim() ?? "";
		Settings.EngineSettings.VastAiOnStartCmd = onStartCmdEdit_.Text?.Trim() ?? "";

		// Search criteria
		Settings.EngineSettings.VastAiGpuNames = gpuNamesEdit_.Text?.Trim() ?? "";
		int.TryParse(minCpuCoresEdit_.Text, out int cpuCores);
		Settings.EngineSettings.VastAiMinCpuCores = cpuCores;
		double.TryParse(maxDphEdit_.Text, out double maxDph);
		Settings.EngineSettings.VastAiMaxDph = maxDph;
		int.TryParse(minGpuRamEdit_.Text, out int gpuRam);
		Settings.EngineSettings.VastAiMinGpuRam = gpuRam;
		int.TryParse(minDiskSpaceEdit_.Text, out int diskSpace);
		Settings.EngineSettings.VastAiMinDiskSpace = diskSpace;
		double.TryParse(minReliabilityEdit_.Text, out double reliability);
		Settings.EngineSettings.VastAiMinReliability = reliability;
		double.TryParse(minInetDownEdit_.Text, out double inetDown);
		Settings.EngineSettings.VastAiMinInetDown = inetDown;
		int.TryParse(numGpusEdit_.Text, out int numGpus);
		Settings.EngineSettings.VastAiNumGpus = numGpus;
		Settings.EngineSettings.VastAiSortAsc = sortAscCheck_.Checked;
		int sortPos = sortFieldSpinner_.SelectedItemPosition;
		Settings.EngineSettings.VastAiSortField = (sortPos >= 0 && sortPos < SortFieldValues.Length)
			? SortFieldValues[sortPos] : "dph_total";

		Settings.Save();
	}

	private VastAiManager GetManager()
	{
		SaveSettings();
		string apiKey = Settings.EngineSettings.VastAiApiKey;
		if (string.IsNullOrEmpty(apiKey))
		{
			Toast.MakeText(this, "APIキーを入力してください", ToastLength.Short).Show();
			return null;
		}
		if (vastAi_ == null)
			vastAi_ = new VastAiManager(apiKey);
		else
			vastAi_.SetApiKey(apiKey);
		return vastAi_;
	}

	// ===== Existing Instances =====

	private async void LoadExistingInstancesAsync()
	{
		var manager = GetManager();
		if (manager == null)
		{
			existingInstancesContainer_.RemoveAllViews();
			var msg = new TextView(this) { Text = "APIキーを設定してください" };
			msg.SetPadding(0, DpToPx(8), 0, DpToPx(8));
			existingInstancesContainer_.AddView(msg);
			return;
		}

		SetBusy(true, "インスタンス一覧を取得中...");

		try
		{
			var allInstances = await manager.ListInstancesAsync(cts_.Token);

			RunOnUiThread(() =>
			{
				existingInstancesContainer_.RemoveAllViews();

				// Show all instances, shogi-labeled first
				var shogiInstances = allInstances
					.Where(i => i.IsShogiInstance)
					.OrderByDescending(i => i.IsRunning)
					.ThenByDescending(i => i.IsLoading)
					.ToList();

				var otherInstances = allInstances
					.Where(i => !i.IsShogiInstance)
					.Where(i => i.IsRunning || i.IsStopped)
					.OrderByDescending(i => i.IsRunning)
					.ToList();

				if (shogiInstances.Count == 0 && otherInstances.Count == 0)
				{
					var msg = new TextView(this) { Text = "稼働中・停止中のインスタンスはありません" };
					msg.SetPadding(0, DpToPx(8), 0, DpToPx(8));
					existingInstancesContainer_.AddView(msg);
					SetBusy(false, "");
					return;
				}

				foreach (var inst in shogiInstances)
					AddInstanceCard(inst, isShogiLabeled: true);

				if (otherInstances.Count > 0)
				{
					var otherHeader = new TextView(this) { Text = "その他のインスタンス" };
					otherHeader.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
					otherHeader.SetTypeface(null, TypefaceStyle.Italic);
					otherHeader.SetTextColor(ColorUtils.Get(this, Resource.Color.vast_card_sub_text));
					var headerLp = new LinearLayout.LayoutParams(
						LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
					headerLp.TopMargin = DpToPx(8);
					otherHeader.LayoutParameters = headerLp;
					existingInstancesContainer_.AddView(otherHeader);

					foreach (var inst in otherInstances)
						AddInstanceCard(inst, isShogiLabeled: false);
				}

				SetBusy(false, "");
			});
		}
		catch (Exception ex)
		{
			RunOnUiThread(() =>
			{
				existingInstancesContainer_.RemoveAllViews();
				SetBusy(false, $"取得エラー: {ex.Message}");
			});
		}
	}

	private void AddInstanceCard(VastAiInstance inst, bool isShogiLabeled)
	{
		bool dark = ColorUtils.IsDarkMode(this);
		string bgColor = inst.IsRunning
			? (dark ? "#1B3A1B" : "#E8F5E9")
			: (inst.IsStopped
				? (dark ? "#3A2A00" : "#FFF3E0")
				: (dark ? "#0D1B3A" : "#E3F2FD"));

		var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
		card.SetBackgroundColor(Color.ParseColor(bgColor));
		card.SetPadding(DpToPx(12), DpToPx(8), DpToPx(12), DpToPx(8));
		var cardLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		cardLp.BottomMargin = DpToPx(6);
		card.LayoutParameters = cardLp;

		// Title: status + label
		string labelStr = isShogiLabeled ? " [将棋]" : "";
		var titleText = new TextView(this)
		{
			Text = $"[{inst.StatusDisplay}]{labelStr} ID:{inst.Id}"
		};
		titleText.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		titleText.SetTypeface(null, TypefaceStyle.Bold);
		card.AddView(titleText);

		// Specs
		var specsText = new TextView(this)
		{
			Text = $"{inst.GpuName} x{inst.NumGpus} | CPU {inst.CpuCoresEffective:F0}cores (割当) | RAM {inst.CpuRamGb:F0}GB | ${inst.DphTotal:F3}/h"
		};
		specsText.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		specsText.SetTextColor(ColorUtils.Get(this, Resource.Color.vast_card_sub_text));
		card.AddView(specsText);

		if (inst.IsRunning && !string.IsNullOrEmpty(inst.PublicIpAddr))
		{
			var ipText = new TextView(this) { Text = $"IP: {inst.PublicIpAddr}" };
			ipText.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
			ipText.SetTextColor(ColorUtils.Get(this, Resource.Color.vast_card_sub_text));
			card.AddView(ipText);
		}

		// Action buttons
		var btnRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
		btnRow.SetGravity(GravityFlags.CenterVertical);
		var btnRowLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		btnRowLp.TopMargin = DpToPx(4);
		btnRow.LayoutParameters = btnRowLp;

		if (inst.IsRunning && !string.IsNullOrEmpty(inst.PublicIpAddr))
		{
			int nnuePort = inst.GetMappedPort(PortNNUE);
			int deepPort = inst.GetMappedPort(PortDEEP);

			var nnueBtn = MakeButton("NNUE接続", Resource.Color.vast_btn_green);
			nnueBtn.Click += (s, e) => ConnectToInstance(inst, nnuePort, "NNUE");
			btnRow.AddView(nnueBtn);

			var deepBtn = MakeButton("DEEP接続", Resource.Color.vast_btn_blue);
			deepBtn.Click += (s, e) => ConnectToInstance(inst, deepPort, "DEEP");
			btnRow.AddView(deepBtn);
		}

		// 停止/再開 トグルボタン
		if (inst.IsRunning)
		{
			var toggleBtn = MakeButton("停止", Resource.Color.vast_btn_orange);
			toggleBtn.Click += (s, e) => StopInstanceAsync(inst.Id);
			btnRow.AddView(toggleBtn);
		}
		else if (inst.IsStopped)
		{
			var toggleBtn = MakeButton("再開", Resource.Color.vast_btn_green);
			toggleBtn.Click += (s, e) => ResumeInstanceAsync(inst.Id);
			btnRow.AddView(toggleBtn);
		}

		var destroyBtn = MakeButton("削除", Resource.Color.vast_btn_red);
		destroyBtn.Click += (s, e) => ConfirmDestroyAsync(inst.Id);
		btnRow.AddView(destroyBtn);

		card.AddView(btnRow);
		existingInstancesContainer_.AddView(card);
	}

	private Button MakeButton(string text, int colorResId)
	{
		var btn = new Button(this) { Text = text };
		btn.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		btn.SetTextColor(ColorUtils.Get(this, colorResId));
		var lp = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1f);
		lp.SetMargins(DpToPx(2), 0, DpToPx(2), 0);
		btn.LayoutParameters = lp;
		return btn;
	}

	private void ConnectToInstance(VastAiInstance inst, int port, string engineType)
	{
		Settings.EngineSettings.RemoteHost = inst.PublicIpAddr;
		Settings.EngineSettings.RemotePort = port.ToString();
		Settings.EngineSettings.EngineNo = RemoteEnginePlayer.RemoteEngineNo;
		Settings.EngineSettings.EngineName = $"{engineType} (vast.ai #{inst.Id})";
		Settings.EngineSettings.VastAiInstanceId = inst.Id;
		Settings.Save();

		// Start auto-suspend watchdog
		VastAiWatchdog.Instance.StartMonitoring(
			inst.Id,
			Settings.EngineSettings.VastAiApiKey);

		var resultIntent = new Intent();
		resultIntent.PutExtra(ExtraHost, inst.PublicIpAddr);
		resultIntent.PutExtra(ExtraPort, port);
		resultIntent.PutExtra(ExtraInstanceId, inst.Id);
		SetResult(Result.Ok, resultIntent);
		Finish();
	}

	private async void StopInstanceAsync(int instanceId)
	{
		var manager = GetManager();
		if (manager == null) return;

		SetBusy(true, $"インスタンス {instanceId} を停止中...");

		try
		{
			await manager.StopInstanceAsync(instanceId, cts_.Token);
			VastAiWatchdog.Instance.StopMonitoring();

			// APIの状態反映を待つ
			await Task.Delay(3000, cts_.Token);

			RunOnUiThread(() =>
			{
				SetBusy(false, "休止しました");
				LoadExistingInstancesAsync();
			});
		}
		catch (Exception ex)
		{
			RunOnUiThread(() =>
			{
				SetBusy(false, $"停止エラー: {ex.Message}");
				Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
			});
		}
	}

	private async void ResumeInstanceAsync(int instanceId)
	{
		var manager = GetManager();
		if (manager == null) return;

		SetBusy(true, $"インスタンス {instanceId} を再開中...");

		try
		{
			await manager.StartInstanceAsync(instanceId, cts_.Token);
			await manager.LabelInstanceAsync(instanceId, VastAiManager.ShogiLabel, cts_.Token);

			Settings.EngineSettings.VastAiInstanceId = instanceId;
			Settings.Save();

			RunOnUiThread(() => statusText_.Text = "起動待機中...");

			var progress = new Progress<string>(msg =>
			{
				RunOnUiThread(() => statusText_.Text = msg);
			});

			await manager.WaitForReadyAsync(instanceId, progress, cts_.Token);

			RunOnUiThread(() => statusText_.Text = "エンジン起動待機中 (30秒)...");
			await Task.Delay(30000, cts_.Token);

			RunOnUiThread(() =>
			{
				SetBusy(false, "再開完了");
				LoadExistingInstancesAsync();
			});
		}
		catch (Exception ex)
		{
			RunOnUiThread(() =>
			{
				SetBusy(false, $"再開エラー: {ex.Message}");
				Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
			});
		}
	}

	private void ConfirmDestroyAsync(int instanceId)
	{
		new AlertDialog.Builder(this)
			.SetTitle("インスタンス削除")
			.SetMessage($"インスタンス {instanceId} を削除しますか？\nデータは失われます。")
			.SetPositiveButton("削除", async (s, e) =>
			{
				var manager = GetManager();
				if (manager == null) return;

				SetBusy(true, "削除中...");
				try
				{
					await manager.DestroyInstanceAsync(instanceId, cts_.Token);
					VastAiWatchdog.Instance.StopMonitoring();

					if (Settings.EngineSettings.VastAiInstanceId == instanceId)
					{
						Settings.EngineSettings.VastAiInstanceId = 0;
						Settings.Save();
					}

					RunOnUiThread(() =>
					{
						SetBusy(false, "削除しました");
						LoadExistingInstancesAsync();
					});
				}
				catch (Exception ex)
				{
					RunOnUiThread(() =>
					{
						SetBusy(false, $"削除エラー: {ex.Message}");
						Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
					});
				}
			})
			.SetNegativeButton("キャンセル", (s, e) => { })
			.Show();
	}

	// ===== New Instance Creation =====

	private async void SearchOffersAsync()
	{
		var manager = GetManager();
		if (manager == null) return;

		SetBusy(true, "オファーを検索中 (interruptible)...");
		offerListContainer_.RemoveAllViews();

		try
		{
			var criteria = BuildCriteriaFromUI();

			var offers = await manager.SearchOffersAsync(criteria, cts_.Token);

			RunOnUiThread(() =>
			{
				offerListContainer_.RemoveAllViews();

				if (offers.Count == 0)
				{
					var noResult = new TextView(this) { Text = "条件に合うオファーが見つかりません" };
					noResult.SetPadding(0, DpToPx(8), 0, DpToPx(8));
					offerListContainer_.AddView(noResult);
					SetBusy(false, "オファーが見つかりません");
					return;
				}

				int displayCount = Math.Min(offers.Count, 10);
				for (int i = 0; i < displayCount; i++)
					AddOfferCard(offers[i]);

				SetBusy(false, $"{offers.Count}件中 上位{displayCount}件表示");
			});
		}
		catch (Exception ex)
		{
			RunOnUiThread(() =>
			{
				SetBusy(false, $"エラー: {ex.Message}");
				Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
			});
		}
	}

	private void AddOfferCard(VastAiOffer offer)
	{
		var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
		card.SetBackgroundColor(ColorUtils.Get(this, Resource.Color.card_background));
		card.SetPadding(DpToPx(12), DpToPx(8), DpToPx(12), DpToPx(8));
		var cardLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		cardLp.BottomMargin = DpToPx(6);
		card.LayoutParameters = cardLp;

		var gpuText = new TextView(this)
		{
			Text = $"{offer.GpuName} x{offer.NumGpus} (VRAM {offer.GpuRamGb:F0}GB)"
		};
		gpuText.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		gpuText.SetTypeface(null, TypefaceStyle.Bold);
		card.AddView(gpuText);

		var cpuText = new TextView(this)
		{
			Text = $"CPU: {offer.CpuName} {offer.CpuCoresEffective:F0}cores (割当) | RAM: {offer.CpuRamGb:F0}GB | 信頼性: {offer.Reliability:F1}%"
		};
		cpuText.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		cpuText.SetTextColor(ColorUtils.Get(this, Resource.Color.vast_card_sub_text));
		card.AddView(cpuText);

		var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
		row.SetGravity(GravityFlags.CenterVertical);

		var priceText = new TextView(this) { Text = $"${offer.DphTotal:F3}/h" };
		priceText.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		priceText.SetTextColor(ColorUtils.Get(this, Resource.Color.title_background));
		priceText.SetTypeface(null, TypefaceStyle.Bold);
		priceText.LayoutParameters = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1f);
		row.AddView(priceText);

		var startBtn = new Button(this) { Text = "起動" };
		startBtn.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		startBtn.Click += (s, e) => CreateAndWaitAsync(offer);
		row.AddView(startBtn);

		card.AddView(row);
		offerListContainer_.AddView(card);
	}

	private async void CreateAndWaitAsync(VastAiOffer offer)
	{
		var manager = GetManager();
		if (manager == null) return;

		searchButton_.Enabled = false;
		SetBusy(true, $"インスタンスを作成中... ({offer.GpuName})");
		offerListContainer_.RemoveAllViews();

		try
		{
			var config = new VastAiInstanceConfig
			{
				DockerImage = Settings.EngineSettings.VastAiDockerImage,
				Ports = new[] { PortNNUE, PortDEEP },
				DiskGb = 8.0,
				OnStartCmd = Settings.EngineSettings.VastAiOnStartCmd
			};

			int instanceId = await manager.CreateInstanceAsync(offer.Id, config, cts_.Token);

			Settings.EngineSettings.VastAiInstanceId = instanceId;
			Settings.Save();

			RunOnUiThread(() => statusText_.Text = "起動待機中...");

			var progress = new Progress<string>(msg =>
			{
				RunOnUiThread(() => statusText_.Text = msg);
			});

			await manager.WaitForReadyAsync(instanceId, progress, cts_.Token);

			RunOnUiThread(() => statusText_.Text = "エンジン起動待機中 (30秒)...");
			await Task.Delay(30000, cts_.Token);

			RunOnUiThread(() =>
			{
				SetBusy(false, "起動完了");
				searchButton_.Enabled = true;
				LoadExistingInstancesAsync();
			});
		}
		catch (System.OperationCanceledException)
		{
			RunOnUiThread(() =>
			{
				SetBusy(false, "キャンセルされました");
				searchButton_.Enabled = true;
			});
		}
		catch (Exception ex)
		{
			RunOnUiThread(() =>
			{
				SetBusy(false, $"エラー: {ex.Message}");
				searchButton_.Enabled = true;
				Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
			});
		}
	}

	// ===== Manual Connection =====

	private void ConnectManual()
	{
		string host = manualHostEdit_.Text?.Trim();
		string portStr = manualPortEdit_.Text?.Trim();
		if (string.IsNullOrEmpty(host))
		{
			Toast.MakeText(this, "ホストを入力してください", ToastLength.Short).Show();
			return;
		}
		int port = 6000;
		int.TryParse(portStr, out port);

		Settings.EngineSettings.RemoteHost = host;
		Settings.EngineSettings.RemotePort = port.ToString();
		Settings.EngineSettings.EngineNo = RemoteEnginePlayer.RemoteEngineNo;
		Settings.EngineSettings.EngineName = $"リモート ({host}:{port})";
		Settings.Save();

		var resultIntent = new Intent();
		resultIntent.PutExtra(ExtraHost, host);
		resultIntent.PutExtra(ExtraPort, port);
		SetResult(Result.Ok, resultIntent);
		Finish();
	}

	// ===== Search Criteria =====

	private VastAiSearchCriteria BuildCriteriaFromUI()
	{
		string gpuText = gpuNamesEdit_.Text?.Trim() ?? "";
		string[] gpuNames = string.IsNullOrEmpty(gpuText)
			? Array.Empty<string>()
			: gpuText.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();

		int.TryParse(minCpuCoresEdit_.Text, out int cpuCores);
		double.TryParse(maxDphEdit_.Text, out double maxDph);
		int.TryParse(minGpuRamEdit_.Text, out int gpuRam);
		int.TryParse(minDiskSpaceEdit_.Text, out int diskSpace);
		double.TryParse(minReliabilityEdit_.Text, out double reliability);
		double.TryParse(minInetDownEdit_.Text, out double inetDown);
		int.TryParse(numGpusEdit_.Text, out int numGpus);

		int sortPos = sortFieldSpinner_.SelectedItemPosition;
		string sortField = (sortPos >= 0 && sortPos < SortFieldValues.Length)
			? SortFieldValues[sortPos] : "dph_total";

		return new VastAiSearchCriteria
		{
			GpuNames = gpuNames,
			MinCpuCoresEffective = cpuCores,
			MaxDphTotal = maxDph,
			MinGpuRam = gpuRam,
			MinDiskSpace = diskSpace,
			MinReliability = reliability,
			MinInetDown = inetDown,
			NumGpus = numGpus,
			SortField = sortField,
			SortAsc = sortAscCheck_.Checked
		};
	}

	private LinearLayout MakeTwoColumnRow()
	{
		var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
		var lp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		lp.TopMargin = DpToPx(2);
		row.LayoutParameters = lp;
		return row;
	}

	private EditText AddCompactEditField(LinearLayout parent, string label, string hint, bool integerOnly)
	{
		var container = new LinearLayout(this) { Orientation = Orientation.Vertical };
		container.LayoutParameters = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1f);
		container.SetPadding(DpToPx(4), 0, DpToPx(4), 0);

		var labelView = new TextView(this) { Text = label };
		labelView.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		labelView.SetTypeface(null, TypefaceStyle.Bold);
		container.AddView(labelView);

		var editText = new EditText(this);
		editText.Hint = hint;
		editText.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
		editText.SetSingleLine(true);
		editText.InputType = integerOnly
			? Android.Text.InputTypes.ClassNumber
			: (Android.Text.InputTypes.ClassNumber | Android.Text.InputTypes.NumberFlagDecimal);
		editText.LayoutParameters = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		container.AddView(editText);

		parent.AddView(container);
		return editText;
	}

	// ===== Helpers =====

	private void SetBusy(bool busy, string message)
	{
		progressBar_.Visibility = busy ? ViewStates.Visible : ViewStates.Gone;
		statusText_.Text = message;
	}

	private void AddSectionHeader(LinearLayout parent, string title)
	{
		var header = new TextView(this);
		header.Text = title;
		header.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
		header.SetTypeface(null, TypefaceStyle.Bold);
		header.SetTextColor(ColorUtils.Get(this, Resource.Color.title_background));
		var lp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		lp.TopMargin = DpToPx(16);
		lp.BottomMargin = DpToPx(4);
		header.LayoutParameters = lp;
		parent.AddView(header);

		var divider = new View(this);
		divider.SetBackgroundColor(ColorUtils.Get(this, Resource.Color.title_background));
		divider.LayoutParameters = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, DpToPx(2));
		parent.AddView(divider);
	}

	private EditText AddEditField(LinearLayout parent, string label, string hint)
	{
		var labelView = new TextView(this) { Text = label };
		labelView.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
		labelView.SetTypeface(null, TypefaceStyle.Bold);
		var labelLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		labelLp.TopMargin = DpToPx(8);
		labelView.LayoutParameters = labelLp;
		parent.AddView(labelView);

		var editText = new EditText(this);
		editText.Hint = hint;
		editText.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
		editText.SetSingleLine(true);
		editText.LayoutParameters = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		parent.AddView(editText);

		return editText;
	}

	private int DpToPx(int dp)
	{
		return (int)(dp * Resources.DisplayMetrics.Density + 0.5f);
	}

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
	}
}
