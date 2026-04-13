using System.Collections.Generic;
using System.Collections.Specialized;

namespace ShogiLib;

public class SNotation
{
	private SPosition position;

	private SPosition initialPosition;

	private Handicap handicap;

	public bool IsOutputInitialPosition;

	private ListDictionary kifuInfos;

	private MoveNode moveFirst;

	private MoveNode moveCurrent;

	private MoveNode movePrev;

	private MoveType result;

	private PlayerColor winColor = PlayerColor.NoColor;

	private int webLoad;

	private Dictionary<int, string> engines;

	public SPosition Position
	{
		get
		{
			if (moveCurrent == moveFirst)
			{
				return initialPosition;
			}
			return position;
		}
	}

	public SPosition InitialPosition => initialPosition;

	public MoveNode MoveCurrent
	{
		get
		{
			return moveCurrent;
		}
		private set
		{
			moveCurrent = value;
		}
	}

	public MoveNode MovePrev => movePrev;

	public string BlackName { get; set; } = string.Empty;

	public string WhiteName { get; set; } = string.Empty;

	public Handicap Handicap
	{
		get
		{
			return handicap;
		}
		set
		{
			handicap = value;
			IsOutputInitialPosition = false;
			switch (handicap)
			{
			case Handicap.RIGHT_KYO:
				initialPosition.SetHandicapRightKyo();
				break;
			case Handicap.KYO:
				initialPosition.SetHandicapKyo();
				break;
			case Handicap.KAKU:
				initialPosition.SetHandicapKaku();
				break;
			case Handicap.HISYA:
				initialPosition.SetHandicapHisya();
				break;
			case Handicap.HIKYO:
				initialPosition.SetHandicapHiKyo();
				break;
			case Handicap.H2:
				initialPosition.SetHandicap2();
				break;
			case Handicap.H3:
				initialPosition.SetHandicap3();
				break;
			case Handicap.H4:
				initialPosition.SetHandicap4();
				break;
			case Handicap.H5:
				initialPosition.SetHandicap5();
				break;
			case Handicap.LEFT5:
				initialPosition.SetHandicapLeft5();
				break;
			case Handicap.H6:
				initialPosition.SetHandicap6();
				break;
			case Handicap.H8:
				initialPosition.SetHandicap8();
				break;
			case Handicap.H10:
				initialPosition.SetHandicap10();
				break;
			case Handicap.OTHER:
				IsOutputInitialPosition = true;
				initialPosition.InitHashKey();
				break;
			}
			moveFirst.Key = initialPosition.HashKey;
		}
	}

	public IEnumerable<MoveNode> MoveNodes
	{
		get
		{
			MoveNode info = moveFirst;
			yield return info;
			while (true)
			{
				MoveNode childCurrent;
				info = (childCurrent = info.ChildCurrent);
				if (childCurrent != null)
				{
					yield return info;
					continue;
				}
				break;
			}
		}
	}

	public ListDictionary KifuInfos
	{
		get
		{
			return kifuInfos;
		}
		set
		{
			kifuInfos = value;
		}
	}

	public MoveNode MoveFirst => moveFirst;

	public PlayerColor WinColor
	{
		get
		{
			return winColor;
		}
		set
		{
			winColor = value;
		}
	}

	public int Count => GetMoveLastNode().Number;

	public MoveType Result => result;

	public int WebLoad
	{
		get
		{
			return webLoad;
		}
		set
		{
			webLoad = value;
		}
	}

	public Dictionary<int, string> Engines => engines;

	public SNotation()
	{
		position = new SPosition();
		initialPosition = new SPosition();
		moveFirst = new MoveNode();
		kifuInfos = new ListDictionary();
		engines = new Dictionary<int, string>();
		Init();
	}

	public SNotation(SNotation notation)
	{
		position = (SPosition)notation.position.Clone();
		initialPosition = (SPosition)notation.initialPosition.Clone();
		moveFirst = MoveNode.DeepCopy(null, notation.moveFirst);
		kifuInfos = DeepCopyHelper.DeepCopy(notation.kifuInfos);
		BlackName = (string)notation.BlackName.Clone();
		WhiteName = (string)notation.WhiteName.Clone();
		handicap = notation.handicap;
		IsOutputInitialPosition = notation.IsOutputInitialPosition;
		moveCurrent = FindMoveNode(notation.moveCurrent);
		movePrev = FindMoveNode(notation.movePrev);
		webLoad = notation.webLoad;
		engines = DeepCopyHelper.DeepCopy(notation.engines);
	}

