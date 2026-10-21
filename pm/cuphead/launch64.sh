# Cuphead's 64-bit build: sourced by Cuphead.sh on a system that cannot run 32-bit (armhf) programs
# (or with CUPHEAD_ROUTE=64, a test switch). Unity has no 64-bit Android player for Cuphead's own
# Unity version, and its 64-bit players run only code compiled ahead of time (IL2CPP): the setup
# compiles the game's code for Unity's 64-bit player 2017.4.40f1, which the port's 64-bit runtime
# (ua_host64) runs.
#
# Everything of it is its own, so a card moved between a 32-bit and a 64-bit-only handheld keeps
# the two builds apart: ua_host64, the setup in setup64/ (its work folder gamedata/.setup64/), the
# marker .cuphead_ready64 and arm64/, the folder ua_host64 runs (the player's libraries in lib/,
# the game's APK, the saves in data/). log.txt and patchlog.txt are shared.

echo "Cuphead (64-bit build${CUPHEAD_ROUTE:+, CUPHEAD_ROUTE=$CUPHEAD_ROUTE}): $CFW_NAME on $DEVICE_NAME ($DEVICE_ARCH), ${DISPLAY_WIDTH}x${DISPLAY_HEIGHT}, RAM $(awk '/MemTotal/{print int($2/1024)" MB"}' /proc/meminfo)"

# ---- first launch: build the game from your copy (PortMaster's patcher shows the progress)
if [ ! -f "$GAMEDIR/.cuphead_ready64" ]; then
  if [ -f "$GAMEDIR/setup64/patchscript" ] && [ -f "$controlfolder/utils/patcher.txt" ]; then
    export GAMEDIR DEVICE_ARCH controlfolder
    $ESUDO chmod +x "$GAMEDIR/setup64/patchscript"
    export PATCHER_FILE="$GAMEDIR/setup64/patchscript"
    export PATCHER_GAME="Cuphead"
    export PATCHER_TIME="several hours, once"
    export PATCHER_QUESTIONS="$GAMEDIR/setup64/questions.lua"
    source "$controlfolder/utils/patcher.txt"
    $ESUDO kill -9 $(pidof gptokeyb gptokeyb2) 2>/dev/null
  fi
  if [ ! -f "$GAMEDIR/.cuphead_ready64" ]; then
    pm_message "Cuphead (64-bit) is not set up yet: put Cuphead.app or GOG's macOS installers in ports/cuphead/gamedata and launch again (ports/cuphead/README.md; log: ports/cuphead/patchlog.txt)."
    sleep 8
    pm_finish
    exit 1
  fi
fi

# the built-in pad reaches the game as an Android gamepad, as in the 32-bit build
export UA_PAD_NAME="${UA_PAD_NAME:-Xbox Wireless Controller}"
export SDL_GAMECONTROLLERCONFIG="$sdl_controllerconfig"   # PortMaster's mapping of the built-in pad
# audio: SDL's own choice (a 64-bit program reaches the system's sound server or ALSA directly);
# raw PCM piped into pw-cat / pacat when SDL finds no device
export UA_FPS=${UA_FPS:-10}                     # a line of frame statistics every 10 s in log.txt
# the helper's code warm-up compiles methods before their first use under Mono; IL2CPP's code is
# compiled already (the warm-up would only cost memory)
export CUPHEAD_CODEWARM=${CUPHEAD_CODEWARM:-0}
$ESUDO chmod +x "$GAMEDIR/ua_host64"

$GPTOKEYB "ua_host64" -c "$GAMEDIR/cuphead_hotkey.gptk" &   # exit hotkey only (Select+Start)
pm_platform_helper "$GAMEDIR/ua_host64"

"$GAMEDIR/ua_host64" "$GAMEDIR/arm64"
echo "game exited ($?)"

$ESUDO kill -9 $(pidof gptokeyb gptokeyb2) 2>/dev/null
pm_finish
