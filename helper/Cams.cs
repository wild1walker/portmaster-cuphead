using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace CupheadPort
{
    // Experiment, CUPHEAD_CAMS=<letters>: switches cameras or image effects off to measure what each
    // costs on the device (some of these change what is shown; measurement only):
    //   c  ClearCamera (renders nothing, clears the screen; the level/map camera clears it again)
    //   l  SceneLoaderCamera (scene transitions; breaks the fades and the iris)
    //   g  CameraGlow (map: renders the UI layer into the glow text's texture every frame)
    //   u  UICamera (the HUD)
    //   e  the film effects on RenderCamera (grain/chromatic aberration and blur/gamma)
    //   a  only the grain/chromatic aberration effect;  b  only the blur/gamma effect
    // CUPHEAD_CAMTIME=1 logs every 10 s how long each camera takes, ms per frame: from its culling to
    // the end of its rendering ("render"), and from there to the next camera's culling ("after":
    // its image effects, and the engine's work between cameras; for the last camera, up to the end
    // of the frame).
    public static class Cams
    {
        static string flags;
        static int frame;

        sealed class Acc { public long render, after, n; }
        static readonly Dictionary<string, Acc> acc = new Dictionary<string, Acc>();
        static readonly Stopwatch sw = new Stopwatch();
        static string lastCam; static long lastPost, startCull;
        static int timeOn = -1, frame0; static float nextLog;

        static Acc Get(string n) { Acc a; if (!acc.TryGetValue(n, out a)) acc[n] = a = new Acc(); return a; }

        static void TimeTick()
        {
            if (timeOn < 0)
            {
                timeOn = Environment.GetEnvironmentVariable("CUPHEAD_CAMTIME") == "1" ? 1 : 0;
                if (timeOn == 0) return;
                sw.Start();
                Camera.onPreCull += c =>
                {
                    long t = sw.ElapsedTicks;
                    if (lastCam != null) { Get(lastCam).after += t - lastPost; lastCam = null; }
                    startCull = t;
                };
                Camera.onPostRender += c =>
                {
                    long t = sw.ElapsedTicks;
                    Acc a = Get(c.name); a.render += t - startCull; a.n++;
                    lastCam = c.name; lastPost = t;
                };
                frame0 = Time.frameCount; nextLog = Time.realtimeSinceStartup + 10f;
            }
            if (timeOn == 0) return;
            if (!endOfFrame && Cuphead.Current != null) { endOfFrame = true; Cuphead.Current.StartCoroutine(EndOfFrame()); }
            if (Time.realtimeSinceStartup < nextLog) return;
            int frames = Math.Max(1, Time.frameCount - frame0);
            double ms = 1000.0 / Stopwatch.Frequency / frames;
            var sb = new StringBuilder("[cupheadport] camera time, " + frames + " frames, scene " +
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " (ms per frame: render, after; renders per frame):");
            foreach (var kv in acc)
                sb.Append("\n  " + (kv.Value.render * ms).ToString("F2") + "  " + (kv.Value.after * ms).ToString("F2") + "  " +
                          ((double)kv.Value.n / frames).ToString("F1") + "  " + kv.Key);
            UnityEngine.Debug.Log(sb.ToString());
            acc.Clear(); frame0 = Time.frameCount; nextLog = Time.realtimeSinceStartup + 10f;
        }

        static bool endOfFrame;

        // after all cameras and their image effects: closes the last camera's "after"
        static System.Collections.IEnumerator EndOfFrame()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                yield return wait;
                long t = sw.ElapsedTicks;
                if (lastCam != null) { Get(lastCam).after += t - lastPost; lastCam = null; }
            }
        }

        public static void Tick()
        {
            TimeTick();
            if (flags == null) flags = Environment.GetEnvironmentVariable("CUPHEAD_CAMS") ?? "";
            if (flags.Length == 0 || frame++ % 30 != 0) return;
            foreach (Camera c in Camera.allCameras)
            {
                string n = c.name;
                if ((Has('c') && n == "ClearCamera") || (Has('l') && n == "SceneLoaderCamera") || (Has('g') && n == "CameraGlow") ||
                    (Has('u') && n.StartsWith("UICamera")))
                {
                    c.enabled = false;
                    UnityEngine.Debug.Log("[cupheadport] cams: " + n + " off");
                }
                else if ((Has('e') || Has('a') || Has('b')) && n.StartsWith("RenderCamera"))
                {
                    foreach (MonoBehaviour b in c.GetComponents<MonoBehaviour>())
                        if (b.enabled && ((b is ChromaticAberrationFilmGrain && (Has('e') || Has('a'))) || (b is BlurGamma && (Has('e') || Has('b')))))
                        {
                            b.enabled = false;
                            UnityEngine.Debug.Log("[cupheadport] cams: " + b.GetType().Name + " off");
                        }
                }
            }
        }

        static bool Has(char f) { return flags.IndexOf(f) >= 0; }
    }
}
