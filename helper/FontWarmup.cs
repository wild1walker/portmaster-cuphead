using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace CupheadPort
{
    // Cuphead's button prompts and much of its UI text are UnityEngine.UI.Text with dynamic fonts.
    // The first time a character is shown at a given pixel size, Unity rasterizes it with FreeType
    // (x86 code under box64 here), uploads the font texture again and rebuilds every text that uses
    // the font: a hitch when a prompt appears for the first time.
    //
    // After every scene load the printable ASCII characters are requested, at the pixel sizes the
    // scene's texts are drawn at (font size times the scale of the canvases on screen), for every
    // Text the scene has loaded, prefabs included; the work happens behind the loading screen.
    // With CUPHEAD_MEMLOG=1 every font texture rebuild is logged (stderr), next to the hitch lines.
    // CUPHEAD_FONTWARM=0 turns it off.
    public static class FontWarmup
    {
        const string Ascii = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
        static bool hooked;
        static readonly HashSet<string> done = new HashSet<string>();

        static string Uptime()
        {
            try { return File.ReadAllText("/proc/uptime").Split(' ')[0]; }
            catch (Exception) { return "?"; }
        }

        static void OnRebuilt(Font f)
        {
            Console.Error.WriteLine("[cupheadport font] up=" + Uptime() + " rebuilt " + (f != null ? f.name : "?") +
                " frame#=" + Time.frameCount);
        }

        // Called after every scene load (Warmup.OnSceneLoaded).
        public static void AfterSceneLoad(string scene)
        {
            if (!hooked)
            {
                hooked = true;
                if (Environment.GetEnvironmentVariable("CUPHEAD_MEMLOG") == "1") Font.textureRebuilt += OnRebuilt;
            }
            if (Environment.GetEnvironmentVariable("CUPHEAD_FONTWARM") == "0") return;
            float t0 = Time.realtimeSinceStartup;
            var scales = new HashSet<float> { 1f };
            foreach (Canvas c in UnityEngine.Object.FindObjectsOfType<Canvas>())
                if (c != null && c.isRootCanvas && c.renderMode != RenderMode.WorldSpace) scales.Add(c.scaleFactor);
            int requests = 0;
            foreach (Text t in Resources.FindObjectsOfTypeAll<Text>())
            {
                if (t == null || t.font == null || !t.font.dynamic) continue;
                var sizes = new List<int> { t.fontSize };
                if (t.resizeTextForBestFit) { sizes.Add(t.resizeTextMaxSize); sizes.Add((t.resizeTextMinSize + t.resizeTextMaxSize) / 2); }
                foreach (int size in sizes)
                    foreach (float s in scales)
                    {
                        int px = Mathf.Clamp(Mathf.RoundToInt(size * s), 1, 300);
                        string key = t.font.GetInstanceID() + "/" + px + "/" + (int)t.fontStyle;
                        if (!done.Add(key)) continue;
                        t.font.RequestCharactersInTexture(Ascii + (t.text ?? ""), px, t.fontStyle);
                        requests++;
                    }
            }
            UnityEngine.Debug.Log("[cupheadport] font warmup for " + scene + ": " + requests + " font sizes in " +
                (int)((Time.realtimeSinceStartup - t0) * 1000f) + " ms");
        }
    }
}
