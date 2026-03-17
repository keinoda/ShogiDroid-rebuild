using Android.Content;
using Android.Media;

namespace ShogiDroid;

public static class PlaySE
{
	public enum SePriority
	{
		Koma = 1
	}

	private static SoundPool soundpool;

	private static int piece_se_id;

	public static void Initialize(Context context)
	{
		if (soundpool == null)
		{
			soundpool = new SoundPool(4, Stream.Music, 0);
			piece_se_id = soundpool.Load(context, Resource.Raw.piece_se, 1);
		}
	}

	public static void Destory()
	{
		if (soundpool != null)
		{
			soundpool.Release();
			soundpool = null;
		}
	}

	public static void Play(SeNo no)
	{
		if (soundpool != null && no == SeNo.KOMA)
		{
			soundpool.Play(piece_se_id, 1f, 1f, 1, 0, 1f);
		}
	}
}
