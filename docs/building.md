# Building

Players do not need any of this: installing uses the release zip ([README](../README.md#install)).
This page is for changing the port, or for converting the game on a computer instead of on the
handheld.

Both ways need a checkout of portmaster-unity (the shared runtime and converter; not public yet)
next to this one, cloned with `--recurse-submodules`, or named with `PMUNITY=<checkout>`.

## The release zip (`package.sh`)

`package.sh` makes `cuphead.zip`, the PortMaster port whose first launch builds the game on the
handheld:

```sh
GAME_MANAGED=<Cuphead.app>/Contents/Resources/Data/Managed ./package.sh out/pm
```

It builds:

- the helper `CupheadPort.dll`, compiled against your copy's assemblies (`GAME_MANAGED`; none of
  them is copied into the zip);
- a self-contained linux-arm64 build of the code patcher (`codepatch`);
- portmaster-unity's runtime (`ua_host`, in Docker) if it is not built yet, and its 64-bit build
  for the [64-bit build](technical.md#the-64-bit-build) (`ARCH=arm64 runtime/build.sh`:
  `runtime/host/out/arm64/ua_host`, shipped as `cuphead/ua_host64`);
- the Python packages the setup adds to PortMaster's `python_3.11` runtime (`pm/build_pylibs.sh`:
  UnityPy and the image libraries for CPython 3.11, aarch64; it downloads what is not cached in
  `pm/cache/wheels311/` yet), and Debian 11's OpenSSL 1.1 for the 64-bit setup's downloads;

and copies the converter (`pmunity`), the 64-bit build's launch part and setup (`launch64.sh`,
`setup64/` with portmaster-unity's `tools/il2cpp/` scripts) and every licence into it
(`cuphead/licenses/`, one `LICENSE.<component>.txt` each: this project's, portmaster-unity's
with its THIRD_PARTY.md, libhybris, the video decoders of both runtimes, the .NET runtime,
Mono.Cecil, OpenSSL and each Python package). Both runtimes are built with `STEAMSTUB=0`
(portmaster-unity's Steamworks stand-ins left out: Cuphead's GOG build has no Steamworks), and
package.sh rebuilds them that way when they are not. The 32-bit build's files are the same as without the 64-bit
build; only `Cuphead.sh` (the route choice), `port.json` and the READMEs changed with it.

Needs: Docker, Python 3 with pip, curl, the .NET 8 SDK (`DOTNET=` if `dotnet` is not on the PATH).

## Converting on a computer instead (`build.sh`)

`build.sh` does the whole conversion on a PC or Mac (minutes instead of hours) and assembles the
folder for `ports/cuphead/` directly. Nothing is downloaded and nothing leaves your computer. It
needs:

- Unity 2017.4.9f1's Android player, unpacked: `UnitySetup-Android-Support-for-Editor-2017.4.9f1.exe`
  from Unity's download archive, unpacked with 7-Zip (`PMUNITY_ANDROID_PLAYER`: the folder with
  `Variations/` and `Data/`);
- Python with portmaster-unity's `convert/requirements.txt` (`PYTHON=`);
- the .NET SDK (`DOTNET=`), and Docker if portmaster-unity's runtime is not built yet.

```sh
export PMUNITY_ANDROID_PLAYER=/path/to/unpacked/android-player
./build.sh "<Cuphead.app>" out      # -> out/device/ = ports/cuphead/; PYTHON=, DOTNET=, PMUNITY=
```

Copy `out/device/` to the handheld as `ports/cuphead/` and `pm/cuphead/Cuphead.sh` into `ports/`. The
folder is marked as set up (`.cuphead_ready`), so the launcher skips the on-device setup.
Re-running `build.sh` continues where it stopped (the conversion keeps a journal in
`out/Cuphead_Data`).

`SHADER_PRECISION=highp ./build.sh ...` samples textures at full precision, as desktop GPUs do
([technical.md](technical.md#options)). `TEXTURE_QUALITY=fast` encodes textures as the setup's
Fast answer does (default `medium`, what the port is tested with). The folder matches what the
on-device setup leaves in `ports/cuphead/` (same packing options, `port.json`,
`licenses/`).

## The 64-bit build (`build64.sh`, draft)

For handhelds without a 32-bit userland: the same conversion, with the game's code compiled by
Unity's IL2CPP for Unity's 64-bit IL2CPP player (2017.4.40f1's: Cuphead's own 2017.4.9f1 has no
64-bit player) instead of shipped for Mono. Needs portmaster-unity's IL2CPP support for Unity
2017.4 (`pmunity il2cpp`, which fetches Unity's il2cpp, the NDK's target files and the player
into `PMUNITY_IL2CPP_HOME` on first use) and its retargeting of data to another release
(`android-data --data-version`); about an hour of CPU.

```sh
PMUNITY_IL2CPP_HOME=/path/to/il2cpp-tools ./build64.sh "<Cuphead.app>" out64   # -> out64/device64/
```

[il2cpp.md](il2cpp.md) has what changes for Cuphead's code under IL2CPP and what is not tried yet.

## Rules for this repository

- No game files and nothing from Unity: not in commits, not in releases. That includes anything
  made from them (converted data, shader dumps, screenshots, logs of game runs, symbol lists).
  `.gitignore` blocks the usual file types; check `git status` before every commit.
- The defaults must keep the game's look and play. Anything that changes the picture or the game
  play is an option, off by default.
- Code is MIT licensed; every third-party part a release carries has its licence in
  `cuphead/licenses/`.
