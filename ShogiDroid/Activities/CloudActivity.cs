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

[Activity(Label = "クラウド", ConfigurationChanges = (Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize), Theme = "@style/AppTheme")]
public class CloudActivity : ThemedActivity
{
	private const int SSH_KEY_PICK_CODE = 130;
	public const string ExtraHost = "vast_ai_host";
	public const string ExtraPort = "vast_ai_port";
	public const string ExtraInstanceId = "vast_ai_instance_id";

	private VastAiManager vastAi_;
	private AwsSpotManager awsManager_;
	private GcpSpotManager gcpManager_;
	private CancellationTokenSource cts_;

	// ── 共通 UI ──
	private LinearLayout rootLayout_;
	private TextView statusText_;
	private ProgressBar progressBar_;
	private LinearLayout existingInstancesContainer_;
	private LinearLayout connectButtonsContainer_;
	private EditText sshKeyPathEdit_;

	// ── vast.ai UI ──
	private LinearLayout vastAiSectionContent_;
	private EditText apiKeyEdit_;
	private EditText dockerImageEdit_;
	private EditText onStartCmdEdit_;
	private Button searchButton_;
	private LinearLayout offerListContainer_;
	private EditText minCpuCoresEdit_;
	private EditText maxDphEdit_;
	private EditText numGpusEdit_;
	private EditText minCudaEdit_;
	private CheckBox interruptibleCheck_;
	private LinearLayout gpuCheckListContainer_;
	private List<CheckBox> gpuCheckBoxes_ = new List<CheckBox>();
	private TextView creditText_;
	private List<VastAiOffer> currentOffers_;
	private int displayedOfferCount_;
	private const int OffersPerPage = 10;

	// ── AWS UI ──
	private LinearLayout awsSectionContent_;
	private EditText awsAccessKeyEdit_;
	private EditText awsSecretKeyEdit_;
	private EditText awsDockerImageEdit_;
	private Spinner awsInstanceTypeSpinner_;
	private LinearLayout awsSpotPriceContainer_;

	// ── GCP UI ──
	private LinearLayout gcpSectionContent_;
	private EditText gcpServiceAccountKeyPathEdit_;
	private EditText gcpDockerImageEdit_;
	private EditText gcpZoneEdit_;
	private Spinner gcpMachineTypeSpinner_;

	// ── インスタンスタイプ定義 ──
	private static readonly (string type, int vCpus, int ramGb)[] AwsInstanceTypes = {
		("c7a.4xlarge", 16, 32),
		("c7a.8xlarge", 32, 64),
		("c7a.12xlarge", 48, 96),
		("c7a.16xlarge", 64, 128),
		("c7a.24xlarge", 96, 192),
		("c7a.48xlarge", 192, 384),
		("c7a.metal-48xl", 192, 384),
	};

	// C4D は hyperdisk-balanced 強制（最小IOPS課金 ~$0.25/h）のためコスパが悪く除外
	private static readonly (string type, int vCpus, int ramGb, double estSpot)[] GcpMachineTypes = {
		("c3d-highcpu-16", 16, 32, 0.10),
		("c3d-highcpu-30", 30, 59, 0.18),
		("c3d-highcpu-60", 60, 118, 0.36),
		("c3d-highcpu-90", 90, 177, 0.53),
		("c3d-highcpu-180", 180, 354, 1.07),
		("c3d-highcpu-360", 360, 708, 2.03),
	};

	// AWS Spot 価格キャッシュ（インスタンスタイプ → 最安AZ価格）
	private Dictionary<string, decimal> awsSpotPriceCache_ = new Dictionary<string, decimal>();
	// Spinner 更新中のイベント再入防止
	private bool suppressSpinnerEvent_ = false;

	// ===== Lifecycle =====

	protected override void OnCreate(Bundle savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		cts_ = new CancellationTokenSource();
		BuildUI();
		LoadSettings();
		LoadAllInstancesAsync();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		cts_?.Cancel();
		cts_?.Dispose();
		vastAi_?.Dispose();
		awsManager_?.Dispose();
		gcpManager_?.Dispose();
	}

	// ===== UI 構築 =====

	private void BuildUI()
	{
		UpdateWindowSettings();

		var outerFrame = new FrameLayout(this);
		outerFrame.SetFitsSystemWindows(true);

		var scroll = new ScrollView(this);
		scroll.SetClipToPadding(false);
		scroll.SetPadding(DpToPx(16), DpToPx(16), DpToPx(16), DpToPx(16));

		rootLayout_ = new LinearLayout(this) { Orientation = Orientation.Vertical };

		// ── 稼働中のインスタンス（統合一覧） ──
		AddSectionHeader(rootLayout_, "稼働中のインスタンス");

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
		refreshBtn.Click += (s, e) => LoadAllInstancesAsync();
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

		// Progress / Status
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

		connectButtonsContainer_ = new LinearLayout(this) { Orientation = Orientation.Vertical };
		connectButtonsContainer_.Visibility = ViewStates.Gone;
		rootLayout_.AddView(connectButtonsContainer_);

		// ── SSH接続設定（共通） ──
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
		sshKeyPathEdit_.Hint = "/sdcard/.ssh/id_ed25519";
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

		// ── AWS スポットインスタンス（折りたたみ） ──
		BuildAwsSection();

		// ── GCP Spot VM（折りたたみ） ──
		BuildGcpSection();

		// ── vast.ai（折りたたみ） ──
		BuildVastAiSection();

		scroll.AddView(rootLayout_);
		outerFrame.AddView(scroll);
		SetContentView(outerFrame);
	}

	/// <summary>
	/// AWS セクション UI を構築（折りたたみ式）
	/// </summary>
	private void BuildAwsSection()
	{
		var header = MakeFoldableHeader("▶ AWS スポットインスタンス");
		awsSectionContent_ = new LinearLayout(this) { Orientation = Orientation.Vertical };
		awsSectionContent_.Visibility = ViewStates.Gone;
		awsSectionContent_.SetPadding(DpToPx(4), 0, 0, DpToPx(8));

		header.Click += (s, e) => ToggleFoldable(header, awsSectionContent_, "AWS スポットインスタンス");

		rootLayout_.AddView(header);
		rootLayout_.AddView(awsSectionContent_);

		// 認証情報
		awsAccessKeyEdit_ = AddEditField(awsSectionContent_, "アクセスキー", "AKIA...");
		awsAccessKeyEdit_.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationPassword;

		awsSecretKeyEdit_ = AddEditField(awsSectionContent_, "シークレットキー", "");
		awsSecretKeyEdit_.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationPassword;

		awsDockerImageEdit_ = AddEditField(awsSectionContent_, "Dockerイメージ", "keinoda/shogi:v9.21nnue");

		// インスタンスタイプ選択
		var typeLabel = new TextView(this) { Text = "インスタンスタイプ" };
		typeLabel.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
		typeLabel.SetTypeface(null, TypefaceStyle.Bold);
		var typeLabelLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		typeLabelLp.TopMargin = DpToPx(8);
		typeLabel.LayoutParameters = typeLabelLp;
		awsSectionContent_.AddView(typeLabel);

		awsInstanceTypeSpinner_ = new Spinner(this);
		var awsItems = AwsInstanceTypes.Select(t => $"{t.type} ({t.vCpus}vCPU {t.ramGb}GB)").ToArray();
		awsInstanceTypeSpinner_.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerDropDownItem, awsItems);
		awsInstanceTypeSpinner_.ItemSelected += (s, e) => FetchAwsSpotPriceForSelectedType();
		awsSectionContent_.AddView(awsInstanceTypeSpinner_);

		// スポット価格一覧（AZ別、起動ボタン付き）
		var priceLabel = new TextView(this) { Text = "Spot 価格（AZ別）" };
		priceLabel.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
		priceLabel.SetTypeface(null, TypefaceStyle.Bold);
		var priceLabelLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		priceLabelLp.TopMargin = DpToPx(12);
		priceLabel.LayoutParameters = priceLabelLp;
		awsSectionContent_.AddView(priceLabel);

		var priceRefreshBtn = new Button(this) { Text = "価格を更新" };
		priceRefreshBtn.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		priceRefreshBtn.Click += (s, e) => { lastFetchedAwsType_ = ""; FetchAwsSpotPriceForSelectedType(); };
		awsSectionContent_.AddView(priceRefreshBtn);

