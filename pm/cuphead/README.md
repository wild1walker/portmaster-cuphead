# Cuphead on PortMaster

This port contains **no game files**: you provide your own copy of Cuphead, and the first launch
builds the handheld version from it. Cuphead runs on Unity's own Android player, run directly on
the handheld's 32-bit Linux by the port's runtime (no Android, no emulator). On a handheld whose
firmware has no 32-bit support, the port builds a 64-bit version instead (see below).

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
  with Knulli), or a 64-bit-only one (below).

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

## Handhelds without 32-bit support (64-bit build)

On a firmware with no 32-bit (armhf) libraries the launcher picks the 64-bit build by itself. Unity
made its 64-bit Android player only for code compiled ahead of time (IL2CPP), and only from a later
Unity 2017.4 release (2017.4.40f1), so the setup also compiles the game's code for that player, on
the handheld:

- It takes longer: several hours more than the 32-bit setup on a small handheld.
- It needs about 12 GB free on the card during the setup.
- It needs Wi-Fi: it reads about 7 MB of Unity's 2017.4.40f1 player from Unity (or from
  `UnitySetup-Android-Support-for-Editor-2017.4.40f1.exe`, if you put it in `gamedata/`), and the
  tools that compile the code (Unity's IL2CPP, parts of Google's Android NDK).
- A handheld that is too weak (or too short of memory) to compile it says so: then build the
  64-bit version on a computer (`build64.sh` in the port's source, see the full guide) and copy the
  result to the handheld.
- Its files are its own: `ports/cuphead/arm64/` (saves: `ports/cuphead/arm64/data/`). If the card
  had the 32-bit build's saves, the setup copies them once.

The 64-bit build is new and has not been tested on a handheld yet.

## Playing

- The buttons work by position, as on an Xbox pad. With the letters of a Nintendo-layout handheld:
  B (bottom) jump and confirm, Y (left) shoot, A (right) EX shot and back, X (top) dash, L1
  switch weapon, R1 lock, Start pause. Options > Controls changes them. The on-screen prompts
  show the letters printed on the handheld, Nintendo layout (B bottom, A right, Y left, X top).
  For a handheld with Xbox letters, add `export CUPHEAD_PROMPTS=xbox` to `Cuphead.sh` (below the
  line `get_controls`).
- **Select + Start** quits.
- Saves and settings: `ports/cuphead/data/` (the 64-bit build: `ports/cuphead/arm64/data/`).
- The last run's log: `ports/cuphead/log.txt`.

## Licences

The port's code is MIT (`licenses/`). The setup tools are removed when the setup is done; their
licences are in `licenses/` too. Cuphead is a trademark of StudioMDHR Entertainment Inc.; Unity is
a trademark of Unity Technologies. This port is not affiliated with either.
