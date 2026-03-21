using Orientation = Android.Content.Res.Orientation;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Runtime;
using Android.Widget;
using ShogiGUI;
using ShogiGUI.Events;
using ShogiLib;

namespace ShogiDroid.Controls.ShogiBoard;

public class ShogiBoard : View
{
	private enum BoardArea
	{
		NONE,
		BOARD,
		BOTTOM_STAND,
		TOP_STAND,
		BOX
	}

	private class BoardOperation
	{
		public int FromSquare;

		public Piece Piece;

		public BoardArea BoardArea;

		public int Xpos;

		public int Ypos;

		public int KeepX;

		public int KeepY;

		public bool Choices;

		private bool skipClick;

		private bool moving;

		public BoardOperation()
		{
			FromSquare = 0;
			Piece = Piece.NoPiece;
			Choices = false;
			skipClick = false;
			moving = false;
			BoardArea = BoardArea.NONE;
		}

		public void MoveStartFromStand(Piece piece, BoardArea boardarea)
		{
			Piece = piece;
			BoardArea = boardarea;
			skipClick = true;
			moving = true;
		}

		public void MoveStartFromBoard(Piece piece, int square)
		{
			Piece = piece;
			BoardArea = BoardArea.BOARD;
			FromSquare = square;
			skipClick = true;
			moving = true;
		}

		public void MoveStartFromBox(PieceType pt)
		{
			Piece = PieceExtensions.MakePiece(pt, PlayerColor.Black);
			BoardArea = BoardArea.BOX;
			skipClick = true;
			moving = true;
		}

		public bool IsSkipClick()
		{
			return skipClick;
		}

		public void ClearSkipClick()
		{
			skipClick = false;
		}

		private void CancelInner()
		{
			moving = false;
			Choices = false;
			skipClick = false;
		}

		public void End()
		{
			CancelInner();
		}

		public bool Cancel()
		{
			bool result = false;
			if (moving)
			{
				CancelInner();
				result = true;
			}
			return result;
		}

		public bool MoveEnd(int to_square)
		{
			if (!moving)
			{
				return false;
			}
			if (Choices)
			{
				return false;
			}
			if (BoardArea == BoardArea.BOARD && to_square == FromSquare)
			{
				CancelInner();
				return false;
			}
			return true;
		}

		public bool IsMoving()
		{
			return moving;
		}

		public void SetPosition(int x, int y)
		{
			if (!Choices)
			{
				Xpos = x;
				Ypos = y;
			}
		}

		public void KeepPosition()
		{
			KeepX = Xpos;
			KeepY = Ypos;
		}

		public void PromotionChoicStart()
		{
			Choices = true;
		}

		public bool IsMovingFromBoard(int square)
		{
			if (!moving)
			{
				return false;
			}
			if (FromSquare == square && BoardArea == BoardArea.BOARD)
			{
				return true;
			}
			return false;
		}

		public bool IsMovingFromStand(Piece piece)
		{
			if (!moving)
			{
				return false;
			}
			if ((BoardArea == BoardArea.BOTTOM_STAND || BoardArea == BoardArea.TOP_STAND) && Piece == piece)
			{
				return true;
			}
			return false;
		}

		public bool IsMovingFromBox(PieceType pt)
		{
			if (!moving)
			{
				return false;
			}
			if (BoardArea == BoardArea.BOX && Piece.TypeOf() == pt)
			{
				return true;
			}
			return false;
		}
	}

	private class GestureListener : GestureDetector.SimpleOnGestureListener
	{
		public override bool OnDoubleTap(MotionEvent e)
		{
			return true;
		}
	}

	private const int MaxWidth = 836;

	private const int MaxHeight = 656;

	private const int PieceStandOffset = 0;

	private const int BoardFixMargin = 5;

	private const int FontSizeMax = 20;

	private const int FontSizeMin = 8;

	private const int PieceFontSizeMax = 24;

	private const int PieceFontSizeMin = 8;

	private const int PieceMinWidth = 32;

	private const int PieceMinHeight = 35;

	private const int PieceDefWidth = 62;

	private const int PieceDefHeight = 68;

	private const int SquareDefWidth = 62;

	private const int SquareDefHeight = 68;

	private const int SquareMinWidth = 3;

	private const int SquareMinHeight = 3;

	private const int PieceSelectOffset = 8;

	private const int HilightToSquare = 220;

	private const int HilightFromSquare = 128;

	private const float ArrowLarge = 1f;

	private const float ArrowMiddle = 0.66f;

	private static readonly string[] ArabiaSuji = new string[10] { "０", "１", "２", "３", "４", "５", "６", "７", "８", "９" };

	private static readonly string[] KanSuji = new string[10] { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

	private static readonly string[] NumberMaru = new string[5] { "最", "次", "③", "④", "⑤" };

	private int screenOfsX;

	private int screenOfsY;

	private int screenWidth = 836;

	private int screenHeight = 656;

	private Paint piecePaint;

	private Paint piecePaintAlpha;

	private float coordFontSize = 9f;

	private TextPaint coordPaint;

	private float pieceFontSize = 9f;

	private TextPaint pieceFontPaint;

	private TextPaint pieceEdgePaint;

	private int pieceWidth = 60;

	private int pieceHeight = 66;

	private Rectangle boardRect = new Rectangle(107, 0, 646, 646);

	private int squareWidth = 62;

	private int squareHeight = 68;

	private int boardMarginX = 15;

	private int boardMarginY = 17;

	private Rect pieceStandTop = new Rect();

	private Rect pieceStandBottom = new Rect();

	private Rect pieceBoxRect = new Rect();

	private Bitmap offscreen;

	private BoardOperation op;

	private MoveData opeMoveData;

	private Paint imageAttrTras;

	private bool dispReverse;

	private PlayerColor bottomPlayerColor;

	private PlayerColor topPlayerColor = PlayerColor.White;

	private SNotation notation;

	private SPosition position;

	private HashKey hashkey;

	private MoveData movePrev;

	private bool[] playerType = new bool[2] { true, true };

	private bool busy;

	private bool beginUpdate;

	private bool isDrawOffsceen;

	private bool hintDisp;

	private HintInfo hintInfo;

	private bool nextMoveDisp;

	private List<MoveData> nextMoveList;

	private Bitmap boardBitmap;

	private Bitmap standBitmap;

	private Bitmap pieceBitmap;

	private Bitmap turnBitmap;

	private string komaFileName = string.Empty;

	private string boardFileName = string.Empty;

	private string standFileName = string.Empty;

	private string backgroundFileName = string.Empty;

	private Color lastPieceBackColor = Color.LightBlue;

	private Color ruleLineColor = Color.Black;

	private Color coordColor = Color.Black;

	private Paint highlightPaint;

	private Paint backPaint;

	private ShogiBoardMode shogiBoardMode;

	private OperationMode operationMode = OperationMode.Touch;

	private PieceMoveAnimation pieceAnimation;

	private AnimeSpeed animaSpeed = AnimeSpeed.Normal;

	private bool drawHintArrowFlag;

	private bool drawBookArrowFlag;

	private MoveStyle moveStyle;

	private AlertDialog menuPromotion;

	private System.Timers.Timer animTimer = new System.Timers.Timer();

	public bool DesignMode;

	private SynchronizationContext syncContext = SynchronizationContext.Current;

	private PieceBox pieceBox = new PieceBox();

	private GestureDetector ges;

	private int topinfo_x;

	private int topinfo_y;

	private int bottominfo_x;

	private int bottominfo_y;

	private int info_width = 130;

	private int info_height = 60;

	private long startime;

	public ShogiBoardMode ShogiBanMode
	{
		get
		{
			return shogiBoardMode;
		}
		set
		{
			if (shogiBoardMode != value)
			{
				shogiBoardMode = value;
				PieceAnimaStop();
				if (shogiBoardMode == ShogiBoardMode.NORMAL)
				{
					position = (SPosition)notation.Position.Clone();
					hashkey = position.HashKey;
				}
			}
		}
	}

	public SNotation Notation
	{
		set
		{
			notation = value;
			if (value != null)
			{
				position = (SPosition)notation.Position.Clone();
				hashkey = position.HashKey;
			}
			else
			{
				position = null;
			}
			if (shogiBoardMode == ShogiBoardMode.EDIT)
			{
				pieceBox.Init(position);
			}
			PieceAnimaStop();
			DrawOffscreenAll();
		}
	}

	public bool Reverse
	{
		get
		{
			return dispReverse;
		}
		set
		{
			if (dispReverse != value)
			{
				dispReverse = value;
				if (dispReverse)
				{
					bottomPlayerColor = PlayerColor.White;
					topPlayerColor = PlayerColor.Black;
				}
				else
				{
					bottomPlayerColor = PlayerColor.Black;
					topPlayerColor = PlayerColor.White;
				}
				op.Cancel();
				pieceAnimation.Resize(squareWidth);
				DrawOffscreenAll();
			}
		}
	}

	public bool PlayerHumanBlack
	{
		get
		{
			return playerType[0];
		}
		set
		{
			playerType[0] = value;
		}
	}

	public bool PlayerHumanWhite
	{
		get
		{
			return playerType[1];
		}
		set
		{
			playerType[1] = value;
		}
	}

	public bool Busy
	{
		get
		{
			return busy;
		}
		set
		{
			busy = value;
			if (busy)
			{
				menuPromotion.Dismiss();
				if (op.Cancel())
				{
					RedrawFromPiece();
				}
			}
		}
	}

	public Color LastPieceBackColor
	{
		get
		{
			return lastPieceBackColor;
		}
		set
		{
			if (lastPieceBackColor != value)
			{
				lastPieceBackColor = value;
				DrawOffscreenAll();
				Invalidate();
			}
		}
	}

	public bool HintDisp
	{
		get
		{
			return hintDisp;
		}
		set
		{
			hintDisp = value;
			Invalidate();
		}
	}

	public HintInfo HintInfo
	{
		get
		{
			return hintInfo;
		}
		set
		{
			hintInfo = value;
			if (hintDisp)
			{
				Invalidate();
			}
		}
	}

	public bool NextMoveDisp
	{
		get
		{
			return nextMoveDisp;
		}
		set
		{
			nextMoveDisp = value;
			InvalidateNextMoveArrow();
		}
	}

	public int ScreenWidth => screenWidth;

	public int ScreenHeight => screenHeight;

	/// <summary>
	/// 現在の盤面をBitmapとして取得する（画像出力用）。
	/// </summary>
	public Bitmap CaptureBoardBitmap()
	{
		if (offscreen == null) return null;
		return Bitmap.CreateBitmap(offscreen);
	}

	public OperationMode OperationMode
	{
		get
		{
			return operationMode;
		}
		set
		{
			operationMode = value;
		}
	}

	public bool IsAnimation => animaSpeed != AnimeSpeed.Off;

	public AnimeSpeed AnimaSpeed
	{
		get
		{
			return animaSpeed;
		}
		set
		{
			animaSpeed = value;
			pieceAnimation.SetAnimaSpeed(value);
		}
	}

	public bool IsPlaying => pieceAnimation.IsAnimation();

	public MoveStyle MoveStyle
	{
		get
		{
			return moveStyle;
		}
		set
		{
			if (moveStyle != value)
			{
				moveStyle = value;
				DrawOffscreenAll();
			}
		}
	}

	public int TopInfoX => topinfo_x;

	public int TopInfoY => topinfo_y;

	public int BottomInfoX => bottominfo_x;

	public int BottomInfoY => bottominfo_y;

	public int InfoWidth => info_width;

	public int InfoHeight => info_height;

	public event EventHandler<NotationEventArgs> NotationChanged;

	public event MakeMoveEventHandler MakeMoveEvent;

	public event EventHandler ScreenSizeChanged;

	public event EventHandler AnimationEnd;

	protected ShogiBoard(IntPtr javaReference, JniHandleOwnership transfer)
		: base(javaReference, transfer)
	{
	}

	public ShogiBoard(Context context)
		: base(context)
	{
		init(null, 0);
	}

	public ShogiBoard(Context context, IAttributeSet attrs)
		: base(context, attrs)
	{
		init(attrs, 0);
	}

	public ShogiBoard(Context context, IAttributeSet attrs, int defStyle)
		: base(context, attrs, defStyle)
	{
		init(attrs, defStyle);
	}

	private void init(IAttributeSet attrs, int defStyle)
	{
		TypedArray typedArray = base.Context.ObtainStyledAttributes(attrs, Resource.Styleable.ShogiBoard, defStyle, 0);
		DesignMode = typedArray.GetBoolean(0, defValue: false);
		shogiBoardMode = (ShogiBoardMode)typedArray.GetInteger(2, 0);
		typedArray.Recycle();
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Context);
		menuPromotion = builder.Create();
		View view = ((Activity)base.Context).LayoutInflater.Inflate(Resource.Layout.promotiondialog, null);
		menuPromotion.SetView(view);
		menuPromotion.DismissEvent += MenuPromotion_DismissEvent;
		((Button)view.FindViewById(Resource.Id.button_promotion)).Click += PromotionButton_Click;
		((Button)view.FindViewById(Resource.Id.button_unpromotion)).Click += UnpromotionButton_Click;
		animTimer.Interval = 10.0;
		animTimer.AutoReset = false;
		animTimer.Elapsed += animTimer_Tick;
		pieceAnimation = new PieceMoveAnimation(squareWidth, GetFromPosition, GetToPosition);
		pieceAnimation.MoveStartEvent += pieceAnim_MoveStartEvent;
		pieceAnimation.MoveEndEvent += pieceAnim_MoveEndEvent;
		pieceAnimation.PieceUpdateEvent += pieceAnim_PieceUpdateEvent;
		offscreen = Bitmap.CreateBitmap(screenWidth, screenHeight, Bitmap.Config.Argb8888);
		pieceBitmap = BitmapFactory.DecodeResource(base.Context.Resources, Resource.Drawable.koma1);
		boardBitmap = BitmapFactory.DecodeResource(base.Context.Resources, Resource.Drawable.board_bg);
		standBitmap = BitmapFactory.DecodeResource(base.Context.Resources, Resource.Drawable.komadai);
		turnBitmap = BitmapFactory.DecodeResource(base.Context.Resources, Resource.Drawable.dot16);
		notation = new SNotation();
		position = new SPosition();
		op = new BoardOperation();
		piecePaint = new Paint();
		piecePaintAlpha = new Paint();
		piecePaintAlpha.Alpha = 128;
		imageAttrTras = new Paint();
		imageAttrTras.Alpha = 128;
		opeMoveData = new MoveData();
		coordPaint = new TextPaint();
		coordPaint.TextSize = coordFontSize;
		pieceFontPaint = new TextPaint();
		pieceFontPaint.TextSize = pieceFontSize;
		pieceFontPaint.Color = Color.Black;
		pieceFontPaint.TextAlign = Paint.Align.Right;
		pieceEdgePaint = new TextPaint();
		pieceEdgePaint.AntiAlias = true;
		pieceEdgePaint.TextSize = pieceFontSize;
		pieceEdgePaint.SetStyle(Paint.Style.Stroke);
		pieceEdgePaint.StrokeWidth = 3f;
		pieceEdgePaint.Color = Color.White;
		pieceEdgePaint.TextAlign = Paint.Align.Right;
		backPaint = new Paint();
		highlightPaint = new Paint();
		highlightPaint.Color = lastPieceBackColor;
		ges = new GestureDetector(base.Context, new GestureListener());
		ges.DoubleTap += Ges_DoubleTap;
	}

