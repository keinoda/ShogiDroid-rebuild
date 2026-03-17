using System;
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
		public override bool OnDoubleTap(MotionEvent e)
		{
			return true;
		}
	}

	private class ScaleGestureListener : ScaleGestureDetector.SimpleOnScaleGestureListener
	{
		public event EventHandler<ScaleGestureEventArgs> Scale;

		public override bool OnScale(ScaleGestureDetector detector)
		{
			if (this.Scale != null)
			{
				this.Scale(this, new ScaleGestureEventArgs(detector));
			}
			return true;
		}
	}

	public class ScaleGestureEventArgs : EventArgs
	{
		public ScaleGestureDetector Detector;

		public ScaleGestureEventArgs(ScaleGestureDetector detector)
		{
			Detector = detector;
		}
	}

	private const float DefaultGraphStartX = 20f;

	private const float DefaultStepWidth = 2.6666667f;

	private const int DefaultStepHeight = 8;

	private const int StepHeightEval = 500;

	private const int BGColor = 9699539;

	private SNotation notation;

	private float scaleFactor = 1f;

	private int graphStartX = 30;

	private int graphCenterY = 70;

	private int graphHeight = 140;

	private int graphStepWidth = 4;

	private int graphStepHeight = 12;

	private int graphVerticalScale = 70;

	private static readonly int[] VerticalScale = new int[9] { 9999, 2000, 1000, 500, 0, -500, -1000, -2000, -9999 };

	private static readonly int[] VerticalScaleLiner = new int[7] { 3000, 2000, 1000, 0, -1000, -2000, -3000 };

	private int dispMaxNum;

	private int maxNumber;

	private int scrollpos;

	private int scrollmax;

	private int lastNumber;

	private GestureDetector ges;

	private ScaleGestureDetector sges;

	private int scrollStartX;

	private int scrollStartY;

	private bool scrolling;

	private float fontHeight;

	private bool dispComGraph = true;

	private bool liner = true;

	public SNotation Notation
	{
		set
		{
			notation = value;
			Invalidate();
		}
	}

	public float ScaleFactor
	{
		get
		{
			return scaleFactor;
		}
		set
		{
			SetScale(value);
		}
	}

	public bool DispComGraph
	{
		get
		{
			return dispComGraph;
		}
		set
		{
			if (dispComGraph != value)
			{
				dispComGraph = value;
				Invalidate();
			}
		}
	}

	public bool GraphLiner
	{
		get
		{
			return liner;
		}
		set
		{
			liner = value;
			Invalidate();
		}
	}

	public event EventHandler<GraphPositoinEventArgs> SelectPosition;

	protected EvalGraph(IntPtr javaReference, JniHandleOwnership transfer)
		: base(javaReference, transfer)
	{
	}

	public EvalGraph(Context context)
		: base(context)
	{
		init(null, 0);
	}

	public EvalGraph(Context context, IAttributeSet attrs)
		: base(context, attrs)
	{
		init(attrs, 0);
	}

	public EvalGraph(Context context, IAttributeSet attrs, int defStyle)
		: base(context, attrs, defStyle)
	{
		init(attrs, defStyle);
	}

	private void init(IAttributeSet attrs, int defStyle)
	{
		SetBackgroundColor(new Color(ContextCompat.GetColor(base.Context, Resource.Color.graph_bg)));
		ges = new GestureDetector(base.Context, new GestureListener());
		ges.DoubleTap += Ges_DoubleTap;
		ScaleGestureListener scaleGestureListener = new ScaleGestureListener();
		sges = new ScaleGestureDetector(base.Context, scaleGestureListener);
		scaleGestureListener.Scale += SGes_Scale;
		graphStartX = (int)(base.Context.Resources.DisplayMetrics.Density * 20f);
	}

	protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
	{
		UpdateScrollBar();
		graphHeight = h;
		graphCenterY = h / 2;
		graphVerticalScale = (int)((float)graphCenterY * scaleFactor);
	}

	public override bool OnTouchEvent(MotionEvent e)
	{
		if (ges.OnTouchEvent(e))
		{
			return true;
		}
		sges.OnTouchEvent(e);
		switch (e.Action)
		{
		case MotionEventActions.Down:
			if (e.PointerCount == 1)
			{
				Touch_ScrollStart((int)e.GetX(), (int)e.GetY());
			}
			return true;
		case MotionEventActions.Up:
			Touch_ScrollStop();
			return true;
		case MotionEventActions.Move:
			if (e.PointerCount == 1 && Touch_Scroll((int)e.GetX(), (int)e.GetY()))
			{
				return true;
			}
			break;
		}
		return base.OnTouchEvent(e);
	}

	protected override void OnDraw(Canvas g)
	{
		_ = DateTime.Now.Ticks;
		drawBase(g);
		if (dispComGraph)
		{
			drawGraph(g);
		}
		drawCursor(g);
	}

	private void drawBase(Canvas canvas)
	{
		using Paint paint = new Paint();
		using TextPaint textPaint = new TextPaint();
		textPaint.Color = Color.Black;
		textPaint.TextSize = base.Context.Resources.GetDimension(Resource.Dimension.graph_font_small);
		using (Paint.FontMetrics fontMetrics = textPaint.GetFontMetrics())
		{
			fontHeight = fontMetrics.Descent - fontMetrics.Ascent;
		}
		paint.Color = Color.Gray;
		canvas.DrawLine(0f, graphCenterY, base.Width, graphCenterY, paint);
		canvas.DrawLine(graphStartX, 0f, graphStartX, graphHeight, paint);
		for (int i = graphStartX + graphStepWidth * 10; i < base.Width; i += graphStepWidth * 10)
		{
			canvas.DrawLine(i, graphCenterY - 4, i, graphCenterY + 4, paint);
		}
		float num = fontHeight;
		textPaint.TextAlign = Paint.Align.Left;
		int[] array = (liner ? VerticalScaleLiner : VerticalScale);
		for (int j = 0; j < array.Length; j++)
		{
			int num2 = array[j];
			if (num2 != 0)
			{
				int num3 = graphCenterY + (int)((0.0 - EvalNormalize(num2)) * (double)graphVerticalScale);
				if (num3 > 8 && num3 < graphHeight - 8)
				{
					canvas.DrawLine(graphStartX - 2, num3, graphStartX + 4, num3, paint);
					canvas.DrawText(num2.ToString(), 0f, num3 + (int)(num / 2f), textPaint);
				}
			}
		}
		textPaint.TextAlign = Paint.Align.Center;
		int num4 = scrollpos;
		int num5 = 10 - num4 % 10;
		int num6 = num4 + num5;
		int num7 = graphStartX + graphStepWidth * num5;
		while (num7 < base.Width)
		{
			canvas.DrawText(num6.ToString(), num7, graphHeight - 1, textPaint);
			num7 += graphStepWidth * 10;
			num6 += 10;
		}
	}

	private void drawGraph(Canvas canvas)
	{
		if (notation == null)
		{
			return;
		}
		int num = graphStartX;
		int num2 = graphCenterY;
		int num3 = graphStartX;
		int num4 = graphCenterY;
		int num5 = graphStartX;
		int num6 = graphCenterY;
		int num7 = 0;
		int num8 = 0;
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		int num9 = 0;
		using Paint paint = new Paint();
		using Paint paint2 = new Paint();
		using Paint paint3 = new Paint();
		using Paint paint4 = new Paint();
		using Paint paint5 = new Paint();
		using Paint paint6 = new Paint();
		paint4.Color = Color.Black;
		paint4.StrokeWidth = 2f;
		paint5.Color = Color.White;
		paint5.StrokeWidth = 2f;
		paint6.Color = Color.Blue;
		paint6.StrokeWidth = 2f;
		paint.Color = Color.Red;
		paint2.Color = Color.Orange;
		paint3.Color = Color.Green;
		int num10 = scrollpos;
		foreach (MoveNode moveNode in notation.MoveNodes)
		{
			if (moveNode.Number < num10 || !moveNode.MoveType.IsMove())
			{
				continue;
			}
			double num11 = (0.0 - EvalNormalize(moveNode.Eval)) * (double)graphVerticalScale;
			int num12 = graphStartX + (moveNode.Number - num10) * graphStepWidth;
			int num13 = graphCenterY + (int)num11;
			if (moveNode.Turn == PlayerColor.Black)
			{
				if (!flag)
				{
					num4 = num13;
				}
				if ((num7 != 0 || num11 != 0.0) && moveNode.HasEval)
				{
					canvas.DrawLine(num3, num4, num12, num13, paint4);
				}
				num7 = (int)num11;
				num3 = num12;
				num4 = num13;
				flag = moveNode.HasEval;
			}
			else
			{
				if (!flag2)
				{
					num6 = num13;
				}
				if ((num8 != 0 || num11 != 0.0) && moveNode.HasEval)
				{
					canvas.DrawLine(num5, num6, num12, num13, paint5);
				}
				num5 = num12;
				num6 = num13;
				num8 = (int)num11;
				flag2 = moveNode.HasEval;
			}
			double num14 = (0.0 - EvalNormalize(moveNode.Score)) * (double)graphVerticalScale;
			num12 = graphStartX + (moveNode.Number - num10) * graphStepWidth;
			num13 = graphCenterY + (int)num14;
			if (!flag3)
			{
				num2 = num13;
			}
			if ((num9 != 0 || num14 != 0.0) && moveNode.HasScore)
			{
				canvas.DrawLine(num, num2, num12, num13, paint6);
			}
			num = num12;
			num2 = num13;
			num9 = (int)num14;
			flag3 = moveNode.HasScore;
		}
		foreach (MoveNode moveNode2 in notation.MoveNodes)
		{
			if (moveNode2.Number < num10)
			{
				continue;
			}
			MoveEval moveEval = MoveEvalExtention.GetMoveEval(moveNode2, moveNode2.Parent);
			if (moveNode2.MoveType.IsMove())
			{
				double num15 = (0.0 - EvalNormalize(moveNode2.Score)) * (double)graphVerticalScale;
				int num16 = graphStartX + (moveNode2.Number - num10) * graphStepWidth;
				int num17 = graphCenterY + (int)num15;
				switch (moveEval)
				{
				case MoveEval.Bad:
					canvas.DrawCircle(num16, num17, 4f, paint2);
					break;
				case MoveEval.Blunder:
					canvas.DrawCircle(num16, num17, 4f, paint);
					break;
				case MoveEval.Best:
					canvas.DrawCircle(num16, num17, 4f, paint3);
					break;
				}
			}
		}
	}

	private double EvalNormalize(int score)
	{
		if (liner)
		{
			return Math.Max(Math.Min((double)score * (double)graphStepHeight / (double)(500 * graphVerticalScale), 1.0), -1.0);
		}
		return Math.Max(Math.Min(2.0 / Math.PI * Math.Asin(Math.Max(Math.Min(2.0 / Math.PI * Math.Atan(0.002056167583560283 * (double)score), 1.0), -1.0)), 1.0), -1.0);
	}

	private void drawCursor(Canvas canvas)
	{
		if (notation == null)
		{
			return;
		}
		int num = scrollpos;
		lastNumber = notation.MoveCurrent.Number;
		if (notation.MoveCurrent.Number == 0 || notation.MoveCurrent.Number <= num)
		{
			return;
		}
		int num2 = graphStartX + (notation.MoveCurrent.Number - num) * graphStepWidth;
		using Paint paint = new Paint();
		paint.Color = Color.Black;
		paint.SetPathEffect(new DashPathEffect(new float[2] { 5f, 5f }, 0f));
		canvas.DrawLine(num2, 0f, num2, graphHeight, paint);
	}

	private int MaxNumber()
	{
		if (notation == null)
		{
			return 0;
		}
		return notation.Count;
	}

	private int NumberFromPosition(int x, int y)
	{
		int num = 0;
		if (x >= graphStartX)
		{
			num = (x - graphStartX) / graphStepWidth;
		}
		return num + scrollpos;
	}

	private void EnsureVisible()
	{
		int num = scrollpos;
		int num2 = scrollpos;
		if (notation == null || dispMaxNum > maxNumber)
		{
			scrollpos = 0;
		}
		else if (notation.MoveCurrent.Number < num)
		{
			scrollpos = notation.MoveCurrent.Number / 10 * 10;
		}
		else if (notation.MoveCurrent.Number > num + dispMaxNum)
		{
			scrollpos = (notation.MoveCurrent.Number - dispMaxNum + 9) / 10 * 10;
		}
		if (scrollpos != num2)
		{
			Invalidate();
		}
	}

	private void SetScale(float scale)
	{
		scaleFactor = Math.Max(1f, Math.Min(scale, 4f));
		graphStepWidth = (int)(scaleFactor * 2.6666667f * base.Context.Resources.DisplayMetrics.Density);
		graphStepHeight = (int)(scaleFactor * 8f * base.Context.Resources.DisplayMetrics.Density);
		graphVerticalScale = (int)((float)graphCenterY * scaleFactor);
		UpdateScrollBar();
		Invalidate();
	}

	private void UpdateScrollBar()
	{
		maxNumber = MaxNumber();
		dispMaxNum = (base.Width - graphStartX) / graphStepWidth;
		if (dispMaxNum < 10)
		{
			dispMaxNum = 10;
		}
		if (notation == null || dispMaxNum > maxNumber)
		{
			scrollmax = 0;
		}
		else
		{
			scrollmax = (maxNumber - dispMaxNum + 9) / 10 * 10;
		}
	}

	public void UpdateNotation(NotationEventId op)
	{
		if (op != NotationEventId.COMMENT)
		{
			UpdateScrollBar();
			EnsureVisible();
		}
		if (op == NotationEventId.NEXT || op == NotationEventId.PREV)
		{
			InvalidateCursor(notation.MoveCurrent.Number, lastNumber);
		}
		else
		{
			Invalidate();
		}
	}

	private void InvalidateCursor(int number, int oldnumber)
	{
		int num = scrollpos;
		if (oldnumber != 0)
		{
			int num2 = graphStartX + (oldnumber - num) * graphStepWidth;
			Invalidate(new Rect(num2, 0, num2 + 2, graphHeight));
		}
		if (number != 0)
		{
			int num3 = graphStartX + (number - num) * graphStepWidth;
			Invalidate(new Rect(num3, 0, num3 + 2, graphHeight));
		}
	}

	private void OnSelectPosition(int number)
	{
		if (this.SelectPosition != null)
		{
			this.SelectPosition(this, new GraphPositoinEventArgs(number));
		}
	}

	private void Ges_DoubleTap(object sender, GestureDetector.DoubleTapEventArgs e)
	{
		int number = NumberFromPosition((int)e.Event.GetX(), (int)e.Event.GetY());
		OnSelectPosition(number);
	}

	private void SGes_Scale(object sender, ScaleGestureEventArgs e)
	{
		float scale = scaleFactor * e.Detector.ScaleFactor;
		SetScale(scale);
	}

	private void Touch_ScrollStart(int xpos, int ypos)
	{
		scrollStartX = xpos;
		scrollStartY = ypos;
	}

	private bool Touch_Scroll(int xpos, int ypos)
	{
		if (notation == null || dispMaxNum > maxNumber)
		{
			return false;
		}
		if (!scrolling && (float)Math.Sqrt(Math.Pow(scrollStartX - xpos, 2.0) + Math.Pow(scrollStartY - ypos, 2.0)) > base.Context.Resources.DisplayMetrics.Density * 40f)
		{
			scrolling = true;
		}
		if (scrolling)
		{
			int num = (int)((float)(ypos - scrollStartY) / base.Context.Resources.DisplayMetrics.Density);
			int num2 = (int)((float)(scrollStartX - xpos) / base.Context.Resources.DisplayMetrics.Density);
			int num3 = ((Math.Abs(num2) <= Math.Abs(num)) ? num : num2);
			if (Math.Abs(num3) >= graphStepWidth)
			{
				scrollpos += num3 / graphStepWidth;
				scrollpos = Math.Max(0, Math.Min(scrollpos, scrollmax));
				Invalidate();
				scrollStartX = xpos;
				scrollStartY = ypos;
			}
		}
		return scrolling;
	}

	private void Touch_ScrollStop()
	{
		scrolling = false;
	}
}
