# Cuphead for PortMaster

Play **Cuphead** (and **The Delicious Last Course** DLC, if you own it) on a Linux handheld with
[PortMaster](https://portmaster.games/).

- **For:** players with a handheld that runs PortMaster and has 32-bit ARM libraries (tested on an
  Anbernic RG34XX-SP with Knulli). Handhelds whose firmware has no 32-bit libraries get a 64-bit
  build instead, picked by the port itself ([64-bit-only handhelds](#64-bit-only-handhelds); new,
  not tested on a handheld yet).
- **You need your own copy of the game.** This port contains **no game files**, and neither do its
  releases. You bring Cuphead for macOS from GOG; the port turns it into a handheld version the
  first time you start it.
- **How it runs:** Unity (the engine Cuphead is made with) has an Android version of its player.
  The port runs that player directly on the handheld's Linux, with no Android and no emulator. That is why Cuphead runs at
  close to its full 60 frames per second.
- **It looks and plays like the original.** Nothing the port turns on by default changes the
  picture or the game play. The few things that do are options, off unless you choose them.

## Contents

- [What you need](#what-you-need)
- [Install](#install)
- [The first launch (setup)](#the-first-launch-setup)
- [64-bit-only handhelds](#64-bit-only-handhelds)
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
| **A handheld** | One that runs PortMaster and has a 32-bit (armhf) userland: the 32-bit ARM libraries, with OpenGL ES 3 graphics. Knulli on Allwinner H700 handhelds has them. A 64-bit ARM firmware without them gets the [64-bit build](#64-bit-only-handhelds). |
| **Tested on** | Players have run it on Knulli, muOS, dArkOS and ROCKNIX (libmali and Adreno); developed on an Anbernic RG34XX-SP (Allwinner H700, Mali-G31, 1 GB RAM, 720x480) with Knulli, where the 64-bit build has run too. |
| **The game** | Cuphead for **macOS** from **GOG**, version 1.3.4, with or without the DLC. Either GOG's macOS installers (`.pkg` files: one for the game, one for the DLC), or the installed `Cuphead.app` copied from a Mac. GOG's current macOS installers have "1.3.9" in their file names; they hold the same game build, and the setup checks it. |
| **Not the Windows version** | The Windows version does not work: its graphics code (shaders) is for Direct3D only. GOG gives you both versions with the game, so download the macOS one. Versions from other stores have not been tried. |
| **Free space** | About 10 GB free on the card while the setup runs (the 64-bit build: about 12 GB). About 2.5 GB stay used afterwards. |
| **Wi-Fi** | Not needed: the port carries Unity's Android player (in small games of its own built with Unity). |
| **Time** | The first launch takes about 1 to 3 hours, once (the 64-bit build: several hours more). Every launch after that starts the game straight away. |

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
4. **Start Cuphead from the Ports menu.** If it does not show up, refresh your game list or restart
   the handheld. The first start runs the setup (next section).

To remove the port, delete `ports/Cuphead.sh` and the `ports/cuphead/` folder. Your saves are in
`ports/cuphead/data/`: copy that folder somewhere first if you want to keep them.

**Updating to a newer release:** back up `ports/cuphead/data/` (your saves) first, and read the
release's notes: they say whether the new release needs the setup to run again.

## The first launch (setup)

The first time you start Cuphead, PortMaster shows a setup screen with the progress.

1. **It builds the game for the handheld.** It unpacks GOG's installers (10 to 20 minutes),
   unpacks Unity's Android player from the port's own files, converts the game's graphics, textures and
   settings for the handheld (the long part), and packs everything for the player.
2. **When it is done, the game starts.** The setup then deletes your installers (or what is left
   of `Cuphead.app`) from `gamedata/`, since the built game does not need them: about 5 GB back
   with the DLC.

Good to know:

- **You can stop it.** If the battery runs out or you press the power button, just start Cuphead
  again: the setup continues where it stopped.
- **Keep the handheld plugged in** if you can: it is a long job for a small battery.
- **The setup's log** is `ports/cuphead/patchlog.txt`. If something goes wrong, that file says why.

You can also do this conversion on a computer in minutes instead of hours: see
[docs/building.md](docs/building.md).

## 64-bit-only handhelds

Some firmwares have no 32-bit ARM libraries at all (the port's launcher sees that by itself). Unity
made a 64-bit Android player only from a later release of Cuphead's Unity line (2017.4.40f1 instead
of the game's 2017.4.9f1), so on such a handheld the port builds a **64-bit version**: the same game,
data and patches, run on Unity's 64-bit player with Unity's Mono for 64-bit ARM, which the port
carries.

- **Install it the same way** (unzip `cuphead.zip`, copy your game into `gamedata/`, start Cuphead).
  The setup asks the same questions and takes about as long as the 32-bit one. No Wi-Fi needed.
- **It needs about 12 GB free** on the card during the setup.
- **Its files are its own,** so a card moved between a 32-bit and a 64-bit-only handheld keeps both
  builds apart: everything of the 64-bit build is in `ports/cuphead/arm64/` (saves:
  `ports/cuphead/arm64/data/`). When the card already has the 32-bit build's saves, the 64-bit setup
  copies them once. A handheld keeps using the build it has set up.

## Controls

The buttons work **by position**, as on an Xbox controller: the **bottom face button jumps and
confirms** in menus. Below, each face button is named by its position and by the letter printed on
Anbernic, Miyoo, Powkiddy and TrimUI handhelds (Nintendo layout).

| Button | In the game | In menus |
|---|---|---|
| D-pad | move; aim while Lock is held | move the selection |
| Bottom face button (**B**) | jump | confirm |
| Left face button (**Y**) | shoot | |
| Right face button (**A**) | EX shot; with five cards full, your Super Art | back |
| Top face button (**X**) | dash | delete a save (on the save screen) |
| **L1** | switch weapon | |
| **R1** | lock: stand still and aim | |
| **Start** | pause | |
| **Select + Start** | quit the game | |

These are Cuphead's own default controls; **Options > Controls** in the game changes them.

**Button prompts.** The letters the game shows on screen are the ones printed on your handheld,
in the Nintendo layout: **B** bottom, **A** right, **Y** left, **X** top, as on Anbernic, Miyoo,
Powkiddy and TrimUI handhelds. So the prompts say B for jump and confirm. If your handheld has
Xbox letters (A at the bottom), set `CUPHEAD_PROMPTS=xbox` (see [Options](#options)).

Saves and settings are in `ports/cuphead/data/` (the 64-bit build: `ports/cuphead/arm64/data/`).

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
- **The first launch is long** (about 1 to 3 hours, once).
- **The macOS version only** (GOG's or Steam's). The Windows version does not work.
- **Firmwares tested by players:** Knulli, muOS, dArkOS, ROCKNIX (libmali and Adreno). Not tried yet:
  AmberELEC, ROCKNIX with Panfrost. If you try another one, please report how it went.

## If the setup stops

PortMaster shows the setup's message. The most common ones:

| Message | What to do |
|---|---|
| "Put the installed Cuphead.app (GOG macOS) or GOG's macOS installers (.pkg) in ports/cuphead/gamedata" | The setup found no game. Check that the files are directly in `ports/cuphead/gamedata/`. |
| "These are the Windows installers" | Download the macOS installers from GOG instead. |
| "This Cuphead version is not supported" | The setup needs the build of version 1.3.4 (Unity 2017.4.9f1), as GOG's macOS installers have it. |
| "Unpacking the installers needs about 9 GB free on the card" | Make space on the card, then start Cuphead again. |
| "PortMaster's 7-Zip is missing: update PortMaster." | Update PortMaster, then start Cuphead again. |
| "Unpacking the installers and building the 64-bit version need about 12 GB free" | Make space on the card, then start Cuphead again. |
| "The setup's tools (ports/cuphead/setup/) are gone" | The other build's setup removed them: install `cuphead.zip` again, copy your game into `gamedata/` again, start Cuphead. |
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
| `log.txt` | the last time the game ran |
| `patchlog.txt` | the setup |

On Knulli they are in `roms/ports/cuphead/` on the card, or `share/roms/ports/cuphead/` over the
network. **Never attach game files**, and remove anything personal from the logs if you see it.

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
- The port's runtime (`ua_host`) and converter use [libhybris](https://github.com/libhybris/libhybris)
  (Apache 2.0); the runtime links the video decoders libvpx, libvorbis, libogg, libopus and OpenH264.
- The setup runs on PortMaster's Python runtime (`python_3.11`) with [UnityPy](https://github.com/K0lb3/UnityPy)
  and ARM's astc-encoder (`setup/pylibs/`); the code patcher
  uses [Mono.Cecil](https://github.com/jbevain/cecil) and the .NET runtime.

The release zip carries the licence of every part it contains, in `cuphead/licenses/`.
