using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using ShogiGUI;

namespace ShogiDroid;

/// <summary>
/// SHOGI-EXTEND API から将棋ウォーズの棋譜一覧を取得し、
/// ネイティブリストで表示する。棋譜タップでアプリに読み込む。
/// </summary>
[Activity(Label = "将棋ウォーズ棋譜一覧", Theme = "@style/AppTheme")]
public class ShogiWarsActivity : ThemedActivity
{
	private const string API_BASE = "https://www.shogi-extend.com/w.json";
	private const string WARS_GAME_URL = "https://shogiwars.heroz.jp/games/";
	private const int PER_PAGE = 20;
	public const string ExtraKifu = "kifu";

	private LinearLayout listContainer_;
	private ProgressBar progressBar_;
	private TextView statusText_;
	private Button loadMoreBtn_;
	private string username_;
	private int currentPage_ = 1;
	private int totalRecords_ = 0;
	private CancellationTokenSource cts_ = new CancellationTokenSource();

	protected override void OnCreate(Bundle savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		BuildUI();
		username_ = Settings.AppSettings.WarsUserName;
		if (!string.IsNullOrEmpty(username_))
		{
			LoadPageAsync(1);
		}
	}

	protected override void OnDestroy()
	{
		cts_?.Cancel();
		cts_?.Dispose();
		base.OnDestroy();
	}

