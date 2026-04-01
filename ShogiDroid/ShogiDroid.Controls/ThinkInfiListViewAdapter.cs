using System.Collections.Generic;
using Android.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;
using Object = Java.Lang.Object;
using ShogiGUI;
using ShogiGUI.Engine;
using ShogiLib;

namespace ShogiDroid.Controls;

public class ThinkInfiListViewAdapter : BaseAdapter
{
	protected Activity activity;

	protected PvInfo[] pvinfos;

	protected MoveStyle moveStyle;

	protected bool dispInfo = true;

	protected PVDispMode pvdisp;

	protected PVDispMode dispMode;

	protected PvInfos orginfos;

	private static readonly string[] AnalyzeCommentKindString = new string[6]
	{
		string.Empty,
		Application.Context.GetString(Resource.String.Game_Text),
		Application.Context.GetString(Resource.String.Analysis_Text),
		Application.Context.GetString(Resource.String.Consider_Text),
		Application.Context.GetString(Resource.String.Mate_Text),
		Application.Context.GetString(Resource.String.Candidate_Text)
	};

	protected virtual int LayoutId
	{
		get
		{
			if (dispMode == PVDispMode.Last)
			{
				return Resource.Layout.thinklistviewitem;
			}
			return Resource.Layout.thinklistviewitem2;
		}
	}

	public override int Count
	{
		get
		{
			if (pvinfos == null)
			{
				return 0;
			}
			return pvinfos.Length;
		}
	}

	public MoveStyle MoveStyle
	{
		get
		{
			return moveStyle;
		}
		set
		{
			if (moveStyle != value)
			{
				moveStyle = value;
				NotifyDataSetInvalidated();
			}
		}
	}

	public PVDispMode PVDispaly
	{
		get
		{
			return pvdisp;
		}
		set
		{
			if (pvdisp != value)
			{
				pvdisp = value;
				if (orginfos != null)
				{
					SetPvInfo(orginfos);
					NotifyDataSetInvalidated();
				}
			}
		}
	}

	public PVDispMode DispMode => dispMode;

	public ThinkInfiListViewAdapter(Activity activity)
	{
		this.activity = activity;
	}

	public override Object GetItem(int position)
	{
		return null;
	}

	public override long GetItemId(int position)
	{
		return position;
	}

	public override View GetView(int position, View convertView, ViewGroup parent)
	{
		View view = convertView;
		if (view == null || view.Id != LayoutId)
		{
			view = activity.LayoutInflater.Inflate(LayoutId, parent, attachToRoot: false);
			view.Id = LayoutId;
		}
		FontUtil.ApplyFont(view);
		if (pvinfos == null || pvinfos.Length == 0)
		{
			SetText(view, Resource.Id.kind, string.Empty);
			SetText(view, Resource.Id.rank, string.Empty);
			SetText(view, Resource.Id.time, string.Empty);
			SetText(view, Resource.Id.depth, string.Empty);
			SetText(view, Resource.Id.nodes, string.Empty);
			SetText(view, Resource.Id.nps, string.Empty);
			TextView textView = view.FindViewById<TextView>(Resource.Id.middle);
			if (textView != null)
			{
				textView.Text = string.Empty;
			}
			else
			{
				SetText(view, Resource.Id.value, string.Empty);
				SetText(view, Resource.Id.moves, string.Empty);
			}
		}
		else
		{
			PvInfo pvInfo = pvinfos[position];
			SetText(view, Resource.Id.kind, kindToString(pvInfo.Kind));
			SetText(view, Resource.Id.rank, PvInfo.RankToString(pvInfo.Rank));
			SetText(view, Resource.Id.time, PvInfo.TimeToString(pvInfo.TimeMs));
			SetText(view, Resource.Id.depth, $"{pvInfo.Depth}/{pvInfo.SelDepth}");
			SetText(view, Resource.Id.nodes, PvInfo.NodesToString(pvInfo.Nodes));
			SetText(view, Resource.Id.nps, PvInfo.NpsToString(pvInfo.NPS) + "N/s");
			string evalStr;
			if (Settings.AppSettings.ConvertEvalToWinRate && pvInfo.HasEval)
			{
				int.TryParse(Settings.AppSettings.WinRateCoefficient, out int coeffInt);
				double coeff = coeffInt > 0 ? coeffInt : WinRateUtil.DefaultCoefficient;
				if (pvInfo.HasMate)
				{
					if (pvInfo.HasMatePly)
					{
						evalStr = WinRateUtil.FormatWinRate(pvInfo.Eval, true, pvInfo.MatePly, coeff);
					}
					else
					{
						evalStr = pvInfo.Eval < 0 ? "0%(詰)" : "100%(詰)";
					}
				}
				else
				{
					evalStr = WinRateUtil.FormatWinRate(pvInfo.Score, false, 0, coeff);
				}
			}
			else
			{
				evalStr = PvInfo.ValueToString(pvInfo.Mate, pvInfo.Score, pvInfo.Bounds);
			}

			TextView textView2 = view.FindViewById<TextView>(Resource.Id.middle);
			if (textView2 != null)
			{
				textView2.Text = activity.GetString(Resource.String.Value_Text) + " " + evalStr + " " + pvInfo.GetMoves(moveStyle);
			}
			else
			{
				SetScaledText(view.FindViewById<TextView>(Resource.Id.value), evalStr);
				// 候補手と残りの読み筋を分離表示
				var firstMoveView = view.FindViewById<TextView>(Resource.Id.first_move);
				var restMovesView = view.FindViewById<TextView>(Resource.Id.rest_moves);
				if (firstMoveView != null && restMovesView != null)
				{
					firstMoveView.Text = pvInfo.GetFirstMove(moveStyle);
					restMovesView.Text = pvInfo.GetRestMoves(moveStyle);
				}
				else
				{
					SetText(view, Resource.Id.moves, pvInfo.GetMoves(moveStyle));
				}
			}
		}
		return view;
	}

