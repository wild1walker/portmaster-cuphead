#!/bin/bash
# package.sh [out dir]: the PortMaster port (cuphead.zip) from pm/cuphead/ (laid out as a
# PortMaster-New pull request), whose first launch builds the game on the handheld from the
# player's own copy. No game files go into it, and from Unity only the port's donors: small games of
# its own built with Unity (a free licence), whose APKs carry the player, so the setup needs no network.
#
# Needs:
#   PMUNITY        a portmaster-unity checkout (default ../portmaster-unity), its runtime built (32-bit
#                  and 64-bit: runtime/host/out/ua_host, runtime/host/out/arm64/ua_host) or Docker
#   GAME_MANAGED   Cuphead's Managed folder (e.g. Cuphead.app/Contents/Resources/Data/Managed): the
#                  helper compiles against the game's assemblies (none of them is copied)
#   PYLIBS         the Python packages the setup adds to PortMaster's python_3.11 runtime
#                  (pm/build_pylibs.sh <dir> builds them; default: built into the out dir from pm/cache)
#   DONORS         the folder with the donor APKs (donor-2017.4.9f1-mono-armv7.apk, donor-2017.4.40f1-il2cpp-arm64.apk,
#                  donor-2017.4.40f1-mono-armv7.apk: donor/ in portmaster-unity's tools, built by its build.sh)
#   ASTC_GUIDES    a folder with the ASTC guides for Cuphead's textures, astc_guide_fast.bin and
#                  astc_guide_medium.bin (each hard texture block's encoder effort for that quality, chosen
#                  on a PC; no pixels in them), made from the game's data (with the DLC) by
#                  `python -m pmunity astc-guide <Cuphead.app/Contents/Resources/Data> <file>
#                  --config convert/cuphead.json --target fast|medium` (portmaster-unity's convert/, guided_astc
#                  built: convert/native/guided_astc/build.py). Without them the setup searches every hard block
#   DOTNET         the .NET 8 SDK's dotnet (default: on PATH, else ~/.dotnet/dotnet)
#   PMUNITY_MONO64_WORK  the work folder of portmaster-unity's tools/mono64 builds (Unity's Mono 2017.4 for
#                  aarch64 and its 4.x class libraries, for the 64-bit build's Mono route; default
#                  <portmaster-unity>/tools/mono64/work; built there in Docker when missing)
set -euo pipefail
HERE=$(cd "$(dirname "$0")" && pwd)
OUT=${1:-$HERE/out/pm}; mkdir -p "$OUT"; OUT=$(cd "$OUT" && pwd)
PM=$(cd "${PMUNITY:-$HERE/../portmaster-unity}" && pwd)
GM=${GAME_MANAGED:?set GAME_MANAGED to Cuphead.app/Contents/Resources/Data/Managed}
DN=$(cd "${DONORS:?set DONORS to the folder with the donor APKs}" && pwd)
DONOR_APKS="donor-2017.4.9f1-mono-armv7.apk donor-2017.4.40f1-il2cpp-arm64.apk donor-2017.4.40f1-mono-armv7.apk"
DOTNET=${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}
export DOTNET_ROOT=$(dirname "$DOTNET") DOTNET_CLI_TELEMETRY_OPTOUT=1
log() { echo "== $*"; }

B="$OUT/build"; mkdir -p "$B"
log "helper (CupheadPort.dll)"
"$DOTNET" build -c Release -o "$B/helper" -p:GameManaged="$GM" "$HERE/helper" >/dev/null
log "code patcher for the handheld (linux-arm64, self-contained)"
"$DOTNET" publish "$HERE/codepatch" -c Release -r linux-arm64 --self-contained -p:PublishSingleFile=true \
  -p:PublishTrimmed=true -p:InvariantGlobalization=true -p:DebugType=none -p:TrimmerRemoveSymbols=true \
  -o "$B/codepatch-arm64" >/dev/null       # (TrimmerRemoveSymbols: else ILLink stamps the trimmed DLLs with obj/'s path)
