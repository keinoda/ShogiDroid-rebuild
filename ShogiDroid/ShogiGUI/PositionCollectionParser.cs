using System;
using System.Collections.Generic;
using System.IO;
using ShogiLib;

namespace ShogiGUI;

public sealed class PositionCollectionEntry
{
	public int Index { get; }

	public int LineNumber { get; }

	public string Sfen { get; }

	public PositionCollectionEntry(int index, int lineNumber, string sfen)
	{
		Index = index;
		LineNumber = lineNumber;
		Sfen = sfen;
	}
}

public static class PositionCollectionParser
{
	public static List<PositionCollectionEntry> Parse(string text)
	{
		List<PositionCollectionEntry> entries = new List<PositionCollectionEntry>();
		if (string.IsNullOrWhiteSpace(text))
		{
			return entries;
		}

		using StringReader reader = new StringReader(text);
		string line;
		int lineNumber = 0;
		while ((line = reader.ReadLine()) != null)
		{
			lineNumber++;
			string trimmed = NormalizeLine(line);
			if (trimmed == string.Empty || IsComment(trimmed))
			{
				continue;
			}

			if (!LooksLikePositionLine(trimmed) || !CanParse(trimmed))
			{
				return new List<PositionCollectionEntry>();
			}

			entries.Add(new PositionCollectionEntry(entries.Count, lineNumber, trimmed));
		}

		return entries;
	}

	private static string NormalizeLine(string line)
	{
		return (line ?? string.Empty).Trim().TrimStart('\uFEFF');
	}

	private static bool IsComment(string line)
	{
		return line.StartsWith("#", StringComparison.Ordinal)
			|| line.StartsWith("//", StringComparison.Ordinal)
			|| line.StartsWith(";", StringComparison.Ordinal);
	}

	private static bool LooksLikePositionLine(string line)
	{
		return line.StartsWith("sfen ", StringComparison.OrdinalIgnoreCase)
			|| line.StartsWith("position ", StringComparison.OrdinalIgnoreCase)
			|| line.StartsWith("startpos", StringComparison.OrdinalIgnoreCase);
	}

	private static bool CanParse(string line)
	{
		try
		{
			SNotation notation = new SNotation();
			Sfen.LoadNotation(notation, line);
			return true;
		}
		catch
		{
			return false;
		}
	}
}
