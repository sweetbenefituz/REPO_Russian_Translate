using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SweetRussianTranslate;

/// <summary>
/// Реплики, которые игра пишет в чат от лица персонажа и отправляет в озвучку.
/// Вшиты в код игры, через систему локализации не проходят. Тексты — в Chat.tsv.
/// Строки с ключом в квадратных скобках — списки, из которых берётся случайная.
/// </summary>
internal static class ChatPhrases
{
	private static readonly List<string> Empty = new List<string>();

	private static Dictionary<string, string> replacements;

	private static Dictionary<string, List<string>> lists;

	internal static Dictionary<string, string> Replacements
	{
		get
		{
			Load();
			return replacements;
		}
	}

	internal static List<string> List(string key)
	{
		Load();
		if (lists.TryGetValue(key, out List<string> list))
		{
			return list;
		}
		return Empty;
	}

	/// <summary>Случайная строка из списка, пустая — если списка нет.</summary>
	internal static string Pick(string key)
	{
		List<string> list = List(key);
		if (list.Count == 0)
		{
			return string.Empty;
		}
		return list[Random.Range(0, list.Count)];
	}

	private static void Load()
	{
		if (replacements != null)
		{
			return;
		}
		replacements = new Dictionary<string, string>();
		lists = new Dictionary<string, List<string>>();
		foreach (KeyValuePair<string, string> row in Tsv.Read("Chat.tsv"))
		{
			if (row.Key.StartsWith("[") && row.Key.EndsWith("]"))
			{
				if (!lists.TryGetValue(row.Key, out List<string> list))
				{
					list = new List<string>();
					lists[row.Key] = list;
				}
				list.Add(row.Value);
			}
			else
			{
				replacements[row.Key] = row.Value;
			}
		}
		int listed = 0;
		foreach (KeyValuePair<string, List<string>> pair in lists)
		{
			listed += pair.Value.Count;
		}
		Plugin.Log.LogInfo("Loaded " + replacements.Count + " chat phrases and " + listed + " listed lines in " + lists.Count + " lists.");
	}
}

/// <summary>Реплики с точным текстом: отсчёт до самоуничтожения, прощания, оханье при падении.</summary>
[HarmonyPatch(typeof(ChatManager), "PossessChat")]
internal static class ChatPossessPatch
{
	[HarmonyPrefix]
	private static void Prefix(ref string message)
	{
		if (message != null && ChatPhrases.Replacements.TryGetValue(message, out string translated))
		{
			message = translated;
		}
	}
}

/// <summary>
/// Первая реплика брошенного игрока. Игра собирает её из кусков случайно, по-русски так не
/// согласуешь, поэтому берём готовую строку из списка.
/// </summary>
[HarmonyPatch(typeof(SemiFunc), "MessageGeneratedGetLeftBehind")]
internal static class LeftBehindMessagePatch
{
	[HarmonyPostfix]
	private static void Postfix(ref string __result)
	{
		string line = ChatPhrases.Pick("[LEFT_BEHIND]");
		if (line.Length > 0)
		{
			__result = line;
		}
	}
}

/// <summary>
/// Реплики любовного зелья. Как в оригинале: случайный шаблон плюс случайные слова из
/// словарей. Прилагательные в мужском роде, глаголы в неопределённой форме — ник игрока
/// рода не имеет, согласовывать не с чем.
/// </summary>
[HarmonyPatch(typeof(ValuableLovePotion), "GenerateAffectionateSentence")]
internal static class LovePotionPatch
{
	private const string NoPlayerNearby = "this potion";

	[HarmonyPostfix]
	private static void Postfix(ref string __result, string ___playerName)
	{
		string line = ChatPhrases.Pick("[LOVE_TEMPLATE]");
		if (line.Length == 0)
		{
			return;
		}
		string name = ((___playerName == NoPlayerNearby) ? "это зелье" : ___playerName);
		string text = line.Replace("{playerName}", name)
			.Replace("{adjective}", ChatPhrases.Pick("[LOVE_ADJECTIVE]"))
			.Replace("{intensifier}", ChatPhrases.Pick("[LOVE_INTENSIFIER]"))
			.Replace("{adverb}", ChatPhrases.Pick("[LOVE_ADVERB]"))
			.Replace("{noun}", ChatPhrases.Pick("[LOVE_NOUN]"))
			.Replace("{transitiveVerb}", ChatPhrases.Pick("[LOVE_TRANSITIVE_VERB]"))
			.Replace("{intransitiveVerb}", ChatPhrases.Pick("[LOVE_INTRANSITIVE_VERB]"));
		__result = char.ToUpper(text[0]) + text.Substring(1);
	}
}
