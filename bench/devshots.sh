#!/bin/bash
# Device-side: deterministic replays with screenshots, one per APK variant, to compare pictures.
#   devshots.sh <tag> <level> <frames> <secs> <apk name>...
# e.g. devshots.sh blur OldMan 120,240,360,480 70 Cuphead Cuphead_B
# Each variant: CUPHEAD_TEST_DETERMINISTIC (game time 1/60 s per frame, seeded Random, no input in
# the level) and CUPHEAD_TEST_SHOTS; Unity's program binary cache is bypassed (UA_SHADER_DUMP) so
# every variant compiles its own shaders. Results: shots/<tag>/<apk>/<Level>-f<frame>.png, the
# shader sources Unity compiled (glsl/) and the log. Compare on the PC with tools/imgdiff.py.
cd "$(dirname "$0")" || exit 1
T=$1; L=$2; F=$3; S=$4; shift 4
for a in "$@"; do
  d=shots/$T/$a; mkdir -p "$d/glsl"
  UA_APK_NAME=$a UA_SHADER_DUMP=$PWD/$d/glsl CUPHEAD_TEST_LEVEL=$L CUPHEAD_TEST_DETERMINISTIC=1 CUPHEAD_TEST_SHOTS=$F \
  CUPHEAD_TEST_GODMODE=1 UA_PAD_NAME='Xbox Wireless Controller' UA_FPS=5 UA_INPUT_SCRIPT="30:a 36:a 50:a 56:a 62:a" \
    ./run.sh "$d/run.log" "$S" release
  for p in data/external/*-f*.png; do [ -e "$p" ] && mv "$p" "$d/"; done
  echo "$(date +%T) $T $a: $(ls "$d" | grep -c png) shots"
done
