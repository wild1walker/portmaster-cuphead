# How the port works

Cuphead (Unity 2017.4.9f1, Mono) runs on Unity's own ARMv7 Android player. The port's runtime
(`ua_host`) loads the player's libraries (`libmain.so`, `libunity.so`, `libmono.so`) on the
handheld's 32-bit (armhf) Linux userland, plays the Java side of Unity's Android activity in C and stands in for the Android APIs
the player uses. No Android, no emulator, no x86 translation. Developed on an Anbernic RG34XX-SP
(Knulli: Allwinner H700, Mali-G31, 1 GB RAM, 720x480).

Firmwares without a 32-bit userland get a [64-bit build](#the-64-bit-build) instead: the same
data and patches, the game's code compiled with IL2CPP for Unity's 64-bit (arm64-v8a) player.

The player's guide is the [README](../README.md); building is in [building.md](building.md).

## The release and the setup

The release zip (`cuphead.zip`, made by `package.sh`) holds `Cuphead.sh` (the launcher) and
`cuphead/`: `ua_host`, PortMaster's exit hotkey file, `port.json`, `gameinfo.xml`, the screenshot
and cover, the setup in `setup/` and every licence in `licenses/` (one `LICENSE.<component>.txt`
each); for the [64-bit build](#the-64-bit-build) also `ua_host64`, `launch64.sh`
and `setup64/`. No game files and nothing from Unity.

On the first launch `Cuphead.sh` runs `setup/patchscript` through PortMaster's patcher
(`PATCHER_FILE`, `PATCHER_QUESTIONS`; a failure is a `PATCH_FAIL_MSG:` line the patcher shows):

1. **Python:** PortMaster's `python_3.11` runtime (`attr.runtime` in `port.json`; `Cuphead.sh`
   mounts it at `$HOME/python_3.11` for the setup) with the packages it lacks in `setup/pylibs/`
   (UnityPy, lz4, astc-encoder, Pillow and their helpers, aarch64, built by `pm/build_pylibs.sh`).
2. **The game:** finds `Cuphead.app` in `gamedata/` (or one folder deeper), or unpacks GOG's macOS
   installers with `pmunity macpkg` (largest first: the game, then the DLC; about 9 GB free
   needed); refuses any build that is not Unity 2017.4.9f1; keeps the
   GOG `.info` files (the DLC check reads them) and Mono's `etc` folder.
3. **Unity's player:** `UnitySetup-Android-Support-for-Editor-2017.4.9f1.exe` from `gamedata/`, or
   downloaded from Unity (`download.unity3d.com`, about 190 MB) and checked against its SHA-256;
   the player's Mono release libraries, its Android `Managed` assemblies and its
   `unity default resources` are extracted with PortMaster's 7-Zip.
4. **Conversion:** `pmunity convert` with `convert/cuphead.json` and the `animclips.py` plugin:
   GLES 3 shaders, ASTC textures (quality `medium` or `fast`: the patcher's question, else
   medium; the first answer is kept in `gamedata/.setup/.texture_quality`),
   settings and audio, and the helper's animation table. The shader settings keep the output the
   handheld was tested with: `glcore4x` off, and `compute` off (Cuphead's one compute shader, the
   post-processing stack's eye-adaptation histogram, keeps no OpenGL ES kernel, so the player
   stays at Android 4.4's level and OpenGL ES 3.0, as tested).
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
sound (PCM piped into `pw-cat`/`pacat` when a sound server holds the card), runs gptokeyb only for the exit hotkey
(Select+Start) and puts `/lib32` and `/usr/lib32` first in the library path (Knulli).

## The 64-bit build

Unity's 64-bit ARM Android players exist only for IL2CPP, and Unity 2017.4 has them from
2017.4.16f1; Cuphead is 2017.4.9f1. A firmware without the 32-bit loader therefore runs Cuphead on
the IL2CPP player of the line's last release, 2017.4.40f1 (a line keeps its serialized layouts),
with the runtime's aarch64 build (`ua_host64`, portmaster-unity's `runtime/host/out/arm64/ua_host`;
portmaster-unity is not public yet). The game's code gets there in one of two ways:

- **The Mono route** (the default): Cuphead's own assemblies, JIT-compiled at run time by Unity's
  Mono of the 2017.4 line (MonoBleedingEdge, Mono 5.0.1, built for aarch64 Linux), behind a bridge
  the player loads as its `libil2cpp.so` (portmaster-unity's `docs/mono-arm64.md`). Cuphead is a
  .NET 3.5 game, whose Mono (2.6) has no 64-bit ARM code generator, so its assemblies run on Mono's
  4.x class libraries (the `unityjit` profile) instead of its own 2.0 ones. The runtime, the bridge
  and those class libraries are built from Unity's MIT-licensed Mono sources and ship in the zip
  (`setup64/mono64/`, about 6 MB of the zip, 15 MB unpacked): nothing is compiled on the handheld and
  nothing but Unity's player is downloaded.
- **IL2CPP** (`CUPHEAD_64_IL2CPP=1` at the first launch): the code compiled ahead of time by
  2017.4.40f1's IL2CPP on the handheld (an hour, 1.2 GB of downloads), or by `build64.sh` on a PC.

What differs on the Mono route, measured in the arm64 rig against the IL2CPP build (which runs the
game's 2.0 class libraries) and the 32-bit build's logs: title, slot select, bosses, a run-and-gun
level and the win screen without a managed exception; deterministic replays (`CUPHEAD_TEST_DETERMINISTIC`)
of Veggies with the IL2CPP build's frames (identical, or 74 - 105 of 345,600 pixels off by 1/255:
float rounding); saves of the 32-bit build load, and the
saves it writes are the same bytes except in one place: Dialoguer's global variables (XML from
`XmlSerializer`, a string in the save) name their two XML namespaces in the other order
(`xmlns:xsd` before `xmlns:xsi`: Mono 5's `XmlSerializer` is .NET's reference source, which keeps
them in a hash table, and Mono's string hash orders them so), which both builds read the same way.

**Which build** (`Cuphead.sh`, decided once, before anything else is written to the log):

| system | build |
|---|---|
| `/lib/ld-linux-armhf.so.3` exists | the 32-bit build, exactly as before (its own lines of `Cuphead.sh`, unchanged) |
| no 32-bit loader | the 64-bit build: `Cuphead.sh` sources `cuphead/launch64.sh`, which runs its own setup and the game |
| `CUPHEAD_ROUTE=64` | the 64-bit build on a system that has both (a test switch, e.g. on the RG34XX-SP) |
| anything else | a message: the firmware needs 32-bit (armhf) support |

**Its own files**, so a card moved between a 32-bit and a 64-bit-only handheld never mixes the
two: `ua_host64`, `setup64/` (work folder `gamedata/.setup64/`), the marker `.cuphead_ready64`,
and `arm64/`, the root `ua_host64` runs: `lib/` (`libmain.so`, `libunity.so`, `libil2cpp.so`), the
APK and `android_package.json`, the GOG `.info` files beside the APK (the DLC check), `data/`
(saves; the 32-bit build's `data/` without its shader cache is copied there once, when the card
has one). The setup's tools stay in `setup/` (shared with the 32-bit setup, unchanged); the 64-bit
setup removes `setup/` and `setup64/` when it is done, but keeps `setup/` on a system that can still
run the 32-bit build when that one is not set up yet. At run time: no `/lib32` library path, SDL's
own audio driver choice (a 64-bit program reaches the sound server directly), the same pad name,
PortMaster's pad mapping, the exit hotkey for `ua_host64`.

**The setup** (`setup64/patchscript`; markers in `gamedata/.setup64/`, PortMaster's
`PATCH_FAIL_MSG:` lines, log `patchlog.txt`). First PortMaster's Python, as in the 32-bit setup (and
`setup64/lib/`: Debian 11's OpenSSL 1.1, which that Python links for its downloads),
and the converter copied into the work folder in portmaster-unity's own layout
(`converter/convert/pmunity` beside `converter/tools/il2cpp`, where the IL2CPP step finds its
scripts: package.sh copies portmaster-unity's `tools/il2cpp/` into `setup64/il2cpp/`). Then:

1. **The game** as in the 32-bit setup (`Cuphead.app` or the GOG installers; 12 GB free for the
   installers, which stay until the end, plus the IL2CPP build).
2. **Unity's player:** `pmunity player fetch 2017.4.40f1 --backend il2cpp --abi arm64-v8a`, with
   `--managed` on the Mono route (its UnityEngine assemblies) (about 7 MB read from Unity's
   installer with range requests; the whole installer as a fallback, or
   `UnitySetup-Android-Support-for-Editor-2017.4.40f1.exe` in `gamedata/`); `libmain.so` and
   `libunity.so` checked against their SHA-256.
3. **Conversion:** exactly the 32-bit setup's (`convert/cuphead.json`, `animclips.py`, the texture
   question).
4. **The code:** `codepatch --target=android` on the original `Assembly-CSharp.dll` (the data's
   `Managed/` stays as it is) and `CupheadPort.dll`, kept in the work folder (`managed/`), as the
   32-bit setup applies them; the patches are IL patches of the assembly IL2CPP compiles, so they
   come first.
5. **IL2CPP on the handheld** (`CUPHEAD_64_IL2CPP=1` only; the Mono route skips it): `pmunity il2cpp fetch 2017.4.40f1 --on-device --abi arm64-v8a --net 2
   --pkg <player> --home <dir>` (the toolchain), then `pmunity il2cpp <data> <out> --on-device
   --abi arm64-v8a --unity 2017.4.40f1 --pkg <player> --home <dir> --managed-file
   <managed>/Assembly-CSharp.dll --managed-file <managed>/CupheadPort.dll [--link-xml
   setup64/link.xml]` (`<out>/lib/arm64-v8a/libil2cpp.so`, `<out>/Data/Managed/...`,
   `<out>/il2cpp.json`). `--unity`, `--net` and `--managed-file` come from portmaster-unity's 2017.4
   IL2CPP work, `--on-device` (run natively, no Docker; jobs from the free memory; resumable) from
   its on-device work, `link.xml` (what Cuphead's code reaches by reflection) from this repository's
   IL2CPP work ([il2cpp.md](il2cpp.md)). A converter whose `il2cpp --help` lacks `--on-device` or
   `--managed-file` stops the setup here with a message pointing to `build64.sh`. Free space
   checked before: 3 GB (an estimate until measured on a handheld).
6. **Packing:** on the Mono route `pmunity android-data --mono64` with the patched
   `Assembly-CSharp.dll`, `CupheadPort.dll` and `setup64/mono64/classlibs/*.dll` as
   `--managed-file`s (the class libraries replace the game's 2.0 ones of the same names), Mono's
   Linux config (`--mono-etc setup64/mono64/etc`) and the 32-bit packing's `--dllmap
   SDL2=libSDL2-2.0.so.0`; with IL2CPP `pmunity android-data --il2cpp <out>` (without the helper,
   Mono's config and dllmap: IL2CPP's output holds the code, and IL2CPP reads no dllmap). Both with
   ua_host's plugin map (`setup64/plugins-il2cpp.json`) and the 32-bit packing's name, package and
   options. android-data rewrites the 2017.4.9f1 data for the 2017.4.40f1 player.
7. **Installing** into `arm64/` (`lib/`: the player's `libmain.so` and `libunity.so`, and on the
   Mono route `setup64/mono64/`'s `libil2cpp.so` (the bridge), `libmonobdwgc-2.0.so` and
   `libMonoPosixHelper.so`), `.cuphead_ready64`, then the tools and the installers are removed.

At run time `launch64.sh` sets `CUPHEAD_CODEWARM=0` for an IL2CPP install: the helper's code
warm-up only helps Mono's JIT (on the Mono route it stays on, as in the 32-bit build).

`build64.sh` makes the same build on a PC or Mac (IL2CPP in Docker there), for handhelds too weak
to compile the code ([building.md](building.md)); its folder is what this setup leaves in
`ports/cuphead/` (`ua_host64`, `launch64.sh`, `arm64/`, `.cuphead_ready64`).

Rehearsed in an aarch64 container (a handheld's busybox tools, a trimmed copy of the game): every
step up to IL2CPP, the stop with its message there, and, with a stand-in for the IL2CPP build, the
packing and the install. Not run yet: the IL2CPP build itself (portmaster-unity's 2017.4 and
on-device IL2CPP work), anything of the 64-bit build on a handheld, and IL2CPP data over 2 GiB
(Cuphead's is about 2.3 GB: APK plus OBB; see portmaster-unity's IL2CPP notes).

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
| `UA_FPS=<seconds>` | a line of frame statistics in `log.txt` every that many seconds (off by default) |
| `UA_PAD_NAME` | the pad name the game sees (launcher default `Xbox Wireless Controller`) |
| `CUPHEAD_ROUTE=64` | a test switch: the [64-bit build](#the-64-bit-build) on a system that can run the 32-bit one (set it above the launcher's route choice, e.g. as an `export` line right after `get_controls`) |

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
