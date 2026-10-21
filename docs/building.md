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
- the setup's Python (`pm/build_pybundle.sh`: python-build-standalone's CPython 3.12 with UnityPy
  and the image libraries; it downloads what is not cached in `pm/cache/` yet);

and copies the converter (`pmunity`), the 64-bit build's launch part and setup (`launch64.sh`,
`setup64/` with portmaster-unity's `tools/il2cpp/` scripts) and every licence into it
(`cuphead/licenses/`: this project's, portmaster-unity's and its THIRD_PARTY.md, libhybris, the
video decoders of both runtimes (`*.arm64.txt`: the 64-bit one's), the .NET runtime, Mono.Cecil,
the setup's Python and its packages). The 32-bit build's files are the same as without the 64-bit
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

Copy `out/device/` to the handheld as `ports/cuphead/` and `pm/Cuphead.sh` into `ports/`. The
folder is marked as set up (`.cuphead_ready`), so the launcher skips the on-device setup.
Re-running `build.sh` continues where it stopped (the conversion keeps a journal in
`out/Cuphead_Data`).

`SHADER_PRECISION=highp ./build.sh ...` samples textures at full precision, as desktop GPUs do
([technical.md](technical.md#options)). `TEXTURE_QUALITY=fast` encodes textures as the setup's
Fast answer does (default `medium`, what the port is tested with). The folder matches what the
on-device setup leaves in `ports/cuphead/` (same packing options, `setup.txt`, `port.json`,
`licenses/`).

## Rules for this repository

- No game files and nothing from Unity: not in commits, not in releases. That includes anything
  made from them (converted data, shader dumps, screenshots, logs of game runs, symbol lists).
  `.gitignore` blocks the usual file types; check `git status` before every commit.
- The defaults must keep the game's look and play. Anything that changes the picture or the game
  play is an option, off by default.
- Code is MIT licensed; every third-party part a release carries has its licence in
  `cuphead/licenses/`.
