#!/bin/bash
# Device-side A/B of shader overrides: ab_shaders.sh <level> <skip-to state|-> <secs> <variant dir>...
# Each variant runs the level once with UA_SHADER_OVERRIDE=shaderexp/<variant> and vsync off;
# logs ab_<level>_<variant>.log. The base variant (an empty dir) runs with the cache off as well.
cd "$(dirname "$0")" || exit 1
L=$1; SK=$2; SECS=$3; shift 3
for v in "$@"; do
  UA_SWAP_INTERVAL=0 UA_SHADER_OVERRIDE=$PWD/shaderexp/$v ./profile_level.sh "$L" "$SK" "$SECS" release
  mv "prof_$L.log" "ab_${L}_$v.log"
done
