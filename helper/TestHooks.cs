using System;

namespace CupheadPort
{
    // Benchmarks (codepatch's bench-level-hook calls this from SlotSelectScreen.EnterGame):
    // CUPHEAD_TEST_LEVEL=<Levels name> starts that level when a save slot is chosen, and
    // CUPHEAD_TEST_LEVEL=Map1 (Map2, Map3) the World 1 (2, 3) map, so they can be measured without playing up to them.
    // CUPHEAD_TEST_PLAYLIST=<Levels name>,<Levels name>,... plays them in turn in one session: the first
    // when a save slot is chosen (if CUPHEAD_TEST_LEVEL is unset), the next after each win screen, or
    // after CUPHEAD_TEST_MAXSECS (default 300) in a level.
    // CUPHEAD_TEST_BOSSTIME=<seconds>: a boss's health drains over that many seconds (the level's own
    // properties.DealDamage, so every phase starts at its real health trigger), besides the player's hits.
    // CUPHEAD_TEST_UNSTICK=1 (or =<units>, default 150; the world is ~1 unit per screen pixel): in
    // run-and-gun levels, when the player made less than units/8 of progress in two seconds, lift them
    // that far forward and 4/3 of it up (over the obstacle; godmode covers the rest).
    // CUPHEAD_TEST_SKIPTO=<boss state name> (profiling one phase): 6 s into a boss level, the game's own
    // debug step properties.DealDamageToNextNamedState() until that state is current.
    // CUPHEAD_TEST_DISABLE_CAMERA=<name>[,<name>] (profiling): cameras by GameObject name switched off
    // in every level (to measure what one camera's rendering costs; changes the picture).
    // CUPHEAD_TEST_DISABLE_EFFECT=<camera>/<component type>[,...] (profiling): image effects switched off.
    // CUPHEAD_TEST_QUIT=1: quit the game when the playlist is done.
    // CUPHEAD_TEST_STALL=<seconds> (default 10 with CUPHEAD_TEST_BENCH): when a boss's health has not moved
    // for that long (shots cannot reach the damageable part) after it had taken hits, 5% of it through properties.DealDamage, the
    // call the boss's own hit handler makes, so its states change as in play.
    // CUPHEAD_TEST_WEAPON=<Weapon name, or homing>: player one's primary weapon for every test level (the
    // Chaser, level_weapon_homing, hits a boss wherever it is, so the boss's own scripts run each phase).
    // CUPHEAD_TEST_MEGADAMAGE=1: the game's own debug switch (DamageReceiver.DEBUG_DO_MEGA_DAMAGE: hits on
    // enemies x10), for shorter fights through the same damage path.
    // CUPHEAD_TEST_BENCH=1: every 5 s logs "[cupheadport bench] <level> state=<boss state> hp=<%> x=<player x>"
    // (in a scene without a level, e.g. a map: "<scene name> ... state=Scene").
    // Playlist entries may also be scenes: scene:<Scenes name> (a world map, a cutscene, the kitchen ...),
    // or Map1..Map4 / MapDLC; such an entry ends after CUPHEAD_TEST_MAXSECS or when another scene loads.
    // CUPHEAD_TEST_PHASEHOLD=<seconds> (coverage): every boss state is held that long (the boss's health
    // is kept just above the next state's trigger, from OnBossDamaged, before the game checks for a
    // state change), then the game's own debug step (DealDamageToNextNamedState) starts the next state.
    // The last state is released after its hold and the fight ends as usual (shots, stall fallback),
    // so every phase gets the same measured time whether the player's shots are fast or slow and
    // parry-only bosses move on too.
    // CUPHEAD_TEST_MODE=easy|normal|hard: the difficulty for every test level.
    // CUPHEAD_TEST_CHARM=<Charm name> (e.g. charm_chalice: play as Ms. Chalice): player one's charm.
    // CUPHEAD_TEST_DETERMINISTIC=<seed> (A/B of exact optimizations, e.g. in the emulator): game time
    // advances 1/60 s per frame however long a frame takes (Time.captureFramerate), and UnityEngine.Random
    // is seeded with <seed> when each level starts, so a level replays frame for frame.
    // CUPHEAD_TEST_SHOTS=<frame>,<frame>,...: screenshots that many frames into each level, by Unity
    // itself at the end of that frame (persistentDataPath/<level>-f<frame>.png).
    // Tick() (every frame, all builds) only acts with these variables set.
    public static class TestHooks
    {
        // true: a scene was started (the game's own choice is skipped)
        public static bool TryEnter()
        {
            string v = Environment.GetEnvironmentVariable("CUPHEAD_TEST_LEVEL");
            if (string.IsNullOrEmpty(v) && Playlist().Length > 0) v = Playlist()[playIndex = 0];
            if (string.IsNullOrEmpty(v)) return false;
            Enter(v, SceneLoader.Transition.Iris);
            return true;
        }

