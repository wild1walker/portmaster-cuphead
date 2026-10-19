#!/bin/bash
# Device-side optimization session (after the coverage bench): A/B tests that passed on the Mac,
# then main-thread profiles of the slow phases. Needs Cuphead_B.apk (blit never) beside Cuphead.apk.
cd "$(dirname "$0")" || exit 1
RUN_IN='75@10:x~9.9 75@10:right~9.9 80@1.2:a'
echo "$(date +%T) session start"
PHASEHOLD=30 MAXSECS=400 BENCH_TAG=cov UA_HITCH_TRACE=120 ./bench_all.sh mausoleum
./ab_device.sh blit OldMan - 200 3 "" "UA_APK_NAME=Cuphead_B"
./ab_device.sh blitair Airplane - 240 2 "" "UA_APK_NAME=Cuphead_B"
./ab_device.sh gcheap Train - 260 2 "" "GC_INITIAL_HEAP_SIZE=67108864"
./ab_device.sh audio RumRunners - 160 2 "" "UA_AUDIO_FRAMES=1024"
mkdir -p profiles
for L in Airplane OldMan SnowCult Saltbaker FlyingMermaid FlyingCowboy FlyingBird; do
  UA_PROF_EVERY=15 PROF_HOLD=40 ./profile_level.sh "$L" - 420 release
  mv "prof_$L.log" "profiles/$L.log"; mv "prof_$L.cpu" "profiles/$L.cpu" 2>/dev/null
done
for L in Platforming_Level_1_2 Platforming_Level_3_2 Platforming_Level_2_1; do
  UA_PROF_EVERY=15 PROF_INPUT="$RUN_IN" PROF_UNSTICK=150 ./profile_level.sh "$L" - 240 release
  mv "prof_$L.log" "profiles/$L.log"; mv "prof_$L.cpu" "profiles/$L.cpu" 2>/dev/null
done
echo "$(date +%T) session done"
