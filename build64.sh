#!/bin/bash
# build64.sh <Cuphead.app> [out]: DRAFT of the 64-bit build, for handhelds without a 32-bit
# userland. The same conversion as build.sh, but Cuphead's code is compiled ahead of time with
# Unity's IL2CPP and runs on Unity's 64-bit (arm64-v8a) IL2CPP Android player instead of the
# ARMv7 Mono one (Unity's Android Mono players are 32-bit only). docs/il2cpp.md has the why.
#
#   codepatch (Assembly-CSharp, --target=android, from the original)   as build.sh
#   -> helper (CupheadPort.dll)                                        as build.sh
#   -> [asmpatch]                                                      none by default (ASMPATCH=)
#   -> pmunity il2cpp       the patched Assembly-CSharp, the helper and the rest of the game's
#                           assemblies -> libil2cpp.so + global-metadata.dat
#   -> pmunity android-data --il2cpp   APK + OBB with IL2CPP's metadata instead of the assemblies
#
# Unity 2017.4.9f1 (Cuphead 1.3.4) has no 64-bit player: Unity's arm64 IL2CPP players start at
# 2017.4.16f1, so the IL2CPP and the player are 2017.4.40f1's (the line's last release), and
# android-data rewrites the data's serialized files for that release (portmaster-unity's
# retarget.py: the version string they carry, PlayerSettings' layout).
#
# Needs what build.sh needs (PMUNITY, PYTHON, DOTNET, Docker), but no PMUNITY_ANDROID_PLAYER:
#   PMUNITY_IL2CPP_HOME   pmunity il2cpp's tool folder (default ~/.cache/pmunity/il2cpp): Unity's
#                         il2cpp and class libraries, the NDK's target files, the IL2CPP player and
#                         Ubuntu's compilers, fetched by pmunity il2cpp from Unity, Google and Ubuntu
#                         on first use (range reads; ~1.2 GB on disk), never shipped
#   IL2CPP_UNITY          the IL2CPP release (default 2017.4.40f1)
#   PMUNITY_IL2CPP_PLAYER the IL2CPP player package (default <home>/<release>/player-arm64-v8a)
#   IL2CPP_JOBS           compiler jobs (default: pmunity il2cpp's)
#   ASMPATCH              a tools/asmpatch command for extra generic patches (default none: the
#                         codepatch already puts Rewired on its Android platform)
#   TEXTURE_QUALITY, SHADER_PRECISION   as build.sh
#   PACK_ARGS             extra pmunity android-data options (e.g. --consume-src when the disk is
#                         tight: the converted data is moved into the APK tree instead of copied)
# Result: <out>/device64/ (ports/cuphead/ for a 64-bit handheld: the arm64 ua_host; lib/ with the
# IL2CPP player's libmain.so and libunity.so and the game's libil2cpp.so; APK + OBB). Like the
# converted data, libil2cpp.so is the owner's own build of their game: not something to share.
# Runs a conversion and a C++ build of about an hour of CPU; 2 GB free for <out>/il2cpp/.work.
set -e
APP=${1:?usage: build64.sh <Cuphead.app> [out]}
APP=$(cd "$APP" && pwd)
HERE=$(cd "$(dirname "$0")" && pwd)
OUT=${2:-$HERE/out64}; mkdir -p "$OUT"; OUT=$(cd "$OUT" && pwd)
PM=$(cd "${PMUNITY:-$HERE/../portmaster-unity}" && pwd)
PY=${PYTHON:-python3}
DOTNET=${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}
DATA="$APP/Contents/Resources/Data"
Q=${TEXTURE_QUALITY:-medium}
ABI=arm64-v8a
UNITY=${IL2CPP_UNITY:-2017.4.40f1}
IHOME=${PMUNITY_IL2CPP_HOME:-$HOME/.cache/pmunity/il2cpp}
IPKG=${PMUNITY_IL2CPP_PLAYER:-$IHOME/$UNITY/player-$ABI}
case "$Q" in fast|medium) ;; *) echo "TEXTURE_QUALITY=$Q: medium or fast"; exit 1;; esac
log() { echo "== $(date +%T) $*"; }
# copy_tree <dir> <into>: <dir>'s contents into <into>, as a clone where the file system makes them
# (APFS: cp -c; Btrfs/XFS: --reflink), else a plain copy (another drive, exFAT: cp -c then leaves
# what it made, which the next try overwrites in place). macOS's -X: no extended attributes, which
# exFAT would get as an AppleDouble ._ file beside every file (Linux's cp has no -X)
copy_tree() { mkdir -p "$2"; cp -cRX "$1/." "$2/" 2>/dev/null || cp -R --reflink=auto "$1/." "$2/" 2>/dev/null ||
  cp -RX "$1/." "$2/" 2>/dev/null || cp -R "$1/." "$2/"; }