	public bool Equals(SNotation notation)
	{
		if (position.Equals(notation.position) && initialPosition.Equals(notation.initialPosition) && BlackName == notation.BlackName && WhiteName == notation.WhiteName && handicap == notation.handicap)
		{
			return Count == notation.Count;
		}
		return false;
	}

	public override bool Equals(object obj)
	{
		return Equals((SNotation)obj);
	}

	public override int GetHashCode()
	{
		return BlackName.GetHashCode() + WhiteName.GetHashCode() + Count.GetHashCode();
	}

	public void Init()
	{
		position.Init();
		initialPosition.Init();
		moveFirst.Key = initialPosition.HashKey;
		BlackName = string.Empty;
		WhiteName = string.Empty;
		handicap = Handicap.HIRATE;
		IsOutputInitialPosition = false;
		moveFirst.Initialize();
		moveCurrent = moveFirst;
		movePrev = moveFirst;
		result = MoveType.NoMove;
		webLoad = 0;
		kifuInfos.Clear();
		engines.Clear();
	}

	public void InitEdit()
	{
		if (moveCurrent != moveFirst)
		{
			position.MoveLast.Initialize();
			initialPosition = (SPosition)position.Clone();
			moveFirst.Key = initialPosition.HashKey;
		}
		moveFirst.Initialize();
		moveCurrent = moveFirst;
		movePrev = moveFirst;
	}

	public bool Continue(bool remove_stop)
	{
		bool flag = false;
		if (moveCurrent.MoveType.HasFlag(MoveType.ResultFlag))
		{
			MoveNode moveNode = moveCurrent;
			moveCurrent = movePrev;
			movePrev = moveCurrent.Parent;
			if (remove_stop && moveNode.MoveType == MoveType.Stop)
			{
				int childIndex = moveCurrent.GetChildIndex(moveNode);
				if (childIndex >= 0)
				{
					moveCurrent.RemoveChild(childIndex);
				}
			}
			flag = true;
		}
		return flag;
	}

	public void SetInitialPosition(SPosition pos)
	{
		initialPosition = (SPosition)pos.Clone();
		moveFirst.Key = initialPosition.HashKey;
	}

	public void InitHashKey()
	{
		initialPosition.InitHashKey();
		moveFirst.Key = initialPosition.HashKey;
	}

	public void SetHandicap(Handicap handicap)
	{
		Handicap = handicap;
		position = (SPosition)initialPosition.Clone();
	}

	public bool AddMove(MoveDataEx moveData)
	{
		return AddMove(moveData, MoveAddMode.ADD, changeChildCurrent: true);
	}

	public bool AddMove(MoveDataEx moveData, MoveAddMode mode, bool changeChildCurrent)
	{
		if (!IsValid(moveData))
		{
			return false;
		}
		if (moveFirst == moveCurrent)
		{
			position = (SPosition)initialPosition.Clone();
		}
		MoveNode moveNode = new MoveNode(moveData);
		if (moveNode.MoveType.IsResult() || moveNode.MoveType == MoveType.Pass)
		{
			moveNode.Piece = PieceExtensions.PieceFlagFromColor(position.Turn);
		}
		else if (moveNode.Piece == Piece.NoPiece && moveNode.MoveType.HasFlag(MoveType.MoveFlag))
		{
			moveNode.Piece = position.GetPiece(moveNode.FromSquare);
		}
		if (moveNode.MoveType.IsResult() && (mode == MoveAddMode.INSERT || result == MoveType.NoMove))
		{
			result = moveNode.MoveType;
			winColor = GetWinColor(moveNode);
		}
		if (moveNode.Number == 0)
		{
			moveNode.Number = moveCurrent.Number + 1;
		}
		if (moveNode.TotalTime == 0)
		{
			moveNode.TotalTime = ((movePrev != null) ? movePrev.TotalTime : 0) + moveNode.Time;
		}
		if (moveNode.MoveType.IsMove())
		{
			if (moveNode.MoveType.IsMoveWithoutPass())
			{
				if (moveCurrent.MoveType.IsMoveWithoutPass() && moveNode.ToSquare == moveCurrent.ToSquare)
				{
					moveNode.MoveType |= MoveType.Same;
				}
				if (Position.GetPiece(moveNode.ToSquare) != Piece.NoPiece)
				{
					moveNode.MoveType |= MoveType.Capture;
					moveNode.CapturePiece = Position.GetPiece(moveNode.ToSquare);
				}
				if (moveNode.IsNotPromotion())
				{
					moveNode.MoveType |= MoveType.Unpromotion;
				}
			}
			moveNode.Action = moveNode.GetAction(Position);
			position.Move(moveNode);
		}
		moveNode.Key = position.HashKey;
		switch (mode)
		{
		case MoveAddMode.INSERT:
			moveCurrent.InsertChild(0, moveNode, changeChildCurrent);
			break;
		case MoveAddMode.MERGE:
			moveNode = moveCurrent.MergeChild(moveNode, changeChildCurrent);
			break;
		default:
			moveCurrent.AddChild(moveNode, changeChildCurrent);
			break;
		}
		movePrev = moveCurrent;
		moveCurrent = moveNode;
		return true;
	}

