#!/bin/bash
# Device-side: one level for profiling. profile_level.sh <Levels name> [boss state to skip to|-] [secs] [release|dev]
# Logs prof_<level>.log (game, UA_FPS stats; with dev, CUPHEAD_MARKERS: Unity's samplers every 10 s)
# and prof_<level>.cpu (per-thread CPU every 10 s, profcpu.py).
cd "$(dirname "$0")" || exit 1
L=$1; SK=${2:--}; SECS=${3:-180}; FL=${4:-dev}
[ "$SK" = - ] && SK=
# PROF_INPUT (in-level part of the input script), PROF_UNSTICK, PROF_HOLD (CUPHEAD_TEST_PHASEHOLD) for run-and-guns and phase walks
CUPHEAD_MARKERS=$([ "$FL" = dev ] && echo 1 || echo 0) UA_PAD_NAME='Xbox Wireless Controller' UA_FPS=5 \
CUPHEAD_TEST_BENCH=1 CUPHEAD_TEST_GODMODE=1 CUPHEAD_TEST_WEAPON=homing CUPHEAD_TEST_LEVEL=$L CUPHEAD_TEST_SKIPTO=$SK \
CUPHEAD_TEST_UNSTICK=${PROF_UNSTICK:-0} CUPHEAD_TEST_PHASEHOLD=${PROF_HOLD:-} \
UA_INPUT_SCRIPT="30:a 36:a 50:a 56:a 62:a ${PROF_INPUT:-75@10:x~9.9 80@1.5:a}" ./run.sh "prof_$L.log" "$SECS" "$FL" &
sleep 3
pid=$(pidof ua_host)
[ -n "$pid" ] && python3 profcpu.py "$pid" 10 7 > "prof_$L.cpu"
wait
