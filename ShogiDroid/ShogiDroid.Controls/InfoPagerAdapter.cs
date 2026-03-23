using System;
using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using AndroidX.ViewPager.Widget;
using Java.Lang;
using ShogiGUI;
using ShogiGUI.Engine;
using ShogiGUI.Events;
using ShogiLib;

namespace ShogiDroid.Controls;

public class InfoPagerAdapter : PagerAdapter
{
	private Activity activity;

	private TextView commentTextView;

	private ListView thinkListView;

	private ThinkInfiListViewAdapter thinkInfoListViewAdapter;

	private EvalGraph graphView;

	private View thinkInfoPage;

	private string comment = string.Empty;

	private string thinktext = string.Empty;

	private SNotation notation;

	private long nodes;

	private int nps;

	private int timeMs;

	private int depth;

	private int selDepth;

	private int hashFull = -1;

	private float scaleFactor = 1f;

	private bool graphLiner = true;

	public override int Count
	{
		get
		{
			if (DispEvalGraph)
			{
				return 3;
			}
			return 2;
		}
	}

	public MoveStyle MoveStyle
	{
		get
		{
			return thinkInfoListViewAdapter.MoveStyle;
		}
		set
		{
			thinkInfoListViewAdapter.MoveStyle = value;
		}
	}

	public PVDispMode PVDispaly
	{
		get
		{
			return thinkInfoListViewAdapter.PVDispaly;
		}
		set
		{
			thinkInfoListViewAdapter.PVDispaly = value;
		}
	}

	public PVDispMode DispMode => thinkInfoListViewAdapter.DispMode;

	public bool DispEvalGraph { get; set; }

	public string Comment
	{
		set
		{
			comment = value;
			if (commentTextView != null)
			{
				commentTextView.Text = value;
				if (value != string.Empty)
				{
					commentTextView.Hint = string.Empty;
				}
			}
		}
	}

	public float EvalGraphScaleFactor
	{
		get
		{
			if (graphView != null)
			{
				return graphView.ScaleFactor;
			}
			return scaleFactor;
		}
		set
		{
			if (graphView != null)
			{
				graphView.ScaleFactor = value;
			}
			scaleFactor = value;
		}
	}

	public bool EvalGraphLiner
	{
		get
		{
			if (graphView != null)
			{
				return graphView.GraphLiner;
			}
			return graphLiner;
		}
		set
		{
			if (graphView != null)
			{
				graphView.GraphLiner = value;
			}
			graphLiner = value;
		}
	}

	public event EventHandler<GraphPositoinEventArgs> SelectPosition;

	public event EventHandler<AdapterView.ItemClickEventArgs> ThinkListViewItemClick;

	public event EventHandler<AdapterView.ItemLongClickEventArgs> ThinkListViewItemLongClick;

	public event EventHandler<View.LongClickEventArgs> CommentLongClick;

	public InfoPagerAdapter(Activity activity)
	{
		this.activity = activity;
		thinkInfoListViewAdapter = new ThinkInfiListViewAdapter(activity);
	}

	public override Java.Lang.Object InstantiateItem(ViewGroup container, int position)
	{
		switch (position)
		{
		case 0:
		{
			ScrollView scrollView = new ScrollView(activity);
			scrollView.FillViewport = true;
			commentTextView = new TextView(activity);
			commentTextView.Text = comment;
			commentTextView.Hint = activity.GetString(Resource.String.Comment_Text);
			commentTextView.SetTextSize(Android.Util.ComplexUnitType.Sp, 15f);
			commentTextView.SetPadding(Dp(18), Dp(18), Dp(18), Dp(24));
			commentTextView.SetLineSpacing(0f, 1.15f);
			// テーマの標準テキスト色を使用（ダークモード対応）
			var typedValue = new Android.Util.TypedValue();
			activity.Theme.ResolveAttribute(Android.Resource.Attribute.TextColorPrimary, typedValue, true);
			commentTextView.SetTextColor(activity.Resources.GetColorStateList(typedValue.ResourceId, activity.Theme));
			commentTextView.SetHintTextColor(ColorUtils.Get(activity, Resource.Color.secondary_text));
			commentTextView.LongClick += CommentTextView_LongClick;
			scrollView.AddView(commentTextView);
			container.AddView(scrollView);
			return scrollView;
		}
		case 1:
		{
			View view = (thinkInfoPage = activity.LayoutInflater.Inflate(Resource.Layout.thinkinfopage, container, attachToRoot: false));
			thinkListView = view.FindViewById<ListView>(Resource.Id.thinklistview);
			thinkListView.Adapter = thinkInfoListViewAdapter;
			thinkListView.ItemLongClick += ThinkListView_ItemLongClick;
			thinkListView.ItemClick += ThinkListView_ItemClick;
			UpdateThinkInfo();
			container.AddView(view);
			return view;
		}
		case 2:
			graphView = new EvalGraph(activity);
			graphView.ScaleFactor = scaleFactor;
			graphView.GraphLiner = graphLiner;
			graphView.SelectPosition += GraphView_SelectPosition;
			if (notation != null)
			{
				graphView.Notation = notation;
			}
			container.AddView(graphView);
			return graphView;
		default:
			return base.InstantiateItem(container, position);
		}
	}

