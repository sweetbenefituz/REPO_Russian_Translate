using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace SweetRussianTranslate;

/// <summary>
/// Подменяет текстуры с английскими надписями — вывески, плакаты, экраны.
/// Файлы игры не трогаем: находим уже загруженные материалы и подставляем им
/// нашу картинку вместо оригинальной.
/// </summary>
internal static class TexturePatch
{
	/// <summary>Как часто искать новые материалы, секунды.</summary>
	private const float ScanEvery = 2f;

	/// <summary>Ключ имени -> путь к нашему PNG.</summary>
	private static readonly Dictionary<string, string> Files = new Dictionary<string, string>();

	/// <summary>Ключ имени -> уже загруженная наша текстура: PNG читаем один раз.</summary>
	private static readonly Dictionary<string, Texture2D> Loaded = new Dictionary<string, Texture2D>();

	/// <summary>
	/// Шейдер -> имена его текстурных свойств. Спрашивать это у шейдера на каждом
	/// проходе дорого, а меняться оно не может: шейдер один и тот же весь запуск.
	/// </summary>
	private static readonly Dictionary<Shader, string[]> Properties = new Dictionary<Shader, string[]>();

	/// <summary>Какие картинки хоть раз подставили: для честного счётчика в логе.</summary>
	private static readonly HashSet<string> Done = new HashSet<string>();

	private static int replaced;

	internal static void Init(string modFolder)
	{
		string dir = Path.Combine(modFolder, "Textures");
		if (!Directory.Exists(dir))
		{
			Plugin.Log.LogWarning("Папки Textures нет, надписи на текстурах останутся английскими: " + dir);
			return;
		}
		foreach (string path in Directory.GetFiles(dir, "*.png"))
		{
			string key = Key(Path.GetFileNameWithoutExtension(path));
			if (key.Length == 0)
			{
				continue;
			}
			if (Files.ContainsKey(key))
			{
				Plugin.Log.LogWarning("Два файла на одно имя текстуры, беру первый: " + path);
				continue;
			}
			Files[key] = path;
		}
		Plugin.Log.LogInfo("Текстур на подмену: " + Files.Count);
	}

	/// <summary>
	/// Ищет материалы, которым можно подставить нашу текстуру.
	/// </summary>
	/// ponytail: опрос раз в две секунды, а не патч по месту. Уровни в REPO
	/// собираются на ходу, материалы подгружаются кусками уже после загрузки
	/// сцены, и точный момент готовности нам неизвестен. Найдётся метод
	/// генерации уровня — заменить опрос на постфикс к нему.
	///
	/// Осматриваем ВСЕ материалы каждый раз, а не только новые. Раньше был список
	/// уже осмотренных, и из-за него подмена слетала при второй загрузке карты:
	/// на новом уровне игра заново берёт материал из своих файлов, с английской
	/// картинкой, а мы его пропускали как «уже видели». Повторная замена ничего
	/// не стоит: нашей картинке мы даём другое имя, и второй раз она под замену
	/// уже не подходит.
	internal static IEnumerator Loop()
	{
		if (Files.Count == 0)
		{
			yield break;
		}
		while (true)
		{
			Scan();
			yield return new WaitForSeconds(ScanEvery);
		}
	}

	private static void Scan()
	{
		Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
		int before = replaced;
		foreach (Material material in materials)
		{
			if (material == null)
			{
				continue;
			}
			Shader shader = material.shader;
			if (shader == null)
			{
				continue;
			}
			foreach (string property in TextureProperties(shader))
			{
				Texture current = material.GetTexture(property);
				if (current == null)
				{
					continue;
				}
				Texture2D ours = Ours(current);
				if (ours == null)
				{
					continue;
				}
				material.SetTexture(property, ours);
				replaced++;
				Done.Add(current.name);
				Plugin.Log.LogInfo("Текстура заменена: " + current.name + " (материал "
					+ material.name + ", " + property + ")");
			}
		}
		if (replaced != before)
		{
			Plugin.Log.LogInfo("Всего замен: " + replaced + ", разных картинок: " + Done.Count
				+ " из " + Files.Count);
		}
	}

	/// <summary>Имена текстурных свойств шейдера, посчитанные один раз.</summary>
	private static string[] TextureProperties(Shader shader)
	{
		if (Properties.TryGetValue(shader, out string[] ready))
		{
			return ready;
		}
		List<string> names = new List<string>();
		int count = shader.GetPropertyCount();
		for (int i = 0; i < count; i++)
		{
			if (shader.GetPropertyType(i) == ShaderPropertyType.Texture)
			{
				names.Add(shader.GetPropertyName(i));
			}
		}
		string[] result = names.ToArray();
		Properties[shader] = result;
		return result;
	}

	/// <summary>Наша картинка для этой текстуры игры или null, если такой нет.</summary>
	private static Texture2D Ours(Texture original)
	{
		string key = Key(original.name);
		if (key.Length == 0 || !Files.TryGetValue(key, out string path))
		{
			return null;
		}
		if (Loaded.TryGetValue(key, out Texture2D ready))
		{
			return ready;
		}
		Texture2D ours = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true, linear: false);
		if (!ours.LoadImage(File.ReadAllBytes(path), markNonReadable: true))
		{
			Plugin.Log.LogWarning("PNG не читается, пропускаю: " + path);
			Object.Destroy(ours);
			Loaded[key] = null;
			return null;
		}
		// имя нарочно другое: иначе на следующем проходе примем свою же картинку
		// за английскую и полезем заменять её снова
		ours.name = original.name + " RU";
		ours.wrapMode = original.wrapMode;
		ours.filterMode = original.filterMode;
		ours.anisoLevel = original.anisoLevel;
		// без этого Unity выкинет картинку при смене уровня: на неё ссылается
		// только материал, а сам ассет никем не удерживается
		ours.hideFlags = HideFlags.DontUnloadUnusedAsset;
		Loaded[key] = ours;
		return ours;
	}

	/// <summary>
	/// Сводит имя текстуры в игре и имя нашего файла к одному виду.
	/// В игре имена с пробелами и амперсандом (<c>cosmetic_danger tape &amp; expert goggles_Albedo</c>),
	/// в файле те же места стали подчёркиваниями, да ещё спереди приписан номер
	/// от выгрузчика (<c>resources_3512_...</c>). Оставляем только буквы и цифры
	/// в нижнем регистре, номер выгрузчика срезаем.
	/// </summary>
	private static string Key(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return string.Empty;
		}
		System.Text.StringBuilder plain = new System.Text.StringBuilder(name.Length);
		foreach (char c in name)
		{
			if (char.IsLetterOrDigit(c))
			{
				plain.Append(char.ToLowerInvariant(c));
			}
		}
		string key = plain.ToString();
		const string dump = "resources";
		if (!key.StartsWith(dump, System.StringComparison.Ordinal))
		{
			return key;
		}
		int at = dump.Length;
		while (at < key.Length && char.IsDigit(key[at]))
		{
			at++;
		}
		// "resources" без номера — имя самой игры, не наш префикс: не режем
		return at > dump.Length ? key.Substring(at) : key;
	}
}
