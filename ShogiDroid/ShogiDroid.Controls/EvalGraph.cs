using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Text;
using Android.Util;
using Android.Views;
using AndroidX.Core.Content;
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
	private const int StepHeightEval = 500;

	private SNotation notation;
	private float scaleFactor = 1f;

	private int graphStartX = 30;
	private int graphCenterY = 70;
	private int graphHeight = 140;
	private int graphStepWidth = 4;
	private int graphStepHeight = 12;
	private int graphVerticalScale = 70;

	private static readonly int[] VerticalScale = { 9999, 2000, 1000, 500, 0, -500, -1000, -2000, -9999 };
	private static readonly int[] VerticalScaleLiner = { 3000, 2000, 1000, 0, -1000, -2000, -3000 };

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
		set { notation = value; UpdateStepWidth(); UpdateScrollBar(); Invalidate(); }
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

	public event EventHandler<GraphPositoinEventArgs> SelectPosition;

	protected EvalGraph(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) { }

	public EvalGraph(Context context) : base(context) { init(); }

	public EvalGraph(Context context, IAttributeSet attrs) : base(context, attrs) { init(); }

	public EvalGraph(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle) { init(); }

	private void init()
	{
		SetBackgroundColor(new Color(ContextCompat.GetColor(Context, Resource.Color.graph_bg)));
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
		graphVerticalScale = (int)(graphCenterY * scaleFactor);
		graphStepHeight = (int)(scaleFactor * 8f * Context.Resources.DisplayMetrics.Density);
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

	// ======= 縦軸: 元のEvalNormalize（評価値ベースで統一） =======

	private double EvalNormalize(int score)
	{
		if (liner)
		{
			return Math.Max(Math.Min((double)score * graphStepHeight / (StepHeightEval * (double)graphVerticalScale), 1.0), -1.0);
		}
		return Math.Max(Math.Min(
			2.0 / Math.PI * Math.Asin(
				Math.Max(Math.Min(
					2.0 / Math.PI * Math.Atan(0.002056167583560283 * score),
				1.0), -1.0)
			), 1.0), -1.0);
	}

	private int EvalToY(int eval)
	{
		return graphCenterY + (int)(-EvalNormalize(eval) * graphVerticalScale);
	}

	// ======= 描画: 折れ線グラフ（全ノードを走査してポイント配列を構築） =======

	private void drawBase(Canvas canvas)
	{
		using var paint = new Paint();
		using var textPaint = new TextPaint();
		textPaint.Color = Color.Black;
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

		// 縦軸ラベル
		textPaint.TextAlign = Paint.Align.Left;
		int[] vscale = liner ? VerticalScaleLiner : VerticalScale;
		for (int j = 0; j < vscale.Length; j++)
		{
			int v = vscale[j];
			if (v == 0) continue;
			int y = EvalToY(v);
			if (y > 8 && y < graphHeight - 8)
			{
				canvas.DrawLine(graphStartX - 2, y, graphStartX + 4, y, paint);
				canvas.DrawText(v.ToString(), 0f, y + fontHeight / 2f, textPaint);
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

		// 全ノードからポイント配列を構築
		var evalPoints = new List<(int x, int y)>();   // Eval線
		var scorePoints = new List<(int x, int y)>();  // Score線(青)

		int sp = scrollpos;
		foreach (MoveNode node in notation.MoveNodes)
		{
			if (!node.MoveType.IsMove()) continue;
			int x = graphStartX + (node.Number - sp) * graphStepWidth;

			if (node.HasEval)
				evalPoints.Add((x, EvalToY(node.Eval)));

			if (node.HasScore)
				scorePoints.Add((x, EvalToY(node.Score)));
		}

		// 折れ線を描画
		using var evalPaint = new Paint { Color = Color.Black, StrokeWidth = 2f, AntiAlias = true };
		using var scorePaint = new Paint { Color = Color.Blue, StrokeWidth = 2f, AntiAlias = true };

		DrawPolyline(canvas, evalPoints, evalPaint);
		DrawPolyline(canvas, scorePoints, scorePaint);

		// 手分類ドット
		using var badPaint = new Paint { Color = Color.Red, AntiAlias = true };
		using var weakPaint = new Paint { Color = Color.Orange, AntiAlias = true };
		using var goodPaint = new Paint { Color = Color.Green, AntiAlias = true };

		foreach (MoveNode node in notation.MoveNodes)
		{
			if (!node.MoveType.IsMove() || !node.HasScore) continue;
			int x = graphStartX + (node.Number - sp) * graphStepWidth;
			if (x < graphStartX || x > Width) continue;
			int y = EvalToY(node.Score);

			MoveEval me = MoveEvalExtention.GetMoveEval(node, node.Parent);
			switch (me)
			{
			case MoveEval.Blunder: canvas.DrawCircle(x, y, 4f, badPaint); break;
			case MoveEval.Bad: canvas.DrawCircle(x, y, 4f, weakPaint); break;
			case MoveEval.Best: canvas.DrawCircle(x, y, 4f, goodPaint); break;
			}
		}
	}

	private void DrawPolyline(Canvas canvas, List<(int x, int y)> points, Paint paint)
	{
		for (int i = 1; i < points.Count; i++)
		{
			var p0 = points[i - 1];
			var p1 = points[i];
			// 画面外のセグメントはスキップ
			if (p0.x > Width && p1.x > Width) continue;
			if (p0.x < graphStartX && p1.x < graphStartX) continue;
			canvas.DrawLine(p0.x, p0.y, p1.x, p1.y, paint);
		}
	}

	private void drawCursor(Canvas canvas)
	{
		if (notation == null) return;
		lastNumber = notation.MoveCurrent.Number;
		if (lastNumber == 0 || lastNumber <= scrollpos) return;
		int x = graphStartX + (lastNumber - scrollpos) * graphStepWidth;
		using var paint = new Paint();
		paint.Color = Color.Black;
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
		graphStepHeight = (int)(scaleFactor * 8f * Context.Resources.DisplayMetrics.Density);
		graphVerticalScale = (int)(graphCenterY * scaleFactor);
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
			UpdateStepWidth();
			UpdateScrollBar();
			EnsureVisible();
		}
		// 常に全体を再描画（折れ線グラフなので部分更新は不適切）
		Invalidate();
	}

	private void OnSelectPosition(int number)
	{
		SelectPosition?.Invoke(this, new GraphPositoinEventArgs(number));
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
