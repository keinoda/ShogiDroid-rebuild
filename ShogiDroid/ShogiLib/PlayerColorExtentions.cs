namespace ShogiLib;

public static class PlayerColorExtentions
{
	public static PlayerColor Opp(this PlayerColor color)
	{
		return color ^ PlayerColor.White;
	}

	public static char ToChar(this PlayerColor color)
	{
		if (color != PlayerColor.Black)
		{
			return '☖';
		}
		return '☗';
	}

	/// <summary>
	/// 棋譜ファイル出力用（標準の三角マーク）。
	/// </summary>
	public static char ToKifChar(this PlayerColor color)
	{
		if (color != PlayerColor.Black)
		{
			return '△';
		}
		return '▲';
	}
}
