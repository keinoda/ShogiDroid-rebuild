using System;
using System.Collections.Generic;
using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using ShogiGUI;

namespace ShogiDroid.Controls;

/// <summary>
/// ドロワーアイテムの種類
/// </summary>
public enum DrawerItemKind
{
	Action,      // 即時実行
	Navigate,    // 画面遷移
	Divider      // 区切り線
}

/// <summary>
/// ドロワーの1項目
/// </summary>
public class DrawerItemModel
{
	public int Id;
	public string Label;
	public DrawerItemKind Kind;
	public Func<bool> IsEnabled;

	public DrawerItemModel(int id, string label, DrawerItemKind kind = DrawerItemKind.Action, Func<bool> isEnabled = null)
	{
		Id = id;
		Label = label;
		Kind = kind;
		IsEnabled = isEnabled;
	}
}

/// <summary>
/// ドロワーのセクション
/// </summary>
public class DrawerSectionModel
{
	public string Title;
	public List<DrawerItemModel> Items;
	public bool IsQuickAction; // クイック操作（常時展開）

	public DrawerSectionModel(string title, bool isQuickAction = false)
	{
		Title = title;
		Items = new List<DrawerItemModel>();
		IsQuickAction = isQuickAction;
	}

	public DrawerSectionModel Add(int id, string label, DrawerItemKind kind = DrawerItemKind.Action, Func<bool> isEnabled = null)
	{
		Items.Add(new DrawerItemModel(id, label, kind, isEnabled));
		return this;
	}
}

/// <summary>
/// ExpandableListView用のドロワーアダプター
/// </summary>
public class DrawerSectionAdapter : BaseExpandableListAdapter
{
	private readonly Activity activity_;
	private readonly List<DrawerSectionModel> sections_;

	public DrawerSectionAdapter(Activity activity, List<DrawerSectionModel> sections)
	{
		activity_ = activity;
		sections_ = sections;
	}

	public override int GroupCount => sections_.Count;

	public override bool HasStableIds => true;

	public override Java.Lang.Object GetGroup(int groupPosition) => null;

	public override long GetGroupId(int groupPosition) => groupPosition;

	public override int GetChildrenCount(int groupPosition) => sections_[groupPosition].Items.Count;

	public override Java.Lang.Object GetChild(int groupPosition, int childPosition) => null;

	public override long GetChildId(int groupPosition, int childPosition) => childPosition;

	public override bool IsChildSelectable(int groupPosition, int childPosition)
	{
		return IsItemEnabled(sections_[groupPosition].Items[childPosition]);
	}

	public DrawerItemModel GetItemModel(int groupPosition, int childPosition)
	{
		return sections_[groupPosition].Items[childPosition];
	}

	public DrawerSectionModel GetSectionModel(int groupPosition)
	{
		return sections_[groupPosition];
	}

	private bool IsItemEnabled(DrawerItemModel item)
	{
		return item.IsEnabled?.Invoke() ?? true;
	}

	public override View GetGroupView(int groupPosition, bool isExpanded, View convertView, ViewGroup parent)
	{
		var section = sections_[groupPosition];

		var tv = convertView as TextView;
		if (tv == null)
		{
			tv = new TextView(activity_);
#if CLASSIC_UI
			tv.SetPadding(Dp(8), Dp(10), Dp(8), Dp(4));
			tv.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
#else
			tv.SetPadding(Dp(8), Dp(14), Dp(8), Dp(8));
			tv.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
#endif
		}

		tv.Text = section.Title;
		tv.SetTypeface(null, section.IsQuickAction ? TypefaceStyle.Bold : TypefaceStyle.Normal);
		tv.SetTextColor(ColorUtils.Get(activity_, section.IsQuickAction ? Resource.Color.title_background : Resource.Color.secondary_text));
		tv.SetBackgroundColor(Color.Transparent);

		return tv;
	}

	public override View GetChildView(int groupPosition, int childPosition, bool isLastChild, View convertView, ViewGroup parent)
	{
		var item = sections_[groupPosition].Items[childPosition];

		var tv = convertView as TextView;
		if (tv == null)
		{
			tv = new TextView(activity_);
#if CLASSIC_UI
			tv.SetPadding(Dp(14), Dp(10), Dp(14), Dp(10));
			tv.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
			tv.SetMinHeight(Dp(42));
#else
			tv.SetPadding(Dp(18), Dp(14), Dp(18), Dp(14));
			tv.SetTextSize(Android.Util.ComplexUnitType.Sp, 15);
			tv.SetMinHeight(Dp(48));
#endif
		}

		tv.Text = item.Label;
		tv.SetBackgroundResource(Resource.Drawable.drawer_item_bg);

		bool enabled = IsItemEnabled(item);
		tv.Enabled = enabled;
		tv.Alpha = enabled ? 1f : 0.45f;
		if (enabled)
		{
			tv.SetTextColor(ColorUtils.Get(activity_, Resource.Color.primary_text));
		}
		else
		{
			tv.SetTextColor(ColorUtils.Get(activity_, Resource.Color.secondary_text));
		}

		FontUtil.ApplyFont(tv);
		return tv;
	}

	public void NotifyChanged()
	{
		NotifyDataSetChanged();
	}

	private int Dp(int dp)
	{
		return (int)(dp * activity_.Resources.DisplayMetrics.Density + 0.5f);
	}
}
