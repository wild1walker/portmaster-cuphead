#!/bin/bash
# Device-side A/B of two configurations, alternating so both see the same thermal state:
#   ab_device.sh <tag> <level> <skip-to state|-> <secs> <reps> "<env A>" "<env B>"
# e.g. ab_device.sh blit OldMan - 200 3 "" "UA_APK_NAME=Cuphead_B"
# Each run is profile_level.sh (release player) with that env; logs ab/<tag>_<A|B><i>.log (+ .cpu).
# Summarize on the Mac with host/absum.py ab/<tag>_*.log
cd "$(dirname "$0")" || exit 1
mkdir -p ab
TAG=$1; L=$2; SK=$3; SECS=$4; REPS=$5; EA=$6; EB=$7
for i in $(seq "$REPS"); do
  for v in A B; do
    e=$EA; [ "$v" = B ] && e=$EB
    echo "$(date +%T) $TAG run $v$i: $e"
    env $e ./profile_level.sh "$L" "$SK" "$SECS" release
    mv "prof_$L.log" "ab/${TAG}_$v$i.log"; mv "prof_$L.cpu" "ab/${TAG}_$v$i.cpu" 2>/dev/null
  done
done
echo "$(date +%T) $TAG done"
