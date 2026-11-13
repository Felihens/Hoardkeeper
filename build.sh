#!/bin/sh
# Compiles Resource Scanner and installs it into the BepInEx plugins folder.
# Usage: sh build.sh   (or: sh build.sh nocopy  to only compile)
set -e
cd "$(dirname "$0")"

GAME="D:/SteamLibrary/steamapps/common/Valheim"
MW="$GAME/valheim_Data/Managed"
CSC="/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"

mkdir -p bin
"$CSC" -noconfig -nologo -nostdlib -optimize+ -codepage:65001 \
  -target:library -out:bin/Hoardkeeper.dll \
  -r:"$MW/mscorlib.dll" -r:"$MW/netstandard.dll" -r:"$MW/System.dll" -r:"$MW/System.Core.dll" \
  -r:"$MW/UnityEngine.dll" -r:"$MW/UnityEngine.CoreModule.dll" -r:"$MW/UnityEngine.PhysicsModule.dll" -r:"$MW/UnityEngine.InputLegacyModule.dll" \
  -r:"$MW/UnityEngine.AudioModule.dll" -r:"$MW/UnityEngine.UnityWebRequestModule.dll" \
  -r:"$MW/UnityEngine.UnityWebRequestAudioModule.dll" \
  -r:"$MW/UnityEngine.UI.dll" -r:"$MW/UnityEngine.UIModule.dll" -r:"$MW/UnityEngine.TextRenderingModule.dll" -r:"$MW/Unity.TextMeshPro.dll" -r:"$MW/assembly_guiutils.dll" \
  -r:"$MW/assembly_valheim.dll" -r:"$MW/assembly_utils.dll" -r:"$MW/SoftReferenceableAssets.dll" \
  -r:"$GAME/BepInEx/core/BepInEx.dll" -r:"$GAME/BepInEx/core/0Harmony.dll" \
  -recurse:'src\*.cs'

if [ "$1" != "nocopy" ]; then
  mkdir -p "$GAME/BepInEx/plugins/Hoardkeeper"
  cp bin/Hoardkeeper.dll "$GAME/BepInEx/plugins/Hoardkeeper/"
  echo "Instalado em $GAME/BepInEx/plugins/Hoardkeeper/"
fi
