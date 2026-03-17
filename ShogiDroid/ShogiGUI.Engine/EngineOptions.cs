using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace ShogiGUI.Engine;

public class EngineOptions
{
	[XmlIgnore]
	public bool All;

	[XmlArray("optionList")]
	[XmlArrayItem("option")]
	public List<EngineOption> OptionList = new List<EngineOption>();

	public EngineOptions()
	{
	}

	public EngineOptions(EngineOptions info)
	{
		foreach (EngineOption option in info.OptionList)
		{
			OptionList.Add(new EngineOption(option));
		}
	}

	public EngineOption GetOption(string key)
	{
		return OptionList.Find((EngineOption en_opt) => en_opt.Key == key);
	}

	public void SetOption(string key, object value)
	{
		EngineOption option = GetOption(key);
		if (option == null)
		{
			option = new EngineOption(key, value);
			OptionList.Add(option);
		}
		else
		{
			option.Value = value;
		}
	}

	public static void Save(string filename, EngineOptions engineInfo)
	{
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(EngineOptions));
		try
		{
			using FileStream stream = new FileStream(filename, FileMode.Create);
			xmlSerializer.Serialize(stream, engineInfo);
		}
		catch (Exception)
		{
		}
	}

	public static EngineOptions Load(string filename)
	{
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(EngineOptions));
		EngineOptions engineOptions;
		try
		{
			using FileStream stream = new FileStream(filename, FileMode.Open);
			engineOptions = (EngineOptions)xmlSerializer.Deserialize(stream);
		}
		catch (Exception)
		{
			engineOptions = new EngineOptions();
		}
		engineOptions.All = true;
		return engineOptions;
	}
}
