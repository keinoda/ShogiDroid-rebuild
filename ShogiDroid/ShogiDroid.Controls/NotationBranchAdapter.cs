using Android.App;
using Android.Views;
using Android.Widget;
using Java.Lang;
using ShogiLib;
using Object = Java.Lang.Object;

namespace ShogiDroid.Controls;

public class NotationBranchAdapter : BaseAdapter
{
	private Activity activity;

	private SNotation notation;

	private MoveStyle moveStyle;

	public override int Count
	{
		get
		{
			if (notation.MoveCurrent.Children.Count <= 1)
			{
				return 0;
			}
			return notation.MoveCurrent.Children.Count;
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

	public NotationBranchAdapter(Activity activity)
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
		View obj = convertView ?? activity.LayoutInflater.Inflate(Resource.Layout.notationlistviewitem, parent, attachToRoot: false);
		TextView textView = obj.FindViewById<TextView>(Resource.Id.move_text);
		MoveNode moveNode = notation.MoveCurrent.Children[position];
		textView.Text = $"{moveNode.Turn.ToChar()}{moveNode.ToString(moveStyle)}";
		return obj;
	}

	public void SetNotation(SNotation notation)
	{
		this.notation = notation;
		NotifyDataSetInvalidated();
	}
}
