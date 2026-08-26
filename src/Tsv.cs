using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SweetRussianTranslate;

/// <summary>Чтение TSV-файлов мода: две колонки, комментарии с #, \n как перенос строки.</summary>
internal static class Tsv
{
	internal static List<KeyValuePair<string, string>> Read(string fileName)
	{
		List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
		string path = Path.Combine(Plugin.ModFolder, fileName);
		if (!File.Exists(path))
		{
			Plugin.Log.LogWarning("Missing file: " + path);
			return rows;
		}
		try
		{
			foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
			{
				if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
				{
					continue;
				}
				string[] cells = line.Split('\t');
				if (cells.Length == 2 && !string.IsNullOrWhiteSpace(cells[1]))
				{
					rows.Add(new KeyValuePair<string, string>(cells[0], cells[1].Replace(@"\n", "\n")));
				}
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogError("Failed to read " + fileName + ": " + e.Message);
		}
		return rows;
	}
}
