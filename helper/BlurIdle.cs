using System;
using System.Collections.Generic;
using UnityEngine;

namespace CupheadPort
{
    // Glumstone the Giant (OldManLevel) has a GeeseCamera that draws the foreground geese (layer 31,
    // set by OldManLevelGoose.Init) into GeeseFGTexture through BlurOptimized: six full-screen passes,
    // ~16 ms of GPU time a frame on the H700, in every frame of the fight, though foreground geese fly
    // only during phase one's goose attacks. With no goose in the camera's view the camera draws
    // nothing, so its image is the cleared colour, and BlurOptimized's passes (weights summing to 1,
    // the centre tap's alpha) turn a uniform image into the same image: a plain copy gives the same
    // pixels. The quad that shows the texture (GeeseTexture, Unlit/Transparent: blending a transparent
    // black texel leaves the screen as it was) is switched off meanwhile and back on with the first
    // goose. Codepatch bluroptimized-idle calls Skip at the start of BlurOptimized.OnRenderImage and
    // returns when it copied; codepatch geese-track calls Track from OldManLevelGoose.Init.
    //
    // Skip copies only in OldManLevel, for an orthographic camera that clears to a colour and draws
    // layer 31 alone, and only while that layer holds nothing but tracked renderers: the scene is
    // searched for others (in the game's code only the geese and, in other levels, the
    // Chalice tutorial and the chess knight use layer 31); when one turns up the camera is blurred
    // as usual until the next scene load (logged). The search runs when the camera is first seen and
    // then every 30 s (a full search costs tens of ms on the handheld). CUPHEAD_BLURIDLE=0 turns it off.
    public static class BlurIdle
    {
        static int on = -1;
        static readonly List<Renderer> tracked = new List<Renderer>();
        static readonly Dictionary<int, Camera> cams = new Dictionary<int, Camera>();       // BlurOptimized -> its camera
        static readonly Dictionary<int, int> state = new Dictionary<int, int>();             // camera -> 1 gated, 0 untracked layers
        static readonly Dictionary<int, float> scanAt = new Dictionary<int, float>();
        static readonly Dictionary<int, List<Renderer>> shows = new Dictionary<int, List<Renderer>>();   // camera -> quads showing its texture
        static readonly List<Renderer> hidden = new List<Renderer>();                                     // quads switched off here
        static int lastIdle = -1;                                                                          // for the log
        const int GeeseMask = 1 << 31;
        const float Margin = 64f;    // world units around the view (culling uses the same bounds)

        static bool On()
        {
            if (on < 0)
            {
                on = Environment.GetEnvironmentVariable("CUPHEAD_BLURIDLE") == "0" ? 0 : 1;
                if (on == 1)
                    UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => { lastIdle = -1; state.Clear(); scanAt.Clear(); cams.Clear(); shows.Clear(); hidden.Clear(); };
            }
            return on == 1;
        }

        // start of OldManLevelGoose.Init (the goose's layer is set later in Init: read at Skip time)
        public static void Track(MonoBehaviour c)
        {
            if (!On() || c == null) return;
            foreach (Renderer r in c.GetComponentsInChildren<Renderer>(true))
                if (!tracked.Contains(r)) tracked.Add(r);
        }

        // start of BlurOptimized.OnRenderImage: true when it copied source to destination instead
        public static bool Skip(MonoBehaviour blur, RenderTexture source, RenderTexture destination)
        {
            if (!On() || blur == null || !(Level.Current is OldManLevel)) return false;
            int id = blur.GetInstanceID();
            Camera c;
            if (!cams.TryGetValue(id, out c)) { c = blur.GetComponent<Camera>(); cams[id] = c; }
            if (c == null || !c.orthographic || c.cullingMask != GeeseMask || c.clearFlags != CameraClearFlags.SolidColor) return false;
            int cid = c.GetInstanceID();
            if (!Scan(c, cid)) { ShowQuads(); return false; }
            if (AnyInView(c)) { ShowQuads(); Note(c, 0); return false; }
            Note(c, 1);
            Graphics.Blit(source, destination);
            List<Renderer> q;
            if (shows.TryGetValue(cid, out q) && IsTransparentBlend(c))
                foreach (Renderer r in q)
                    if (r != null && r.enabled) { r.enabled = false; hidden.Add(r); }
            return true;
        }

        static void Note(Camera c, int idle)
        {
            if (idle == lastIdle) return;
            lastIdle = idle;
            UnityEngine.Debug.Log("[cupheadport] blur idle: " + c.name + (idle == 1 ? " idle" : " drawing geese") + " at frame " + Time.frameCount + " (" + tracked.Count + " tracked)");
        }

        static void ShowQuads()
        {
            foreach (Renderer r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
        }

        // the cleared colour is transparent black (what the quads then show)
        static bool IsTransparentBlend(Camera c)
        {
            Color k = c.backgroundColor;
            return k.r == 0f && k.g == 0f && k.b == 0f && k.a == 0f;
        }

        static bool AnyInView(Camera c)
        {
            Vector3 p = c.transform.position;
            float hh = c.orthographicSize + Margin, hw = c.orthographicSize * c.aspect + Margin;
            int mask = c.cullingMask;
            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                Renderer r = tracked[i];
                if (r == null) { tracked.RemoveAt(i); continue; }
                if (!r.enabled || !r.gameObject.activeInHierarchy || (mask & (1 << r.gameObject.layer)) == 0) continue;
                Bounds b = r.bounds;
                if (b.max.x >= p.x - hw && b.min.x <= p.x + hw && b.max.y >= p.y - hh && b.min.y <= p.y + hh) return true;
            }
            return false;
        }

        // false once a renderer that is not tracked was found on the camera's layers (this scene)
        static bool Scan(Camera c, int cid)
        {
            int st;
            if (state.TryGetValue(cid, out st) && st == 0) return false;
            float at, now = Time.realtimeSinceStartup;
            if (state.ContainsKey(cid) && scanAt.TryGetValue(cid, out at) && now < at) return true;
            scanAt[cid] = now + 30f;      // a full search of the scene takes tens of ms here: rarely
            int mask = c.cullingMask;
            RenderTexture rt = c.targetTexture;
            var q = new List<Renderer>();
            foreach (Renderer r in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                if (rt != null && r.gameObject.layer != 31 && r.sharedMaterials.Length == 1 && r.sharedMaterial != null &&
                    r.sharedMaterial.mainTexture == rt && r.sharedMaterial.shader.name == "Unlit/Transparent" && !hidden.Contains(r))
                    q.Add(r);
                if ((mask & (1 << r.gameObject.layer)) == 0 || tracked.Contains(r)) continue;
                state[cid] = 0;
                UnityEngine.Debug.Log("[cupheadport] blur idle: camera " + c.name + " also draws " + r.name + " (layer " + r.gameObject.layer + "): blurred as usual");
                return false;
            }
            foreach (Renderer r in hidden) if (r != null && !q.Contains(r)) q.Add(r);
            if (!state.ContainsKey(cid))
                UnityEngine.Debug.Log("[cupheadport] blur idle: camera " + c.name + " (layers 0x" + mask.ToString("x") + ") copies instead of blurring while none of its " +
                                      tracked.Count + " tracked renderers is in view; quads showing its texture: " + q.Count);
            shows[cid] = q;
            state[cid] = 1;
            return true;
        }
    }
}
