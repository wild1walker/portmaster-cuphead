using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CupheadPort
{
    // Work done after every scene load, while the loading screen is still up, so it does not
    // happen in the middle of a level:
    //  - Shader warmup: the first time a shader variant is drawn, the GLES driver compiles it (under
    //    box64 on the H700 that was part of the hitch when a new effect appeared). All shaders the
    //    scene has loaded are compiled up front. CUPHEAD_WARMUP=0 turns it off.
    //  - Memory trim: loading a level allocates hundreds of MB of temporary buffers. glibc keeps
    //    freed memory inside its per-thread heaps, so the game held on to it (about 690 MB resident
    //    in the tutorial) and the 1 GB device was left with ~50 MB for the file cache: code and data
    //    were read from the SD card again and again, with the game stalled on each read.
    //    malloc_trim(0) hands every free page back to the system; it runs right after the load and
    //    once more a few seconds later (Cuphead unloads the previous level's assets after the new
    //    scene starts). CUPHEAD_TRIM=0 turns it off.
    public static class Warmup
    {
        static bool registered, trimOff;
        static float trimAgainAt = -1f;

        [DllImport("libc", EntryPoint = "malloc_trim")]
        static extern int malloc_trim(UIntPtr pad);

        [DllImport("libc", EntryPoint = "malloc_stats")]
        static extern void malloc_stats();

        public static void Register()
        {
            if (registered) return;
            registered = true;
            trimOff = Environment.GetEnvironmentVariable("CUPHEAD_TRIM") == "0";
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Called every frame (Diagnostics.Tick).
        public static void Tick()
        {
            if (trimAgainAt > 0f && Time.realtimeSinceStartup >= trimAgainAt)
            {
                trimAgainAt = -1f;
                Trim("later");
            }
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CodeWarmup.AfterSceneLoad(scene.name);
            FontWarmup.AfterSceneLoad(scene.name);
            if (Environment.GetEnvironmentVariable("CUPHEAD_WARMUP") != "0")
            {
                float t0 = Time.realtimeSinceStartup;
                Shader.WarmupAllShaders();
                UnityEngine.Debug.Log("[cupheadport] shader warmup for " + scene.name + ": " + (int)((Time.realtimeSinceStartup - t0) * 1000f) + " ms");
            }
            Trim(scene.name);
            trimAgainAt = Time.realtimeSinceStartup + 4f;
            SceneStats.SceneLoaded();
        }

        static long RssKB()
        {
            try
            {
                foreach (var line in File.ReadAllLines("/proc/self/status"))
                    if (line.StartsWith("VmRSS:")) return long.Parse(line.Substring(6).Trim().Split(' ')[0]);
            }
            catch (Exception) { }
            return -1;
        }

        static void Trim(string when)
        {
            if (trimOff) return;
            try
            {
                long before = RssKB();
                float t0 = Time.realtimeSinceStartup;
                malloc_trim(UIntPtr.Zero);
                if (Environment.GetEnvironmentVariable("CUPHEAD_MEMLOG") == "1" && when != "later")
                {
                    Console.Error.WriteLine("[cupheadport] malloc arenas after loading " + when + ":");
                    malloc_stats();  // per-arena "system bytes" (held) and "in use bytes", on stderr
                }
                UnityEngine.Debug.Log("[cupheadport] memory trim (" + when + "): rss " + before / 1024 + " -> " + RssKB() / 1024 + " MB in " +
                    (int)((Time.realtimeSinceStartup - t0) * 1000f) + " ms");
            }
            catch (Exception e)
            {
                trimOff = true;
                UnityEngine.Debug.Log("[cupheadport] memory trim unavailable: " + e.Message);
            }
        }
    }
}
