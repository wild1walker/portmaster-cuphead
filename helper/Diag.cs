using System.Collections.Generic;
using UnityEngine;

namespace CupheadPort
{
    // Test builds only (codepatch --test-hooks, test-find-sites): call counts per call site.
    public static class Diag
    {
        static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        static float next = -1f;

        static readonly Dictionary<string, int> pairs = new Dictionary<string, int>();
        static float pairsNext = -1f;

        static string N(GameObject g) { return g == null ? "?" : g.name.Replace("(Clone)", "").Trim(); }

        // test-trigger-pairs: AbstractCollidableObject.checkCollision(col, phase)
        public static void Pair(object self, Collider2D col, object phase)
        {
            Component c = self as Component;
            string k = (c != null ? N(c.gameObject) : "?") + " <- " + (col != null ? N(col.gameObject) : "?") + " " + phase;
            int n; pairs.TryGetValue(k, out n); pairs[k] = n + 1;
            if (pairsNext < 0f) pairsNext = Time.realtimeSinceStartup + 20f;
            if (Time.realtimeSinceStartup < pairsNext) return;
            pairsNext = Time.realtimeSinceStartup + 20f;
            var l = new List<KeyValuePair<string, int>>(pairs);
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new System.Text.StringBuilder("[cupheadport pairs] last 20 s, scene " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ":");
            for (int i = 0; i < l.Count && i < 40; i++) sb.Append("\n  " + l[i].Value + "  " + l[i].Key);
            pairs.Clear();
            UnityEngine.Debug.Log(sb.ToString());
        }

        public static void Site(string name)
        {
            int n; counts.TryGetValue(name, out n); counts[name] = n + 1;
            if (next < 0f) next = Time.realtimeSinceStartup + 20f;
            if (Time.realtimeSinceStartup < next) return;
            next = Time.realtimeSinceStartup + 20f;
            var sb = new System.Text.StringBuilder("[cupheadport sites] last 20 s:");
            foreach (var kv in counts) sb.Append(" " + kv.Key + "=" + kv.Value);
            counts.Clear();
            UnityEngine.Debug.Log(sb.ToString());
        }
    }
}
