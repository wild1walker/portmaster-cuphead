using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CupheadPort
{
    // Experiment only (CUPHEAD_ANIMPROBE=1, with CUPHEAD_ANIMCULL=0): which of the animators that
    // CUPHEAD_ANIMCULL would cull move transforms themselves? Such an animator, culled while off screen,
    // keeps its pieces where they were; one that animates its picture into view could stay out of view.
    // Every 30 frames the local position, rotation and scale of every transform below each candidate
    // (always animating, a sprite shown) is compared with the last sample; a change below the animator's
    // own object counts as animated (scripts usually move the object itself). Logged every 20 s: the
    // candidates, how many animate their pieces, with or without a collider below, and their names.
    public static class AnimProbe
    {
        sealed class Cand { public Animator a; public Transform[] ts; public Vector3[] p, s; public Quaternion[] r; public bool moved, hitbox; public string name; public string events; }
        static int on = -1;
        static readonly List<Cand> cands = new List<Cand>();
        static readonly HashSet<int> seen = new HashSet<int>();
        static float nextScan, nextLog;

        public static void Tick()
        {
            if (on < 0)
            {
                on = Environment.GetEnvironmentVariable("CUPHEAD_ANIMPROBE") == "1" ? 1 : 0;
                if (on == 1) SceneManager.sceneLoaded += (sc, m) => { cands.Clear(); seen.Clear(); nextScan = Time.realtimeSinceStartup + 2f; nextLog = Time.realtimeSinceStartup + 20f; };
            }
            if (on == 0) return;
            string scene = SceneManager.GetActiveScene().name;
            if (!scene.StartsWith("scene_level_platforming") && !scene.StartsWith("scene_map")) return;
            if (Time.realtimeSinceStartup >= nextScan)
            {
                nextScan = Time.realtimeSinceStartup + 10f;
                foreach (Animator a in UnityEngine.Object.FindObjectsOfType<Animator>())
                {
                    if (!seen.Add(a.GetInstanceID()) || a.cullingMode != AnimatorCullingMode.AlwaysAnimate) continue;
                    bool shows = false;
                    foreach (SpriteRenderer sr in a.GetComponentsInChildren<SpriteRenderer>()) if (sr.enabled && sr.sprite != null) { shows = true; break; }
                    if (!shows) continue;
                    var c = new Cand { a = a, name = a.name.Replace("(Clone)", "").Trim(), hitbox = a.GetComponentInChildren<Collider2D>(true) != null };
                    var fns = new HashSet<string>();
                    if (a.runtimeAnimatorController != null)
                        foreach (AnimationClip clip in a.runtimeAnimatorController.animationClips)
                            if (clip != null) foreach (AnimationEvent e in clip.events) fns.Add(e.functionName);
                    c.events = fns.Count > 0 ? string.Join("/", new List<string>(fns).ToArray()) : null;
                    var list = new List<Transform>(a.GetComponentsInChildren<Transform>(true));
                    list.Remove(a.transform);
                    c.ts = list.ToArray();
                    c.p = new Vector3[c.ts.Length]; c.s = new Vector3[c.ts.Length]; c.r = new Quaternion[c.ts.Length];
                    for (int i = 0; i < c.ts.Length; i++) { c.p[i] = c.ts[i].localPosition; c.s[i] = c.ts[i].localScale; c.r[i] = c.ts[i].localRotation; }
                    cands.Add(c);
                }
            }
            if (Time.frameCount % 30 == 0)
                foreach (Cand c in cands)
                {
                    if (c.moved || c.a == null) continue;
                    for (int i = 0; i < c.ts.Length; i++)
                    {
                        Transform t = c.ts[i];
                        if (t == null) continue;
                        if ((t.localPosition - c.p[i]).sqrMagnitude > 1e-6f || (t.localScale - c.s[i]).sqrMagnitude > 1e-8f || Quaternion.Angle(t.localRotation, c.r[i]) > 0.01f) { c.moved = true; break; }
                    }
                }
            if (Time.realtimeSinceStartup < nextLog) return;
            nextLog = Time.realtimeSinceStartup + 20f;
            int moved = 0, movedHit = 0, hit = 0;
            var names = new Dictionary<string, int>();
            foreach (Cand c in cands)
            {
                if (c.hitbox) hit++;
                if (!c.moved) continue;
                moved++; if (c.hitbox) movedHit++;
                string k = c.name + (c.hitbox ? " [collider]" : "");
                int n; names.TryGetValue(k, out n); names[k] = n + 1;
            }
            var sb = new StringBuilder("[cupheadport animprobe] " + scene + ": " + cands.Count + " candidates (" + hit + " with a collider below), " + moved + " animate their pieces (" + movedHit + " with a collider):");
            foreach (var kv in names) sb.Append(" " + kv.Key + (kv.Value > 1 ? " x" + kv.Value : ""));
            var ev = new Dictionary<string, int>();
            int withEv = 0, withEvNoHit = 0;
            foreach (Cand c in cands)
            {
                if (c.events == null) continue;
                withEv++; if (!c.hitbox) withEvNoHit++;
                string k = c.name + (c.hitbox ? " [collider]" : "") + ": " + c.events;
                int n; ev.TryGetValue(k, out n); ev[k] = n + 1;
            }
            sb.Append("\n  with animation events: " + withEv + " (" + withEvNoHit + " without a collider, which the default culling stops):");
            foreach (var kv in ev) sb.Append("\n    " + kv.Key + (kv.Value > 1 ? " x" + kv.Value : ""));
            UnityEngine.Debug.Log(sb.ToString());
        }
    }
}
