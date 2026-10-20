# portmaster-cuphead

Cuphead (with The Delicious Last Course) on ARM Linux handhelds, as a PortMaster port: Unity's own
Android player, run natively by the runtime from
[portmaster-unity](https://github.com/wild1walker/portmaster-unity) (the shared tools; you do not
need it to install). Made on an Anbernic RG34XX-SP (Knulli: Allwinner H700, Mali-G31, 1 GB RAM,
720x480).

**No game files are in this repository or its releases.** You need your own copy of Cuphead: the
port builds the game from it on the handheld the first time you launch it (see Install).

## Status (RG34XX-SP)

Every level and boss phase the bench covers plays (84 scenes, 169 phase measurements; King Dice's
final fight is not covered by the bench yet); nearly all of the base game's bosses run at 55-60 fps, most
run-and-gun levels and the DLC at 45-60. The slowest parts (measured with the scripted bench,
hot device): Glumstone the Giant on Expert ~33-35, Howling Aces' last phases and Saltbaker's
second phase ~45-49, Treetop Trouble / Perilous Piers ~47-49. The picture and the game play are
the original's: every default either changes nothing on screen or (texture size, for the 1 GB of
RAM) is the conversion's documented setting. What is cropped or switched off is listed under
Options.

## Install (PortMaster: the game is built on the handheld)

1. Download `cuphead.zip` from this repository's
   [Releases](https://github.com/wild1walker/portmaster-cuphead/releases) and unzip it into
   the handheld's `ports/` folder (it holds `Cuphead.sh` and `cuphead/`). That is everything the
   port needs: the runtime, the converter and the setup's tools are inside.
2. Put your copy of Cuphead into `ports/cuphead/gamedata/`: either the installed `Cuphead.app`
   (GOG, macOS, 1.3.4; with The Delicious Last Course if you have it), copied from a Mac, or GOG's
   macOS installers (`.pkg`: the game's and the DLC's). Windows versions do not work (Direct3D
   shaders only).
3. Launch Cuphead from the Ports menu. On the first launch PortMaster's patcher runs the setup
   (`pm/cuphead/setup/patchscript`): it unpacks the game (GOG's installers with
   `pmunity macpkg`), downloads Unity 2017.4.9f1's Android player from Unity (about 190 MB, Wi-Fi;
   or put `UnitySetup-Android-Support-for-Editor-2017.4.9f1.exe` from Unity's download archive in
   `gamedata/`), converts the game with portmaster-unity's converter, patches its code, packs it for
   the player and installs it. It first asks for the texture quality: Medium (recommended) or Fast
   (about 2.5 times faster to encode, slightly lower quality; `setup.txt` when PortMaster asks no
   questions). It takes one
   to a few hours, once, needs about 10 GB free on the
   card while it runs (2.5 GB stay), and continues where it stopped if it is interrupted. Log:
   `ports/cuphead/patchlog.txt`.

Saves live in `ports/cuphead/data/`. Select+Start quits. The launcher expects PortMaster
(`control.txt`, gptokeyb, the patcher, 7-Zip) and the 32-bit libraries in `/lib32` and `/usr/lib32`
(Knulli).

## Building the port

Only needed to change the port: installing uses the release. `package.sh` makes `cuphead.zip`: it builds the helper (against your copy's assemblies; none of
them is copied), a self-contained linux-arm64 build of the code patcher, portmaster-unity's runtime
(Docker), the setup's Python (`pm/build_pybundle.sh`: python-build-standalone's CPython 3.12 with
UnityPy and the image libraries; downloads what is not cached yet), and copies the converter and
every licence into it.

```sh
GAME_MANAGED=/Applications/Cuphead.app/Contents/Resources/Data/Managed ./package.sh out/pm
```

