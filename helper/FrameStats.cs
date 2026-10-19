using System;
using System.Text;
using UnityEngine;

namespace CupheadPort
{
    // Diagnostics, CUPHEAD_FRAMESTATS=1: every 10 s, how the frame times spread (Time.unscaledDeltaTime
    // in 4 ms bins), to see whether frames bunch at whole display refreshes (16.7, 33.3, 50 ms: vsync
    // holding finished frames back) or spread out (the work itself sets the pace).
    public static class FrameStats
    {
        static int on = -1;
        static readonly int[] bins = new int[16];   // 0-4, 4-8, ... 56-60, 60+ ms
        static int frames; static float sum; static float next;

        public static void Tick()
        {
            if (on < 0)
            {
                on = Environment.GetEnvironmentVariable("CUPHEAD_FRAMESTATS") == "1" ? 1 : 0;
                next = Time.realtimeSinceStartup + 10f;
            }
            if (on == 0) return;
            float ms = Time.unscaledDeltaTime * 1000f;
            bins[Math.Min(15, (int)(ms / 4f))]++; frames++; sum += ms;
            if (Time.realtimeSinceStartup < next) return;
            next = Time.realtimeSinceStartup + 10f;
            var sb = new StringBuilder("[cupheadport] frame times, " + frames + " frames, mean " + (sum / Math.Max(1, frames)).ToString("F1") + " ms, scene " +
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ", vSyncCount " + QualitySettings.vSyncCount + ":");
            for (int i = 0; i < bins.Length; i++)
                if (bins[i] > 0) sb.Append(" " + (i * 4) + (i == 15 ? "+" : "-" + (i * 4 + 4)) + "ms:" + bins[i]);
            UnityEngine.Debug.Log(sb.ToString());
            Array.Clear(bins, 0, bins.Length); frames = 0; sum = 0f;
        }
    }
}
