# The 64-bit build (IL2CPP; in progress)

The port runs Cuphead's code on Unity's 32-bit ARM Android player with Mono, which needs a 32-bit
userland on the handheld. Unity's 64-bit ARM Android players exist only for IL2CPP, so for
64-bit-only firmwares the code is compiled ahead of time: [build64.sh](../build64.sh) (a draft, for
a PC or Mac) does what [build.sh](../build.sh) does, with portmaster-unity's `pmunity il2cpp` between
the code patches and the packing:

```
codepatch --target=android (Assembly-CSharp)   the same patches as the 32-bit port
helper (CupheadPort.dll)                        the same helper
[tools/asmpatch]                                not needed: the codepatch sets Rewired's platform
pmunity il2cpp                                  all of the game's assemblies -> libil2cpp.so + metadata
pmunity android-data --il2cpp                   APK + OBB with IL2CPP's metadata instead of assemblies
```

The patches must be applied before IL2CPP (they are IL patches of the assembly IL2CPP compiles),
from the original Assembly-CSharp, as for the 32-bit build.

## Unity 2017.4.40f1 instead of 2017.4.9f1

Cuphead is Unity 2017.4.9f1, which has no 64-bit player: arm64-v8a IL2CPP players start at
2017.4.16f1. build64.sh uses 2017.4.40f1, the line's last release, for IL2CPP and the player.
That player refuses data of another release ("Invalid serialized file version ... Expected
version: 2017.4.40f1. Actual version: 2017.4.9f1", then "Unable to initialize the Unity Engine"),
so portmaster-unity's android-data rewrites the data for it (`--data-version player`, its
default): the version string every serialized file carries, and PlayerSettings, whose layout
changed between the two releases. Bundles carry type trees and load as they are. With that,
Cuphead's 2017.4.9f1 data reaches its title screen on the 2017.4.40f1 player (Mono, 32-bit, in
portmaster-unity's emulated rig; the same data as the 32-bit build).

## What in Cuphead's code meets IL2CPP

From a reading of the assemblies' metadata and IL (Cuphead 1.3.4: Assembly-CSharp,
Assembly-CSharp-firstpass, Rewired_Core, GalaxyCSharp, Unity's packages; the patched
Assembly-CSharp; CupheadPort.dll):

- **References.** Every type, method and field the game's, Rewired's and the helper's code
  references exists in the IL2CPP build's assembly set (2017.4.40f1's IL2CPP UnityEngine modules,
  the ".NET 2.0 Subset" class libraries Cuphead ships, Mono/lib/mono/unity): IL2CPP resolves all of
  them at build time, where Mono only did when a method first ran. The patched Assembly-CSharp
  references CupheadPort 0.0.0.0, the helper is 1.0.0.0: the tools resolve by name, as Mono did.
- **No run-time code generation**: no System.Reflection.Emit, DynamicMethod or compiled
  expression trees anywhere; this profile's XmlSerializer has no serializer generation either.
- **Reflection** (kept by [il2cpp/link.xml](../il2cpp/link.xml), which 2017.4.40f1's UnityLinker
  takes as `--include-link-xml`; with it, Cuphead's assembly set links: 61 assemblies, the game's
  kept whole, all of System.Xml.Serialization's 121 types, of which 37 go without it. That linker
  ignores a link.xml with a comment before `<linker>`): Unity's own lookups by name
  (MonoBehaviours, SendMessage/Invoke/StartCoroutine strings, JsonUtility for the save files);
  the helper's reads of the game's private members; Rewired's initializers by name; and the one
  class-library part reached through reflection, **XmlSerializer**: Dialoguer stores its global
  variables (lists of bool, float, string) as XML in every save and reads them back from it. The
  List<bool>/List<float> code it needs is compiled (the game itself uses those lists).
- **Generic code made at run time**: the helper's `loadAllAssetsSynchronous<Object>` through
  MakeGenericMethod (reference type: IL2CPP's shared code for the game's `<Texture2D>` serves
  it); Rewired's TinyJson `MakeGenericType` belongs to UserDataStore_PlayerPrefs (Cuphead saves
  its controller maps with its own UserDataStore_Cuphead, as XML). Generic virtual methods with
  value-type arguments: none in Cuphead's code; Rewired's own (GetMaps<T>) are built for IL2CPP
  (Rewired supports IL2CPP).
- **Native calls.** IL2CPP has no Mono dllmap: the helper's `DllImport("SDL2")` (the rumble
  pulses) goes through ua_host's plugin map ([convert/plugins-il2cpp.json](../convert/plugins-il2cpp.json):
  the system's libSDL2-2.0.so.0). `DllImport("libc")` (malloc_trim after loads) becomes
  `libc.so` under IL2CPP's name rules (name, name.so, libname.so, ...), which is Android's libc
  for ua_host: if that has no malloc_trim the helper logs "memory trim unavailable" and goes on
  (RSS somewhat higher). GOG Galaxy, Steamworks and the Switch/PlayStation paths are never called
  (DllNotFoundException at a first call, as on the 32-bit build). Marshal use (Rewired's native
  input structures, the helper's strings) is supported by IL2CPP.
- **The helper's code warm-up** (`CodeWarmup`: loads or compiles each method before first use) has
  nothing to do under IL2CPP and costs reflection objects IL2CPP never frees: the 64-bit launcher
  should set `CUPHEAD_CODEWARM=0`.
- **Floating point.** IL2CPP's C++ computes `float` in single precision (old Mono often in
  double) and the compiler may fuse multiply-adds: results can differ in the last bits, so
  deterministic replays (`CUPHEAD_TEST_DETERMINISTIC`) of the two builds can drift apart. Nothing
  visible is expected; the game's own logic does not depend on exact float bits.
- **GC.** IL2CPP 2017.4 uses the same non-incremental Boehm collector as Mono, and it reads
  `GC_INITIAL_HEAP_SIZE` too (ua_host's `UA_GC_HEAP`).

## Not tried yet

The IL2CPP build itself (pmunity il2cpp for Unity 2017.4 is in progress in portmaster-unity: its
staging and UnityLinker run for Cuphead, its C++ link with NDK r13b did not work yet), the
packing of IL2CPP data over 2 GiB (Cuphead's is 2.3 GB: APK plus OBB, where Unity's copy of
`assets/bin/Data/Managed` to its files folder must still find the APK's files), and a run.
