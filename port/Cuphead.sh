#!/bin/bash
# PortMaster launcher: Cuphead on Unity's own Android player, run natively by portmaster-unity's
# ua_host on the handheld's 32-bit userland. ports/cuphead/ holds build.sh's device/ folder:
# ua_host, lib/ (Unity's player), Cuphead.apk, main.1.com.cupheadport.game.obb,
# android_package.json, assets/ (Mono's config), goggame-*.info, animclips.txt,
# cuphead_hotkey.gptk. Saves and settings go to ports/cuphead/data/. Select+Start quits.

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
exec > >(tee "$GAMEDIR/log.txt") 2>&1

# the built-in pad reaches the game as an Android gamepad (SDL GameController -> KeyEvent and
# MotionEvent); Rewired knows Android pads by name, and this one maps as a standard gamepad
export UA_PAD_NAME="${UA_PAD_NAME:-Xbox Wireless Controller}"
# audio: the system's default ALSA device, else (a sound server holding the card and no 32-bit
# client libraries) raw PCM piped into pw-cat / pacat
export SDL_AUDIODRIVER=alsa
# a line of frame statistics every 10 s in log.txt
export UA_FPS=${UA_FPS:-10}

# exit hotkey only
$GPTOKEYB "ua_host" -c "$GAMEDIR/cuphead_hotkey.gptk" &
pm_platform_helper "$GAMEDIR/ua_host"

# the 32-bit libraries first (Knulli: /lib32, /usr/lib32)
LD_LIBRARY_PATH=/lib32:/usr/lib32${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH} ./ua_host "$GAMEDIR"
echo "game exited ($?)"

$ESUDO kill -9 $(pidof gptokeyb gptokeyb2) 2>/dev/null
pm_finish
