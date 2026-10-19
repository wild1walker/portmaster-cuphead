using System;
using System.Collections.Generic;
using UnityEngine;

namespace CupheadPort
{
    // Cameras that render into a fixed-size texture taller than the screen: Glumstone the Giant's
    // GeeseCamera and Howling Aces' BGCamera (1280x720, each followed by BlurOptimized at full size).
    // The texture is shown scaled down to the screen, so the extra pixels are never seen, and
    // Cuphead's BlurOptimized sizes its blur relative to the destination (blurSize * height / 1080),
    // so at screen resolution it looks the same. On the H700 (720x480) those two passes cost ~40 ms
    // of GPU time a frame (Glumstone: 17 fps; without the geese camera 59.6). Here the texture is
    // resized to the screen's height (same aspect) whenever such a camera is active, and half a
    // second after a scene load for every camera of the scene, on or not (Saltbaker's phase cameras).
    // CUPHEAD_RTFIT=0 turns it off.
    public static class RenderTextureFit
    {
        static int off = -1;
        static float nextCheck, sweepAt = -1f;

        // Called every frame (Diagnostics.Tick); looks at the cameras twice a second.
        public static void Tick()
        {
            if (off < 0)
            {
                off = Environment.GetEnvironmentVariable("CUPHEAD_RTFIT") == "0" ? 1 : 0;
                if (off == 0) UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => sweepAt = Time.realtimeSinceStartup + 0.5f;
            }
            if (off == 1 || Time.realtimeSinceStartup < nextCheck) return;
            nextCheck = Time.realtimeSinceStartup + 0.5f;
            int sh = Screen.height;
            if (sh <= 0) return;
            // after a scene load also the cameras that are not on yet (a boss phase's camera): their
            // textures take memory from the start
            IEnumerable<Camera> cams = Camera.allCameras;
            if (sweepAt >= 0f && Time.realtimeSinceStartup >= sweepAt)
            {
                sweepAt = -1f;
                var all = new List<Camera>();
                foreach (Camera c in Resources.FindObjectsOfTypeAll<Camera>())
                    if (c.gameObject.scene.IsValid()) all.Add(c);
                cams = all;
            }
            foreach (Camera c in cams)
            {
                RenderTexture rt = c.targetTexture;
                if (rt == null || rt.height <= sh) continue;
                int w = Mathf.Max(1, Mathf.RoundToInt(rt.width * (float)sh / rt.height));
                UnityEngine.Debug.Log("[cupheadport] render texture " + rt.name + " of camera " + c.name + ": " +
                          rt.width + "x" + rt.height + " -> " + w + "x" + sh + " (screen height)");
                bool created = rt.IsCreated();      // one not drawn into yet is created on first use, at the new size
                rt.Release();
                rt.width = w;
                rt.height = sh;
                if (created) rt.Create();
            }
        }
    }
}