		awsSpotPriceContainer_ = new LinearLayout(this) { Orientation = Orientation.Vertical };
		awsSectionContent_.AddView(awsSpotPriceContainer_);
	}

	/// <summary>
	/// AWS Spinner 選択変更時に Spot 価格を取得して表示
	/// </summary>
	private string lastFetchedAwsType_ = "";

	private async void FetchAwsSpotPriceForSelectedType()
	{
		if (suppressSpinnerEvent_) return;
		int pos = awsInstanceTypeSpinner_.SelectedItemPosition;
		if (pos < 0 || pos >= AwsInstanceTypes.Length) return;
		string instanceType = AwsInstanceTypes[pos].type;

		// 同じタイプの再取得を抑制
		if (instanceType == lastFetchedAwsType_) return;
		lastFetchedAwsType_ = instanceType;

		Settings.EngineSettings.AwsInstanceType = instanceType;

		var manager = GetAwsManager();
		if (manager == null)
		{
			awsSpotPriceContainer_.RemoveAllViews();
			return;
		}

		awsSpotPriceContainer_.RemoveAllViews();
		var loadingText = new TextView(this) { Text = "価格を取得中..." };
		loadingText.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		awsSpotPriceContainer_.AddView(loadingText);

		try
		{
			var prices = await manager.GetSpotPricesAsync(instanceType, cts_.Token);

			RunOnUiThread(() =>
			{
				awsSpotPriceContainer_.RemoveAllViews();
				if (prices.Count == 0)
				{
					var msg = new TextView(this) { Text = "価格情報が取得できませんでした" };
					msg.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
					awsSpotPriceContainer_.AddView(msg);
					return;
				}

				// Spinner のラベルも価格付きに更新
				decimal cheapest = prices.Min(p => p.Price);
				var updatedItems = AwsInstanceTypes.Select(t =>
				{
					string label = $"{t.type} ({t.vCpus}vCPU {t.ramGb}GB)";
					if (t.type == instanceType)
						label += $" ~${cheapest:F4}/h";
					else if (awsSpotPriceCache_.TryGetValue(t.type, out decimal cached))
						label += $" ~${cached:F4}/h";
					return label;
				}).ToArray();
				awsSpotPriceCache_[instanceType] = cheapest;
				suppressSpinnerEvent_ = true;
				awsInstanceTypeSpinner_.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerDropDownItem, updatedItems);
				awsInstanceTypeSpinner_.SetSelection(pos);
				suppressSpinnerEvent_ = false;

				foreach (var price in prices)
				{
					var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
					row.SetGravity(GravityFlags.CenterVertical);
					row.SetPadding(0, DpToPx(4), 0, DpToPx(4));

					var priceText = new TextView(this)
					{
						Text = $"{price.AvailabilityZone}: ${price.Price:F4}/h"
					};
					priceText.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
					priceText.LayoutParameters = new LinearLayout.LayoutParams(
						0, LinearLayout.LayoutParams.WrapContent, 1f);
					row.AddView(priceText);

					var launchBtn = new Button(this) { Text = "起動" };
					launchBtn.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
					string az = price.AvailabilityZone;
					launchBtn.Click += (s2, e2) => LaunchAwsSpotAsync(az);
					row.AddView(launchBtn);

					awsSpotPriceContainer_.AddView(row);
				}
			});
		}
		catch (Exception ex)
		{
			RunOnUiThread(() =>
			{
				awsSpotPriceContainer_.RemoveAllViews();
				var msg = new TextView(this) { Text = $"価格取得エラー: {ex.Message}" };
				msg.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
				awsSpotPriceContainer_.AddView(msg);
			});
		}
	}

	/// <summary>
	/// GCP セクション UI を構築（折りたたみ式）
	/// </summary>
	private void BuildGcpSection()
	{
		var header = MakeFoldableHeader("▶ GCP Spot VM");
		gcpSectionContent_ = new LinearLayout(this) { Orientation = Orientation.Vertical };
		gcpSectionContent_.Visibility = ViewStates.Gone;
		gcpSectionContent_.SetPadding(DpToPx(4), 0, 0, DpToPx(8));

		header.Click += (s, e) =>
		{
			ToggleFoldable(header, gcpSectionContent_, "GCP Spot VM");
			// セクション展開時に価格取得
			if (gcpSectionContent_.Visibility == ViewStates.Visible)
				FetchGcpSpotPrices();
		};

		rootLayout_.AddView(header);
		rootLayout_.AddView(gcpSectionContent_);

		gcpServiceAccountKeyPathEdit_ = AddEditField(gcpSectionContent_, "サービスアカウントキー (JSON)", "/sdcard/.gcp/sa-key.json");
		gcpDockerImageEdit_ = AddEditField(gcpSectionContent_, "Dockerイメージ", "keinoda/shogi:v9.21nnue");
		gcpZoneEdit_ = AddEditField(gcpSectionContent_, "ゾーン", "us-central1-a");

		// マシンタイプ選択（Spinner）
		var typeLabel = new TextView(this) { Text = "マシンタイプ" };
		typeLabel.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
		typeLabel.SetTypeface(null, TypefaceStyle.Bold);
		var typeLabelLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		typeLabelLp.TopMargin = DpToPx(8);
		typeLabel.LayoutParameters = typeLabelLp;
		gcpSectionContent_.AddView(typeLabel);

		gcpMachineTypeSpinner_ = new Spinner(this);
		UpdateGcpSpinnerItems(null);
		gcpSectionContent_.AddView(gcpMachineTypeSpinner_);

		var launchBtn = new Button(this) { Text = "Spot VM を起動" };
		launchBtn.Click += (s, e) => ConfirmAndLaunchGcpSpotAsync();
		var launchLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		launchLp.TopMargin = DpToPx(8);
		launchBtn.LayoutParameters = launchLp;
		gcpSectionContent_.AddView(launchBtn);
	}

	/// <summary>
	/// GCP Spinner のアイテムを更新（価格情報付き）
	/// </summary>
	private void UpdateGcpSpinnerItems(Dictionary<string, double> prices)
	{
		int prevPos = gcpMachineTypeSpinner_?.SelectedItemPosition ?? 0;
		var items = GcpMachineTypes.Select(t =>
		{
			string label = $"{t.type} ({t.vCpus}vCPU {t.ramGb}GB)";
			if (prices != null && prices.TryGetValue(t.type, out double price))
				label += $" ${price:F3}/h";
			else if (t.estSpot > 0)
				label += $" ~${t.estSpot:F2}/h";
			return label;
		}).ToArray();
		suppressSpinnerEvent_ = true;
		gcpMachineTypeSpinner_.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerDropDownItem, items);
		if (prevPos >= 0 && prevPos < items.Length)
			gcpMachineTypeSpinner_.SetSelection(prevPos);
		suppressSpinnerEvent_ = false;
	}

	/// <summary>
	/// GCP Billing Catalog API から Spot 価格を取得して Spinner を更新
	/// </summary>
	private async void FetchGcpSpotPrices()
	{
		string keyPath = Settings.EngineSettings.GcpServiceAccountKeyPath;
		if (string.IsNullOrEmpty(keyPath) || !System.IO.File.Exists(keyPath)) return;

		try
		{
			string zone = gcpZoneEdit_.Text?.Trim() ?? "us-central1-a";
			string region = zone.Length > 2 ? zone.Substring(0, zone.LastIndexOf('-')) : zone;

			using var mgr = new GcpSpotManager(keyPath);
			string[] types = GcpMachineTypes.Select(t => t.type).ToArray();
			var prices = await mgr.GetSpotPricingAsync(region, types, cts_.Token);

			RunOnUiThread(() =>
			{
				if (prices.Count > 0)
					UpdateGcpSpinnerItems(prices);
			});
		}
		catch (Exception ex)
		{
			AppDebug.Log.Info($"GCP価格取得エラー: {ex.Message}");
		}
	}

	/// <summary>
	/// vast.ai セクション UI を構築（折りたたみ式）
	/// </summary>
	private void BuildVastAiSection()
	{
		var header = MakeFoldableHeader("▶ vast.ai");
		vastAiSectionContent_ = new LinearLayout(this) { Orientation = Orientation.Vertical };
		vastAiSectionContent_.Visibility = ViewStates.Gone;
		vastAiSectionContent_.SetPadding(DpToPx(4), 0, 0, DpToPx(8));

		header.Click += (s, e) => ToggleFoldable(header, vastAiSectionContent_, "vast.ai");

		rootLayout_.AddView(header);
		rootLayout_.AddView(vastAiSectionContent_);

		// API設定
		apiKeyEdit_ = AddEditField(vastAiSectionContent_, "APIキー", "vast.ai API Key");
		apiKeyEdit_.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationPassword;

		dockerImageEdit_ = AddEditField(vastAiSectionContent_, "Dockerイメージ", "keinoda/shogi:v9.0");

		onStartCmdEdit_ = AddEditField(vastAiSectionContent_, "起動コマンド", "onstart command");
		onStartCmdEdit_.SetMaxLines(3);
		onStartCmdEdit_.SetSingleLine(false);

		// 検索条件
		var searchHeader = new TextView(this) { Text = "検索条件" };
		searchHeader.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		searchHeader.SetTypeface(null, TypefaceStyle.Bold);
		var shLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		shLp.TopMargin = DpToPx(12);
		searchHeader.LayoutParameters = shLp;
		vastAiSectionContent_.AddView(searchHeader);

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
		vastAiSectionContent_.AddView(gpuHeader);
		vastAiSectionContent_.AddView(gpuCheckListContainer_);

		// 計算条件（2列）
		var row1 = MakeTwoColumnRow();
		minCpuCoresEdit_ = AddCompactEditField(row1, "最小CPUコア数", "32", true);
		maxDphEdit_ = AddCompactEditField(row1, "最大単価 ($/h)", "0.5", false);
		vastAiSectionContent_.AddView(row1);

		var row2 = MakeTwoColumnRow();
		numGpusEdit_ = AddCompactEditField(row2, "最小GPU数 (0=指定なし)", "0", true);
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
		vastAiSectionContent_.AddView(row2);

		var row3 = MakeTwoColumnRow();
		minCudaEdit_ = AddCompactEditField(row3, "最小CUDA Ver", "0", false);
		vastAiSectionContent_.AddView(row3);

		searchButton_ = new Button(this) { Text = "オファー検索" };
		searchButton_.Click += (s, e) => SearchOffersAsync();
		var searchLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		searchLp.TopMargin = DpToPx(8);
		searchButton_.LayoutParameters = searchLp;
		vastAiSectionContent_.AddView(searchButton_);

		// 検索結果
		offerListContainer_ = new LinearLayout(this) { Orientation = Orientation.Vertical };
		vastAiSectionContent_.AddView(offerListContainer_);
	}

	// ===== 設定読み書き =====

	private void LoadSettings()
	{
		// 共通
		sshKeyPathEdit_.Text = Settings.EngineSettings.VastAiSshKeyPath;

		// vast.ai
		apiKeyEdit_.Text = Settings.EngineSettings.VastAiApiKey;
		dockerImageEdit_.Text = Settings.EngineSettings.VastAiDockerImage;
		onStartCmdEdit_.Text = Settings.EngineSettings.VastAiOnStartCmd;
		minCpuCoresEdit_.Text = Settings.EngineSettings.VastAiMinCpuCores.ToString();
		maxDphEdit_.Text = Settings.EngineSettings.VastAiMaxDph.ToString("G");
		numGpusEdit_.Text = Settings.EngineSettings.VastAiNumGpus.ToString();
		minCudaEdit_.Text = Settings.EngineSettings.VastAiMinCudaVersion.ToString("G");
		interruptibleCheck_.Checked = Settings.EngineSettings.VastAiSortField != "on-demand";

		var savedGpus = (Settings.EngineSettings.VastAiGpuNames ?? "")
			.Split(',', StringSplitOptions.RemoveEmptyEntries)
			.Select(g => g.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var cb in gpuCheckBoxes_)
			cb.Checked = savedGpus.Contains(cb.Text);

		// AWS
		awsAccessKeyEdit_.Text = Settings.EngineSettings.AwsAccessKey;
		awsSecretKeyEdit_.Text = Settings.EngineSettings.AwsSecretKey;
		awsDockerImageEdit_.Text = Settings.EngineSettings.AwsDockerImage;

		// AWS: Spinner の選択位置を復元
		string savedAwsType = Settings.EngineSettings.AwsInstanceType;
		for (int i = 0; i < AwsInstanceTypes.Length; i++)
		{
			if (AwsInstanceTypes[i].type == savedAwsType)
			{
				awsInstanceTypeSpinner_.SetSelection(i);
				break;
			}
		}

		// GCP
		gcpServiceAccountKeyPathEdit_.Text = Settings.EngineSettings.GcpServiceAccountKeyPath;
		gcpDockerImageEdit_.Text = Settings.EngineSettings.GcpDockerImage;
		gcpZoneEdit_.Text = Settings.EngineSettings.GcpZone;
		// Spinner の選択位置を復元
		string savedGcpType = Settings.EngineSettings.GcpMachineType;
		for (int i = 0; i < GcpMachineTypes.Length; i++)
		{
			if (GcpMachineTypes[i].type == savedGcpType)
			{
				gcpMachineTypeSpinner_.SetSelection(i);
				break;
			}
		}
	}

	private void SaveSettings()
	{
		// 共通
		Settings.EngineSettings.VastAiSshKeyPath = sshKeyPathEdit_.Text?.Trim() ?? "";

		// vast.ai
		Settings.EngineSettings.VastAiApiKey = apiKeyEdit_.Text?.Trim() ?? "";
		Settings.EngineSettings.VastAiDockerImage = dockerImageEdit_.Text?.Trim() ?? "";
		Settings.EngineSettings.VastAiOnStartCmd = onStartCmdEdit_.Text?.Trim() ?? "";
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

		// AWS
		Settings.EngineSettings.AwsAccessKey = awsAccessKeyEdit_.Text?.Trim() ?? "";
		Settings.EngineSettings.AwsSecretKey = awsSecretKeyEdit_.Text?.Trim() ?? "";
		Settings.EngineSettings.AwsDockerImage = awsDockerImageEdit_.Text?.Trim() ?? "";

		// GCP
		Settings.EngineSettings.GcpServiceAccountKeyPath = gcpServiceAccountKeyPathEdit_.Text?.Trim() ?? "";
		Settings.EngineSettings.GcpDockerImage = gcpDockerImageEdit_.Text?.Trim() ?? "";
		Settings.EngineSettings.GcpZone = gcpZoneEdit_.Text?.Trim() ?? "";
		int gcpPos = gcpMachineTypeSpinner_.SelectedItemPosition;
		if (gcpPos >= 0 && gcpPos < GcpMachineTypes.Length)
			Settings.EngineSettings.GcpMachineType = GcpMachineTypes[gcpPos].type;

		Settings.Save();
	}

	// ===== Manager 取得 =====

	private VastAiManager GetVastAiManager()
	{
		SaveSettings();
		string apiKey = Settings.EngineSettings.VastAiApiKey;
		if (string.IsNullOrEmpty(apiKey))
		{
			Toast.MakeText(this, "vast.ai APIキーを入力してください", ToastLength.Short).Show();
			return null;
		}
		if (vastAi_ == null)
			vastAi_ = new VastAiManager(apiKey);
		else
			vastAi_.SetApiKey(apiKey);
		return vastAi_;
	}

	private string awsManagerKey_ = "";

	private AwsSpotManager GetAwsManager()
	{
		SaveSettings();
		string accessKey = Settings.EngineSettings.AwsAccessKey;
		string secretKey = Settings.EngineSettings.AwsSecretKey;
		string region = Settings.EngineSettings.AwsRegion;
		if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
		{
			Toast.MakeText(this, "AWSアクセスキーとシークレットキーを入力してください", ToastLength.Short).Show();
			return null;
		}
		// 認証情報が変わっていなければ既存インスタンスを再利用
		string key = $"{accessKey}:{secretKey}:{region}";
		if (awsManager_ != null && awsManagerKey_ == key)
			return awsManager_;
		awsManager_?.Dispose();
		awsManager_ = new AwsSpotManager(accessKey, secretKey, region);
		awsManagerKey_ = key;
		return awsManager_;
	}

	private string gcpManagerKey_ = "";

	private GcpSpotManager GetGcpManager()
	{
		SaveSettings();
		string keyPath = Settings.EngineSettings.GcpServiceAccountKeyPath;
		if (string.IsNullOrEmpty(keyPath))
		{
			Toast.MakeText(this, "GCPサービスアカウントキーのパスを入力してください", ToastLength.Short).Show();
			return null;
		}
		// キーパスが変わっていなければ既存インスタンスを再利用
		if (gcpManager_ != null && gcpManagerKey_ == keyPath)
			return gcpManager_;
		try
		{
			gcpManager_?.Dispose();
			gcpManager_ = new GcpSpotManager(keyPath);
			gcpManagerKey_ = keyPath;
			return gcpManager_;
		}
		catch (Exception ex)
		{
			Toast.MakeText(this, $"GCPキー読み込みエラー: {ex.Message}", ToastLength.Long).Show();
			return null;
		}
	}

	// ===== 統合インスタンス一覧 =====

	private async void LoadAllInstancesAsync()
	{
		existingInstancesContainer_.RemoveAllViews();
		SetBusy(true, "インスタンス一覧を取得中...");

		bool hasVastAi = !string.IsNullOrEmpty(Settings.EngineSettings.VastAiApiKey);
		bool hasAws = !string.IsNullOrEmpty(Settings.EngineSettings.AwsAccessKey)
			&& !string.IsNullOrEmpty(Settings.EngineSettings.AwsSecretKey);
		bool hasGcp = !string.IsNullOrEmpty(Settings.EngineSettings.GcpServiceAccountKeyPath);

		// vast.ai インスタンス
		List<VastAiInstance> vastAiInstances = null;
		double? vastAiCredit = null;
		if (hasVastAi)
		{
			try
			{
				var manager = GetVastAiManager();
				if (manager != null)
				{
					vastAiInstances = await manager.ListInstancesAsync(cts_.Token);
					vastAiCredit = await manager.GetCreditBalanceAsync(cts_.Token);
				}
			}
			catch (Exception ex)
			{
				AppDebug.Log.Error($"CloudActivity: vast.ai一覧取得エラー: {ex.Message}");
			}
		}

		// AWS インスタンス
		List<AwsInstance> awsInstances = null;
		if (hasAws)
		{
			try
			{
				var manager = GetAwsManager();
				if (manager != null)
				{
					awsInstances = await manager.ListInstancesAsync(cts_.Token);
				}
			}
			catch (Exception ex)
			{
				AppDebug.Log.Error($"CloudActivity: AWS一覧取得エラー: {ex.Message}");
			}
		}

		// GCP インスタンス
		List<GcpInstance> gcpInstances = null;
		if (hasGcp && System.IO.File.Exists(Settings.EngineSettings.GcpServiceAccountKeyPath))
		{
			try
			{
				using var gcpTemp = new GcpSpotManager(Settings.EngineSettings.GcpServiceAccountKeyPath);
				gcpInstances = await gcpTemp.ListInstancesAsync(cts_.Token);
			}
			catch (Exception ex)
			{
				AppDebug.Log.Error($"CloudActivity: GCP一覧取得エラー: {ex.Message}");
			}
		}

		RunOnUiThread(() =>
		{
			existingInstancesContainer_.RemoveAllViews();

			if (vastAiCredit.HasValue)
				creditText_.Text = $"vast.ai残高: ${vastAiCredit.Value:F2}";

			bool hasAny = false;

			// AWS インスタンスを先に表示
			if (awsInstances != null && awsInstances.Count > 0)
			{
				foreach (var inst in awsInstances.OrderByDescending(i => i.State == "running"))
				{
					AddAwsInstanceCard(inst);
					hasAny = true;
				}
			}

			// GCP インスタンス
			if (gcpInstances != null && gcpInstances.Count > 0)
			{
				foreach (var inst in gcpInstances.OrderByDescending(i => i.IsRunning))
				{
					AddGcpInstanceCard(inst);
					hasAny = true;
				}
			}

			// vast.ai インスタンス
			if (vastAiInstances != null)
			{
				var shogiInstances = vastAiInstances
					.Where(i => i.IsShogiInstance)
					.OrderByDescending(i => i.IsRunning)
					.ThenByDescending(i => i.IsLoading)
					.ToList();

				var otherInstances = vastAiInstances
					.Where(i => !i.IsShogiInstance)
					.Where(i => i.IsRunning || i.IsStopped)
					.OrderByDescending(i => i.IsRunning)
					.ToList();

				foreach (var inst in shogiInstances)
				{
					AddVastAiInstanceCard(inst, isShogiLabeled: true);
					hasAny = true;
				}

				if (otherInstances.Count > 0)
				{
					var otherLabel = new TextView(this) { Text = "vast.ai: その他のインスタンス" };
					otherLabel.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
					otherLabel.SetTypeface(null, TypefaceStyle.Italic);
					otherLabel.SetTextColor(ColorUtils.Get(this, Resource.Color.vast_card_sub_text));
					var olp = new LinearLayout.LayoutParams(
						LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
					olp.TopMargin = DpToPx(8);
					otherLabel.LayoutParameters = olp;
					existingInstancesContainer_.AddView(otherLabel);

					foreach (var inst in otherInstances)
					{
						AddVastAiInstanceCard(inst, isShogiLabeled: false);
						hasAny = true;
					}
				}
			}

			if (!hasAny)
			{
				var msg = new TextView(this) { Text = "稼働中のインスタンスはありません" };
				msg.SetPadding(0, DpToPx(8), 0, DpToPx(8));
				existingInstancesContainer_.AddView(msg);
			}
			SetBusy(false, "");
		});
	}

	// ===== AWS インスタンスカード =====

	private void AddAwsInstanceCard(AwsInstance inst)
	{
		bool dark = ColorUtils.IsDarkMode(this);
		string bgColor = inst.State == "running"
			? (dark ? "#1B3A1B" : "#E8F5E9")
			: (dark ? "#0D1B3A" : "#E3F2FD");

		var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
		card.SetBackgroundColor(Color.ParseColor(bgColor));
		card.SetPadding(DpToPx(12), DpToPx(8), DpToPx(12), DpToPx(8));
		var cardLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		cardLp.BottomMargin = DpToPx(6);
		card.LayoutParameters = cardLp;

		var titleText = new TextView(this)
		{
			Text = $"[AWS] [{inst.StatusDisplay}] {inst.InstanceType}"
		};
		titleText.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		titleText.SetTypeface(null, TypefaceStyle.Bold);
		card.AddView(titleText);

		// インスタンスタイプからスペック・料金を推定
		string awsSpecs = GetAwsInstanceSpecs(inst.InstanceType);
		var specsText = new TextView(this)
		{
			Text = $"{awsSpecs} | {inst.AvailabilityZone}"
		};
		specsText.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		specsText.SetTextColor(ColorUtils.Get(this, Resource.Color.vast_card_sub_text));
		card.AddView(specsText);

		var idText = new TextView(this) { Text = inst.InstanceId };
		idText.SetTextSize(Android.Util.ComplexUnitType.Sp, 11);
		idText.SetTextColor(ColorUtils.Get(this, Resource.Color.vast_card_sub_text));
		card.AddView(idText);

		if (inst.State == "running" && !string.IsNullOrEmpty(inst.PublicIp))
		{
			var ipText = new TextView(this) { Text = $"SSH: {inst.PublicIp}:22" };
			ipText.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
			ipText.SetTextColor(ColorUtils.Get(this, Resource.Color.vast_card_sub_text));
			card.AddView(ipText);
		}

		// ボタン
		var btnRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
		btnRow.SetGravity(GravityFlags.CenterVertical);
		var btnRowLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		btnRowLp.TopMargin = DpToPx(4);
		btnRow.LayoutParameters = btnRowLp;

		if (inst.State == "running" && !string.IsNullOrEmpty(inst.PublicIp))
		{
			var connectBtn = MakeButton("接続", Resource.Color.vast_btn_green);
			connectBtn.Click += (s, e) => ScanAndSelectEngineAwsAsync(inst);
			btnRow.AddView(connectBtn);
		}

		if (inst.State == "running" || inst.State == "pending")
		{
			var termBtn = MakeButton("終了", Resource.Color.vast_btn_red);
			termBtn.Click += (s, e) => ConfirmTerminateAwsAsync(inst.InstanceId);
			btnRow.AddView(termBtn);
		}

		card.AddView(btnRow);
		existingInstancesContainer_.AddView(card);
	}

	/// <summary>
	/// AWSインスタンスタイプからスペック・推定Spot料金を返す
	/// </summary>
	private static string GetAwsInstanceSpecs(string instanceType)
	{
		// 主要なインスタンスタイプのスペックと推定Spot料金
		return instanceType switch
		{
			"c7a.metal-48xl" => "192vCPU 384GB RAM | Spot ~$2.3/h",
			"c7a.24xlarge"   => "96vCPU 192GB RAM | Spot ~$1.1/h",
			"c7a.16xlarge"   => "64vCPU 128GB RAM | Spot ~$0.8/h",
			"c7a.12xlarge"   => "48vCPU 96GB RAM | Spot ~$0.6/h",
			"c7a.8xlarge"    => "32vCPU 64GB RAM | Spot ~$0.4/h",
			"c7a.4xlarge"    => "16vCPU 32GB RAM | Spot ~$0.2/h",
			"c7i.metal-48xl" => "192vCPU 384GB RAM | Spot ~$2.5/h",
			"c7i.24xlarge"   => "96vCPU 192GB RAM | Spot ~$1.3/h",
			_ => instanceType
		};
	}

	// ===== vast.ai インスタンスカード =====

	private void AddVastAiInstanceCard(VastAiInstance inst, bool isShogiLabeled)
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

		string labelStr = isShogiLabeled ? " [将棋]" : "";
		var titleText = new TextView(this)
		{
			Text = $"[vast.ai] [{inst.StatusDisplay}]{labelStr} ID:{inst.Id}"
		};
		titleText.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		titleText.SetTypeface(null, TypefaceStyle.Bold);
		card.AddView(titleText);

		var specsText = new TextView(this)
		{
			Text = $"{inst.GpuName} x{inst.NumGpus} | {inst.CpuName} {inst.CpuCoresEffective:F0}cores | RAM {inst.CpuRamGb:F0}GB | ${inst.DphTotal:F3}/h"
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

		var btnRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
		btnRow.SetGravity(GravityFlags.CenterVertical);
		var btnRowLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		btnRowLp.TopMargin = DpToPx(4);
		btnRow.LayoutParameters = btnRowLp;

		if (inst.IsRunning && !string.IsNullOrEmpty(inst.PublicIpAddr))
		{
			var connectBtn = MakeButton("接続", Resource.Color.vast_btn_green);
			connectBtn.Click += (s, e) => ScanAndSelectEngineVastAiAsync(inst);
			btnRow.AddView(connectBtn);
		}

		if (inst.IsRunning)
		{
			var toggleBtn = MakeButton("停止", Resource.Color.vast_btn_orange);
			toggleBtn.Click += (s, e) => StopVastAiInstanceAsync(inst.Id);
			btnRow.AddView(toggleBtn);
		}
		else if (inst.IsStopped)
		{
			var toggleBtn = MakeButton("再開", Resource.Color.vast_btn_green);
			toggleBtn.Click += (s, e) => ResumeVastAiInstanceAsync(inst.Id);
			btnRow.AddView(toggleBtn);
		}

		var destroyBtn = MakeButton("削除", Resource.Color.vast_btn_red);
		destroyBtn.Click += (s, e) => ConfirmDestroyVastAiAsync(inst.Id);
		btnRow.AddView(destroyBtn);

		card.AddView(btnRow);
		existingInstancesContainer_.AddView(card);
	}

	// ===== AWS: エンジン検出・接続 =====

	private async void ScanAndSelectEngineAwsAsync(AwsInstance inst)
	{
		string sshKeyPath = Settings.EngineSettings.VastAiSshKeyPath;
		if (string.IsNullOrEmpty(sshKeyPath))
		{
			Toast.MakeText(this, "SSH秘密鍵パスを設定してください", ToastLength.Short).Show();
			return;
		}

		SetBusy(true, "Docker 準備状況を確認中...");

		// user data 完了を確認
		bool ready = false;
		try
		{
			ready = await Task.Run(() => AwsSpotManager.CheckReadyMarkerAsync(inst.PublicIp, sshKeyPath, cts_.Token));
		}
		catch { }

		if (!ready)
		{
			RunOnUiThread(() =>
			{
				SetBusy(false, "");
				Toast.MakeText(this, "Dockerイメージの準備がまだ完了していません。しばらくお待ちください。", ToastLength.Long).Show();
			});
			return;
		}

		RunOnUiThread(() => SetBusy(true, "エンジンを検出中..."));

		(List<EngineInfo> engines, int actualRamMb) result;
		try
		{
			result = await Task.Run(() => ScanEnginesVastAi(inst.PublicIp, 22, sshKeyPath));
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
			if (result.engines.Count == 0)
			{
				Toast.MakeText(this, "エンジンが見つかりません", ToastLength.Long).Show();
				return;
			}
			ShowEngineSelectDialog(result.engines, (selected) => ConnectToAwsInstance(inst, selected.Command, selected.DisplayName, result.actualRamMb));
		});
	}

	private void ConnectToAwsInstance(AwsInstance inst, string engineCommand, string engineType, int actualRamMb = 0)
	{
		string sshKeyPath = Settings.EngineSettings.VastAiSshKeyPath;
		if (string.IsNullOrEmpty(sshKeyPath))
		{
			Toast.MakeText(this, "SSH秘密鍵パスを設定してください", ToastLength.Short).Show();
			return;
		}

		// 実インスタンスタイプからスペックを算出
		string actualType = inst.InstanceType;
		int awsMachineHash = actualType.GetHashCode();
		int prevMachineId = Settings.EngineSettings.VastAiMachineId;
		if (awsMachineHash != prevMachineId)
		{
			Settings.EngineSettings.VastAiOptionsMachineId = 0;
		}

		// インスタンスタイプからvCPU/RAMを推定
		int vCpus = 0;
		int defaultRamMb = 0;
		foreach (var t in AwsInstanceTypes)
		{
			if (t.type == actualType) { vCpus = t.vCpus; defaultRamMb = t.ramGb * 1024; break; }
		}
		if (vCpus == 0 && inst.VCpuCount > 0) vCpus = inst.VCpuCount;

		Settings.EngineSettings.RemoteHost = inst.PublicIp;
		Settings.EngineSettings.VastAiSshPort = 22;
		Settings.EngineSettings.VastAiSshEngineCommand = engineCommand;
		Settings.EngineSettings.EngineNo = RemoteEnginePlayer.RemoteEngineNo;
		Settings.EngineSettings.EngineName = $"{engineType} (AWS {inst.AvailabilityZone})";
		Settings.EngineSettings.AwsInstanceId = inst.InstanceId;
		Settings.EngineSettings.AwsInstanceType = actualType;
		Settings.EngineSettings.VastAiMachineId = awsMachineHash;
		Settings.EngineSettings.CloudProvider = "aws";
		Settings.EngineSettings.VastAiCpuCores = vCpus;
		Settings.EngineSettings.VastAiRamMb = actualRamMb > 0 ? actualRamMb : defaultRamMb;
		Settings.EngineSettings.VastAiGpuRamMb = 0;
		Settings.Save();

		var resultIntent = new Intent();
		resultIntent.PutExtra(ExtraHost, inst.PublicIp);
		SetResult(Result.Ok, resultIntent);
		Finish();
	}

	// ===== AWS: スポット価格取得 =====

	private async void LoadSpotPricesAsync()
	{
		var manager = GetAwsManager();
		if (manager == null) return;

		SetBusy(true, "スポット価格を取得中...");
		awsSpotPriceContainer_.RemoveAllViews();

		try
		{
			var prices = await manager.GetSpotPricesAsync(
				Settings.EngineSettings.AwsInstanceType, cts_.Token);

			RunOnUiThread(() =>
			{
				awsSpotPriceContainer_.RemoveAllViews();

				if (prices.Count == 0)
				{
					var msg = new TextView(this) { Text = "価格情報が取得できませんでした" };
					awsSpotPriceContainer_.AddView(msg);
					SetBusy(false, "");
					return;
				}

				foreach (var price in prices)
				{
					var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
					row.SetGravity(GravityFlags.CenterVertical);
					row.SetPadding(0, DpToPx(4), 0, DpToPx(4));

					var priceText = new TextView(this)
					{
						Text = $"{price.AvailabilityZone}: ${price.Price:F4}/h"
					};
					priceText.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
					priceText.LayoutParameters = new LinearLayout.LayoutParams(
						0, LinearLayout.LayoutParams.WrapContent, 1f);
					row.AddView(priceText);

					var launchBtn = new Button(this) { Text = "起動" };
					launchBtn.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
					string az = price.AvailabilityZone;
					launchBtn.Click += (s, e) => LaunchAwsSpotAsync(az);
					row.AddView(launchBtn);

					awsSpotPriceContainer_.AddView(row);
				}
				SetBusy(false, $"{prices.Count}件のAZで利用可能");
			});
		}
		catch (Exception ex)
		{
			RunOnUiThread(() =>
			{
				SetBusy(false, $"価格取得エラー: {ex.Message}");
				Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
			});
		}
	}

	// ===== AWS: スポットインスタンス起動 =====

	private async void LaunchAwsSpotAsync(string availabilityZone)
	{
		var manager = GetAwsManager();
		if (manager == null) return;

		SetBusy(true, "リソースを準備中...");

		try
		{
			// SSH公開鍵パスを秘密鍵パスから推定
			string sshKeyPath = Settings.EngineSettings.VastAiSshKeyPath;
			if (string.IsNullOrEmpty(sshKeyPath))
			{
				RunOnUiThread(() =>
				{
					SetBusy(false, "");
					Toast.MakeText(this, "SSH秘密鍵パスを設定してください", ToastLength.Short).Show();
				});
				return;
			}

			// キーペアとセキュリティグループを確保
			RunOnUiThread(() => statusText_.Text = "キーペアを確認中...");
			string keyPairName = await manager.EnsureKeyPairAsync(sshKeyPath + ".pub", cts_.Token);
			Settings.EngineSettings.AwsKeyPairName = keyPairName;

			RunOnUiThread(() => statusText_.Text = "セキュリティグループを確認中...");
			string sgId = await manager.EnsureSecurityGroupAsync(cts_.Token);
			Settings.EngineSettings.AwsSecurityGroupId = sgId;

			// AMI取得（カスタムAMIがあればDocker プリインストール済み）
			RunOnUiThread(() => statusText_.Text = "AMIを取得中...");
			var (amiId, isCustomAmi) = await manager.GetBestAmiAsync(cts_.Token);

			// インスタンス起動
			string amiLabel = isCustomAmi ? "カスタムAMI" : "ベースAMI";
			RunOnUiThread(() => statusText_.Text = $"スポットインスタンスを起動中 ({amiLabel}, {availabilityZone})...");
			var config = new AwsLaunchConfig
			{
				AmiId = amiId,
				InstanceType = Settings.EngineSettings.AwsInstanceType,
				KeyPairName = keyPairName,
				SecurityGroupId = sgId,
				AvailabilityZone = availabilityZone,
				DockerImage = Settings.EngineSettings.AwsDockerImage,
				IsCustomAmi = isCustomAmi
			};

			string instanceId = await manager.LaunchSpotInstanceAsync(config, cts_.Token);
			Settings.EngineSettings.AwsInstanceId = instanceId;
			Settings.EngineSettings.AwsAvailabilityZone = availabilityZone;
			Settings.Save();

			// SSH到達を待機
			RunOnUiThread(() => statusText_.Text = "SSH到達を待機中...");
			var runningInst = await manager.WaitForSshReadyAsync(instanceId, ct: cts_.Token);

			// Docker準備完了を待機（user-data の docker pull + コンテナ起動）
			RunOnUiThread(() => statusText_.Text = "Docker準備完了を待機中...");
			for (int i = 0; i < 120; i++)
			{
				cts_.Token.ThrowIfCancellationRequested();
				if (await AwsSpotManager.CheckReadyMarkerAsync(runningInst.PublicIp, sshKeyPath, cts_.Token))
					break;
				if (i == 119)
					throw new TimeoutException("Docker準備完了がタイムアウトしました");
				await Task.Delay(5000, cts_.Token);
			}

			string statusMsg = "起動完了";
			RunOnUiThread(() =>
			{
				SetBusy(false, statusMsg);
				LoadAllInstancesAsync();
			});
		}
		catch (System.OperationCanceledException)
		{
			RunOnUiThread(() => SetBusy(false, "キャンセルされました"));
		}
		catch (Exception ex)
		{
			AppDebug.Log.ErrorException(ex, $"CloudActivity: AWSスポット起動エラー");
			RunOnUiThread(() =>
			{
				SetBusy(false, $"起動エラー: {ex.Message}");
				Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
			});
		}
	}

	// ===== AWS: インスタンス終了 =====

	private void ConfirmTerminateAwsAsync(string instanceId)
	{
		new AlertDialog.Builder(this)
			.SetTitle("AWS インスタンス終了")
			.SetMessage($"インスタンス {instanceId} を終了しますか？\n課金が停止します。")
			.SetPositiveButton("終了", async (s, e) =>
			{
				var manager = GetAwsManager();
				if (manager == null) return;

				SetBusy(true, "終了中...");
				try
				{
					await manager.TerminateInstanceAsync(instanceId, cts_.Token);

					if (Settings.EngineSettings.AwsInstanceId == instanceId)
					{
						Settings.EngineSettings.AwsInstanceId = string.Empty;
						Settings.Save();
					}

					RunOnUiThread(() =>
					{
						SetBusy(false, "終了しました");
						LoadAllInstancesAsync();
					});
				}
				catch (Exception ex)
				{
					RunOnUiThread(() =>
					{
						SetBusy(false, $"終了エラー: {ex.Message}");
						Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
					});
				}
			})
			.SetNegativeButton("キャンセル", (s, e) => { })
			.Show();
	}

	/// <summary>
	/// GCPマシンタイプからスペック・推定Spot料金を返す
	/// </summary>
	private static string GetGcpInstanceSpecs(string machineType)
	{
		// マシンタイプからvCPU数を抽出
		int vCpus = 0;
		var parts = machineType.Split('-');
		if (parts.Length >= 3) int.TryParse(parts[parts.Length - 1], out vCpus);

		string family = parts.Length >= 1 ? parts[0] : "";
		string tier = parts.Length >= 2 ? parts[1] : "";

		// メモリ推定 (highcpu=2GB, standard=4GB, highmem=8GB per vCPU)
		int ramGb = tier switch
		{
			"highcpu" => vCpus * 2,
			"highmem" => vCpus * 8,
			_ => vCpus * 4
		};

		// Spot推定料金 ($/vCPU/h, リージョンにより変動)
		double pricePerVcpu = (family, tier) switch
		{
			("c3d", "highcpu") => 0.006,
			("c3d", _)         => 0.008,
			("c3", "highcpu")  => 0.007,
			("c3", _)          => 0.009,
			("c2d", "highcpu") => 0.009,
			("c2d", _)         => 0.011,
			("c2", _)          => 0.010,
			_                  => 0.010
		};
		double price = vCpus * pricePerVcpu;

		if (vCpus > 0)
			return $"{vCpus}vCPU {ramGb}GB RAM | Spot ~${price:F2}/h";
		return machineType;
	}

	// ===== GCP: インスタンスカード =====

	private void AddGcpInstanceCard(GcpInstance inst)
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

		string machineShort = inst.MachineType?.Split('/')?.LastOrDefault() ?? "";
		var titleText = new TextView(this)
		{
			Text = $"[GCP] [{inst.StatusDisplay}] {machineShort}"
		};
		titleText.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		titleText.SetTypeface(null, TypefaceStyle.Bold);
		card.AddView(titleText);

		// マシンタイプからスペック・料金を推定
		string gcpSpecs = GetGcpInstanceSpecs(machineShort);
		var specsText = new TextView(this)
		{
			Text = $"{gcpSpecs} | {inst.Zone}"
		};
		specsText.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
		specsText.SetTextColor(ColorUtils.Get(this, Resource.Color.vast_card_sub_text));
		card.AddView(specsText);

		var nameText = new TextView(this) { Text = inst.Name };
		nameText.SetTextSize(Android.Util.ComplexUnitType.Sp, 11);
		nameText.SetTextColor(ColorUtils.Get(this, Resource.Color.vast_card_sub_text));
		card.AddView(nameText);

		if (inst.IsRunning && !string.IsNullOrEmpty(inst.ExternalIp))
		{
			var ipText = new TextView(this) { Text = $"SSH: {inst.ExternalIp}:22" };
			ipText.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
			ipText.SetTextColor(ColorUtils.Get(this, Resource.Color.vast_card_sub_text));
			card.AddView(ipText);
		}

		var btnRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
		btnRow.SetGravity(GravityFlags.CenterVertical);
		var btnRowLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		btnRowLp.TopMargin = DpToPx(4);
		btnRow.LayoutParameters = btnRowLp;

		if (inst.IsRunning && !string.IsNullOrEmpty(inst.ExternalIp))
		{
			var connectBtn = MakeButton("接続", Resource.Color.vast_btn_green);
			connectBtn.Click += (s, e) => ScanAndSelectEngineGcpAsync(inst);
			btnRow.AddView(connectBtn);
		}

		if (inst.IsRunning)
		{
			var stopBtn = MakeButton("停止", Resource.Color.vast_btn_orange);
			stopBtn.Click += async (s, e) =>
			{
				var manager = GetGcpManager();
				if (manager == null) return;
				SetBusy(true, "停止中...");
				try
				{
					await manager.StopInstanceAsync(inst.Zone, inst.Name, cts_.Token);
					RunOnUiThread(() => { SetBusy(false, "停止しました"); LoadAllInstancesAsync(); });
				}
				catch (Exception ex)
				{
					RunOnUiThread(() => { SetBusy(false, $"停止エラー: {ex.Message}"); });
				}
			};
			btnRow.AddView(stopBtn);
		}
		else if (inst.IsStopped)
		{
			var startBtn = MakeButton("再開", Resource.Color.vast_btn_green);
			startBtn.Click += async (s, e) =>
			{
				var manager = GetGcpManager();
				if (manager == null) return;
				SetBusy(true, "再開中...");
				try
				{
					await manager.StartInstanceAsync(inst.Zone, inst.Name, cts_.Token);
					RunOnUiThread(() => { SetBusy(false, "再開しました"); LoadAllInstancesAsync(); });
				}
				catch (Exception ex)
				{
					RunOnUiThread(() => { SetBusy(false, $"再開エラー: {ex.Message}"); });
				}
			};
			btnRow.AddView(startBtn);
		}

		var deleteBtn = MakeButton("削除", Resource.Color.vast_btn_red);
		deleteBtn.Click += (s, e) => ConfirmDeleteGcpAsync(inst.Zone, inst.Name);
		btnRow.AddView(deleteBtn);

		card.AddView(btnRow);
		existingInstancesContainer_.AddView(card);
	}

	// ===== GCP: エンジン検出・接続 =====

	private async void ScanAndSelectEngineGcpAsync(GcpInstance inst)
	{
		string sshKeyPath = Settings.EngineSettings.VastAiSshKeyPath;
		if (string.IsNullOrEmpty(sshKeyPath))
		{
			Toast.MakeText(this, "SSH秘密鍵パスを設定してください", ToastLength.Short).Show();
			return;
		}

		SetBusy(true, "Docker 準備状況を確認中...");

		bool ready = false;
		try
		{
			ready = await Task.Run(() => GcpSpotManager.CheckReadyMarkerAsync(inst.ExternalIp, sshKeyPath, cts_.Token));
		}
		catch { }

		if (!ready)
		{
			RunOnUiThread(() =>
			{
				SetBusy(false, "");
				Toast.MakeText(this, "Dockerイメージの準備がまだ完了していません。しばらくお待ちください。", ToastLength.Long).Show();
			});
			return;
		}

		RunOnUiThread(() => SetBusy(true, "エンジンを検出中..."));

		(List<EngineInfo> engines, int actualRamMb) result;
		try
		{
			result = await Task.Run(() => ScanEnginesVastAi(inst.ExternalIp, 22, sshKeyPath));
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
			if (result.engines.Count == 0)
			{
				Toast.MakeText(this, "エンジンが見つかりません", ToastLength.Long).Show();
				return;
			}
			ShowEngineSelectDialog(result.engines, (selected) => ConnectToGcpInstance(inst, selected.Command, selected.DisplayName, result.actualRamMb));
		});
	}

	private void ConnectToGcpInstance(GcpInstance inst, string engineCommand, string engineType, int actualRamMb = 0)
	{
		string sshKeyPath = Settings.EngineSettings.VastAiSshKeyPath;
		if (string.IsNullOrEmpty(sshKeyPath))
		{
			Toast.MakeText(this, "SSH秘密鍵パスを設定してください", ToastLength.Short).Show();
			return;
		}

		// マシンタイプでスペックを識別
		string machineShort = inst.MachineType?.Split('/')?.LastOrDefault() ?? "";
		int gcpMachineHash = machineShort.GetHashCode();
		int prevMachineId = Settings.EngineSettings.VastAiMachineId;
		if (gcpMachineHash != prevMachineId)
		{
			Settings.EngineSettings.VastAiOptionsMachineId = 0;
		}

		// マシンタイプからvCPU数を推定（例: c3d-highcpu-180 → 180）
		int vCpus = 0;
		var parts = machineShort.Split('-');
		if (parts.Length > 0) int.TryParse(parts[parts.Length - 1], out vCpus);

		Settings.EngineSettings.RemoteHost = inst.ExternalIp;
		Settings.EngineSettings.VastAiSshPort = 22;
		Settings.EngineSettings.VastAiSshEngineCommand = engineCommand;
		Settings.EngineSettings.EngineNo = RemoteEnginePlayer.RemoteEngineNo;
		Settings.EngineSettings.EngineName = $"{engineType} (GCP {inst.Zone})";
		Settings.EngineSettings.GcpInstanceName = inst.Name;
		Settings.EngineSettings.VastAiMachineId = gcpMachineHash;
		Settings.EngineSettings.CloudProvider = "gcp";
		Settings.EngineSettings.VastAiCpuCores = vCpus > 0 ? vCpus : 180;
		Settings.EngineSettings.VastAiRamMb = actualRamMb > 0 ? actualRamMb : vCpus * 2 * 1024; // highcpu: 2GB/vCPU
		Settings.EngineSettings.VastAiGpuRamMb = 0;
		Settings.Save();

		var resultIntent = new Intent();
		resultIntent.PutExtra(ExtraHost, inst.ExternalIp);
		SetResult(Result.Ok, resultIntent);
		Finish();
	}

	// ===== GCP: Spot VM 起動 =====

	/// <summary>
	/// マシンスペックと料金を表示して確認後に起動
	/// </summary>
	private void ConfirmAndLaunchGcpSpotAsync()
	{
		int pos = gcpMachineTypeSpinner_.SelectedItemPosition;
		if (pos < 0 || pos >= GcpMachineTypes.Length) return;
		var selected = GcpMachineTypes[pos];
		string zone = gcpZoneEdit_.Text?.Trim() ?? "";

		// Spinner の表示テキストから価格を取得（動的取得済みなら含まれている）
		string spinnerText = gcpMachineTypeSpinner_.SelectedItem?.ToString() ?? "";
		string priceStr = spinnerText.Contains("$") ? spinnerText.Substring(spinnerText.LastIndexOf('$')) : $"~${selected.estSpot:F2}/h";

		string cpuArch = selected.type.StartsWith("c3d") ? "AMD EPYC Genoa (Zen 4)"
			: selected.type.StartsWith("c4d") ? "AMD EPYC Turin (Zen 5)"
			: selected.type.StartsWith("c3-") ? "Intel Sapphire Rapids"
			: "";

		string message =
			$"マシンタイプ: {selected.type}\n" +
			$"CPU: {cpuArch}\n" +
			$"vCPU: {selected.vCpus}コア\n" +
			$"メモリ: {selected.ramGb} GB\n" +
			$"ディスク: 10 GB\n" +
			$"ゾーン: {zone}\n" +
			$"\n" +
			$"Spot 料金: {priceStr}\n" +
			$"\n" +
			$"60分後に自動シャットダウンします。";

		new AlertDialog.Builder(this)
			.SetTitle("GCP Spot VM を起動しますか？")
			.SetMessage(message)
			.SetPositiveButton("起動", (s, e) => LaunchGcpSpotAsync())
			.SetNegativeButton("キャンセル", (s, e) => { })
			.Show();
	}

	private async void LaunchGcpSpotAsync()
	{
		var manager = GetGcpManager();
		if (manager == null) return;

		string sshKeyPath = Settings.EngineSettings.VastAiSshKeyPath;
		if (string.IsNullOrEmpty(sshKeyPath))
		{
			Toast.MakeText(this, "SSH秘密鍵パスを設定してください", ToastLength.Short).Show();
			return;
		}

		SetBusy(true, "リソースを準備中...");

		try
		{
			// SSH公開鍵を読み込み
			string pubKeyPath = sshKeyPath + ".pub";
			if (!System.IO.File.Exists(pubKeyPath))
				throw new System.IO.FileNotFoundException($"SSH公開鍵が見つかりません: {pubKeyPath}");
			string pubKeyContent = System.IO.File.ReadAllText(pubKeyPath).Trim();

			// ファイアウォールルールを確保
			RunOnUiThread(() => statusText_.Text = "ファイアウォールルールを確認中...");
			await manager.EnsureFirewallRuleAsync(cts_.Token);

			// Spot VM を起動
			string zone = Settings.EngineSettings.GcpZone;
			string machineType = Settings.EngineSettings.GcpMachineType;
			RunOnUiThread(() => statusText_.Text = $"Spot VM を起動中 ({machineType}, {zone})...");

			var config = new GcpLaunchConfig
			{
				Zone = zone,
				MachineType = machineType,
				DockerImage = Settings.EngineSettings.GcpDockerImage,
				SshPublicKeyContent = pubKeyContent,
				AutoShutdownMinutes = 60
			};

			string instanceName = await manager.CreateSpotInstanceAsync(config, cts_.Token);
			Settings.EngineSettings.GcpInstanceName = instanceName;
			Settings.EngineSettings.GcpZone = zone;
			Settings.Save();

			// running 待ち
			RunOnUiThread(() => statusText_.Text = "インスタンスの起動を待機中...");
			await manager.WaitForRunningAsync(zone, instanceName, ct: cts_.Token);

			// SSH到達を待機
			RunOnUiThread(() => statusText_.Text = "セットアップ完了を待機中...");
			await manager.WaitForSshReadyAsync(zone, instanceName, sshKeyPath, ct: cts_.Token);

			RunOnUiThread(() =>
			{
				SetBusy(false, "起動完了");
				LoadAllInstancesAsync();
			});
		}
		catch (System.OperationCanceledException)
		{
			RunOnUiThread(() => SetBusy(false, "キャンセルされました"));
		}
		catch (Exception ex)
		{
			AppDebug.Log.ErrorException(ex, "CloudActivity: GCP Spot起動エラー");
			RunOnUiThread(() =>
			{
				SetBusy(false, $"起動エラー: {ex.Message}");
				Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
			});
		}
	}

	// ===== GCP: インスタンス削除 =====

	private void ConfirmDeleteGcpAsync(string zone, string instanceName)
	{
		new AlertDialog.Builder(this)
			.SetTitle("GCP インスタンス削除")
			.SetMessage($"インスタンス {instanceName} を削除しますか？\n課金が停止します。")
			.SetPositiveButton("削除", async (s, e) =>
			{
				var manager = GetGcpManager();
				if (manager == null) return;

				SetBusy(true, "削除中...");
				try
				{
					await manager.DeleteInstanceAsync(zone, instanceName, cts_.Token);

					if (Settings.EngineSettings.GcpInstanceName == instanceName)
					{
						Settings.EngineSettings.GcpInstanceName = string.Empty;
						Settings.Save();
					}

					RunOnUiThread(() =>
					{
						SetBusy(false, "削除しました");
						LoadAllInstancesAsync();
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

	// ===== vast.ai: エンジン検出・接続 =====

	private async void ScanAndSelectEngineVastAiAsync(VastAiInstance inst)
	{
		string sshKeyPath = Settings.EngineSettings.VastAiSshKeyPath;
		if (string.IsNullOrEmpty(sshKeyPath))
		{
			Toast.MakeText(this, "SSH秘密鍵パスを設定してください", ToastLength.Short).Show();
			return;
		}

		var (sshHost, sshPort) = inst.GetSshEndpoint();
		SetBusy(true, "エンジンを検出中...");

		(List<EngineInfo> engines, int actualRamMb) result;
		try
		{
			result = await Task.Run(() => ScanEnginesVastAi(sshHost, sshPort, sshKeyPath));
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
			if (result.engines.Count == 0)
			{
				Toast.MakeText(this, "/workspace にエンジンが見つかりません", ToastLength.Long).Show();
				return;
			}
			ShowEngineSelectDialog(result.engines, (selected) => ConnectToVastAiInstance(inst, selected.Command, selected.DisplayName, result.actualRamMb));
		});
	}

	/// <summary>
	/// vast.ai: コンテナ内に直接SSHしてエンジンを検出
	/// </summary>
	private (List<EngineInfo> engines, int actualRamMb) ScanEnginesVastAi(string host, int sshPort, string keyPath)
	{
		var results = new List<EngineInfo>();
		using var keyFile = new PrivateKeyFile(keyPath);
		using var client = new SshClient(host, sshPort, "root", keyFile);
		client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(15);
		client.Connect();

		var cmd = client.RunCommand("find /workspace -maxdepth 2 -type f -executable 2>/dev/null | head -50");
		string output = cmd.Result ?? "";

		foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
		{
			string path = line.Trim();
			if (string.IsNullOrEmpty(path)) continue;

			string fileName = System.IO.Path.GetFileName(path);
			if (IsIgnoredFile(fileName)) continue;

			string dir = System.IO.Path.GetDirectoryName(path);
			string dirName = System.IO.Path.GetFileName(dir);
			results.Add(new EngineInfo
			{
				DisplayName = $"{dirName}/{fileName}",
				Command = $"cd {dir} && exec ./{fileName}",
				Path = path
			});
		}

		// 実際に利用可能なRAMを取得
		int actualRamMb = QueryActualRamMb(client);

		client.Disconnect();
		return (results, actualRamMb);
	}

	/// <summary>
	/// SSH接続経由で実際に利用可能なRAMをMB単位で取得する。
	/// cgroupメモリ制限と物理メモリの小さい方を返す。
	/// </summary>
	private int QueryActualRamMb(SshClient client)
	{
		int ramMb = 0;
		try
		{
			var freeCmd = client.RunCommand("free -m | awk '/^Mem:/{print $2}'");
			int physicalRamMb = 0;
			int.TryParse(freeCmd.Result?.Trim(), out physicalRamMb);

			var cgroupCmd = client.RunCommand(
				"cat /sys/fs/cgroup/memory.max 2>/dev/null || " +
				"cat /sys/fs/cgroup/memory/memory.limit_in_bytes 2>/dev/null");
			string cgroupStr = cgroupCmd.Result?.Trim() ?? "";
			long cgroupBytes = 0;
			int cgroupRamMb = int.MaxValue;
			if (cgroupStr != "max" && long.TryParse(cgroupStr, out cgroupBytes) && cgroupBytes > 0)
			{
				cgroupRamMb = (int)(cgroupBytes / (1024L * 1024L));
			}

			if (physicalRamMb > 0)
			{
				ramMb = System.Math.Min(physicalRamMb, cgroupRamMb);
			}

			AppDebug.Log.Info($"Cloud RAM検出: physical={physicalRamMb}MB, cgroup={cgroupRamMb}MB, actual={ramMb}MB");
		}
		catch (Exception ex)
		{
			AppDebug.Log.Info($"Cloud RAM検出失敗: {ex.Message}");
		}
		return ramMb;
	}

	private void ConnectToVastAiInstance(VastAiInstance inst, string engineCommand, string engineType, int actualRamMb = 0)
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

		// マシンが変わった場合、保存済みオプションのマシンIDをリセット
		int prevMachineId = Settings.EngineSettings.VastAiMachineId;
		if (inst.MachineId > 0 && inst.MachineId != prevMachineId)
		{
			Settings.EngineSettings.VastAiOptionsMachineId = 0;
		}

		Settings.EngineSettings.RemoteHost = sshHost;
		Settings.EngineSettings.VastAiSshPort = sshPort;
		Settings.EngineSettings.VastAiSshEngineCommand = engineCommand;
		Settings.EngineSettings.EngineNo = RemoteEnginePlayer.RemoteEngineNo;
		Settings.EngineSettings.EngineName = $"{engineType} (vast.ai #{inst.Id})";
		Settings.EngineSettings.VastAiInstanceId = inst.Id;
		Settings.EngineSettings.VastAiMachineId = inst.MachineId;
		Settings.EngineSettings.CloudProvider = "vastai";
		Settings.EngineSettings.VastAiCpuCores = (int)inst.CpuCoresEffective;
		Settings.EngineSettings.VastAiRamMb = actualRamMb > 0 ? actualRamMb : (int)(inst.CpuRamGb * 1024);
		Settings.EngineSettings.VastAiGpuRamMb = (int)(inst.GpuRamGb * 1024);
		Settings.Save();

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

	// ===== vast.ai: インスタンス操作 =====

	private async void StopVastAiInstanceAsync(int instanceId)
	{
		var manager = GetVastAiManager();
		if (manager == null) return;

		SetBusy(true, $"インスタンス {instanceId} を停止中...");
		try
		{
			await manager.StopInstanceAsync(instanceId, cts_.Token);
			VastAiWatchdog.Instance.StopMonitoring();
			await Task.Delay(3000, cts_.Token);
			RunOnUiThread(() =>
			{
				SetBusy(false, "休止しました");
				LoadAllInstancesAsync();
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

	private async void ResumeVastAiInstanceAsync(int instanceId)
	{
		var manager = GetVastAiManager();
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
				LoadAllInstancesAsync();
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

	private void ConfirmDestroyVastAiAsync(int instanceId)
	{
		new AlertDialog.Builder(this)
			.SetTitle("インスタンス削除")
			.SetMessage($"インスタンス {instanceId} を削除しますか？\nデータは失われます。")
			.SetPositiveButton("削除", async (s, e) =>
			{
				var manager = GetVastAiManager();
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
						LoadAllInstancesAsync();
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

	// ===== vast.ai: オファー検索 =====

	private async void SearchOffersAsync()
	{
		var manager = GetVastAiManager();
		if (manager == null) return;

		SetBusy(true, "オファーを検索中...");
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

		var existingMore = offerListContainer_.FindViewWithTag("more_button");
		if (existingMore != null) offerListContainer_.RemoveView(existingMore);

		int end = Math.Min(displayedOfferCount_ + OffersPerPage, currentOffers_.Count);
		for (int i = displayedOfferCount_; i < end; i++)
			AddOfferCard(currentOffers_[i]);
		displayedOfferCount_ = end;

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
		startBtn.Click += (s, e) => CreateAndWaitVastAiAsync(offer);
		row.AddView(startBtn);

		card.AddView(row);
		offerListContainer_.AddView(card);
	}

	private async void CreateAndWaitVastAiAsync(VastAiOffer offer)
	{
		var manager = GetVastAiManager();
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
				LoadAllInstancesAsync();
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

	// ===== 共通ヘルパー =====

	private class EngineInfo
	{
		public string DisplayName;
		public string Command;
		public string Path;
	}

	private static bool IsIgnoredFile(string fileName)
	{
		return fileName.StartsWith(".") || fileName == "python" || fileName == "python3" ||
			fileName == "pip" || fileName == "pip3" || fileName == "bash" || fileName == "sh" ||
			fileName.EndsWith(".sh") || fileName.EndsWith(".py");
	}

	private void ShowEngineSelectDialog(List<EngineInfo> engines, Action<EngineInfo> onSelected)
	{
		string[] items = engines.Select(e => e.DisplayName).ToArray();
		new AlertDialog.Builder(this)
			.SetTitle("エンジンを選択")
			.SetItems(items, (s, e) => onSelected(engines[e.Which]))
			.SetNegativeButton("キャンセル", (s, e) => { })
			.Show();
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

	private Button MakeFoldableHeader(string text)
	{
		var header = new Button(this);
		header.Text = text;
		header.SetTextSize(Android.Util.ComplexUnitType.Sp, 15);
		header.Gravity = GravityFlags.Left | GravityFlags.CenterVertical;
		header.SetBackgroundColor(Android.Graphics.Color.Transparent);
		header.SetTextColor(ColorUtils.Get(this, Resource.Color.title_background));
		header.SetTypeface(null, TypefaceStyle.Bold);
		var lp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		lp.TopMargin = DpToPx(16);
		header.LayoutParameters = lp;
		return header;
	}

	private void ToggleFoldable(Button header, LinearLayout content, string title)
	{
		if (content.Visibility == ViewStates.Gone)
		{
			content.Visibility = ViewStates.Visible;
			header.Text = $"▼ {title}";
		}
		else
		{
			content.Visibility = ViewStates.Gone;
			header.Text = $"▶ {title}";
		}
	}

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

	private static string CountryCodeToFlag(string countryCode)
	{
		if (string.IsNullOrEmpty(countryCode) || countryCode.Length < 2)
			return "";
		countryCode = countryCode.Trim().ToUpperInvariant();
		if (countryCode.Length < 2)
			return "";
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
				var uri = data.Data;
				string destDir = System.IO.Path.Combine(FilesDir.AbsolutePath, "ssh");
				System.IO.Directory.CreateDirectory(destDir);

				// 元のファイル名を取得（例: id_ed25519）
				string originalName = GetFileNameFromUri(uri) ?? "id_rsa";
				// .pub が選ばれた場合は秘密鍵名に戻す
				if (originalName.EndsWith(".pub"))
					originalName = originalName.Substring(0, originalName.Length - 4);

				string destPath = System.IO.Path.Combine(destDir, originalName);

				using (var input = ContentResolver.OpenInputStream(uri))
				using (var output = new System.IO.FileStream(destPath, System.IO.FileMode.Create))
				{
					input.CopyTo(output);
				}

				var file = new Java.IO.File(destPath);
				file.SetReadable(false, false);
				file.SetReadable(true, true);
				file.SetWritable(false, false);

				// 同じディレクトリにある .pub ファイルも探してコピー
				string pubDestPath = destPath + ".pub";
				bool pubCopied = false;
				try
				{
					// 元のURIのパスから .pub を推定して探す
					string uriPath = uri.Path;
					if (!string.IsNullOrEmpty(uriPath))
					{
						// SAF経由のパスから実際のファイルパスを推定
						// /document/primary:xxx/id_ed25519 → /sdcard/xxx/id_ed25519.pub
						string[] searchPaths = {
							uriPath + ".pub",
							"/sdcard/.ssh/" + originalName + ".pub",
							"/storage/emulated/0/.ssh/" + originalName + ".pub",
						};
						foreach (var pubPath in searchPaths)
						{
							if (System.IO.File.Exists(pubPath))
							{
								System.IO.File.Copy(pubPath, pubDestPath, true);
								pubCopied = true;
								AppDebug.Log.Info($"SSH公開鍵をコピー: {pubPath} → {pubDestPath}");
								break;
							}
						}
					}
				}
				catch (Exception pubEx)
				{
					AppDebug.Log.Info($"SSH公開鍵のコピーに失敗: {pubEx.Message}");
				}

				sshKeyPathEdit_.Text = destPath;
				// パスを即座に永続化（LoadSettings で上書きされるのを防ぐ）
				Settings.EngineSettings.VastAiSshKeyPath = destPath;
				Settings.Save();

				string msg = pubCopied
					? "SSH秘密鍵と公開鍵をインポートしました"
					: "SSH秘密鍵をインポートしました（公開鍵は手動で配置してください: " + pubDestPath + "）";
				Toast.MakeText(this, msg, pubCopied ? ToastLength.Short : ToastLength.Long).Show();
			}
			catch (Exception ex)
			{
				Toast.MakeText(this, $"秘密鍵の読み込みに失敗: {ex.Message}", ToastLength.Long).Show();
			}
		}
	}

	private string GetFileNameFromUri(Android.Net.Uri uri)
	{
		string name = null;
		if (uri.Scheme == "content")
		{
			using var cursor = ContentResolver.Query(uri, null, null, null, null);
			if (cursor != null && cursor.MoveToFirst())
			{
				int idx = cursor.GetColumnIndex(Android.Provider.OpenableColumns.DisplayName);
				if (idx >= 0) name = cursor.GetString(idx);
			}
		}
		if (string.IsNullOrEmpty(name))
			name = System.IO.Path.GetFileName(uri.Path);
		return name;
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
