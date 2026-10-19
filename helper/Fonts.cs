using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CupheadPort
{
    // Cuphead loads all 50 font bundles at start-up (about 130 MB of Japanese, Korean and Chinese
    // fonts and SDF atlases besides its own). Only the Cuphead fonts are loaded at start-up here;
    // the first time the game asks for any other font, the rest are loaded synchronously, so every
    // language keeps working.
    public static class Fonts
    {
        static readonly string[] CoreFonts = { "CupheadFelix", "CupheadHenriette", "CupheadMemphis",
                                               "CupheadPoster", "CupheadVogue", "DSRefinedLetterB", "FBBlue" };
        const BindingFlags Any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static bool deferredLoaded;

        static bool IsCore(string enumName)
        {
            foreach (var c in CoreFonts)
                if (enumName.StartsWith(c)) return true;
            return false;
        }

        static IDictionary Cache(string field) => (IDictionary)typeof(FontLoader).GetField(field, Any).GetValue(null);

        static void AddTMPObjects(FontLoader.TMPFontType key, UnityEngine.Object[] objects)
        {
            var tmp = Cache("TMPFontCache");
            var materials = Cache("TMPMaterialCache");
            foreach (var obj in objects)
            {
                if (obj is Material)
                {
                    if (!materials.Contains(obj.name)) materials.Add(obj.name, obj);
                }
                else if (obj.GetType().Name == "TMP_FontAsset")
                {
                    if (!tmp.Contains(key)) tmp.Add(key, obj);
                }
            }
        }

        // Replaces FontLoader.Initialize: the same asynchronous loads, Cuphead's own fonts only.
        public static Coroutine[] Initialize()
        {
            var list = new List<Coroutine>();
            foreach (FontLoader.FontType t in Enum.GetValues(typeof(FontLoader.FontType)))
            {
                if (t == FontLoader.FontType.None || !IsCore(t.ToString())) continue;
                var key = t;
                list.Add(AssetBundleLoader.LoadFont(t.ToString(), FontLoader.GetFilename(t), f => Cache("FontCache").Add(key, f)));
            }
            foreach (FontLoader.TMPFontType t in Enum.GetValues(typeof(FontLoader.TMPFontType)))
            {
                if (t == FontLoader.TMPFontType.None || !IsCore(t.ToString())) continue;
                var key = t;
                list.Add(AssetBundleLoader.LoadTMPFont(t.ToString(), objs => AddTMPObjects(key, objs)));
            }
            return list.ToArray();
        }

        static UnityEngine.Object[] LoadBundleSync(string bundleName)
        {
            var loaderT = typeof(AssetBundleLoader);
            var instance = loaderT.GetField("Instance", Any).GetValue(null);
            var locT = loaderT.GetNestedType("AssetBundleLocation", BindingFlags.NonPublic);
            var m = loaderT.GetMethod("loadAllAssetsSynchronous", Any).MakeGenericMethod(typeof(UnityEngine.Object));
            return (UnityEngine.Object[])m.Invoke(instance, new object[] { bundleName, Enum.Parse(locT, "StreamingAssets") });
        }

        // Loads every font that Initialize left out (once).
        public static void LoadDeferred()
        {
            if (deferredLoaded) return;
            deferredLoaded = true;
            float t0 = Time.realtimeSinceStartup;
            var fonts = Cache("FontCache");
            foreach (FontLoader.FontType t in Enum.GetValues(typeof(FontLoader.FontType)))
            {
                if (t == FontLoader.FontType.None || IsCore(t.ToString()) || fonts.Contains(t)) continue;
                string file = FontLoader.GetFilename(t);
                Font found = null;
                foreach (var o in LoadBundleSync(AssetBundleLoader.AssetBundlePrefixFont + t.ToString().ToLowerInvariant()))
                    if (o is Font && (found == null || o.name == file)) found = (Font)o;
                if (found != null) fonts.Add(t, found);
            }
            foreach (FontLoader.TMPFontType t in Enum.GetValues(typeof(FontLoader.TMPFontType)))
            {
                if (t == FontLoader.TMPFontType.None || IsCore(t.ToString()) || Cache("TMPFontCache").Contains(t)) continue;
                AddTMPObjects(t, LoadBundleSync(AssetBundleLoader.AssetBundlePrefixTMPFont + t.ToString().ToLowerInvariant()));
            }
            UnityEngine.Debug.Log("[cupheadport] loaded the other languages' fonts in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s");
        }

        // Called at the start of FontLoader.GetFont / GetTMPFont / GetTMPMaterial.
        public static void BeforeGetFont(FontLoader.FontType t)
        {
            if (!deferredLoaded && t != FontLoader.FontType.None && !Cache("FontCache").Contains(t)) LoadDeferred();
        }

        public static void BeforeGetTMPFont(FontLoader.TMPFontType t)
        {
            if (!deferredLoaded && t != FontLoader.TMPFontType.None && !Cache("TMPFontCache").Contains(t)) LoadDeferred();
        }

        public static void BeforeGetTMPMaterial(string name)
        {
            if (!deferredLoaded && !Cache("TMPMaterialCache").Contains(name)) LoadDeferred();
        }
    }
}
