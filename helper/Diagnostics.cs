using System;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace CupheadPort
{
    // CUPHEAD_MEMLOG=1 (ports/cuphead/options.txt): every 10 s the player log gets the process's
    // resident and swapped memory, the device's free memory and Unity's own counters.
    public static class Diagnostics
    {
        static int enabled = -1;
        static float last = -100f;
        static int frames, over50, over100, gcAtLast = -1;
        static float worst, hitchSec = -1f;
        static int fontRebuilds;

        static string ProcField(string file, string key)
        {
            try
            {
                foreach (var line in File.ReadAllLines(file))
                    if (line.StartsWith(key)) return (long.Parse(line.Substring(key.Length).Trim().Split(' ')[0]) / 1024) + "MB";
            }
            catch (Exception) { }
            return "?";
        }

        // seconds since boot (the clock of /proc/uptime and of the fps and monitor logs)
        static string Uptime()
        {
            try { return File.ReadAllText("/proc/uptime").Split(' ')[0]; }
            catch (Exception) { return "?"; }
        }

        public static void Tick()
        {
            if (UpdTime.Active)                     // test builds (test-time-updates): each helper timed
            {
                { long t = UpdTime.T0(); Pad.Tick(); UpdTime.T1(null, "port helper Pad.Tick()", t); }
                { long t = UpdTime.T0(); Warmup.Register(); UpdTime.T1(null, "port helper Warmup.Register()", t); }
                { long t = UpdTime.T0(); Warmup.Tick(); UpdTime.T1(null, "port helper Warmup.Tick()", t); }
                { long t = UpdTime.T0(); Rumble.Tick(); UpdTime.T1(null, "port helper Rumble.Tick()", t); }
                { long t = UpdTime.T0(); Tuning.Tick(); UpdTime.T1(null, "port helper Tuning.Tick()", t); }
                { long t = UpdTime.T0(); JitMap.Tick(); UpdTime.T1(null, "port helper JitMap.Tick()", t); }
                { long t = UpdTime.T0(); TestHooks.Tick(); UpdTime.T1(null, "port helper TestHooks.Tick()", t); }
                { long t = UpdTime.T0(); SceneStats.Tick(); UpdTime.T1(null, "port helper SceneStats.Tick()", t); }
                { long t = UpdTime.T0(); Decor.Tick(); UpdTime.T1(null, "port helper Decor.Tick()", t); }
                { long t = UpdTime.T0(); Markers.Tick(); UpdTime.T1(null, "port helper Markers.Tick()", t); }
                { long t = UpdTime.T0(); Cams.Tick(); UpdTime.T1(null, "port helper Cams.Tick()", t); }
                { long t = UpdTime.T0(); Glow.Tick(); UpdTime.T1(null, "port helper Glow.Tick()", t); }
                { long t = UpdTime.T0(); CameraTrim.Tick(); UpdTime.T1(null, "port helper CameraTrim.Tick()", t); }
                { long t = UpdTime.T0(); RenderTextureFit.Tick(); UpdTime.T1(null, "port helper RenderTextureFit.Tick()", t); }
                { long t = UpdTime.T0(); FrameStats.Tick(); UpdTime.T1(null, "port helper FrameStats.Tick()", t); }
                { long t = UpdTime.T0(); ColStats.Tick(); UpdTime.T1(null, "port helper ColStats.Tick()", t); }
                { long t = UpdTime.T0(); Movers.Tick(); UpdTime.T1(null, "port helper Movers.Tick()", t); }
                { long t = UpdTime.T0(); HeavySprites.Tick(); UpdTime.T1(null, "port helper HeavySprites.Tick()", t); }
                StaticBodies.Tick();
                { long t = UpdTime.T0(); UpdTime.Tick(); UpdTime.T1(null, "port helper UpdTime.Tick()", t); }
                { long t = UpdTime.T0(); CastProbe.Tick(); UpdTime.T1(null, "port helper CastProbe.Tick()", t); }
                AnimProbe.Tick();
                CullProbe.Tick();
                MemReport.Tick();
                { long t = UpdTime.T0(); AtlasTrim.Tick(); UpdTime.T1(null, "port helper AtlasTrim.Tick()", t); }
            }
            else
            {
                Pad.Tick();
                Warmup.Register();
                Warmup.Tick();
                Rumble.Tick();
                Tuning.Tick();
                JitMap.Tick();
                TestHooks.Tick();
                SceneStats.Tick();
                Decor.Tick();
                Markers.Tick();
                Cams.Tick();
                Glow.Tick();
                CameraTrim.Tick();
                RenderTextureFit.Tick();
                FrameStats.Tick();
                ColStats.Tick();
                Movers.Tick();
                HeavySprites.Tick();
                StaticBodies.Tick();
                UpdTime.Tick();
                CastProbe.Tick();
                AnimProbe.Tick();
                CullProbe.Tick();
                MemReport.Tick();
                AtlasTrim.Tick();
            }
            if (enabled < 0)
            {
                string v = Environment.GetEnvironmentVariable("CUPHEAD_MEMLOG");
                enabled = v != null && v.Trim() == "1" ? 1 : 0;
                // dynamic font atlas rebuilds: each one makes Unity re-mesh every text using the font
                // (TextMesh through a search of all objects, UI Text through FontUpdateTracker)
                if (enabled == 1) Font.textureRebuilt += f => fontRebuilds++;
                UnityEngine.Debug.Log("[cupheadport] port helper active; CUPHEAD_MEMLOG=" + (v ?? "(unset)"));
            }
            if (enabled == 0) return;
            frames++;
            float dt = Time.unscaledDeltaTime;
            if (dt > worst) worst = dt;
            if (dt > 0.05f) over50++;
            if (dt > 0.1f) over100++;
            float now = Time.realtimeSinceStartup;
            // each long frame on stderr (log.txt), next to the driver's and Mono's own messages
            if (hitchSec < 0f)
            {
                int ms;
                string v = Environment.GetEnvironmentVariable("CUPHEAD_HITCH_MS");
                hitchSec = (v != null && int.TryParse(v, out ms) && ms > 0 ? ms : 50) / 1000f;
            }
            if (dt > hitchSec)
                Console.Error.WriteLine("[cupheadport hitch] up=" + Uptime() + " frame=" + (int)(dt * 1000) +
                    "ms gcCount=" + GC.CollectionCount(0) + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            if (now - last < 10f) return;
            float fps = last < 0 ? 0 : frames / (now - last);
            last = now; frames = 0;
            int gc = GC.CollectionCount(0);
            int gcs = gcAtLast < 0 ? 0 : gc - gcAtLast;
            gcAtLast = gc;
            string hitches = " worstFrame=" + (int)(worst * 1000) + "ms over50ms=" + over50 + " over100ms=" + over100 + " gcRuns=" + gcs + " fontRebuilds=" + fontRebuilds;
            worst = 0; over50 = over100 = 0; fontRebuilds = 0;
            UnityEngine.Debug.Log("[cupheadport memlog] t=" + (int)now + " fps=" + fps.ToString("0.0") +
                " rss=" + ProcField("/proc/self/status", "VmRSS:") + " swap=" + ProcField("/proc/self/status", "VmSwap:") +
                " memAvailable=" + ProcField("/proc/meminfo", "MemAvailable:") +
                " unityAlloc=" + (Profiler.GetTotalAllocatedMemoryLong() >> 20) + "MB" +
                " unityReserved=" + (Profiler.GetTotalReservedMemoryLong() >> 20) + "MB" +
                " gc=" + (GC.GetTotalMemory(false) >> 20) + "MB" +
                " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + hitches);
        }
    }
}
