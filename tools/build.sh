#!/usr/bin/env bash
# Builds build/Kotik.exe with the C# compiler that ships with Windows (.NET Framework 4.x): nothing to install.
# csc cannot open files under a Cyrillic path, so it compiles in a temporary ASCII folder and copies the result back.
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p build
[ -f build/app.ico ] || python tools/make_assets.py
CSC="/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
TMP="/c/Windows/Temp/kotik_build_$$"
mkdir -p "$TMP"
cp src/*.cs build/app.ico build/purr.wav build/meow.wav "$TMP/"
( cd "$TMP" && MSYS_NO_PATHCONV=1 "$CSC" -nologo -target:winexe -optimize+ -codepage:65001 -win32icon:app.ico -out:Kotik.exe \
    -resource:purr.wav,Kotik.purr.wav -resource:meow.wav,Kotik.meow.wav \
    -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll *.cs )
cp "$TMP/Kotik.exe" build/Kotik.exe
rm -rf "$TMP"
ls -la build/Kotik.exe
