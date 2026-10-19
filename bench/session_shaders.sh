#!/bin/bash
# Device-side session for the new shader converter: level screenshots of the three APKs
# (Cuphead = old converter, Cuphead_L = new converter with ES default sampler precision,
# Cuphead_S = new converter, full precision), then fps A/B of the rewrites and of the precision.
cd "$(dirname "$0")" || exit 1
./devshots.sh blur2 OldMan 120,300,480,660 115 Cuphead_L Cuphead Cuphead_S
./devshots.sh veg2 Veggies 120,300,480,660 115 Cuphead_L Cuphead Cuphead_S
./ab_device.sh rw OldMan - 150 3 "" "UA_APK_NAME=Cuphead_L"
./ab_device.sh prec OldMan - 150 3 "UA_APK_NAME=Cuphead_L" "UA_APK_NAME=Cuphead_S"
echo "$(date +%T) session done"
/etc/init.d/S31emulationstation start >/dev/null 2>&1
