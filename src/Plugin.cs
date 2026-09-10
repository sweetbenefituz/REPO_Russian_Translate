using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SweetRussianTranslate;

[BepInPlugin(Guid, "Sweet Russian Translate", "0.2.1")]
// Мягкая зависимость: espeakTTS ставится вместе с модом через Thunderstore, но если
// его снесли руками — перевод должен работать дальше, просто без озвучки. Флаг всё
// равно даёт нужный порядок загрузки: espeakTTS успевает создать свои настройки
// раньше, чем мы их правим.
[BepInDependency(EspeakGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
	public const string Guid = "sweet.russiantranslate";

	private const string EspeakGuid = "Lavighju.espeakTTS";

	private const string EspeakDefaultLanguage = "en";

	private const int EspeakDefaultSpeed = 175;

	private const int RussianSpeed = 100;

	internal static ManualLogSource Log;

	internal static string ModFolder;

	/// <summary>На нём крутим корутины: живёт весь запуск игры, в отличие от уровня.</summary>
	internal static Plugin Instance;

	private void Awake()
	{
		Instance = this;
		Log = Logger;
		ModFolder = Path.GetDirectoryName(Info.Location);
		Harmony harmony = new Harmony(Guid);
		harmony.PatchAll(Assembly.GetExecutingAssembly());
		// Реплики в чате видно только в игре с людьми, поэтому пишем в лог, сколько
		// методов реально пропатчено: ждём 9.
		int patched = 0;
		foreach (MethodBase method in harmony.GetPatchedMethods())
		{
			patched++;
		}
		Log.LogInfo("Sweet Russian Translate loaded from " + ModFolder + ", patched methods: " + patched);
		TuneEspeak();
		TuneTextures();
	}

	/// <summary>
	/// Реплики любовного зелья и прочие фразы персонажа игра отправляет в чат, а всё
	/// из чата зачитывает вслух. Встроенный синтезатор кириллицу не умеет, читает
	/// espeakTTS — но из коробки он настроен на английский.
	/// </summary>
	private void TuneEspeak()
	{
		if (!Config.Bind("Speech", "Tune espeakTTS for Russian speech", true,
				"Настроить espeakTTS под русский, чтобы реплики персонажа читались вслух внятно: "
				+ "язык en -> ru, скорость 175 -> 100 слов в минуту. Трогаются только значения по "
				+ "умолчанию: то, что ты выбрал сам, мод не перебивает. Выключи, если хочешь "
				+ "настраивать espeakTTS сам.").Value)
		{
			return;
		}
		if (!Chainloader.PluginInfos.TryGetValue(EspeakGuid, out PluginInfo info) || info.Instance == null)
		{
			Log.LogInfo("espeakTTS is not installed, Russian speech will stay silent.");
			return;
		}
		ConfigFile espeak = info.Instance.Config;
		Retune(espeak, "Language", EspeakDefaultLanguage, "ru");
		Retune(espeak, "Speed", EspeakDefaultSpeed, RussianSpeed);
	}

	/// <summary>
	/// Надписи на вывесках и плакатах нарисованы прямо на текстурах, таблицами
	/// их не переведёшь — только подменой картинки. Файлы игры при этом не
	/// трогаются, подмена живёт в памяти, см. <see cref="TexturePatch" />.
	/// </summary>
	private void TuneTextures()
	{
		if (!Config.Bind("Textures", "Translate textures", true,
				"Переводить надписи, нарисованные на текстурах: вывески комнат, плакаты, экраны. "
				+ "Выключи, если хочешь видеть их по-английски или подозреваешь их в тормозах. "
				+ "Меняется только при перезапуске игры.").Value)
		{
			Log.LogInfo("Textures translation is off by config, signs stay English.");
			return;
		}
		TexturePatch.Init(ModFolder);
	}

	/// <summary>Ставит своё значение только там, где стоит нетронутое умолчание espeakTTS.</summary>
	private static void Retune<T>(ConfigFile config, string key, T untouched, T wanted)
	{
		if (!config.TryGetEntry(new ConfigDefinition("General", key), out ConfigEntry<T> entry))
		{
			Log.LogWarning("espeakTTS has no General/" + key + " setting, leaving it alone.");
			return;
		}
		if (!Equals(entry.Value, untouched))
		{
			Log.LogInfo("espeakTTS " + key + " is already " + entry.Value + ", leaving it alone.");
			return;
		}
		entry.Value = wanted;
		Log.LogInfo("espeakTTS " + key + " switched from " + untouched + " to " + wanted + ".");
	}
}
