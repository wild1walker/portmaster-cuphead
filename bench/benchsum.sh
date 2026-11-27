#!/bin/sh
# benchsum.sh bench/*.log [--states] [--hitches]: bench_all.sh's logs summed up (it was benchsum.py).
#
# Each "[fps]" window (host/frames.c) is attributed to the level and boss state of the latest
# "[cupheadport bench]" line (the helper's TestHooks). Per level (and with --states, per boss state):
# average fps over its windows, the worst window, the worst p99 and worst frame, late (>25 ms) and
# hitch (>50 ms) frame counts, GC collections. --hitches lists every frame over 100 ms with its
# level/state. Windows while a level loads or between levels (after a win, from the port helper's
# scene-load warm-ups until the next bench line) are "(loading)". POSIX sh and awk.
states=0; hitches=0; files=
for a in "$@"; do
  case $a in --states) states=1 ;; --hitches) hitches=1 ;; --*) ;; *) files="$files
$a" ;; esac
done
printf '%s\n' "$files" | sed '/^$/d' | while IFS= read -r f; do printf '%s\n' "### $f"; cat "$f"; done |
awk -v by_state="$states" -v list_hitches="$hitches" '
function num(s, key,   i, r) {   # the number after <key> in s ("" when none)
  i = index(s, key); if (!i) return ""
  r = substr(s, i + length(key)); sub(/[^-0-9.].*/, "", r); return r
}
function word_after(s, key,   i, r) { i = index(s, key); if (!i) return ""; r = substr(s, i + length(key)); sub(/[ \t].*/, "", r); return r }
function base(p) { sub(/.*\//, "", p); return p }
/^### / { path = substr($0, 5); level = ""; state = ""; loading = 1; lt = 0; named = "1st"; next }
/\[cupheadport\] (code|font|shader) warmup for |\[cupheadport\] memory trim \(scene/ { loading = 1; next }
/\[cupheadport bench\] [A-Za-z0-9_]+ t=[0-9]+ state=[^ ]+ hp=[^ ]+ x=[^ ]+/ {
  i = index($0, "[cupheadport bench] "); rest = substr($0, i + 20)
  lv = rest; sub(/ .*/, "", lv); sub(/Level$/, "", lv)
  if (rest ~ / WON/) { loading = 1; next }   # won: the win screen and the next load follow
  level = lv; state = word_after(rest, "state="); loading = 0; lt = num(rest, " t=") + 0
  if (level == "Platforming") { level = named; sub(/Platforming_Level_/, "RunGun ", level) }   # every run-and-gun: named from the playlist
  next
}
/\[cupheadport bench\] / {
  i = index($0, "[cupheadport bench] "); txt = substr($0, i + 20)
  if (txt ~ /^next:/) { named = substr(txt, 6); gsub(/^[ \t]+|[ \t]+$/, "", named) }
  if (txt ~ /^next:/ || txt ~ /finished/ || txt ~ /limit/ || txt ~ /-> next/) loading = 1
  if (txt ~ /limit/ || txt ~ /stalled/ || txt ~ /unstick/) notes[++nn] = base(path) ": " txt
  next
}
/\[hitch\] [0-9.]+ ms at [0-9.]+ s/ {
  w = (loading || level == "") ? "(loading)" : level "/" state (lt < 10 ? " (start)" : "")
  hn++; hms[hn] = num($0, "[hitch] "); hat[hn] = num($0, " ms at "); hwhere[hn] = w; hfile[hn] = base(path)
  next
}
/\[fps\] [0-9.]+ fps, frame [0-9.]+ avg \/ [0-9.]+ p99 \/ [0-9.]+ worst ms, late [0-9]+ hitch [0-9]+, gc -?[0-9]+ heap -?[0-9]+ MB \([0-9]+ frames, [0-9]+ total, [0-9]+ s\)/ {
  fps = num($0, "[fps] "); avg = num($0, "fps, frame "); p99 = num($0, "avg / "); worst = num($0, "p99 / ")
  late = num($0, "late "); hitch = num($0, " hitch "); gc = num($0, ", gc "); heap = num($0, " heap ")
  i = index($0, " frames, "); s = substr($0, 1, i - 1); sub(/.*\(/, "", s); frames = s
  t = num($0, " total, "); rss = num($0, " rss ")
  # the first 10 s of a level: the tail of the load (shader warm-up while the screen fades in)
  key = (loading || level == "") ? "(loading)" : (lt < 10 ? "(level start)" : (by_state ? level " / " state : level))
  if (!(key in win)) { order[++nk] = key; minfps[key] = 999; p99m[key] = 0; worstm[key] = 0; heapm[key] = 0; rssm[key] = 0 }
  win[key]++; fr[key] += frames; tm[key] += frames * avg / 1000
  if (fps + 0 < minfps[key]) minfps[key] = fps + 0
  if (p99 + 0 > p99m[key]) p99m[key] = p99 + 0
  if (worst + 0 > worstm[key]) worstm[key] = worst + 0
  lat[key] += late; hit[key] += hitch; if (gc + 0 > 0) gcs[key] += gc
  if (heap + 0 > heapm[key]) heapm[key] = heap + 0
  if (rss + 0 > rssm[key]) rssm[key] = rss + 0
}
END {
  printf "%-44s %5s %5s %6s %7s %5s %5s %4s %5s %5s %5s\n", "level" (by_state ? " / state" : ""), "avg", "min", "p99", "worst", "late", "hitch", "gc", "heap", "rss", "secs"
  for (k = 1; k <= nk; k++) {
    key = order[k]; a = tm[key] ? fr[key] / tm[key] : 0
    printf "%-44s %5.1f %5.1f %6.1f %7.1f %5d %5d %4d %5d %5d %5.0f\n", key, a, minfps[key], p99m[key], worstm[key], lat[key], hit[key], gcs[key], heapm[key], rssm[key], tm[key]
  }
  if (nn) { print "\nnotes:"; for (i = 1; i <= nn; i++) print "  " notes[i] }
  if (list_hitches && hn) {
    print "\nframes over 100 ms:"
    for (i = 1; i <= hn; i++) printf "  %7.1f ms  %7.1f s  %-36s %s\n", hms[i], hat[i], hwhere[i], hfile[i]
  }
}'
