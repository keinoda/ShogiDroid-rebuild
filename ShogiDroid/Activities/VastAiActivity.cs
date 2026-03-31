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
using Renci.SshNet;
using ShogiGUI;
using ShogiGUI.Engine;

namespace ShogiDroid;

[Activity(Label = "リモートエンジン (vast.ai)", ConfigurationChanges = (Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize), Theme = "@style/AppTheme")]
public class VastAiActivity : ThemedActivity
{
	private const int SSH_KEY_PICK_CODE = 130;
	public const string ExtraHost = "vast_ai_host";
	public const string ExtraPort = "vast_ai_port";
	public const string ExtraInstanceId = "vast_ai_instance_id";
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
	private EditText sshKeyPathEdit_;

	// Search criteria UI
	private EditText minCpuCoresEdit_;
	private EditText maxDphEdit_;
	private EditText numGpusEdit_;
	private EditText minCudaEdit_;
	private CheckBox interruptibleCheck_;
	private LinearLayout gpuCheckListContainer_;
	private List<CheckBox> gpuCheckBoxes_ = new List<CheckBox>();
	private TextView creditText_;

	// 検索結果の表示制御
	private List<VastAiOffer> currentOffers_;
	private int displayedOfferCount_;
	private const int OffersPerPage = 10;

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

		var outerFrame = new FrameLayout(this);
		outerFrame.SetFitsSystemWindows(true);

		var scroll = new ScrollView(this);
		scroll.SetClipToPadding(false);
		scroll.SetPadding(DpToPx(16), DpToPx(16), DpToPx(16), DpToPx(16));

		rootLayout_ = new LinearLayout(this) { Orientation = Orientation.Vertical };

		// === 既存インスタンス ===
		AddSectionHeader(rootLayout_, "既存インスタンス");

		// 更新ボタン + クレジット残高を同じ行に
		var refreshRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
		refreshRow.SetGravity(GravityFlags.CenterVertical);
		var refreshRowLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		refreshRowLp.BottomMargin = DpToPx(4);
		refreshRow.LayoutParameters = refreshRowLp;

