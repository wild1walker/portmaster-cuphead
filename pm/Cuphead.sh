#!/bin/bash
# PORTMASTER: cuphead.zip, Cuphead.sh
#
# Cuphead (GOG macOS 1.3.4, with or without The Delicious Last Course) on Unity's own Android
# player, run natively on the handheld's 32-bit userland by portmaster-unity's ua_host. On the first
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

# ---- first launch: build the game from your copy (PortMaster's patcher shows the progress)
if [ ! -f "$GAMEDIR/.cuphead_ready" ]; then
  if [ -f "$GAMEDIR/setup/patchscript" ] && [ -f "$controlfolder/utils/patcher.txt" ]; then
    export GAMEDIR DEVICE_ARCH controlfolder
    chmod +x "$GAMEDIR/setup/patchscript"
    export PATCHER_FILE="$GAMEDIR/setup/patchscript"
    export PATCHER_GAME="Cuphead"
    export PATCHER_TIME="one to a few hours, once"
    source "$controlfolder/utils/patcher.txt"
    $ESUDO kill -9 $(pidof gptokeyb) 2>/dev/null
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
# audio: the system's default ALSA device, else (a sound server holding the card and no 32-bit
# client libraries) raw PCM piped into pw-cat / pacat
export SDL_AUDIODRIVER=alsa
export UA_FPS=${UA_FPS:-10}                     # a line of frame statistics every 10 s in log.txt
chmod +x "$GAMEDIR/ua_host"

$GPTOKEYB "ua_host" -c "$GAMEDIR/cuphead_hotkey.gptk" &   # exit hotkey only (Select+Start)
pm_platform_helper "$GAMEDIR/ua_host"

# the 32-bit libraries first (Knulli: /lib32, /usr/lib32)
LD_LIBRARY_PATH=/lib32:/usr/lib32${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH} ./ua_host "$GAMEDIR"
echo "game exited ($?)"

$ESUDO kill -9 $(pidof gptokeyb gptokeyb2) 2>/dev/null
pm_finish