Building needs a clone of [portmaster-unity](https://github.com/wild1walker/portmaster-unity)
(the shared tools: runtime and converter) next to this one (`--recurse-submodules`), Docker,
Python 3 with pip, the .NET 8 SDK.

## Building on a computer instead

`build.sh` does the whole conversion on a PC (minutes instead of hours) and assembles the folder for
`ports/cuphead/` directly; it needs Unity's Android player unpacked there
(`PMUNITY_ANDROID_PLAYER`, from the installer above with 7-Zip) and Python with
portmaster-unity's `convert/requirements.txt`:

```sh
export PMUNITY_ANDROID_PLAYER=/path/to/unpacked/android-player
./build.sh "/Applications/Cuphead.app" out      # -> out/device/ = ports/cuphead/; PYTHON=, DOTNET=, PMUNITY=
```

Copy `out/device/` as `ports/cuphead/` and `pm/Cuphead.sh` into `ports/` (the folder is marked
as set up, so the launcher skips the on-device setup).

## What the port changes

`codepatch/` (Mono.Cecil, `--target=android`) patches `Assembly-CSharp.dll` from the original:

| patch | what |
|---|---|
| `rewired-android-input` | Rewired reads the pad through Unity's Android input |
| `gog-offline`, `dlc-from-gog-install` | no GOG Galaxy connection; the DLC is detected from the GOG install's `.info` file |
| `fonts-lazy` | fonts load when first used (memory) |
| `diagnostics`, `bench-level-hook` | the helper's per-frame hook; test hooks (inactive unless `CUPHEAD_TEST_*` is set) |
| `aspect-crop` | the 3:2 screen shows the 16:9 picture cropped at the sides instead of letterboxed (`CUPHEAD_ASPECT=16:9` for the letterbox) |
| `glow-camera` | the title cards' glow camera renders only while a glow is shown |
| `bluroptimized-idle`, `geese-track` | Glumstone's geese layer is not blurred while it is empty (blurring nothing gives nothing) |
| `atlas-trim-reload` | atlases trimmed in a fight are loaded again before a retry |

`helper/` is `CupheadPort.dll`, loaded by the patched game. Defaults that never change the picture:
render textures no taller than the screen (`CUPHEAD_RTFIT`), sprites with huge meshes drawn as
quads (`CUPHEAD_HEAVYSPRITES`), decoration far off screen switched off (`CUPHEAD_DECOR`), cameras
that draw nothing skipped (`CUPHEAD_CAMTRIM`), finished boss phases' atlases unloaded in
Saltbaker's and Glumstone's fights (`CUPHEAD_ATLASTRIM`), shader, font and code warm-up during
loads, memory trims after loads. Each has its switch (`=0` turns it off; see the source).

Options (environment, e.g. in `Cuphead.sh`):

| variable | effect |
|---|---|
| `CUPHEAD_ASPECT=16:9` | letterbox instead of the side crop |
| `CUPHEAD_PROMPTS=xbox` | Xbox button letters (default: the handheld's Nintendo layout) |
| `CUPHEAD_RUMBLE=0` / `=game` | no rumble / the game's own rumble. Default: the game's hit rumble as short SDL haptic pulses that end by themselves (the RG34XX-SP's motor did not stop on the game's stop call when the port ran under box64; not re-checked on this route yet) |
| `CUPHEAD_FX=half` / `sharp` | cheaper film blur (changes the picture slightly; default `game`) |

At build time, `SHADER_PRECISION=highp ./build.sh ...` samples textures at full precision as a
desktop GPU does. The default keeps GLSL ES's default sampler precision, which is also what Unity's
own GLES compiler gives Cuphead's samplers: on the RG34XX-SP that is up to a few percent faster,
and the difference is at most 2 of 255 levels in about a tenth of the pixels (measured on
deterministic replays: not visible).

## Tests and measurements

`bench/` runs on the handheld (with the port's test hooks): `bench_all.sh` plays every level and
boss phase unattended (god mode, scripted pad) in groups, `benchsum.py` sums up the logs per level
and phase, `ab_device.sh` + `absum.py` alternate two configurations so both see the same thermal
state, `devshots.sh` takes deterministic screenshots of several builds for pixel comparisons.
portmaster-unity's `tools/` has the generic profiling (`UA_PROF_EVERY`, `hitchsum.py`, `profcpu.py`).

## Licence

MIT (see [LICENSE](LICENSE)) for the code here. Cuphead is a trademark of StudioMDHR
Entertainment Inc.; this project is not affiliated with or endorsed by StudioMDHR or Unity
Technologies. The code patches and the helper operate on your own copy of the game and contain
none of its code or assets.
