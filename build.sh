#!/bin/bash
# build.sh <Cuphead.app> [out]: your own macOS copy of Cuphead -> the files for the handheld, with
# portmaster-unity's converter and runtime. Nothing is downloaded and nothing leaves your computer.
#
# Needs (see README.md):
#   PMUNITY                 a portmaster-unity checkout (default: ../portmaster-unity)
#   PMUNITY_ANDROID_PLAYER  Unity 2017.4.9f1's Android player, unpacked from
#                           UnitySetup-Android-Support-for-Editor-2017.4.9f1.exe (the folder with
#                           Variations/ and Data/)
#   PYTHON                  a Python with portmaster-unity/convert/requirements.txt (default python3)
#   DOTNET                  the .NET SDK's dotnet (default: dotnet on PATH, else ~/.dotnet/dotnet)
#   Docker                  to build the runtime, if portmaster-unity/runtime/host/out/ua_host is missing
#   SHADER_PRECISION=highp  sample textures at full precision, as desktop GPUs do (default: GLSL ES's
#                           default sampler precision, as Unity's own GLES builds; see README.md)
#   TEXTURE_QUALITY         medium (default, what the port is tested with) or fast, as the on-device
#                           setup's question (pm/cuphead/setup/patchscript)
# Result: <out>/device/ (copy it to the handheld as ports/cuphead/, see README.md).
# Re-running continues where it stopped: the conversion keeps a journal in <out>/Cuphead_Data.
set -e
APP=${1:?usage: build.sh <Cuphead.app> [out]}
APP=$(cd "$APP" && pwd)
HERE=$(cd "$(dirname "$0")" && pwd)
OUT=${2:-$HERE/out}; mkdir -p "$OUT"; OUT=$(cd "$OUT" && pwd)
PM=$(cd "${PMUNITY:-$HERE/../portmaster-unity}" && pwd)
PLAYER=${PMUNITY_ANDROID_PLAYER:?set PMUNITY_ANDROID_PLAYER to the unpacked Unity 2017.4.9f1 Android player}
PY=${PYTHON:-python3}
DOTNET=${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}
DATA="$APP/Contents/Resources/Data"
Q=${TEXTURE_QUALITY:-medium}
case "$Q" in fast|medium) ;; *) echo "TEXTURE_QUALITY=$Q: medium or fast"; exit 1;; esac
log() { echo "== $(date +%T) $*"; }

# ---- checks
[ -f "$DATA/globalgamemanagers" ] || { echo "$APP: not a Unity macOS app (no Contents/Resources/Data)"; exit 1; }
grep -aq "2017.4.9f1" "$DATA/globalgamemanagers" || { echo "$APP: not Unity 2017.4.9f1 (Cuphead 1.3.4)"; exit 1; }
[ -f "$PLAYER/Variations/mono/Release/Libs/armeabi-v7a/libunity.so" ] || { echo "$PLAYER: not an unpacked Unity Android player"; exit 1; }
[ -x "$DOTNET" ] || { echo "no dotnet (set DOTNET)"; exit 1; }
"$PY" -c "import UnityPy, lz4, astc_encoder, PIL" || { echo "$PY lacks portmaster-unity/convert/requirements.txt"; exit 1; }
[ -f "$APP/Contents/Resources/goggame-1963513391.info" ] || echo "note: no GOG install marker; this port is made for the GOG macOS build"
[ -f "$APP/Contents/Resources/goggame-2048763853.info" ] && echo "The Delicious Last Course: installed" || echo "The Delicious Last Course: not installed"

# ---- the port's code: the patch set for the game's own assembly and the runtime helper
log "codepatch and helper (.NET)"
export DOTNET_ROOT=$(dirname "$DOTNET")
"$DOTNET" build -c Release -o "$OUT/build/codepatch" "$HERE/codepatch" >/dev/null
"$DOTNET" build -c Release -o "$OUT/build/helper" -p:GameManaged="$DATA/Managed" "$HERE/helper" >/dev/null