[ -x "$PM/runtime/host/out/ua_host" ] || { log "runtime (Docker)"; "$PM/runtime/build.sh"; }
[ -x "$PM/runtime/host/out/arm64/ua_host" ] || { log "64-bit runtime (Docker)"; ARCH=arm64 "$PM/runtime/build.sh"; }
# the 64-bit build's Mono route: Unity's Mono of the 2017.4 line (MonoBleedingEdge, Mono 5.0.1) for
# aarch64, the bridge the IL2CPP player loads as libil2cpp.so, and Mono's 4.x class libraries, all
# from Unity's MIT-licensed Mono sources at one commit (portmaster-unity docs/mono-arm64.md)
M64W=${PMUNITY_MONO64_WORK:-$PM/tools/mono64/work}; MO="$M64W/out/unity-2017.4-mbe"
export MONO_REF=538d2255088694b8d0fe78b2280a71b1b7bd94c3     # unity-2017.4-mbe's last commit
[ -f "$PM/runtime/mono64/out/arm64/libil2cpp_mono.so" ] || { log "the Mono bridge (Docker)"; "$PM/runtime/mono64/build.sh"; }
[ -f "$MO/libmonobdwgc-2.0.so" ] || { log "Unity's Mono 2017.4 for aarch64 (Docker)"; "$PM/tools/mono64/build-mono.sh" 2017.4 "$M64W"; }
[ -f "$MO/classlibs/mscorlib.dll" ] || { log "Mono's 4.x class libraries (Docker)"; "$PM/tools/mono64/build-classlibs.sh" 2017.4 "$M64W"; }
# the class libraries Cuphead's assemblies reference, and theirs
CLASSLIBS="mscorlib.dll System.dll System.Core.dll System.Xml.dll Mono.Security.dll System.Configuration.dll System.Security.dll Mono.Posix.dll"
# Cuphead does not use Steamworks: ua_host without the runtime's Steamworks stand-ins (STEAMSTUB=0)
for a in armhf arm64; do
  h=$PM/runtime/host/out/ua_host; [ $a = arm64 ] && h=$PM/runtime/host/out/arm64/ua_host
  if LC_ALL=C grep -aqi steam "$h"; then log "ua_host ($a) without Steamworks stand-ins (Docker)"; STEAMSTUB=0 ARCH=$a "$PM/runtime/host/build.sh" >/dev/null; fi
  if LC_ALL=C grep -aqi steam "$h"; then log "$h still has Steamworks stand-ins"; exit 1; fi
done
PYL=${PYLIBS:-$B/pylibs}
[ -d "$PYL/UnityPy" ] && [ -f "$PYL"/guided_astc.abi3.so ] || { log "setup's Python packages"; GUIDED_ASTC="$PM/convert/native/guided_astc" "$HERE/pm/build_pylibs.sh" "$PYL"; }

log "assembling"
Z="$OUT/zip"; rm -rf "$Z"; mkdir -p "$Z"
R="$HERE/pm/cuphead"                      # the port as a PortMaster-New pull request lays it out
cp "$R/Cuphead.sh" "$Z/"
P="$Z/cuphead"; cp -R "$R/cuphead" "$P"
# what PortMaster's release build adds to the port folder
cp "$R/port.json" "$R/gameinfo.xml" "$R/screenshot.jpg" "$R/cover.jpg" "$P/"
cp "$R/README.md" "$P/cuphead.md"
echo "${RELEASE:-dev $(git -C "$HERE" rev-parse --short HEAD)}" > "$P/version.txt"   # (the logs name it)
cp "$PM/runtime/host/out/ua_host" "$P/"
cp "$PM/runtime/host/out/arm64/ua_host" "$P/ua_host64"
S="$P/setup"
cp "$B/helper/CupheadPort.dll" "$B/codepatch-arm64/codepatch" "$HERE/convert/cuphead.json" "$HERE/convert/animclips.py" "$S/"
for q in fast medium; do
  if [ -f "${ASTC_GUIDES:-/nonexistent}/astc_guide_$q.bin" ]; then cp "$ASTC_GUIDES/astc_guide_$q.bin" "$S/"
  else log "WARNING: no astc_guide_$q.bin in ASTC_GUIDES: the setup encodes $q textures without a guide (slower)"; fi
done
# (etcpak: UnityPy's BC encoder, which the setup never uses: it encodes ASTC itself)
rsync -a --exclude 'etcpak*' "$PYL/" "$S/pylibs/"
# the converter's modules the setup can import (pm/setup_modules.py), and its shader files
{ python3 "$HERE/pm/setup_modules.py" "$PM/convert" "$HERE/convert/animclips.py"
  (cd "$PM/convert" && find pmunity -name '*.shader'); } | while read -r f; do
  mkdir -p "$S/$(dirname "$f")"; cp "$PM/convert/$f" "$S/$f"
done
S64="$P/setup64"
mkdir -p "$S64"; cp "$HERE/convert/plugins-il2cpp.json" "$S64/"
M="$S64/mono64"; mkdir -p "$M/classlibs"
cp "$PM/runtime/mono64/out/arm64/libil2cpp_mono.so" "$M/libil2cpp.so"
cp "$MO/libmonobdwgc-2.0.so" "$MO/libMonoPosixHelper.so" "$M/"
for n in $CLASSLIBS; do cp "$MO/classlibs/$n" "$M/classlibs/"; done
cp -R "$MO/classlibs/etc" "$M/etc"                         # Mono's Linux config, 4.5 machine.config
mkdir -p "$P/donor"; for n in $DONOR_APKS; do cp "$DN/$n" "$P/donor/"; done
find "$Z" -name '*.sh' -exec chmod 644 {} +            # (the launcher sets the exec bits it needs)