        // "scene:<Scenes name>", Map1..Map4, MapDLC: the scene's name; null for a Levels name
        static string SceneOf(string v)
        {
            if (v.StartsWith("scene:")) return v.Substring(6);
            if (v == "MapDLC") return "scene_map_world_DLC";
            if (v.Length == 4 && v.StartsWith("Map") && char.IsDigit(v[3])) return "scene_map_world_" + v[3];
            return null;
        }

        static string sceneEntry;          // the scene of the current entry, if it is one

        static void Enter(string v, SceneLoader.Transition tr)
        {
            Equip();
            sceneEntry = SceneOf(v);
            if (sceneEntry != null) SceneLoader.LoadScene((Scenes)Enum.Parse(typeof(Scenes), sceneEntry), tr, SceneLoader.Transition.Iris);
            else SceneLoader.LoadLevel((Levels)Enum.Parse(typeof(Levels), v), tr, SceneLoader.Icon.Hourglass);
        }

        static void Equip()
        {
            string w = Environment.GetEnvironmentVariable("CUPHEAD_TEST_WEAPON");
            if (!string.IsNullOrEmpty(w))
            {
                var weapon = (Weapon)Enum.Parse(typeof(Weapon), w == "homing" ? "level_weapon_homing" : w);
                PlayerData.Data.Loadouts.GetPlayerLoadout(PlayerId.PlayerOne).primaryWeapon = weapon;
            }
            if (Environment.GetEnvironmentVariable("CUPHEAD_TEST_MEGADAMAGE") == "1") DamageReceiver.DEBUG_DO_MEGA_DAMAGE = true;
            string md = Environment.GetEnvironmentVariable("CUPHEAD_TEST_MODE");
            if (!string.IsNullOrEmpty(md))
                Level.SetCurrentMode(md == "easy" ? Level.Mode.Easy : md == "hard" ? Level.Mode.Hard : Level.Mode.Normal);
            string ch = Environment.GetEnvironmentVariable("CUPHEAD_TEST_CHARM");
            if (!string.IsNullOrEmpty(ch))
                PlayerData.Data.Loadouts.GetPlayerLoadout(PlayerId.PlayerOne).charm = (Charm)Enum.Parse(typeof(Charm), ch);
        }

        // CUPHEAD_TEST_NEXT=<seconds>:<Iris|Fade|None>:<Map1 or a Levels name> (tests): that many seconds
        // into the first level or map, leave for the given scene with that transition (once).
        static string next;
        static float nextAt = -1f;

