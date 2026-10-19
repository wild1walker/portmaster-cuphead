#!/usr/bin/env python3
"""A/B summary of ab_device.sh runs: absum.py ab/<tag>_*.log
Per boss state: mean fps of A runs and of B runs (time-weighted, from the [fps] windows), the
difference, and each run's own figure, so run-to-run spread is visible."""
import collections, os, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
FPS = re.compile(r'\[fps\] ([\d.]+) fps, frame ([\d.]+) avg .*?\((\d+) frames')
BENCH = re.compile(r'\[cupheadport bench\] (\w+?)(?:Level)? t=(\d+) state=(\S+) hp=\S+ x=\S+( WON)?')
res = collections.defaultdict(lambda: collections.defaultdict(list))   # state -> variant -> [(frames, time)]
for path in sys.argv[1:]:
    v = re.search(r'_([AB])\d+\.log$', path).group(1)
    state, lt, per = None, 0, collections.defaultdict(lambda: [0, 0.0])
    for line in open(path, errors='replace'):
        m = BENCH.search(line)
        if m:
            state = None if m.group(4) else f'{m.group(1)}/{m.group(3)}'; lt = int(m.group(2)); continue
        m = FPS.search(line)
        if m and state and lt >= 10:
            n = int(m.group(3)); per[state][0] += n; per[state][1] += n * float(m.group(2)) / 1000
    for st, (n, t) in per.items():
        if t > 0: res[st][v].append(n / t)
for st, vs in res.items():
    a, b = vs.get('A', []), vs.get('B', [])
    ma = sum(a) / len(a) if a else 0; mb = sum(b) / len(b) if b else 0
    print(f'{st:<32} A {ma:5.1f}  B {mb:5.1f}  B-A {mb - ma:+5.1f} ({100 * (mb - ma) / ma if ma else 0:+.1f}%)   A runs ' +
          ' '.join(f'{x:.1f}' for x in a) + '   B runs ' + ' '.join(f'{x:.1f}' for x in b))