pmu() { (cd "$PM/convert" && "$PY" -m pmunity "$@"); }

# ---- checks
[ -f "$DATA/globalgamemanagers" ] || { echo "$APP: not a Unity macOS app (no Contents/Resources/Data)"; exit 1; }
grep -aq "2017.4.9f1" "$DATA/globalgamemanagers" || { echo "$APP: not Unity 2017.4.9f1 (Cuphead 1.3.4)"; exit 1; }
[ -x "$DOTNET" ] || { echo "no dotnet (set DOTNET)"; exit 1; }
"$PY" -c "import UnityPy, lz4, astc_encoder, PIL" || { echo "$PY lacks portmaster-unity/convert/requirements.txt"; exit 1; }
docker info >/dev/null 2>&1 || { echo "Docker is not running (pmunity il2cpp builds in a container)"; exit 1; }
pmu il2cpp --help | grep -q -- --managed-file ||
  { echo "$PM: pmunity il2cpp without --managed-file (needs portmaster-unity's IL2CPP support for Unity 2017.4)"; exit 1; }
pmu android-data --help | grep -q -- --data-version ||
  { echo "$PM: android-data without --data-version (needs portmaster-unity's retargeting of data to another release)"; exit 1; }
LINKXML=()
if pmu il2cpp --help | grep -q -- --link-xml; then LINKXML=(--link-xml "$HERE/il2cpp/link.xml")
else echo "note: pmunity il2cpp takes no --link-xml: il2cpp/link.xml not used (its strip level keeps the game's assemblies whole)"; fi
[ -f "$APP/Contents/Resources/goggame-1963513391.info" ] || echo "note: no GOG install marker; this port is made for the GOG macOS build"
[ -f "$APP/Contents/Resources/goggame-2048763853.info" ] && echo "The Delicious Last Course: installed" || echo "The Delicious Last Course: not installed"

# ---- the port's code: the patched Assembly-CSharp and the helper, the assemblies IL2CPP compiles
log "codepatch and helper (.NET)"
export DOTNET_ROOT=$(dirname "$DOTNET")
"$DOTNET" build -c Release -o "$OUT/build/codepatch" "$HERE/codepatch" >/dev/null
"$DOTNET" build -c Release -o "$OUT/build/helper" -p:GameManaged="$DATA/Managed" "$HERE/helper" >/dev/null
M="$OUT/build/managed"
rm -rf "$M"; mkdir -p "$M"
"$DOTNET" "$OUT/build/codepatch/codepatch.dll" --target=android "$DATA/Managed" "$M"   # (prints the patches)
cp "$OUT/build/helper/CupheadPort.dll" "$M/"

# ---- a working copy of the game's data and its conversion: exactly as build.sh
if [ ! -f "$OUT/Cuphead_Data/globalgamemanagers" ]; then
  log "copying the game data"
  rm -rf "$OUT/Cuphead_Data.tmp"
  copy_tree "$DATA" "$OUT/Cuphead_Data.tmp"
  mv "$OUT/Cuphead_Data.tmp" "$OUT/Cuphead_Data"
fi
cp "$APP/Contents/Resources/"goggame-*.info "$OUT/" 2>/dev/null || true
log "converting (shaders, settings, textures, audio, animclips; texture quality $Q)"
pmu convert "$OUT/Cuphead_Data" --config "$HERE/convert/cuphead.json" \
   --plugin "$HERE/convert/animclips.py" --set textures.quality="$Q" \
   ${SHADER_PRECISION:+--set shaders.precision=$SHADER_PRECISION}

