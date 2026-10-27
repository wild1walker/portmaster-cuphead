# Cuphead's 64-bit build, sourced by Cuphead.sh on a firmware without 32-bit support. Unity has no
# 64-bit Android player for Cuphead's Unity version and its 64-bit players are IL2CPP ones: the
# game's code runs on Unity 2017.4.40f1's 64-bit player through Unity's Mono for aarch64, which the
# port ships (the Mono route; with CUPHEAD_64_IL2CPP=1 the setup compiles the code with IL2CPP
# instead). Its files are its own: setup64/, .cuphead_ready64 and arm64/ (the player, the game, the
# saves in arm64/data/).

T="one to a few hours, once"; [ "${CUPHEAD_64_IL2CPP:-0}" = 1 ] && T="several hours, once"
[ -f "$GAMEDIR/.cuphead_ready64" ] || run_setup "$GAMEDIR/setup64/patchscript" "$T"
if [ ! -f "$GAMEDIR/.cuphead_ready64" ]; then
  pm_message "Cuphead (64-bit) is not set up yet: put Cuphead.app or GOG's macOS installers in ports/cuphead/gamedata and launch again (log: ports/cuphead/patchlog.txt)."
  sleep 8
  pm_finish
  exit 1
fi
dlc_waiting "$GAMEDIR/arm64" && export DLC_TARGET="$GAMEDIR/arm64" && run_setup "$GAMEDIR/setup/dlcscript" "about half an hour, once" no-questions

# the picture shape the setup asked for (aspect.txt: "fill", "16:9" for the game's letterbox on any
# screen, "crop" to fill 4:3 screens too, or a shape such as "4:3" between the two),
# and an optional smaller render size (resolution.txt: a share of the panel's, e.g. 0.75, which the
# display engine scales up to the whole panel)
A=$(cat "$GAMEDIR/aspect.txt" 2>/dev/null); case "$A" in crop|[0-9]*:[0-9]*) export CUPHEAD_ASPECT="$A" ;; esac
R=$(cat "$GAMEDIR/resolution.txt" 2>/dev/null); [ -n "$R" ] && [ "$R" != device ] && export UA_RENDER_SCALE="$R"

# the helper's code warm-up is for Mono's JIT (the Mono route: on, as in the 32-bit build); IL2CPP's
# code is compiled already
[ -f "$GAMEDIR/arm64/lib/libmonobdwgc-2.0.so" ] || export CUPHEAD_CODEWARM=0
$ESUDO chmod +x "$GAMEDIR/ua_host64"
$GPTOKEYB "ua_host64" -c "$GAMEDIR/cuphead_hotkey.gptk" &
pm_platform_helper "$GAMEDIR/ua_host64"
"$GAMEDIR/ua_host64" "$GAMEDIR/arm64"

pm_finish
