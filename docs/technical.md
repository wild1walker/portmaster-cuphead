# How the port works

Cuphead (Unity 2017.4.9f1, Mono) runs on Unity's own ARMv7 Android player. The port's runtime
(`ua_host`) loads the player's libraries (`libmain.so`, `libunity.so`, `libmono.so`) on the
handheld's 32-bit (armhf) Linux userland, plays the Java side of Unity's Android activity in C and stands in for the Android APIs
the player uses. No Android, no emulator, no x86 translation. Developed on an Anbernic RG34XX-SP
(Knulli: Allwinner H700, Mali-G31, 1 GB RAM, 720x480).

The player's guide is the [README](../README.md); building is in [building.md](building.md).

## The release and the setup

The release zip (`cuphead.zip`, made by `package.sh`) holds `Cuphead.sh` (the launcher) and
`cuphead/`: `ua_host`, PortMaster's exit hotkey file, `port.json`, the setup in `setup/` and every
licence in `licenses/`. No game files and nothing from Unity.

On the first launch `Cuphead.sh` runs `setup/patchscript` through PortMaster's patcher
(`PATCHER_FILE`, `PATCHER_QUESTIONS`; a failure is a `PATCH_FAIL_MSG:` line the patcher shows):

1. **Python:** unpacks the setup's Python (`python.tar.gz`: python-build-standalone's CPython 3.12
   with UnityPy, lz4, astc-encoder and Pillow, aarch64).
2. **The game:** finds `Cuphead.app` in `gamedata/` (or one folder deeper), or unpacks GOG's macOS
   installers with `pmunity macpkg` (largest first: the game, then the DLC; about 9 GB free
   needed); refuses the Windows installers and any build that is not Unity 2017.4.9f1; keeps the
   GOG `.info` files (the DLC check reads them) and Mono's `etc` folder.
3. **Unity's player:** `UnitySetup-Android-Support-for-Editor-2017.4.9f1.exe` from `gamedata/`, or
   downloaded from Unity (`download.unity3d.com`, about 190 MB) and checked against its SHA-256;
   the player's Mono release libraries, its Android `Managed` assemblies and its
   `unity default resources` are extracted with PortMaster's 7-Zip.
4. **Conversion:** `pmunity convert` with `convert/cuphead.json` and the `animclips.py` plugin:
   GLES 3 shaders, ASTC textures (quality `medium` or `fast`: the patcher's question, else
   `setup.txt`, else medium; the first answer is kept in `gamedata/.setup/.texture_quality`),
   settings and audio, and the helper's animation table.
5. **Packing:** `pmunity android-data` writes the APK and OBB the player reads, with the code
   patcher run on the original `Assembly-CSharp.dll` (`codepatch --target=android`), the helper
   `CupheadPort.dll`, Mono's config and the dllmap for SDL2.
6. **Installing:** the player's three libraries go into `lib/`, the APK, OBB and their files into
   `ports/cuphead/`; the work folder, the game's unpacked data and `setup/` are removed and
   `.cuphead_ready` marks the setup done.

Every step leaves a marker in `gamedata/.setup/`, so an interrupted setup continues where it
stopped. Log: `ports/cuphead/patchlog.txt`.

At every launch `Cuphead.sh` presents the built-in pad to the game as an Android gamepad named
`Xbox Wireless Controller` (`UA_PAD_NAME`; Rewired knows Android pads by name), uses ALSA for
sound (PCM piped into `pw-cat`/`pacat` when a sound server holds the card), writes a line of frame
statistics every 10 s to `log.txt` (`UA_FPS`), runs gptokeyb only for the exit hotkey
(Select+Start) and puts `/lib32` and `/usr/lib32` first in the library path (Knulli).

## Status (RG34XX-SP)

Every level and boss phase the bench covers plays (84 scenes, 169 phase measurements; King Dice's
final fight is not covered by the bench yet). Nearly all of the base game's bosses run at 55-60
fps, most run-and-gun levels and the DLC at 45-60. The slowest parts (measured with the scripted
bench on a warm device): Glumstone the Giant on Expert ~33-35, Howling Aces' last phases ~41-49,
Saltbaker's second phase ~45-49, Treetop Trouble / Perilous Piers ~47-49.

The picture and the game play are the original's: every default either changes nothing on screen
or (texture size, for the 1 GB of RAM) is the conversion's documented setting. What is cropped or
switched off is listed under [Options](#options).

Under sustained load the H700 throttles (CPU and GPU clocks capped by its thermal limits), so long
sessions run slower than cool runs. Compare measurements only in the same thermal state.

## What the port changes

`codepatch/` (Mono.Cecil, `--target=android`) patches `Assembly-CSharp.dll` from the original:

| patch | what |
|---|---|
| `rewired-android-input` | Rewired reads the pad through Unity's Android input |
| `pad-prompts-android` | the prompts show the letters printed on the handheld: Rewired's Xbox controller names the face buttons by their Xbox position, and the helper's `Pad.Letters` gives the Nintendo layout's letters (B bottom, A right, Y left, X top) unless `CUPHEAD_PROMPTS=xbox`; the buttons' actions do not move |
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

## Options

Set in the environment, e.g. as `export` lines in `Cuphead.sh` (after `get_controls`):

| variable | effect |
|---|---|
| `CUPHEAD_ASPECT=16:9` | letterbox instead of the side crop |
| `CUPHEAD_PROMPTS=xbox` | Xbox button letters, for Xbox-labelled handhelds (default: the letters printed on Anbernic, Miyoo, Powkiddy and TrimUI handhelds, Nintendo layout: B bottom, A right, Y left, X top). The buttons themselves are positional either way: the bottom face button is jump/confirm, as on an Xbox pad |
| `CUPHEAD_RUMBLE=0` / `=game` | no rumble / the game's own rumble. Default: the game's hit rumble as short SDL haptic pulses that end by themselves (the RG34XX-SP's motor did not stop on the game's stop call when the port ran under box64; not re-checked on this route yet) |
| `CUPHEAD_FX=half` / `sharp` | cheaper film blur (changes the picture slightly; default `game`) |
| `CUPHEAD_RTFIT`, `CUPHEAD_HEAVYSPRITES`, `CUPHEAD_DECOR`, `CUPHEAD_CAMTRIM`, `CUPHEAD_ATLASTRIM` `=0` | turn off one of the helper's defaults above (for comparisons) |
| `UA_FPS=<seconds>` | how often `log.txt` gets a line of frame statistics (launcher default 10) |
| `UA_PAD_NAME` | the pad name the game sees (launcher default `Xbox Wireless Controller`) |

The runtime has more `UA_*` switches of its own (its documentation, docs/runtime.md, is in its
repository, which is not public yet).

At build time, `SHADER_PRECISION=highp ./build.sh ...` samples textures at full precision as a
desktop GPU does. The default keeps GLSL ES's default sampler precision, which is also what Unity's
own GLES compiler gives Cuphead's samplers: on the RG34XX-SP that is up to a few percent faster,
and the difference is at most 2 of 255 levels in about a tenth of the pixels (measured on
deterministic replays: not visible).

## Tests and measurements

`bench/` runs on the handheld (with the port's test hooks; [bench/README.md](../bench/README.md)):
`bench_all.sh` plays every level and boss phase unattended (god mode, scripted pad) in groups,
`benchsum.py` sums up the logs per level and phase, `ab_device.sh` + `absum.py` alternate two
configurations so both see the same thermal state, `devshots.sh` takes deterministic screenshots
of several builds for pixel comparisons. portmaster-unity's `tools/` has the generic profiling
(`UA_PROF_EVERY`, `hitchsum.py`, `profcpu.py`).
