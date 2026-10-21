#!/bin/bash
# PORTMASTER: cuphead.zip, Cuphead.sh
#
# Cuphead (GOG macOS 1.3.4, with or without The Delicious Last Course) on Unity's own Android
# player, run natively on the handheld's 32-bit userland by the port's runtime (ua_host). On the first
# launch the setup builds the game from your copy in ports/cuphead/gamedata/ (see README.md there).

XDG_DATA_HOME=${XDG_DATA_HOME:-$HOME/.local/share}
if [ -d "/opt/system/Tools/PortMaster/" ]; then
  controlfolder="/opt/system/Tools/PortMaster"
elif [ -d "/opt/tools/PortMaster/" ]; then
  controlfolder="/opt/tools/PortMaster"
elif [ -d "$XDG_DATA_HOME/PortMaster/" ]; then
  controlfolder="$XDG_DATA_HOME/PortMaster"
else
  controlfolder="/roms/ports/PortMaster"
fi
source $controlfolder/control.txt
[ -f "${controlfolder}/mod_${CFW_NAME}.txt" ] && source "${controlfolder}/mod_${CFW_NAME}.txt"
get_controls

GAMEDIR="/$directory/ports/cuphead"
cd "$GAMEDIR" || exit 1
mv -f "$GAMEDIR/log.txt" "$GAMEDIR/log.prev.txt" 2>/dev/null
> "$GAMEDIR/log.txt" && exec > >(tee "$GAMEDIR/log.txt") 2>&1
echo "Cuphead: $CFW_NAME on $DEVICE_NAME ($DEVICE_ARCH), ${DISPLAY_WIDTH}x${DISPLAY_HEIGHT}, RAM $(awk '/MemTotal/{print int($2/1024)" MB"}' /proc/meminfo)"

# Cuphead's Unity player is 32-bit: without the 32-bit loader (ua_host's interpreter) nothing can
# run, so stop before the setup's hours of work
if [ ! -e /lib/ld-linux-armhf.so.3 ]; then
  pm_message "Cuphead needs a firmware with 32-bit (armhf) support, and this one has none (no /lib/ld-linux-armhf.so.3)."
  sleep 8
  pm_finish
  exit 1
fi

# ---- first launch: build the game from your copy (PortMaster's patcher shows the progress)
if [ ! -f "$GAMEDIR/.cuphead_ready" ]; then
  if [ -f "$GAMEDIR/setup/patchscript" ] && [ -f "$controlfolder/utils/patcher.txt" ]; then
    export GAMEDIR DEVICE_ARCH controlfolder
    $ESUDO chmod +x "$GAMEDIR/setup/patchscript"
    export PATCHER_FILE="$GAMEDIR/setup/patchscript"
    export PATCHER_GAME="Cuphead"
    export PATCHER_TIME="one to a few hours, once"
    export PATCHER_QUESTIONS="$GAMEDIR/setup/questions.lua"
    source "$controlfolder/utils/patcher.txt"
    $ESUDO kill -9 $(pidof gptokeyb gptokeyb2) 2>/dev/null
  fi
  if [ ! -f "$GAMEDIR/.cuphead_ready" ]; then
    pm_message "Cuphead is not set up yet: put Cuphead.app or GOG's macOS installers in ports/cuphead/gamedata and launch again (ports/cuphead/README.md; log: ports/cuphead/patchlog.txt)."
    sleep 8
    pm_finish
    exit 1
  fi
fi

# the built-in pad reaches the game as an Android gamepad (SDL GameController -> KeyEvent and
# MotionEvent); Rewired knows Android pads by name, and this one maps as a standard gamepad
export UA_PAD_NAME="${UA_PAD_NAME:-Xbox Wireless Controller}"
export SDL_GAMECONTROLLERCONFIG="$sdl_controllerconfig"   # PortMaster's mapping of the built-in pad
# audio: the system's default ALSA device, else (a sound server holding the card and no 32-bit
# client libraries) raw PCM piped into pw-cat / pacat
export SDL_AUDIODRIVER=alsa
# dArkOS on a kernel older than 5.6 (RK3326: 4.4): its 32-bit alsa-lib (Debian's libasound2t64,
# 64-bit time_t) sends the time64 PCM ioctls those kernels do not know ("Inappropriate ioctl for
# device"), and dArkOS runs no sound server for the pipe fallback. The PCM goes to the system's own
# 64-bit aplay instead: the card directly (no dmix buffer: ~90 ms less lag), else, when the card is
# busy, dArkOS's default device (plug -> dmix). aplay opens the device before it reads the pipe.
kv=$(uname -r); kmaj=${kv%%.*}; kmin=${kv#*.}; kmin=${kmin%%.*}
if [ "$CFW_NAME" = "dArkOS" ] && command -v aplay >/dev/null && \
   { [ "$kmaj" -lt 5 ] || { [ "$kmaj" -eq 5 ] && [ "$kmin" -lt 6 ]; }; }; then
  a="aplay -q -t raw -f S16_LE -r ${UA_AUDIO_RATE:-48000} -c 2 --buffer-time=40000"
  export ANDROID_SLES_OUTPUT=${ANDROID_SLES_OUTPUT:-pipe}
  export ANDROID_SLES_PIPE_CMD=${ANDROID_SLES_PIPE_CMD:-"$a -D plughw:0,0 --period-time=10000 - 2>/dev/null || { echo 'audio: plughw:0,0 busy, dmix' >&2; exec $a -; }"}
  echo "audio: kernel $kv, 32-bit ALSA unusable: pipe -> $ANDROID_SLES_PIPE_CMD"
fi
export UA_FPS=${UA_FPS:-10}                     # a line of frame statistics every 10 s in log.txt
$ESUDO chmod +x "$GAMEDIR/ua_host"

$GPTOKEYB "ua_host" -c "$GAMEDIR/cuphead_hotkey.gptk" &   # exit hotkey only (Select+Start)
pm_platform_helper "$GAMEDIR/ua_host"

# the 32-bit libraries first (Knulli: /lib32, /usr/lib32)
LD_LIBRARY_PATH=/lib32:/usr/lib32${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH} ./ua_host "$GAMEDIR"
echo "game exited ($?)"

$ESUDO kill -9 $(pidof gptokeyb gptokeyb2) 2>/dev/null
pm_finish
