using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CupheadPort
{
    // Diagnostics, CUPHEAD_SCENESTATS=1: 15 s after each scene load and every 30 s after that, logs the
    // cameras and how many engine components are active (what Unity updates, culls and sorts every
    // frame), to see where the engine's per-frame work comes from.
    public static class SceneStats
    {
        static int on = -1;
        static float nextAt = -1f;

        public static void SceneLoaded()
        {
            if (on == 1) nextAt = Time.realtimeSinceStartup + 15f;
        }

        // A camera's other components (image effects, GUILayer, ...), enabled ones only.
        static string Parts(GameObject go)
        {
            var sb = new StringBuilder(" parts:");
            foreach (Component k in go.GetComponents<Component>())
            {
                if (k == null || k is Transform || k is Camera) continue;
                Behaviour b = k as Behaviour;
                if (b != null && !b.enabled) continue;
                sb.Append(' ').Append(k.GetType().Name);
            }
            return sb.ToString();
        }

        public static void Tick()
        {
            if (on < 0)
            {
                on = Environment.GetEnvironmentVariable("CUPHEAD_SCENESTATS") == "1" ? 1 : 0;
                if (on == 1) nextAt = Time.realtimeSinceStartup + 15f;
            }
            if (on == 0 || nextAt < 0f || Time.realtimeSinceStartup < nextAt) return;
            nextAt = Time.realtimeSinceStartup + 10f;
            var sb = new StringBuilder("[cupheadport scenestats] scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            foreach (Camera c in Camera.allCameras)
                sb.Append("\n  camera " + c.name + " depth=" + c.depth + " mask=" + c.cullingMask.ToString("x") + " eventMask=" + c.eventMask.ToString("x") +
                          " clear=" + c.clearFlags + " rt=" + (c.targetTexture != null) + " rect=" + c.rect + " ortho=" + c.orthographic + Parts(c.gameObject));
            int sr = 0, srVis = 0; long verts = 0;
            var big = new List<KeyValuePair<int, string>>();
            foreach (SpriteRenderer r in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>())
                if (r.enabled)
                {
                    sr++;
                    if (!r.isVisible || r.sprite == null) continue;
                    srVis++;
                    int v = r.sprite.vertices.Length; verts += v;
                    big.Add(new KeyValuePair<int, string>(v, r.gameObject.name + "/" + r.sprite.name + " " + r.drawMode + " " + r.sharedMaterial.name));
                }
            big.Sort((a, b) => b.Key.CompareTo(a.Key));
            sb.Append("\n  visible sprites " + srVis + ", vertices " + verts + "; largest:");
            for (int i = 0; i < big.Count && i < 6; i++) sb.Append(" " + big[i].Key + " " + big[i].Value + ";");
            int an = 0, anVis = 0; var cull = new Dictionary<AnimatorCullingMode, int>();
            foreach (Animator a in UnityEngine.Object.FindObjectsOfType<Animator>())
                if (a.enabled)
                {
                    an++;
                    int n; cull.TryGetValue(a.cullingMode, out n); cull[a.cullingMode] = n + 1;
                    Renderer rr = a.GetComponent<Renderer>(); if (rr != null && rr.isVisible) anVis++;
                }
            int au = 0, auPlaying = 0;
            foreach (AudioSource s in UnityEngine.Object.FindObjectsOfType<AudioSource>())
                if (s.enabled) { au++; if (s.isPlaying) auPlaying++; }
            int col = 0, colTrig = 0;
            foreach (Collider2D c in UnityEngine.Object.FindObjectsOfType<Collider2D>())
                if (c.enabled) { col++; if (c.isTrigger) colTrig++; }
            int rb = UnityEngine.Object.FindObjectsOfType<Rigidbody2D>().Length;
            int mb = 0;
            var gui = new Dictionary<string, int>();      // enabled behaviours with an OnGUI method (Unity runs its GUI pass for them)
            foreach (MonoBehaviour b in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
                if (b.enabled)
                {
                    mb++;
                    for (Type t = b.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                        if (t.GetMethod("OnGUI", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly) != null)
                        {
                            string k = b.GetType().FullName + " on " + b.gameObject.name;
                            int n; gui.TryGetValue(k, out n); gui[k] = n + 1;
                            break;
                        }
                }
            foreach (var kv in gui) sb.Append("\n  OnGUI: " + kv.Key + (kv.Value > 1 ? " x" + kv.Value : ""));
            // every behaviour in memory with an OnGUI (inactive, DontDestroyOnLoad, prefabs too)
            foreach (MonoBehaviour b in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (b == null) continue;
                for (Type t = b.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                    if (t.GetMethod("OnGUI", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly) != null)
                    {
                        sb.Append("\n  OnGUI (all): " + b.GetType().FullName + " on " + b.gameObject.name + " enabled=" + b.enabled +
                                  " active=" + b.gameObject.activeInHierarchy + " scene=" + (b.gameObject.scene.IsValid() ? b.gameObject.scene.name : "(asset)"));
                        break;
                    }
            }
            // what the scene loader's camera (layer 10) would draw: every active renderer/graphic on layer 10,
            // and the loader's fader and icon
            var l10 = new StringBuilder();
            foreach (Renderer r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                if (r.gameObject.layer == 10 && r.enabled) l10.Append(" " + r.GetType().Name + ":" + r.gameObject.name);
            foreach (UnityEngine.UI.Graphic g in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Graphic>())
                if (g.gameObject.layer == 10 && g.enabled)
                {
                    var im = g as UnityEngine.UI.Image;
                    l10.Append(" " + g.GetType().Name + ":" + g.gameObject.name + "(a=" + g.color.a.ToString("F2") +
                               (im != null ? " sprite=" + (im.sprite != null ? im.sprite.name : "none") : "") + ")");
                }
            sb.Append("\n  layer 10:" + l10 + " | loading=" + SceneLoader.CurrentlyLoading + " iris=" + SceneLoader.IsInIrisTransition + " blur=" + SceneLoader.IsInBlurTransition);
            int go = UnityEngine.Object.FindObjectsOfType<Transform>().Length;
            int canv = UnityEngine.Object.FindObjectsOfType<Canvas>().Length;
            int ps = UnityEngine.Object.FindObjectsOfType<ParticleSystem>().Length;
            var cs = new StringBuilder();
            foreach (var kv in cull) cs.Append(kv.Key + ":" + kv.Value + " ");
            sb.Append("\n  active transforms " + go + ", sprite renderers " + sr + " (visible " + srVis + "), animators " + an + " (visible " + anVis + "; " + cs +
                      "), audio sources " + au + " (playing " + auPlaying + "), 2D colliders " + col + " (triggers " + colTrig + "), rigidbodies " + rb +
                      ", behaviours " + mb + ", canvases " + canv + ", particle systems " + ps);
            UnityEngine.Debug.Log(sb.ToString());
        }
    }
}
