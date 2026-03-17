using System.Collections.Generic;

namespace ShogiGUI;

public class CommandMap
{
	private class Command
	{
		private OnExecute execute;

		private IsEnableCallback is_enable;

		public CmdNo CmdNo { get; set; }

		public int Id { get; set; }

		public Command(CmdNo cmdno, int id, OnExecute exec, IsEnableCallback isenable)
		{
			CmdNo = cmdno;
			Id = id;
			execute = exec;
			is_enable = isenable;
		}

		public bool IsEnable()
		{
			bool result = true;
			if (is_enable != null)
			{
				result = is_enable();
			}
			return result;
		}

		public void Execute()
		{
			if (execute != null)
			{
				execute();
			}
		}
	}

	public delegate void OnExecute();

	public delegate bool IsEnableCallback();

	private Dictionary<CmdNo, Command> cmdTable = new Dictionary<CmdNo, Command>();

	private Dictionary<int, Command> idTable = new Dictionary<int, Command>();

	public void Add(CmdNo cmdno, int id, OnExecute exec, IsEnableCallback isenable)
	{
		Command command = new Command(cmdno, id, exec, isenable);
		cmdTable.Add(command.CmdNo, command);
		idTable.Add(command.Id, command);
	}

	public bool IsEnable(int id)
	{
		bool result = true;
		if (idTable.ContainsKey(id))
		{
			result = idTable[id].IsEnable();
		}
		return result;
	}

	public bool IsEnable(CmdNo cmdno)
	{
		bool result = true;
		if (cmdTable.ContainsKey(cmdno))
		{
			result = cmdTable[cmdno].IsEnable();
		}
		return result;
	}

	public bool Execute(int id)
	{
		bool result = false;
		if (idTable.ContainsKey(id))
		{
			idTable[id].Execute();
			result = true;
		}
		return result;
	}

	public bool Execute(CmdNo cmdno)
	{
		bool result = false;
		if (cmdTable.ContainsKey(cmdno))
		{
			cmdTable[cmdno].Execute();
			result = true;
		}
		return result;
	}

	public int GetId(CmdNo cmdno)
	{
		int result = -1;
		if (cmdTable.ContainsKey(cmdno))
		{
			result = cmdTable[cmdno].Id;
		}
		return result;
	}
}
