#!/bin/sh
# absum.sh ab/<tag>_*.log: A/B summary of ab_device.sh runs (it was absum.py). Per boss state: mean
# fps of the A runs and of the B runs (time-weighted, from the [fps] windows), the difference, and each
# run's own figure, so run-to-run spread is visible. POSIX sh and awk.
for f in "$@"; do printf '%s\n' "### $f"; cat "$f"; done | awk '
function num(s, key,   i, r) { i = index(s, key); if (!i) return ""; r = substr(s, i + length(key)); sub(/[^-0-9.].*/, "", r); return r }
function flush(   k) { for (k in pn) if (pt[k] > 0) { if (!(k in seen)) { order[++ns] = k; seen[k] = 1 }; runs[k, v] = runs[k, v] " " sprintf("%.1f", pn[k] / pt[k]); sum[k, v] += pn[k] / pt[k]; cnt[k, v]++ }
  split("", pn); split("", pt) }
/^### / { if (started) flush(); started = 1; p = substr($0, 5); v = p; sub(/\.log$/, "", v); sub(/.*_/, "", v); v = substr(v, 1, 1); state = ""; lt = 0; next }
/\[cupheadport bench\] [A-Za-z0-9_]+ t=[0-9]+ state=[^ ]+ hp=[^ ]+ x=[^ ]+/ {
  i = index($0, "[cupheadport bench] "); rest = substr($0, i + 20)
  lv = rest; sub(/ .*/, "", lv); sub(/Level$/, "", lv)
  st = rest; sub(/.* state=/, "", st); sub(/ .*/, "", st)
  state = (rest ~ / WON/) ? "" : lv "/" st; lt = num(rest, " t=") + 0; next
}
/\[fps\] [0-9.]+ fps, frame [0-9.]+ avg .*\([0-9]+ frames/ {
  if (state == "" || lt < 10) next
  i = index($0, " frames"); s = substr($0, 1, i - 1); sub(/.*\(/, "", s)
  n = s + 0; pn[state] += n; pt[state] += n * num($0, "fps, frame ") / 1000
}
END {
  if (started) flush()
  for (k = 1; k <= ns; k++) {
    s = order[k]; ma = cnt[s, "A"] ? sum[s, "A"] / cnt[s, "A"] : 0; mb = cnt[s, "B"] ? sum[s, "B"] / cnt[s, "B"] : 0
    printf "%-32s A %5.1f  B %5.1f  B-A %+5.1f (%+.1f%%)   A runs%s   B runs%s\n", s, ma, mb, mb - ma, ma ? 100 * (mb - ma) / ma : 0, runs[s, "A"], runs[s, "B"]
  }
}'
