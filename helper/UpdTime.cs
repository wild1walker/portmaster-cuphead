using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace CupheadPort
{
    // Test builds only (codepatch --test=test-time-updates): the game's Update, FixedUpdate,
    // LateUpdate, physics callbacks and coroutine steps are timed per method and per runtime type.
    // Every 10 s the player log gets ms per frame, calls per frame and us per call, inclusive (a call
    // made from another timed one is in both), plus the time spent in the outermost timed calls.
    public static class UpdTime
    {
        sealed class Acc { public long ticks; public int calls; public Dictionary<Type, Acc> byType; }
        sealed class RefEq : IEqualityComparer<string>
        {
            public bool Equals(string a, string b) { return ReferenceEquals(a, b); }
            public int GetHashCode(string s) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s); }
        }
        static readonly Dictionary<string, Acc> acc = new Dictionary<string, Acc>(new RefEq());   // keys are the patch's string literals (interned)
        static int depth;
        static long outer;
        static float next = -1f;
        static int frame0;
        static float fixed0;
        static double overheadUs = -1;

        public static bool Active { get { return acc.Count > 0; } }

        public static long T0() { depth++; return Stopwatch.GetTimestamp(); }

        public static void T1(object self, string key, long t0)
        {
            long dt = Stopwatch.GetTimestamp() - t0;
            if (--depth <= 0) { depth = 0; outer += dt; }
            Acc a;
            if (!acc.TryGetValue(key, out a)) acc[key] = a = new Acc { byType = new Dictionary<Type, Acc>() };
            a.ticks += dt; a.calls++;
            if (self == null) return;
            Type t = self.GetType();
            Acc b;
            if (!a.byType.TryGetValue(t, out b)) a.byType[t] = b = new Acc();
            b.ticks += dt; b.calls++;
        }

        // test-time-updates: called first in the motors' HandleRaycasts, so the transform sync their
        // first cast would trigger (autoSyncTransforms) is timed on its own; the same sync, so the
        // casts see the same world
        public static void Sync(string key)
        {
            long t = T0();
            Physics2D.SyncTransforms();
            T1(null, key, t);
        }

        static void Measure()
        {
            const string k = "UpdTime.overhead";
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 2000; i++) { long t = T0(); T1(k, k, t); }
            overheadUs = sw.Elapsed.TotalMilliseconds * 1000.0 / 2000;
            acc.Remove(k); outer = 0;
        }

        // every 30 frames: how many 2D bodies there are, of which type, and how many are asleep
        static int bodySamples, bodies, kinematic, dynamicB, staticB, sleeping, sleepingKinematic;

        static void CountBodies()
        {
            foreach (Rigidbody2D b in UnityEngine.Object.FindObjectsOfType<Rigidbody2D>())
            {
                bodies++;
                if (b.bodyType == RigidbodyType2D.Kinematic) kinematic++;
                else if (b.bodyType == RigidbodyType2D.Dynamic) dynamicB++;
                else staticB++;
                if (b.IsSleeping()) { sleeping++; if (b.bodyType == RigidbodyType2D.Kinematic) sleepingKinematic++; }
            }
            bodySamples++;
        }

        public static void Tick()
        {
            if (acc.Count == 0) return;                          // nothing timed: not a test build
            if (Time.frameCount % 30 == 0) CountBodies();
            if (next < 0f)
            {
                Measure();
                next = Time.realtimeSinceStartup + 10f; frame0 = Time.frameCount; fixed0 = Time.fixedTime;
                acc.Clear(); outer = 0;
                return;
            }
            if (Time.realtimeSinceStartup < next) return;
            int frames = Math.Max(1, Time.frameCount - frame0);
            float steps = (Time.fixedTime - fixed0) / Time.fixedDeltaTime;
            double tickMs = 1000.0 / Stopwatch.Frequency;
            var rows = new List<KeyValuePair<string, Acc>>(acc);
            rows.Sort((x, y) => y.Value.ticks.CompareTo(x.Value.ticks));
            long calls = 0; foreach (var r in rows) calls += r.Value.calls;
            var sb = new StringBuilder();
            sb.Append("[cupheadport updtime] scene " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ": " + frames + " frames, " +
                (steps / frames).ToString("F2") + " physics steps a frame, outermost timed calls " + (outer * tickMs / frames).ToString("F2") + " ms a frame, " +
                (calls / (double)frames).ToString("F0") + " timed calls a frame (timing costs ~" + overheadUs.ToString("F2") + " us a call)");
            if (bodySamples > 0)
                sb.Append("\n  2D bodies (mean of " + bodySamples + " samples): " + (bodies / (float)bodySamples).ToString("F1") + " (kinematic " + (kinematic / (float)bodySamples).ToString("F1") +
                    ", dynamic " + (dynamicB / (float)bodySamples).ToString("F1") + ", static " + (staticB / (float)bodySamples).ToString("F1") + "), asleep " + (sleeping / (float)bodySamples).ToString("F2") +
                    " (kinematic " + (sleepingKinematic / (float)bodySamples).ToString("F2") + ")");
            bodySamples = bodies = kinematic = dynamicB = staticB = sleeping = sleepingKinematic = 0;
            sb.Append("\n   ms/frame  calls/frame   us/call  method [runtime types]");
            for (int i = 0; i < rows.Count && i < 60; i++)
            {
                Acc a = rows[i].Value;
                sb.Append("\n  " + (a.ticks * tickMs / frames).ToString("F3").PadLeft(9) + (a.calls / (double)frames).ToString("F1").PadLeft(13) +
                    (a.ticks * tickMs * 1000.0 / Math.Max(1, a.calls)).ToString("F1").PadLeft(10) + "  " + rows[i].Key);
                if (a.byType.Count > 1 || (a.byType.Count == 1 && !rows[i].Key.StartsWith(First(a.byType).FullName + ".")))
                {
                    var ts = new List<KeyValuePair<Type, Acc>>(a.byType);
                    ts.Sort((x, y) => y.Value.ticks.CompareTo(x.Value.ticks));
                    sb.Append("  [");
                    for (int j = 0; j < ts.Count && j < 6; j++)
                        sb.Append((j > 0 ? ", " : "") + ts[j].Key.Name + " " + (ts[j].Value.ticks * tickMs / frames).ToString("F2") + "/" + (ts[j].Value.calls / (double)frames).ToString("F1"));
                    if (ts.Count > 6) sb.Append(", +" + (ts.Count - 6));
                    sb.Append("]");
                }
            }
            UnityEngine.Debug.Log(sb.ToString());
            acc.Clear(); outer = 0;
            next = Time.realtimeSinceStartup + 10f; frame0 = Time.frameCount; fixed0 = Time.fixedTime;
        }

        static Type First(Dictionary<Type, Acc> d) { foreach (var kv in d) return kv.Key; return null; }
    }
}