	private void SetText(View view, int id, string msg)
	{
		TextView textView = view.FindViewById<TextView>(id);
		if (textView != null)
		{
			textView.Text = msg;
		}
	}

	private void SetScaledText(TextView textView, string msg)
	{
		if (textView == null)
		{
			return;
		}

		textView.Text = msg ?? string.Empty;
		textView.SetTextSize(ComplexUnitType.Sp, GetEvalTextSizeSp(msg));
		textView.TextScaleX = 1f;

		var lp = textView.LayoutParameters;
		if (lp == null || lp.Width <= 0 || string.IsNullOrEmpty(msg))
		{
			return;
		}

		float availableWidth = lp.Width - textView.PaddingLeft - textView.PaddingRight;
		if (availableWidth <= 0f)
		{
			return;
		}

		float textWidth = textView.Paint.MeasureText(msg);
		if (textWidth > availableWidth)
		{
			textView.TextScaleX = Math.Max(0.1f, availableWidth / textWidth);
		}
	}

	private static float GetEvalTextSizeSp(string msg)
	{
		if (string.IsNullOrEmpty(msg))
		{
			return 13f;
		}

		if (!msg.Contains("("))
		{
			return 15f;
		}

		if (msg.Length <= 6)
		{
			return 14f;
		}

		return 13f;
	}

	public void SetPvInfo(PvInfos infos)
	{
		dispMode = pvdisp;
		orginfos = infos;
		if (dispMode == PVDispMode.Auto)
		{
			if (infos.Count >= 2)
			{
				dispMode = PVDispMode.Last;
			}
			else
			{
				dispMode = PVDispMode.TimeSeries;
			}
		}
		if (dispMode == PVDispMode.Last)
		{
			pvinfos = new PvInfo[infos.Count];
			infos.Values.CopyTo(pvinfos, 0);
		}
		else
		{
			pvinfos = new PvInfo[infos.InfoList.Count];
			infos.InfoList.CopyTo(pvinfos, 0);
		}
		NotifyDataSetChanged();
	}

	public void SetPvInfo(IList<PvInfo> info)
	{
		pvinfos = new PvInfo[info.Count];
		info.CopyTo(pvinfos, 0);
		NotifyDataSetChanged();
	}

	private string kindToString(AnalyzeCommentKind kind)
	{
		return AnalyzeCommentKindString[(int)kind];
	}
}
