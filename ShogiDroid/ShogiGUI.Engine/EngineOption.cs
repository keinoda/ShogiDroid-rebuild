using System.Xml.Serialization;

namespace ShogiGUI.Engine;

public class EngineOption
{
	public string Key;

	[XmlElement("value")]
	public object Value;

	[XmlIgnore]
	public bool Changed;

	public EngineOption(string key, object val)
	{
		Key = key;
		Value = val;
	}

	public EngineOption(EngineOption opt)
	{
		Key = opt.Key;
		Value = opt.Value;
	}

	public EngineOption()
	{
		Key = string.Empty;
		Value = string.Empty;
	}
}
