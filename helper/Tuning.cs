using System;
using UnityEngine;

namespace CupheadPort
{
    // Engine settings that cost CPU under box64, adjustable from options.txt (all off by default
    // until measured):
    //   CUPHEAD_TUNE_AUTOSYNC=0   Physics2D.autoSyncTransforms off: moved transforms reach the 2D
    //                             physics engine at each physics step instead of before every query
    //   CUPHEAD_TUNE_ITER=v,p     2D physics solver iterations (game default 8,3)
    //   CUPHEAD_TUNE_VOICES=n     audio voices FMOD actually mixes (game default 32)
    //   CUPHEAD_TUNE_AUDIORATE=hz audio output rate (default: the system's, 48000)
    //   CUPHEAD_TUNE_DSPBUF=n     samples per FMOD mixing step (default: FMOD's; larger = fewer wake-ups)
    //   CUPHEAD_ANIMCULL          animators whose sprites are all off screen are not evaluated while
    //                             they are, where that changes nothing but the picture there: the maps and
    //                             run-and-gun levels animate scores of objects nobody sees (RGSP: World 1
    //                             map 35.7 -> 42.1 fps, Treetop Trouble 20.4 -> 24.1, Forest Follies 31.2 ->
    //                             32.3). Default (exact), on the maps and in run-and-gun levels, from what
    //                             each clip animates (Cuphead_Data/animclips.txt, written by the setup):
    //                             stopped (CullCompletely) when nothing below has a collider and every clip
    //                             only changes sprites and has no events; not evaluated (CullUpdateTransforms:
    //                             its state machine and events go on, Unity writes none of its values) when
    //                             every clip only changes sprites and transforms and no animated transform
    //                             carries a collider; else left alone. A paused decoration picks up its loop
    //                             where it was, not where the game's would be. "fast" = the earlier rule
    //                             (enemies' animated hitboxes stand still off screen too: Forest +10%),
    //                             =1 / =2 its map / level rule everywhere, =0 off.
    public static class Tuning
    {
        static bool applied, audioApplied;
        static int animCull = -1;
        static bool animCullMild;      // CUPHEAD_ANIMCULL_MILD=1: decoration too only stops writing (CullUpdateTransforms)
        static float nextAnimSweep;
        static readonly System.Collections.Generic.HashSet<int> animSeen = new System.Collections.Generic.HashSet<int>();

        // CUPHEAD_ANIMCULL=exact: per clip (Cuphead_Data/animclips.txt, written by the setup from the
        // game's own files: name, S = only SpriteRenderer curves and no events, the path hashes of the
        // transforms it animates):
        //   2 = stopped off screen (CullCompletely): no collider below, every clip S;
        //   1 = not evaluated off screen (CullUpdateTransforms: Unity then writes none of its animated
        //       values, sprites included - measured with CullProbe - while its state machine and events
        //       go on): every clip H or S (only SpriteRenderer and Transform curves) and no animated
        //       transform has a collider on it or below it, so nothing that collides or plays stands
        //       still where the game would change it;
        //   0 = left as the game has it (also when a clip is not in the list).
        sealed class ClipInfo { public bool sprites, visualOnly; public uint[] paths; }
        static System.Collections.Generic.Dictionary<string, ClipInfo> clipInfo;
        static int exactPaths, exactMissing;

        static void LoadClips()
        {
            clipInfo = new System.Collections.Generic.Dictionary<string, ClipInfo>();
            string f = Environment.GetEnvironmentVariable("CUPHEAD_ANIMCLIPS");
            if (string.IsNullOrEmpty(f))
            {
                f = System.IO.Path.Combine(Application.dataPath, "animclips.txt");
                // Android player: dataPath is the APK/OBB itself; the list sits beside it
                if (!System.IO.File.Exists(f)) f = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "animclips.txt");
            }
            try
            {
                foreach (string l in System.IO.File.ReadAllLines(f))
                {
                    string[] p = l.Split('\t');
                    if (p.Length < 3 || p[0].Length == 0) continue;
                    var ci = new ClipInfo { sprites = p[1] == "S", visualOnly = p[1] == "S" || p[1] == "H" };
                    string[] h = p[2].Length > 0 ? p[2].Split(',') : new string[0];
                    ci.paths = new uint[h.Length];
                    for (int i = 0; i < h.Length; i++) ci.paths[i] = uint.Parse(h[i]);
                    clipInfo[p[0]] = ci;
                }
            }
            catch (Exception e) { UnityEngine.Debug.Log("[cupheadport] animator culling exact: no clip list (" + e.Message + "), nothing culled"); }
            UnityEngine.Debug.Log("[cupheadport] animator culling exact: " + clipInfo.Count + " clips");
        }

        static uint[] crcTable;

