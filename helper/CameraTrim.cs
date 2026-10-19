using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace CupheadPort
{
    // Every camera Unity renders costs the RGSP's main thread about a millisecond, even one that draws
    // nothing (culling, render setup, the hand-over to the render thread). Two of Cuphead's cameras
    // draw nothing most of the time; CUPHEAD_CAMTRIM switches them off while that holds (default "cl";
    // =0 or "" keeps both):
    //   c  ClearCamera (no layers, clears the screen): the level/map camera covers the whole screen
    //      and clears it again right after; off while it does.
    //   l  SceneLoaderCamera (the iris/fade and the hourglass of scene loads): during play its fader
    //      is fully transparent or stopped on the open iris frame and the hourglass at alpha 0; off
    //      while nothing is loading or in transition and the fader and icon are like that, back on
    //      the frame any of it changes. (The first frame of every transition shows nothing yet, an
    //      open iris or alpha 0, so being a frame late does not show.)
    public static class CameraTrim
    {
        static string flags;
        static Camera clearCam, loaderCam;
        static Image fader, icon;
        static FieldInfo faderField, iconField, cameraField;
        static SceneLoader loaderSeen;
        static Sprite lastSprite; static float lastAlpha = -1f; static int stable;
        static int frame;

        static bool Has(char f) { return flags.IndexOf(f) >= 0; }

        public static void Tick()
        {
            if (flags == null)
            {
                string v = Environment.GetEnvironmentVariable("CUPHEAD_CAMTRIM");
                flags = v == null ? "cl" : v == "0" ? "" : v;
                if (flags.Length > 0)
                {
                    UnityEngine.Debug.Log("[cupheadport] camera trim: " + flags);
                    faderField = typeof(SceneLoader).GetField("fader", BindingFlags.Instance | BindingFlags.NonPublic);
                    iconField = typeof(SceneLoader).GetField("icon", BindingFlags.Instance | BindingFlags.NonPublic);
                    cameraField = typeof(SceneLoader).GetField("camera", BindingFlags.Instance | BindingFlags.NonPublic);
                }
            }
            if (flags.Length == 0) return;
            frame++;
            if (Has('l')) Loader();
            if (Has('c') && frame % 15 == 0) Clear();
        }

        static bool bound;

        // every frame: few calls into the engine (each is slow under box64), plain reference checks
        // for the loader and its parts, their Unity null checks every 60 frames
        static void Loader()
        {
            if (!SceneLoader.Exists) return;
            SceneLoader sl = SceneLoader.instance;
            if (!ReferenceEquals(sl, loaderSeen) || (frame % 60 == 0 && bound && (loaderCam == null || fader == null || icon == null)))
            {
                loaderSeen = sl; loaderCam = null; fader = icon = null;
                if (faderField != null) fader = faderField.GetValue(sl) as Image;
                if (iconField != null) icon = iconField.GetValue(sl) as Image;
                AbstractCupheadCamera c = cameraField != null ? cameraField.GetValue(sl) as AbstractCupheadCamera : null;
                if (c != null) loaderCam = c.camera;
                stable = 0;
                bound = loaderCam != null && fader != null && icon != null;
            }
            if (!bound) return;
            Sprite s = fader.sprite; float a = fader.color.a;
            if (s == lastSprite && a == lastAlpha) stable++; else { stable = 0; lastSprite = s; lastAlpha = a; }
            bool idle = !SceneLoader.CurrentlyLoading && !SceneLoader.IsInIrisTransition && !SceneLoader.IsInBlurTransition &&
                        (!icon.enabled || icon.color.a <= 0f) &&
                        (!fader.enabled || a <= 0f || (stable >= 15 && s != null && s.name.StartsWith("iris")));
            if (loaderCam.enabled == idle)
            {
                loaderCam.enabled = !idle;
                UnityEngine.Debug.Log("[cupheadport] camera trim: frame " + Time.frameCount + " scene loader camera " + (idle ? "off" : "on") + " (fader " +
                    (s != null ? s.name : "none") + " a=" + a.ToString("F2") + ", loading " + SceneLoader.CurrentlyLoading + ", iris " + SceneLoader.IsInIrisTransition + ")");
            }
        }

        static void Clear()
        {
            Camera game = null;
            if (CupheadLevelCamera.Current != null) game = CupheadLevelCamera.Current.camera;
            else if (CupheadMapCamera.Current != null) game = CupheadMapCamera.Current.camera;
            if (clearCam == null)
            {
                if (game == null) return;
                foreach (Camera c in Camera.allCameras)
                    if (c.name == "ClearCamera" && c.cullingMask == 0 && c.targetTexture == null) { clearCam = c; break; }
                if (clearCam == null) return;
            }
            bool covered = game != null && game.isActiveAndEnabled && game.targetTexture == null &&
                           (game.clearFlags == CameraClearFlags.SolidColor || game.clearFlags == CameraClearFlags.Skybox) &&
                           game.depth > clearCam.depth && game.rect == new Rect(0f, 0f, 1f, 1f);
            if (clearCam.enabled == covered)
            {
                clearCam.enabled = !covered;
                UnityEngine.Debug.Log("[cupheadport] camera trim: ClearCamera " + (covered ? "off" : "on"));
            }
        }
    }
}
