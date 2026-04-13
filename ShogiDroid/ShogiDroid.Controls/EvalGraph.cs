using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Text;
using Android.Util;
using Android.Views;
using ShogiGUI;
using ShogiGUI.Events;
using ShogiLib;

namespace ShogiDroid.Controls;

public class EvalGraph : View
{
	private class GestureListener : GestureDetector.SimpleOnGestureListener
	{
		public override bool OnDoubleTap(MotionEvent e) => true;
	}

	private class ScaleGestureListener : ScaleGestureDetector.SimpleOnScaleGestureListener
	{
		public event EventHandler<ScaleGestureEventArgs> Scale;
		public override bool OnScale(ScaleGestureDetector detector)
		{
			Scale?.Invoke(this, new ScaleGestureEventArgs(detector));
			return true;
		}
	}

	public class ScaleGestureEventArgs : EventArgs
	{
		public ScaleGestureDetector Detector;
		public ScaleGestureEventArgs(ScaleGestureDetector detector) { Detector = detector; }
	}

	private const int DefaultDisplayMoves = 100;

	private SNotation notation;
	private float scaleFactor = 1f;

	private int graphStartX = 30;
	private int graphCenterY = 70;
	private int graphHeight = 140;
	private int graphStepWidth = 4;

	// 勝率変換の係数
	private double winRateCoeff = 750.0;

	private int dispMaxNum;
	private int maxNumber;
	private int scrollpos;
	private int scrollmax;
	private int lastNumber;

	private GestureDetector ges;
	private ScaleGestureDetector sges;
	private int scrollStartX;
	private bool scrolling;
	private float fontHeight;
	private bool dispComGraph = true;
	private bool liner = true;

	public SNotation Notation
	{
		set { notation = value; UpdateWinRateCoeff(); UpdateStepWidth(); UpdateScrollBar(); Invalidate(); }
	}

	public float ScaleFactor
	{
		get => scaleFactor;
		set => SetScale(value);
	}

	public bool DispComGraph
	{
		get => dispComGraph;
		set { if (dispComGraph != value) { dispComGraph = value; Invalidate(); } }
	}

	public bool GraphLiner
	{
		get => liner;
		set { liner = value; Invalidate(); }
	}

	public event EventHandler<GraphPositionEventArgs> SelectPosition;

	protected EvalGraph(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) { }

	public EvalGraph(Context context) : base(context) { init(); }

	public EvalGraph(Context context, IAttributeSet attrs) : base(context, attrs) { init(); }

	public EvalGraph(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle) { init(); }

	private void init()
	{
		SetBackgroundColor(ColorUtils.Get(Context, Resource.Color.graph_bg));
		ges = new GestureDetector(Context, new GestureListener());
		ges.DoubleTap += Ges_DoubleTap;
		var sgl = new ScaleGestureListener();
		sges = new ScaleGestureDetector(Context, sgl);
		sgl.Scale += SGes_Scale;
		graphStartX = (int)(Context.Resources.DisplayMetrics.Density * 20f);
	}

	protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
	{
		graphHeight = h;
		graphCenterY = h / 2;
		UpdateStepWidth();
		UpdateScrollBar();
	}

	public override bool OnTouchEvent(MotionEvent e)
	{
		if (ges.OnTouchEvent(e)) return true;
		sges.OnTouchEvent(e);
		switch (e.Action)
		{
		case MotionEventActions.Down:
			if (e.PointerCount == 1) { scrollStartX = (int)e.GetX(); scrolling = false; }
			return true;
		case MotionEventActions.Up:
			scrolling = false;
			return true;
		case MotionEventActions.Move:
			if (e.PointerCount == 1)
			{
				int xpos = (int)e.GetX();
				if (!scrolling && Math.Abs(xpos - scrollStartX) > Context.Resources.DisplayMetrics.Density * 20f)
					scrolling = true;
				if (scrolling)
				{
					int dx = (int)((scrollStartX - xpos) / Context.Resources.DisplayMetrics.Density);
					if (Math.Abs(dx) >= Math.Max(graphStepWidth, 1))
					{
						scrollpos += dx / Math.Max(graphStepWidth, 1);
						scrollpos = Math.Max(0, Math.Min(scrollpos, scrollmax));
						Invalidate();
						scrollStartX = xpos;
					}
					return true;
				}
			}
			break;
		}
		return base.OnTouchEvent(e);
	}

	protected override void OnDraw(Canvas g)
	{
		drawBase(g);
		if (dispComGraph) drawGraph(g);
		drawCursor(g);
	}

	// ======= 横軸ステップ幅: 100手基準、超えたら縮小 =======

