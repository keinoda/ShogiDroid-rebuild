using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using ShogiGUI.Events;
using ShogiGUI.Engine;
using ShogiLib;

namespace ShogiGUI.Models;

public class NotationModel
{
	public enum ChangeState
	{
		Initialized,
		Loaded,
		Modified
	}

	public const string TempFilename = "temp.kif";

	private SNotation notation = new SNotation();

	private string kifuFilename;

	private UndoManager undoManager = new UndoManager();

	private ChangeState changeState;

	public SNotation Notation
	{
		get
		{
			return notation;
		}
		set
		{
			notation = value;
		}
	}

	public string FileName
	{
		get
		{
			return kifuFilename;
		}
		set
		{
			kifuFilename = value;
		}
	}

	public ChangeState State => changeState;

	public event EventHandler<NotationEventArgs> NotationChanged;

	public NotationModel()
	{
		kifuFilename = string.Empty;
		notation.KifuInfos["開始日時"] = DateTime.Now.ToString();
		changeState = ChangeState.Initialized;
	}

	public void OnNotationChangedPublic(NotationEventArgs e)
	{
		OnNotationChanged(e);
	}

	protected virtual void OnNotationChanged(NotationEventArgs e)
	{
		if (this.NotationChanged != null)
		{
			this.NotationChanged(this, e);
		}
	}

	public string GetFileName()
	{
		string text;
		if (notation.KifuInfos.Contains("開始日時"))
		{
			text = notation.KifuInfos["開始日時"].ToString();
			text = text.Replace(" ", "_");
			text = text.Replace(":", string.Empty);
			text = text.Replace("/", string.Empty);
		}
		else
		{
			DateTime now = DateTime.Now;
			text = $"{now.Year:D4}{now.Month:D2}{now.Day:D2}_{now.Hour:D2}{now.Minute:D2}{now.Second:D2}";
		}
		if (notation.BlackName != string.Empty || notation.WhiteName != string.Empty)
		{
			text += $"_{notation.BlackName}vs{notation.WhiteName}";
		}
		text += ".kif";
		return text.ReplaceInvalidFileNameChars();
	}

	public void Initialize()
	{
		kifuFilename = string.Empty;
		notation.Init();
		notation.KifuInfos["開始日時"] = DateTime.Now.ToString();
		undoManager.Reset();
		changeState = ChangeState.Initialized;
		OnNotationChanged(new NotationEventArgs(NotationEventId.INIT));
	}

	public void Initialize(GameStartPosition startPosition, GameStartMode startMode, string blackName, string whiteName, Handicap handicap)
	{
		if (startPosition == GameStartPosition.InitialPosition)
		{
			kifuFilename = string.Empty;
			notation.Init();
			notation.BlackName = blackName;
			notation.WhiteName = whiteName;
			notation.KifuInfos["開始日時"] = DateTime.Now.ToString();
			notation.Handicap = handicap;
			changeState = ChangeState.Modified;
			OnNotationChanged(new NotationEventArgs(NotationEventId.INIT));
			return;
		}
		notation.Continue(remove_stop: true);
		if (startMode == GameStartMode.NewGame)
		{
			kifuFilename = string.Empty;
			notation.BlackName = blackName;
			notation.WhiteName = whiteName;
			notation.KifuInfos["開始日時"] = DateTime.Now.ToString();
			notation.DeleteNotCurrent();
		}
		changeState = ChangeState.Modified;
		OnNotationChanged(new NotationEventArgs(NotationEventId.INIT));
	}

	public void Load(string filename)
	{
		Encoding encoding = Encoding.GetEncoding(932);
		try
		{
			string text = Path.GetExtension(filename).ToLower();
			if (text.ToLower() == ".csa")
			{
				Csa csa = new Csa();
				if (filename.StartsWith("assets/"))
				{
					filename = filename.Substring(7);
					csa.Load(notation, EmbResource.Open(filename));
				}
				else
				{
					csa.Load(notation, filename);
				}
				return;
			}
			Kifu kifu = new Kifu();
			encoding = ((text.Length == 0 || text[text.Length - 1] != 'u') ? StringUtil.GetEncording(filename) : Encoding.UTF8);
			if (filename.StartsWith("assets/"))
			{
				filename = filename.Substring(7);
				kifu.Load(notation, EmbResource.Open(filename), encoding);
			}
			else
			{
				kifu.Load(notation, filename, encoding);
			}
			if (notation.Result == MoveType.Stop || notation.Result == MoveType.NoMove)
			{
				notation.Last();
			}
		}
		catch (Exception ex)
		{
			throw ex;
		}
		finally
		{
			undoManager.Reset();
			if (Path.GetDirectoryName(filename) == LocalFile.KifPath)
			{
				kifuFilename = Path.GetFileName(filename);
				changeState = ChangeState.Loaded;
			}
			else
			{
				kifuFilename = string.Empty;
				changeState = ChangeState.Modified;
			}
			RestoreScoresFromComments();
			OnNotationChanged(new NotationEventArgs(NotationEventId.LOAD));
		}
	}