	private bool IsValid(MoveDataEx moveData)
	{
		if (moveData.Number != 0 && moveCurrent.Number + 1 != moveData.Number)
		{
			return false;
		}
		moveData.MoveType.IsMove();
		return true;
	}

	public bool ChangeCurrent(int index)
	{
		MovePosition(index);
		return moveCurrent.InitChildCurrent();
	}

	public bool Jump(int index)
	{
		MovePosition(index);
		return false;
	}

	public bool ChangeChildCurrent(int no)
	{
		bool num = movePrev.ChangeChildCurrent(no);
		moveCurrent = movePrev.ChildCurrent;
		if (num)
		{
			MovePosition(moveCurrent.Number);
		}
		return num;
	}

	public bool ChangeCurrent(MoveNode moveNode)
	{
		bool flag = false;
		if (moveCurrent != moveNode)
		{
			flag = true;
			MoveNode moveNode2 = moveNode;
			while (moveNode2.Parent != null)
			{
				moveNode2.Parent.ChangeChildCurrent(moveNode2);
				moveNode2 = moveNode2.Parent;
			}
			MovePosition(moveNode.Number);
		}
		return flag;
	}

	public bool Remove(MoveNode move_node)
	{
		bool flag = false;
		if (move_node == null)
		{
			return false;
		}
		int childIndex = move_node.Parent.GetChildIndex(move_node);
		if (childIndex >= 0)
		{
			flag = true;
			bool flag2 = false;
			foreach (MoveNode moveNode in MoveNodes)
			{
				if (moveNode == move_node)
				{
					flag2 = true;
					break;
				}
			}
			if (flag2 && MoveCurrent.Number >= move_node.Number)
			{
				MovePosition(move_node.Parent.Number);
			}
			move_node.Parent.RemoveChild(childIndex);
		}
		return flag;
	}

	public void Merge(MoveNode parent, int dest, int src)
	{
		MoveNode moveNode = parent.Children[src];
		if (parent.Children[dest].Merge(moveNode))
		{
			Remove(moveNode);
		}
	}

	public void Merge(SNotation notation)
	{
		moveFirst.Merge(notation.MoveFirst);
	}

	public void AddBranch(SNotation notation)
	{
		if (moveFirst.IsEqual(notation.MoveFirst))
		{
			moveFirst.AddChildren(notation.MoveFirst);
			return;
		}
		MoveNode moveNode = new MoveNode(new MoveDataEx(notation.initialPosition.MoveLast));
		moveNode.Key = notation.initialPosition.HashKey;
		if (moveCurrent.IsEqual(moveNode))
		{
			moveCurrent.AddChildren(notation.MoveFirst);
			return;
		}
		MoveNode moveNode2 = FindMoveNode(moveNode);
		if (moveNode2 == null)
		{
			moveNode2 = FindMoveNode(notation.initialPosition.HashKey);
		}
		if (moveNode2 != null && moveNode2.MoveType.IsResult())
		{
			moveNode2 = moveNode2.Parent;
		}
		moveNode2?.AddChildren(notation.MoveFirst);
	}

	public void DeleteNotCurrent()
	{
		foreach (MoveNode moveNode in MoveNodes)
		{
			if (moveNode == moveCurrent)
			{
				moveNode.ClearChildren();
				break;
			}
			moveNode.DeleteNotCurrent();
		}
	}

	public void Matta()
	{
		int num = MoveCurrent.Number - 2;
		if (num < 0)
		{
			num = 0;
		}
		MovePosition(num);
		Remove(MoveCurrent.ChildCurrent);
	}

	public void Back()
	{
		if (MoveCurrent.Children.Count != 0)
		{
			Prev(1);
			return;
		}
		Prev(1);
		Remove(MoveCurrent.ChildCurrent);
	}

