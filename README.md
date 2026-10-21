# Cuphead for PortMaster

Play **Cuphead** (and **The Delicious Last Course** DLC, if you own it) on a Linux handheld with
[PortMaster](https://portmaster.games/).

- **For:** players with a handheld that runs PortMaster and has 32-bit ARM libraries (tested on an
  Anbernic RG34XX-SP with Knulli).
- **You need your own copy of the game.** This port contains **no game files**, and neither do its
  releases. You bring Cuphead for macOS from GOG; the port turns it into a handheld version the
  first time you start it.
- **How it runs:** Unity (the engine Cuphead is made with) has an Android version of its player.
  The [portmaster-unity](https://github.com/wild1walker/portmaster-unity) tools run that player
  directly on the handheld's Linux, with no Android and no emulator. That is why Cuphead runs at
  close to its full 60 frames per second.
- **It looks and plays like the original.** Nothing the port turns on by default changes the
  picture or the game play. The few things that do are options, off unless you choose them.

## Contents

- [What you need](#what-you-need)
- [Install](#install)
- [The first launch (setup)](#the-first-launch-setup)
- [Controls](#controls)
- [Performance](#performance)
- [Options](#options)
- [Known issues](#known-issues)
- [If the setup stops](#if-the-setup-stops)
- [Getting help and reporting a bug](#getting-help-and-reporting-a-bug)
- [For developers](#for-developers)
- [Licence and credits](#licence-and-credits)

## What you need

| | |
|---|---|
| **A handheld** | One that runs PortMaster and has a 32-bit (armhf) userland: the 32-bit ARM libraries, with OpenGL ES 3 graphics. Knulli on Allwinner H700 handhelds has them. |
| **Tested on** | Anbernic RG34XX-SP (Allwinner H700, Mali-G31, 1 GB RAM, 720x480 screen) with Knulli. Other handhelds and firmwares have not been tried yet. ROCKNIX: unknown. Firmwares without 32-bit libraries cannot run this port. |
| **The game** | Cuphead for **macOS** from **GOG**, version 1.3.4, with or without the DLC. Either GOG's macOS installers (`.pkg` files: one for the game, one for the DLC), or the installed `Cuphead.app` copied from a Mac. GOG's current macOS installers have "1.3.9" in their file names; they hold the same game build, and the setup checks it. |
| **Not the Windows version** | The Windows version does not work: its graphics code (shaders) is for Direct3D only. GOG gives you both versions with the game, so download the macOS one. Versions from other stores have not been tried. |
| **Free space** | About 10 GB free on the card while the setup runs. About 2.5 GB stay used afterwards. |
| **Wi-Fi** | On the first launch only, to download Unity's Android player from Unity (about 190 MB). No Wi-Fi? See step 4 below. |
| **Time** | The first launch takes about 1 to 3 hours, once. Every launch after that starts the game straight away. |

## Install

PortMaster keeps its games in a folder called `ports`. On Knulli that is the `roms/ports` folder on
the card with your games (`/userdata/roms/ports` on the handheld, `share/roms/ports` over the
network). On other firmwares use the `ports` folder PortMaster installs its ports into.

1. **Download the port.** Get `cuphead.zip` from this project's
   [Releases page](https://github.com/wild1walker/portmaster-cuphead/releases). The newest release
   may be marked "Pre-release": that is the one to take.
2. **Unzip it into `ports`.** You should now have:
   ```
   ports/
     Cuphead.sh
     cuphead/
       gamedata/
       README.md
       ...
   ```
3. **Copy your game into `ports/cuphead/gamedata/`.** Either the GOG macOS installers (the `.pkg`
   files, the game's and the DLC's) or the whole `Cuphead.app` folder, as it is.
4. **No Wi-Fi on the handheld?** Download `UnitySetup-Android-Support-for-Editor-2017.4.9f1.exe`
   from Unity's download archive on a computer and put it into `ports/cuphead/gamedata/` too. The
   setup then uses that file instead of downloading it.
5. **Start Cuphead from the Ports menu.** If it does not show up, refresh your game list or restart
   the handheld. The first start runs the setup (next section).

To remove the port, delete `ports/Cuphead.sh` and the `ports/cuphead/` folder. Your saves are in
`ports/cuphead/data/`: copy that folder somewhere first if you want to keep them.

**Updating to a newer release:** back up `ports/cuphead/data/` (your saves) first, and read the
release's notes: they say whether the new release needs the setup to run again.

## The first launch (setup)

The first time you start Cuphead, PortMaster shows a setup screen with the progress.

1. **It asks for the texture quality.**
   - **Medium (recommended):** what the port is tested with.
   - **Fast:** the setup takes about an hour less, and the textures are slightly less exact (hard to
     see on a small screen).

   Your first answer is kept, even if the setup is interrupted. Older PortMaster versions do not
   ask: then `ports/cuphead/setup.txt` decides (`texture_quality=medium` or `fast`).
2. **It builds the game for the handheld.** It unpacks GOG's installers (10 to 20 minutes),
   downloads and unpacks Unity's Android player, converts the game's graphics, textures and
   settings for the handheld (the long part), and packs everything for the player.
3. **When it is done, the game starts.** You can then delete the installers or `Cuphead.app` from
   `gamedata/`.

Good to know:

- **You can stop it.** If the battery runs out or you press the power button, just start Cuphead
  again: the setup continues where it stopped.
- **Keep the handheld plugged in** if you can: it is a long job for a small battery.
- **The setup's log** is `ports/cuphead/patchlog.txt`. If something goes wrong, that file says why.

You can also do this conversion on a computer in minutes instead of hours: see
[docs/building.md](docs/building.md).

## Controls

The buttons work **by position**, as on an Xbox controller: the **bottom face button jumps and
confirms** in menus, and the other buttons follow the game's usual controller layout by position.

| Button | Does |
|---|---|
| Bottom face button | jump; confirm in menus |
| The other buttons | the game's normal controller layout, by position (as on an Xbox pad) |
| **Select + Start** | quit the game |

**Button prompts.** The letters the game shows on screen are the ones printed on your handheld,
in the Nintendo layout: **B** bottom, **A** right, **Y** left, **X** top, as on Anbernic, Miyoo,
Powkiddy and TrimUI handhelds. So the prompts say B for jump and confirm. If your handheld has
Xbox letters (A at the bottom), set `CUPHEAD_PROMPTS=xbox` (see [Options](#options)).

Saves and settings are in `ports/cuphead/data/`.

## Performance

Measured on the RG34XX-SP with an automated run through every level and boss phase:

- **About 60 frames per second** in most fights and levels.
- **About 40 to 49 fps** in the heaviest phases of the DLC (Howling Aces' last phases, Saltbaker's
  second phase) and in two run-and-gun levels (Treetop Trouble, Perilous Piers).
- **About 33 to 35 fps** for Glumstone the Giant on Expert, the slowest fight.
- After a long session the handheld gets warm and runs a little slower.
- Copying big files to the card while you play makes the game stutter.

The numbers per level and what the port does to get there: [docs/technical.md](docs/technical.md).

## Options

Options are settings you can change by hand. To use one, open `ports/Cuphead.sh` in a text editor
and add a line such as `export CUPHEAD_PROMPTS=xbox` below the line that says `get_controls`.
Installing a new release replaces `Cuphead.sh`, so add your lines again after an update.

| Option | What it does |
|---|---|
| `CUPHEAD_PROMPTS=xbox` | Show Xbox button letters, for handhelds labelled like an Xbox pad (default: the Nintendo layout's letters). The buttons themselves do not move. |
| `CUPHEAD_ASPECT=16:9` | Show the whole 16:9 picture with black bars, instead of filling the 3:2 screen with the sides cropped (default). |
| `CUPHEAD_RUMBLE=0` | No rumble. |
| `CUPHEAD_RUMBLE=game` | The game's own rumble (default: the game's hit rumble as short pulses). |
| `CUPHEAD_FX=half` or `sharp` | A cheaper blur effect. **Changes the picture slightly**; the default keeps the game's own. |

More options for testing and measuring: [docs/technical.md](docs/technical.md#options).

## Known issues

- **Slower phases:** a few DLC phases and Glumstone on Expert run below 60 fps (see
  [Performance](#performance)).
- **King Dice's final fight** has not been checked by the automated test run yet.
- **Cropped sides:** on a 3:2 screen the 16:9 picture is cropped a little at the left and right, so
  it fills the screen. `CUPHEAD_ASPECT=16:9` shows the whole picture with black bars instead.
- **Rumble** plays as short pulses by default. With an earlier version of this port, the
  RG34XX-SP's motor did not stop when the game asked it to; this has not been checked again with
  the current port, so the short pulses stay the default.
- **The first launch is long** (1 to 3 hours) and needs Wi-Fi, or the Unity file from step 4.
- **Only GOG's macOS version** has been tested. The Windows version does not work.
- **Other handhelds:** only the RG34XX-SP with Knulli has been tested. If you try another one,
  please report how it went.

## If the setup stops

PortMaster shows the setup's message. The most common ones:

| Message | What to do |
|---|---|
| "Put the installed Cuphead.app (GOG macOS) or GOG's macOS installers (.pkg) in ports/cuphead/gamedata" | The setup found no game. Check that the files are directly in `ports/cuphead/gamedata/`. |
| "These are the Windows installers" | Download the macOS installers from GOG instead. |
| "This Cuphead version is not supported" | The setup needs the build of version 1.3.4 (Unity 2017.4.9f1), as GOG's macOS installers have it. |
| "Unpacking the installers needs about 9 GB free on the card" | Make space on the card, then start Cuphead again. |
| "Downloading Unity's player failed: is Wi-Fi on?" | Turn on Wi-Fi, or put the Unity file from step 4 into `gamedata/`. |
| "PortMaster's 7-Zip is missing: update PortMaster." | Update PortMaster, then start Cuphead again. |
| Any other message | Start Cuphead again (the setup continues where it stopped). If it stops again, [report it](#getting-help-and-reporting-a-bug) with `patchlog.txt`. |

## Getting help and reporting a bug

1. Check [Known issues](#known-issues) and [If the setup stops](#if-the-setup-stops) above.
2. Look at the [open issues](https://github.com/wild1walker/portmaster-cuphead/issues): someone may
   have reported it already.
3. Open a [new issue](https://github.com/wild1walker/portmaster-cuphead/issues/new/choose) and pick
   a form: **Problem report** (setup, start or in game), **Performance report** or **Feature
   request**. The forms ask for what we need to help.

Please attach these two files from `ports/cuphead/`:

| File | What is in it |
|---|---|
| `log.txt` | the last time the game ran (the time before that: `log.prev.txt`) |
| `patchlog.txt` | the setup |

On Knulli they are in `roms/ports/cuphead/` on the card, or `share/roms/ports/cuphead/` over the
network. **Never attach game files**, and remove anything personal from the logs if you see it.

Problems with the tools for other Unity games belong in
[portmaster-unity](https://github.com/wild1walker/portmaster-unity/issues) instead.

## For developers

- [docs/technical.md](docs/technical.md): how the port works, what it changes in the game, every
  option, the measurements and the test scripts.
- [docs/building.md](docs/building.md): building the release zip, and converting the game on a
  computer instead of the handheld.
- [bench/README.md](bench/README.md): the automated tests that run on the handheld.

**No game files and nothing from Unity** may ever be added to this repository or its releases:
the port's code works on the player's own copy of the game, on their handheld.

## Licence and credits

The code in this repository is MIT licensed (see [LICENSE](LICENSE)).

Cuphead is a trademark of StudioMDHR Entertainment Inc. Unity is a trademark of Unity
Technologies. This project is not affiliated with or endorsed by StudioMDHR or Unity Technologies.
The code patches and the helper work on your own copy of the game and contain none of its code or
assets.

Credits:

- **StudioMDHR** made Cuphead.
- **[PortMaster](https://portmaster.games/)** installs and launches the port, and its patcher shows
  the setup.
- **[portmaster-unity](https://github.com/wild1walker/portmaster-unity)** provides the runtime
  (`ua_host`) and the converter. It uses [libhybris](https://github.com/libhybris/libhybris)
  (Apache 2.0) and links the video decoders libvpx, libvorbis, libogg, libopus and OpenH264.
- The setup runs on [python-build-standalone](https://github.com/astral-sh/python-build-standalone)'s
  CPython with [UnityPy](https://github.com/K0lb3/UnityPy) and ARM's astc-encoder; the code patcher
  uses [Mono.Cecil](https://github.com/jbevain/cecil) and the .NET runtime.

The release zip carries the licence of every part it contains, in `cuphead/licenses/`.
