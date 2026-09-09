# -*- coding: utf-8 -*-
"""Сборка DLL и упаковка архива для Thunderstore.

Запуск:  python pack.py
Файлы мода берутся из папки MOD по явному списку PAYLOAD: нет файла — не пакуем.
Готовый архив кладётся в папку проекта, рядом с src.
После упаковки те же файлы раскладываются в INSTALL — папку профиля
Thunderstore, чтобы проверить сборку в игре. Источник правды — MOD в
репозитории: папку профиля Thunderstore переписывает при обновлениях.
"""
import io
import json
import os
import re
import shutil
import subprocess
import sys
import zipfile

SRC = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(SRC)
MOD = os.path.join(PROJECT, "mod")


def profile():
    """Имя профиля Thunderstore берём из src/Local.props — он в git не попадает."""
    path = os.path.join(SRC, "Local.props")
    if os.path.isfile(path):
        found = re.search(r"<RepoProfile>(.+?)</RepoProfile>",
                          io.open(path, encoding="utf-8-sig").read())
        if found:
            return found.group(1).strip()
    return "Default"


INSTALL = os.path.join(
    os.environ.get("APPDATA", ""), "Thunderstore Mod Manager", "DataFolder",
    "REPO", "profiles", profile(), "BepInEx", "plugins",
    "Sweet_team-Sweet_Russian_Translate")

PAYLOAD = [
    "manifest.json",
    "icon.png",
    "README.md",
    "CHANGELOG.md",
    "LICENSE",
    "OFL.txt",
    "SweetRussianTranslate.dll",
    "Teko-Cyrillic.ttf",
    "Menu.tsv",
    "HUD.tsv",
    "Game.tsv",
    "Hardcoded.tsv",
    "Chat.tsv",
]

# Папки с картинками: пакуем целиком, каждый файл внутри — отдельная надпись.
PAYLOAD_DIRS = [
    "Textures",
]


def dir_files(name):
    """Файлы внутри папки мода, путями относительно MOD, в порядке имён."""
    root = os.path.join(MOD, name)
    if not os.path.isdir(root):
        return []
    return [os.path.join(name, f) for f in sorted(os.listdir(root))
            if os.path.isfile(os.path.join(root, f))]


def read(path):
    return io.open(path, encoding="utf-8-sig").read()


def versions():
    """Версия живёт в трёх местах, разъедутся — сайт покажет одно, игра другое."""
    manifest = json.loads(read(os.path.join(MOD, "manifest.json")))["version_number"]
    csproj = re.search(r"<Version>(.+?)</Version>",
                       read(os.path.join(SRC, "SweetRussianTranslate.csproj"))).group(1)
    plugin = re.search(r'BepInPlugin\(Guid, ".*?", "(.+?)"\)',
                       read(os.path.join(SRC, "Plugin.cs"))).group(1)
    return {"manifest.json": manifest, "csproj": csproj, "Plugin.cs": plugin}


def main():
    found = versions()
    if len(set(found.values())) != 1:
        sys.exit("Версии разошлись, не пакую: "
                 + ", ".join("%s=%s" % kv for kv in found.items()))
    version = found["manifest.json"]

    subprocess.check_call(["dotnet", "build", "-c", "Release"], cwd=SRC, shell=True)
    shutil.copy(os.path.join(SRC, "bin", "Release", "SweetRussianTranslate.dll"),
                os.path.join(MOD, "SweetRussianTranslate.dll"))

    missing = [f for f in PAYLOAD if not os.path.isfile(os.path.join(MOD, f))]
    if missing:
        sys.exit("В папке мода нет файлов, не пакую: " + ", ".join(missing))

    empty = [d for d in PAYLOAD_DIRS if not dir_files(d)]
    if empty:
        sys.exit("Папки с картинками пустые или отсутствуют, не пакую: " + ", ".join(empty))

    payload = PAYLOAD + [f for d in PAYLOAD_DIRS for f in dir_files(d)]

    out = os.path.join(PROJECT, "Sweet_Russian_Translate-%s.zip" % version)
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for f in payload:
            z.write(os.path.join(MOD, f), f.replace(os.sep, "/"))

    print("Готово: %s (%d файлов, версия %s)" % (out, len(payload), version))

    # ponytail: раскладка в профиль — простое копирование поверх, без удаления
    # лишнего. Понадобится чистая установка — снести папку профиля руками.
    if os.path.isdir(INSTALL):
        for d in PAYLOAD_DIRS:
            if not os.path.isdir(os.path.join(INSTALL, d)):
                os.makedirs(os.path.join(INSTALL, d))
        for f in payload:
            shutil.copy(os.path.join(MOD, f), os.path.join(INSTALL, f))
        print("Разложено для проверки в игре: %s" % INSTALL)
    else:
        print("Папки профиля нет, в игру не разложил: %s" % INSTALL)


if __name__ == "__main__":
    main()
