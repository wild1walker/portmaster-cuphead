using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CupheadPort
{
    // A few of Cuphead's sprites have a mesh traced around every speck of their picture: Djimmi the
    // Great's starry space backgrounds are 33,487 and 16,741 vertices (Forest Follies' visible sprites
    // are ~1,400 together, most levels 2,000-5,000), and Unity copies every drawn sprite's vertices
    // into a dynamic buffer each frame: on the RGSP Djimmi's level camera took 10 ms of the main
    // thread a frame. Such a sprite is drawn here as a plain quad (CUPHEAD_HEAVYSPRITES=<vertices>,
    // default 5000; =0 off):
    //  - not packed tightly in an atlas: a quad over the same texture area (Sprite.Create, FullRect);
    //  - packed tightly (other sprites sit in its gaps): baked once, its own mesh drawn with a plain
    //    copy shader into a texture of the sprite's size, and the quad shows that texture.
    // Same placement (pivot, pixels per unit) and the same material, so the same picture; the
    // transparent parts cost a little GPU fill. With CUPHEAD_MEMLOG=1 every bake is checked: the
    // original mesh and the quad are both drawn into textures and compared (mean/max difference).
    // Renderers get the quad version just before the cameras cull (animated ones every frame, after
    // their animator has set the frame); sprites are baked in the update, never while rendering.
    // CUPHEAD_HEAVYSPRITES=hide<n> (test only) hides them instead, to measure what they cost.
    public static class HeavySprites
    {
        static int limit = -1;
        static bool hide;
        static float scanAt = -1f;
        static int scans;
        static int frameDone = -1;
        static readonly List<SpriteRenderer> tracked = new List<SpriteRenderer>();
        static readonly Dictionary<int, Sprite> quads = new Dictionary<int, Sprite>();   // heavy sprite -> quad
        static readonly HashSet<int> light = new HashSet<int>();                         // sprites left as they are
        static readonly List<Sprite> pending = new List<Sprite>();                       // seen while rendering, not prepared yet
        static Shader copy;

        public static void Tick()
        {
            if (limit < 0)
            {
                string v = Environment.GetEnvironmentVariable("CUPHEAD_HEAVYSPRITES");
                hide = v != null && v.StartsWith("hide");
                if (hide) v = v.Substring(4);
                int n;
                limit = string.IsNullOrEmpty(v) ? 5000 : int.TryParse(v, out n) ? n : 5000;
                if (limit == 0) return;
                SceneManager.sceneLoaded += (s, m) => { tracked.Clear(); scans = 0; scanAt = Time.realtimeSinceStartup + 0.5f; };
                scanAt = Time.realtimeSinceStartup + 0.5f;
                Camera.onPreCull += c => Swap();
            }
            if (limit == 0) return;
            for (int i = 0; i < pending.Count; i++) if (pending[i] != null) Prepare(pending[i]);
            pending.Clear();
            if (scanAt < 0f || Time.realtimeSinceStartup < scanAt) return;
            scans++;
            scanAt = Time.realtimeSinceStartup + (scans < 10 ? 2f : 10f);   // new objects keep appearing: every 2 s at first, then every 10 s
            foreach (SpriteRenderer r in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>())
                if (r.sprite != null && Prepare(r.sprite) && !tracked.Contains(r)) tracked.Add(r);
        }

        // true when the sprite is heavy and has its quad (made now if needed)
        static bool Prepare(Sprite s)
        {
            int id = s.GetInstanceID();
            if (light.Contains(id)) return false;
            if (quads.ContainsKey(id)) return true;
            Sprite q = null;
            try
            {
                int nv = s.vertices.Length;
                if (nv > limit && s.texture != null)
                {
                    if (hide) { quads[id] = s; return true; }
                    bool tight = s.packed && s.packingMode == SpritePackingMode.Tight;
                    q = tight ? Bake(s) : Plain(s);
                    if (q != null)
                    {
                        q.name = s.name + (tight ? " (baked quad)" : " (quad)");
                        if (Environment.GetEnvironmentVariable("CUPHEAD_MEMLOG") == "1")
                            UnityEngine.Debug.Log("[cupheadport] heavy sprite " + s.name + ": " + nv + " vertices, drawn as a " + (tight ? "baked " : "") + "quad " +
                                (int)s.rect.width + "x" + (int)s.rect.height + Check(s, q));
                    }
                }
            }
            catch (Exception e) { UnityEngine.Debug.Log("[cupheadport] heavy sprite " + s.name + " left alone: " + e.Message); q = null; }
            if (q == null) { light.Add(id); return false; }
            quads[id] = q;
            return true;
        }

        static Sprite Plain(Sprite s)
        {
            Rect tr = s.textureRect;
            // the pivot is given relative to the sprite's rect; the quad covers its texture rect
            Vector2 pivot = new Vector2((s.pivot.x - s.textureRectOffset.x) / tr.width, (s.pivot.y - s.textureRectOffset.y) / tr.height);
            return Sprite.Create(s.texture, tr, pivot, s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        static int bakeLayer = -2;

        // a layer no game object uses, for the bake camera
        static int BakeLayer()
        {
            if (bakeLayer == -2)
            {
                bakeLayer = -1;
                for (int l = 31; l >= 8; l--) if (string.IsNullOrEmpty(LayerMask.LayerToName(l))) { bakeLayer = l; break; }
            }
            return bakeLayer;
        }

        // the sprite drawn with a plain copy of its texels (no blending, no tint) into a new texture the
        // size of its rect, by a camera of its own (disabled; rendered once by hand)
        static RenderTexture Draw(Sprite s, Sprite shown)
        {
            int layer = BakeLayer();
            if (layer < 0) return null;
            if (copy == null) copy = Shader.Find("Hidden/BlitCopy");
            if (copy == null) return null;
            int w = Mathf.RoundToInt(s.rect.width), h = Mathf.RoundToInt(s.rect.height);
            float ppu = s.pixelsPerUnit;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var mat = new Material(copy);
            var go = new GameObject("cupheadport bake") { hideFlags = HideFlags.HideAndDontSave, layer = layer };
            go.transform.position = new Vector3(20000f, 20000f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = shown; sr.sharedMaterial = mat;
            var camGo = new GameObject("cupheadport bake camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = h / (2f * ppu);
            cam.aspect = (float)w / h;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << layer;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 100f;
            cam.targetTexture = rt;
            // the camera's view is the sprite's rect: its centre sits (w/2, h/2) - pivot pixels from the pivot
            camGo.transform.position = go.transform.position + new Vector3((w * 0.5f - s.pivot.x) / ppu, (h * 0.5f - s.pivot.y) / ppu, -10f);
            cam.Render();
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(camGo); UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.Destroy(mat);
            return rt;
        }

        static Texture2D Read(RenderTexture rt)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            tex.Apply(false, false);
            RenderTexture.active = prev;
            return tex;
        }

        static int Drawn(Color32[] p) { int n = 0; for (int i = 0; i < p.Length; i++) if (p[i].a > 0) n++; return n; }

        static Sprite Bake(Sprite s)
        {
            int w = Mathf.RoundToInt(s.rect.width), h = Mathf.RoundToInt(s.rect.height);
            if (w <= 0 || h <= 0 || w > 4096 || h > 4096) return null;
            RenderTexture rt = Draw(s, s);
            if (rt == null) { UnityEngine.Debug.Log("[cupheadport] heavy sprites: no bake layer or copy shader"); return null; }
            Texture2D tex = Read(rt);
            RenderTexture.ReleaseTemporary(rt);
            int drawn = Drawn(tex.GetPixels32());
            if (drawn == 0)
            {
                UnityEngine.Debug.Log("[cupheadport] heavy sprite " + s.name + ": the bake drew nothing, left alone");
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.filterMode = s.texture.filterMode;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(false, true);                                   // GPU only from here on
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(s.pivot.x / w, s.pivot.y / h), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        // CUPHEAD_MEMLOG=1: the original and the quad drawn alike (plain copy), compared
        static string Check(Sprite s, Sprite q)
        {
            if (Environment.GetEnvironmentVariable("CUPHEAD_MEMLOG") != "1") return "";
            RenderTexture ra = Draw(s, s), rb = Draw(s, q);
            if (ra == null || rb == null) return "";
            Texture2D ta = Read(ra), tb = Read(rb);
            RenderTexture.ReleaseTemporary(ra); RenderTexture.ReleaseTemporary(rb);
            Color32[] pa = ta.GetPixels32(), pb = tb.GetPixels32();
            long sum = 0; int max = 0;
            for (int i = 0; i < pa.Length; i++)
            {
                int d = Math.Abs(pa[i].r - pb[i].r) + Math.Abs(pa[i].g - pb[i].g) + Math.Abs(pa[i].b - pb[i].b) + Math.Abs(pa[i].a - pb[i].a);
                sum += d; if (d > max) max = d;
            }
            string r = ", check: mean difference " + ((double)sum / Math.Max(1, pa.Length)).ToString("F3") + " max " + max + " (of 1020), drawn pixels " + Drawn(pa) + " / " + Drawn(pb);
            UnityEngine.Object.Destroy(ta); UnityEngine.Object.Destroy(tb);
            return r;
        }

        static void Swap()
        {
            int f = Time.frameCount;
            if (f == frameDone) return;
            frameDone = f;
            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                SpriteRenderer r = tracked[i];
                if (r == null) { tracked.RemoveAt(i); continue; }
                Sprite s = r.sprite;
                if (s == null) continue;
                int id = s.GetInstanceID();
                Sprite q;
                if (quads.TryGetValue(id, out q))
                {
                    if (hide) { if (r.enabled) r.enabled = false; }
                    else if (q != s) r.sprite = q;
                }
                else if (!light.Contains(id) && !pending.Contains(s)) pending.Add(s);   // a new frame: prepared in the next update
            }
        }
    }
}