	public void LoadTemp(string filename)
	{
		Load(Path.Combine(LocalFile.PersonalFolderPath, "temp.kif"));
		kifuFilename = filename;
	}

	public void LoadFromString(string str)
	{
		try
		{
			if (Csa.IsCsa(str))
			{
				new Csa().LoadFromString(notation, str);
			}
			else if (Sfen.IsSfen(str))
			{
				Sfen.LoadNotation(notation, str);
			}
			else
			{
				new Kifu().FromString(notation, str);
			}
			if (notation.Result == MoveType.Stop || notation.Result == MoveType.NoMove)
			{
				notation.Last();
			}
		}
		catch (Exception ex)
		{
			throw ex;
		}
		finally
		{
			kifuFilename = string.Empty;
			changeState = ChangeState.Modified;
			RestoreScoresFromComments();
			OnNotationChanged(new NotationEventArgs(NotationEventId.LOAD));
		}
	}

	/// <summary>
	/// 解析コメントからMoveNode.Scoreを復元（棋譜再読み込み時の評価グラフ表示用）
	/// </summary>
	private void RestoreScoresFromComments()
	{
		foreach (MoveNode moveNode in notation.MoveNodes)
		{
			if (moveNode.HasScore) continue;
			foreach (string comment in moveNode.CommentList)
			{
				if (string.IsNullOrEmpty(comment) || comment[0] != '*') continue;
				var pvInfo = AnalyzeInfoList.Parse(comment);
				if (pvInfo.HasEval)
				{
					moveNode.Score = pvInfo.Eval;
					break;
				}
			}
		}
	}

	private Dictionary<HashKey, List<BookMove>> activeBook;

	/// <summary>
	/// 定跡閲覧モードが有効かどうか
	/// </summary>
	public bool IsBookBrowseMode => activeBook != null && activeBook.Count > 0;

	public Dictionary<string, List<BookMove>> ParseBookFile(string filename)
	{
		string ext = Path.GetExtension(filename).ToLower();
		if (ext == ".sbk")
		{
			// 同名のdbファイルがあればそちらを使用
			string dbPath = Path.ChangeExtension(filename, ".db");
			if (System.IO.File.Exists(dbPath))
			{
				return BookParser.LoadDb(dbPath);
			}

			// sbkを読み込み→db形式で保存
			var book = BookParser.LoadSbk(filename);
			try
			{
				BookParser.SaveDb(book, dbPath);
			}
			catch { }
			return book;
		}
		return BookParser.LoadDb(filename);
	}

	/// <summary>
	/// 定跡閲覧モード開始: HashKey辞書を保持して現在局面に適用
	/// </summary>
	public void StartBookBrowse(Dictionary<HashKey, List<BookMove>> hashBook)
	{
		activeBook = hashBook;
		notation.Init();
		notation.InitHashKey();
		ApplyBookAtCurrentPosition();
		undoManager.Reset();
		kifuFilename = string.Empty;
		changeState = ChangeState.Modified;
		OnNotationChanged(new NotationEventArgs(NotationEventId.LOAD));
	}

	/// <summary>
	/// 現在局面に定跡候補手を分岐として追加する
	/// </summary>
	public bool ApplyBookAtCurrentPosition()
	{
		if (activeBook == null)
		{
			return false;
		}
		return BookExpander.ApplyBookAtPosition(notation, activeBook);
	}

	/// <summary>
	/// 定跡閲覧モード終了
	/// </summary>
	public void StopBookBrowse()
	{
		activeBook = null;
	}

	public void OnBookLoaded()
	{
		undoManager.Reset();
		kifuFilename = string.Empty;
		changeState = ChangeState.Modified;
		OnNotationChanged(new NotationEventArgs(NotationEventId.LOAD));
	}

	public void Save(string filename)
	{
		try
		{
			save_impl(filename);
		}
		finally
		{
			LocalFile.ScanFile(filename);
			kifuFilename = Path.GetFileName(filename);
		}
	}

	public void SaveTemp()
	{
		try
		{
			save_impl(Path.Combine(LocalFile.PersonalFolderPath, "temp.kif"));
		}
		catch
		{
		}
	}

	private void save_impl(string filename)
	{
		Kifu kifu = new Kifu();
		string text = Path.GetExtension(filename).ToLower();
		kifu.Save(encode: (text.Length == 0 || text[text.Length - 1] != 'u') ? Encoding.GetEncoding(932) : Encoding.UTF8, notation: notation, filename: filename);
	}

