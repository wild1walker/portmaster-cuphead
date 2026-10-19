#!/bin/bash
# Device-side: old defaults (A: Cuphead.apk, Boehm's own heap) against the new ones (B: Cuphead_B.apk =
# blit never + 4 Rewired joysticks + helper scan fixes, 64 MB GC heap), alternating, every phase held.
cd "$(dirname "$0")" || exit 1
echo "$(date +%T) defaults session start"
for L in OldMan Saltbaker Airplane FlyingMermaid; do
  ./ab_device.sh "def_$L" "$L" - 330 2 "UA_GC_HEAP=0 PROF_HOLD=30" "UA_APK_NAME=Cuphead_B PROF_HOLD=30"
done
# Treetop: run-and-gun input (ab_device.sh's env cannot carry spaces)
RUN_IN='75@10:x~9.9 75@10:right~9.9 80@1.2:a'
for i in 1 2; do
  for v in A B; do
    if [ $v = A ]; then e="UA_GC_HEAP=0"; else e="UA_APK_NAME=Cuphead_B"; fi
    echo "$(date +%T) def_tree run $v$i: $e"
    env $e PROF_INPUT="$RUN_IN" PROF_UNSTICK=150 ./profile_level.sh Platforming_Level_1_2 - 240 release
    mv prof_Platforming_Level_1_2.log "ab/def_tree_$v$i.log"; mv prof_Platforming_Level_1_2.cpu "ab/def_tree_$v$i.cpu" 2>/dev/null
  done
done
echo "$(date +%T) defaults session done"
