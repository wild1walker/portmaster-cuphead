using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace CupheadPort
{
    // Experiment only (CUPHEAD_CASTPROBE=1): does a 2D box cast cost less when it is shorter, and is
    // its nearest hit the same? Every frame in a level, from the player's position, after a transform
    // sync, box casts like the player motor's (left, right, up, down; the motor's own layer masks)
    // are timed at 2000, 300 and 100 units. When the 2000-unit cast's nearest hit is within the
    // shorter length, the shorter cast's hit is compared with it (collider, distance, point, normal).
    // Logged every 10 s.
    public static class CastProbe
    {
        static int on = -1;
        static LevelPlayerMotor motor;
        static int wall, ground, ceiling;
        static float next;
        static readonly float[] lengths = { 2000f, 300f, 100f };
        static readonly int[] order = { 2, 1, 0 };          // shortest first: a cast does not run right after a longer one in the same direction
        static readonly string[] dirs = { "left", "right", "up", "down" };
        static readonly long[,] ticks = new long[4, 3];
        static readonly int[,] n = new int[4, 3], hitsIn = new int[4, 3], same = new int[4, 3], differ = new int[4, 3], bitSame = new int[4, 3];
        static readonly double[,] maxD = new double[4, 3];
        static long allHits; static int allCasts;

        static int Mask(string name)
        {
            FieldInfo f = typeof(LevelPlayerMotor).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            object v = f != null ? f.GetValue(motor) : null;
            if (v is LayerMask) return ((LayerMask)v).value;
            if (v is int) return (int)v;
            return Physics2D.DefaultRaycastLayers;
        }

        // CUPHEAD_CALLPROBE=1: what common engine calls cost from game code (us a call, 200 calls each,
        // every frame in a level, logged every 10 s)
        static int callOn = -1;
        static readonly string[] callNames = { "RaycastHit2D.collider", "Component.transform", "Transform.position get", "Transform.position set",
            "GetComponent<Collider2D>", "Physics2D.Raycast 10 units", "UnityEngine.Object == null", "Time.deltaTime" };
        static readonly long[] callTicks = new long[8];
        static int callFrames;
        static float callNext;

        static void Calls()
        {
            if (motor == null) return;
            Transform tr = motor.transform;
            Vector3 p = tr.position;
            RaycastHit2D h = Physics2D.Raycast((Vector2)p + new Vector2(0f, 20f), Vector2.down, 2000f, ground);
            object sink = null; float f = 0f;
            for (int k = 0; k < 8; k++)
            {
                long t0 = Stopwatch.GetTimestamp();
                for (int i = 0; i < 200; i++)
                    switch (k)
                    {
                        case 0: sink = h.collider; break;
                        case 1: sink = motor.transform; break;
                        case 2: p = tr.position; break;
                        case 3: tr.position = p; break;
                        case 4: sink = motor.GetComponent<Collider2D>(); break;
                        case 5: f += Physics2D.Raycast((Vector2)p + new Vector2(0f, 20f), Vector2.down, 10f, ground).distance; break;
                        case 6: if (motor == null) f += 1f; break;
                        case 7: f += Time.deltaTime; break;
                    }
                callTicks[k] += Stopwatch.GetTimestamp() - t0;
            }
            if (sink == null && f < -1f) UnityEngine.Debug.Log("");
            callFrames++;
            if (Time.realtimeSinceStartup < callNext) return;
            callNext = Time.realtimeSinceStartup + 10f;
            var sb = new StringBuilder("[cupheadport callprobe] us a call:");
            for (int k = 0; k < 8; k++) { sb.Append(" " + callNames[k] + " " + (callTicks[k] * 1e6 / Stopwatch.Frequency / (200.0 * callFrames)).ToString("F2") + ";"); callTicks[k] = 0; }
            callFrames = 0;
            UnityEngine.Debug.Log(sb.ToString());
        }

        public static void Tick()
        {
            if (on < 0) on = Environment.GetEnvironmentVariable("CUPHEAD_CASTPROBE") == "1" ? 1 : 0;
            if (callOn < 0) callOn = Environment.GetEnvironmentVariable("CUPHEAD_CALLPROBE") == "1" ? 1 : 0;
            if (callOn == 1)
            {
                if (motor == null && Time.frameCount % 30 == 0) { motor = UnityEngine.Object.FindObjectOfType<LevelPlayerMotor>(); if (motor != null) { wall = Mask("wallMask"); ground = Mask("groundMask"); ceiling = Mask("ceilingMask"); } }
                Calls();
            }
            if (on == 0) return;
            if (motor == null)
            {
                if (Time.frameCount % 30 != 0) return;
                motor = UnityEngine.Object.FindObjectOfType<LevelPlayerMotor>();
                if (motor == null) return;
                wall = Mask("wallMask"); ground = Mask("groundMask"); ceiling = Mask("ceilingMask");
                UnityEngine.Debug.Log("[cupheadport castprobe] masks wall " + wall + " ground " + ground + " ceiling " + ceiling);
                next = Time.realtimeSinceStartup + 10f;
            }
            Physics2D.SyncTransforms();
            Vector2 c = (Vector2)motor.transform.position + new Vector2(0f, 60f);
            for (int d = 0; d < 4; d++)
            {
                Vector2 dir = d == 0 ? Vector2.left : d == 1 ? Vector2.right : d == 2 ? Vector2.up : Vector2.down;
                Vector2 size = d < 2 ? new Vector2(1f, 100f) : new Vector2(60f, 1f);
                int mask = d < 2 ? wall : d == 2 ? ceiling : ground;
                RaycastHit2D[] got = new RaycastHit2D[3];
                for (int k = 0; k < 3; k++)
                {
                    int l = order[k];
                    long t0 = Stopwatch.GetTimestamp();
                    got[l] = Physics2D.BoxCast(c, size, 0f, dir, lengths[l], mask);
                    ticks[d, l] += Stopwatch.GetTimestamp() - t0;
                    n[d, l]++;
                }
                RaycastHit2D full = got[0];
                for (int l = 1; l < 3; l++)
                {
                    RaycastHit2D h = got[l];
                    if (full.collider == null || full.distance > lengths[l]) continue;
                    hitsIn[d, l]++;
                    if (h.collider != full.collider) { differ[d, l]++; continue; }
                    same[d, l]++;
                    double dd = Math.Max(Math.Abs(h.distance - full.distance), Math.Max((h.point - full.point).magnitude, (h.normal - full.normal).magnitude));
                    if (dd > maxD[d, l]) maxD[d, l] = dd;
                    if (h.distance == full.distance && h.point == full.point && h.normal == full.normal && h.fraction * lengths[l] == full.fraction * lengths[0]) bitSame[d, l]++;
                }
                if (Time.frameCount % 10 == 0) { allHits += Physics2D.BoxCastAll(c, size, 0f, dir, 2000f, mask).Length; allCasts++; }
            }
            if (Time.realtimeSinceStartup < next) return;
            next = Time.realtimeSinceStartup + 10f;
            double us = 1e6 / Stopwatch.Frequency;
            var sb = new StringBuilder("[cupheadport castprobe] scene " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name +
                ", hits along a 2000-unit cast: " + (allHits / (double)Math.Max(1, allCasts)).ToString("F1") + " on average");
            for (int d = 0; d < 4; d++)
            {
                sb.Append("\n  " + dirs[d].PadRight(6));
                for (int l = 0; l < 3; l++)
                {
                    sb.Append("  " + lengths[l] + ": " + (ticks[d, l] * us / Math.Max(1, n[d, l])).ToString("F1") + " us");
                    if (l > 0) sb.Append(" (nearest hit within: " + hitsIn[d, l] + ", same collider " + same[d, l] + ", bit-identical " + bitSame[d, l] +
                        ", other collider " + differ[d, l] + ", max diff " + maxD[d, l].ToString("G3") + ")");
                    ticks[d, l] = 0; n[d, l] = 0; hitsIn[d, l] = 0; same[d, l] = 0; differ[d, l] = 0; bitSame[d, l] = 0; maxD[d, l] = 0;
                }
            }
            allHits = 0; allCasts = 0;
            UnityEngine.Debug.Log(sb.ToString());
        }
    }
}
