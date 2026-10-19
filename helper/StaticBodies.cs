using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CupheadPort
{
    // Experiment (CUPHEAD_STATICBODIES=1): a 2D collider without a Rigidbody2D is static, and Unity
    // rebuilds its Box2D shape every time its transform moves (destroys and recreates the fixture,
    // flags its contacts for recreation and wakes every body touching it): Treetop Trouble moves ~62
    // such colliders every frame (the mosquito platforms), ~3 ms of the RGSP's frame plus the contact
    // churn in the next physics step. A Static Rigidbody2D is the same kind of Box2D body (static),
    // with the same contacts, triggers and messages, but moving it only moves its body. So every 0.5 s
    // in run-and-gun levels the colliders without a body that moved get one, each on its own object
    // (children with colliders too, deepest first, so no collider attaches to a parent's body and no
    // message goes to another object than before). What the rebuild did besides: it woke the bodies
    // touching the collider. Here, at the end of every frame, the bodies that overlapped the old
    // bounds of a moved one are woken (Box2D only updates contacts of awake bodies).
    public static class StaticBodies
    {
        sealed class Made { public Collider2D col; public Vector3 pos; public Quaternion rot; public Vector3 scale; }
        static int on = -1;
        static float nextAt;
        static readonly Dictionary<int, Vector3> seen = new Dictionary<int, Vector3>();
        static readonly List<Made> made = new List<Made>();
        static readonly Collider2D[] buf = new Collider2D[64];
        static bool looping;
        static int added, woken, scans;
        static float logAt;

        public static void Tick()
        {
            if (on < 0)
            {
                on = Environment.GetEnvironmentVariable("CUPHEAD_STATICBODIES") == "1" ? 1 : 0;
                if (on == 1) SceneManager.sceneLoaded += (s, m) => { seen.Clear(); made.Clear(); nextAt = Time.realtimeSinceStartup + 1f; looping = false; scans = 0; };
            }
            if (on == 0) return;
            if (!SceneManager.GetActiveScene().name.StartsWith("scene_level_platforming")) return;
            if (!looping && Cuphead.Current != null) { looping = true; Cuphead.Current.StartCoroutine(EndOfFrames()); }
            if (Time.realtimeSinceStartup >= logAt && Environment.GetEnvironmentVariable("CUPHEAD_MEMLOG") == "1")
            {
                logAt = Time.realtimeSinceStartup + 10f;
                UnityEngine.Debug.Log("[cupheadport] static bodies: " + made.Count + " moving colliders have one (" + added + " added), bodies woken in the last 10 s: " + woken);
                woken = 0;
            }
            if (Time.realtimeSinceStartup < nextAt) return;
            scans++;
            nextAt = Time.realtimeSinceStartup + (scans < 20 ? 0.5f : 5f);   // a full scan costs ~10 ms
            var moved = new List<Collider2D>();
            foreach (Collider2D c in UnityEngine.Object.FindObjectsOfType<Collider2D>())
            {
                if (c.attachedRigidbody != null) continue;
                int id = c.GetInstanceID();
                Vector3 p = c.transform.position, before;
                bool m = seen.TryGetValue(id, out before) && (before - p).sqrMagnitude > 1e-6f;
                seen[id] = p;
                if (m) moved.Add(c);
            }
            if (moved.Count == 0) return;
            // every collider of the moving object's hierarchy that has no body yet, deepest first
            var todo = new List<Collider2D>();
            var done = new HashSet<int>();
            foreach (Collider2D c in moved)
                foreach (Collider2D k in c.GetComponentsInChildren<Collider2D>(true))
                    if (k.GetComponent<Rigidbody2D>() == null && done.Add(k.GetInstanceID())) todo.Add(k);
            todo.Sort((a, b) => Depth(b.transform).CompareTo(Depth(a.transform)));
            foreach (Collider2D k in todo)
            {
                if (k == null || k.GetComponent<Rigidbody2D>() != null) continue;
                // a parent with a body would now take this collider: leave such hierarchies alone
                if (k.transform.parent != null && k.transform.parent.GetComponentInParent<Rigidbody2D>() != null) continue;
                Rigidbody2D rb = k.gameObject.AddComponent<Rigidbody2D>();
                if (rb == null) continue;
                rb.bodyType = RigidbodyType2D.Static;
                added++;
                Transform t = k.transform;
                made.Add(new Made { col = k, pos = t.position, rot = t.rotation, scale = t.lossyScale });
            }
        }

        static int Depth(Transform t) { int d = 0; for (; t != null; t = t.parent) d++; return d; }

        // after everything that moves things this frame (scripts, coroutines, animators): wake what the
        // rebuild would have woken
        static IEnumerator EndOfFrames()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                if (on == 0 || !looping) yield break;
                for (int i = made.Count - 1; i >= 0; i--)
                {
                    Made m = made[i];
                    if (m.col == null) { made.RemoveAt(i); continue; }
                    Transform t = m.col.transform;
                    Vector3 p = t.position;
                    if (p == m.pos && t.rotation == m.rot && t.lossyScale == m.scale) continue;
                    if (m.col.enabled && m.col.gameObject.activeInHierarchy)
                    {
                        // the collider's bounds where it was (Box2D's fat AABB: a little larger)
                        // only layers this collider collides with (Box2D makes no contact otherwise), triggers included
                        Bounds b = m.col.bounds;
                        Vector2 center = (Vector2)(b.center - (p - m.pos));
                        var filter = new ContactFilter2D();
                        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(m.col.gameObject.layer));
                        filter.useTriggers = true;
                        int n = Physics2D.OverlapBox(center, (Vector2)b.size + new Vector2(0.2f, 0.2f), 0f, filter, buf);
                        for (int k = 0; k < n; k++)
                        {
                            Rigidbody2D rb = buf[k] != null ? buf[k].attachedRigidbody : null;
                            if (rb != null && rb.bodyType != RigidbodyType2D.Static && rb.IsSleeping()) { rb.WakeUp(); woken++; }
                        }
                    }
                    m.pos = p; m.rot = t.rotation; m.scale = t.lossyScale;
                }
            }
        }
    }
}
