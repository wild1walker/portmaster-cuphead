# Cuphead on PortMaster

This port contains **no game files**: you provide your own copy of Cuphead, and the first launch
builds the handheld version from it. Cuphead runs on Unity's own Android player, run directly on
the handheld's 32-bit Linux by the port's runtime (no Android, no emulator).

Full guide, known issues and bug reports: https://github.com/wild1walker/portmaster-cuphead

## What you need

- **Cuphead for macOS from GOG** (version 1.3.4; GOG's current macOS installers say 1.3.9 in their
  names and hold the same build). The DLC, The Delicious Last Course, is picked up when it is
  there. Either:
  - GOG's macOS installers (`.pkg`): the game's and, if you have it, the DLC's, or
  - the installed `Cuphead.app`, copied from a Mac (keep the folder as it is).

  The Windows version does not work (its shaders are Direct3D only).
- **Wi-Fi on the first launch**, to download Unity 2017.4.9f1's Android player from Unity (about
  190 MB). Or put `UnitySetup-Android-Support-for-Editor-2017.4.9f1.exe` from Unity's download
  archive into `gamedata/` yourself.
- **About 10 GB free** on the card during the setup; about 2.5 GB stay used afterwards.
- **A handheld with a 32-bit (armhf) userland** and OpenGL ES 3 (tested on an Anbernic RG34XX-SP
  with Knulli).

## Setting it up

1. Copy the `.pkg` installers or `Cuphead.app` into `ports/cuphead/gamedata/`.
2. Start Cuphead from the Ports menu. PortMaster first asks for the texture quality:
   - **Medium** (recommended): what the port is tested with.
   - **Fast**: the setup takes about an hour less, with slightly less exact textures.

   With an older PortMaster that asks no questions, `ports/cuphead/setup.txt` decides
   (`texture_quality=fast`).
3. Wait. The setup unpacks the game, downloads Unity's player, converts the game for the handheld
   and packs it: about 1 to 3 hours, once. If it is interrupted (battery, power button), start
   Cuphead again: it continues where it stopped. Its log is `ports/cuphead/patchlog.txt`.
4. When it is done the game starts, and the setup deletes the installers in `gamedata/` (set
   `keep_installers=1` in `setup.txt` to keep them).

## Playing

- The buttons work by position, as on an Xbox pad. With the letters of a Nintendo-layout handheld:
  B (bottom) jump and confirm, Y (left) shoot, A (right) EX shot and back, X (top) dash, L1
  switch weapon, R1 lock, Start pause. Options > Controls changes them. The on-screen prompts
  show the letters printed on the handheld, Nintendo layout (B bottom, A right, Y left, X top).
  For a handheld with Xbox letters, add `export CUPHEAD_PROMPTS=xbox` to `Cuphead.sh` (below the
  line `get_controls`).
- **Select + Start** quits.
- Saves and settings: `ports/cuphead/data/`.
- The last run's log: `ports/cuphead/log.txt`.

## Licences

The port's code is MIT (`licenses/`). The setup tools are removed when the setup is done; their
licences are in `licenses/` too. Cuphead is a trademark of StudioMDHR Entertainment Inc.; Unity is
a trademark of Unity Technologies. This port is not affiliated with either.