	private void UpdateStepWidth()
	{
		int drawableWidth = Width - graphStartX;
		if (drawableWidth <= 0) drawableWidth = 300;
		int totalMoves = MaxNumber();
		int displayMoves = Math.Max(totalMoves, DefaultDisplayMoves);
		graphStepWidth = Math.Max(1, drawableWidth / displayMoves);
	}

	// ======= 縦軸: データ範囲に自動適応 =======

	/// <summary>
	/// 勝率変換の係数を設定から読み込む。
	/// </summary>
	private void UpdateWinRateCoeff()
	{
		int.TryParse(ShogiGUI.Settings.AppSettings.WinRateCoefficient, out int coeff);
		winRateCoeff = coeff > 0 ? coeff : 750.0;
	}

	/// <summary>
	/// 評価値(cp)をY座標に変換する。
	/// cp → 勝率(0〜100%) → Y座標にマッピング。
	/// 上端=100%(先手必勝)、中央=50%(互角)、下端=0%(後手必勝)。
	/// </summary>
	private int EvalToY(int eval)
	{
		double winRate = ShogiGUI.WinRateUtil.CpToWinRate(eval, winRateCoeff);
		// winRate: 0.0(後手勝ち) 〜 1.0(先手勝ち)
		// 上端=1.0, 下端=0.0 にマッピング
		int margin = (int)(graphHeight * 0.05); // 上下5%余白
		int drawArea = graphHeight - margin * 2;
		return margin + (int)((1.0 - winRate) * drawArea);
	}

	// ======= 描画: 折れ線グラフ（全ノードを走査してポイント配列を構築） =======

	private void drawBase(Canvas canvas)
	{
		using var paint = new Paint();
		using var textPaint = new TextPaint();
		textPaint.Color = ColorUtils.Get(Context, Resource.Color.graph_text);
		textPaint.TextSize = Context.Resources.GetDimension(Resource.Dimension.graph_font_small);
		textPaint.AntiAlias = true;
		using (var fm = textPaint.GetFontMetrics())
			fontHeight = fm.Descent - fm.Ascent;

		paint.Color = Color.Gray;
		canvas.DrawLine(0f, graphCenterY, Width, graphCenterY, paint);
		canvas.DrawLine(graphStartX, 0f, graphStartX, graphHeight, paint);

		// 横軸目盛り
		for (int i = graphStartX + graphStepWidth * 10; i < Width; i += graphStepWidth * 10)
			canvas.DrawLine(i, graphCenterY - 3, i, graphCenterY + 3, paint);

		// 縦軸ラベル（勝率%固定: 90%, 70%, 50%は中央線, 30%, 10%）
		textPaint.TextAlign = Paint.Align.Left;
		using var gridPaint = new Paint { Color = ColorUtils.Get(Context, Resource.Color.graph_grid), StrokeWidth = 1f, AntiAlias = true };

		int[] pctLabels = { 90, 70, 30, 10 };
		foreach (int pct in pctLabels)
		{
			int cpForPct = ShogiGUI.WinRateUtil.WinRateToCp(pct / 100.0, winRateCoeff);
			int y = EvalToY(cpForPct);
			if (y > 8 && y < graphHeight - 8)
			{
				canvas.DrawLine(graphStartX, y, Width, y, gridPaint);
				canvas.DrawLine(graphStartX - 2, y, graphStartX + 4, y, paint);
				canvas.DrawText($"{pct}%", 0f, y + fontHeight / 2f, textPaint);
			}
		}

		// 横軸手数ラベル
		textPaint.TextAlign = Paint.Align.Center;
		int sp = scrollpos;
		int offset = 10 - sp % 10;
		int labelNum = sp + offset;
		int labelX = graphStartX + graphStepWidth * offset;
		while (labelX < Width)
		{
			canvas.DrawText(labelNum.ToString(), labelX, graphHeight - 1, textPaint);
			labelX += graphStepWidth * 10;
			labelNum += 10;
		}
	}

