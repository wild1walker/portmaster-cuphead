using System;
using System.Runtime.InteropServices;

namespace CupheadPort
{
    // Rumble on damage, as a pulse the device stops by itself.
    //
    // Cuphead rumbles only when a player takes a hit: Vibrator.Vibrate(1, 0.2 s), through Rewired's
    // SDL2 haptics, followed 0.2 s later by a stop. On the RGSP the stop never took effect and the
    // motor ran until the game quit. So the game's own rumble stays off (Pad switches its Rumble
    // setting off), and this plays the same rumble as one SDL haptic effect with a length of 200 ms:
    // the kernel ends it after that time, no stop needed.
    //
    // CUPHEAD_RUMBLE=0: no rumble. CUPHEAD_RUMBLE=game: the game's own rumble (may not stop).
    public static class Rumble
    {
        const uint SDL_INIT_HAPTIC = 0x1000;

        [DllImport("SDL2")] static extern int SDL_InitSubSystem(uint flags);
        [DllImport("SDL2")] static extern int SDL_NumHaptics();
        [DllImport("SDL2")] static extern IntPtr SDL_HapticName(int index);
        [DllImport("SDL2")] static extern IntPtr SDL_HapticOpen(int index);
        [DllImport("SDL2")] static extern int SDL_HapticRumbleSupported(IntPtr haptic);
        [DllImport("SDL2")] static extern int SDL_HapticRumbleInit(IntPtr haptic);
        [DllImport("SDL2")] static extern int SDL_HapticRumblePlay(IntPtr haptic, float strength, uint lengthMs);
        [DllImport("SDL2")] static extern IntPtr SDL_GetError();

        static int mode = -1;     // 0 off, 1 pulse, 2 the game's own
        static int state;         // 0 not set up, 1 ready, -1 no device
        static IntPtr haptic;
        static UnityEngine.Object lastPlayer;
        static int lastHealth = -1;

        public static bool GameRumble()
        {
            if (mode < 0)
            {
                string v = Environment.GetEnvironmentVariable("CUPHEAD_RUMBLE");
                mode = v == "0" ? 0 : v == "game" || v == "1" ? 2 : 1;
            }
            return mode == 2;
        }

        static string Str(IntPtr p)
        {
            return p == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(p);
        }

        static bool Init()
        {
            try
            {
                SDL_InitSubSystem(SDL_INIT_HAPTIC);
                int n = SDL_NumHaptics();
                for (int i = 0; i < n; i++)
                {
                    IntPtr h = SDL_HapticOpen(i);
                    if (h == IntPtr.Zero) continue;
                    if (SDL_HapticRumbleSupported(h) == 1 && SDL_HapticRumbleInit(h) == 0)
                    {
                        haptic = h;
                        UnityEngine.Debug.Log("[cupheadport] rumble: " + Str(SDL_HapticName(i)) + " (200 ms pulse on damage)");
                        return true;
                    }
                }
                UnityEngine.Debug.Log("[cupheadport] rumble: no device that can rumble (" + n + " haptic devices; " + Str(SDL_GetError()) + ")");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log("[cupheadport] rumble unavailable: " + e.Message);
            }
            return false;
        }

        static void Pulse()
        {
            if (state == 0) state = Init() ? 1 : -1;
            if (state == 1) SDL_HapticRumblePlay(haptic, 1f, 200);
        }

        // Called every frame (Diagnostics.Tick): player one's health going down is a hit.
        public static void Tick()
        {
            if (GameRumble() || mode == 0 || state < 0) return;
            if (Level.Current == null) { lastPlayer = null; return; }   // players exist in levels only
            AbstractPlayerController p;
            try { p = PlayerManager.GetPlayer(PlayerId.PlayerOne); }
            catch (Exception) { return; }
            if (p == null) { lastPlayer = null; return; }
            PlayerStatsManager stats = p.stats;
            if (stats == null) return;
            int health = stats.Health;
            if (!ReferenceEquals(p, lastPlayer)) { lastPlayer = p; lastHealth = health; return; }
            if (health < lastHealth) Pulse();
            lastHealth = health;
        }
    }
}
