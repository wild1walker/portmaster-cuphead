using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.U2D;

namespace CupheadPort
{
    // Memory for the DLC's multi-phase bosses on a 1 GB handheld. Cuphead preloads every phase's
    // sprite atlases with the level and keeps them until the next scene: Saltbaker's sixteen atlases
    // are 286 MB of textures, and with Unity's ~170 MB and the system the RGSP runs out of memory
    // (zram swapping, the Mali driver's page allocation failures: multi-second frames in phases
    // two to four). A boss's atlases are named <boss>_Phase<n>...; once the fight is in a later
    // phase, and for three checks in a row (two seconds apart) no sprite on screen uses an atlas
    // (renderers and UI images), that atlas and its bundle are unloaded.
    //
    // Nothing changes on screen: the phases never come back within a fight. A sprite that still
    // asked for an unloaded atlas would get it back through the game's own late binding
    // (SpriteAtlasLoader), logged as "requested again". Before a retry (SceneLoader.ReloadLevel,
    // and early, when the death card comes up) the unloaded atlases are loaded again through the
    // game's AssetLoader, and the scene loader waits for them (AssetBundleLoader.loadCounter) as it
    // does for its own preloads, so the retried fight starts as the first one did.
    // CUPHEAD_ATLASTRIM=0 turns it off; =log only logs what would be unloaded.
    public static class AtlasTrim
    {
        // level class -> its boss states in phase order (index + 1 = the atlases' phase number)
        static readonly Dictionary<string, string[]> Phases = new Dictionary<string, string[]>
        {
            { "SaltbakerLevel", new[] { "Main", "PhaseTwo", "PhaseThree", "PhaseFour" } },
            { "OldManLevel", new[] { "Main", "SockPuppet", "GnomeLeader" } },
        };

        static int mode = -1;                 // 0 off, 1 on, 2 log only
        static Level level;
        static string[] states;
        static float nextCheck;
        static readonly Dictionary<string, int> idle = new Dictionary<string, int>();     // atlas -> checks in a row unused
        static readonly Dictionary<string, int> busy = new Dictionary<string, int>();     // atlas -> checks it was on screen
        static readonly List<string> ready = new List<string>();                           // unused long enough: unloaded one at a time
        static readonly List<string> trimmed = new List<string>();                         // unloaded in this level (reloaded before a retry)
        static readonly HashSet<string> logged = new HashSet<string>();
        static FieldInfo fInstance, fLoaded, fBundleInstance, fBundles;
        static PropertyInfo pState;
        static object props;

