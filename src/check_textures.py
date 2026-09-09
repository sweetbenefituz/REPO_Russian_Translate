# -*- coding: utf-8 -*-
"""Проверка, что каждой нашей картинке есть текстура с таким же именем в игре.

Запуск:  python check_textures.py

Плагин ищет текстуры по имени, сведённому к одному виду: только буквы и цифры
в нижнем регистре, номер выгрузчика спереди срезается. В игре имена с
пробелами и амперсандом (`cosmetic_danger tape & expert goggles_Albedo`), у нас
те же места стали подчёркиваниями (`resources_3512_cosmetic_danger_tape___...`).
Правило свести обязано их в одно. Тут это и проверяется — до запуска игры.

Путь к игре берётся из src/Local.props (GameManaged), он в git не попадает.
"""
import io
import os
import re
import sys

SRC = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(SRC)
TEXTURES = os.path.join(PROJECT, "mod", "Textures")

# в именах ассетов Unity встречается и амперсанд, и скобки, и дефис
NAME = re.compile(rb"[A-Za-z0-9_ ()&+.,'-]{4,120}")


def game_data():
    path = os.path.join(SRC, "Local.props")
    if not os.path.isfile(path):
        sys.exit("Нет src/Local.props — скопируй Local.props.example и впиши свои пути.")
    found = re.search(r"<GameManaged>(.+?)</GameManaged>",
                      io.open(path, encoding="utf-8-sig").read())
    if not found:
        sys.exit("В src/Local.props не указан GameManaged.")
    return os.path.dirname(found.group(1).strip())


def key(name):
    """То же правило, что в TexturePatch.Key — держать в синхроне."""
    plain = "".join(c.lower() for c in name if c.isalnum())
    if plain.startswith("resources"):
        at = len("resources")
        while at < len(plain) and plain[at].isdigit():
            at += 1
        if at > len("resources"):
            return plain[at:]
    return plain


def main():
    data = game_data()
    files = sorted(f for f in os.listdir(TEXTURES) if f.lower().endswith(".png"))
    if not files:
        sys.exit("В mod/Textures нет картинок.")

    names = set()
    for chunk in ("resources.assets", "sharedassets0.assets"):
        path = os.path.join(data, chunk)
        if not os.path.isfile(path):
            sys.exit("Не найден файл игры: " + path)
        for m in NAME.findall(io.open(path, "rb").read()):
            names.add(m.decode("ascii"))

    have = set(key(n) for n in names)
    missing = [f for f in files if key(os.path.splitext(f)[0]) not in have]

    print("картинок: %d, имён в файлах игры: %d" % (len(files), len(names)))
    if missing:
        print("НЕ НАШЛОСЬ ТЕКСТУРЫ В ИГРЕ (%d) — такие в игре не заменятся:" % len(missing))
        for f in missing:
            print("   %s   (искали имя %r)" % (f, key(os.path.splitext(f)[0])))
        sys.exit(1)
    print("все имена сошлись")


if __name__ == "__main__":
    main()
