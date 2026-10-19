using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace CupheadPort
{
    // BlurGamma (the film look's soft blur and the brightness setting, on Cuphead's last camera) costs
    // the RGSP about a tenth of a run-and-gun frame (Forest Follies 30.6 -> 33.7 fps without it; the
    // grain/chromatic aberration effect next to it costs almost nothing). Codepatch fx-blurgamma
    // routes BlurGamma.OnRenderImage here; CUPHEAD_FX picks how it is drawn:
    //   game     (default) exactly what the game does: a temporary texture per frame, horizontal blur
    //            into it, vertical blur + gamma (+ the filter) out of it
    //   keep     the same passes, but one intermediate texture kept from frame to frame (no
    //            GetTemporary/ReleaseTemporary each frame) and the source's filter mode set only
    //            when it differs
    //   merge    the camera's other effect, ChromaticAberrationFilmGrain (Unity runs it first), is
    //            drawn from here too, before the blur, and its component is switched off: Unity then
    //            runs one image effect instead of a chain of two. Same passes, same picture; switching
    //            off either effect had saved ~3.5 ms of main thread a frame on the RGSP (Forest
    //            Follies +10%), so the cost was the chaining, not the effects.
    //   half     one pass: the vertical blur only, with the gamma and the filter (a lighter blur)
    //   sharp    one pass: no blur, the gamma (brightness setting) and the filter only (a sharper picture)
    // CUPHEAD_FXTIME=1 logs every 10 s what each step costs the main thread.
    public static class Effects
    {
        static int mode = -1;            // 0 game, 1 keep, 2 merge
        static ChromaticAberrationFilmGrain ca;
        static bool caOurs;              // the grain effect was switched off by us (and is drawn here)
        static Action<ChromaticAberrationFilmGrain, RenderTexture, RenderTexture> caRender;
        static bool timing;
        static FieldInfo matField;
        static RenderTexture kept;
        static readonly Stopwatch sw = Stopwatch.StartNew();
        static readonly long[] acc = new long[6];
        static int frames; static float nextLog;
        static readonly int parameterId = Shader.PropertyToID("_Parameter");

        static void Init()
        {
            string v = Environment.GetEnvironmentVariable("CUPHEAD_FX");
            mode = v == "keep" ? 1 : v == "merge" ? 2 : v == "sharp" ? 3 : v == "half" ? 4 : 0;
            MethodInfo mi = typeof(ChromaticAberrationFilmGrain).GetMethod("OnRenderImage", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (mi != null)
                caRender = (Action<ChromaticAberrationFilmGrain, RenderTexture, RenderTexture>)Delegate.CreateDelegate(
                    typeof(Action<ChromaticAberrationFilmGrain, RenderTexture, RenderTexture>), mi);
            if (mode == 2 && caRender == null) mode = 0;
            timing = Environment.GetEnvironmentVariable("CUPHEAD_FXTIME") == "1";
            matField = typeof(BlurGamma).GetField("blurMaterial", BindingFlags.Instance | BindingFlags.NonPublic);
            nextLog = Time.realtimeSinceStartup + 10f;
            UnityEngine.Debug.Log("[cupheadport] fx: blur/gamma " + new[] { "game", "keep", "merge", "sharp", "half" }[mode] + (timing ? " (timed)" : ""));
        }

        static void Mark(int i, ref long t)
        {
            if (!timing) return;
            long n = sw.ElapsedTicks; acc[i] += n - t; t = n;
        }

        // replaces BlurGamma.OnRenderImage (codepatch fx-blurgamma)
        public static void RenderBlurGamma(global::BlurGamma bg, RenderTexture source, RenderTexture destination)
        {
            if (mode < 0) Init();
            long t = timing ? sw.ElapsedTicks : 0;
            RenderTexture caOut = null;
            if (mode == 2)
            {
                if (ca == null || ca.gameObject != bg.gameObject) { ca = bg.GetComponent<ChromaticAberrationFilmGrain>(); caOurs = false; }
                if (ca != null && ca.enabled) { ca.enabled = false; caOurs = true; }
                if (ca != null && caOurs)
                {
                    caOut = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
                    caRender(ca, source, caOut);
                    source = caOut;
                }
            }
            Material mat = null;
            if (bg.CheckResources() && matField != null) mat = matField.GetValue(bg) as Material;
            if (mat == null) { Graphics.Blit(source, destination); if (caOut != null) RenderTexture.ReleaseTemporary(caOut); return; }
            Mark(0, ref t);
            // the game's parameters (BlurGamma.OnRenderImage)
            float aspect = (float)source.width / (float)source.height;
            float scale = aspect < 1.7777778f ? aspect / 1.7777778f : 1f;
            scale *= 1f - 0.1f * SettingsData.Data.overscan;
            float size = (float)source.height / 1080f * scale;
            if (SettingsData.Data.filter == global::BlurGamma.Filter.BW) size *= 1.35f;
            mat.SetVector(parameterId, new Vector4(bg.blurSize * size, (0f - bg.blurSize) * size, Mathf.Pow(1.4f, 0f - SettingsData.Data.Brightness), 0f));
            int pass = 1;
            switch (SettingsData.Data.filter)
            {
                case global::BlurGamma.Filter.TwoStrip: pass++; break;
                case global::BlurGamma.Filter.BW: pass += 2; break;
            }
            Mark(1, ref t);
            if (mode >= 3)
            {
                // one pass: the vertical blur (half) or none (sharp), with the gamma and the filter
                if (mode == 3) mat.SetVector(parameterId, new Vector4(0f, 0f, Mathf.Pow(1.4f, 0f - SettingsData.Data.Brightness), 0f));
                source.filterMode = FilterMode.Bilinear;
                Graphics.Blit(source, destination, mat, pass);
                if (caOut != null) RenderTexture.ReleaseTemporary(caOut);
                return;
            }
            RenderTexture mid;
            if (mode == 1)
            {
                if (source.filterMode != FilterMode.Bilinear) source.filterMode = FilterMode.Bilinear;
                if (kept == null || kept.width != source.width || kept.height != source.height || kept.format != source.format)
                {
                    if (kept != null) kept.Release();
                    kept = new RenderTexture(source.width, source.height, 0, source.format);
                    kept.filterMode = FilterMode.Bilinear;
                    kept.Create();
                }
                mid = kept;
            }
            else
            {
                source.filterMode = FilterMode.Bilinear;
                mid = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
            }
            Mark(2, ref t);
            Graphics.Blit(source, mid, mat, 0);
            Mark(3, ref t);
            Graphics.Blit(mid, destination, mat, pass);
            Mark(4, ref t);
            if (mode != 1) RenderTexture.ReleaseTemporary(mid);
            if (caOut != null) RenderTexture.ReleaseTemporary(caOut);
            Mark(5, ref t);
            if (!timing) return;
            frames++;
            if (Time.realtimeSinceStartup < nextLog) return;
            nextLog = Time.realtimeSinceStartup + 10f;
            double ms = 1000.0 / Stopwatch.Frequency / Math.Max(1, frames);
            UnityEngine.Debug.Log("[cupheadport] fx blur/gamma, ms per frame: check " + (acc[0] * ms).ToString("F3") + ", parameters " + (acc[1] * ms).ToString("F3") +
                ", texture " + (acc[2] * ms).ToString("F3") + ", blur 1 " + (acc[3] * ms).ToString("F3") + ", blur 2 " + (acc[4] * ms).ToString("F3") +
                ", release " + (acc[5] * ms).ToString("F3") + " (" + frames + " frames)");
            Array.Clear(acc, 0, acc.Length); frames = 0;
        }
    }
}