# ---- a working copy of the game's data (a clone on APFS / reflink file systems: no extra space)
if [ ! -f "$OUT/Cuphead_Data/globalgamemanagers" ]; then
  log "copying the game data"
  rm -rf "$OUT/Cuphead_Data.tmp"
  cp -cR "$DATA" "$OUT/Cuphead_Data.tmp" 2>/dev/null || cp -R --reflink=auto "$DATA" "$OUT/Cuphead_Data.tmp"
  mv "$OUT/Cuphead_Data.tmp" "$OUT/Cuphead_Data"
fi
cp "$APP/Contents/Resources/"goggame-*.info "$OUT/" 2>/dev/null || true

# ---- conversion: GLES3 shaders, ASTC textures, settings, audio, the helper's animation table
log "converting (shaders, settings, textures, audio, animclips; texture quality $Q)"
(cd "$PM/convert" && "$PY" -m pmunity convert "$OUT/Cuphead_Data" --config "$HERE/convert/cuphead.json" \
   --plugin "$HERE/convert/animclips.py" --set textures.quality="$Q" \
   ${SHADER_PRECISION:+--set shaders.precision=$SHADER_PRECISION})

# ---- what Unity's Android player reads: APK + OBB
log "Android data (APK + OBB)"
(cd "$PM/convert" && "$PY" -m pmunity android-data --src "$OUT/Cuphead_Data" --pkg "$PLAYER" --out "$OUT/android" \
   --name Cuphead --package com.cupheadport.game --root-file animclips.txt \
   --managed-file "$OUT/build/helper/CupheadPort.dll" --game-managed "$DATA/Managed" \
   --assembly-patch Assembly-CSharp.dll "'$DOTNET' '$OUT/build/codepatch/codepatch.dll' --target=android {managed} {out}" \
   --mono-etc "$APP/Contents/Mono/etc" --dllmap SDL2=libSDL2-2.0.so.0 --pack all --no-video-shader)
                                               # (as the setup packs it: Cuphead plays no video)

# ---- the runtime
[ -x "$PM/runtime/host/out/ua_host" ] || { log "building the runtime (Docker)"; "$PM/runtime/build.sh"; }

# ---- the folder for the handheld
log "device folder"
D="$OUT/device"
mkdir -p "$D/lib"
cp "$PM/runtime/host/out/ua_host" "$D/"
for f in Libs/armeabi-v7a/libmain.so Libs/armeabi-v7a/libunity.so MonoLibs/armeabi-v7a/libmono.so; do
  cp "$PLAYER/Variations/mono/Release/$f" "$D/lib/"
done
cp -cR "$OUT/android/zip/." "$D/" 2>/dev/null || cp -R --reflink=auto "$OUT/android/zip/." "$D/"
cp "$HERE/pm/cuphead/cuphead_hotkey.gptk" "$HERE/pm/cuphead/README.md" "$HERE/pm/cuphead/port.json" \
   "$HERE/pm/cuphead/setup.txt" "$D/"
# licences of what the folder carries (as package.sh; the setup's tools are not in it)
L="$D/licenses"; mkdir -p "$L"
cp "$HERE/LICENSE" "$L/LICENSE.portmaster-cuphead.txt"
cp "$PM/LICENSE" "$L/LICENSE.portmaster-unity.txt"
cp "$PM/THIRD_PARTY.md" "$L/THIRD_PARTY.portmaster-unity.md"
cp "$PM/runtime/thirdparty/libhybris/LICENSE.Apache2" "$L/LICENSE.libhybris-Apache-2.0.txt"   # in ua_host
for f in "$PM/runtime/media/out/licenses"/*.copyright; do                                   # in ua_host (video decoders)
  if [ -f "$f" ]; then cp "$f" "$L/LICENSE.$(basename "$f" .copyright).txt"; fi
done
touch "$D/.cuphead_ready"                      # (Cuphead.sh then skips the on-device setup)
log "done: $D ($(du -sh "$D" | cut -f1)); copy it as ports/cuphead/ and pm/Cuphead.sh into ports/ (README.md)"