        // Unity's binding path hash: CRC-32 (zlib's) of the path below the animator, "" for itself
        static uint Crc(string s)
        {
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint i = 0; i < 256; i++) { uint c = i; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; crcTable[i] = c; }
            }
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in System.Text.Encoding.UTF8.GetBytes(s)) crc = crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }

        static void AddPaths(Transform t, string path, System.Collections.Generic.Dictionary<uint, Transform> map)
        {
            map[Crc(path)] = t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform c = t.GetChild(i);
                AddPaths(c, path.Length == 0 ? c.name : path + "/" + c.name, map);
            }
        }

        static int Exact(Animator a, bool hitbox)
        {
            RuntimeAnimatorController rc = a.runtimeAnimatorController;
            if (rc == null) return 0;
            AnimationClip[] clips = rc.animationClips;
            if (clips == null || clips.Length == 0) return 0;
            bool allSprites = true;
            System.Collections.Generic.Dictionary<uint, Transform> map = null;
            foreach (AnimationClip clip in clips)
            {
                ClipInfo ci;
                if (clip == null || !clipInfo.TryGetValue(clip.name, out ci) || !ci.visualOnly) return 0;
                if (!ci.sprites) allSprites = false;
                foreach (uint h in ci.paths)
                {
                    if (map == null) { map = new System.Collections.Generic.Dictionary<uint, Transform>(); AddPaths(a.transform, "", map); }
                    Transform t;
                    exactPaths++;
                    if (!map.TryGetValue(h, out t)) { exactMissing++; continue; }      // a binding to a child this object does not have
                    if (t.GetComponentInChildren<Collider2D>(true) != null) return 0;
                }
            }
            return !hitbox && allSprites ? 2 : 1;
        }

        static bool ShowsSprite(Animator a)
        {
            foreach (SpriteRenderer r in a.GetComponentsInChildren<SpriteRenderer>())
                if (r.enabled && r.sprite != null) return true;
            return false;
        }

        // CUPHEAD_MEMLOG=1: which of the animators culled here have animation events (functions called
        // from their clips: sounds, spawns), logged with the sweep; a stopped animator skips them
        static readonly System.Collections.Generic.Dictionary<string, int> eventFns = new System.Collections.Generic.Dictionary<string, int>();
        static int withEvents, stoppedWithEvents;

        static void NoteEvents(Animator a)
        {
            RuntimeAnimatorController c = a.runtimeAnimatorController;
            if (c == null) return;
            bool any = false;
            foreach (AnimationClip clip in c.animationClips)
                if (clip != null)
                    foreach (AnimationEvent e in clip.events)
                    {
                        any = true;
                        string k = e.functionName + (a.cullingMode == AnimatorCullingMode.CullCompletely ? " (stopped)" : "");
                        int n; eventFns.TryGetValue(k, out n); eventFns[k] = n + 1;
                    }
            if (any) { withEvents++; if (a.cullingMode == AnimatorCullingMode.CullCompletely) stoppedWithEvents++; }
        }

        // 2 s after each scene load, then every 10 s: animators that appeared since the last sweep
        // (FindObjectsOfType scans every object: a few ms on the RGSP).
        static void SweepAnimators()
        {
            if (animCull < 0)
            {
                string v = Environment.GetEnvironmentVariable("CUPHEAD_ANIMCULL");
                animCull = v == "0" ? 0 : v == "1" ? 1 : v == "2" ? 2 : v == "fast" ? 3 : 6;
                if (animCull == 6) LoadClips();
                animCullMild = Environment.GetEnvironmentVariable("CUPHEAD_ANIMCULL_MILD") == "1";
                if (animCull > 0) UnityEngine.SceneManagement.SceneManager.sceneLoaded += (sc, m) => { animSeen.Clear(); eventFns.Clear(); withEvents = stoppedWithEvents = 0; nextAnimSweep = Time.realtimeSinceStartup + 2f; };
            }
            if (animCull == 0 || Time.realtimeSinceStartup < nextAnimSweep) return;
            nextAnimSweep = Time.realtimeSinceStartup + 10f;
            int rule = animCull;
            if (rule == 6)
            {
                string sc6 = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (!sc6.StartsWith("scene_map") && !sc6.StartsWith("scene_level_platforming")) return;
                if (clipInfo == null || clipInfo.Count == 0) return;
            }
            else if (rule == 3)
            {
                string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                rule = scene.StartsWith("scene_map") ? 1 : scene.StartsWith("scene_level_platforming") ? 2 : 0;
                if (rule == 0) return;
            }
            int culled = 0, stopped = 0;
            var sweepWatch = System.Diagnostics.Stopwatch.StartNew();
            Animator[] all = UnityEngine.Object.FindObjectsOfType<Animator>();
            long findMs = sweepWatch.ElapsedMilliseconds;
            foreach (Animator a in all)
            {
                if (!animSeen.Add(a.GetInstanceID())) continue;
                if (a.cullingMode != AnimatorCullingMode.AlwaysAnimate || !ShowsSprite(a)) continue;
                bool hitbox = a.GetComponentInChildren<Collider2D>(true) != null;
                if (rule == 1 && hitbox) continue;
                if (rule == 6)
                {
                    int how = Exact(a, hitbox);
                    if (how == 0) continue;
                    a.cullingMode = how == 2 ? AnimatorCullingMode.CullCompletely : AnimatorCullingMode.CullUpdateTransforms;
                    if (how == 2) stopped++; else culled++;
                    continue;
                }
                // decoration (no collider): stopped entirely while off screen (state machine and graph
                // too, most of the cost); anything with a collider only stops writing its values
                a.cullingMode = hitbox || animCullMild ? AnimatorCullingMode.CullUpdateTransforms : AnimatorCullingMode.CullCompletely;
                if (a.cullingMode == AnimatorCullingMode.CullCompletely) stopped++; else culled++;
                if (Environment.GetEnvironmentVariable("CUPHEAD_MEMLOG") == "1") NoteEvents(a);
            }
            if (Environment.GetEnvironmentVariable("CUPHEAD_MEMLOG") == "1")
                UnityEngine.Debug.Log("[cupheadport] animator sweep: " + all.Length + " animators, search " + findMs + " ms, all " + sweepWatch.ElapsedMilliseconds + " ms");
            if (culled + stopped > 0 && Environment.GetEnvironmentVariable("CUPHEAD_MEMLOG") == "1")
            {
                UnityEngine.Debug.Log("[cupheadport] animator culling: " + stopped + " stopped and " + culled + " not written while off screen (" +
                    animSeen.Count + " animators seen in this scene)");
                if (rule == 6) UnityEngine.Debug.Log("[cupheadport] animator culling exact: animated transform paths " + exactPaths + ", not found below their animator " + exactMissing);
                var sb = new System.Text.StringBuilder("[cupheadport] animator culling, clips with events: " + withEvents + " animators (" + stoppedWithEvents + " stopped):");
                foreach (var kv in eventFns) sb.Append(" " + kv.Key + "=" + kv.Value);
                UnityEngine.Debug.Log(sb.ToString());
            }
        }

        static int Int(string name, int fallback)
        {
            int v;
            string s = Environment.GetEnvironmentVariable(name);
            return s != null && int.TryParse(s.Trim(), out v) ? v : fallback;
        }

        // Called every frame (Diagnostics.Tick); settings are applied once.
        static int vsync = -2;
        static int vsyncFrame;

        // CUPHEAD_VSYNC=0/1 (tests): keeps QualitySettings.vSyncCount at that value, over the game's
        // own setting (Options > Visual > V-Sync).
        static void ForceVsync()
        {
            if (vsync == -2) vsync = Int("CUPHEAD_VSYNC", -1);
            if (vsync < 0 || ++vsyncFrame % 60 != 0 || QualitySettings.vSyncCount == vsync) return;
            QualitySettings.vSyncCount = vsync;
            UnityEngine.Debug.Log("[cupheadport] vSyncCount " + vsync + " (CUPHEAD_VSYNC), target " + Application.targetFrameRate + " fps");
        }

        public static void Tick()
        {
            SweepAnimators();
            ForceVsync();
            if (!audioApplied)
            {
                audioApplied = true;
                int voices = Int("CUPHEAD_TUNE_VOICES", 0), rate = Int("CUPHEAD_TUNE_AUDIORATE", 0), dsp = Int("CUPHEAD_TUNE_DSPBUF", 0);
                if (voices > 0 || rate > 0 || dsp > 0)
                {
                    AudioConfiguration c = AudioSettings.GetConfiguration();
                    if (voices > 0) c.numRealVoices = voices;
                    if (rate > 0) c.sampleRate = rate;
                    if (dsp > 0) c.dspBufferSize = dsp;
                    bool ok = AudioSettings.Reset(c);
                    c = AudioSettings.GetConfiguration();
                    UnityEngine.Debug.Log("[cupheadport] audio: " + c.numRealVoices + " real voices, " + c.sampleRate + " Hz, DSP buffer " +
                        c.dspBufferSize + (ok ? "" : " (reset failed)"));
                }
            }
            if (applied) return;
            applied = true;
            if (Environment.GetEnvironmentVariable("CUPHEAD_TUNE_AUTOSYNC") == "0")
            {
                Physics2D.autoSyncTransforms = false;
                UnityEngine.Debug.Log("[cupheadport] Physics2D.autoSyncTransforms off");
            }
            string iter = Environment.GetEnvironmentVariable("CUPHEAD_TUNE_ITER");
            if (!string.IsNullOrEmpty(iter))
            {
                string[] p = iter.Split(',');
                int v, q;
                if (p.Length == 2 && int.TryParse(p[0], out v) && int.TryParse(p[1], out q))
                {
                    Physics2D.velocityIterations = v;
                    Physics2D.positionIterations = q;
                    UnityEngine.Debug.Log("[cupheadport] Physics2D iterations " + v + "," + q);
                }
            }
        }
    }
}
