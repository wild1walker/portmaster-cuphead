#!/usr/bin/env python3
"""Summarize bench_all.sh logs: benchsum.py bench/*.log [--states] [--hitches]

Each "[fps]" window (host/frames.c) is attributed to the level and boss state of the latest
"[cupheadport bench]" line (converter/helper TestHooks). Per level (and with --states, per boss
state): average fps over its windows, the worst window, the worst p99 and worst frame, late
(>25 ms) and hitch (>50 ms) frame counts, GC collections. --hitches lists every frame over 100 ms
with its level/state. Windows while a level loads or between levels (after a win, from the port
helper's scene-load warm-ups until the next bench line) are "(loading)"."""
import re, sys
from collections import OrderedDict

FPS = re.compile(r'\[fps\] ([\d.]+) fps, frame ([\d.]+) avg / ([\d.]+) p99 / ([\d.]+) worst ms, late (\d+) hitch (\d+), gc (-?\d+) heap (-?\d+) MB \((\d+) frames, \d+ total, (\d+) s\)(?: rss (\d+) MB)?')
BENCH = re.compile(r'\[cupheadport bench\] (\w+?)(?:Level)? t=(\d+) state=(\S+) hp=(\S+) x=(\S+)( WON)?')
EVT = re.compile(r'\[cupheadport bench\] (.*)')
LOADING = re.compile(r'\[cupheadport\] (code|font|shader) warmup for |\[cupheadport\] memory trim \(scene')
HITCH = re.compile(r'\[hitch\] ([\d.]+) ms at ([\d.]+) s')

def summarize(paths, by_state, list_hitches):
    rows = OrderedDict()
    hitches, notes = [], []
    for path in paths:
        level, state, loading, lt, named = None, None, True, 0, '1st'
        for line in open(path, errors='replace'):
            if LOADING.search(line):                    # a scene load (the port helper's warm-ups)
                loading = True
                continue
            m = BENCH.search(line)
            if m:
                if m.group(6):                          # won: the win screen and the next load follow
                    loading = True
                    continue
                level, state, loading, lt = m.group(1), m.group(3), False, int(m.group(2))
                if level == 'Platforming':              # one class for every run-and-gun: name it from the playlist
                    level = named.replace('Platforming_Level_', 'RunGun ')
                continue
            m = EVT.search(line)
            if m and not BENCH.search(line):
                txt = m.group(1)
                if txt.startswith('next:'):
                    named = txt[5:].strip()
                if txt.startswith('next:') or 'finished' in txt or 'limit' in txt or '-> next' in txt:
                    loading = True
                if 'limit' in txt or 'stalled' in txt or 'unstick' in txt:
                    notes.append(f'{path.split("/")[-1]}: {txt}')
                continue
            m = HITCH.search(line)
            if m:
                hitches.append((float(m.group(1)), float(m.group(2)), '(loading)' if loading or not level else f'{level}/{state}' + (' (start)' if lt < 10 else ''), path.split('/')[-1]))
                continue
            m = FPS.search(line)
            if not m:
                continue
            fps, avg, p99, worst, late, hitch, gc, heap, frames, t, rss = m.groups()
            # the first 10 s of a level: the load's tail (shader warm-up while the screen fades in)
            key = '(loading)' if loading or not level else '(level start)' if lt < 10 else (f'{level} / {state}' if by_state else level)
            r = rows.setdefault(key, dict(win=0, frames=0, time=0.0, minfps=999.0, minat='', p99=0.0, worst=0.0, late=0, hitch=0, gc=0, heap=0, rss=0))
            r['win'] += 1; r['frames'] += int(frames); r['time'] += int(frames) * float(avg) / 1000
            if float(fps) < r['minfps']: r['minfps'], r['minat'] = float(fps), f'{t}s'
            r['p99'] = max(r['p99'], float(p99)); r['worst'] = max(r['worst'], float(worst))
            r['late'] += int(late); r['hitch'] += int(hitch); r['gc'] += max(int(gc), 0); r['heap'] = max(r['heap'], int(heap)); r['rss'] = max(r['rss'], int(rss or 0))
    print(f'{"level" + (" / state" if by_state else ""):<44} {"avg":>5} {"min":>5} {"p99":>6} {"worst":>7} {"late":>5} {"hitch":>5} {"gc":>4} {"heap":>5} {"rss":>5} {"secs":>5}')
    for k, r in rows.items():
        avg = r['frames'] / r['time'] if r['time'] else 0
        print(f'{k:<44} {avg:5.1f} {r["minfps"]:5.1f} {r["p99"]:6.1f} {r["worst"]:7.1f} {r["late"]:5d} {r["hitch"]:5d} {r["gc"]:4d} {r["heap"]:5d} {r["rss"]:5d} {r["time"]:5.0f}')
    if notes:
        print('\nnotes:'); [print('  ' + n) for n in notes]
    if list_hitches and hitches:
        print('\nframes over 100 ms:')
        for ms, t, where, f in hitches:
            print(f'  {ms:7.1f} ms  {t:7.1f} s  {where:<36} {f}')

if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    summarize(args, '--states' in sys.argv, '--hitches' in sys.argv)