	public void UpdateNotation(NotationEventId eventId)
	{
		if (eventId != NotationEventId.COMMENT)
		{
			menuPromotion.Dismiss();
			if (op.Cancel())
			{
				if (eventId == NotationEventId.MAKE_MOVE && !notation.MoveCurrent.MoveType.IsMove())
				{
					RedrawFromPiece();
				}
				else if (eventId == NotationEventId.NEXT || eventId == NotationEventId.PREV)
				{
					RedrawFromPiece();
				}
			}
		}
		if (animaSpeed != AnimeSpeed.Off)
		{
			switch (eventId)
			{
			case NotationEventId.MAKE_MOVE:
			case NotationEventId.NEXT:
				PieceAnimaStart(MoveDataDir.Next, notation.MoveCurrent);
				return;
			case NotationEventId.PREV:
				PieceAnimaStart(MoveDataDir.Prev, notation.MoveCurrent.ChildCurrent);
				return;
			case NotationEventId.COMMENT:
				return;
			}
			PieceAnimaStop();
			movePrev = new MoveData(position.MoveLast);
			position = (SPosition)notation.Position.Clone();
			hashkey = position.HashKey;
			if (shogiBoardMode == ShogiBoardMode.EDIT)
			{
				pieceBox.Init(position);
			}
			DrawOffscreenAll();
			return;
		}
		PieceAnimaStop();
		if (position.HashKey.Equals(notation.Position.HashKey) && shogiBoardMode == ShogiBoardMode.NORMAL)
		{
			return;
		}
		movePrev = new MoveData(position.MoveLast);
		position = (SPosition)notation.Position.Clone();
		hashkey = position.HashKey;
		switch (eventId)
		{
		case NotationEventId.MAKE_MOVE:
		case NotationEventId.NEXT:
			DrawOffscreenMoveNext();
			UpdateArrow();
			return;
		case NotationEventId.PREV:
			DrawOffscreenMovePrev();
			UpdateArrow();
			return;
		case NotationEventId.COMMENT:
			return;
		}
		if (shogiBoardMode == ShogiBoardMode.EDIT)
		{
			pieceBox.Init(position);
		}
		DrawOffscreenAll();
	}

	public void UpdateResource()
	{
		bool flag = false;
		if (pieceBitmap != ShogiBanResource.PieceBitmap)
		{
			flag = true;
			pieceBitmap = ShogiBanResource.PieceBitmap;
		}
		if (lastPieceBackColor != ShogiBanResource.LastPieceBackColor)
		{
			flag = true;
			lastPieceBackColor = ShogiBanResource.LastPieceBackColor;
		}
		if (ruleLineColor != ShogiBanResource.RuleLineColor)
		{
			flag = true;
			ruleLineColor = ShogiBanResource.RuleLineColor;
		}
		if (coordColor != ShogiBanResource.CoordColor)
		{
			flag = true;
			coordColor = ShogiBanResource.CoordColor;
		}
		if (flag)
		{
			DrawOffscreenAll();
			Invalidate();
		}
	}

	public void BeginUpdate()
	{
		beginUpdate = true;
	}

	public void EndUpdate()
	{
		beginUpdate = false;
		DrawOffscreenAll();
	}

	public void AnimationStop()
	{
		PieceAnimaStop();
	}

	private int ReverseRank(int rank)
	{
		return 9 - rank - 1;
	}

	private int ReverseFile(int file)
	{
		return 9 - file - 1;
	}

	private bool CanOperation()
	{
		if (busy)
		{
			return false;
		}
		return playerType[(int)notation.Position.Turn];
	}

	private bool CanDraw()
	{
		if (beginUpdate || !isDrawOffsceen || offscreen == null)
		{
			return false;
		}
		return true;
	}

	private void DrawOffscreenAll()
	{
		if (CanDraw())
		{
			_ = DateTime.Now.Ticks;
			using (Canvas canvas = new Canvas(offscreen))
			{
				DrawAll(canvas);
			}
			InvalidateRect(0, 0, offscreen.Width, offscreen.Height);
		}
	}

	private void InvalidateRect(int x, int y, int w, int h)
	{
		x += screenOfsX;
		y += screenOfsY;
		Invalidate(x, y, x + w - 1, y + h - 1);
	}

	private void InvalidateRect(Rect rect)
	{
		Invalidate(rect.Left + screenOfsX, rect.Top + screenOfsY, rect.Right + screenOfsX, rect.Bottom + screenOfsY);
	}

	private void InvalidateSquare(Canvas g, int square)
	{
		InvalidateRect(GetSquareRect(square));
	}

	private void InvalidateTurn()
	{
	}

	private void InvalidateNextMoveArrow()
	{
		if (notation.MoveCurrent.ChildCurrent != null)
		{
			Invalidate();
		}
		if (nextMoveList != null)
		{
			Invalidate();
			nextMoveList = null;
		}
	}