	private void BuildUI()
	{
		var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
		root.SetFitsSystemWindows(true);

		// タイトルバー
		var titleBar = new LinearLayout(this) { Orientation = Orientation.Horizontal };
		titleBar.SetGravity(GravityFlags.CenterVertical);
		titleBar.SetPadding(DpToPx(4), DpToPx(4), DpToPx(4), DpToPx(4));
		titleBar.SetBackgroundColor(GetColorFromAttr(Resource.Attribute.colorPrimary));

		var closeBtn = new ImageButton(this);
		closeBtn.SetImageResource(Android.Resource.Drawable.IcMenuCloseClearCancel);
		// 属性をテーマ経由で解決してから背景に設定
		var outValue = new Android.Util.TypedValue();
		Theme.ResolveAttribute(Android.Resource.Attribute.SelectableItemBackgroundBorderless, outValue, true);
		closeBtn.SetBackgroundResource(outValue.ResourceId);
		closeBtn.LayoutParameters = new LinearLayout.LayoutParams(DpToPx(48), DpToPx(48));
		closeBtn.Click += (s, e) => { SetResult(Result.Canceled); Finish(); };
		titleBar.AddView(closeBtn);

		var titleText = new TextView(this)
		{
			Text = $"将棋ウォーズ棋譜一覧 - {Settings.AppSettings.WarsUserName}",
		};
		titleText.SetTextColor(Color.White);
		titleText.SetTextSize(Android.Util.ComplexUnitType.Sp, 18);
		titleText.SetPadding(DpToPx(8), 0, 0, 0);
		titleText.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
		titleBar.AddView(titleText);

		root.AddView(titleBar);

		// プログレスバー
		progressBar_ = new ProgressBar(this, null, Android.Resource.Attribute.ProgressBarStyleHorizontal);
		progressBar_.Indeterminate = true;
		progressBar_.Visibility = ViewStates.Gone;
		progressBar_.LayoutParameters = new LinearLayout.LayoutParams(
			ViewGroup.LayoutParams.MatchParent, DpToPx(4));
		root.AddView(progressBar_);

		// ステータス
		statusText_ = new TextView(this) { Text = "" };
		statusText_.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
		statusText_.SetPadding(DpToPx(16), DpToPx(4), DpToPx(16), DpToPx(4));
		root.AddView(statusText_);

		// スクロール＋リスト
		var scroll = new ScrollView(this);
		scroll.LayoutParameters = new LinearLayout.LayoutParams(
			ViewGroup.LayoutParams.MatchParent, 0, 1f);
		scroll.SetClipToPadding(false);

		var innerLayout = new LinearLayout(this) { Orientation = Orientation.Vertical };
		innerLayout.SetPadding(DpToPx(8), DpToPx(4), DpToPx(8), DpToPx(8));

		listContainer_ = new LinearLayout(this) { Orientation = Orientation.Vertical };
		innerLayout.AddView(listContainer_);

		// もっと読み込むボタン
		loadMoreBtn_ = new Button(this) { Text = "もっと読み込む" };
		loadMoreBtn_.Visibility = ViewStates.Gone;
		loadMoreBtn_.Click += LoadMoreClick;
		var btnLp = new LinearLayout.LayoutParams(
			ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
		btnLp.TopMargin = DpToPx(8);
		loadMoreBtn_.LayoutParameters = btnLp;
		innerLayout.AddView(loadMoreBtn_);

		scroll.AddView(innerLayout);
		root.AddView(scroll);

		SetContentView(root);
	}

	private async void LoadPageAsync(int page)
	{
		progressBar_.Visibility = ViewStates.Visible;
		loadMoreBtn_.Enabled = false;

		try
		{
			// page=1 の初回は page パラメータなしで送信（SHOGI-EXTEND の自動インポートをトリガー）
			// per は常に指定してページネーションを有効にする
			string url = page <= 1
				? $"{API_BASE}?query={Android.Net.Uri.Encode(username_)}&per={PER_PAGE}"
				: $"{API_BASE}?query={Android.Net.Uri.Encode(username_)}&page={page}&per={PER_PAGE}";
			AppDebug.Log.Info($"ShogiWars: 棋譜一覧取得 page={page} → {url}");

			using var http = new HttpClient();
			http.DefaultRequestHeaders.Add("Accept", "application/json");
			string json = await http.GetStringAsync(url);

			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			totalRecords_ = root.TryGetProperty("total", out var totalEl) ? totalEl.GetInt32() : 0;
			currentPage_ = page;

			if (root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
			{
				RunOnUiThread(() =>
				{
					foreach (var record in records.EnumerateArray())
					{
						AddGameCard(record);
					}

					int loaded = listContainer_.ChildCount;
					statusText_.Text = $"{loaded} / {totalRecords_} 件（SHOGI-EXTEND 登録分）";

					loadMoreBtn_.Visibility = loaded < totalRecords_ ? ViewStates.Visible : ViewStates.Gone;
					loadMoreBtn_.Enabled = true;
					progressBar_.Visibility = ViewStates.Gone;

					// API からの通知メッセージがあれば表示
					if (root.TryGetProperty("xnotice", out var xn) &&
						xn.TryGetProperty("infos", out var infos) &&
						infos.ValueKind == JsonValueKind.Array)
					{
						foreach (var info in infos.EnumerateArray())
						{
							if (info.TryGetProperty("message", out var msg))
							{
								string message = msg.GetString();
								if (!string.IsNullOrEmpty(message))
									Toast.MakeText(this, message, ToastLength.Long).Show();
							}
						}
					}

					// 全件表示済みで件数が少ない場合、SHOGI-EXTEND でのインポートを案内
					if (loaded >= totalRecords_ && totalRecords_ < 100)
					{
						loadMoreBtn_.Text = "SHOGI-EXTEND で棋譜をインポート";
						loadMoreBtn_.Visibility = ViewStates.Visible;
						loadMoreBtn_.Click -= LoadMoreClick;
						loadMoreBtn_.Click += OpenShogiExtend;
					}
				});
			}
			else
			{
				RunOnUiThread(() =>
				{
					statusText_.Text = "棋譜が見つかりませんでした";
					progressBar_.Visibility = ViewStates.Gone;

					// エラーメッセージ確認
					if (root.TryGetProperty("xnotice", out var notice) &&
						notice.TryGetProperty("infos", out var infos) &&
						infos.ValueKind == JsonValueKind.Array)
					{
						foreach (var info in infos.EnumerateArray())
						{
							if (info.TryGetProperty("message", out var msg))
							{
								statusText_.Text = msg.GetString();
								break;
							}
						}
					}
				});
			}
		}
		catch (Exception ex)
		{
			AppDebug.Log.Error($"ShogiWars: 棋譜一覧取得エラー: {ex.Message}");
			RunOnUiThread(() =>
			{
				progressBar_.Visibility = ViewStates.Gone;
				loadMoreBtn_.Enabled = true;
				Toast.MakeText(this, $"取得エラー: {ex.Message}", ToastLength.Long).Show();
			});
		}
	}

	private void AddGameCard(JsonElement record)
	{
		string key = record.TryGetProperty("key", out var k) ? k.GetString() : "";
		string battledAt = record.TryGetProperty("battled_at", out var ba) ? ba.GetString() : "";
		string description = record.TryGetProperty("description", out var desc) ? desc.GetString() : "";

		// 対局者情報 — memberships の location_key で先後を正しく紐付け
		string blackName = "", blackGrade = "", whiteName = "", whiteGrade = "";
		string judgeKey = "";
		if (record.TryGetProperty("memberships", out var mems) && mems.ValueKind == JsonValueKind.Array)
		{
			foreach (var m in mems.EnumerateArray())
			{
				string locKey = m.TryGetProperty("location_key", out var lk) ? lk.GetString() : "";
				string userName = "";
				if (m.TryGetProperty("user", out var u) && u.TryGetProperty("key", out var uk))
					userName = uk.GetString() ?? "";
				string grade = "";
				if (m.TryGetProperty("grade_info", out var gi) && gi.TryGetProperty("name", out var gn))
					grade = gn.GetString() ?? "";

				if (locKey == "black") { blackName = userName; blackGrade = grade; }
				else if (locKey == "white") { whiteName = userName; whiteGrade = grade; }

				if (userName == username_)
					judgeKey = m.TryGetProperty("judge_key", out var jk) ? jk.GetString() : "";
			}
		}

		// 結果
		string finalName = "";
		if (record.TryGetProperty("final_info", out var fi))
			finalName = fi.TryGetProperty("name", out var fn) ? fn.GetString() : "";

		// 対局ルール（10分/3分/10秒）
		string ruleName = "";
		if (record.TryGetProperty("rule_info", out var ri) && ri.TryGetProperty("name", out var rn))
			ruleName = rn.GetString() ?? "";

		// 手数
		int turnMax = record.TryGetProperty("turn_max", out var tm) ? tm.GetInt32() : 0;

		// カード構築
		var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
		card.SetBackgroundResource(Resource.Drawable.drawer_item_bg);
		card.SetPadding(DpToPx(16), DpToPx(12), DpToPx(16), DpToPx(12));
		var cardLp = new LinearLayout.LayoutParams(
			ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
		cardLp.BottomMargin = DpToPx(6);
		card.LayoutParameters = cardLp;

		// 勝敗バッジ + 日時
		var topRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
		topRow.SetGravity(GravityFlags.CenterVertical);

		string badge = judgeKey == "win" ? "勝ち" : judgeKey == "lose" ? "負け" : judgeKey == "draw" ? "引分" : "";
		if (!string.IsNullOrEmpty(badge))
		{
			var badgeText = new TextView(this) { Text = badge };
			badgeText.SetTextSize(Android.Util.ComplexUnitType.Sp, 15);
			badgeText.SetTypeface(null, TypefaceStyle.Bold);
			badgeText.SetTextColor(judgeKey == "win" ? Color.ParseColor("#2196F3") : Color.ParseColor("#F44336"));
			badgeText.SetPadding(0, 0, DpToPx(8), 0);
			topRow.AddView(badgeText);
		}

		// 日時を短縮表示（ISO8601 → MM/dd HH:mm）
		string dateDisplay = battledAt;
		if (DateTime.TryParse(battledAt, out var dt))
			dateDisplay = dt.ToString("MM/dd HH:mm");

		var dateText = new TextView(this) { Text = dateDisplay };
		dateText.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		dateText.SetTextColor(Color.Gray);
		topRow.AddView(dateText);

		// ルール + 終局理由 + 手数
		string metaInfo = "";
		if (!string.IsNullOrEmpty(ruleName)) metaInfo += ruleName;
		if (turnMax > 0) metaInfo += $" {turnMax}手";
		if (!string.IsNullOrEmpty(finalName)) metaInfo += $" {finalName}";
		if (!string.IsNullOrEmpty(metaInfo))
		{
			var metaText = new TextView(this) { Text = $"  {metaInfo}" };
			metaText.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
			metaText.SetTextColor(Color.Gray);
			topRow.AddView(metaText);
		}
		card.AddView(topRow);

		// 対局者名
		string players = $"☗{blackName} {blackGrade}  vs  ☖{whiteName} {whiteGrade}";
		var playerText = new TextView(this) { Text = players };
		playerText.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
		var playerLp = new LinearLayout.LayoutParams(
			ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
		playerLp.TopMargin = DpToPx(4);
		playerText.LayoutParameters = playerLp;
		card.AddView(playerText);

		// タップで棋譜読み込み
		card.Clickable = true;
		card.Click += (s, e) => LoadKifuAsync(key);

		listContainer_.AddView(card);
	}

	private async void LoadKifuAsync(string gameKey)
	{
		if (string.IsNullOrEmpty(gameKey)) return;

		progressBar_.Visibility = ViewStates.Visible;
		statusText_.Text = "棋譜を読み込み中...";
		AppDebug.Log.Info($"ShogiWars: 棋譜読み込み → {gameKey}");

		try
		{
			string url = WARS_GAME_URL + gameKey;
			string kifu = await Task.Run(() => WebKifuFile.LoadKifu(url));

			if (string.IsNullOrEmpty(kifu))
			{
				RunOnUiThread(() =>
				{
					progressBar_.Visibility = ViewStates.Gone;
					statusText_.Text = "";
					Toast.MakeText(this, "棋譜の取得に失敗しました", ToastLength.Long).Show();
				});
				return;
			}

			RunOnUiThread(() =>
			{
				var resultIntent = new Intent();
				resultIntent.PutExtra(ExtraKifu, kifu);
				SetResult(Result.Ok, resultIntent);
				Finish();
			});
		}
		catch (Exception ex)
		{
			AppDebug.Log.Error($"ShogiWars: 棋譜取得エラー: {ex.Message}");
			RunOnUiThread(() =>
			{
				progressBar_.Visibility = ViewStates.Gone;
				statusText_.Text = "";
				Toast.MakeText(this, $"棋譜取得エラー: {ex.Message}", ToastLength.Long).Show();
			});
		}
	}

	private Color GetColorFromAttr(int attrId)
	{
		var typedValue = new Android.Util.TypedValue();
		Theme.ResolveAttribute(attrId, typedValue, true);
		return new Color(typedValue.Data);
	}

	private void LoadMoreClick(object sender, EventArgs e)
	{
		LoadPageAsync(currentPage_ + 1);
	}

	private void OpenShogiExtend(object sender, EventArgs e)
	{
		string url = $"https://www.shogi-extend.com/swars/search?query={Android.Net.Uri.Encode(username_)}";
		StartActivity(new Intent(Intent.ActionView, Android.Net.Uri.Parse(url)));
	}

	private new int DpToPx(int dp)
	{
		return (int)(dp * Resources.DisplayMetrics.Density);
	}
}
