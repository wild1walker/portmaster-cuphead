# Cuphead (draft PR description for PortMaster-New)

Adds `ports/cuphead` (the folder is `pm/cuphead/` in this repository).

Cuphead runs on Unity's own Android player (Unity 2017.4.9f1, ARMv7), loaded natively on the
handheld's Linux by a small runtime (`ua_host`, from portmaster-unity: an ELF loader with the
Android libraries and Java calls the player needs). No Android, no emulator. The port ships no game
files and nothing from Unity.

## Why the setup converts the game on the handheld

Cuphead is a paid game and Unity's player is Unity's, so neither can be in the zip. The player
brings their GOG macOS copy (the Windows build has only Direct3D shaders), and the first launch
builds the Android version from it through PortMaster's patcher: it unpacks GOG's installers,
downloads Unity's Android player from Unity (checked by SHA-256), converts the shaders to GLES 3,
the textures to ASTC and the settings for the handheld, and packs it all as the player's APK/OBB.
This takes one to a few hours, once, and resumes if interrupted. The installers and the setup's
tools are deleted afterwards.

## What is in `cuphead/`

- `ua_host` (32-bit), `ua_host64` and `launch64.txt`: the runtime, and the 64-bit build for
  firmwares without 32-bit support (its setup also compiles the game's code with Unity's IL2CPP).
- `setup/patchscript`, `setup/questions.lua`: the first-launch setup and its one question
  (texture quality).
- `setup/pmunity/`: the converter (Python; portmaster-unity), run on PortMaster's `python_3.11`
  runtime; `setup/pylibs/`: the Python packages it needs that the runtime lacks (UnityPy, Pillow,
  lz4, astc-encoder, ...; aarch64 builds for CPython 3.11).
- `setup/codepatch`: patches the game's `Assembly-CSharp.dll` for the Android player (Mono.Cecil,
  self-contained .NET); `setup/CupheadPort.dll`: the port's helper (frame-rate and memory fixes);
  `setup/cuphead.json`, `setup/animclips.py`: the converter's settings for Cuphead.
- `setup64/`: the 64-bit setup, the IL2CPP step's scripts, and `lib/` (Debian 11's OpenSSL 1.1,
  which the `python_3.11` runtime links for its HTTPS downloads and most firmwares no longer have).
- `cuphead_hotkey.gptk`: gptokeyb for the exit hotkey only (the game reads the pad through SDL).
- `licenses/`: one `LICENSE.<component>.txt` per bundled component.

Source, build scripts and tests: https://github.com/wild1walker/portmaster-cuphead and
https://github.com/wild1walker/portmaster-unity (both MIT).

## Testing

Anbernic RG34XX-SP (H700), Knulli, 720x480: setup end to end, title and boss fights at about
59-60 fps. Other firmwares, devices and resolutions are not tested yet.
