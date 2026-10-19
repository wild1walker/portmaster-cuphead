using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace CupheadPort
{
    // Diagnostics, development player only (Profiler.GetRuntimeMemorySizeLong is 0 in a release
    // player): CUPHEAD_MEMREPORT=<s> logs every <s> seconds Unity's memory counters and, per asset
    // type, the total and the largest loaded objects ("[cupheadport mem]" lines), to see what fills
    // the handheld's 1 GB.
    public static class MemReport
    {
        static float every = -1f, next;

        public static void Tick()
        {
            if (every < 0f)
            {
                float v;
                every = float.TryParse(Environment.GetEnvironmentVariable("CUPHEAD_MEMREPORT") ?? "", out v) ? v : 0f;
                next = Time.realtimeSinceStartup + every;
            }
            if (every <= 0f || Time.realtimeSinceStartup < next) return;
            next = Time.realtimeSinceStartup + every;
            Report();
        }

        static long MB(long b) => b >> 20;

        static void Report()
        {
            UnityEngine.Debug.Log("[cupheadport mem] unity allocated " + MB(Profiler.GetTotalAllocatedMemoryLong()) + " MB, reserved " +
                MB(Profiler.GetTotalReservedMemoryLong()) + " MB, mono used " + MB(Profiler.GetMonoUsedSizeLong()) + " of " +
                MB(Profiler.GetMonoHeapSizeLong()) + " MB");
            Of<Texture2D>(t => t.width + "x" + t.height + " " + t.format);
            Groups();
            Bundles();
            ByType();
            Of<RenderTexture>(t => t.width + "x" + t.height + " " + t.format);
            Of<AudioClip>(a => a.loadType + " " + a.length.ToString("F0") + "s " + a.channels + "ch " + a.frequency + "Hz");
            Of<Mesh>(m => m.vertexCount + " verts");
            Of<AnimationClip>(a => a.length.ToString("F1") + "s");
            Of<Font>(f => "");
            Of<Shader>(s => "");
            Of<Material>(m => "");
            Of<Sprite>(s => "");
        }

        // textures by name (an atlas's pages share one) and how many sprites point at each
        static void Groups()
        {
            var size = new Dictionary<string, long>();
            var count = new Dictionary<string, int>();
            foreach (Texture2D t in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                string n = t.name;
                long sz = Profiler.GetRuntimeMemorySizeLong(t);
                long v; size[n] = (size.TryGetValue(n, out v) ? v : 0) + sz;
                int c; count[n] = (count.TryGetValue(n, out c) ? c : 0) + 1;
            }
            var sprites = new Dictionary<string, int>();
            foreach (Sprite sp in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                string n = sp.texture != null ? sp.texture.name : "(none)";
                int c; sprites[n] = (sprites.TryGetValue(n, out c) ? c : 0) + 1;
            }
            var keys = new List<string>(size.Keys);
            keys.Sort((a, b) => size[b].CompareTo(size[a]));
            var sb = new StringBuilder("[cupheadport mem] textures by name:");
            foreach (string k in keys)
            {
                if (size[k] < (256 << 10)) break;
                int sc; sprites.TryGetValue(k, out sc);
                sb.Append(" " + k + " " + count[k] + "x " + MB(size[k]) + "MB " + sc + "spr;");
            }
            UnityEngine.Debug.Log(sb.ToString());
        }

        // every loaded object by type: where Unity's own memory goes
        static void ByType()
        {
            var size = new Dictionary<string, long>();
            var count = new Dictionary<string, int>();
            foreach (UnityEngine.Object o in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
            {
                string n = o.GetType().Name;
                long v; size[n] = (size.TryGetValue(n, out v) ? v : 0) + Profiler.GetRuntimeMemorySizeLong(o);
                int c; count[n] = (count.TryGetValue(n, out c) ? c : 0) + 1;
            }
            var keys = new List<string>(size.Keys);
            keys.Sort((a, b) => size[b].CompareTo(size[a]));
            var sb = new StringBuilder("[cupheadport mem] by type:");
            for (int i = 0; i < keys.Count && i < 30; i++) sb.Append(" " + keys[i] + " " + count[keys[i]] + "x " + (size[keys[i]] >> 10) + "K;");
            UnityEngine.Debug.Log(sb.ToString());
        }

        static void Bundles()
        {
            try
            {
                string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                UnityEngine.Debug.Log("[cupheadport mem] atlases loaded: " + string.Join(", ", AssetLoader<UnityEngine.U2D.SpriteAtlas>.DEBUG_GetLoadedAssets().ToArray()));
                UnityEngine.Debug.Log("[cupheadport mem] preload for " + scene + ": " + string.Join(", ", AssetLoader<UnityEngine.U2D.SpriteAtlas>.GetPreloadAssetNames(scene)));
            }
            catch (Exception e) { UnityEngine.Debug.Log("[cupheadport mem] atlas list: " + e.Message); }
            var sb = new StringBuilder("[cupheadport mem] loaded bundles:");
            int n = 0;
            foreach (AssetBundle b in AssetBundle.GetAllLoadedAssetBundles()) { sb.Append(" " + b.name); n++; }
            UnityEngine.Debug.Log(sb.Append(" (" + n + ")").ToString());
        }

        static void Of<T>(Func<T, string> what) where T : UnityEngine.Object
        {
            var list = new List<KeyValuePair<long, T>>();
            long total = 0;
            foreach (T o in Resources.FindObjectsOfTypeAll<T>())
            {
                long sz = Profiler.GetRuntimeMemorySizeLong(o);
                total += sz;
                list.Add(new KeyValuePair<long, T>(sz, o));
            }
            list.Sort((a, b) => b.Key.CompareTo(a.Key));
            var sb = new StringBuilder("[cupheadport mem] " + typeof(T).Name + ": " + list.Count + " objects, " + MB(total) + " MB;");
            for (int i = 0; i < list.Count && i < 8; i++)
                sb.Append(" " + list[i].Value.name + " " + (list[i].Key >> 10) + "K " + what(list[i].Value) + ";");
            UnityEngine.Debug.Log(sb.ToString());
        }
    }
}
