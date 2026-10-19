using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CupheadPort
{
    // CUPHEAD_DECOR (default on in the run-and-gun levels; =1 the world maps too, =0 off): decoration far
    // off screen is switched off (SetActive(false)) and
    // back on before it can be seen, so Unity stops updating its transforms, sprites, animators and
    // scripts meanwhile. The run-and-gun levels are built from hundreds of such pieces (Forest Follies:
    // 225 parallax pieces, ~400 sprites, 28 of them on screen). Decoration here is strictly: a parallax
    // piece (PlatformingLevelParallax) or a scene root whose whole hierarchy holds only Transforms,
    // SpriteRenderers, Animators and the game's PlatformingLevelParallax(Child) / AnimationHelper: no
    // collider, sound, particle, camera, UI or other script. A parallax piece's position is a function
    // of the camera's (the game's formula), so it is put where it belongs as it comes back.
    // CUPHEAD_DECOR=log only counts what it would switch off. RGSP: Forest Follies +4..6%, Treetop +4%.
    public static class Decor
    {
        sealed class Entry
        {
            public GameObject go; public Transform tr; public PlatformingLevelParallax px;
            public float speed, ySpeed; public Vector3 basePos, staticPos;
            public float x0, x1, y0, y1; public bool off;
        }

        static int mode = -1;
        static bool withMaps;
        static float scanAt = -1f;
        static int frame;
        static readonly List<Entry> entries = new List<Entry>();
        static PropertyInfo layerProp;
        static FieldInfo speedField;
        static int switchedOff, switchedOn;

        static readonly HashSet<Type> allowedScripts = new HashSet<Type> {
            typeof(PlatformingLevelParallax), typeof(PlatformingLevelParallaxChild), typeof(AnimationHelper) };

        public static void Tick()
        {
            if (mode < 0)
            {
                string v = Environment.GetEnvironmentVariable("CUPHEAD_DECOR");
                // default: the run-and-gun levels (measured there); =1 also the world maps
                mode = v == "0" ? 0 : v == "log" ? 2 : 1;
                withMaps = v == "1" || v == "log";
                if (mode == 0) return;
                SceneManager.sceneLoaded += (s, m) => { entries.Clear(); scanAt = Time.realtimeSinceStartup + 3f; };
                scanAt = Time.realtimeSinceStartup + 3f;
            }
            if (mode == 0) return;
            if (scanAt > 0f && Time.realtimeSinceStartup >= scanAt) { scanAt = -1f; Scan(); }
            if (entries.Count == 0 || ++frame % 4 != 0) return;
            Camera cam = ViewCamera();
            if (cam == null) return;
            Vector3 c = cam.transform.position;
            float halfH = cam.orthographicSize, halfW = halfH * 1.7777778f;
            float reachX = halfW * 2f, reachY = halfH * 2f;             // the view plus a screen on each side
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e.go == null) { entries.RemoveAt(i--); continue; }
                // a parallax piece follows the camera while its component is enabled (the game freezes
                // some on purpose: Funfair Fever's clouds, Rugged Ridge's elevator backgrounds), and the
                // game may move its base (UpdateBasePosition): read both now
                bool follows = e.px != null && e.px.enabled;
                Vector3 p = follows
                    ? new Vector3(e.px.basePos.x + c.x * e.speed, e.px.basePos.y + c.y * e.ySpeed, e.staticPos.z)
                    : e.px != null ? e.tr.position : e.staticPos;
                bool near = p.x + e.x1 > c.x - reachX && p.x + e.x0 < c.x + reachX && p.y + e.y1 > c.y - reachY && p.y + e.y0 < c.y + reachY;
                if (near == !e.off) continue;
                if (mode == 2) { e.off = !near; if (!near) switchedOff++; else switchedOn++; continue; }
                if (near)
                {
                    if (follows) e.tr.position = p;                       // where its own LateUpdate would put it
                    e.go.SetActive(true); e.off = false; switchedOn++;
                }
                else
                {
                    if (!e.go.activeSelf) { entries.RemoveAt(i--); continue; }   // switched off by the game itself: not ours
                    e.go.SetActive(false); e.off = true; switchedOff++;
                }
            }
            if (mode == 2 && frame % 600 == 0)
                UnityEngine.Debug.Log("[cupheadport] decor (log only): " + entries.Count + " pieces, " + Count(true) + " would be off now");
        }

        static int Count(bool off) { int n = 0; foreach (Entry e in entries) if (e.off == off) n++; return n; }

        static Camera ViewCamera()
        {
            CupheadLevelCamera lc = CupheadLevelCamera.Current;
            if (lc != null && lc.camera != null) return lc.camera;
            Camera m = Camera.main;
            return m != null && m.orthographic ? m : null;
        }

        static void Scan()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (!scene.StartsWith("scene_level_platforming") && !(withMaps && scene.StartsWith("scene_map"))) return;
            entries.Clear();
            int sprites = 0;
            var seen = new HashSet<int>();
            foreach (PlatformingLevelParallax px in UnityEngine.Object.FindObjectsOfType<PlatformingLevelParallax>())
                if (seen.Add(px.gameObject.GetInstanceID())) sprites += Add(px.gameObject, px);
            foreach (GameObject root in scene.Length > 0 ? SceneManager.GetActiveScene().GetRootGameObjects() : new GameObject[0])
                if (root.activeInHierarchy && seen.Add(root.GetInstanceID()) && root.GetComponentInChildren<PlatformingLevelParallax>(true) == null)
                    sprites += Add(root, null);
            UnityEngine.Debug.Log("[cupheadport] decor: " + entries.Count + " pieces of decoration (" + sprites + " sprites) in " + scene +
                (mode == 2 ? " (log only)" : ""));
        }

        // Adds a piece if its hierarchy is decoration only; returns its sprite count (0 = not added).
        static int Add(GameObject go, PlatformingLevelParallax px)
        {
            if (!go.activeInHierarchy) return 0;
            int sprites = 0;
            foreach (Component k in go.GetComponentsInChildren<Component>(true))
            {
                if (k == null || k is Transform || k is Animator) continue;
                if (k is SpriteRenderer) { sprites++; continue; }
                if (k is MonoBehaviour && allowedScripts.Contains(k.GetType())) continue;
                return 0;
            }
            if (sprites == 0) return 0;
            var e = new Entry { go = go, tr = go.transform, px = px };
            e.staticPos = e.tr.position;
            if (px != null)
            {
                if (layerProp == null)
                {
                    layerProp = typeof(PlatformingLevelParallax).GetProperty("LayerProperties", BindingFlags.Instance | BindingFlags.NonPublic);
                    speedField = layerProp != null ? layerProp.PropertyType.GetField("speed") : null;
                }
                if (layerProp == null || speedField == null) return 0;
                object layer = layerProp.GetValue(px, null);
                if (layer == null) return 0;
                e.speed = (float)speedField.GetValue(layer);
                e.ySpeed = px.overrideLayerYSpeed ? px.overrideYSpeed : e.speed;
                e.basePos = px.basePos;
            }
            bool first = true;
            foreach (SpriteRenderer r in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Bounds b = r.bounds;
                float a0 = b.min.x - e.staticPos.x, a1 = b.max.x - e.staticPos.x, c0 = b.min.y - e.staticPos.y, c1 = b.max.y - e.staticPos.y;
                if (first) { e.x0 = a0; e.x1 = a1; e.y0 = c0; e.y1 = c1; first = false; }
                else { e.x0 = Mathf.Min(e.x0, a0); e.x1 = Mathf.Max(e.x1, a1); e.y0 = Mathf.Min(e.y0, c0); e.y1 = Mathf.Max(e.y1, c1); }
            }
            entries.Add(e);
            return sprites;
        }
    }
}
