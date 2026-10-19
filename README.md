# portmaster-cuphead

Cuphead (with The Delicious Last Course) on ARM Linux handhelds, on Unity's own Android player run
natively by [portmaster-unity](https://github.com/wild1walker/portmaster-unity). Made on an
Anbernic RG34XX-SP (Knulli: Allwinner H700, Mali-G31, 1 GB RAM, 720x480).

**No game files are in this repository, and none are made available anywhere.** You need your own
copy of Cuphead; `build.sh` converts it on your computer.

## Status (RG34XX-SP)

Every level and boss phase the bench covers plays (84 scenes, 169 phase measurements; King Dice's
final fight is not covered by the bench yet); nearly all of the base game's bosses run at 55-60 fps, most
run-and-gun levels and the DLC at 45-60. The slowest parts (measured with the scripted bench,
hot device): Glumstone the Giant on Expert ~33-35, Howling Aces' last phases and Saltbaker's
second phase ~45-49, Treetop Trouble / Perilous Piers ~47-49. The picture and the game play are
the original's: every default either changes nothing on screen or (texture size, for the 1 GB of
RAM) is the conversion's documented setting. What is cropped or switched off is listed under
Options.

## What you need

- **Cuphead 1.3.4 for macOS** (GOG; the DLC is picked up when it is installed). Steam's macOS
  build has the same data but its DLC check and store layer are untested here. Windows builds
  have no shaders the handheld's GPU can use.
- **Unity 2017.4.9f1's Android player**: `UnitySetup-Android-Support-for-Editor-2017.4.9f1.exe`
  from Unity's download archive, unpacked with 7-Zip (`7z x ...`). The folder that contains
  `Variations/` and `Data/` is `PMUNITY_ANDROID_PLAYER` below. It is Unity's software, under
  Unity's terms; use it only as they allow.
- A clone of [portmaster-unity](https://github.com/wild1walker/portmaster-unity) next to this one
  (`--recurse-submodules`), Docker (to build its runtime), Python 3.10+ with its
  `convert/requirements.txt`, the .NET 8 SDK.
- About 6 GB of free disk (much less on APFS, where the copies are clones).

## Build

```sh
export PMUNITY_ANDROID_PLAYER=/path/to/unpacked/android-player
./build.sh "/Applications/Cuphead.app" out      # PYTHON=..., DOTNET=..., PMUNITY=... if not the defaults
```

It builds the code patch set and the helper (below), converts the game's data (shaders to GLES 3,
textures to ASTC at 0.375 scale, settings, audio, the helper's animation table), packs the APK +
OBB for the Android player, builds the runtime and assembles `out/device/`. A full run takes a few
minutes on a recent Mac; re-running continues where it stopped.

## Install

1. Copy `out/device/` to the handheld as `ports/cuphead/` (Knulli: `/userdata/roms/ports/cuphead/`)
   and `port/Cuphead.sh` into `ports/`.
2. Start Cuphead from the Ports menu. Saves live in `ports/cuphead/data/`. Select+Start quits.

The launcher expects PortMaster (`control.txt`, gptokeyb) and the 32-bit libraries in `/lib32` and
`/usr/lib32` (Knulli). Other firmware may keep them elsewhere: adjust `LD_LIBRARY_PATH` there.

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