        static bool On()
        {
            if (mode < 0)
            {
                string v = Environment.GetEnvironmentVariable("CUPHEAD_ATLASTRIM");
                mode = v == "0" ? 0 : v == "log" ? 2 : 1;
                var al = typeof(AssetLoader<SpriteAtlas>);
                fInstance = al.GetField("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                fLoaded = al.GetField("loadedAssets", BindingFlags.Instance | BindingFlags.NonPublic);
                fBundleInstance = typeof(AssetBundleLoader).GetField("Instance", BindingFlags.Static | BindingFlags.NonPublic);
                fBundles = typeof(AssetBundleLoader).GetField("loadedBundles", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fInstance == null || fLoaded == null || fBundleInstance == null || fBundles == null)
                {
                    UnityEngine.Debug.Log("[cupheadport] atlas trim: loader fields not found, off");
                    mode = 0;
                }
            }
            return mode > 0;
        }

        static IDictionary Loaded() => fLoaded.GetValue(fInstance.GetValue(null)) as IDictionary;
        static IDictionary Bundles() => fBundles.GetValue(fBundleInstance.GetValue(null)) as IDictionary;

        // Diagnostics.Tick, every frame
        public static void Tick()
        {
            if (!On()) return;
            Level l = Level.Current;
            if (l != level)
            {
                level = l; props = null; pState = null; idle.Clear(); busy.Clear(); ready.Clear();
                states = l != null && Phases.ContainsKey(l.GetType().Name) ? Phases[l.GetType().Name] : null;
                if (states == null) trimmed.Clear();          // another scene: the game unloads everything itself
            }
            if (states == null || Time.realtimeSinceStartup < nextCheck) return;
            if (ready.Count > 0)
            {
                // one atlas per 0.3 s: destroying a bundle's textures takes a while, several at once stall a frame
                IDictionary ld = Loaded();
                string r = ready[0];
                ready.RemoveAt(0);
                if (ld != null && ld.Contains(r)) Unload(r, ld, Phase(l));
                nextCheck = Time.realtimeSinceStartup + 0.3f;
                return;
            }
            nextCheck = Time.realtimeSinceStartup + 2f;   // the on-screen check searches the scene: not often
            int phase = Phase(l);
            if (phase < 2) return;
            // (an atlas seen on screen in 20 checks of a later phase stays: no more searches for it)
            string prefix = l.GetType().Name.Replace("Level", "") + "_";
            IDictionary loaded = Loaded();
            if (loaded == null) return;
            var names = new List<string>();
            foreach (object k in loaded.Keys) names.Add((string)k);
            foreach (string t in trimmed)
                if (loaded.Contains(t) && logged.Add("again:" + t))
                    UnityEngine.Debug.Log("[cupheadport] atlas trim: " + t + " requested again in phase " + phase);
            HashSet<string> inUse = null;
            foreach (string n in names)
            {
                int p = AtlasPhase(n, prefix);
                if (p <= 0 || p >= phase || trimmed.Contains(n)) continue;
                int b;
                if (busy.TryGetValue(n, out b) && b >= 20) continue;          // still on screen in a later phase (a background): keep it
                if (inUse == null) inUse = InUse();
                int c;
                if (inUse.Contains(n)) busy[n] = (busy.TryGetValue(n, out b) ? b : 0) + 1;
                idle[n] = inUse.Contains(n) ? 0 : (idle.TryGetValue(n, out c) ? c : 0) + 1;
                if (idle[n] < 3) continue;
                if (mode == 2) { if (logged.Add("would:" + n)) UnityEngine.Debug.Log("[cupheadport] atlas trim: would unload " + n + " in phase " + phase); continue; }
                if (!ready.Contains(n)) ready.Add(n);
            }
        }

        // phase number in an atlas name "<Boss>_Phase<n>..." or "<Boss>_Background_Phase<n>", 0 if none
        static int AtlasPhase(string name, string prefix)
        {
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return 0;
            int i = name.IndexOf("Phase", prefix.Length, StringComparison.OrdinalIgnoreCase);
            if (i < 0 || i + 5 >= name.Length || !char.IsDigit(name[i + 5])) return 0;
            return name[i + 5] - '0';
        }

        static int Phase(Level l)
        {
            if (props == null)
            {
                for (Type t = l.GetType(); t != null && props == null; t = t.BaseType)
                {
                    var f = t.GetField("properties", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (f != null) props = f.GetValue(l);
                }
                if (props == null) return 0;
                pState = props.GetType().GetProperty("CurrentState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            object st = pState != null ? pState.GetValue(props, null) : null;
            string name = st?.GetType().GetField("stateName")?.GetValue(st)?.ToString();
            return name == null ? 0 : Array.IndexOf(states, name) + 1;
        }

        // atlases whose sprites are on screen now (sprite renderers and UI images of active objects)
        static HashSet<string> InUse()
        {
            var tex = new HashSet<Texture2D>();
            foreach (SpriteRenderer r in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>())
                if (r.enabled && r.sprite != null && r.sprite.texture != null) tex.Add(r.sprite.texture);
            foreach (UnityEngine.UI.Image im in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Image>())
                if (im.enabled && im.sprite != null && im.sprite.texture != null) tex.Add(im.sprite.texture);
            var names = new HashSet<string>();
            IDictionary loaded = Loaded();
            foreach (Texture2D t in tex)
                foreach (object k in loaded.Keys)
                    if (t.name.IndexOf("-" + (string)k + "-", StringComparison.Ordinal) >= 0) names.Add((string)k);
            return names;
        }

        static void Unload(string name, IDictionary loaded, int phase)
        {
            object container = loaded[name];
            var asset = Member(container, "asset") as UnityEngine.Object;
            loaded.Remove(name);
            if (asset != null) UnityEngine.Object.Destroy(asset);
            IDictionary bundles = Bundles();
            string bn = AssetBundleLoader.AssetBundlePrefixSpriteAtlas + name.ToLowerInvariant();
            if (bundles != null && bundles.Contains(bn))
            {
                object bc = bundles[bn];
                var ab = Member(bc, "assetBundle") as AssetBundle;
                bundles.Remove(bn);
                if (ab != null) ab.Unload(true);
            }
            trimmed.Add(name);
            UnityEngine.Debug.Log("[cupheadport] atlas trim: unloaded " + name + " in phase " + phase);
        }

        static object Member(object o, string name)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo f = o.GetType().GetField(name, all);
            if (f != null) return f.GetValue(o);
            PropertyInfo p = o.GetType().GetProperty(name, all);
            return p != null ? p.GetValue(o, null) : null;
        }

        // start of SceneLoader.ReloadLevel and of LevelGameOverGUI.In: load the trimmed atlases again
        public static void BeforeReload()
        {
            if (!On() || trimmed.Count == 0) return;
            IDictionary loaded = Loaded();
            int n = 0;
            foreach (string t in trimmed)
                if (loaded == null || !loaded.Contains(t)) { AssetLoader<SpriteAtlas>.LoadAsset(t, AssetLoaderOption.None()); n++; }
            UnityEngine.Debug.Log("[cupheadport] atlas trim: loading " + n + " atlases again for a retry");
            trimmed.Clear();
            idle.Clear();
            ready.Clear();
        }
    }
}
