using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore;

namespace SweetRussianTranslate;

/// <summary>Штатная локализация игры: наши TSV кладутся в таблицы игры сразу после её собственных.</summary>
[HarmonyPatch(typeof(LocalizationManager), "Awake")]
internal static class LocalizationOverridePatch
{
	private static readonly string[] TableNames = { "HUD", "Menu", "Game" };

	private static bool fontLoaded;

	[HarmonyPostfix]
	private static void Postfix()
	{
		LoadCyrillicFallbackFont();
		foreach (string tableName in TableNames)
		{
			try
			{
				ApplyTable(tableName);
			}
			catch (Exception e)
			{
				Plugin.Log.LogError("Failed to apply " + tableName + ".tsv: " + e.Message);
			}
		}
	}

	private static void ApplyTable(string tableName)
	{
		List<KeyValuePair<string, string>> rows = Tsv.Read(tableName + ".tsv");
		if (rows.Count == 0)
		{
			return;
		}
		StringTable table = LocalizationSettings.StringDatabase.GetTable(tableName, LocalizationSettings.ProjectLocale);
		if (table == null)
		{
			Plugin.Log.LogError("String table not found: " + tableName);
			return;
		}
		foreach (KeyValuePair<string, string> row in rows)
		{
			StringTableEntry entry = table.GetEntry(row.Key);
			if (entry != null)
			{
				entry.Value = row.Value;
			}
			else
			{
				table.AddEntry(row.Key, row.Value);
			}
		}
		Plugin.Log.LogInfo("Applied " + rows.Count + " strings to table " + tableName + ".");
	}

	private static void LoadCyrillicFallbackFont()
	{
		if (fontLoaded)
		{
			return;
		}
		fontLoaded = true;
		string path = Path.Combine(Plugin.ModFolder, "Teko-Cyrillic.ttf");
		if (!File.Exists(path))
		{
			Plugin.Log.LogError("Font not found: " + path);
			return;
		}
		try
		{
			TMP_FontAsset gameTeko = FindGameTekoAsset();
			TMP_FontAsset cyrillic;
			if (gameTeko != null)
			{
				cyrillic = TMP_FontAsset.CreateFontAsset(new Font(path), gameTeko.faceInfo.pointSize, gameTeko.atlasPadding, gameTeko.atlasRenderMode, gameTeko.atlasWidth, gameTeko.atlasHeight, AtlasPopulationMode.Dynamic, true);
			}
			else
			{
				cyrillic = TMP_FontAsset.CreateFontAsset(new Font(path));
			}
			cyrillic.name = "Teko-Cyrillic SDF";
			cyrillic.hideFlags = HideFlags.DontUnloadUnusedAsset;
			if (gameTeko != null)
			{
				CopyVerticalMetrics(gameTeko, cyrillic);
			}
			TMP_Settings.fallbackFontAssets?.Insert(0, cyrillic);
			foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
			{
				if (font != cyrillic && font.name.Contains("Teko"))
				{
					if (font.fallbackFontAssetTable == null)
					{
						font.fallbackFontAssetTable = new List<TMP_FontAsset>();
					}
					font.fallbackFontAssetTable.Insert(0, cyrillic);
				}
			}
			Plugin.Log.LogInfo("Cyrillic fallback font registered" + ((gameTeko != null) ? (" (matched to " + gameTeko.name + ").") : " (no Teko reference found)."));
		}
		catch (Exception e)
		{
			Plugin.Log.LogError("Failed to load Cyrillic font: " + e);
		}
	}

	private static void CopyVerticalMetrics(TMP_FontAsset from, TMP_FontAsset to)
	{
		FaceInfo source = from.faceInfo;
		FaceInfo target = to.faceInfo;
		target.scale = source.scale;
		target.lineHeight = source.lineHeight;
		target.ascentLine = source.ascentLine;
		target.capLine = source.capLine;
		target.meanLine = source.meanLine;
		target.baseline = source.baseline;
		target.descentLine = source.descentLine;
		target.superscriptOffset = source.superscriptOffset;
		target.superscriptSize = source.superscriptSize;
		target.subscriptOffset = source.subscriptOffset;
		target.subscriptSize = source.subscriptSize;
		target.underlineOffset = source.underlineOffset;
		target.underlineThickness = source.underlineThickness;
		target.strikethroughOffset = source.strikethroughOffset;
		target.strikethroughThickness = source.strikethroughThickness;
		target.tabWidth = source.tabWidth;
		to.faceInfo = target;
		Plugin.Log.LogInfo($"Metrics aligned to {from.name}: pointSize {source.pointSize}, lineHeight {source.lineHeight}, baseline {source.baseline}, ascent {source.ascentLine}, descent {source.descentLine}.");
	}

	private static TMP_FontAsset FindGameTekoAsset()
	{
		foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
		{
			if (font.name.Contains("Teko"))
			{
				return font;
			}
		}
		return null;
	}
}