		var refreshBtn = new Button(this) { Text = "更新" };
		refreshBtn.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		refreshBtn.LayoutParameters = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.WrapContent, LinearLayout.LayoutParams.WrapContent);
		refreshBtn.Click += (s, e) => LoadExistingInstancesAsync();
		refreshRow.AddView(refreshBtn);

		creditText_ = new TextView(this) { Text = "" };
		creditText_.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
		creditText_.Gravity = GravityFlags.Right | GravityFlags.CenterVertical;
		creditText_.LayoutParameters = new LinearLayout.LayoutParams(
			0, LinearLayout.LayoutParams.WrapContent, 1f);
		refreshRow.AddView(creditText_);

		rootLayout_.AddView(refreshRow);

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

		dockerImageEdit_ = AddEditField(rootLayout_, "Dockerイメージ", "ngs43/shogi:v9.0");

		onStartCmdEdit_ = AddEditField(rootLayout_, "起動コマンド", "onstart command");
		onStartCmdEdit_.SetMaxLines(3);
		onStartCmdEdit_.SetSingleLine(false);

		// SSH接続設定
		AddSectionHeader(rootLayout_, "SSH接続設定");

		var sshKeyLabel = new TextView(this) { Text = "SSH秘密鍵パス" };
		sshKeyLabel.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
		sshKeyLabel.SetTypeface(null, TypefaceStyle.Bold);
		var sshKeyLabelLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		sshKeyLabelLp.TopMargin = DpToPx(8);
		sshKeyLabel.LayoutParameters = sshKeyLabelLp;
		rootLayout_.AddView(sshKeyLabel);

		var sshKeyRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
		sshKeyRow.SetGravity(GravityFlags.CenterVertical);
		sshKeyRow.LayoutParameters = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);

		sshKeyPathEdit_ = new EditText(this);
		sshKeyPathEdit_.Hint = "/sdcard/.ssh/id_rsa";
		sshKeyPathEdit_.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
		sshKeyPathEdit_.SetSingleLine(true);
		sshKeyPathEdit_.LayoutParameters = new LinearLayout.LayoutParams(
			0, LinearLayout.LayoutParams.WrapContent, 1f);
		sshKeyRow.AddView(sshKeyPathEdit_);

		var sshKeyBrowseBtn = new Button(this) { Text = "選択" };
		sshKeyBrowseBtn.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		sshKeyBrowseBtn.LayoutParameters = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.WrapContent, LinearLayout.LayoutParams.WrapContent);
		sshKeyBrowseBtn.Click += (s, e) =>
		{
			var intent = new Intent(Intent.ActionOpenDocument);
			intent.AddCategory(Intent.CategoryOpenable);
			intent.SetType("*/*");
			StartActivityForResult(intent, SSH_KEY_PICK_CODE);
		};
		sshKeyRow.AddView(sshKeyBrowseBtn);

		rootLayout_.AddView(sshKeyRow);

		// === 検索条件 ===
		AddSectionHeader(rootLayout_, "検索条件");

		// GPU選択（折りたたみリスト）
		var gpuHeader = new Button(this) { Text = "▶ GPU選択" };
		gpuHeader.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
		gpuHeader.Gravity = GravityFlags.Left | GravityFlags.CenterVertical;
		gpuHeader.SetBackgroundColor(Android.Graphics.Color.Transparent);
		gpuHeader.SetTextColor(statusText_.TextColors);
		gpuHeader.SetTypeface(null, TypefaceStyle.Bold);

		gpuCheckListContainer_ = new LinearLayout(this) { Orientation = Orientation.Vertical };
		gpuCheckListContainer_.Visibility = ViewStates.Gone;
		gpuCheckListContainer_.SetPadding(DpToPx(8), 0, 0, DpToPx(4));

		var gpuNames = new[] {
			"RTX 5090", "RTX 5080", "RTX 5070 Ti",
			"RTX 4090", "RTX 4090 D", "RTX 4080", "RTX 4070 Ti",
			"RTX 3090", "RTX 3090 Ti", "RTX 3080",
			"A100", "A100 SXM4", "H100", "H100 SXM5", "L40S"
		};
		foreach (var name in gpuNames)
		{
			var cb = new CheckBox(this) { Text = name };
			cb.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
			gpuCheckBoxes_.Add(cb);
			gpuCheckListContainer_.AddView(cb);
		}

		gpuHeader.Click += (s, e) =>
		{
			if (gpuCheckListContainer_.Visibility == ViewStates.Gone)
			{
				gpuCheckListContainer_.Visibility = ViewStates.Visible;
				gpuHeader.Text = "▼ GPU選択";
			}
			else
			{
				gpuCheckListContainer_.Visibility = ViewStates.Gone;
				gpuHeader.Text = "▶ GPU選択";
			}
		};
		rootLayout_.AddView(gpuHeader);
		rootLayout_.AddView(gpuCheckListContainer_);

		// 計算条件（2列）
		var row1 = MakeTwoColumnRow();
		minCpuCoresEdit_ = AddCompactEditField(row1, "最小CPUコア数", "32", true);
		maxDphEdit_ = AddCompactEditField(row1, "最大単価 ($/h)", "0.5", false);
		rootLayout_.AddView(row1);

		var row2 = MakeTwoColumnRow();
		numGpusEdit_ = AddCompactEditField(row2, "最小GPU数 (0=指定なし)", "0", true);
		// interruptible/on-demand
		var typeContainer = new LinearLayout(this) { Orientation = Orientation.Vertical };
		typeContainer.LayoutParameters = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1f);
		typeContainer.SetPadding(DpToPx(4), 0, DpToPx(4), 0);
		var typeLabel = new TextView(this) { Text = "タイプ" };
		typeLabel.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		typeLabel.SetTypeface(null, TypefaceStyle.Bold);
		typeContainer.AddView(typeLabel);
		interruptibleCheck_ = new CheckBox(this) { Text = "Interruptible", Checked = true };
		interruptibleCheck_.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		typeContainer.AddView(interruptibleCheck_);
		row2.AddView(typeContainer);
		rootLayout_.AddView(row2);

		var row3 = MakeTwoColumnRow();
		minCudaEdit_ = AddCompactEditField(row3, "最小CUDA Ver", "0", false);
		rootLayout_.AddView(row3);

		searchButton_ = new Button(this) { Text = "オファー検索" };
		searchButton_.Click += (s, e) => SearchOffersAsync();
		var searchLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		searchLp.TopMargin = DpToPx(8);
		searchButton_.LayoutParameters = searchLp;
		rootLayout_.AddView(searchButton_);

		// === 検索結果 ===
		offerListContainer_ = new LinearLayout(this) { Orientation = Orientation.Vertical };
		rootLayout_.AddView(offerListContainer_);

		scroll.AddView(rootLayout_);
		outerFrame.AddView(scroll);
		SetContentView(outerFrame);
	}

	private static readonly string[] SortFieldValues = { "dph_total", "dlperf", "cpu_cores_effective", "reliability2", "inet_down" };

	private void LoadSettings()
	{
		apiKeyEdit_.Text = Settings.EngineSettings.VastAiApiKey;
		dockerImageEdit_.Text = Settings.EngineSettings.VastAiDockerImage;
		onStartCmdEdit_.Text = Settings.EngineSettings.VastAiOnStartCmd;

		// SSH設定
		sshKeyPathEdit_.Text = Settings.EngineSettings.VastAiSshKeyPath;

		// Search criteria
		minCpuCoresEdit_.Text = Settings.EngineSettings.VastAiMinCpuCores.ToString();
		maxDphEdit_.Text = Settings.EngineSettings.VastAiMaxDph.ToString("G");
		numGpusEdit_.Text = Settings.EngineSettings.VastAiNumGpus.ToString();
		minCudaEdit_.Text = Settings.EngineSettings.VastAiMinCudaVersion.ToString("G");
		interruptibleCheck_.Checked = Settings.EngineSettings.VastAiSortField != "on-demand";

		// GPU チェックボックスを設定から復元
		var savedGpus = (Settings.EngineSettings.VastAiGpuNames ?? "")
			.Split(',', StringSplitOptions.RemoveEmptyEntries)
			.Select(g => g.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var cb in gpuCheckBoxes_)
		{
			cb.Checked = savedGpus.Contains(cb.Text);
		}
	}

	private void SaveSettings()
	{
		Settings.EngineSettings.VastAiApiKey = apiKeyEdit_.Text?.Trim() ?? "";
		Settings.EngineSettings.VastAiDockerImage = dockerImageEdit_.Text?.Trim() ?? "";
		Settings.EngineSettings.VastAiOnStartCmd = onStartCmdEdit_.Text?.Trim() ?? "";

		// SSH設定
		Settings.EngineSettings.VastAiSshKeyPath = sshKeyPathEdit_.Text?.Trim() ?? "";

		// Search criteria
		var selectedGpus = gpuCheckBoxes_.Where(cb => cb.Checked).Select(cb => cb.Text);
		Settings.EngineSettings.VastAiGpuNames = string.Join(", ", selectedGpus);
		int.TryParse(minCpuCoresEdit_.Text, out int cpuCores);
		Settings.EngineSettings.VastAiMinCpuCores = cpuCores;
		double.TryParse(maxDphEdit_.Text, out double maxDph);
		Settings.EngineSettings.VastAiMaxDph = maxDph;
		int.TryParse(numGpusEdit_.Text, out int numGpus);
		Settings.EngineSettings.VastAiNumGpus = numGpus;
		double.TryParse(minCudaEdit_.Text, out double cudaVer);
		Settings.EngineSettings.VastAiMinCudaVersion = cudaVer;
		Settings.EngineSettings.VastAiSortField = interruptibleCheck_.Checked ? "dph_total" : "on-demand";
		Settings.EngineSettings.VastAiSortAsc = true;

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

			// クレジット残高を取得
			var credit = await manager.GetCreditBalanceAsync(cts_.Token);

			RunOnUiThread(() =>
			{
				if (credit.HasValue)
				{
					creditText_.Text = $"残高: ${credit.Value:F2}";
				}
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
			Text = $"{inst.GpuName} x{inst.NumGpus} | {inst.CpuName} {inst.CpuCoresEffective:F0}cores (割当) | RAM {inst.CpuRamGb:F0}GB | ${inst.DphTotal:F3}/h"
		};
		specsText.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		specsText.SetTextColor(ColorUtils.Get(this, Resource.Color.vast_card_sub_text));
		card.AddView(specsText);

		if (inst.IsRunning && !string.IsNullOrEmpty(inst.PublicIpAddr))
		{
			var (dispHost, dispPort) = inst.GetSshEndpoint();
			var ipText = new TextView(this) { Text = $"SSH: {dispHost}:{dispPort}" };
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
			var connectBtn = MakeButton("接続", Resource.Color.vast_btn_green);
			connectBtn.Click += (s, e) => ScanAndSelectEngineAsync(inst);
			btnRow.AddView(connectBtn);
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

	/// <summary>
	/// SSHでインスタンスに接続し、/workspace内のエンジンを検出して選択ダイアログを表示
	/// </summary>
	private async void ScanAndSelectEngineAsync(VastAiInstance inst)
	{
		string sshKeyPath = Settings.EngineSettings.VastAiSshKeyPath;
		if (string.IsNullOrEmpty(sshKeyPath))
		{
			Toast.MakeText(this, "SSH秘密鍵パスを設定してください", ToastLength.Short).Show();
			return;
		}

		var (sshHost, sshPort) = inst.GetSshEndpoint();
		SetBusy(true, "エンジンを検出中...");

		List<EngineInfo> engines;
		try
		{
			engines = await Task.Run(() => ScanEngines(sshHost, sshPort, sshKeyPath));
		}
		catch (Exception ex)
		{
			RunOnUiThread(() =>
			{
				SetBusy(false, $"エンジン検出エラー: {ex.Message}");
				Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
			});
			return;
		}

		RunOnUiThread(() =>
		{
			SetBusy(false, "");
			if (engines.Count == 0)
			{
				Toast.MakeText(this, "/workspace にエンジンが見つかりません", ToastLength.Long).Show();
				return;
			}
			ShowEngineSelectDialog(inst, engines);
		});
	}

	private List<EngineInfo> ScanEngines(string host, int sshPort, string keyPath)
	{
		var results = new List<EngineInfo>();
		using var keyFile = new PrivateKeyFile(keyPath);
		using var client = new SshClient(host, sshPort, "root", keyFile);
		client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(15);
		client.Connect();

		// /workspace 直下のディレクトリを列挙し、各ディレクトリ内の実行可能ファイルを検出
		var cmd = client.RunCommand("find /workspace -maxdepth 2 -type f -executable 2>/dev/null | head -50");
		string output = cmd.Result ?? "";

		foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
		{
			string path = line.Trim();
			if (string.IsNullOrEmpty(path)) continue;

			// 明らかにエンジンでないファイルを除外
			string fileName = System.IO.Path.GetFileName(path);
			if (fileName.StartsWith(".") || fileName == "python" || fileName == "python3" ||
				fileName == "pip" || fileName == "pip3" || fileName == "bash" || fileName == "sh" ||
				fileName.EndsWith(".sh") || fileName.EndsWith(".py"))
				continue;

			string dir = System.IO.Path.GetDirectoryName(path);
			string dirName = System.IO.Path.GetFileName(dir);
			results.Add(new EngineInfo
			{
				DisplayName = $"{dirName}/{fileName}",
				Command = $"cd {dir} && exec ./{fileName}",
				Path = path
			});
		}

		client.Disconnect();
		return results;
	}

	private void ShowEngineSelectDialog(VastAiInstance inst, List<EngineInfo> engines)
	{
		string[] items = engines.Select(e => e.DisplayName).ToArray();

		new AlertDialog.Builder(this)
			.SetTitle("エンジンを選択")
			.SetItems(items, (s, e) =>
			{
				var selected = engines[e.Which];
				ConnectToInstance(inst, selected.Command, selected.DisplayName);
			})
			.SetNegativeButton("キャンセル", (s, e) => { })
			.Show();
	}

	private class EngineInfo
	{
		public string DisplayName;
		public string Command;
		public string Path;
	}

	private void ConnectToInstance(VastAiInstance inst, string engineCommand, string engineType)
	{
		string sshKeyPath = Settings.EngineSettings.VastAiSshKeyPath;
		if (string.IsNullOrEmpty(sshKeyPath))
		{
			Toast.MakeText(this, "SSH秘密鍵パスを設定してください", ToastLength.Short).Show();
			return;
		}
		if (string.IsNullOrEmpty(engineCommand))
		{
			Toast.MakeText(this, $"{engineType}エンジンコマンドを設定してください", ToastLength.Short).Show();
			return;
		}

		var (sshHost, sshPort) = inst.GetSshEndpoint();

		Settings.EngineSettings.RemoteHost = sshHost;
		Settings.EngineSettings.VastAiSshPort = sshPort;
		Settings.EngineSettings.VastAiSshEngineCommand = engineCommand;
		Settings.EngineSettings.EngineNo = RemoteEnginePlayer.RemoteEngineNo;
		Settings.EngineSettings.EngineName = $"{engineType} (vast.ai #{inst.Id})";
		Settings.EngineSettings.VastAiInstanceId = inst.Id;
		Settings.EngineSettings.VastAiCpuCores = (int)inst.CpuCoresEffective;
		Settings.EngineSettings.VastAiRamMb = (int)(inst.CpuRamGb * 1024);
		Settings.EngineSettings.VastAiGpuRamMb = (int)(inst.GpuRamGb * 1024);
		Settings.Save();

		// アイドル自動終了監視を開始し、接続情報を保存
		VastAiWatchdog.Instance.StartMonitoring(
			inst.Id,
			Settings.EngineSettings.VastAiApiKey);
		VastAiWatchdog.Instance.SaveLastConnectionInfo(
			inst.Id, sshHost, sshPort, engineCommand);

		var resultIntent = new Intent();
		resultIntent.PutExtra(ExtraHost, inst.PublicIpAddr);
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
				currentOffers_ = offers;
				displayedOfferCount_ = 0;

				if (offers.Count == 0)
				{
					var noResult = new TextView(this) { Text = "条件に合うオファーが見つかりません" };
					noResult.SetPadding(0, DpToPx(8), 0, DpToPx(8));
					offerListContainer_.AddView(noResult);
					SetBusy(false, "オファーが見つかりません");
					return;
				}

				ShowMoreOffers();
				SetBusy(false, $"{offers.Count}件のオファーが見つかりました");
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

	private void ShowMoreOffers()
	{
		if (currentOffers_ == null) return;

		// 「もっと表示」ボタンがあれば削除
		var existingMore = offerListContainer_.FindViewWithTag("more_button");
		if (existingMore != null) offerListContainer_.RemoveView(existingMore);

		int end = Math.Min(displayedOfferCount_ + OffersPerPage, currentOffers_.Count);
		for (int i = displayedOfferCount_; i < end; i++)
		{
			AddOfferCard(currentOffers_[i]);
		}
		displayedOfferCount_ = end;

		// まだ残りがあれば「もっと表示」ボタンを追加
		if (displayedOfferCount_ < currentOffers_.Count)
		{
			var moreBtn = new Button(this) { Text = $"もっと表示 ({currentOffers_.Count - displayedOfferCount_}件)" };
			moreBtn.Tag = "more_button";
			moreBtn.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
			var moreLp = new LinearLayout.LayoutParams(
				LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
			moreLp.TopMargin = DpToPx(4);
			moreBtn.LayoutParameters = moreLp;
			moreBtn.Click += (s, e) => ShowMoreOffers();
			offerListContainer_.AddView(moreBtn);
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

		string flag = CountryCodeToFlag(offer.Geolocation);
		var gpuText = new TextView(this)
		{
			Text = $"{flag} {offer.GpuName} x{offer.NumGpus} (VRAM {offer.GpuRamGb:F0}GB)"
		};
		gpuText.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		gpuText.SetTypeface(null, TypefaceStyle.Bold);
		card.AddView(gpuText);

		string cudaStr = offer.CudaMaxGood > 0 ? $" | CUDA {offer.CudaMaxGood:F1}" : "";
		var cpuText = new TextView(this)
		{
			Text = $"{offer.CpuName} {offer.CpuCoresEffective:F0}cores | RAM {offer.CpuRamGb:F0}GB | 信頼性 {offer.Reliability:F1}%{cudaStr}"
		};
		cpuText.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		cpuText.SetTextColor(ColorUtils.Get(this, Resource.Color.vast_card_sub_text));
		card.AddView(cpuText);

		var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
		row.SetGravity(GravityFlags.CenterVertical);

		bool isOnDemand = interruptibleCheck_ != null && !interruptibleCheck_.Checked;
		string priceLabel;
		if (isOnDemand)
			priceLabel = $"${offer.DphTotal:F3}/h (on-demand)";
		else if (offer.MinBid > 0)
			priceLabel = $"${offer.DphTotal:F3}/h (最低入札: ${offer.MinBid:F3})";
		else
			priceLabel = $"${offer.DphTotal:F3}/h (interruptible)";
		var priceText = new TextView(this) { Text = priceLabel };
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
			bool isInterruptible = interruptibleCheck_ != null && interruptibleCheck_.Checked;
			var config = new VastAiInstanceConfig
			{
				DockerImage = Settings.EngineSettings.VastAiDockerImage,
				Ports = Array.Empty<int>(),
				DiskGb = 8.0,
				OnStartCmd = Settings.EngineSettings.VastAiOnStartCmd,
				// interruptibleの場合、オファーの表示価格を入札額として使用
				BidPrice = isInterruptible ? offer.DphTotal : 0
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


	// ===== Search Criteria =====

	private VastAiSearchCriteria BuildCriteriaFromUI()
	{
		var selectedGpus = gpuCheckBoxes_
			.Where(cb => cb.Checked)
			.Select(cb => cb.Text)
			.ToArray();

		int.TryParse(minCpuCoresEdit_.Text, out int cpuCores);
		double.TryParse(maxDphEdit_.Text, out double maxDph);
		int.TryParse(numGpusEdit_.Text, out int numGpus);

		double.TryParse(minCudaEdit_.Text, out double cudaVer);

		return new VastAiSearchCriteria
		{
			GpuNames = selectedGpus,
			MinCpuCoresEffective = cpuCores,
			MaxDphTotal = maxDph,
			NumGpus = numGpus,
			MinCudaVersion = cudaVer,
			SortField = "dph_total",
			SortAsc = true,
			RentType = interruptibleCheck_.Checked ? "bid" : "on-demand"
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

	/// <summary>
	/// 2文字の国コード（ISO 3166-1 alpha-2）をUnicode国旗絵文字に変換
	/// </summary>
	private static string CountryCodeToFlag(string countryCode)
	{
		if (string.IsNullOrEmpty(countryCode) || countryCode.Length < 2)
			return "";
		countryCode = countryCode.Trim().ToUpperInvariant();
		if (countryCode.Length < 2)
			return "";
		// Regional Indicator Symbol Letter: U+1F1E6 ('A') ~ U+1F1FF ('Z')
		int first = 0x1F1E6 + (countryCode[0] - 'A');
		int second = 0x1F1E6 + (countryCode[1] - 'A');
		return char.ConvertFromUtf32(first) + char.ConvertFromUtf32(second);
	}

	private int DpToPx(int dp)
	{
		return (int)(dp * Resources.DisplayMetrics.Density + 0.5f);
	}

	protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
	{
		base.OnActivityResult(requestCode, resultCode, data);
		if (requestCode == SSH_KEY_PICK_CODE && resultCode == Result.Ok && data?.Data != null)
		{
			try
			{
				// 選択されたファイルをアプリ内部ストレージにコピー
				var uri = data.Data;
				string destDir = System.IO.Path.Combine(FilesDir.AbsolutePath, "ssh");
				System.IO.Directory.CreateDirectory(destDir);
				string destPath = System.IO.Path.Combine(destDir, "id_rsa");

				using (var input = ContentResolver.OpenInputStream(uri))
				using (var output = new System.IO.FileStream(destPath, System.IO.FileMode.Create))
				{
					input.CopyTo(output);
				}

				// パーミッションを制限
				Java.IO.File file = new Java.IO.File(destPath);
				file.SetReadable(false, false);
				file.SetReadable(true, true);
				file.SetWritable(false, false);

				sshKeyPathEdit_.Text = destPath;
				Toast.MakeText(this, "SSH秘密鍵をインポートしました", ToastLength.Short).Show();
			}
			catch (Exception ex)
			{
				Toast.MakeText(this, $"秘密鍵の読み込みに失敗: {ex.Message}", ToastLength.Long).Show();
			}
		}
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
