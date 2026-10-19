using System;
using UnityEngine;

namespace CupheadPort
{
    // Cuphead letterboxes to 16:9 (AbstractCupheadCamera.UpdateRect): bars above and below on the
    // RGSP's 3:2 screen. By default the cameras fill 3:2 and 16:10 screens instead and the sides are
    // cropped (the full height is kept; on 3:2 about 7.8 % is cut from each side). Narrower screens
    // (4:3, square) keep the game's letterbox: a crop there would cut 12-22 % from each side of the
    // play field. CUPHEAD_ASPECT=16:9: always the letterbox; CUPHEAD_ASPECT=crop: crop any screen.
    // Screens 16:9 or wider keep the game's behaviour either way.
    public static class Aspect
    {
        const float NarrowestCrop = 1.45f;   // 3:2 is 1.5, 4:3 is 1.33
        static int crop = -1;                // 0 never, 1 3:2 and wider, 2 always

        public static bool Crop()
        {
            if (crop < 0)
            {
                string v = (Environment.GetEnvironmentVariable("CUPHEAD_ASPECT") ?? "").Trim();
                crop = v == "16:9" ? 0 : v == "crop" ? 2 : 1;
            }
            return crop > 0;
        }

        static int rescales;
        static readonly System.Collections.Generic.HashSet<int> seen = new System.Collections.Generic.HashSet<int>();
        static int checkedFrame = -1;
        static bool letterboxScene;

        // The shops (Porkrind's Emporium, the DLC shop) lay out their UI in the scene itself (world-space
        // canvases: button help, coin counters, item cards) across the whole 16:9 width, so a crop cuts
        // it off. They keep the game's own letterbox; the crop comes back when the shop is left.
        static bool LetterboxScene()
        {
            if (Time.frameCount != checkedFrame)
            {
                checkedFrame = Time.frameCount;
                letterboxScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("scene_shop");
            }
            return letterboxScene;
        }

        // Called at the start of AbstractCupheadCamera.UpdateRect; true = handled (skip the original).
        // Every camera fills the screen at full height (the sides of the 16:9 picture are cropped).
        // The UI canvases get the scale they have in the game's letterbox, so the 16:9 layout of
        // menus, button hints and the HUD fits the screen's width and nothing is cut off. The game
        // sets that scale when a camera's viewport changes (each new scene, from full screen to the
        // letterbox); here the viewport stays full screen, so it is done for every new camera.
        public static bool UpdateRect(Camera camera)
        {
            if (!Crop() || camera == null || LetterboxScene()) return false;
            float aspect = (float)Screen.width / Screen.height;
            if (aspect >= 1.7777778f || (crop == 1 && aspect < NarrowestCrop)) return false;
            float o = 1f - 0.1f * SettingsData.Data.overscan;
            Rect rect = new Rect(0.5f - o / 2f, 0.5f - o / 2f, o, o);
            bool fresh = seen.Add(camera.GetInstanceID());
            if (camera.rect != rect || fresh)
            {
                if (Environment.GetEnvironmentVariable("CUPHEAD_MEMLOG") == "1" && ++rescales % 300 == 1)
                    UnityEngine.Debug.Log("[cupheadport] aspect: canvas scale set " + rescales + " times (camera " + camera.name +
                        " rect " + camera.rect + (fresh ? ", new camera)" : ")"));
                camera.rect = rect;
                float letterboxHeight = o * aspect / 1.7777778f;
                foreach (UnityEngine.UI.CanvasScaler scaler in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.CanvasScaler>())
                    scaler.referenceResolution = new Vector2(1280f / letterboxHeight, 720f / letterboxHeight);
            }
            return true;
        }
    }
}
