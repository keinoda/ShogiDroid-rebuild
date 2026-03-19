using Android.Graphics;
using ShogiLib;
using System;
using System.Collections.Generic;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using IODirectory = System.IO.Directory;

namespace ShogiGUI;

/// <summary>
/// 書籍・新聞風の将棋盤面図をBitmapとして生成する。
/// </summary>
public static class BoardImageExporter
{
	private static readonly Dictionary<Piece, string> PieceToKanji = new Dictionary<Piece, string>
	{
		{ Piece.BOU, "王" }, { Piece.BHI, "飛" }, { Piece.BRYU, "龍" },
		{ Piece.BKAK, "角" }, { Piece.BUMA, "馬" }, { Piece.BKIN, "金" },
		{ Piece.BGIN, "銀" }, { Piece.BNGIN, "全" }, { Piece.BKEI, "桂" },
		{ Piece.BNKEI, "圭" }, { Piece.BKYO, "香" }, { Piece.BNKYO, "杏" },
		{ Piece.BFU, "歩" }, { Piece.BTO, "と" },
		{ Piece.WOU, "玉" }, { Piece.WHI, "飛" }, { Piece.WRYU, "龍" },
		{ Piece.WKAK, "角" }, { Piece.WUMA, "馬" }, { Piece.WKIN, "金" },
		{ Piece.WGIN, "銀" }, { Piece.WNGIN, "全" }, { Piece.WKEI, "桂" },
		{ Piece.WNKEI, "圭" }, { Piece.WKYO, "香" }, { Piece.WNKYO, "杏" },
		{ Piece.WFU, "歩" }, { Piece.WTO, "と" },
	};

	private static readonly string[] RankLabels = { "一", "二", "三", "四", "五", "六", "七", "八", "九" };
	private static readonly string[] FileLabels = { "９", "８", "７", "６", "５", "４", "３", "２", "１" };

	private static readonly PieceType[] HandOrder = {
		PieceType.HI, PieceType.KAK, PieceType.KIN, PieceType.GIN,
		PieceType.KEI, PieceType.KYO, PieceType.FU
	};

	private static readonly Dictionary<PieceType, string> HandPieceKanji = new Dictionary<PieceType, string>
	{
		{ PieceType.HI, "飛" }, { PieceType.KAK, "角" }, { PieceType.KIN, "金" },
		{ PieceType.GIN, "銀" }, { PieceType.KEI, "桂" }, { PieceType.KYO, "香" },
		{ PieceType.FU, "歩" }
	};

	private static readonly string[] KanjiNumbers = { "", "", "二", "三", "四", "五", "六", "七", "八", "九", "十",
		"十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八" };

	public static Bitmap Generate(SPosition position, string blackName = "先手",
		string whiteName = "後手", string headerText = null, int imageSize = 1200,
		Typeface typeface = null)
	{
		int size = imageSize;
		var bmp = Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888);
		var canvas = new Canvas(bmp);
		canvas.DrawColor(Color.White);

		var tf = typeface ?? FontUtil.Normal;
		var tfBold = typeface != null ? Typeface.Create(typeface, TypefaceStyle.Bold) : FontUtil.Bold;

		// レイアウト計算（ShogiHome SimpleBoardView準拠）
		float boardSize = size * 0.68f;
		float cellSize = boardSize / 9f;
		float boardLeft = size * 0.17f;
		float boardTop = size * 0.10f;
		float boardRight = boardLeft + boardSize;
		float boardBottom = boardTop + boardSize;

		// 持ち駒エリアの幅
		float handWidth = size * 0.08f;
		// 先手持ち駒エリア: 段ラベルの右
		float senteHandX = boardRight + cellSize * 0.9f;
		// 後手持ち駒エリア: 盤面の左
		float goteHandX = boardLeft - cellSize * 0.3f;

		// === グリッド描画 ===
		var gridPaint = new Paint { Color = Color.Black, AntiAlias = true };
		gridPaint.SetStyle(Paint.Style.Stroke);

		// 外枠（太線）
		gridPaint.StrokeWidth = size * 0.0035f;
		canvas.DrawRect(boardLeft, boardTop, boardRight, boardBottom, gridPaint);

		// 内部線（細線）
		gridPaint.StrokeWidth = size * 0.0015f;
		for (int i = 1; i < 9; i++)
		{
			float x = boardLeft + cellSize * i;
			canvas.DrawLine(x, boardTop, x, boardBottom, gridPaint);
			float y = boardTop + cellSize * i;
			canvas.DrawLine(boardLeft, y, boardRight, y, gridPaint);
		}

		// === 筋ラベル（盤面上部: 9 8 7 6 5 4 3 2 1）===
		var labelPaint = new Paint { Color = Color.Black, AntiAlias = true };
		labelPaint.SetTypeface(tf);
		labelPaint.TextSize = cellSize * 0.32f;
		labelPaint.TextAlign = Paint.Align.Center;
		for (int f = 0; f < 9; f++)
		{
			float cx = boardLeft + cellSize * f + cellSize / 2f;
			canvas.DrawText(FileLabels[f], cx, boardTop - cellSize * 0.2f, labelPaint);
		}

		// === 段ラベル（盤面右側: 一〜九）===
		labelPaint.TextAlign = Paint.Align.Left;
		labelPaint.TextSize = cellSize * 0.32f;
		for (int r = 0; r < 9; r++)
		{
			float cy = boardTop + cellSize * r + cellSize * 0.62f;
			canvas.DrawText(RankLabels[r], boardRight + cellSize * 0.15f, cy, labelPaint);
		}

