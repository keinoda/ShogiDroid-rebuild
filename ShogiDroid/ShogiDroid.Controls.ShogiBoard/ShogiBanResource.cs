using Android.Content;
using Android.Graphics;

namespace ShogiDroid.Controls.ShogiBoard;

public static class ShogiBanResource
{
	private static Bitmap pieceBitmap;

	private static Color lastPieceBackColor = Color.LightBlue;

	private static string pieceFileName = string.Empty;

	private static string boardFileName = string.Empty;

	private static string standFileName = string.Empty;

	private static string backgroundFileName = string.Empty;

	private static Color ruleLineColor;

	private static Color coordColor;

	private static string coordFontName;

	private static float coordFontSize;

	private static Context context;

	public static Color LastPieceBackColor
	{
		get
		{
			return lastPieceBackColor;
		}
		set
		{
			lastPieceBackColor = value;
		}
	}

	public static Color RuleLineColor
	{
		get
		{
			return ruleLineColor;
		}
		set
		{
			ruleLineColor = value;
		}
	}

	public static Color CoordColor
	{
		get
		{
			return coordColor;
		}
		set
		{
			coordColor = value;
		}
	}

	public static string CoordFontName
	{
		get
		{
			return coordFontName;
		}
		set
		{
			coordFontName = value;
		}
	}

	public static float CoordFontSize
	{
		get
		{
			return coordFontSize;
		}
		set
		{
			coordFontSize = value;
		}
	}

	public static Bitmap PieceBitmap
	{
		get
		{
			if (pieceBitmap == null)
			{
				pieceBitmap = BitmapFactory.DecodeResource(context.Resources, Resource.Drawable.koma1);
			}
			return pieceBitmap;
		}
	}

	public static void Init(Context context)
	{
		ShogiBanResource.context = context;
	}

	public static void LoadLastPieceBackColor()
	{
	}

	public static void LoadPieceImage(string filename)
	{
	}
}