# ---- IL2CPP: the game's code for the 64-bit player (reads the original data folder: its Managed
# assemblies, boot.config, the Unity version; the patched and added assemblies replace or join them)
log "IL2CPP $UNITY ($ABI): Cuphead's code -> libil2cpp.so"
pmu il2cpp "$DATA" "$OUT/il2cpp" --abi "$ABI" --unity "$UNITY" --home "$IHOME" ${PMUNITY_IL2CPP_PLAYER:+--pkg "$IPKG"} \
   --managed-file "$M/Assembly-CSharp.dll" --managed-file "$M/CupheadPort.dll" "${LINKXML[@]}" \
   ${ASMPATCH:+--asmpatch "$ASMPATCH"} ${IL2CPP_JOBS:+--jobs "$IL2CPP_JOBS"}
SO="$OUT/il2cpp/lib/$ABI/libil2cpp.so"
[ -f "$SO" ] || { echo "pmunity il2cpp wrote no $SO"; exit 1; }
[ -f "$IPKG/Variations/il2cpp/Release/Libs/$ABI/libunity.so" ] || { echo "$IPKG: not the IL2CPP $ABI player package"; exit 1; }

# ---- what the IL2CPP player reads: APK + OBB, IL2CPP's Managed/ (metadata, resources, etc/)
# instead of the assemblies and Mono's config. No dllmap under IL2CPP: the helper's DllImport("SDL2")
# (rumble) goes through ua_host's plugin map (convert/plugins-il2cpp.json)
log "Android data (APK + OBB, IL2CPP)"
pmu android-data --src "$OUT/Cuphead_Data" --pkg "$IPKG" --il2cpp "$OUT/il2cpp" --out "$OUT/android" \
   --name Cuphead --package com.cupheadport.game --root-file animclips.txt \
   --plugin-map "$HERE/convert/plugins-il2cpp.json" --pack all --no-video-shader $PACK_ARGS

# ---- the runtime, 64-bit
HB="$PM/runtime/host/out/arm64"
[ -x "$HB/ua_host" ] || { log "building the 64-bit runtime (Docker)"; ARCH=arm64 "$PM/runtime/build.sh"; }

# ---- the folder for the handheld (as build.sh: device/ there, device64/ here)
log "device folder"
D="$OUT/device64"
mkdir -p "$D/lib"
cp "$HB/ua_host" "$D/"
for f in libmain.so libunity.so; do cp "$IPKG/Variations/il2cpp/Release/Libs/$ABI/$f" "$D/lib/"; done
cp "$SO" "$D/lib/"
copy_tree "$OUT/android/zip" "$D"
cp "$HERE/pm/cuphead/cuphead_hotkey.gptk" "$HERE/pm/cuphead/README.md" "$HERE/pm/cuphead/port.json" \
   "$HERE/pm/cuphead/setup.txt" "$D/"
L="$D/licenses"; mkdir -p "$L"
cp "$HERE/LICENSE" "$L/LICENSE.portmaster-cuphead.txt"
cp "$PM/LICENSE" "$L/LICENSE.portmaster-unity.txt"
cp "$PM/THIRD_PARTY.md" "$L/THIRD_PARTY.portmaster-unity.md"
cp "$PM/runtime/thirdparty/libhybris/LICENSE.Apache2" "$L/LICENSE.libhybris-Apache-2.0.txt"   # in ua_host
for f in "$PM/runtime/media/out/arm64/licenses"/*; do                                     # in ua_host (video decoders)
  if [ -f "$f" ]; then n=$(basename "$f"); cp "$f" "$L/LICENSE.${n%.*}.txt"; fi
done
touch "$D/.cuphead_ready"                      # (Cuphead.sh then skips the on-device setup)
log "done: $D ($(du -sh "$D" | cut -f1)): ports/cuphead/ for a 64-bit (aarch64) handheld"
