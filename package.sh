#!/bin/bash
# package.sh [out dir]: the PortMaster port (cuphead.zip) from pm/cuphead/ (laid out as a
# PortMaster-New pull request), whose first launch builds the game on the handheld from the
# player's own copy. No game files and nothing from Unity go into it.
#
# Needs:
#   PMUNITY        a portmaster-unity checkout (default ../portmaster-unity), its runtime built (32-bit
#                  and 64-bit: runtime/host/out/ua_host, runtime/host/out/arm64/ua_host) or Docker
#   GAME_MANAGED   Cuphead's Managed folder (e.g. Cuphead.app/Contents/Resources/Data/Managed): the
#                  helper compiles against the game's assemblies (none of them is copied)
#   PYLIBS         the Python packages the setup adds to PortMaster's python_3.11 runtime
#                  (pm/build_pylibs.sh <dir> builds them; default: built into the out dir from pm/cache)
#   DOTNET         the .NET 8 SDK's dotnet (default: on PATH, else ~/.dotnet/dotnet)
set -euo pipefail
HERE=$(cd "$(dirname "$0")" && pwd)
OUT=${1:-$HERE/out/pm}; mkdir -p "$OUT"; OUT=$(cd "$OUT" && pwd)
PM=$(cd "${PMUNITY:-$HERE/../portmaster-unity}" && pwd)
GM=${GAME_MANAGED:?set GAME_MANAGED to Cuphead.app/Contents/Resources/Data/Managed}
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
# Cuphead does not use Steamworks: ua_host without the runtime's Steamworks stand-ins (STEAMSTUB=0)
for a in armhf arm64; do
  h=$PM/runtime/host/out/ua_host; [ $a = arm64 ] && h=$PM/runtime/host/out/arm64/ua_host
  if LC_ALL=C grep -aqi steam "$h"; then log "ua_host ($a) without Steamworks stand-ins (Docker)"; STEAMSTUB=0 ARCH=$a "$PM/runtime/host/build.sh" >/dev/null; fi
  if LC_ALL=C grep -aqi steam "$h"; then log "$h still has Steamworks stand-ins"; exit 1; fi
done
PYL=${PYLIBS:-$B/pylibs}
[ -d "$PYL/UnityPy" ] || { log "setup's Python packages"; "$HERE/pm/build_pylibs.sh" "$PYL"; }
# OpenSSL 1.1 for the 64-bit setup's downloads (PortMaster's Python links it; most firmwares have 3)
DEB="$HERE/pm/cache/libssl1.1_1.1.1w-0+deb11u1_arm64.deb"
[ -f "$DEB" ] || curl -fL -o "$DEB" "https://deb.debian.org/debian/pool/main/o/openssl/$(basename "$DEB")"
[ "$(shasum -a 256 "$DEB" | cut -d' ' -f1)" = fe7a7d313c87e46e62e614a07137e4a476a79fc9e5aab7b23e8235211280fee3 ] \
  || { echo "$DEB: checksum mismatch" >&2; exit 1; }
T=$(mktemp -d); trap 'rm -rf "$T"' EXIT
(cd "$T" && ar x "$DEB" data.tar.xz && tar -xJf data.tar.xz ./usr/lib/aarch64-linux-gnu/libssl.so.1.1 \
   ./usr/lib/aarch64-linux-gnu/libcrypto.so.1.1 ./usr/share/doc/libssl1.1/copyright)

log "assembling"
Z="$OUT/zip"; rm -rf "$Z"; mkdir -p "$Z"
R="$HERE/pm/cuphead"                      # the port as a PortMaster-New pull request lays it out
cp "$R/Cuphead.sh" "$Z/"
P="$Z/cuphead"; cp -R "$R/cuphead" "$P"
# what PortMaster's release build adds to the port folder
cp "$R/port.json" "$R/gameinfo.xml" "$R/screenshot.jpg" "$R/cover.jpg" "$P/"
cp "$R/README.md" "$P/cuphead.md"
cp "$PM/runtime/host/out/ua_host" "$P/"
cp "$PM/runtime/host/out/arm64/ua_host" "$P/ua_host64"
S="$P/setup"
cp "$B/helper/CupheadPort.dll" "$B/codepatch-arm64/codepatch" "$HERE/convert/cuphead.json" "$HERE/convert/animclips.py" "$S/"
cp -R "$PYL" "$S/pylibs"
(cd "$PM/convert" && find pmunity \( -name '*.py' -o -name '*.shader' \) -not -path '*/__pycache__/*') | while read -r f; do
  mkdir -p "$S/$(dirname "$f")"; cp "$PM/convert/$f" "$S/$f"
done
S64="$P/setup64"
cp -R "$PM/tools/il2cpp" "$S64/il2cpp"                    # (the converter's IL2CPP step's scripts)
cp "$HERE/il2cpp/link.xml" "$HERE/convert/plugins-il2cpp.json" "$S64/"
mkdir -p "$S64/lib"; cp "$T"/usr/lib/aarch64-linux-gnu/lib*.so.1.1 "$S64/lib/"
find "$Z" -name '*.sh' -exec chmod 644 {} +            # (the launcher sets the exec bits it needs)

# licences: one LICENSE.<component>.txt per component the zip carries
L="$P/licenses"; mkdir -p "$L"
cp "$HERE/LICENSE" "$L/LICENSE.portmaster-cuphead.txt"
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
for d in "$PYL"/*.dist-info; do                            # the setup's Python packages
  n=$(sed -n 's/^Name: //p' "$d/METADATA" | head -1 | tr 'A-Z_' 'a-z-')
  for f in "$d"/LICEN[CS]E* "$d"/licenses/* "$d"/COPYING*; do [ -f "$f" ] && cat "$f" && printf '\n'; done > "$L/LICENSE.$n.txt"
  [ -s "$L/LICENSE.$n.txt" ] || { echo "$d: no licence file" >&2; exit 1; }
done
cp "$T/usr/share/doc/libssl1.1/copyright" "$L/LICENSE.openssl.txt"

# nothing in the zip may name this machine's folders (a build path in a binary: e.g. ILLink's PDB
# paths in the trimmed DLLs of an obj/ built before TrimmerRemoveSymbols: delete codepatch/obj)
case $HOME in /Users/?*|/home/?*) H=$HOME ;; *) H=$HERE ;; esac
for p in "$H" "$HERE" "$PM" "$OUT" /Users/ /Volumes/; do
  if LC_ALL=C grep -r -a -l -F "$p" "$Z"; then log "the files above name $p: not packaged"; exit 1; fi
done
rm -f "$OUT/cuphead.zip"
(cd "$Z" && zip -qr -X "$OUT/cuphead.zip" Cuphead.sh cuphead)
log "built $OUT/cuphead.zip ($(du -h "$OUT/cuphead.zip" | cut -f1)): unzip it into the handheld's ports folder"
