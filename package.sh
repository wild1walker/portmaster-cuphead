#!/bin/bash
# package.sh [out dir]: the PortMaster port (cuphead.zip) whose first launch builds the game on the
# handheld from the player's own copy (pm/cuphead/setup/patchscript; on a 64-bit-only system
# pm/cuphead/setup64/patchscript, the 64-bit build). No game files and nothing from Unity go into it.
#
# Needs:
#   PMUNITY        a portmaster-unity checkout (default ../portmaster-unity), its runtime built (32-bit
#                  and 64-bit: runtime/host/out/ua_host, runtime/host/out/arm64/ua_host) or Docker
#   GAME_MANAGED   Cuphead's Managed folder (e.g. Cuphead.app/Contents/Resources/Data/Managed): the
#                  helper compiles against the game's assemblies (none of them is copied)
#   PYBUNDLE       the setup's Python (pm/build_pybundle.sh <file> builds it; default: built into the
#                  out dir from pm/cache)
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
  -p:PublishTrimmed=true -p:InvariantGlobalization=true -p:DebugType=none -o "$B/codepatch-arm64" >/dev/null
[ -x "$PM/runtime/host/out/ua_host" ] || { log "runtime (Docker)"; "$PM/runtime/build.sh"; }
[ -x "$PM/runtime/host/out/arm64/ua_host" ] || { log "64-bit runtime (Docker)"; ARCH=arm64 "$PM/runtime/build.sh"; }
PYB=${PYBUNDLE:-$B/python.tar.gz}
[ -f "$PYB" ] || { log "setup Python"; "$HERE/pm/build_pybundle.sh" "$PYB"; }

log "assembling"
Z="$OUT/zip"; rm -rf "$Z"; mkdir -p "$Z"
cp "$HERE/pm/Cuphead.sh" "$Z/"
P="$Z/cuphead"; mkdir -p "$P/setup" "$P/licenses" "$P/gamedata"
cp "$HERE/pm/cuphead/README.md" "$HERE/pm/cuphead/cuphead_hotkey.gptk" "$HERE/pm/cuphead/port.json" "$HERE/pm/cuphead/setup.txt" "$P/"
cp "$HERE/pm/cuphead/gamedata/PUT_GAME_FILES_HERE.txt" "$P/gamedata/"
cp "$PM/runtime/host/out/ua_host" "$P/"
S="$P/setup"
cp "$HERE/pm/cuphead/setup/patchscript" "$HERE/pm/cuphead/setup/questions.lua" "$PYB" "$B/helper/CupheadPort.dll" "$B/codepatch-arm64/codepatch" \
   "$HERE/convert/cuphead.json" "$HERE/convert/animclips.py" "$S/"
[ "$(basename "$PYB")" = python.tar.gz ] || mv "$S/$(basename "$PYB")" "$S/python.tar.gz"
(cd "$PM/convert" && find pmunity \( -name '*.py' -o -name '*.shader' \) -not -path '*/__pycache__/*') | while read -r f; do
  mkdir -p "$S/$(dirname "$f")"; cp "$PM/convert/$f" "$S/$f"
done
chmod +x "$Z/Cuphead.sh" "$P/ua_host" "$S/patchscript" "$S/codepatch"
# the 64-bit build (launch64.sh): its runtime, and its setup, which runs the tools in setup/ too
cp "$HERE/pm/cuphead/launch64.sh" "$P/"
cp "$PM/runtime/host/out/arm64/ua_host" "$P/ua_host64"
S64="$P/setup64"; mkdir -p "$S64"
cp "$HERE/pm/cuphead/setup64/patchscript" "$HERE/pm/cuphead/setup/questions.lua" "$S64/"
cp -R "$PM/tools/il2cpp" "$S64/il2cpp"                    # (the converter's IL2CPP step's scripts)
for f in il2cpp/link.xml convert/plugins-il2cpp.json; do    # Cuphead's IL2CPP files, once in the repository
  if [ -f "$HERE/$f" ]; then cp "$HERE/$f" "$S64/"; fi
done
chmod +x "$P/ua_host64" "$S64/patchscript"

# licences: ours, and every third-party part the zip carries
L="$P/licenses"
cp "$HERE/LICENSE" "$L/LICENSE.portmaster-cuphead.txt"
cp "$PM/LICENSE" "$L/LICENSE.portmaster-unity.txt"
cp "$PM/THIRD_PARTY.md" "$L/THIRD_PARTY.portmaster-unity.md"
cp "$PM/runtime/thirdparty/libhybris/LICENSE.Apache2" "$L/LICENSE.libhybris-Apache-2.0.txt"   # in ua_host
ML="$PM/runtime/media/out/licenses"                                                         # in ua_host (video decoders)
ls "$ML"/*.copyright >/dev/null 2>&1 || { echo "no decoder licences in $ML: build portmaster-unity's runtime first" >&2; exit 1; }
for f in "$ML"/*.copyright; do cp "$f" "$L/LICENSE.$(basename "$f" .copyright).txt"; done
ML64="$PM/runtime/media/out/arm64/licenses"                                                 # in ua_host64
ls "$ML64"/* >/dev/null 2>&1 || { echo "no decoder licences in $ML64: build portmaster-unity's 64-bit runtime first" >&2; exit 1; }
for f in "$ML64"/*; do n=$(basename "$f"); cp "$f" "$L/LICENSE.${n%.*}.arm64.txt"; done
NUGET=${NUGET_PACKAGES:-$HOME/.nuget/packages}
RT=$(ls -d "$NUGET"/microsoft.netcore.app.runtime.linux-arm64/8.* | tail -1)
cp "$RT/LICENSE.TXT" "$L/LICENSE.dotnet-runtime.txt"                                        # in codepatch
cp "$RT/THIRD-PARTY-NOTICES.TXT" "$L/THIRD-PARTY-NOTICES.dotnet-runtime.txt"
cat > "$L/LICENSE.mono.cecil.txt" <<'TXT'
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
T=$(mktemp -d); trap 'rm -rf "$T"' EXIT
tar -xzf "$PYB" -C "$T"
{
  echo "The setup's Python (removed from the handheld when the setup is done): python-build-standalone's"
  echo "CPython 3.12 and these packages. Their licences follow."
  for f in "$T"/python/lib/python3.12/LICENSE.txt "$T"/python/lib/python3.12/site-packages/*.dist-info/LICENSE* \
           "$T"/python/lib/python3.12/site-packages/*.dist-info/licenses/*; do
    [ -f "$f" ] || continue
    rel=${f#"$T"/python/lib/python3.12/}
    printf '\n\n==== %s ====\n\n' "$rel"; cat "$f"
  done
} > "$L/LICENSE.setup-python.txt"

rm -f "$OUT/cuphead.zip"
(cd "$Z" && zip -qr -X "$OUT/cuphead.zip" Cuphead.sh cuphead)
log "built $OUT/cuphead.zip ($(du -h "$OUT/cuphead.zip" | cut -f1)): unzip it into the handheld's ports folder"