	private void DrawOffscreenMoveNext()
	{
		if (CanDraw())
		{
			using (Canvas g = new Canvas(offscreen))
			{
				startime = DateTime.Now.Ticks;
				DrawMoveNext(g);
			}
			InvalidateRect(op.Xpos - pieceWidth / 2, op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
		}
	}

	private void DrawOffscreenMovePrev()
	{
		if (!CanDraw())
		{
			return;
		}
		using Canvas g = new Canvas(offscreen);
		startime = DateTime.Now.Ticks;
		DrawMovePrev(g);
	}

	private void DrawAll(Canvas canvas)
	{
		canvas.DrawColor(Color.Red, PorterDuff.Mode.Clear);
		DrawBan(canvas);
		DrawBottomPieceStand(canvas);
		DrawTopPieceStand(canvas);
		DrawPieceBox(canvas);
	}

	private void DrawBan(Canvas g)
	{
		DrawBackgroundImage(g, boardBitmap, boardRect.Rect);
		int square_org_x = boardRect.X + boardMarginX;
		int square_org_y = boardRect.Y + boardMarginY;
		MoveData moveLast = position.MoveLast;
		if (moveLast.MoveType.IsMoveWithoutPass())
		{
			DrawBoardHilight(g, moveLast.ToSquare, 220);
			if (!moveLast.MoveType.HasFlag(MoveType.DropFlag))
			{
				DrawBoardHilight(g, moveLast.FromSquare, 128);
			}
		}
		if (position != null)
		{
			DrawPieces(g, square_org_x, square_org_y);
		}
		DrawBoardGridLine(g);
		DrawDanSujiString(g, square_org_x, square_org_y);
	}

	private void DrawDanSujiString(Canvas g, int square_org_x, int square_org_y)
	{
		coordPaint.Color = coordColor;
		coordPaint.TextAlign = Paint.Align.Center;
		if (dispReverse)
		{
			Paint.FontMetrics fontMetrics = coordPaint.GetFontMetrics();
			int num = boardRect.Bottom - (int)fontMetrics.Descent;
			int num2 = 0;
			int num3 = 1;
			while (num2 < 9)
			{
				int num4 = square_org_x + squareWidth * num2 + squareWidth / 2;
				g.DrawText(ArabiaSuji[num3], num4, num, coordPaint);
				num2++;
				num3++;
			}
		}
		else
		{
			Paint.FontMetrics fontMetrics2 = coordPaint.GetFontMetrics();
			int num5 = boardRect.Top + (int)(0f - fontMetrics2.Ascent);
			int num6 = 0;
			int num7 = 9;
			while (num6 < 9)
			{
				int num8 = square_org_x + squareWidth * num6 + squareWidth / 2;
				g.DrawText(ArabiaSuji[num7], num8, num5, coordPaint);
				num6++;
				num7--;
			}
		}
		if (dispReverse)
		{
			coordPaint.TextAlign = Paint.Align.Right;
			Paint.FontMetrics fontMetrics3 = coordPaint.GetFontMetrics();
			float num9 = fontMetrics3.Descent - fontMetrics3.Ascent;
			int num10 = 0;
			int num11 = 9;
			while (num10 < 9)
			{
				int num12 = square_org_y + squareHeight * num10 + squareHeight / 2 + (int)(num9 / 2f) - (int)fontMetrics3.Descent;
				if (moveStyle == MoveStyle.English)
				{
					g.DrawText(ArabiaSuji[num11], square_org_x, num12, coordPaint);
				}
				else
				{
					g.DrawText(KanSuji[num11], square_org_x, num12, coordPaint);
				}
				num10++;
				num11--;
			}
			return;
		}
		coordPaint.TextAlign = Paint.Align.Left;
		int num13 = square_org_x + squareWidth * 9 + 1;
		Paint.FontMetrics fontMetrics4 = coordPaint.GetFontMetrics();
		float num14 = fontMetrics4.Descent - fontMetrics4.Ascent;
		int num15 = 0;
		int num16 = 1;
		while (num15 < 9)
		{
			int num17 = square_org_y + squareHeight * num15 + squareHeight / 2 + (int)(num14 / 2f) - (int)fontMetrics4.Descent;
			if (moveStyle == MoveStyle.English)
			{
				g.DrawText(ArabiaSuji[num16], num13, num17, coordPaint);
			}
			else
			{
				g.DrawText(KanSuji[num16], num13, num17, coordPaint);
			}
			num15++;
			num16++;
		}
	}

	private void DrawPieces(Canvas g, int square_org_x, int square_org_y)
	{
		for (int i = 0; i < 9; i++)
		{
			for (int j = 0; j < 9; j++)
			{
				int num = square_org_x + j * squareWidth;
				int num2 = square_org_y + i * squareHeight;
				int x = num + (squareWidth - pieceWidth) / 2;
				int y = num2 + (squareHeight - pieceHeight) / 2;
				int num3 = ((!dispReverse) ? Square.Make(j, i) : Square.Make(ReverseFile(j), ReverseRank(i)));
				Piece piece = position.GetPiece(num3);
				if (piece != Piece.NoPiece && !pieceAnimation.IsMoving(num3, piece))
				{
					if (op.IsMovingFromBoard(num3))
					{
						DrawPieceTrans(g, piece, x, y, pieceWidth, pieceHeight);
					}
					else
					{
						DrawPiece(g, piece, x, y, pieceWidth, pieceHeight);
					}
				}
			}
		}
	}

	private void DrawTopPieceStand(Canvas canvas)
	{
		if (position == null)
		{
			return;
		}
		DrawBackgroundImage(canvas, standBitmap, pieceStandTop);
		int num = pieceStandTop.Left + (squareWidth - pieceWidth) / 2;
		int num2 = pieceStandTop.Top + pieceStandTop.Height() - squareHeight + (squareHeight - pieceHeight) / 2;
		Piece piece = Piece.WFU;
		PlayerColor playerColor = PlayerColor.White;
		if (dispReverse)
		{
			piece = Piece.BFU;
			playerColor = PlayerColor.Black;
		}
		MoveData moveLast = position.MoveLast;
		int num3 = 0;
		while (num3 < 7)
		{
			int num4 = position.GetHand(playerColor, piece.TypeOf());
			if (moveLast.Turn == playerColor && moveLast.MoveType.HasFlag(MoveType.DropFlag) && moveLast.Piece.ToHandIndex() == piece.ToHandIndex())
			{
				highlightPaint.Alpha = 128;
				canvas.DrawRect(new Rect(num, num2, num + pieceWidth - 1, num2 + pieceHeight - 1), highlightPaint);
			}
			if (num4 != 0)
			{
				if (pieceAnimation.IsMoving(piece) || op.IsMovingFromStand(piece))
				{
					num4--;
				}
				if (op.IsMovingFromStand(piece) && num4 == 0)
				{
					DrawPieceTrans(canvas, piece, num, num2, pieceWidth, pieceHeight);
				}
				else if (num4 != 0)
				{
					DrawPiece(canvas, piece, num, num2, pieceWidth, pieceHeight);
				}
				if (num4 > 1)
				{
					Paint.FontMetrics fontMetrics = pieceFontPaint.GetFontMetrics();
					float num5 = (float)pieceHeight - fontMetrics.Descent;
					int num6 = pieceWidth * 9 / 10;
					canvas.DrawText(num4.ToString(), num + num6, (float)num2 + num5, pieceEdgePaint);
					canvas.DrawText(num4.ToString(), num + num6, (float)num2 + num5, pieceFontPaint);
				}
			}
			num += squareWidth;
			num3++;
			piece++;
		}
	}

	private void DrawBottomPieceStand(Canvas canvas)
	{
		if (position == null)
		{
			return;
		}
		DrawBackgroundImage(canvas, standBitmap, pieceStandBottom);
		int top = pieceStandBottom.Top;
		int num = pieceStandBottom.Left + (squareWidth - pieceWidth) / 2;
		Piece piece = Piece.BHI;
		PlayerColor playerColor = PlayerColor.Black;
		if (dispReverse)
		{
			piece = Piece.WHI;
			playerColor = PlayerColor.White;
		}
		MoveData moveLast = position.MoveLast;
		int num2 = 0;
		while (num2 < 7)
		{
			int num3 = position.GetHand(playerColor, piece.ToHandIndex());
			if (moveLast.Turn == playerColor && moveLast.MoveType.HasFlag(MoveType.DropFlag) && moveLast.Piece.ToHandIndex() == piece.ToHandIndex())
			{
				highlightPaint.Alpha = 128;
				canvas.DrawRect(new Rect(num, top, num + pieceWidth - 1, top + pieceHeight - 1), highlightPaint);
			}
			if (num3 != 0)
			{
				if (pieceAnimation.IsMoving(piece) || op.IsMovingFromStand(piece))
				{
					num3--;
				}
				if (op.IsMovingFromStand(piece) && num3 == 0)
				{
					DrawPieceTrans(canvas, piece, num, top, pieceWidth, pieceHeight);
				}
				else if (num3 != 0)
				{
					DrawPiece(canvas, piece, num, top, pieceWidth, pieceHeight);
				}
				if (num3 > 1)
				{
					float num4 = 0f - pieceFontPaint.GetFontMetrics().Ascent;
					int num5 = pieceWidth * 9 / 10;
					canvas.DrawText(num3.ToString(), num + num5, (float)top + num4, pieceEdgePaint);
					canvas.DrawText(num3.ToString(), num + num5, (float)top + num4, pieceFontPaint);
				}
			}
			num += squareWidth;
			num2++;
			piece--;
		}
	}

	private void DrawPieceBox(Canvas canvas)
	{
		if (position == null || shogiBoardMode != ShogiBoardMode.EDIT)
		{
			return;
		}
		using (Paint paint = new Paint())
		{
			paint.Color = Color.Argb(255, 255, 209, 122);
			canvas.DrawRect(pieceBoxRect, paint);
		}
		int num = pieceBoxRect.Top;
		int left = pieceBoxRect.Left;
		PieceType pieceType = PieceType.OU;
		while ((int)pieceType >= 1)
		{
			int num2 = pieceBox.Box[(uint)pieceType];
			Piece piece = PieceExtensions.MakePiece(pieceType, bottomPlayerColor);
			if (num2 != 0)
			{
				if (op.IsMovingFromBox(pieceType))
				{
					num2--;
				}
				if (op.IsMovingFromBox(pieceType) && num2 == 0)
				{
					DrawPieceTrans(canvas, piece, left, num, pieceWidth, pieceHeight);
				}
				else if (num2 != 0)
				{
					DrawPiece(canvas, piece, left, num, pieceWidth, pieceHeight);
				}
				if (num2 > 1)
				{
					float num3 = 0f - pieceFontPaint.GetFontMetrics().Ascent;
					int num4 = pieceWidth * 9 / 10;
					canvas.DrawText(num2.ToString(), left + num4, (float)num + num3, pieceEdgePaint);
					canvas.DrawText(num2.ToString(), left + num4, (float)num + num3, pieceFontPaint);
				}
			}
			num += squareHeight;
			pieceType--;
		}
	}

	private void DrawTurn(Canvas canvas)
	{
		int num = pieceWidth / 3;
		int num2 = num;
		Rect rect = ((bottomPlayerColor != position.Turn) ? new Rect(pieceStandTop.Right, pieceStandTop.Bottom - num2, pieceStandTop.Right + num, pieceStandTop.Bottom) : new Rect(pieceStandBottom.Left - num, pieceStandBottom.Top, pieceStandBottom.Left, pieceStandBottom.Top + num2));
		rect.Left += screenOfsX;
		rect.Right += screenOfsX;
		rect.Top += screenOfsY;
		rect.Bottom += screenOfsY;
		canvas.DrawBitmap(turnBitmap, new Rect(0, 0, turnBitmap.Width, turnBitmap.Height), rect, piecePaint);
	}

	private void DrawAnimation(Canvas g)
	{
		if (pieceAnimation.IsAnimation() && pieceAnimation.MoveData.MoveType != MoveType.Pass)
		{
			DrawPiece(g, pieceAnimation.MoveData.Piece, screenOfsX + pieceAnimation.NowPoint.X, screenOfsY + pieceAnimation.NowPoint.Y, pieceWidth, pieceHeight);
		}
	}

	private void DrawBackgroundImage(Canvas g, Bitmap bmp, Rect rect)
	{
		int num = bmp.Width;
		int num2 = bmp.Height;
		if (rect.Width() <= num)
		{
			num = rect.Width();
		}
		if (rect.Height() <= num2)
		{
			num2 = rect.Height();
		}
		g.DrawBitmap(bmp, new Rect(0, 0, num - 1, num2 - 1), rect, piecePaint);
	}

	private void DrawBoardGridLine(Canvas canvas)
	{
		int num = boardRect.X + boardMarginX;
		int num2 = boardRect.Y + boardMarginY;
		Rect rect = new Rect(num, num2, num + squareWidth * 9 - 1, num2 + squareHeight * 9 - 1);
		using (Paint paint = new Paint())
		{
			paint.Color = ruleLineColor;
			paint.StrokeWidth = 2f;
			paint.SetStyle(Paint.Style.Stroke);
			canvas.DrawRect(rect, paint);
		}
		using (Paint paint2 = new Paint())
		{
			paint2.Color = ruleLineColor;
			for (int i = 1; i < 9; i++)
			{
				int num3 = num2 + i * squareHeight;
				canvas.DrawLine(num, num3, num + rect.Width(), num3, paint2);
			}
			for (int j = 1; j < 9; j++)
			{
				int num4 = num + j * squareWidth;
				canvas.DrawLine(num4, num2, num4, num2 + rect.Height(), paint2);
			}
		}
		using Paint paint3 = new Paint();
		paint3.Color = ruleLineColor;
		int num5 = squareWidth / 11;
		if ((num5 & 1) == 1)
		{
			num5++;
		}
		paint3.AntiAlias = true;
		canvas.DrawCircle(num + squareWidth * 3, num2 + squareHeight * 3, num5, paint3);
		canvas.DrawCircle(num + squareWidth * 3, num2 + squareHeight * 6, num5, paint3);
		canvas.DrawCircle(num + squareWidth * 6, num2 + squareHeight * 3, num5, paint3);
		canvas.DrawCircle(num + squareWidth * 6, num2 + squareHeight * 6, num5, paint3);
		paint3.AntiAlias = false;
	}

	private Rect GetSquareRect(int square)
	{
		int num = boardRect.X + boardMarginX;
		int num2 = boardRect.Y + boardMarginY;
		int num3 = square.RankOf();
		int num4 = square.FileOf();
		if (dispReverse)
		{
			num3 = ReverseRank(num3);
			num4 = ReverseFile(num4);
		}
		int num5 = num + num4 * squareWidth;
		int num6 = num2 + num3 * squareHeight;
		return new Rect(num5, num6, num5 + squareWidth - 1, num6 + squareHeight - 1);
	}

	private void DrawBanBG(Canvas g, int square)
	{
		Rect squareRect = GetSquareRect(square);
		g.Save();
		g.ClipRect(squareRect);
		DrawBackgroundImage(g, boardBitmap, boardRect.Rect);
		g.Restore();
	}

	private void DrawBoardHilight(Canvas g, int square, int alpha)
	{
		if (!pieceAnimation.IsAnimation())
		{
			highlightPaint.Alpha = alpha;
			g.DrawRect(GetSquareRect(square), highlightPaint);
		}
	}

	private void RedrawFromPiece()
	{
		if (op.BoardArea == BoardArea.BOTTOM_STAND || op.BoardArea == BoardArea.TOP_STAND)
		{
			if (op.Piece.ColorOf() == bottomPlayerColor)
			{
				DrawOffscreenBottomStand();
			}
			else
			{
				DrawOffscreenTopStand();
			}
			InvalidateRect(op.Xpos - pieceWidth / 2, op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
		}
		else
		{
			DrawOffscreenSquare(op.FromSquare);
			InvalidateRect(op.Xpos - pieceWidth / 2, op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
		}
	}

	private void DrawMoveNext(Canvas g)
	{
		MoveData moveLast = position.MoveLast;
		MoveData moveData = movePrev;
		if (moveData.MoveType.IsMoveWithoutPass())
		{
			DrawBanBG(g, moveData.ToSquare);
			DrawPiece(g, moveData.ToSquare);
			InvalidateSquare(g, moveData.ToSquare);
			if (!moveData.MoveType.HasFlag(MoveType.DropFlag))
			{
				DrawBanBG(g, moveData.FromSquare);
				DrawPiece(g, moveData.FromSquare);
				InvalidateSquare(g, moveData.FromSquare);
			}
		}
		if (moveLast.MoveType.IsMoveWithoutPass())
		{
			DrawBanBG(g, moveLast.ToSquare);
			DrawBoardHilight(g, moveLast.ToSquare, 220);
			DrawPiece(g, moveLast.ToSquare);
			InvalidateSquare(g, moveLast.ToSquare);
			if (!moveLast.MoveType.HasFlag(MoveType.DropFlag))
			{
				DrawBanBG(g, moveLast.FromSquare);
				DrawBoardHilight(g, moveLast.FromSquare, 128);
				InvalidateSquare(g, moveLast.FromSquare);
			}
		}
		if (moveLast.MoveType.HasFlag(MoveType.DropFlag) || moveLast.MoveType.HasFlag(MoveType.Capture) || moveData.MoveType.HasFlag(MoveType.DropFlag))
		{
			DrawTopPieceStand(g);
			DrawBottomPieceStand(g);
			InvalidateRect(pieceStandTop);
			InvalidateRect(pieceStandBottom);
		}
		DrawBoardGridLine(g);
	}

	private void DrawMovePrev(Canvas g)
	{
		MoveData[] obj = new MoveData[2] { position.MoveLast, movePrev };
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		bool flag = false;
		bool flag2 = false;
		int num = 0;
		MoveData[] array = obj;
		foreach (MoveData moveData in array)
		{
			if (moveData != null && moveData.MoveType.IsMoveWithoutPass())
			{
				if (!dictionary.ContainsKey(moveData.ToSquare))
				{
					dictionary[moveData.ToSquare] = num;
				}
				if (moveData.MoveType.HasFlag(MoveType.DropFlag))
				{
					if (moveData.Turn == bottomPlayerColor)
					{
						flag2 = true;
					}
					else
					{
						flag = true;
					}
				}
				else
				{
					if (!dictionary.ContainsKey(moveData.FromSquare))
					{
						dictionary[moveData.FromSquare] = num + 1;
					}
					if (moveData.MoveType.HasFlag(MoveType.Capture))
					{
						if (moveData.Turn == bottomPlayerColor)
						{
							flag2 = true;
						}
						else
						{
							flag = true;
						}
					}
				}
			}
			num += 2;
		}
		foreach (KeyValuePair<int, int> item in dictionary)
		{
			DrawBanBG(g, item.Key);
			if (item.Value == 0)
			{
				DrawBoardHilight(g, item.Key, 220);
			}
			else if (item.Value == 1)
			{
				DrawBoardHilight(g, item.Key, 128);
			}
			DrawPiece(g, item.Key);
			InvalidateSquare(g, item.Key);
		}
		if (flag2)
		{
			DrawBottomPieceStand(g);
			InvalidateRect(pieceStandBottom);
		}
		if (flag)
		{
			DrawTopPieceStand(g);
			InvalidateRect(pieceStandTop);
		}
		DrawBoardGridLine(g);
	}

	private void DrawOffscreenBottomStand()
	{
		if (CanDraw())
		{
			startime = DateTime.Now.Ticks;
			using (Canvas canvas = new Canvas(offscreen))
			{
				DrawBottomPieceStand(canvas);
			}
			InvalidateRect(pieceStandBottom);
		}
	}

	private void DrawOffscreenTopStand()
	{
		if (CanDraw())
		{
			startime = DateTime.Now.Ticks;
			using (Canvas canvas = new Canvas(offscreen))
			{
				DrawTopPieceStand(canvas);
			}
			InvalidateRect(pieceStandTop);
		}
	}

	private void DrawOffscreenSquare(int square)
	{
		if (!CanDraw())
		{
			return;
		}
		startime = DateTime.Now.Ticks;
		using Canvas canvas = new Canvas(offscreen);
		canvas.ClipRect(GetSquareRect(square));
		DrawBanBG(canvas, square);
		DrawPiece(canvas, square);
		DrawBoardGridLine(canvas);
		InvalidateSquare(canvas, square);
	}

	private void DrawOffscreenPieceBox()
	{
		if (CanDraw())
		{
			startime = DateTime.Now.Ticks;
			using (Canvas canvas = new Canvas(offscreen))
			{
				DrawPieceBox(canvas);
			}
			InvalidateRect(pieceBoxRect);
		}
	}

	private void UpdateArrow()
	{
		bool flag = false;
		bool flag2 = false;
		if (hintInfo != null && hintDisp && hintInfo.MoveCurrent != null)
		{
			flag = hintInfo.IsEqual(position.HashKey, position.MoveLast);
		}
		if (flag || drawHintArrowFlag != flag || flag2 || drawBookArrowFlag != flag2)
		{
			Invalidate();
		}
		if (nextMoveDisp || nextMoveList != null)
		{
			InvalidateNextMoveArrow();
		}
	}

	private int ImageFromPiece(Piece piece)
	{
		int num = (int)piece;
		if (piece == Piece.BOU)
		{
			num += 8;
		}
		if (dispReverse)
		{
			num = (piece.HasFlag(Piece.WhiteFlag) ? (num - 16) : (num + 16));
		}
		return num - 1;
	}

	private void DrawPiece(Canvas g, Piece piece, int x, int y, int width, int height)
	{
		Rect src = PieceRectFromPiece(piece);
		g.DrawBitmap(pieceBitmap, src, new Rect(x, y, x + width - 1, y + height - 1), piecePaint);
	}

	private void DrawPieceTrans(Canvas g, Piece piece, int x, int y, int width, int height)
	{
		Rect src = PieceRectFromPiece(piece);
		g.DrawBitmap(pieceBitmap, src, new Rect(x, y, x + width - 1, y + height - 1), piecePaintAlpha);
	}

	private void DrawPiece(Canvas g, int square)
	{
		Piece piece = position.GetPiece(square);
		if (piece == Piece.NoPiece)
		{
			return;
		}
		int num = boardRect.X + boardMarginX;
		int num2 = boardRect.Y + boardMarginY;
		int num3 = square.RankOf();
		int num4 = square.FileOf();
		if (dispReverse)
		{
			num3 = ReverseRank(num3);
			num4 = ReverseRank(num4);
		}
		int num5 = num + num4 * squareWidth;
		int num6 = num2 + num3 * squareHeight;
		int x = num5 + (squareWidth - pieceWidth) / 2;
		int y = num6 + (squareHeight - pieceHeight) / 2;
		if (!pieceAnimation.IsMoving(square, piece))
		{
			if (op.IsMovingFromBoard(square))
			{
				DrawPieceTrans(g, piece, x, y, pieceWidth, pieceHeight);
			}
			else
			{
				DrawPiece(g, piece, x, y, pieceWidth, pieceHeight);
			}
		}
	}

	private Rect PieceRectFromPiece(Piece piece)
	{
		int num = pieceBitmap.Width / 8;
		int num2 = pieceBitmap.Height / 4;
		int num3 = (int)(piece - 1);
		if (piece == Piece.BOU)
		{
			num3 += 8;
		}
		if (dispReverse)
		{
			num3 = (piece.IsWhite() ? (num3 - 16) : (num3 + 16));
		}
		int num4 = pieceBitmap.Width - num - num * (num3 % 8);
		int num5 = num2 * (num3 / 8);
		return new Rect(num4, num5, num4 + num - 1, num5 + num2 - 1);
	}

	private int GetBoardPos(int mouse_x, int mouse_y)
	{
		if (notation == null)
		{
			return 81;
		}
		if (mouse_x < boardRect.X + boardMarginX || mouse_x > boardRect.Right - boardMarginX)
		{
			return 81;
		}
		if (mouse_y < boardRect.Y + boardMarginY || mouse_y > boardRect.Bottom - boardMarginY)
		{
			return 81;
		}
		int num = (mouse_x - (boardRect.X + boardMarginX)) / squareWidth;
		int num2 = (mouse_y - (boardRect.Y + boardMarginY)) / squareHeight;
		if (num2 < 0 || num2 >= 9)
		{
			return 81;
		}
		if (num < 0 || num >= 9)
		{
			return 81;
		}
		if (dispReverse)
		{
			num = ReverseFile(num);
			num2 = ReverseRank(num2);
		}
		return Square.Make(num, num2);
	}

	private PieceType GetPieceFromPieceStandBottom(int mouse_x, int mouse_y)
	{
		if (notation == null)
		{
			return PieceType.NoPieceType;
		}
		if (!pieceStandBottom.Contains(mouse_x, mouse_y))
		{
			return PieceType.NoPieceType;
		}
		int num = (mouse_x - pieceStandBottom.Left) / squareWidth;
		num = 7 - num;
		if (num < 1)
		{
			return PieceType.NoPieceType;
		}
		return (PieceType)num;
	}

	private PieceType GetPieceFromPieceStandTop(int mouse_x, int mouse_y)
	{
		if (notation == null)
		{
			return PieceType.NoPieceType;
		}
		if (!pieceStandTop.Contains(mouse_x, mouse_y))
		{
			return PieceType.NoPieceType;
		}
		int num = (mouse_x - pieceStandTop.Left) / squareWidth;
		num = 1 + num;
		if (num > 7)
		{
			return PieceType.NoPieceType;
		}
		return (PieceType)num;
	}

	private PieceType GetPieceFromPieceBox(int mouse_x, int mouse_y)
	{
		if (notation == null)
		{
			return PieceType.NoPieceType;
		}
		if (!pieceBoxRect.Contains(mouse_x, mouse_y))
		{
			return PieceType.NoPieceType;
		}
		int num = (mouse_y - pieceBoxRect.Top) / squareHeight;
		num = 8 - num;
		if (num < 1)
		{
			return PieceType.NoPieceType;
		}
		return (PieceType)num;
	}

	private Point GetSquarePosCenter(int square)
	{
		Point boardPosFromSquare = GetBoardPosFromSquare(square);
		boardPosFromSquare.X += pieceWidth / 2;
		boardPosFromSquare.Y += pieceHeight / 2;
		return boardPosFromSquare;
	}

	private Point GetBoardPosFromSquare(int square)
	{
		int num = boardRect.X + boardMarginX;
		int num2 = boardRect.Y + boardMarginY;
		int num3 = square.RankOf();
		int num4 = square.FileOf();
		if (dispReverse)
		{
			num3 = ReverseRank(num3);
			num4 = ReverseRank(num4);
		}
		int num5 = num + num4 * squareWidth;
		int num6 = num2 + num3 * squareHeight;
		int x = num5 + (squareWidth - pieceWidth) / 2;
		int y = num6 + (squareHeight - pieceHeight) / 2;
		return new Point(x, y);
	}

	private Point GetStandPosCenter(Piece piece)
	{
		Point standPos = GetStandPos(piece);
		standPos.X += pieceWidth / 2;
		standPos.Y += pieceHeight / 2;
		return standPos;
	}

	private Point GetStandPos(Piece piece)
	{
		int top;
		int num;
		if (bottomPlayerColor == piece.ColorOf())
		{
			num = pieceStandBottom.Left + (squareWidth - pieceWidth) / 2;
			top = pieceStandBottom.Top;
			int num2 = Piece.BHI.ToHandIndex() - piece.ToHandIndex();
			num += squareWidth * num2;
		}
		else
		{
			num = pieceStandTop.Left + (squareWidth - pieceWidth) / 2;
			top = pieceStandTop.Top;
			int num3 = piece.ToHandIndex() - Piece.BFU.ToHandIndex();
			num += squareWidth * num3;
		}
		return new Point(num, top);
	}

	private Point GetPieceBoxPos(PieceType pt)
	{
		int left = pieceBoxRect.Left;
		int top = pieceBoxRect.Top;
		int num = (int)(8 - pt);
		top += squareHeight * num;
		return new Point(left, top);
	}

	private Point GetPieceBoxCenter(PieceType pt)
	{
		Point pieceBoxPos = GetPieceBoxPos(pt);
		pieceBoxPos.X += pieceWidth / 2;
		pieceBoxPos.Y += pieceHeight / 2;
		return pieceBoxPos;
	}

	private void DrawHintArrows(Canvas g)
	{
		if (hintInfo.MoveCurrent != null && hintInfo.IsEqual(position.HashKey, position.MoveLast))
		{
			drawHintArrowFlag = true;
			for (int i = 1; i < hintInfo.MoveArray.Length; i++)
			{
				DrawHintArrow(g, hintInfo.MoveArray[i], 1, Color.DarkOrange, 0.66f);
				DrawHintNumber(g, hintInfo.MoveArray[i], 1, i);
			}
			DrawHintArrow(g, hintInfo.MoveArray[0], 1, Color.Red, 1f);
			DrawHintNumber(g, hintInfo.MoveArray[0], 1, 0);
		}
	}

	private void DrawHintArrow(Canvas g, List<MoveData> moves, int max, Color color, float penwidth)
	{
		int num = max;
		if (num > moves.Count)
		{
			num = moves.Count;
		}
		using Paint paint = new Paint();
		using Paint paint2 = new Paint();
		paint.Color = color;
		paint.Alpha = 128;
		paint2.Color = new Color(color.R / 2, color.G / 2, color.B / 2);
		paint2.Alpha = 192;
		paint2.SetStyle(Paint.Style.Stroke);
		for (int i = 0; i < num; i++)
		{
			MoveData moveData = moves[i];
			if (moveData.MoveType.IsMoveWithoutPass())
			{
				float num2 = (float)squareWidth / 2f * penwidth;
				float num3 = (float)squareHeight / 2f * penwidth;
				Point point;
				Point squarePosCenter;
				if (moveData.MoveType.HasFlag(MoveType.DropFlag))
				{
					point = GetStandPosCenter(moveData.Piece);
					squarePosCenter = GetSquarePosCenter(moveData.ToSquare);
				}
				else
				{
					point = GetSquarePosCenter(moveData.FromSquare);
					squarePosCenter = GetSquarePosCenter(moveData.ToSquare);
				}
				if (point.X < squarePosCenter.X)
				{
					point.X += squareWidth / 4;
				}
				else if (point.X != squarePosCenter.X)
				{
					point.X -= squareWidth / 4;
				}
				if (point.Y < squarePosCenter.Y)
				{
					point.Y += squareWidth / 4;
				}
				else if (point.Y != squarePosCenter.Y)
				{
					point.Y -= squareWidth / 4;
				}
				float y = (float)Math.Sqrt(Math.Pow(point.X - squarePosCenter.X, 2.0) + Math.Pow(point.Y - squarePosCenter.Y, 2.0));
				PointF pointF = new PointF(0f, y);
				PointF pointF2 = new PointF((0f - num2) * 0.9f, pointF.Y - num3);
				PointF pointF3 = new PointF((0f - num2) * 0.3f, pointF.Y - num3 * 0.9f);
				PointF pointF4 = new PointF((0f - num2) * 0.2f, 0f);
				using Path path = new Path();
				path.MoveTo(pointF.X, pointF.Y);
				path.LineTo(pointF2.X, pointF2.Y);
				path.LineTo(pointF3.X, pointF3.Y);
				path.LineTo(pointF4.X, pointF4.Y);
				path.LineTo(0f - pointF4.X, pointF4.Y);
				path.LineTo(0f - pointF3.X, pointF3.Y);
				path.LineTo(0f - pointF2.X, pointF2.Y);
				path.Close();
				Matrix matrix = new Matrix();
				float degrees = (float)(Math.Atan2(-(squarePosCenter.X - point.X), squarePosCenter.Y - point.Y) * 180.0 / Math.PI);
				matrix.PostRotate(degrees);
				matrix.PostTranslate(point.X + screenOfsX, point.Y + screenOfsY);
				path.Transform(matrix);
				g.DrawPath(path, paint);
				g.DrawPath(path, paint2);
			}
			penwidth /= 2f;
			if (penwidth < 0.5f)
			{
				penwidth = 0.5f;
			}
			paint.Alpha -= 16;
		}
	}

	private void DrawHintNumber(Canvas g, List<MoveData> moves, int max, int number)
	{
	}

	private void DrawNextMoveArrow(Canvas g)
	{
		if (notation.MoveCurrent.ChildCurrent == null)
		{
			return;
		}
		nextMoveList = new List<MoveData> { notation.MoveCurrent.ChildCurrent };
		foreach (MoveNode child in notation.MoveCurrent.Children)
		{
			if (notation.MoveCurrent.ChildCurrent != child)
			{
				nextMoveList.Add(child);
			}
		}
		DrawHintArrow(g, nextMoveList, 10, Color.Green, 0.66f);
	}

	private void CalcSize(int w, int h)
	{
		int num2;
		int num;
		if (shogiBoardMode == ShogiBoardMode.EDIT)
		{
			num = (w - 10) * 2 / 21;
			num2 = (h - 10) * 2 / 23;
		}
		else
		{
			num = (w - 10) * 2 / 19;
			num2 = (h - 10) * 2 / 23;
		}
		if (num < 3 || num2 < 3)
		{
			num = 3;
			num2 = 3;
		}
		else
		{
			num &= -2;
			num2 &= -2;
			int num3 = (num * 68 + 31) / 62;
			num3 &= -2;
			if (num3 <= num2)
			{
				num2 = num3 & -2;
			}
			else
			{
				num = (num2 * 62 + 34) / 68;
				num &= -2;
			}
		}
		squareWidth = num;
		squareHeight = num2;
		int num4 = num * 19 / 2 + 10;
		int height = num2 * 19 / 2 + 10;
		pieceWidth = num * 62 / 62;
		pieceHeight = num2 * 68 / 68;
		int num5 = squareWidth * 7;
		int num6 = squareHeight;
		if (shogiBoardMode == ShogiBoardMode.EDIT)
		{
			boardRect.X = num;
			boardRect.Y = squareHeight;
		}
		else
		{
			boardRect.X = 0;
			boardRect.Y = squareHeight;
		}
		boardRect.Width = num4;
		boardRect.Height = height;
		boardMarginX = squareWidth / 4 + 5;
		boardMarginY = squareHeight / 4 + 5;
		pieceStandTop.Left = boardRect.Left + boardMarginX + squareWidth * 2;
		pieceStandTop.Top = 0;
		pieceStandTop.Right = pieceStandTop.Left + num5 - 1;
		pieceStandTop.Bottom = pieceStandTop.Top + num6 - 1;
		pieceStandBottom.Left = boardRect.Left + boardMarginX + boardRect.X;
		pieceStandBottom.Top = boardRect.Bottom + 1;
		pieceStandBottom.Right = pieceStandBottom.Left + num5 - 1;
		pieceStandBottom.Bottom = pieceStandBottom.Top + num6 - 1;
		screenWidth = boardRect.X + num4;
		screenHeight = pieceStandBottom.Bottom + 1;
	}

	private void UpdateSize(int w, int h)
	{
		int num2;
		int num;
		if (shogiBoardMode == ShogiBoardMode.EDIT)
		{
			num = (w - 10) * 2 / 21;
			num2 = (h - 10) * 2 / 23;
		}
		else
		{
			num = (w - 10) * 2 / 19;
			num2 = (h - 10) * 2 / 23;
		}
		if (num < 3 || num2 < 3)
		{
			num = 3;
			num2 = 3;
		}
		else
		{
			num &= -2;
			num2 &= -2;
			int num3 = (num * 68 + 31) / 62;
			num3 &= -2;
			if (num3 <= num2)
			{
				num2 = num3 & -2;
			}
			else
			{
				num = (num2 * 62 + 34) / 68;
				num &= -2;
			}
		}
		squareWidth = num;
		squareHeight = num2;
		int num4 = num * 19 / 2 + 10;
		int height = num2 * 19 / 2 + 10;
		pieceWidth = num * 62 / 62;
		pieceHeight = num2 * 68 / 68;
		int num5 = squareWidth * 7;
		int num6 = squareHeight;
		if (shogiBoardMode == ShogiBoardMode.EDIT)
		{
			boardRect.X = num;
			boardRect.Y = squareHeight;
		}
		else
		{
			boardRect.X = 0;
			boardRect.Y = squareHeight;
		}
		boardRect.Width = num4;
		boardRect.Height = height;
		boardMarginX = squareWidth / 4 + 5;
		boardMarginY = squareHeight / 4 + 5;
		pieceStandTop.Left = boardRect.Left + boardMarginX + squareWidth * 2;
		pieceStandTop.Top = 0;
		pieceStandTop.Right = pieceStandTop.Left + num5 - 1;
		pieceStandTop.Bottom = pieceStandTop.Top + num6 - 1;
		pieceStandBottom.Left = boardRect.Left + boardMarginX + boardRect.X;
		pieceStandBottom.Top = boardRect.Bottom + 1;
		pieceStandBottom.Right = pieceStandBottom.Left + num5 - 1;
		pieceStandBottom.Bottom = pieceStandBottom.Top + num6 - 1;
		screenWidth = boardRect.X + num4;
		screenHeight = pieceStandBottom.Bottom + 1;
		coordFontSize = 8 + 12 * pieceWidth / 62;
		if (coordPaint != null)
		{
			coordPaint.TextSize = coordFontSize;
		}
		pieceFontSize = 8 + 16 * pieceWidth / 62;
		pieceFontPaint.TextSize = pieceFontSize;
		pieceEdgePaint.TextSize = pieceFontSize;
		screenOfsX = (w - screenWidth) / 2;
		screenOfsY = (h - screenHeight) / 2;
		if (shogiBoardMode == ShogiBoardMode.EDIT)
		{
			pieceBoxRect.Left = 0;
			pieceBoxRect.Top = boardRect.Y + num2 / 2;
			pieceBoxRect.Right = pieceBoxRect.Left + num - 1;
			pieceBoxRect.Bottom = pieceBoxRect.Top + num2 * 8 - 1;
		}
		topinfo_x = 0;
		topinfo_y = screenOfsY;
		bottominfo_x = screenOfsX + pieceStandBottom.Right;
		bottominfo_y = screenOfsY + pieceStandBottom.Top;
		info_width = w - bottominfo_x;
		info_height = num6;
	}

	private BoardArea GetBoardArea(int x, int y)
	{
		if (GetBoardPos(x, y) != 81)
		{
			return BoardArea.BOARD;
		}
		if (pieceStandBottom.Contains(x, y))
		{
			return BoardArea.BOTTOM_STAND;
		}
		if (pieceStandTop.Contains(x, y))
		{
			return BoardArea.TOP_STAND;
		}
		if (pieceBoxRect.Contains(x, y))
		{
			return BoardArea.BOX;
		}
		return BoardArea.NONE;
	}

	private void PieceAnimaStart(MoveDataDir dir, MoveData moveData)
	{
		HashKey hashKey = position.HashKey;
		if (pieceAnimation.IsAnimation())
		{
			hashKey = hashkey;
		}
		if (moveData != null && moveData.MoveType.IsMove() && !hashKey.Equals(notation.Position.HashKey))
		{
			if (dir == MoveDataDir.Next)
			{
				pieceAnimation.Add(new PieceMoveData(dir, moveData));
			}
			else
			{
				pieceAnimation.Add(new PieceMoveData(dir, moveData, (notation.MoveCurrent.Number == 0) ? notation.InitialPosition.MoveLast : notation.MoveCurrent));
			}
			animTimer.Enabled = true;
		}
		hashkey = notation.Position.HashKey;
	}

	private void PieceAnimaStop()
	{
		if (pieceAnimation.IsAnimation())
		{
			pieceAnimation.Stop();
			animTimer.Stop();
			position = (SPosition)notation.Position.Clone();
			hashkey = position.HashKey;
			DrawOffscreenAll();
			OnAnimationEnd(new EventArgs());
		}
	}

	private void PieceAnimaPlay()
	{
		if (pieceAnimation.IsAnimation())
		{
			pieceAnimation.Animation();
		}
		if (pieceAnimation.IsAnimation())
		{
			animTimer.Enabled = true;
			return;
		}
		animTimer.Stop();
		OnAnimationEnd(new EventArgs());
	}

	private Point GetFromPosition(PieceMoveData moveData)
	{
		if (moveData.MoveType.IsMoveWithoutPass())
		{
			if (moveData.MoveType.HasFlag(MoveType.DropFlag))
			{
				return GetStandPos(moveData.Piece);
			}
			return GetBoardPosFromSquare(moveData.FromSquare);
		}
		return new Point(0, 0);
	}

	private Point GetToPosition(PieceMoveData moveData)
	{
		if (moveData.MoveType.IsMoveWithoutPass())
		{
			return GetBoardPosFromSquare(moveData.ToSquare);
		}
		return new Point(0, 0);
	}

	private void MakeMove(MoveData moveData)
	{
		PieceAnimaStop();
		movePrev = new MoveData(position.MoveLast);
		position.Move(moveData);
		hashkey = position.HashKey;
		DrawOffscreenMoveNext();
		UpdateArrow();
		OnMakeMove(new MakeMoveEventArgs(moveData));
	}

	private void DrawEdgeString(Canvas canvas, string str, int x, int y, Paint font, Paint edge)
	{
		canvas.DrawText(str, x, y, font);
		canvas.DrawText(str, x, y, edge);
	}

	protected virtual void OnNotationChanged(NotationEventArgs e)
	{
		if (this.NotationChanged != null)
		{
			this.NotationChanged(this, e);
		}
	}

	protected virtual void OnMakeMove(MakeMoveEventArgs e)
	{
		if (this.MakeMoveEvent != null)
		{
			this.MakeMoveEvent(this, e);
		}
	}

	protected virtual void OnScreenSizeChanged(EventArgs e)
	{
		if (this.ScreenSizeChanged != null)
		{
			this.ScreenSizeChanged(this, e);
		}
	}

	protected virtual void OnAnimationEnd(EventArgs e)
	{
		if (this.AnimationEnd != null)
		{
			this.AnimationEnd(this, e);
		}
	}

	protected override void OnDraw(Canvas g)
	{
		if (offscreen != null)
		{
			startime = DateTime.Now.Ticks;
			g.DrawBitmap(offscreen, screenOfsX, screenOfsY, null);
			// ダークモード時は盤面に半透明オーバーレイを適用
			var overlayColor = ShogiGUI.ColorUtils.Get(Context, Resource.Color.board_overlay);
			if (overlayColor != Android.Graphics.Color.Transparent)
			{
				using var overlayPaint = new Paint { Color = overlayColor };
				g.DrawRect(screenOfsX, screenOfsY, screenOfsX + offscreen.Width, screenOfsY + offscreen.Height, overlayPaint);
			}
			drawHintArrowFlag = false;
			drawBookArrowFlag = false;
			DrawTurn(g);
			if (nextMoveDisp && !pieceAnimation.IsAnimation())
			{
				nextMoveList = null;
				DrawNextMoveArrow(g);
			}
			if (hintInfo != null && hintDisp && !pieceAnimation.IsAnimation())
			{
				DrawHintArrows(g);
			}
			if (op.IsMoving())
			{
				DrawPiece(g, op.Piece, screenOfsX + op.Xpos - pieceWidth / 2, screenOfsY + op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
			}
			DrawAnimation(g);
		}
	}

	protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
	{
		base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
		if (Resources.Configuration.Orientation != Orientation.Landscape)
		{
			int measuredWidth = base.MeasuredWidth;
			int h = base.MeasuredHeight * 14 / 20;
			CalcSize(measuredWidth, h);
			SetMeasuredDimension(base.MeasuredWidth, screenHeight);
		}
	}

	protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
	{
		isDrawOffsceen = true;
		base.OnSizeChanged(w, h, oldw, oldh);
		base.Left = 0;
		base.Top = 0;
		if (w != oldw || h != oldh)
		{
			UpdateSize(w, h);
			if (offscreen != null)
			{
				offscreen.Dispose();
				offscreen = null;
			}
			offscreen = Bitmap.CreateBitmap(screenWidth, screenHeight, Bitmap.Config.Argb8888);
			OnScreenSizeChanged(new EventArgs());
			pieceAnimation.Resize(squareWidth);
			DrawOffscreenAll();
		}
	}

	protected override void OnDetachedFromWindow()
	{
		base.OnDetachedFromWindow();
		if (pieceAnimation.IsAnimation())
		{
			pieceAnimation.Stop();
			animTimer.Stop();
		}
		if (menuPromotion != null)
		{
			menuPromotion.Dismiss();
		}
		if (boardBitmap != null)
		{
			boardBitmap.Dispose();
		}
		if (pieceBitmap != null)
		{
			pieceBitmap.Dispose();
		}
		if (standBitmap != null)
		{
			standBitmap.Dispose();
		}
		if (turnBitmap != null)
		{
			turnBitmap.Dispose();
		}
		if (offscreen != null)
		{
			offscreen.Dispose();
			offscreen = null;
		}
		if (piecePaint != null)
		{
			piecePaint.Dispose();
		}
		if (piecePaintAlpha != null)
		{
			piecePaintAlpha.Dispose();
		}
		if (imageAttrTras != null)
		{
			imageAttrTras.Dispose();
		}
		if (backPaint != null)
		{
			backPaint.Dispose();
		}
		if (highlightPaint != null)
		{
			highlightPaint.Dispose();
		}
		if (coordPaint != null)
		{
			coordPaint.Dispose();
		}
		if (pieceFontPaint != null)
		{
			pieceFontPaint.Dispose();
		}
		if (pieceEdgePaint != null)
		{
			pieceEdgePaint.Dispose();
		}
	}

	public override bool OnTouchEvent(MotionEvent e)
	{
		if (ges.OnTouchEvent(e))
		{
			return true;
		}
		switch (e.Action)
		{
		case MotionEventActions.Down:
			op.ClearSkipClick();
			if (shogiBoardMode == ShogiBoardMode.EDIT)
			{
				EditMouseDown(e);
			}
			else
			{
				ShogiBan_MouseDown(e);
			}
			return true;
		case MotionEventActions.Up:
			if (shogiBoardMode == ShogiBoardMode.EDIT)
			{
				EditMouseClick(e);
			}
			else
			{
				ShogiBan_MouseClick(e);
			}
			break;
		case MotionEventActions.Move:
			ShogiBan_MouseMove(e);
			break;
		case MotionEventActions.Cancel:
			ShogiBan_MouseLeave(e);
			break;
		}
		return base.OnTouchEvent(e);
	}

	private void Ges_DoubleTap(object sender, GestureDetector.DoubleTapEventArgs e)
	{
		int mouse_x = (int)e.Event.GetX() - screenOfsX;
		int mouse_y = (int)e.Event.GetY() - screenOfsY;
		if (shogiBoardMode != ShogiBoardMode.EDIT)
		{
			return;
		}
		int boardPos = GetBoardPos(mouse_x, mouse_y);
		if (boardPos == 81)
		{
			return;
		}
		Piece piece = notation.Position.GetPiece(boardPos);
		if (piece != Piece.NoPiece)
		{
			if (piece.TypeOf() == PieceType.OU)
			{
				if (pieceBox.Box[8] > 0)
				{
					piece = ((piece.ColorOf() != PlayerColor.Black) ? (piece & (Piece)239) : (piece | Piece.WhiteFlag));
				}
			}
			else if (piece.IsPromoted() || piece.TypeOf() == PieceType.KIN)
			{
				piece &= (Piece)247;
				piece = ((piece.ColorOf() != PlayerColor.Black) ? (piece & (Piece)239) : (piece | Piece.WhiteFlag));
			}
			else
			{
				piece |= Piece.BOU;
			}
		}
		op.Cancel();
		notation.Position.SetPiece(boardPos, piece);
		position = (SPosition)notation.Position.Clone();
		DrawOffscreenSquare(boardPos);
	}

	private void ShogiBan_MouseDown(MotionEvent e)
	{
		int num = (int)e.GetX() - screenOfsX;
		int num2 = (int)e.GetY() - screenOfsY;
		if (!CanOperation() || op.Choices)
		{
			return;
		}
		int boardPos;
		PieceType pieceFromPieceStandBottom;
		if ((boardPos = GetBoardPos(num, num2)) != 81)
		{
			if (op.IsMoving())
			{
				return;
			}
			Piece piece = notation.Position.GetPiece(boardPos);
			if (piece.ColorOf() != notation.Position.Turn)
			{
				return;
			}
			op.MoveStartFromBoard(piece, boardPos);
			if (operationMode == OperationMode.Touch)
			{
				Point squarePosCenter = GetSquarePosCenter(boardPos);
				if (notation.Position.Turn == bottomPlayerColor)
				{
					squarePosCenter.X -= pieceWidth / 8;
					squarePosCenter.Y -= pieceHeight / 8;
				}
				else
				{
					squarePosCenter.X += pieceWidth / 8;
					squarePosCenter.Y += pieceHeight / 8;
				}
				op.SetPosition(squarePosCenter.X, squarePosCenter.Y);
			}
			else
			{
				op.SetPosition(num, num2);
			}
			op.KeepPosition();
			DrawOffscreenSquare(boardPos);
			InvalidateRect(op.Xpos - pieceWidth / 2, op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
		}
		else if ((pieceFromPieceStandBottom = GetPieceFromPieceStandBottom(num, num2)) != PieceType.NoPieceType)
		{
			if (!op.IsMoving() && notation.Position.IsHand(bottomPlayerColor, pieceFromPieceStandBottom) && notation.Position.Turn == bottomPlayerColor)
			{
				Piece piece = PieceExtensions.MakePiece(pieceFromPieceStandBottom, bottomPlayerColor);
				op.MoveStartFromStand(piece, BoardArea.BOTTOM_STAND);
				if (operationMode == OperationMode.Touch)
				{
					Point standPosCenter = GetStandPosCenter(piece);
					standPosCenter.X -= pieceWidth / 8;
					standPosCenter.Y -= pieceHeight / 8;
					op.SetPosition(standPosCenter.X, standPosCenter.Y);
				}
				else
				{
					op.SetPosition(num, num2);
				}
				op.KeepPosition();
				DrawOffscreenBottomStand();
				InvalidateRect(op.Xpos - pieceWidth / 2, op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
			}
		}
		else if ((pieceFromPieceStandBottom = GetPieceFromPieceStandTop(num, num2)) != PieceType.NoPieceType && !op.IsMoving() && notation.Position.IsHand(topPlayerColor, pieceFromPieceStandBottom) && notation.Position.Turn == topPlayerColor)
		{
			Piece piece = PieceExtensions.MakePiece(pieceFromPieceStandBottom, topPlayerColor);
			op.MoveStartFromStand(piece, BoardArea.TOP_STAND);
			if (operationMode == OperationMode.Touch)
			{
				Point standPosCenter2 = GetStandPosCenter(piece);
				standPosCenter2.X += pieceWidth / 8;
				standPosCenter2.Y += pieceHeight / 8;
				op.SetPosition(standPosCenter2.X, standPosCenter2.Y);
			}
			else
			{
				op.SetPosition(num, num2);
			}
			op.KeepPosition();
			DrawOffscreenTopStand();
			InvalidateRect(op.Xpos - pieceWidth / 2, op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
		}
	}

	private void EditMouseDown(MotionEvent e)
	{
		int num = (int)e.GetX() - screenOfsX;
		int num2 = (int)e.GetY() - screenOfsY;
		int boardPos;
		PieceType pieceFromPieceStandBottom;
		if ((boardPos = GetBoardPos(num, num2)) != 81)
		{
			if (op.IsMoving())
			{
				return;
			}
			Piece piece = notation.Position.GetPiece(boardPos);
			if (piece == Piece.NoPiece)
			{
				return;
			}
			op.MoveStartFromBoard(piece, boardPos);
			if (operationMode == OperationMode.Touch)
			{
				Point squarePosCenter = GetSquarePosCenter(boardPos);
				if (notation.Position.Turn == bottomPlayerColor)
				{
					squarePosCenter.X -= pieceWidth / 8;
					squarePosCenter.Y -= pieceHeight / 8;
				}
				else
				{
					squarePosCenter.X += pieceWidth / 8;
					squarePosCenter.Y += pieceHeight / 8;
				}
				op.SetPosition(squarePosCenter.X, squarePosCenter.Y);
			}
			else
			{
				op.SetPosition(num, num2);
			}
			DrawOffscreenSquare(boardPos);
			InvalidateRect(op.Xpos - pieceWidth / 2, op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
		}
		else if ((pieceFromPieceStandBottom = GetPieceFromPieceStandBottom(num, num2)) != PieceType.NoPieceType)
		{
			if (!op.IsMoving() && notation.Position.IsHand(bottomPlayerColor, pieceFromPieceStandBottom))
			{
				Piece piece = PieceExtensions.MakePiece(pieceFromPieceStandBottom, bottomPlayerColor);
				op.MoveStartFromStand(piece, BoardArea.BOTTOM_STAND);
				if (operationMode == OperationMode.Touch)
				{
					Point standPosCenter = GetStandPosCenter(piece);
					standPosCenter.X -= pieceWidth / 8;
					standPosCenter.Y -= pieceHeight / 8;
					op.SetPosition(standPosCenter.X, standPosCenter.Y);
				}
				else
				{
					op.SetPosition(num, num2);
				}
				op.SetPosition(num, num2);
				DrawOffscreenBottomStand();
				InvalidateRect(op.Xpos - pieceWidth / 2, op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
			}
		}
		else if ((pieceFromPieceStandBottom = GetPieceFromPieceStandTop(num, num2)) != PieceType.NoPieceType)
		{
			if (!op.IsMoving() && notation.Position.IsHand(topPlayerColor, pieceFromPieceStandBottom))
			{
				Piece piece = PieceExtensions.MakePiece(pieceFromPieceStandBottom, topPlayerColor);
				op.MoveStartFromStand(piece, BoardArea.TOP_STAND);
				if (operationMode == OperationMode.Touch)
				{
					Point standPosCenter2 = GetStandPosCenter(piece);
					standPosCenter2.X += pieceWidth / 8;
					standPosCenter2.Y += pieceHeight / 8;
					op.SetPosition(standPosCenter2.X, standPosCenter2.Y);
				}
				else
				{
					op.SetPosition(num, num2);
				}
				DrawOffscreenTopStand();
				InvalidateRect(op.Xpos - pieceWidth / 2, op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
			}
		}
		else if ((pieceFromPieceStandBottom = GetPieceFromPieceBox(num, num2)) != PieceType.NoPieceType && !op.IsMoving() && pieceBox.Box[(uint)pieceFromPieceStandBottom] > 0)
		{
			op.MoveStartFromBox(pieceFromPieceStandBottom);
			if (operationMode == OperationMode.Touch)
			{
				Point pieceBoxCenter = GetPieceBoxCenter(pieceFromPieceStandBottom);
				pieceBoxCenter.X += pieceWidth / 8;
				pieceBoxCenter.Y -= pieceHeight / 8;
				op.SetPosition(pieceBoxCenter.X, pieceBoxCenter.Y);
			}
			else
			{
				op.SetPosition(num, num2);
			}
			DrawOffscreenPieceBox();
			InvalidateRect(op.Xpos - pieceWidth / 2, op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
		}
	}

	private void ShogiBan_MouseClick(MotionEvent e)
	{
		if (!CanOperation() || op.Choices)
		{
			return;
		}
		int num = (int)e.GetX() - screenOfsX;
		int num2 = (int)e.GetY() - screenOfsY;
		BoardArea boardArea = GetBoardArea(num, num2);
		if (boardArea == BoardArea.NONE)
		{
			return;
		}
		int boardPos = GetBoardPos(num, num2);
		if (op.IsSkipClick())
		{
			if (operationMode != OperationMode.DragAndDrop)
			{
				return;
			}
			switch (boardArea)
			{
			case BoardArea.BOARD:
				if (op.FromSquare == boardPos)
				{
					return;
				}
				break;
			case BoardArea.BOTTOM_STAND:
				if (op.Piece.ColorOf() == bottomPlayerColor && op.BoardArea == BoardArea.BOTTOM_STAND)
				{
					return;
				}
				break;
			case BoardArea.TOP_STAND:
				if (op.Piece.ColorOf() == topPlayerColor && op.BoardArea == BoardArea.TOP_STAND)
				{
					return;
				}
				break;
			}
		}
		switch (boardArea)
		{
		case BoardArea.BOARD:
			if (!op.IsMoving())
			{
				break;
			}
			if (op.MoveEnd(boardPos))
			{
				opeMoveData.Initialize();
				if (op.BoardArea == BoardArea.BOARD)
				{
					opeMoveData.MoveType = MoveType.MoveFlag;
				}
				else
				{
					opeMoveData.MoveType = MoveType.DropFlag;
				}
				opeMoveData.FromSquare = op.FromSquare;
				opeMoveData.ToSquare = boardPos;
				opeMoveData.Piece = op.Piece;
				opeMoveData.CapturePiece = notation.Position.GetPiece(boardPos);
				if (opeMoveData.CapturePiece != Piece.NoPiece)
				{
					opeMoveData.MoveType |= MoveType.Capture;
				}
				if (MoveCheck.IsValid(notation.Position, opeMoveData))
				{
					if (MoveCheck.ForcePromotion(opeMoveData.Piece, opeMoveData.ToSquare))
					{
						opeMoveData.MoveType |= MoveType.MoveMask;
					}
					else if (MoveCheck.CanPromota(opeMoveData))
					{
						op.PromotionChoicStart();
						menuPromotion.Window.SetFlags(WindowManagerFlags.NotFocusable, WindowManagerFlags.NotFocusable);
						menuPromotion.Show();
						menuPromotion.Window.DecorView.SystemUiVisibility = ((Activity)base.Context).Window.DecorView.SystemUiVisibility;
						menuPromotion.Window.ClearFlags(WindowManagerFlags.NotFocusable);
						break;
					}
					op.End();
					MakeMove(opeMoveData);
				}
				else if (operationMode == OperationMode.Touch)
				{
					op.End();
					RedrawFromPiece();
					InvalidateRect(op.Xpos - pieceWidth / 2, op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
				}
			}
			else
			{
				RedrawFromPiece();
				InvalidateRect(op.Xpos - pieceWidth / 2, op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
			}
			break;
		case BoardArea.BOTTOM_STAND:
			if (op.IsMoving() && op.Cancel())
			{
				RedrawFromPiece();
			}
			break;
		case BoardArea.TOP_STAND:
			if (op.IsMoving() && op.Cancel())
			{
				RedrawFromPiece();
			}
			break;
		}
	}

	private void EditMouseClick(MotionEvent e)
	{
		int num = (int)e.GetX() - screenOfsX;
		int num2 = (int)e.GetY() - screenOfsY;
		int boardPos = GetBoardPos(num, num2);
		BoardArea boardArea = GetBoardArea(num, num2);
		if (boardArea == BoardArea.NONE)
		{
			return;
		}
		if (op.IsSkipClick())
		{
			switch (boardArea)
			{
			case BoardArea.BOARD:
				if (op.FromSquare == boardPos)
				{
					return;
				}
				break;
			case BoardArea.BOTTOM_STAND:
				if (op.Piece.ColorOf() == bottomPlayerColor && op.BoardArea == BoardArea.BOTTOM_STAND)
				{
					return;
				}
				break;
			case BoardArea.TOP_STAND:
				if (op.Piece.ColorOf() == topPlayerColor && op.BoardArea == BoardArea.TOP_STAND)
				{
					return;
				}
				break;
			case BoardArea.BOX:
				if (op.BoardArea == BoardArea.BOX)
				{
					return;
				}
				break;
			}
		}
		if (!op.IsMoving())
		{
			return;
		}
		if (boardArea != BoardArea.BOX)
		{
			if (boardPos == 81)
			{
				if (op.Piece.TypeOf() == PieceType.OU)
				{
					return;
				}
			}
			else
			{
				Piece piece = notation.Position.GetPiece(boardPos);
				if (piece.TypeOf() == PieceType.OU && piece != op.Piece)
				{
					return;
				}
			}
		}
		op.End();
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		if (op.BoardArea == BoardArea.BOARD)
		{
			notation.Position.SetPiece(op.FromSquare, Piece.NoPiece);
			position = (SPosition)notation.Position.Clone();
			DrawOffscreenSquare(op.FromSquare);
		}
		else if (op.BoardArea == BoardArea.BOX)
		{
			PieceType pieceType = op.Piece.TypeOf();
			pieceBox.Box[(uint)pieceType]--;
			flag3 = true;
		}
		else
		{
			PieceType pieceType = op.Piece.TypeOf();
			if (op.Piece.ColorOf() == PlayerColor.Black)
			{
				notation.Position.SetBlackHand(pieceType, notation.Position.GetBlackHand(pieceType) - 1);
				flag = true;
			}
			else
			{
				notation.Position.SetWhiteHand(pieceType, notation.Position.GetWhiteHand(pieceType) - 1);
				flag2 = true;
			}
			position = (SPosition)notation.Position.Clone();
		}
		switch (boardArea)
		{
		case BoardArea.BOARD:
		{
			Piece piece2 = notation.Position.GetPiece(boardPos);
			if (piece2 != Piece.NoPiece)
			{
				PieceType pieceType = piece2.TypeOf();
				if (op.Piece.ColorOf() == PlayerColor.Black)
				{
					notation.Position.SetBlackHand(pieceType, notation.Position.GetBlackHand(pieceType) + 1);
					flag = true;
				}
				else
				{
					notation.Position.SetWhiteHand(pieceType, notation.Position.GetWhiteHand(pieceType) + 1);
					flag2 = true;
				}
			}
			piece2 = op.Piece;
			if (piece2.TypeOf() == PieceType.OU)
			{
				piece2 = ((notation.Position.SearchPiece(Piece.WOU) != 81) ? Piece.BOU : Piece.WOU);
			}
			if (MoveCheck.ForcePromotion(piece2, boardPos))
			{
				notation.Position.SetPiece(boardPos, piece2 | Piece.BOU);
			}
			else
			{
				notation.Position.SetPiece(boardPos, piece2);
			}
			position = (SPosition)notation.Position.Clone();
			DrawOffscreenSquare(boardPos);
			break;
		}
		case BoardArea.BOTTOM_STAND:
		{
			PieceType pieceType = op.Piece.TypeOf();
			if (bottomPlayerColor == PlayerColor.Black)
			{
				notation.Position.SetBlackHand(pieceType, notation.Position.GetBlackHand(pieceType) + 1);
				flag = true;
			}
			else
			{
				notation.Position.SetWhiteHand(pieceType, notation.Position.GetWhiteHand(pieceType) + 1);
				flag2 = true;
			}
			position = (SPosition)notation.Position.Clone();
			break;
		}
		case BoardArea.TOP_STAND:
		{
			PieceType pieceType = op.Piece.TypeOf();
			if (topPlayerColor == PlayerColor.Black)
			{
				notation.Position.SetBlackHand(pieceType, notation.Position.GetBlackHand(pieceType) + 1);
				flag = true;
			}
			else
			{
				notation.Position.SetWhiteHand(pieceType, notation.Position.GetWhiteHand(pieceType) + 1);
				flag2 = true;
			}
			position = (SPosition)notation.Position.Clone();
			break;
		}
		case BoardArea.BOX:
		{
			PieceType pieceType = op.Piece.TypeOf();
			pieceBox.Box[(uint)pieceType]++;
			flag3 = true;
			break;
		}
		}
		if (flag)
		{
			if (bottomPlayerColor == PlayerColor.Black)
			{
				DrawOffscreenBottomStand();
			}
			else
			{
				DrawOffscreenTopStand();
			}
		}
		if (flag2)
		{
			if (topPlayerColor == PlayerColor.Black)
			{
				DrawOffscreenBottomStand();
			}
			else
			{
				DrawOffscreenTopStand();
			}
		}
		if (flag3)
		{
			DrawOffscreenPieceBox();
		}
		InvalidateRect(op.Xpos - pieceWidth / 2, op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
	}

	private void ShogiBan_MouseMove(MotionEvent e)
	{
		int x = (int)e.GetX() - screenOfsX;
		int y = (int)e.GetY() - screenOfsY;
		if (CanOperation() && !op.Choices && (!op.IsSkipClick() || operationMode != OperationMode.Touch) && op.IsMoving())
		{
			int xpos = op.Xpos;
			int ypos = op.Ypos;
			if (operationMode != OperationMode.Touch)
			{
				op.SetPosition(x, y);
			}
			InvalidateRect(xpos - pieceWidth / 2, ypos - pieceHeight / 2, pieceWidth, pieceHeight);
			InvalidateRect(op.Xpos - pieceWidth / 2, op.Ypos - pieceHeight / 2, pieceWidth, pieceHeight);
		}
	}

	private void ShogiBan_MouseLeave(MotionEvent e)
	{
		if (!op.Choices && op.Cancel())
		{
			menuPromotion.Dismiss();
			DrawOffscreenAll();
			Invalidate();
		}
	}

	private void MenuPromotion_DismissEvent(object sender, EventArgs e)
	{
		if (op.Cancel())
		{
			DrawOffscreenAll();
			Invalidate();
		}
	}

	private void PromotionButton_Click(object sender, EventArgs e)
	{
		opeMoveData.MoveType |= MoveType.MoveMask;
		op.End();
		MakeMove(opeMoveData);
		menuPromotion.Dismiss();
	}

	private void UnpromotionButton_Click(object sender, EventArgs e)
	{
		op.End();
		MakeMove(opeMoveData);
		menuPromotion.Dismiss();
	}

	private void animTimer_Tick(object sender, ElapsedEventArgs e)
	{
		syncContext.Post(delegate
		{
			PieceAnimaPlay();
		}, null);
	}

	private void pieceAnim_MoveStartEvent(object sender, PieceMoveEventArgs e)
	{
		if (e.MoveData.Dir == MoveDataDir.Prev)
		{
			movePrev = new MoveData(position.MoveLast);
			position.UnMove(e.MoveData, e.MoveData.CurrentMoveData);
			DrawOffscreenMovePrev();
			UpdateArrow();
		}
		else if (e.MoveData.MoveType.HasFlag(MoveType.DropFlag))
		{
			if (e.MoveData.Piece.ColorOf() == bottomPlayerColor)
			{
				DrawOffscreenBottomStand();
			}
			else
			{
				DrawOffscreenTopStand();
			}
		}
		else
		{
			DrawOffscreenSquare(e.MoveData.FromSquare);
		}
	}

	private void pieceAnim_MoveEndEvent(object sender, PieceMoveEventArgs e)
	{
		if (e.MoveData.Dir == MoveDataDir.Next)
		{
			movePrev = new MoveData(position.MoveLast);
			position.Move(e.MoveData);
			DrawOffscreenMoveNext();
			UpdateArrow();
		}
		else if (!pieceAnimation.IsAnimation())
		{
			movePrev = e.MoveData;
			DrawOffscreenMovePrev();
		}
		else
		{
			DrawOffscreenSquare(e.MoveData.FromSquare);
		}
	}

	private void pieceAnim_PieceUpdateEvent(object sender, PieceUpdateEventArgs e)
	{
		InvalidateRect(e.Pos.X, e.Pos.Y, pieceWidth, pieceHeight);
	}
}
