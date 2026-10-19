using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace CupheadPort
{
    // Diagnostics, development player only (Unity's profiler markers are compiled into it):
    // CUPHEAD_MARKERS=1 records every profiler sampler with UnityEngine.Profiling.Recorder and every
    // 10 s logs the 50 most expensive (ms per frame, nested time included, and calls per frame), so
    // the engine's own breakdown (Physics2D.Simulate, BehaviourUpdate, Animator.Update, ...) can be
    // read from a device run.
    public static class Markers
    {
        static int state;                  // 0 not started, 1 on, -1 off
        static Recorder[] recs;
        static string[] names;
        static double[] ns;
        static long[] calls;
        static int frames;
        static float next;

        public static void Tick()
        {
            if (state < 0) return;
            if (state == 0)
            {
                state = -1;
                string v = Environment.GetEnvironmentVariable("CUPHEAD_MARKERS");
                Console.Error.WriteLine("[cupheadport markers] first tick, CUPHEAD_MARKERS=" + (v ?? "(unset)"));
                if (v != "1") return;
                var list = new List<string>();
                Sampler.GetNames(list);
                var r = new List<Recorder>(); var n = new List<string>();
                foreach (string s in list)
                {
                    Recorder rec = Recorder.Get(s);
                    if (rec == null || !rec.isValid) continue;
                    rec.enabled = true;
                    r.Add(rec); n.Add(s);
                }
                if (r.Count == 0) { UnityEngine.Debug.Log("[cupheadport markers] no samplers can be recorded (not a development player?)"); return; }
                recs = r.ToArray(); names = n.ToArray();
                ns = new double[recs.Length]; calls = new long[recs.Length];
                next = Time.realtimeSinceStartup + 10f;
                UnityEngine.Debug.Log("[cupheadport markers] recording " + recs.Length + " of " + list.Count + " samplers");
                state = 1;
                return;
            }
            for (int i = 0; i < recs.Length; i++)
            {
                long t = recs[i].elapsedNanoseconds;
                if (t == 0) continue;
                ns[i] += t; calls[i] += recs[i].sampleBlockCount;
            }
            frames++;
            if (Time.realtimeSinceStartup < next) return;
            next = Time.realtimeSinceStartup + 10f;
            var order = new int[recs.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => ns[b].CompareTo(ns[a]));
            var sb = new StringBuilder("[cupheadport markers] " + frames + " frames, scene " +
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " (ms per frame, calls per frame):");
            for (int k = 0; k < 50 && k < order.Length; k++)
            {
                int i = order[k];
                if (ns[i] <= 0) break;
                sb.Append("\n  " + (ns[i] / frames / 1e6).ToString("F2") + " ms  " + ((double)calls[i] / frames).ToString("F1") + "  " + names[i]);
            }
            UnityEngine.Debug.Log(sb.ToString());
            Array.Clear(ns, 0, ns.Length); Array.Clear(calls, 0, calls.Length); frames = 0;
        }
    }
}