	public override void DestroyItem(ViewGroup container, int position, Java.Lang.Object objectValue)
	{
		container.RemoveView((View)objectValue);
	}

	public override bool IsViewFromObject(View view, Java.Lang.Object objectValue)
	{
		return view == objectValue;
	}

	private void GraphView_SelectPosition(object sender, GraphPositoinEventArgs e)
	{
		if (this.SelectPosition != null)
		{
			this.SelectPosition(sender, e);
		}
	}

	private void ThinkListView_ItemLongClick(object sender, AdapterView.ItemLongClickEventArgs e)
	{
		if (this.ThinkListViewItemLongClick != null)
		{
			this.ThinkListViewItemLongClick(sender, e);
		}
	}

	private void ThinkListView_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
	{
		if (this.ThinkListViewItemClick != null)
		{
			this.ThinkListViewItemClick(sender, e);
		}
	}

	private void CommentTextView_LongClick(object sender, View.LongClickEventArgs e)
	{
		if (this.CommentLongClick != null)
		{
			this.CommentLongClick(sender, e);
		}
	}

	private double remoteCpu_ = -1;
	private double remoteGpu_ = -1;

	public void SetRemoteStats(double cpuUsage, double gpuUsage)
	{
		remoteCpu_ = cpuUsage;
		remoteGpu_ = gpuUsage;
		UpdateRemoteStatsLine();
	}

	public void HideRemoteStats()
	{
		remoteCpu_ = -1;
		remoteGpu_ = -1;
		UpdateRemoteStatsLine();
	}

	private void UpdateRemoteStatsLine()
	{
		if (thinkInfoPage == null) return;
		var tv = thinkInfoPage.FindViewById<TextView>(Resource.Id.remote_stats);
		if (tv == null) return;

		var parts = new System.Collections.Generic.List<string>();

		// Hash使用率
		if (hashFull >= 0)
			parts.Add($"Hash: {hashFull / 10.0:F1}%");

		// CPU/GPU利用率
		if (remoteCpu_ >= 0)
			parts.Add($"CPU: {remoteCpu_:F0}%");
		if (remoteGpu_ >= 0)
			parts.Add($"GPU: {remoteGpu_:F0}%");

		if (parts.Count > 0)
		{
			tv.Text = string.Join("  ", parts);
			tv.Visibility = ViewStates.Visible;
		}
		else
		{
			tv.Visibility = ViewStates.Gone;
		}
	}

	public void SetThinkInfo(PvInfos pvinfos)
	{
		nodes = pvinfos.Nodes;
		nps = pvinfos.Nps;
		timeMs = pvinfos.TimeMs;
		depth = pvinfos.Depth;
		selDepth = pvinfos.SelDepth;
		hashFull = pvinfos.HashFull;
		UpdateThinkInfo();
		UpdateRemoteStatsLine();
		thinkInfoListViewAdapter.SetPvInfo(pvinfos);
	}

	private void UpdateThinkInfo()
	{
		if (thinkInfoPage != null)
		{
			thinkInfoPage.FindViewById<TextView>(Resource.Id.time).Text = PvInfo.TimeToString(timeMs);
			thinkInfoPage.FindViewById<TextView>(Resource.Id.depth).Text = $"Depth {depth}/{selDepth}";
			thinkInfoPage.FindViewById<TextView>(Resource.Id.nodes).Text = "Nodes " + PvInfo.NodesToString(nodes);
			thinkInfoPage.FindViewById<TextView>(Resource.Id.nps).Text = "NPS " + PvInfo.NpsToString(nps);
		}
	}

	public void SetNotation(SNotation notation)
	{
		this.notation = notation;
		if (graphView != null)
		{
			graphView.Notation = notation;
		}
	}

	public void UpdateNotation(NotationEventId op)
	{
		if (graphView != null)
		{
			graphView.UpdateNotation(op);
		}
	}

	private int Dp(int dp)
	{
		return (int)(dp * activity.Resources.DisplayMetrics.Density + 0.5f);
	}
}
