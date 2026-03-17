using System.Collections.Generic;

namespace ShogiGUI.Engine;

public class PvInfos
{
	private SortedDictionary<int, PvInfo> infos = new SortedDictionary<int, PvInfo>();

	private List<PvInfo> infoList = new List<PvInfo>();

	public PvInfo this[int pvnum]
	{
		get
		{
			return infos[pvnum];
		}
		set
		{
			infos[pvnum] = value;
		}
	}

	public int Count => infos.Count;

	public SortedDictionary<int, PvInfo>.ValueCollection Values => infos.Values;

	public List<PvInfo> InfoList => infoList;

	public int TimeMs { get; set; }

	public int Depth { get; set; }

	public int SelDepth { get; set; }

	public long Nodes { get; set; }

	public int Nps { get; set; }

	public void Clear()
	{
		infos.Clear();
		infoList.Clear();
		TimeMs = 0;
		Depth = 0;
		SelDepth = 0;
		Nodes = 0L;
		Nps = 0;
	}

	public bool ContainsKey(int pvnum)
	{
		return infos.ContainsKey(pvnum);
	}

	public PvInfo GetPvInfo(int num, PVDispMode dispMode)
	{
		if (dispMode == PVDispMode.Last)
		{
			if (!Domain.Game.PvInfos.ContainsKey(num + 1))
			{
				return null;
			}
			return Domain.Game.PvInfos[num + 1];
		}
		if (num < 0 || num >= Domain.Game.PvInfos.InfoList.Count)
		{
			return null;
		}
		return Domain.Game.PvInfos.InfoList[num];
	}

	public void Add(PvInfo info)
	{
		if (info == null)
		{
			return;
		}
		if (info.HasTimeMs)
		{
			TimeMs = info.TimeMs;
		}
		if (info.HasDepth)
		{
			Depth = info.Depth;
		}
		if (info.HasSelDepth)
		{
			SelDepth = info.SelDepth;
		}
		if (info.HasNodes)
		{
			Nodes = info.Nodes;
		}
		if (info.HasNPS)
		{
			Nps = info.NPS;
		}
		if (info.PvMoves != null && info.PvMoves.Count != 0)
		{
			int key = ((info.Rank == 0) ? 1 : info.Rank);
			infos[key] = info;
			infoList.Insert(0, info);
			if (infoList.Count > 10)
			{
				infoList.RemoveAt(infoList.Count - 1);
			}
		}
	}

	public static List<PvInfo> LoadPvInfos(IList<string> comments)
	{
		List<PvInfo> list = new List<PvInfo>();
		foreach (string comment in comments)
		{
			if (!string.IsNullOrEmpty(comment) && comment[0] == '*')
			{
				PvInfo item = AnalyzeInfoList.Parse(comment);
				list.Add(item);
			}
		}
		return list;
	}
}
