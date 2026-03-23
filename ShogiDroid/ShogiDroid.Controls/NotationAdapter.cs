using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Java.Lang;
using ShogiGUI;
using ShogiLib;
using Object = Java.Lang.Object;

namespace ShogiDroid.Controls;

public class NotationAdapter : BaseAdapter
{
	private Activity activity;

	private SNotation notation;

	private MoveStyle moveStyle;

	public override int Count => notation.Count + 1;

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

	public NotationAdapter(Activity activity)
	{
		this.activity = activity;
		notation = new SNotation();
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
		View view = convertView ?? activity.LayoutInflater.Inflate(Resource.Layout.notationlistviewitem, parent, attachToRoot: false);
		FontUtil.ApplyFont(view);
		TextView textView = view.FindViewById<TextView>(Resource.Id.move_text);
		MoveNode moveNode = notation.GetMoveNode(position);
		TextView textView2 = view.FindViewById<TextView>(Resource.Id.time_text);
		if (moveNode.Number == 0)
		{
			textView.Text = $"=== {MoveStringExtention.InitialPosition(moveStyle)} ===";
			textView2.Text = string.Empty;
		}
		else
		{
			textView.Text = string.Format("{0,3} {2}{1}", position, moveNode.ToString(moveStyle), moveNode.Turn.ToChar());
			textView2.Text = $"{moveNode.Time / 60,2}:{moveNode.Time % 60:D2}";
		}
		textView.SetTextColor(ColorUtils.Get(activity, Resource.Color.primary_text));
		textView2.SetTextColor(ColorUtils.Get(activity, Resource.Color.secondary_text));
		TextView textView3 = view.FindViewById<TextView>(Resource.Id.state_info_text);
		textView3.SetTextColor(ColorUtils.Get(activity, Resource.Color.secondary_text));
		if (moveNode.ThinkInfoCount != 0)
		{
			textView3.Text = "i";
		}
		else
		{
			textView3.Text = string.Empty;
		}
		TextView textView4 = view.FindViewById<TextView>(Resource.Id.state_comment_text);
		textView4.SetTextColor(ColorUtils.Get(activity, Resource.Color.secondary_text));
		if (moveNode.CommentCount != 0)
		{
			textView4.Text = "*";
		}
		else
		{
			textView4.Text = string.Empty;
		}
		TextView textView5 = view.FindViewById<TextView>(Resource.Id.branch_text);
		textView5.SetTextColor(ColorUtils.Get(activity, Resource.Color.secondary_text));
		if (moveNode.Parent != null && moveNode.Parent.Children.Count > 1)
		{
			textView5.Text = "+";
		}
		else
		{
			textView5.Text = string.Empty;
		}
		TextView textView6 = view.FindViewById<TextView>(Resource.Id.eval_text);
		if (moveNode.HasScore)
		{
			textView6.Text = MoveStringExtention.ToEvalString(moveNode.Score, moveStyle);
		}
		else if (moveNode.HasEval)
		{
			textView6.Text = MoveStringExtention.ToEvalString(moveNode.Eval, moveStyle);
		}
		else
		{
			textView6.Text = string.Empty;
		}
		switch (MoveEvalExtention.GetMoveEval(moveNode, moveNode.Parent))
		{
		case MoveEval.Bad:
			textView6.SetTextColor(ColorUtils.Get(activity, Resource.Color.warning_tint));
			break;
		case MoveEval.Blunder:
			textView6.SetTextColor(ColorUtils.Get(activity, Resource.Color.negative_tint));
			break;
		case MoveEval.Best:
			textView6.SetTextColor(ColorUtils.Get(activity, Resource.Color.positive_tint));
			break;
		default:
			textView6.SetTextColor(ColorUtils.Get(activity, Resource.Color.secondary_text));
			break;
		}
		if (moveNode == notation.MoveCurrent)
		{
			view.SetBackgroundColor(ColorUtils.Get(activity, Resource.Color.notation_select));
		}
		else if (moveNode.Parent != null && moveNode != moveNode.Parent.Children[0])
		{
			view.SetBackgroundColor(ColorUtils.Get(activity, Resource.Color.notation_select_branch));
		}
		else if (moveNode.Number == 0 || moveNode.Turn == PlayerColor.White)
		{
			view.SetBackgroundColor(ColorUtils.Get(activity, Resource.Color.notation_white_back));
		}
		else
		{
			view.SetBackgroundColor(ColorUtils.Get(activity, Resource.Color.notation_black_back));
		}
		return view;
	}

	public void SetNotation(SNotation notation)
	{
		this.notation = notation;
		NotifyDataSetInvalidated();
	}
}