	private void MovePosition(int index)
	{
		moveCurrent = moveFirst;
		movePrev = moveFirst;
		position = (SPosition)initialPosition.Clone();
		foreach (MoveNode moveNode in MoveNodes)
		{
			if (moveNode.Number > index)
			{
				break;
			}
			movePrev = moveCurrent;
			moveCurrent = moveNode;
			if (moveNode.MoveType.IsMove())
			{
				position.Move(moveNode);
			}
		}
	}

	public bool Next(int num)
	{
		bool flag = false;
		for (int i = 0; i < num; i++)
		{
			if (moveCurrent.ChildCurrent == null)
			{
				break;
			}
			movePrev = moveCurrent;
			moveCurrent = moveCurrent.ChildCurrent;
			if (moveCurrent.MoveType.IsMove())
			{
				position.Move(moveCurrent);
				flag = true;
			}
		}
		return flag;
	}

	public bool MoveChild(MoveNode moveNode)
	{
		bool flag = false;
		if (moveCurrent.Children.Contains(moveNode))
		{
			movePrev = moveCurrent;
			moveCurrent = moveNode;
			if (moveCurrent.MoveType.IsMove())
			{
				position.Move(moveCurrent);
				flag = true;
			}
		}
		return flag;
	}

	public bool MoveParent()
	{
		bool flag = false;
		if (moveCurrent.MoveType.IsMove())
		{
			position.UnMove(moveCurrent, moveCurrent.Parent);
			flag = true;
		}
		if (moveCurrent.Parent != null)
		{
			moveCurrent = moveCurrent.Parent;
			movePrev = moveCurrent.Parent;
		}
		return flag;
	}

	public bool Prev(int num)
	{
		if (num == 1)
		{
			return MoveParent();
		}
		bool flag = false;
		int num2 = moveCurrent.Number - num;
		if (num2 < 0)
		{
			num2 = 0;
		}
		if (num2 != moveCurrent.Number)
		{
			flag = true;
		}
		MovePosition(num2);
		return flag;
	}

	public void First()
	{
		moveCurrent = moveFirst;
		movePrev = moveFirst;
		position = (SPosition)initialPosition.Clone();
	}

	public void Last()
	{
		while (moveCurrent.ChildCurrent != null)
		{
			movePrev = moveCurrent;
			moveCurrent = moveCurrent.ChildCurrent;
			if (moveCurrent.MoveType.IsMove())
			{
				position.Move(moveCurrent);
			}
		}
	}

	private MoveNode GetMoveLastNode()
	{
		MoveNode childCurrent = moveCurrent;
		while (childCurrent.ChildCurrent != null)
		{
			childCurrent = childCurrent.ChildCurrent;
		}
		return childCurrent;
	}

	public MoveNode GetMoveNode(int index)
	{
		MoveNode childCurrent = moveCurrent;
		if (moveCurrent.Number > index)
		{
			childCurrent = moveFirst;
		}
		while (childCurrent.Number != index && childCurrent.ChildCurrent != null)
		{
			childCurrent = childCurrent.ChildCurrent;
		}
		return childCurrent;
	}

	public void StartBranch(int index)
	{
		if (index != 0)
		{
			MovePosition(index - 1);
		}
	}

	public void AddKifuInfo(string key, string val)
	{
		kifuInfos[key] = val;
	}

	public MoveNode FindMoveNode(MoveNode node)
	{
		if (node == null)
		{
			return null;
		}
		if (moveCurrent != null && moveCurrent.IsEqual(node))
		{
			return moveCurrent;
		}
		return findMoveNode(moveFirst, node);
	}

	private MoveNode findMoveNode(MoveNode node, MoveNode checknode)
	{
		MoveNode moveNode = null;
		if (node.IsEqual(checknode))
		{
			return node;
		}
		if (node.Children.Count != 0)
		{
			if (node.ChildCurrent != null)
			{
				moveNode = findMoveNode(node.ChildCurrent, checknode);
				if (moveNode != null)
				{
					return moveNode;
				}
			}
			foreach (MoveNode child in node.Children)
			{
				if (node.ChildCurrent != child)
				{
					moveNode = findMoveNode(child, checknode);
					if (moveNode != null)
					{
						break;
					}
				}
			}
		}
		return moveNode;
	}

	public MoveNode FindMoveNode(HashKey key)
	{
		if (moveCurrent != null && moveCurrent.Key.Equals(key))
		{
			return moveCurrent;
		}
		return findMoveNode(moveFirst, key);
	}