        static void NextScene()
        {
            if (next == null)
            {
                next = Environment.GetEnvironmentVariable("CUPHEAD_TEST_NEXT") ?? "";
                if (next.Length > 0)
                    UnityEngine.SceneManagement.SceneManager.sceneLoaded += (sc, m) =>
                    {
                        if (nextAt < 0f && next.Length > 0 && (sc.name.StartsWith("scene_level") || sc.name.StartsWith("scene_map")))
                            nextAt = UnityEngine.Time.realtimeSinceStartup + float.Parse(next.Split(':')[0]);
                    };
            }
            if (nextAt <= 0f || UnityEngine.Time.realtimeSinceStartup < nextAt) return;
            nextAt = 0f;
            string[] p = next.Split(':');
            var tr = (SceneLoader.Transition)Enum.Parse(typeof(SceneLoader.Transition), p[1]);
            UnityEngine.Debug.Log("[cupheadport] test: frame " + UnityEngine.Time.frameCount + " leaving for " + p[2] + " (" + tr + ")");
            if (p[2] == "Map1") SceneLoader.LoadScene(Scenes.scene_map_world_1, tr, SceneLoader.Transition.Iris);
            else SceneLoader.LoadScene(LevelProperties.GetLevelScene((Levels)Enum.Parse(typeof(Levels), p[2])), tr, SceneLoader.Transition.Iris);
        }

        // ---- playlist, boss drain, unstick, bench log --------------------------------------------
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        static string[] playlist;
        static int playIndex = -1;
        static float nextAt2 = -1f, stall = -1f, lastHp = -1f, lastHpAt;
        static bool hpMoved, skipped;
        static string skipTo;
        static System.Reflection.MethodInfo nextNamedState;      // the level's health takes hits (The Root Pack's never does: its vegetables have their own)
        static float maxSecs = -1f, bossTime = -2f, levelStart = -1f, wonAt = -1f, nextLog, stuckCheck, stuckX;
        static int bench = -1;
        static float unstick = -1f;
        static Level seenLevel;
        static object props;
        static System.Reflection.MethodInfo dealDamage;
        static System.Reflection.PropertyInfo pCurrent, pState;
        static System.Reflection.FieldInfo fTotal;
        static float phaseHold = -1f, stateSince;
        static string holdState;
        static bool stepping, holdReleased;
        static System.Reflection.MethodInfo nextTrigger, setHealth;

        static string[] Playlist()
        {
            if (playlist == null)
            {
                string v = Environment.GetEnvironmentVariable("CUPHEAD_TEST_PLAYLIST") ?? "";
                playlist = v.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                float.TryParse(Environment.GetEnvironmentVariable("CUPHEAD_TEST_MAXSECS") ?? "300", out maxSecs);
                if (playlist.Length > 0)
                    UnityEngine.SceneManagement.SceneManager.sceneLoaded += (sc, m) =>
                    {
                        if (sceneEntry != null && playIndex >= 0 && playIndex < playlist.Length)
                        {
                            if (sc.name == sceneEntry && levelStart < 0f) { levelStart = UnityEngine.Time.realtimeSinceStartup; return; }
                            if (sc.name == sceneEntry || sc.name == "scene_load_helper" || levelStart < 0f) return;
                            UnityEngine.Debug.Log("[cupheadport bench] " + playlist[playIndex] + " finished (" + sc.name + ")");
                            levelStart = -1f; sceneEntry = null;
                            nextAt2 = UnityEngine.Time.realtimeSinceStartup + 5f;
                            return;
                        }
                        // the win screen (or a map after a level) ends a playlist entry
                        if (playIndex >= 0 && levelStart > 0f && (sc.name == "scene_win" || sc.name.StartsWith("scene_map")))
                        {
                            UnityEngine.Debug.Log("[cupheadport bench] " + playlist[playIndex] + " finished (" + sc.name + ")");
                            levelStart = -1f;
                            nextAt2 = UnityEngine.Time.realtimeSinceStartup + 5f;   // let that scene finish its transition
                        }
                    };
            }
            return playlist;
        }

