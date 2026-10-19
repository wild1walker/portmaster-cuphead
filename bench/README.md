# bench: unattended tests on the handheld

These scripts run on the handheld, in the game folder, next to `ua_host` and portmaster-unity's
`tools/device/` scripts (`run.sh`, `profcpu.py`, `memprof.py`, `stacks.py`): copy both there. They
drive the game with the helper's test hooks (`CUPHEAD_TEST_*`, see `helper/TestHooks.cs`: level
playlist, god mode, scripted pad, phase holds, deterministic replay, screenshots). Stop
EmulationStation first (the game needs the screen); the session scripts start it again at the end.

| script | what |
|---|---|
| `profile_level.sh <Level> [state] [secs] [release\|dev]` | one level, with per-thread CPU every 10 s |
| `bench_all.sh [groups]` | every level and boss phase in groups (bosses, dice, DLC, shmups, run-and-guns, maps, hubs, tutorials, cutscenes, Expert, Ms. Chalice, graveyard, mausoleum) |
| `ab_device.sh <tag> <level> <state> <secs> <reps> "<env A>" "<env B>"` | A/B of two configurations, alternating runs (same thermal state for both) |
| `devshots.sh <tag> <level> <frames> <secs> <apk>...` | deterministic replays with screenshots, one per APK build, for pixel comparisons (`tools/emu/imgdiff.py`) |
| `session_*.sh` | the sessions used for the shader converter and the default settings |

On the PC: `benchsum.py bench/*.log [--states] [--hitches]` (per level and phase: fps, p99, late
frames, hitches, GC), `absum.py ab/<tag>_*.log` (A/B per phase). The device warms up: compare only
runs made in the same thermal state (A/B alternation), not numbers from different sessions.
