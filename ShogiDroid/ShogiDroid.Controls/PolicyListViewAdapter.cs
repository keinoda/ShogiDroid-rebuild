using System.Collections.Generic;
using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using ShogiGUI;
using ShogiGUI.Engine;

namespace ShogiDroid;

/// <summary>
/// 推定選択率の一覧表示用アダプター
/// </summary>
public class PolicyListViewAdapter : BaseAdapter<PolicyMoveInfo>
{
	private readonly Activity activity;
	private List<PolicyMoveInfo> items = new List<PolicyMoveInfo>();
	private PolicyState state = PolicyState.None;

	public PolicyListViewAdapter(Activity activity)
	{
		this.activity = activity;
	}

	public void SetPolicyInfo(PolicyInfo info)
	{
		if (info == null)
		{
			state = PolicyState.None;
			items = new List<PolicyMoveInfo>();
		}
		else
		{
			state = info.State;
			items = info.Moves ?? new List<PolicyMoveInfo>();
		}
		NotifyDataSetChanged();
	}

	public override int Count => (state == PolicyState.Done) ? items.Count : (state == PolicyState.Analyzing ? 1 : 0);

	public override PolicyMoveInfo this[int position] => (position < items.Count) ? items[position] : null;

	public override long GetItemId(int position) => position;

	public override View GetView(int position, View convertView, ViewGroup parent)
	{
		// 解析中表示
		if (state == PolicyState.Analyzing)
		{
			var tv = new TextView(activity);
			tv.Text = "推定中…";
			tv.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
			tv.SetPadding(DpToPx(8), DpToPx(4), DpToPx(8), DpToPx(4));
			tv.SetTextColor(ColorUtils.Get(activity, Resource.Color.primary_text));
			return tv;
		}

		if (position >= items.Count) return new View(activity);

		var item = items[position];

		// 行レイアウト
		var row = new LinearLayout(activity) { Orientation = Orientation.Horizontal };
		row.SetGravity(GravityFlags.CenterVertical);
		row.SetPadding(DpToPx(6), DpToPx(2), DpToPx(6), DpToPx(2));

		// 手の表示（USI表記をそのまま使用。アプリ組み込み時にKI2に変換予定）
		string moveText = item.MoveUSI;
		string rateText = $"{item.SelectionRate:F1}%";

		var moveLabel = new TextView(activity);
		moveLabel.Text = moveText;
		moveLabel.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		moveLabel.SetTypeface(null, TypefaceStyle.Bold);
		moveLabel.SetTextColor(ColorUtils.Get(activity, Resource.Color.primary_text));
		var moveLp = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1f);
		moveLabel.LayoutParameters = moveLp;
		row.AddView(moveLabel);

		// バー
		var barContainer = new FrameLayout(activity);
		var barLp = new LinearLayout.LayoutParams(DpToPx(80), DpToPx(14));
		barLp.SetMargins(DpToPx(4), 0, DpToPx(4), 0);
		barContainer.LayoutParameters = barLp;

		var barBg = new View(activity);
		barBg.SetBackgroundColor(Color.ParseColor("#333333"));
		barBg.LayoutParameters = new FrameLayout.LayoutParams(
			FrameLayout.LayoutParams.MatchParent, FrameLayout.LayoutParams.MatchParent);
		barContainer.AddView(barBg);

		int barWidth = (int)(DpToPx(80) * (item.SelectionRate / 100.0));
		var barFill = new View(activity);
		barFill.SetBackgroundColor(Color.ParseColor("#4CAF50"));
		barFill.LayoutParameters = new FrameLayout.LayoutParams(
			barWidth, FrameLayout.LayoutParams.MatchParent);
		barContainer.AddView(barFill);
		row.AddView(barContainer);

		// パーセンテージ
		var rateLabel = new TextView(activity);
		rateLabel.Text = rateText;
		rateLabel.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
		rateLabel.SetTextColor(ColorUtils.Get(activity, Resource.Color.primary_text));
		rateLabel.Gravity = GravityFlags.Right;
		var rateLp = new LinearLayout.LayoutParams(DpToPx(50), LinearLayout.LayoutParams.WrapContent);
		rateLabel.LayoutParameters = rateLp;
		row.AddView(rateLabel);

		return row;
	}

	private int DpToPx(int dp)
	{
		return (int)(dp * activity.Resources.DisplayMetrics.Density + 0.5f);
	}
}
