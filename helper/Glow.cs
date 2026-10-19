using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CupheadPort
{
    // GlowText (the glowing title text of the map's level cards, the win screen and the Tower of Power
    // scorecard) has a camera of its own, CameraGlow, that draws the UI layer into the glow's texture.
    // The game left it on all the time, though the texture is shown only while the glow is
    // (BeginGlow .. StopGlow): on the world maps it was the most expensive camera of all. The camera
    // now renders only while a glow is shown: codepatch glow-camera calls Begin and Stop, and after
    // each scene load (the level card's GlowText stays inactive until the card opens) the cameras of
    // every GlowText are switched off unless their glow has begun. CUPHEAD_GLOWCAM=always leaves them
    // on as the game had them.
    public static class Glow
    {
        static int on = -1;
        static readonly HashSet<int> glowing = new HashSet<int>();   // camera objects whose glow has begun
        static float sweepAt = -1f;
        static int sweeps;
        static FieldInfo camField;

        static bool On()
        {
            if (on < 0)
            {
                on = Environment.GetEnvironmentVariable("CUPHEAD_GLOWCAM") == "always" ? 0 : 1;
                if (on == 1)
                {
                    UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => { sweepAt = Time.realtimeSinceStartup + 0.5f; sweeps = 0; };
                    sweepAt = Time.realtimeSinceStartup + 0.5f;
                    camField = typeof(GlowText).GetField("cameraGlow", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }
            }
            return on == 1;
        }

        static void Set(GameObject cameraGlow, bool render)
        {
            if (cameraGlow == null) return;
            Camera c = cameraGlow.GetComponent<Camera>();
            if (c != null && c.enabled != render) c.enabled = render;
        }

        // start of GlowText.BeginGlow: the camera renders this frame already (the glow's own bloom
        // pass is switched on for this one frame)
        public static void Begin(GameObject cameraGlow)
        {
            if (!On() || cameraGlow == null) return;
            glowing.Add(cameraGlow.GetInstanceID());
            Set(cameraGlow, true);
        }

        // start of GlowText.StopGlow
        public static void Stop(GameObject cameraGlow)
        {
            if (!On() || cameraGlow == null) return;
            glowing.Remove(cameraGlow.GetInstanceID());
            Set(cameraGlow, false);
        }

        // Diagnostics.Tick: 0.5 s, 2 s and 5 s after a scene load
        public static void Tick()
        {
            if (!On() || sweepAt < 0f || Time.realtimeSinceStartup < sweepAt || camField == null) return;
            sweeps++;
            sweepAt = sweeps >= 3 ? -1f : Time.realtimeSinceStartup + (sweeps == 1 ? 1.5f : 3f);
            int off = 0;
            foreach (GlowText g in Resources.FindObjectsOfTypeAll<GlowText>())
            {
                if (!g.gameObject.scene.IsValid()) continue;               // a prefab, not in a scene
                GameObject cam = camField.GetValue(g) as GameObject;
                if (cam == null || glowing.Contains(cam.GetInstanceID())) continue;
                Camera c = cam.GetComponent<Camera>();
                if (c != null && c.enabled) { c.enabled = false; off++; }
            }
            if (off > 0) UnityEngine.Debug.Log("[cupheadport] glow cameras off until their glow begins: " + off);
        }
    }
}
