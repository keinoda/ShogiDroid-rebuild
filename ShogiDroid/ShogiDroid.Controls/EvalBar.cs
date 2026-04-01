using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Util;
using Android.Views;
using ShogiGUI;
using System;

namespace ShogiDroid.Controls;

/// <summary>
/// 形勢バー: 先手(下側=黒)と後手(上側=白)の勝率を縦のバーで表示する。
/// 放送やPC将棋GUIで一般的な形勢表示方式。
/// </summary>
public class EvalBar : View
{
	private Paint blackPaint_;
	private Paint whitePaint_;
	private Paint borderPaint_;
	private Paint textPaint_;
	private Paint textBgPaint_;

	private double winRate_ = 0.5; // 先手の勝率 (0.0-1.0)
	private string evalText_ = "";
	private bool isMate_ = false;
	private bool dispReverse_ = false;

	public EvalBar(Context context) : base(context)
	{
		InitPaints();
	}

	public EvalBar(Context context, IAttributeSet attrs) : base(context, attrs)
	{
		InitPaints();
	}

	public EvalBar(Context context, IAttributeSet attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
	{
		InitPaints();
	}

	protected EvalBar(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
	{
		InitPaints();
	}

	private void InitPaints()
	{
		blackPaint_ = new Paint { AntiAlias = true, Color = Color.ParseColor("#333333") };
		whitePaint_ = new Paint { AntiAlias = true, Color = Color.ParseColor("#EEEEEE") };
		borderPaint_ = new Paint { AntiAlias = true, Color = Color.ParseColor("#888888") };
		borderPaint_.SetStyle(Paint.Style.Stroke);
		borderPaint_.StrokeWidth = 1f;

		textPaint_ = new Paint { AntiAlias = true, Color = Color.White, TextAlign = Paint.Align.Center };
		textPaint_.SetTypeface(Typeface.DefaultBold);

		textBgPaint_ = new Paint { AntiAlias = true, Color = Color.ParseColor("#AA000000") };
	}

	/// <summary>
	/// 評価値を更新する。
	/// </summary>
	/// <param name="cp">評価値(centipawn)。先手有利が正。</param>
	/// <param name="isMate">詰みの場合 true。</param>
	/// <param name="matePly">詰み手数。正=先手勝ち、負=後手勝ち。</param>
	public void SetEval(int cp, bool isMate = false, int matePly = 0)
	{
		isMate_ = isMate;
		if (isMate)
		{
			winRate_ = matePly > 0 ? 1.0 : 0.0;
			if (matePly > 0)
			{
				evalText_ = $"詰{matePly}";
			}
			else if (matePly < 0)
			{
				evalText_ = $"被詰{-matePly}";
			}
			else
			{
				evalText_ = "詰";
			}
		}
		else
		{
			winRate_ = WinRateUtil.CpToWinRate(cp);
			evalText_ = WinRateUtil.FormatWinRate(cp, false, 0);
		}
		Invalidate();
	}

	/// <summary>
	/// 盤面反転に対応。
	/// </summary>
	public bool DispReverse
	{
		get => dispReverse_;
		set
		{
			if (dispReverse_ != value)
			{
				dispReverse_ = value;
				Invalidate();
			}
		}
	}

	/// <summary>
	/// 表示をリセットする（50%に戻す）。
	/// </summary>
	public void Reset()
	{
		winRate_ = 0.5;
		evalText_ = "";
		isMate_ = false;
		Invalidate();
	}

	protected override void OnDraw(Canvas canvas)
	{
		base.OnDraw(canvas);

		int w = Width;
		int h = Height;
		if (w <= 0 || h <= 0) return;

		// 先手の勝率に基づいて黒(下)と白(上)の領域を描画
		double rate = dispReverse_ ? (1.0 - winRate_) : winRate_;
		int blackHeight = (int)(h * rate);
		int whiteHeight = h - blackHeight;

		// 白(後手/上側)
		canvas.DrawRect(0, 0, w, whiteHeight, whitePaint_);
		// 黒(先手/下側)
		canvas.DrawRect(0, whiteHeight, w, h, blackPaint_);

		// 中央線(50%ライン)
		float centerY = h / 2f;
		var centerPaint = new Paint { Color = Color.ParseColor("#CC0000"), StrokeWidth = 1f };
		canvas.DrawLine(0, centerY, w, centerY, centerPaint);
		centerPaint.Dispose();

		// 枠線
		canvas.DrawRect(0, 0, w, h, borderPaint_);

		// 勝率テキスト（90度回転で境界線付近に表示）
		if (!string.IsNullOrEmpty(evalText_))
		{
			float textSize = 10f * Resources.DisplayMetrics.Density;
			textPaint_.TextSize = textSize;

			float textX = w / 2f;
			float textY = Math.Max(Math.Min(whiteHeight, h - textSize), textSize);

			// テキスト背景
			float textW = textPaint_.MeasureText(evalText_);
			var fm = textPaint_.GetFontMetrics();
			float textH = fm.Bottom - fm.Top;
			float bgPad = 2f * Resources.DisplayMetrics.Density;

			canvas.Save();
			canvas.Rotate(-90, textX, textY);

			float bgLeft = textX - textW / 2 - bgPad;
			float bgRight = textX + textW / 2 + bgPad;
			float bgTop = textY - textH / 2 - bgPad;
			float bgBottom = textY + textH / 2 + bgPad;
			canvas.DrawRect(bgLeft, bgTop, bgRight, bgBottom, textBgPaint_);
			canvas.DrawText(evalText_, textX, textY + (fm.Bottom - fm.Top) / 2 - fm.Bottom, textPaint_);

			canvas.Restore();
		}
	}

	protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
	{
		int desiredWidth = (int)(16 * Resources.DisplayMetrics.Density);
		int width = ResolveSize(desiredWidth, widthMeasureSpec);
		int height = MeasureSpec.GetSize(heightMeasureSpec);
		SetMeasuredDimension(width, height);
	}
}