	private MoveNode findMoveNode(MoveNode node, HashKey key)
	{
		MoveNode moveNode = null;
		if (node.Key.Equals(key))
		{
			return node;
		}
		if (node.Children.Count != 0)
		{
			if (node.ChildCurrent != null)
			{
				moveNode = findMoveNode(node.ChildCurrent, key);
				if (moveNode != null)
				{
					return moveNode;
				}
			}
			foreach (MoveNode child in node.Children)
			{
				if (node.ChildCurrent != child)
				{
					moveNode = findMoveNode(child, key);
					if (moveNode != null)
					{
						break;
					}
				}
			}
		}
		return moveNode;
	}

	public bool HasMoveNode(MoveNode node)
	{
		return findMoveNode(moveFirst, node) != null;
	}

	public int TotalTime(PlayerColor player)
	{
		if (MoveCurrent.MoveType == MoveType.NoMove)
		{
			return 0;
		}
		if (MoveCurrent.Turn == player)
		{
			return MoveCurrent.TotalTime;
		}
		if (movePrev == null)
		{
			return 0;
		}
		return movePrev.TotalTime;
	}

	public void DecisionHandicap()
	{
		SPosition sPosition = new SPosition();
		IsOutputInitialPosition = false;
		if (sPosition.Equals(initialPosition))
		{
			handicap = Handicap.HIRATE;
			return;
		}
		sPosition.SetHandicapKyo();
		if (sPosition.Equals(initialPosition))
		{
			handicap = Handicap.KYO;
			return;
		}
		sPosition.SetHandicapRightKyo();
		if (sPosition.Equals(initialPosition))
		{
			handicap = Handicap.RIGHT_KYO;
			return;
		}
		sPosition.SetHandicapKaku();
		if (sPosition.Equals(initialPosition))
		{
			handicap = Handicap.KAKU;
			return;
		}
		sPosition.SetHandicapHisya();
		if (sPosition.Equals(initialPosition))
		{
			handicap = Handicap.HISYA;
			return;
		}
		sPosition.SetHandicapHiKyo();
		if (sPosition.Equals(initialPosition))
		{
			handicap = Handicap.HIKYO;
			return;
		}
		sPosition.SetHandicap2();
		if (sPosition.Equals(initialPosition))
		{
			handicap = Handicap.H2;
			return;
		}
		sPosition.SetHandicap3();
		if (sPosition.Equals(initialPosition))
		{
			handicap = Handicap.H3;
			return;
		}
		sPosition.SetHandicap4();
		if (sPosition.Equals(initialPosition))
		{
			handicap = Handicap.H4;
			return;
		}
		sPosition.SetHandicap5();
		if (sPosition.Equals(initialPosition))
		{
			handicap = Handicap.H5;
			return;
		}
		sPosition.SetHandicapLeft5();
		if (sPosition.Equals(initialPosition))
		{
			handicap = Handicap.LEFT5;
			return;
		}
		sPosition.SetHandicap6();
		if (sPosition.Equals(initialPosition))
		{
			handicap = Handicap.H6;
			return;
		}
		sPosition.SetHandicap8();
		if (sPosition.Equals(initialPosition))
		{
			handicap = Handicap.H8;
			return;
		}
		sPosition.SetHandicap10();
		if (sPosition.Equals(initialPosition))
		{
			handicap = Handicap.H10;
			return;
		}
		IsOutputInitialPosition = true;
		handicap = Handicap.OTHER;
	}

	private PlayerColor GetWinColor(MoveNode node)
	{
		PlayerColor playerColor = PlayerColor.NoColor;
		switch (node.MoveType)
		{
		default:
			playerColor = node.Turn.Opp();
			break;
		case MoveType.WinFoul:
		case MoveType.WinNyugyoku:
			playerColor = node.Turn;
			break;
		case MoveType.Stop:
		case MoveType.Repetition:
		case MoveType.Draw:
		case MoveType.RepeSup:
		case MoveType.RepeInf:
			break;
		}
		return playerColor;
	}

	public void SetMarker(string marker)
	{
		moveCurrent.Marker = marker;
	}

	public void SetMarker(MoveNode node, string marker)
	{
		FindMoveNode(node).Marker = marker;
	}

	public IDictionary<uint, MoveNode> GetMarkerList()
	{
		Dictionary<uint, MoveNode> marker = new Dictionary<uint, MoveNode>();
		getMarkerList(moveFirst, marker);
		return marker;
	}

	private void getMarkerList(MoveNode node, Dictionary<uint, MoveNode> marker)
	{
		if (node.Marker != null)
		{
			marker.Add(node.Id, node);
		}
		if (node.Children.Count == 0)
		{
			return;
		}
		_ = node.ChildCurrent;
		foreach (MoveNode child in node.Children)
		{
			getMarkerList(child, marker);
		}
	}
}
