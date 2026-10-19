using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CupheadPort
{
    // Diagnostics, CUPHEAD_COLSTATS=1: why the 2D physics engine rebuilds collider shapes every step
    // (Treetop Trouble: ~57 created and destroyed a step). Every 20 s, for 120 frames, every 2D
    // collider is compared with the frame before: created, destroyed, switched on or off, moved to
    // another rigidbody, resized (bounds size) or rescaled (lossyScale). Logged per object name.
    public static class ColStats
    {
        sealed class Seen { public bool enabled; public Vector3 size, scale, pos; public Rigidbody2D body; public string name; public Collider2D col; }
        static int on = -1;
        static float nextAt;
        static int left;
        static Dictionary<int, Seen> last = new Dictionary<int, Seen>();
        static readonly Dictionary<string, int[]> tally = new Dictionary<string, int[]>();   // created, destroyed, toggled, body, resized, rescaled, moved without a rigidbody, moved with one
        static readonly string[] what = { "created", "destroyed", "toggled", "body", "resized", "rescaled", "moved-static", "moved-body" };

        static string Key(Collider2D c)
        {
            string n = c.gameObject.name.Replace("(Clone)", "").Trim();
            return n + " [" + c.GetType().Name + "]";
        }

        static void Count(string k, int i)
        {
            int[] t; if (!tally.TryGetValue(k, out t)) tally[k] = t = new int[8];
            t[i]++;
        }

        public static void Tick()
        {
            if (on < 0)
            {
                on = Environment.GetEnvironmentVariable("CUPHEAD_COLSTATS") == "1" ? 1 : 0;
                nextAt = Time.realtimeSinceStartup + 20f;
            }
            if (on == 0) return;
            if (left == 0)
            {
                if (Time.realtimeSinceStartup < nextAt) return;
                left = 121; last.Clear(); tally.Clear();
            }
            var now = new Dictionary<int, Seen>();
            foreach (Collider2D c in UnityEngine.Object.FindObjectsOfType<Collider2D>())
            {
                var s = new Seen { enabled = c.enabled, size = c.bounds.size, scale = c.transform.lossyScale, pos = c.transform.position, body = c.attachedRigidbody, name = Key(c), col = c };
                int id = c.GetInstanceID();
                now[id] = s;
                if (left == 121) continue;                           // the first frame only records
                Seen p;
                if (!last.TryGetValue(id, out p)) { Count(s.name, 0); continue; }
                if (p.enabled != s.enabled) Count(s.name, 2);
                if (p.body != s.body) Count(s.name, 3);
                if (s.enabled && (p.size - s.size).sqrMagnitude > 1e-6f) Count(s.name, 4);
                if ((p.scale - s.scale).sqrMagnitude > 1e-8f) Count(s.name, 5);
                if (s.enabled && (p.pos - s.pos).sqrMagnitude > 1e-6f) Count(s.name, s.body == null ? 6 : 7);
            }
            if (left != 121)
                foreach (var kv in last)
                    if (!now.ContainsKey(kv.Key)) Count(kv.Value.name, 1);
            last = now;
            if (--left > 0) return;
            nextAt = Time.realtimeSinceStartup + 20f;
            var rows = new List<KeyValuePair<string, int[]>>(tally);
            // the costly kind first: colliders without a rigidbody that move (Box2D rebuilds their shapes)
            rows.Sort((a, b) => b.Value[6] != a.Value[6] ? b.Value[6].CompareTo(a.Value[6]) : Sum(b.Value).CompareTo(Sum(a.Value)));
            var sb = new StringBuilder("[cupheadport] collider changes in 120 frames, scene " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name +
                " (" + now.Count + " colliders now):");
            for (int i = 0; i < rows.Count && i < 25; i++)
            {
                sb.Append("\n  " + Sum(rows[i].Value) + "  " + rows[i].Key + ":");
                for (int j = 0; j < 8; j++) if (rows[i].Value[j] > 0) sb.Append(" " + what[j] + " " + rows[i].Value[j]);
                if (rows[i].Value[6] > 0) sb.Append("  | " + Describe(rows[i].Key, now));
            }
            UnityEngine.Debug.Log(sb.ToString());
        }

        // components of one such object, its parents (with their scripts) and the trigger flag
        static string Describe(string key, Dictionary<int, Seen> now)
        {
            foreach (var kv in now)
            {
                if (kv.Value.name != key || kv.Value.col == null) continue;
                Collider2D c = kv.Value.col;
                var sb = new StringBuilder(c.isTrigger ? "trigger;" : "solid;");
                foreach (Component k in c.GetComponents<Component>()) if (!(k is Transform)) sb.Append(" " + k.GetType().Name);
                for (Transform t = c.transform.parent; t != null; t = t.parent)
                {
                    sb.Append(" < " + t.name.Replace("(Clone)", "").Trim() + "(");
                    foreach (MonoBehaviour b in t.GetComponents<MonoBehaviour>()) if (b != null) sb.Append(b.GetType().Name + ",");
                    sb.Append(")");
                }
                return sb.ToString();
            }
            return "";
        }

        static int Sum(int[] t) { int s = 0; foreach (int v in t) s += v; return s; }
    }
}
