using System.Collections.Generic;
using HarmonyLib;
using TMPro;

namespace SweetRussianTranslate;

/// <summary>Надписи, вшитые в код игры мимо системы локализации. Тексты — в Hardcoded.tsv.</summary>
internal static class HardcodedLabels
{
	private static Dictionary<string, string> labels;

	internal static Dictionary<string, string> All
	{
		get
		{
			if (labels == null)
			{
				labels = new Dictionary<string, string>();
				foreach (KeyValuePair<string, string> row in Tsv.Read("Hardcoded.tsv"))
				{
					labels[row.Key] = row.Value;
				}
				Plugin.Log.LogInfo("Loaded " + labels.Count + " hardcoded label replacements.");
			}
			return labels;
		}
	}

	/// <summary>Подменяет текст, если он есть в словаре. Возвращает true, если подменил.</summary>
	internal static bool Translate(ref string text)
	{
		if (text != null && All.TryGetValue(text, out string translated))
		{
			text = translated;
			return true;
		}
		return false;
	}
}

/// <summary>Надписи на элементах интерфейса: подмена по точному тексту при создании.</summary>
[HarmonyPatch(typeof(SemiUI), "Start")]
internal static class HardcodedLabelPatch
{
	[HarmonyPostfix]
	private static void Postfix(SemiUI __instance)
	{
		if (HardcodedLabels.All.Count == 0)
		{
			return;
		}
		foreach (TMP_Text text in __instance.GetComponentsInChildren<TMP_Text>(true))
		{
			string value = text.text;
			if (HardcodedLabels.Translate(ref value))
			{
				text.text = value;
			}
		}
	}
}

/// <summary>Большие сообщения посреди экрана: часть из них игра пишет мимо локализации.</summary>
[HarmonyPatch(typeof(BigMessageUI), "BigMessage")]
internal static class BigMessagePatch
{
	[HarmonyPrefix]
	private static void Prefix(ref string message)
	{
		HardcodedLabels.Translate(ref message);
	}
}

/// <summary>Экранчик трекера: НАЙДЕН / НЕ НАЙДЕН.</summary>
[HarmonyPatch(typeof(ItemTracker), "DisplayColorOverride")]
internal static class ItemTrackerDisplayPatch
{
	[HarmonyPrefix]
	private static void Prefix(ref string _text)
	{
		HardcodedLabels.Translate(ref _text);
	}
}

/// <summary>Кнопка переназначения клавиши в настройках управления.</summary>
[HarmonyPatch(typeof(MenuBigButton), "StateEdit")]
internal static class MenuBigButtonEditPatch
{
	[HarmonyPostfix]
	private static void Postfix(MenuBigButton __instance)
	{
		// Поле buttonText в игре internal, из мода напрямую не видно.
		TMP_Text label = Traverse.Create(__instance.menuButton).Field("buttonText").GetValue<TMP_Text>();
		if (label == null)
		{
			return;
		}
		string value = label.text;
		if (HardcodedLabels.Translate(ref value))
		{
			label.text = value;
		}
	}
}
