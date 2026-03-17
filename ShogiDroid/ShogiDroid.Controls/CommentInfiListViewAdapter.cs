using Android.App;

namespace ShogiDroid.Controls;

public class CommentInfiListViewAdapter : ThinkInfiListViewAdapter
{
	protected override int LayoutId => Resource.Layout.commentinfoitem;

	public CommentInfiListViewAdapter(Activity activity)
		: base(activity)
	{
		dispInfo = false;
	}
}
