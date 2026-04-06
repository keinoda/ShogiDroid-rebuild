using Android.App;

namespace ShogiDroid.Controls;

public class CommentInfoListViewAdapter : ThinkInfoListViewAdapter
{
	protected override int LayoutId => Resource.Layout.commentinfoitem;

	public CommentInfoListViewAdapter(Activity activity)
		: base(activity)
	{
		dispInfo = false;
	}
}
