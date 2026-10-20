# Cuphead on PortMaster

Cuphead runs on Unity's own Android player, run natively on the handheld's 32-bit userland by
[portmaster-unity](https://github.com/wild1walker/portmaster-unity)'s `ua_host`. No game files come
with this port: you provide your own copy, and the first launch builds the game from it on the
handheld.

## What you need

- **Cuphead for macOS, version 1.3.4, from GOG** (the DLC, The Delicious Last Course, is picked up
  when it is there). Either:
  - the installed `Cuphead.app`, copied from a Mac (keep the folder as it is), or
  - GOG's macOS installers (`.pkg`): the game's and, if you have it, the DLC's.
  The Windows version does not work (its shaders are Direct3D only).
- Wi-Fi on the first launch, to download Unity 2017.4.9f1's Android player from Unity (about
  190 MB). Or put `UnitySetup-Android-Support-for-Editor-2017.4.9f1.exe` from Unity's download
  archive into `gamedata/` yourself.
- About 10 GB free on the card during the setup (installers + unpacked game + the converted
  game); about 2.5 GB stay afterwards.
- A handheld with a 32-bit (armhf) userland and GLES 3 (made on an Anbernic RG34XX-SP with Knulli).

## Setting it up

1. Copy `Cuphead.app` or the `.pkg` installers into `ports/cuphead/gamedata/`.
2. Launch Cuphead from the Ports menu. PortMaster's patcher shows the setup's progress: it unpacks
   the game, downloads and unpacks Unity's player, converts the game (GLES 3 shaders, textures
   for the small screen, settings) and packs it for the player. It takes one to a few hours. If it
   is interrupted (battery, power button), launch again: it continues where it stopped. The log is
   `ports/cuphead/patchlog.txt`.
3. When it is done the game starts. The installers in `gamedata/` can be deleted then.

The setup screen first asks for the texture quality: Medium (recommended, what the port is tested
with) or Fast (the textures are encoded about 2.5 times faster, roughly an hour less setup on an H700
device, at slightly lower quality). With an older PortMaster that asks no questions,
`ports/cuphead/setup.txt` sets it (`texture_quality=fast`).

Saves and settings: `ports/cuphead/data/`. Select+Start quits.

## Licences

The port's code is MIT (`licenses/`). The setup tools are removed when the setup is done; their
licences are in `licenses/` too. Cuphead is a trademark of StudioMDHR Entertainment Inc.; Unity is
a trademark of Unity Technologies. This port is not affiliated with either.