# licences: one LICENSE.<component>.txt per component the zip carries
L="$P/licenses"; mkdir -p "$L"
cp "$HERE/LICENSE" "$L/LICENSE.portmaster-cuphead.txt"
cat > "$L/LICENSE.unity-donor.txt" <<'TXT'
donor/*.apk: small games built with Unity (Unity Personal) for this port: a game of pong (one
scene, one script; portmaster-unity's tools/donor). Each carries Unity's Android player of its Unity version (the
Unity runtime), which the setup uses to run the player's own copy of Cuphead. The Unity runtime
is Unity Technologies' software, distributed as part of these Made-with-Unity applications under
Unity's terms of service: https://unity.com/legal/terms-of-service
TXT
{ cat "$PM/LICENSE"; printf '\n\n'; cat "$PM/THIRD_PARTY.md"; } > "$L/LICENSE.portmaster-unity.txt"
cp "$PM/runtime/thirdparty/libhybris/LICENSE.Apache2" "$L/LICENSE.libhybris.txt"            # in ua_host
for d in "$PM/runtime/media/out/licenses" "$PM/runtime/media/out/arm64/licenses"; do         # in ua_host, ua_host64 (video decoders)
  ls "$d"/* >/dev/null 2>&1 || { echo "no decoder licences in $d: build portmaster-unity's runtime first" >&2; exit 1; }
done
for f in "$PM"/runtime/media/out/licenses/*; do
  n=$(basename "$f"); n=${n%.*}; g=$(ls "$PM"/runtime/media/out/arm64/licenses/"$n".* 2>/dev/null | head -1)
  { echo "$n (armhf, in ua_host)"; echo; cat "$f"
    if [ -n "$g" ] && ! cmp -s "$f" "$g"; then printf '\n\n%s (arm64, in ua_host64)\n\n' "$n"; cat "$g"; fi; } > "$L/LICENSE.$n.txt"
done
NUGET=${NUGET_PACKAGES:-$HOME/.nuget/packages}
RT=$(ls -d "$NUGET"/microsoft.netcore.app.runtime.linux-arm64/8.* | tail -1)
{ cat "$RT/LICENSE.TXT"; printf '\n\n'; cat "$RT/THIRD-PARTY-NOTICES.TXT"; } > "$L/LICENSE.dotnet-runtime.txt"   # in codepatch
cat > "$L/LICENSE.mono-cecil.txt" <<'TXT'
Mono.Cecil 0.11.6 (in codepatch)

Copyright (c) 2008 - 2015 Jb Evain
Copyright (c) 2008 - 2011 Novell, Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES
OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
TXT
for d in "$S"/pylibs/*.dist-info; do                       # the setup's Python packages
  n=$(sed -n 's/^Name: //p' "$d/METADATA" | head -1 | tr 'A-Z_' 'a-z-')
  [ "$n" = guided-astc ] && continue                       # (portmaster-unity's: LICENSE.portmaster-unity.txt)
  for f in "$d"/LICEN[CS]E* "$d"/licenses/* "$d"/COPYING*; do [ -f "$f" ] && cat "$f" && printf '\n'; done > "$L/LICENSE.$n.txt"
  [ -s "$L/LICENSE.$n.txt" ] || { echo "$d: no licence file" >&2; exit 1; }
done
MS="$M64W/src/unity-2017.4-mbe"                            # the 64-bit build's Mono route
{ echo "Unity's fork of Mono, branch unity-2017.4-mbe ($(cat "$MO/SOURCE.txt")): setup64/mono64/libmonobdwgc-2.0.so, libMonoPosixHelper.so"
  echo; cat "$MS/LICENSE"; printf '\n\n'; cat "$MS/PATENTS.TXT"; } > "$L/LICENSE.mono.txt"
{ echo "Unity's fork of bdwgc (in libmonobdwgc-2.0.so)"; echo; cat "$MS/external/bdwgc/README.QUICK"; } > "$L/LICENSE.bdwgc.txt"
{ echo "Mono's class libraries, unityjit profile ($(cat "$MO/classlibs/SOURCE.txt")): setup64/mono64/classlibs, etc"
  echo; cat "$MO/classlibs/LICENSE.txt"; } > "$L/LICENSE.mono-class-libraries.txt"

# nothing in the zip may name this machine's folders (a build path in a binary: e.g. ILLink's PDB
# paths in the trimmed DLLs of an obj/ built before TrimmerRemoveSymbols: delete codepatch/obj)
case $HOME in /Users/?*|/home/?*) H=$HOME ;; *) H=$HERE ;; esac
for p in "$H" "$HERE" "$PM" "$OUT" /Users/ /Volumes/; do
  if LC_ALL=C grep -r -a -l -F "$p" "$Z"; then log "the files above name $p: not packaged"; exit 1; fi
done
# the setup's tools zipped, one archive a folder (PortMaster's repository then holds a few files
# instead of hundreds; Cuphead.sh unpacks them before the first setup): all but pm/cuphead's scripts
for d in setup setup64; do
  (cd "$P/$d" && t=$(ls -A | grep -vxF "$(ls -A "$R/cuphead/$d")") && zip -qr -X "$d.zip" $t && rm -rf $t)
done
rm -f "$OUT/cuphead.zip"
(cd "$Z" && zip -qr -X "$OUT/cuphead.zip" Cuphead.sh cuphead)
log "built $OUT/cuphead.zip ($(du -h "$OUT/cuphead.zip" | cut -f1)): unzip it into the handheld's ports folder"
