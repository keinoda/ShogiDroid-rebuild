using System.Collections.Generic;

namespace ShogiLib;

public class MoveNode : MoveDataEx
{
	private HashKey key;

	private static uint count = 1u;

	private uint id;

	public List<MoveNode> Children { get; private set; }

	public MoveNode ChildCurrent { get; private set; }

	public MoveNode Parent { get; private set; }

	public uint Id => id;

	public HashKey Key
	{
		get
		{
			return key;
		}
		set
		{
			key = value;
		}
	}

	public IEnumerable<MoveNode> ChildCurrents
	{
		get
		{
			MoveNode info = ChildCurrent;
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

	public MoveNode()
	{
		Children = new List<MoveNode>();
		ChildCurrent = null;
		Parent = null;
		id = count++;
	}

	public MoveNode(MoveData move_data, HashKey key)
		: base(move_data)
	{
		Children = new List<MoveNode>();
		ChildCurrent = null;
		Parent = null;
		this.key = key;
		id = count++;
	}

	public MoveNode(MoveDataEx move_data)
		: base(move_data)
	{
		Children = new List<MoveNode>();
		ChildCurrent = null;
		Parent = null;
		id = count++;
	}

	public MoveNode(MoveNode move_node)
		: base(move_node)
	{
		Children = new List<MoveNode>();
		ChildCurrent = null;
		Parent = null;
		key = move_node.key;
		id = move_node.id;
	}

	public override void Initialize()
	{
		base.Initialize();
		Children.Clear();
		ChildCurrent = null;
		Parent = null;
	}

	public bool IsEqual(MoveNode node)
	{
		bool result = false;
		if (node == null)
		{
			return false;
		}
		if (id == node.id && Equals(node) && key.Equals(node.Key))
		{
			result = true;
		}
		return result;
	}

	public MoveNode FindChild(MoveData moveData)
	{
		for (int num = Children.Count - 1; num >= 0; num--)
		{
			MoveNode moveNode = Children[num];
			if (moveNode.Equals(moveData))
			{
				return moveNode;
			}
		}
		return null;
	}

	public int FindChildIndex(MoveData moveData)
	{
		for (int num = Children.Count - 1; num >= 0; num--)
		{
			if (Children[num].Equals(moveData))
			{
				return num;
			}
		}
		return 0;
	}

	public MoveNode MergeChild(MoveNode move_node, bool changeChildCurrent)
	{
		MoveNode moveNode = FindChild(move_node);
		if (moveNode == null)
		{
			Children.Add(move_node);
			if (changeChildCurrent || ChildCurrent == null)
			{
				ChildCurrent = move_node;
			}
			move_node.Parent = this;
			moveNode = move_node;
		}
		else
		{
			if (changeChildCurrent || ChildCurrent == null)
			{
				ChildCurrent = moveNode;
			}
			if (move_node.HasScore)
			{
				moveNode.Score = move_node.Score;
			}
			if (move_node.CommentList.Count != 0)
			{
				moveNode.CommentList.AddRange(move_node.CommentList);
				moveNode.UpdateCommentCount();
			}
		}
		return moveNode;
	}

	public void AddChild(MoveNode move_node, bool changeChildCurrent)
	{
		Children.Add(move_node);
		if (changeChildCurrent || ChildCurrent == null)
		{
			ChildCurrent = move_node;
		}
		move_node.Parent = this;
	}

	public void InsertChild(int index, MoveNode move_node, bool changeChildCurrent)
	{
		Children.Insert(index, move_node);
		if (changeChildCurrent || ChildCurrent == null)
		{
			ChildCurrent = move_node;
		}
		move_node.Parent = this;
	}

	public bool ChangeChildCurrent(int index)
	{
		bool result = false;
		if (index < Children.Count && ChildCurrent != Children[index])
		{
			ChildCurrent = Children[index];
			result = true;
		}
		return result;
	}

	public void ChangeChildCurrent(MoveNode move_node)
	{
		if (move_node == ChildCurrent)
		{
			return;
		}
		foreach (MoveNode child in Children)
		{
			if (child == move_node)
			{
				ChildCurrent = child;
				break;
			}
		}
	}

	public void RemoveChild(int index)
	{
		if (index >= 0 && index < Children.Count)
		{
			Children.RemoveAt(index);
			if (Children.Count == 0)
			{
				ChildCurrent = null;
			}
			else
			{
				ChildCurrent = Children[0];
			}
		}
	}

	public void RemoveChild(MoveNode node)
	{
		Children.Remove(node);
		if (Children.Count == 0)
		{
			ChildCurrent = null;
		}
		else if (ChildCurrent == node)
		{
			ChildCurrent = Children[0];
		}
	}

	public void ChangeChildOrder(int to, int from)
	{
		MoveNode item = Children[from];
		if (to < from)
		{
			Children.RemoveAt(from);
			Children.Insert(to, item);
		}
		else
		{
			Children.Insert(to + 1, item);
			Children.RemoveAt(from);
		}
	}

	public int GetChildIndex(MoveNode moveNode)
	{
		int num = 0;
		foreach (MoveNode child in Children)
		{
			if (child == moveNode)
			{
				return num;
			}
			num++;
		}
		return -1;
	}

	public int GetChildIndex()
	{
		int num = 0;
		foreach (MoveNode child in Children)
		{
			if (child == ChildCurrent)
			{
				return num;
			}
			num++;
		}
		return 0;
	}

	public bool InitChildCurrent()
	{
		bool result = false;
		if (Children.Count >= 1 && ChildCurrent != Children[0])
		{
			ChildCurrent = Children[0];
			result = true;
		}
		foreach (MoveNode child in Children)
		{
			if (child.InitChildCurrent())
			{
				result = true;
			}
		}
		return result;
	}

	public void ClearChildren()
	{
		ChildCurrent = null;
		Children.Clear();
	}

	public void DeleteNotCurrent()
	{
		Children.Clear();
		if (ChildCurrent != null)
		{
			Children.Add(ChildCurrent);
		}
	}

	public bool Merge(MoveNode node)
	{
		if (!Equals(node))
		{
			return false;
		}
		if (!base.HasScore && node.HasScore)
		{
			base.Score = node.Score;
		}
		if (node.CommentList.Count != 0)
		{
			base.CommentList.AddRange(node.CommentList);
			UpdateCommentCount();
		}
		foreach (MoveNode child in node.Children)
		{
			if (ChildCurrent == null)
			{
				ChildCurrent = child;
			}
			MoveNode moveNode = FindChild(child);
			if (moveNode == null)
			{
				Children.Add(child);
				child.Parent = this;
			}
			else
			{
				moveNode.Merge(child);
			}
		}
		return true;
	}

	public bool AddChildren(MoveNode node)
	{
		if (!base.HasScore && node.HasScore)
		{
			base.Score = node.Score;
		}
		if (node.CommentList.Count != 0)
		{
			base.CommentList.AddRange(node.CommentList);
			UpdateCommentCount();
		}
		foreach (MoveNode child in node.Children)
		{
			Children.Add(DeepCopy(this, child));
			if (ChildCurrent == null)
			{
				ChildCurrent = Children[0];
			}
		}
		return true;
	}

	public static MoveNode DeepCopy(MoveNode parent, MoveNode move_node)
	{
		MoveNode moveNode = new MoveNode(move_node);
		moveNode.Parent = parent;
		if (parent != null)
		{
			moveNode.Number = parent.Number + 1;
		}
		foreach (MoveNode child in move_node.Children)
		{
			moveNode.Children.Add(DeepCopy(moveNode, child));
		}
		if (move_node.ChildCurrent != null)
		{
			moveNode.ChildCurrent = moveNode.Children[0];
			foreach (MoveNode child2 in moveNode.Children)
			{
				if (child2.key.Equals(move_node.ChildCurrent.key))
				{
					moveNode.ChildCurrent = child2;
					break;
				}
			}
		}
		return moveNode;
	}
}