	private void drawGraph(Canvas canvas)
	{
		if (notation == null) return;

		int sp = scrollpos;
		int totalMoves = MaxNumber();

		// 各手番号に対して1つの評価値のみを格納する配列
		// index=手番号, value=評価値(null=データなし)
		int?[] evalByMove = new int?[totalMoves + 1];
		MoveEval[] gradeByMove = new MoveEval[totalMoves + 1];

		foreach (MoveNode node in notation.MoveNodes)
		{
			if (!node.MoveType.IsMove()) continue;
			int num = node.Number;
			if (num < 0 || num > totalMoves) continue;

			// Scoreを優先、なければEvalを使う（1手に1つの値のみ）
			if (node.HasScore)
				evalByMove[num] = node.Score;
			else if (node.HasEval)
				evalByMove[num] = node.Eval;

			gradeByMove[num] = MoveEvalExtension.GetMoveEval(node, node.Parent);
		}

		using var linePaint = new Paint { Color = ColorUtils.Get(Context, Resource.Color.graph_line), StrokeWidth = 2f, AntiAlias = true };
		using var badPaint = new Paint { Color = Color.Red, AntiAlias = true };
		using var weakPaint = new Paint { Color = Color.Orange, AntiAlias = true };
		using var goodPaint = new Paint { Color = Color.Green, AntiAlias = true };

		// 折れ線グラフ描画: 連続する手同士のみを線で結ぶ
		int prevX = -1, prevY = -1;
		bool hasPrev = false;

		for (int num = 1; num <= totalMoves; num++)
		{
			if (!evalByMove[num].HasValue)
			{
				hasPrev = false; // データがない手で線を切る
				continue;
			}

			int x = graphStartX + (num - sp) * graphStepWidth;
			int y = EvalToY(evalByMove[num].Value);

			// 直前の手と線で結ぶ（直前の手=num-1にデータがある場合のみ）
			if (hasPrev)
			{
				canvas.DrawLine(prevX, prevY, x, y, linePaint);
			}

			prevX = x;
			prevY = y;
			hasPrev = true;
		}

		// 手分類ドット描画
		for (int num = 1; num <= totalMoves; num++)
		{
			if (!evalByMove[num].HasValue) continue;

			int x = graphStartX + (num - sp) * graphStepWidth;
			if (x < graphStartX || x > Width) continue;
			int y = EvalToY(evalByMove[num].Value);

			switch (gradeByMove[num])
			{
			case MoveEval.Blunder: canvas.DrawCircle(x, y, 4f, badPaint); break;
			case MoveEval.Bad: canvas.DrawCircle(x, y, 4f, weakPaint); break;
			case MoveEval.Best: canvas.DrawCircle(x, y, 4f, goodPaint); break;
			}
		}
	}

	private void drawCursor(Canvas canvas)
	{
		if (notation == null) return;
		lastNumber = notation.MoveCurrent.Number;
		if (lastNumber == 0 || lastNumber <= scrollpos) return;
		int x = graphStartX + (lastNumber - scrollpos) * graphStepWidth;
		using var paint = new Paint();
		paint.Color = ColorUtils.Get(Context, Resource.Color.graph_cursor);
		paint.SetPathEffect(new DashPathEffect(new float[] { 5f, 5f }, 0f));
		canvas.DrawLine(x, 0f, x, graphHeight, paint);
	}

	// ======= スクロール・ズーム =======

	private int MaxNumber() => notation?.Count ?? 0;

	private int NumberFromPosition(int x, int y)
	{
		int n = 0;
		if (x >= graphStartX) n = (x - graphStartX) / Math.Max(graphStepWidth, 1);
		return n + scrollpos;
	}

	private void EnsureVisible()
	{
		int old = scrollpos;
		if (notation == null || dispMaxNum > maxNumber)
			scrollpos = 0;
		else if (notation.MoveCurrent.Number < scrollpos)
			scrollpos = notation.MoveCurrent.Number / 10 * 10;
		else if (notation.MoveCurrent.Number > scrollpos + dispMaxNum)
			scrollpos = (notation.MoveCurrent.Number - dispMaxNum + 9) / 10 * 10;
		if (scrollpos != old) Invalidate();
	}

	private void SetScale(float scale)
	{
		scaleFactor = Math.Max(1f, Math.Min(scale, 4f));
		UpdateStepWidth();
		UpdateScrollBar();
		Invalidate();
	}

	private void UpdateScrollBar()
	{
		maxNumber = MaxNumber();
		dispMaxNum = (Width - graphStartX) / Math.Max(graphStepWidth, 1);
		if (dispMaxNum < 10) dispMaxNum = 10;
		if (notation == null || dispMaxNum > maxNumber)
			scrollmax = 0;
		else
			scrollmax = (maxNumber - dispMaxNum + 9) / 10 * 10;
	}

	public void UpdateNotation(NotationEventId op)
	{
		if (op != NotationEventId.COMMENT)
		{
			UpdateWinRateCoeff();
			UpdateStepWidth();
			UpdateScrollBar();
			EnsureVisible();
		}
		// 常に全体を再描画（折れ線グラフなので部分更新は不適切）
		Invalidate();
	}

	private void OnSelectPosition(int number)
	{
		SelectPosition?.Invoke(this, new GraphPositionEventArgs(number));
	}

	private void Ges_DoubleTap(object sender, GestureDetector.DoubleTapEventArgs e)
	{
		OnSelectPosition(NumberFromPosition((int)e.Event.GetX(), (int)e.Event.GetY()));
	}

	private void SGes_Scale(object sender, ScaleGestureEventArgs e)
	{
		SetScale(scaleFactor * e.Detector.ScaleFactor);
	}
}
