using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace SweetRussianTranslate;

[BepInPlugin(Guid, "Sweet Russian Translate", "0.1.2")]
public class Plugin : BaseUnityPlugin
{
	public const string Guid = "sweet.russiantranslate";

	internal static ManualLogSource Log;

	internal static string ModFolder;

	private void Awake()
	{
		Log = Logger;
		ModFolder = Path.GetDirectoryName(Info.Location);
		Harmony harmony = new Harmony(Guid);
		harmony.PatchAll(Assembly.GetExecutingAssembly());
		// Реплики в чате видно только в игре с людьми, поэтому пишем в лог, сколько
		// методов реально пропатчено: ждём 8.
		int patched = 0;
		foreach (MethodBase method in harmony.GetPatchedMethods())
		{
			patched++;
		}
		Log.LogInfo("Sweet Russian Translate loaded from " + ModFolder + ", patched methods: " + patched);
	}
}
