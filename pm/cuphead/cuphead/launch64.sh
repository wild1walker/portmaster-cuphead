# Cuphead's 64-bit build, sourced by Cuphead.sh on a firmware without 32-bit support. Unity has no
# 64-bit Android player for Cuphead's Unity version and runs only IL2CPP code on 64-bit, so the
# setup compiles the game's code for Unity 2017.4.40f1's 64-bit player. Its files are its own:
# setup64/, .cuphead_ready64 and arm64/ (the player, the game, the saves in arm64/data/).

[ -f "$GAMEDIR/.cuphead_ready64" ] || run_setup "$GAMEDIR/setup64/patchscript" "several hours, once"
if [ ! -f "$GAMEDIR/.cuphead_ready64" ]; then
  pm_message "Cuphead (64-bit) is not set up yet: put Cuphead.app or GOG's macOS installers in ports/cuphead/gamedata and launch again (log: ports/cuphead/patchlog.txt)."
  sleep 8
  pm_finish
  exit 1
fi

# the helper's code warm-up is for Mono; IL2CPP's code is compiled already
export CUPHEAD_CODEWARM=0
$ESUDO chmod +x "$GAMEDIR/ua_host64"
$GPTOKEYB "ua_host64" -c "$GAMEDIR/cuphead_hotkey.gptk" &
pm_platform_helper "$GAMEDIR/ua_host64"
"$GAMEDIR/ua_host64" "$GAMEDIR/arm64"

pm_finish