        static void PlayNext(string why)
        {
            UnityEngine.Debug.Log("[cupheadport bench] " + (playIndex >= 0 && playIndex < playlist.Length ? playlist[playIndex] : "?") + " " + why);
            levelStart = -1f; wonAt = -1f; seenLevel = null;
            if (++playIndex >= playlist.Length)
            {
                UnityEngine.Debug.Log("[cupheadport bench] playlist done");
                playIndex = playlist.Length;
                if (Environment.GetEnvironmentVariable("CUPHEAD_TEST_QUIT") == "1") UnityEngine.Application.Quit();
                return;
            }
            UnityEngine.Debug.Log("[cupheadport bench] next: " + playlist[playIndex]);
            Enter(playlist[playIndex], SceneLoader.Transition.Fade);
        }

        static int detSeed = -2, levelFrame0, sceneFrame0;
        static string sceneName;
        static int[] shots;

        static void Deterministic()
        {
            if (detSeed == -2)
            {
                if (!int.TryParse(Environment.GetEnvironmentVariable("CUPHEAD_TEST_DETERMINISTIC") ?? "", out detSeed)) detSeed = -1;
                if (detSeed >= 0)
                {
                    UnityEngine.Time.captureFramerate = 60;
                    UnityEngine.Debug.Log("[cupheadport bench] deterministic: 60 frames a second of game time, seed " + detSeed);
                    // before the scene's Start methods draw from it; and the film grain, which runs
                    // from boot, starts over (its frame would depend on how long the menus took)
                    UnityEngine.SceneManagement.SceneManager.sceneLoaded += (sc, md) =>
                    {
                        UnityEngine.Random.InitState(detSeed);
                        if (sc.name != "scene_load_helper") { sceneFrame0 = UnityEngine.Time.frameCount; sceneName = sc.name; }
                        // particle systems pick their own random seed: fixed ones (scene systems; ones spawned later stay random)
                        foreach (var ps in UnityEngine.Object.FindObjectsOfType<UnityEngine.ParticleSystem>())
                        {
                            bool playing = ps.isPlaying;
                            ps.Stop(true, UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
                            ps.useAutoRandomSeed = false;
                            ps.randomSeed = (uint)detSeed;
                            if (playing || ps.main.playOnAwake) ps.Play(true);
                        }
                        // no input from the level's first frame (the scripted menu presses run on wall-clock time)
                        if (sc.name.StartsWith("scene_level") || sc.name.StartsWith("scene_map"))
                        {
                            var rp0 = Rewired.ReInput.players.GetPlayer(0);
                            if (rp0 != null) rp0.controllers.maps.SetAllMapsEnabled(false);
                        }
                        foreach (var g in UnityEngine.Object.FindObjectsOfType<ChromaticAberrationFilmGrain>()) RestartGrain(g);
                    };
                }
                var sv = (Environment.GetEnvironmentVariable("CUPHEAD_TEST_SHOTS") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                shots = Array.ConvertAll(sv, int.Parse);
            }
            if (shots.Length == 0 || (seenLevel == null && sceneName == null)) return;
            // frames into the level, or (a map, a cutscene) into the scene
            int f = UnityEngine.Time.frameCount - (seenLevel != null ? levelFrame0 : sceneFrame0);
            if (Array.IndexOf(shots, f) >= 0)
            {
                string name = (seenLevel != null ? seenLevel.GetType().Name.Replace("Level", "") : sceneName.Replace("scene_", "")) + "-f" + f + ".png";
                UnityEngine.ScreenCapture.CaptureScreenshot(name);      // this frame, as rendered (into persistentDataPath)
                UnityEngine.Debug.Log("[cupheadport bench] screenshot " + UnityEngine.Application.persistentDataPath + "/" + name);
            }
        }

        static void RestartGrain(ChromaticAberrationFilmGrain g)
        {
            var t = typeof(ChromaticAberrationFilmGrain);
            g.StopAllCoroutines();
            t.GetField("currentTexture", Any)?.SetValue(g, 0);
            t.GetField("UV_Transform", Any)?.SetValue(g, new UnityEngine.Vector4(1f, 0f, 0f, 1f));
            var cr = t.GetMethod("animate_cr", Any);
            if (cr != null) g.StartCoroutine((System.Collections.IEnumerator)cr.Invoke(g, null));
        }

        static void LevelChanged(Level l)
        {
            levelFrame0 = UnityEngine.Time.frameCount;
            if (detSeed >= 0)
            {
                // no input in a replayed level: the scripted menu presses run on wall-clock time
                var rp = Rewired.ReInput.players.GetPlayer(0);
                if (rp != null) rp.controllers.maps.SetAllMapsEnabled(false);
            }
            // The King's Leap: a won (or lost) chess fight returns to the castle, not to a map
            if (playIndex >= 0 && playIndex < playlist.Length && sceneEntry == null && l is ChessCastleLevel &&
                playlist[playIndex].StartsWith("Chess") && levelStart > 0f && nextAt2 < 0f)
            {
                UnityEngine.Debug.Log("[cupheadport bench] " + playlist[playIndex] + " finished (chess castle)");
                levelStart = -1f;
                nextAt2 = UnityEngine.Time.realtimeSinceStartup + 5f;
            }
            seenLevel = l; props = null; dealDamage = null; camSig = null;
            levelStart = UnityEngine.Time.realtimeSinceStartup; wonAt = -1f; stuckCheck = levelStart + 6f; stuckX = float.NaN;
            hpMoved = false; lastHp = -1f;
            if (bench == 1) LogCameras();
            string de = Environment.GetEnvironmentVariable("CUPHEAD_TEST_DISABLE_EFFECT");   // <camera>/<component type>
            if (!string.IsNullOrEmpty(de))
                foreach (var c in UnityEngine.Camera.allCameras)
                    foreach (string spec in de.Split(','))
                    {
                        string[] cs = spec.Split('/');
                        if (cs.Length != 2 || c.gameObject.name != cs[0]) continue;
                        foreach (var mb in c.GetComponents<UnityEngine.MonoBehaviour>())
                            if (mb != null && mb.GetType().Name == cs[1])
                            {
                                mb.enabled = false;
                                UnityEngine.Debug.Log("[cupheadport bench] effect " + spec + " disabled (test)");
                            }
                    }
            string dc = Environment.GetEnvironmentVariable("CUPHEAD_TEST_DISABLE_CAMERA");
            if (!string.IsNullOrEmpty(dc))
                foreach (var c in UnityEngine.Camera.allCameras)
                    if (Array.IndexOf(dc.Split(','), c.gameObject.name) >= 0)
                    {
                        c.enabled = false;
                        UnityEngine.Debug.Log("[cupheadport bench] camera " + c.gameObject.name + " disabled (test)");
                    }
            for (Type t = l.GetType(); t != null && props == null; t = t.BaseType)
            {
                var f = t.GetField("properties", Any);
                if (f != null) props = f.GetValue(l);
            }
            if (props == null) return;
            Type pt = props.GetType();
            dealDamage = pt.GetMethod("DealDamage", new[] { typeof(float) });
            nextNamedState = pt.GetMethod("DealDamageToNextNamedState", Type.EmptyTypes);
            skipped = false;
            pCurrent = pt.GetProperty("CurrentHealth", Any);
            pState = pt.GetProperty("CurrentState", Any);
            fTotal = pt.GetField("TotalHealth", Any);
            nextTrigger = pt.GetMethod("GetNextStateHealthTrigger", Type.EmptyTypes);
            setHealth = pCurrent != null ? pCurrent.GetSetMethod(true) : null;
            holdState = null; holdReleased = false;
            if (phaseHold > 0f)
            {
                var ev = pt.GetEvent("OnBossDamaged", Any);
                var handler = typeof(TestHooks).GetMethod("OnBossDamaged", Any);
                if (ev != null && handler != null) ev.AddEventHandler(props, Delegate.CreateDelegate(ev.EventHandlerType, handler));
            }
        }

        // the boss was hit (health already lowered, its state not yet updated): while a state is held,
        // keep the health above the next state's trigger (above 0 in the last state)
        static void OnBossDamaged(float damage)
        {
            if (stepping || holdReleased || props == null || setHealth == null || nextTrigger == null || Level.Won) return;
            float total = (float)fTotal.GetValue(props), hp = (float)pCurrent.GetValue(props, null);
            float floor = (float)nextTrigger.Invoke(props, null) * total + 0.01f * total;
            if (hp < floor) setHealth.Invoke(props, new object[] { floor + 0.01f * total });
        }

        // cameras rendering into textures and their image effects (with the effects' settings)
        static void LogCameras()
        {
            UnityEngine.Debug.Log("[cupheadport bench] screen " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height +
                " current " + UnityEngine.Screen.currentResolution.width + "x" + UnityEngine.Screen.currentResolution.height +
                " fullscreen=" + UnityEngine.Screen.fullScreen);
            foreach (var c in UnityEngine.Camera.allCameras)
            {
                var sb = new System.Text.StringBuilder("[cupheadport bench] camera " + c.gameObject.name + " depth=" + c.depth + " px=" + c.pixelWidth + "x" + c.pixelHeight +
                    " mask=0x" + c.cullingMask.ToString("x") + " clear=" + c.clearFlags + " bg=" + c.backgroundColor + (c.orthographic ? " ortho=" + c.orthographicSize : " persp"));
                if (c.targetTexture != null)
                    sb.Append(" rt=" + c.targetTexture.name + " " + c.targetTexture.width + "x" + c.targetTexture.height +
                              " " + c.targetTexture.format + " depthbits=" + c.targetTexture.depth);
                foreach (var mb in c.GetComponents<UnityEngine.MonoBehaviour>())
                {
                    if (mb == null) continue;
                    var t = mb.GetType();
                    if (t.GetMethod("OnRenderImage", Any) == null) continue;
                    sb.Append(" | " + t.Name + (mb.enabled ? "" : "(off)"));
                    foreach (var f in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                        if (f.FieldType.IsPrimitive || f.FieldType.IsEnum) sb.Append(" " + f.Name + "=" + f.GetValue(mb));
                }
                UnityEngine.Debug.Log(sb.ToString());
            }
            // where the cameras' textures are shown
            foreach (var r in UnityEngine.Object.FindObjectsOfType<UnityEngine.Renderer>())
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null || !(mat.mainTexture is UnityEngine.RenderTexture)) continue;
                    UnityEngine.Debug.Log("[cupheadport bench] renderer " + r.name + " layer=" + r.gameObject.layer + " sorting=" + r.sortingLayerName + "/" + r.sortingOrder +
                        " shows " + mat.mainTexture.name + " with " + mat.shader.name + (r.enabled ? "" : " (off)"));
                }
        }

        static string StateName()
        {
            object st = props != null && pState != null ? pState.GetValue(props, null) : null;
            return st?.GetType().GetField("stateName")?.GetValue(st)?.ToString() ?? "-";
        }

        static float camCheckAt;
        static string camSig;

        static void Bench()
        {
            if (bench < 0)
            {
                bench = Environment.GetEnvironmentVariable("CUPHEAD_TEST_BENCH") == "1" ? 1 : 0;
                string u = Environment.GetEnvironmentVariable("CUPHEAD_TEST_UNSTICK");
                unstick = u == "1" ? 150f : float.TryParse(u ?? "", out unstick) ? unstick : 0f;
                if (!float.TryParse(Environment.GetEnvironmentVariable("CUPHEAD_TEST_BOSSTIME") ?? "", out bossTime)) bossTime = 0f;
                if (!float.TryParse(Environment.GetEnvironmentVariable("CUPHEAD_TEST_STALL") ?? "", out stall)) stall = bench == 1 ? 10f : 0f;
                Playlist();
            }
            if (nextAt2 > 0f && UnityEngine.Time.realtimeSinceStartup >= nextAt2) { nextAt2 = -1f; PlayNext("-> next"); }
            Level l = Level.Current;
            if (l == null)
            {
                seenLevel = null;
                Deterministic();
                float t = UnityEngine.Time.realtimeSinceStartup;
                if (sceneEntry != null && levelStart > 0f)
                {
                    if (bench == 1 && t >= nextLog)
                    {
                        nextLog = t + 5f;
                        UnityEngine.Debug.Log("[cupheadport bench] " + sceneEntry.Replace("scene_", "") + " t=" + (t - levelStart).ToString("F0") + " state=Scene hp=- x=-");
                    }
                    if (t - levelStart > maxSecs) { sceneEntry = null; PlayNext("time limit"); }
                }
                return;
            }
            if (l != seenLevel) LevelChanged(l);
            Deterministic();
            float now = UnityEngine.Time.realtimeSinceStartup;
            float total = props != null && fTotal != null ? (float)fTotal.GetValue(props) : 0f;
            float hp = props != null && pCurrent != null ? (float)pCurrent.GetValue(props, null) : 0f;
            // boss drain, from 4 s in (after the intro), until the knockout
            if (bossTime > 0f && dealDamage != null && total > 0f && hp > 0f && !Level.Won && now - levelStart > 4f)
                dealDamage.Invoke(props, new object[] { total * UnityEngine.Time.deltaTime / bossTime });
            if (skipTo == null) skipTo = Environment.GetEnvironmentVariable("CUPHEAD_TEST_SKIPTO") ?? "";
            if (skipTo.Length > 0 && !skipped && nextNamedState != null && now - levelStart > 6f)
            {
                skipped = true;
                for (int i = 0; i < 12 && !Level.Won && StateName() != skipTo; i++) nextNamedState.Invoke(props, null);
                if (bench == 1) UnityEngine.Debug.Log("[cupheadport bench] skipped to state " + StateName());
            }
            if (bench == 1 && now >= camCheckAt)            // cameras switched on mid-fight (e.g. Saltbaker's phase four blur)
            {
                camCheckAt = now + 1f;
                var sig = new System.Text.StringBuilder();
                foreach (var c in UnityEngine.Camera.allCameras)
                    sig.Append(c.name).Append(c.targetTexture != null ? c.targetTexture.width : 0).Append(';');
                string sg = sig.ToString();
                if (camSig != null && sg != camSig) { UnityEngine.Debug.Log("[cupheadport bench] cameras changed (state " + StateName() + ")"); LogCameras(); }
                camSig = sg;
            }
            if (phaseHold < 0f && !float.TryParse(Environment.GetEnvironmentVariable("CUPHEAD_TEST_PHASEHOLD") ?? "", out phaseHold)) phaseHold = 0f;
            bool holding = false;
            if (phaseHold > 0f && props != null && nextTrigger != null && !Level.Won)
            {
                string st = StateName();
                if (st != holdState || !l.Started) { holdState = st; stateSince = now; }   // the clock starts with the fight (after loading and the intro)
                bool last = (float)nextTrigger.Invoke(props, null) <= 0f;
                holding = !holdReleased;
                if (holding && now - stateSince >= phaseHold)
                {
                    if (last)
                    {
                        holdReleased = true; hpMoved = true; lastHpAt = now;
                        if (bench == 1) UnityEngine.Debug.Log("[cupheadport bench] held " + st + " for " + phaseHold.ToString("F0") + " s: last state, released");
                    }
                    else
                    {
                        stepping = true;
                        nextNamedState.Invoke(props, null);
                        stepping = false;
                        if (bench == 1) UnityEngine.Debug.Log("[cupheadport bench] held " + st + " for " + phaseHold.ToString("F0") + " s -> " + StateName());
                        holdState = StateName(); stateSince = now; hpMoved = true; lastHpAt = now;
                    }
                }
            }
            if (hp != lastHp) { if (lastHp >= 0f && hp < lastHp) hpMoved = true; lastHp = hp; lastHpAt = now; }
            if (stall > 0f && !holding && hpMoved && dealDamage != null && total > 0f && hp > 0f && !Level.Won && now - levelStart > 8f && now - lastHpAt > stall)
            {
                if (bench == 1) UnityEngine.Debug.Log("[cupheadport bench] stalled at hp=" + (100f * hp / total).ToString("F0") + "%: 5% via DealDamage");
                dealDamage.Invoke(props, new object[] { total * 0.05f });
                lastHpAt = now;
            }
            var player = PlayerManager.GetPlayer(PlayerId.PlayerOne);
            if (unstick > 0f && l is PlatformingLevel && player != null && now >= stuckCheck)
            {
                var pos = player.transform.position;
                if (!float.IsNaN(stuckX) && pos.x - stuckX < unstick / 8f && !Level.Won)
                {
                    player.transform.position = new UnityEngine.Vector3(pos.x + unstick, pos.y + unstick * 4f / 3f, pos.z);
                    if (bench == 1) UnityEngine.Debug.Log("[cupheadport bench] unstick at x=" + pos.x.ToString("F1"));
                }
                stuckX = player.transform.position.x; stuckCheck = now + 2f;
            }
            if (Level.Won && wonAt < 0f) wonAt = now;
            if (bench == 1 && now >= nextLog)
            {
                nextLog = now + 5f;
                string state = !l.Started ? "Intro" :
                    props != null && pState != null ? pState.GetValue(props, null)?.GetType().GetField("stateName")?.GetValue(pState.GetValue(props, null))?.ToString() : "-";
                UnityEngine.Debug.Log("[cupheadport bench] " + l.GetType().Name + " t=" + (now - levelStart).ToString("F0") + " state=" + state +
                    " hp=" + (total > 0f ? (100f * hp / total).ToString("F0") + "%" : "-") +
                    " x=" + (player != null ? player.transform.position.x.ToString("F0") : "-") + (Level.Won ? " WON" : ""));
            }
            // playlist: time limit, or a win whose screen never came
            if (playIndex >= 0 && playIndex < playlist.Length && levelStart > 0f &&
                (now - levelStart > maxSecs || (wonAt > 0f && now - wonAt > 20f)))
                PlayNext(now - levelStart > maxSecs ? "time limit" : "won");
        }

        static int godmode = -1;
        static System.Reflection.MethodInfo setDebugInvincible;

        // CUPHEAD_TEST_GODMODE=1 (benchmarks): the player takes no damage, so a scripted run through a
        // level lasts (and the Mausoleum's urn is not lost). Sets the game's own debug flag (PlayerStatsManager.DebugInvincible) every second.
        public static void Tick()
        {
            if (detSeed == -2) Deterministic();
            NextScene();
            Bench();
            if (godmode < 0)
            {
                godmode = Environment.GetEnvironmentVariable("CUPHEAD_TEST_GODMODE") == "1" ? 1 : 0;
                if (godmode == 1)
                    setDebugInvincible = typeof(PlayerStatsManager).GetProperty("DebugInvincible").GetSetMethod(true);
            }
            // the Mausoleum is lost when a ghost reaches the urn (only parries stop them): with godmode not
            if (godmode == 1 && Level.Current is MausoleumLevel ml && ml.LoseGame != null)
            {
                ml.LoseGame = null;
                UnityEngine.Debug.Log("[cupheadport bench] godmode: the Mausoleum's urn cannot be lost");
            }
            if (godmode == 0 || UnityEngine.Time.frameCount % 60 != 0 || setDebugInvincible == null) return;
            setDebugInvincible.Invoke(null, new object[] { true });
        }
    }
}
