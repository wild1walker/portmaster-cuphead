## Notes

Requires **Cuphead for macOS from GOG** (version 1.3.4; GOG's current macOS installers hold the
same build), with or without The Delicious Last Course. The Windows version does not work (its
shaders are Direct3D only).

Copy GOG's macOS installers (`.pkg`) or the installed `Cuphead.app` into `ports/cuphead/gamedata/`
and start Cuphead. The first launch builds the game on the handheld (one to a few hours, once; about
10 GB free on the card during the setup, about 2.5 GB afterwards): it downloads Unity 2017.4.9f1's
Android player from Unity (about 190 MB, Wi-Fi needed; or put
`UnitySetup-Android-Support-for-Editor-2017.4.9f1.exe` into `gamedata/`), converts the game's
shaders, textures and audio for the handheld, and removes the installers when it is done. If it is
interrupted, start Cuphead again: it continues where it stopped (log: `ports/cuphead/patchlog.txt`).

The game runs on Unity's own Android player, loaded natively on the handheld's Linux by the port's
runtime (no Android, no emulation). On a firmware without 32-bit (armhf) support the setup builds
a 64-bit version instead, which takes several hours longer (it compiles the game's code with Unity's
IL2CPP on the handheld) and needs about 12 GB free.

Saves: `ports/cuphead/data/` (64-bit build: `ports/cuphead/arm64/data/`).

Source and build instructions: https://github.com/wild1walker/portmaster-cuphead

Thanks to StudioMDHR for Cuphead, and to the PortMaster team.

## Controls

The buttons work by position, as on an Xbox pad; Options > Controls changes them.

| Button (Nintendo layout) | Action |
|--|--|
| B (bottom) | Jump / Confirm |
| Y (left) | Shoot |
| A (right) | EX shot / Back |
| X (top) | Dash |
| L1 | Switch weapon |
| R1 | Lock |
| Start | Pause |
| Select + Start | Quit |