		// === 駒の描画 ===
		var piecePaint = new Paint { Color = Color.Black, AntiAlias = true };
		piecePaint.SetTypeface(tfBold);
		piecePaint.TextAlign = Paint.Align.Center;
		float pieceFontSize = cellSize * 0.62f;
		piecePaint.TextSize = pieceFontSize;

		var fm = piecePaint.GetFontMetrics();
		float textCenterOffset = -(fm.Ascent + fm.Descent) / 2f;

		for (int rank = 0; rank < 9; rank++)
		{
			for (int file = 0; file < 9; file++)
			{
				int sq = rank * 9 + file;
				Piece piece = position.GetPiece(sq);
				if (piece == Piece.NoPiece) continue;
				if (!PieceToKanji.TryGetValue(piece, out string kanji)) continue;

				float cx = boardLeft + cellSize * file + cellSize / 2f;
				float cy = boardTop + cellSize * rank + cellSize / 2f;

				bool isWhite = piece.ColorOf() == PlayerColor.White;
				if (isWhite)
				{
					// 後手の駒: 上下左右反転で描画
					canvas.Save();
					canvas.Scale(-1, -1, cx, cy);
					canvas.DrawText(kanji, cx, cy + textCenterOffset, piecePaint);
					canvas.Restore();
				}
				else
				{
					canvas.DrawText(kanji, cx, cy + textCenterOffset, piecePaint);
				}
			}
		}

		// === 持ち駒（縦書き） ===
		var handPaint = new Paint { Color = Color.Black, AntiAlias = true };
		handPaint.SetTypeface(tf);
		handPaint.TextAlign = Paint.Align.Center;
		float handFontSize = cellSize * 0.35f;
		handPaint.TextSize = handFontSize;
		float handLineHeight = handFontSize * 1.3f;

		// 先手持ち駒（右側、下から上へ縦書き）
		string senteHandStr = FormatHandVertical(position, PlayerColor.Black);
		string senteTitle = "☗" + blackName;
		float senteStartY = boardBottom - cellSize * 0.3f;
		DrawVerticalDown(canvas, senteTitle, senteHandX, senteStartY, handPaint, handLineHeight);
		if (senteHandStr.Length > 0)
		{
			DrawVerticalDown(canvas, senteHandStr,
				senteHandX + handFontSize * 1.3f, senteStartY,
				handPaint, handLineHeight);
		}

		// 後手持ち駒（左側、上から下へ縦書き）
		string goteHandStr = FormatHandVertical(position, PlayerColor.White);
		string goteTitle = "☖" + whiteName;
		float goteStartY = boardTop;
		DrawVerticalUp(canvas, goteTitle, goteHandX, goteStartY, handPaint, handLineHeight);
		if (goteHandStr.Length > 0)
		{
			DrawVerticalUp(canvas, goteHandStr,
				goteHandX - handFontSize * 1.3f, goteStartY,
				handPaint, handLineHeight);
		}

		// === ヘッダー ===
		if (!string.IsNullOrEmpty(headerText))
		{
			var headerPaint = new Paint { Color = Color.Black, AntiAlias = true };
			headerPaint.SetTypeface(tf);
			headerPaint.TextSize = cellSize * 0.38f;
			headerPaint.TextAlign = Paint.Align.Center;
			canvas.DrawText(headerText, size / 2f, boardTop - cellSize * 0.7f, headerPaint);
			headerPaint.Dispose();
		}

		gridPaint.Dispose();
		labelPaint.Dispose();
		piecePaint.Dispose();
		handPaint.Dispose();
		fm.Dispose();

		return bmp;
	}

	/// <summary>
	/// 持ち駒を縦書き用文字列にフォーマットする（例: "飛角金銀二桂歩三"）。
	/// </summary>
	private static string FormatHandVertical(SPosition pos, PlayerColor color)
	{
		int[] hand = color == PlayerColor.Black ? pos.BlackHand : pos.WhiteHand;
		string result = "";
		foreach (var pt in HandOrder)
		{
			int count = hand[(int)pt];
			if (count <= 0) continue;
			result += HandPieceKanji[pt];
			if (count >= 2)
				result += KanjiNumbers[count];
		}
		return result.Length > 0 ? result : "なし";
	}

	/// <summary>
	/// 縦書き: 下から上へ描画（先手持ち駒用）。
	/// </summary>
	private static void DrawVerticalDown(Canvas canvas, string text, float x, float startY, Paint paint, float lineHeight)
	{
		float y = startY;
		foreach (char c in text)
		{
			canvas.DrawText(c.ToString(), x, y, paint);
			y -= lineHeight;
		}
	}

	/// <summary>
	/// 縦書き: 上から下へ描画（後手持ち駒用）。
	/// </summary>
	private static void DrawVerticalUp(Canvas canvas, string text, float x, float startY, Paint paint, float lineHeight)
	{
		float y = startY + lineHeight;
		foreach (char c in text)
		{
			canvas.DrawText(c.ToString(), x, y, paint);
			y += lineHeight;
		}
	}

	public static string SaveToFile(Bitmap bmp, string directory, string filename = "board.png")
	{
		if (!IODirectory.Exists(directory))
			IODirectory.CreateDirectory(directory);
		string path = IOPath.Combine(directory, filename);
		using (var fs = IOFile.Create(path))
		{
			bmp.Compress(Bitmap.CompressFormat.Png, 100, fs);
			fs.Flush();
		}
		return path;
	}
}
