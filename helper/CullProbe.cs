using System;
using System.Collections.Generic;
using UnityEngine;

namespace CupheadPort
{
    // Experiment only (CUPHEAD_CULLPROBE=1): what an animator Unity has culled still writes. Every 5
    // frames, for each animator whose sprites are all off screen, the sprite of each of its sprite
    // renderers is compared with the last sample: per culling mode, how many animators were off screen
    // and how many of them still changed a sprite meanwhile (= Unity still writes non-transform values
    // of an animator culled in that mode). Logged every 20 s.
    public static class CullProbe
    {
        sealed class S { public Animator a; public SpriteRenderer[] rs; public Sprite[] last; public bool wasOff; }
        static int on = -1;
        static readonly List<S> list = new List<S>();
        static float nextScan, nextLog;
        static readonly int[] offSeen = new int[3], changedOff = new int[3];
        static readonly HashSet<int>[] offIds = { new HashSet<int>(), new HashSet<int>(), new HashSet<int>() }, chIds = { new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };

        public static void Tick()
        {
            if (on < 0) on = Environment.GetEnvironmentVariable("CUPHEAD_CULLPROBE") == "1" ? 1 : 0;
            if (on == 0) return;
            if (Time.realtimeSinceStartup >= nextScan)
            {
                nextScan = Time.realtimeSinceStartup + 5f;
                list.Clear();
                foreach (Animator a in UnityEngine.Object.FindObjectsOfType<Animator>())
                {
                    var rs = a.GetComponentsInChildren<SpriteRenderer>();
                    if (rs.Length == 0) continue;
                    var s = new S { a = a, rs = rs, last = new Sprite[rs.Length] };
                    for (int i = 0; i < rs.Length; i++) s.last[i] = rs[i].sprite;
                    list.Add(s);
                }
            }
            if (Time.frameCount % 5 == 0)
                foreach (S s in list)
                {
                    if (s.a == null) continue;
                    bool off = true;
                    for (int i = 0; i < s.rs.Length; i++) if (s.rs[i] != null && s.rs[i].isVisible) { off = false; break; }
                    int m = (int)s.a.cullingMode;
                    bool changed = false;
                    for (int i = 0; i < s.rs.Length; i++)
                    {
                        if (s.rs[i] == null) continue;
                        Sprite now = s.rs[i].sprite;
                        if (now != s.last[i]) { changed = true; s.last[i] = now; }
                    }
                    if (off && s.wasOff && m >= 0 && m < 3)
                    {
                        offIds[m].Add(s.a.GetInstanceID());
                        if (changed && s.a.enabled && s.a.gameObject.activeInHierarchy) chIds[m].Add(s.a.GetInstanceID());
                    }
                    s.wasOff = off;
                }
            if (Time.realtimeSinceStartup < nextLog) return;
            nextLog = Time.realtimeSinceStartup + 20f;
            string[] names = { "AlwaysAnimate", "CullUpdateTransforms", "CullCompletely" };
            var sb = new System.Text.StringBuilder("[cupheadport cullprobe] " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ", animators off screen / of them still changing a sprite there:");
            for (int m = 0; m < 3; m++) { sb.Append(" " + names[m] + " " + offIds[m].Count + "/" + chIds[m].Count + ";"); offIds[m].Clear(); chIds[m].Clear(); }
            UnityEngine.Debug.Log(sb.ToString());
        }
    }
}