	public bool AddMove(MoveDataEx moveData, MoveAddMode mode, bool changeChildCurrent)
	{
		bool num = notation.AddMove(moveData, mode, changeChildCurrent);
		changeState = ChangeState.Modified;
		if (num)
		{
			ApplyBookAtCurrentPosition();
			OnNotationChanged(new NotationEventArgs(NotationEventId.MAKE_MOVE));
		}
		return num;
	}

	public void Matta()
	{
		notation.Matta();
		changeState = ChangeState.Modified;
		OnNotationChanged(new NotationEventArgs(NotationEventId.OTHER));
	}

	public void InputCancel()
	{
		notation.Back();
		changeState = ChangeState.Modified;
		OnNotationChanged(new NotationEventArgs(NotationEventId.OTHER));
	}

	public bool Prev()
	{
		bool result = notation.Prev(1);
		ApplyBookAtCurrentPosition();
		OnNotationChanged(new NotationEventArgs(NotationEventId.PREV));
		return result;
	}

	public bool Next()
	{
		bool result = notation.Next(1);
		ApplyBookAtCurrentPosition();
		OnNotationChanged(new NotationEventArgs(NotationEventId.NEXT));
		return result;
	}

	public void Jump(int number)
	{
		notation.ChangeCurrent(number);
		ApplyBookAtCurrentPosition();
		OnNotationChanged(new NotationEventArgs(NotationEventId.OTHER));
	}

	public void Continue(bool flag)
	{
		if (notation.Continue(flag))
		{
			OnNotationChanged(new NotationEventArgs(NotationEventId.OTHER));
		}
	}

	public void First()
	{
		notation.First();
		ApplyBookAtCurrentPosition();
		OnNotationChanged(new NotationEventArgs(NotationEventId.OTHER));
	}

	public void Last()
	{
		notation.Last();
		OnNotationChanged(new NotationEventArgs(NotationEventId.OTHER));
	}

	public void ChangeBranch(int number, int child)
	{
		notation.ChangeCurrent(number);
		notation.ChangeChildCurrent(child);
		ApplyBookAtCurrentPosition();
		OnNotationChanged(new NotationEventArgs(NotationEventId.OTHER));
	}

	public void NextChild(int child)
	{
		notation.MoveCurrent.ChangeChildCurrent(child);
		notation.Next(1);
		ApplyBookAtCurrentPosition();
		OnNotationChanged(new NotationEventArgs(NotationEventId.OTHER));
	}

	public void EditBoard(SNotation notation)
	{
		notation.InitHashKey();
		notation.DecisionHandicap();
		this.notation = notation;
		OnNotationChanged(new NotationEventArgs(NotationEventId.OBJECT_CHANGED));
	}

	public void AddBranch(MoveData ponder, List<MoveDataEx> moveDataList)
	{
		if (moveDataList.Count != 0)
		{
			MoveNode move_node = notation.MoveCurrent;
			if (Notation.Position.Turn != moveDataList[0].Turn)
			{
				move_node = notation.MovePrev;
			}
			notation.AddBranches(ponder, moveDataList, move_node, MoveAddMode.ADD);
			OnNotationChanged(new NotationEventArgs(NotationEventId.OTHER));
		}
	}

	public void AddComment(string comment)
	{
		notation.MoveCurrent.CommentAdd(comment);
		OnNotationChanged(new NotationEventArgs(NotationEventId.COMMENT));
	}

	public void SetComment(string comment)
	{
		notation.MoveCurrent.CommentList.Clear();
		string[] array = comment.Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string str in array)
		{
			notation.MoveCurrent.CommentAdd(str);
		}
		notation.MoveCurrent.UpdateCommentCount();
		OnNotationChanged(new NotationEventArgs(NotationEventId.COMMENT));
	}

	public static void SetMoves(SNotation notation, SPosition position, MoveData ponder, List<MoveDataEx> moveDataList)
	{
		notation.Init();
		notation.SetInitialPosition(position);
		notation.DecisionHandicap();
		if (ponder != null && !position.MoveLast.Equals(ponder))
		{
			if (!MoveCheck.IsValid(notation.Position, ponder))
			{
				return;
			}
			notation.AddMove(new MoveDataEx(ponder));
		}
		if (moveDataList != null && moveDataList.Count != 0)
		{
			int i = 0;
			if (position.MoveLast.Equals(moveDataList[0]))
			{
				i = 1;
			}
			for (; i < moveDataList.Count; i++)
			{
				MoveData moveData = moveDataList[i];
				if (!MoveCheck.IsValid(notation.Position, moveData))
				{
					break;
				}
				notation.AddMove(new MoveDataEx(moveData));
			}
		}
		notation.First();
	}

	public static void SetMoves(SNotation notation, SPosition position, string str)
	{
		notation.Init();
		notation.SetInitialPosition(position);
		notation.DecisionHandicap();
		if (!string.IsNullOrEmpty(str))
		{
			new Kifu().ParseMoves(notation, MoveAddMode.ADD, str);
		}
		notation.First();
	}
}
