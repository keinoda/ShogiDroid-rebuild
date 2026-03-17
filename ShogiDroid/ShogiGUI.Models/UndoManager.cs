using System.Collections.Generic;
using ShogiLib;

namespace ShogiGUI.Models;

public class UndoManager
{
	private Stack<SNotation> undoStack;

	private Stack<SNotation> redoStack;

	public int UndoCount => undoStack.Count;

	public int RedoCount => redoStack.Count;

	public UndoManager()
	{
		undoStack = new Stack<SNotation>();
		redoStack = new Stack<SNotation>();
	}

	public void Reset()
	{
		undoStack.Clear();
		redoStack.Clear();
	}

	public void Keep(SNotation notation)
	{
		undoStack.Push(new SNotation(notation));
		redoStack.Clear();
	}

	public SNotation Undo(SNotation in_notation)
	{
		SNotation result;
		if (undoStack.Count > 0)
		{
			result = undoStack.Pop();
			redoStack.Push(in_notation);
		}
		else
		{
			result = in_notation;
		}
		return result;
	}

	public SNotation Redo(SNotation in_notation)
	{
		SNotation result;
		if (redoStack.Count > 0)
		{
			result = redoStack.Pop();
			undoStack.Push(in_notation);
		}
		else
		{
			result = in_notation;
		}
		return result;
	}
}
