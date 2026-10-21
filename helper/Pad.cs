using System;
using System.Collections.Generic;
using Rewired;

namespace CupheadPort
{
    // Input on handhelds.
    //
    // Default (CUPHEAD_INPUT unset or "sdl"): Rewired reads controllers through the system's SDL2
    // (its SDL2 input source, P/Invoke "SDL2" -> box64's wrapped native libSDL2). SDL2 knows the
    // built-in pad from PortMaster's SDL_GAMECONTROLLERCONFIG, so Cuphead sees a standard gamepad:
    // controller button prompts, remapping in the Controls menu, and input that keeps working when
    // the compositor drops its input after the screen blanks. Joysticks Rewired does not recognise
    // are never given to a player.
    //
    // CUPHEAD_INPUT=keyboard: Rewired uses Unity's input, gptokeyb turns the buttons into Cuphead's
    // keyboard keys, and every joystick is kept away from the players (an unmapped joystick took
    // player one and no button did anything).
    public static class Pad
    {
        static int keyboardOnly = -1;
        static int frame;
        static string lastSeen = "";

        // Logs the joysticks Unity and Rewired see whenever that list changes (for bug reports).
        static void LogJoysticks()
        {
            var sb = new System.Text.StringBuilder();
            // (no UnityEngine.Input.GetJoystickNames(): with two joysticks it crashed the player under box64)
            sb.Append("rewired=[");
            foreach (Joystick j in ReInput.controllers.Joysticks)
            {
                sb.Append(j.name).Append(" / ").Append(j.hardwareName).Append(" guid=").Append(j.hardwareTypeGuid)
                  .Append(Usable(j) ? " usable" : " ignored").Append(" elements:");
                foreach (var e in j.ElementIdentifiers) sb.Append(' ').Append(e.id).Append('=').Append(e.name).Append(',');
                sb.Append("; ");
            }
            sb.Append("]");
            string seen = sb.ToString();
            if (seen == lastSeen) return;
            lastSeen = seen;
            UnityEngine.Debug.Log("[cupheadport] input: " + (KeyboardOnly() ? "keyboard mode, " : "pad mode, ") + seen);
        }

        // Called from Rewired's InputManager.DetectPlatform (codepatch rewired-linux-input), before
        // Rewired picks its input sources.
        public static void ConfigureRewired(Rewired.Data.ConfigVars cv)
        {
            if (KeyboardOnly())
            {
                cv.alwaysUseUnityInput = true;
            }
            else
            {
                cv.alwaysUseUnityInput = false;
                cv.linux_primaryInputSource = Rewired.Platforms.LinuxStandalonePrimaryInputSource.SDL2;
                if (Lite() && cv.platformVars_linuxStandalone != null) cv.platformVars_linuxStandalone.disableKeyboard = true;
            }
            UnityEngine.Debug.Log("[cupheadport] Rewired update loop: " + cv.updateLoop + ", keyboard " +
                (cv.platformVars_linuxStandalone != null && cv.platformVars_linuxStandalone.disableKeyboard ? "off" : "on"));
            UnityEngine.Debug.Log("[cupheadport] input source: " + (KeyboardOnly() ? "Unity input (keyboard mode)" : "SDL2"));
        }

        // The generic SDL gamepad's face buttons are called "Button 1".."Button 4" (bottom, right,
        // left, top, as SDL's a/b/x/y). Cuphead shows element names as button prompts; give them the
        // letters printed on the handheld instead (one letter -> Cuphead draws a round button glyph).
        // Anbernic's pads have the Nintendo layout (B bottom, A right, Y left, X top);
        // CUPHEAD_PROMPTS=xbox shows the Xbox letters (A bottom, B right, X left, Y top).
        // Called from CupheadInput.InputDisplayForButton (codepatch pad-prompts).
        public static string FriendlyName(string elementName)
        {
            bool xbox = Environment.GetEnvironmentVariable("CUPHEAD_PROMPTS") == "xbox";
            switch (elementName)
            {
                case "Button 1": return xbox ? "A" : "B";
                case "Button 2": return xbox ? "B" : "A";
                case "Button 3": return xbox ? "X" : "Y";
                case "Button 4": return xbox ? "Y" : "X";
                default: return elementName;
            }
        }

        // On Unity's Android player (portmaster-unity) the pad is Rewired's Xbox Wireless Controller, whose
        // face buttons are named after their Xbox letters by position (A bottom, B right, X left, Y top).
        // Shown as the letters printed on the handheld: Nintendo layout by default (B bottom, A right,
        // Y left, X top, as on Anbernic, Miyoo, Powkiddy and TrimUI pads); CUPHEAD_PROMPTS=xbox keeps the
        // Xbox letters. The buttons' actions stay where Cuphead puts them (the bottom one jumps).
        // Called from CupheadInput.InputDisplayForButton (codepatch pad-prompts-android).
        public static string Letters(string elementName)
        {
            if (Environment.GetEnvironmentVariable("CUPHEAD_PROMPTS") == "xbox") return elementName;
            switch (elementName)
            {
                case "A": return "B";
                case "B": return "A";
                case "X": return "Y";
                case "Y": return "X";
                default: return elementName;
            }
        }

        // Pad mode has no keyboard or mouse, but Rewired still read and mapped both every frame for
        // both players (Unity input calls under box64: a measurable part of each frame). In pad mode
        // they are switched off (CUPHEAD_RWLITE=0 keeps them).
        static int lite = -1;
        static bool Lite()
        {
            if (lite < 0) lite = !KeyboardOnly() && Environment.GetEnvironmentVariable("CUPHEAD_RWLITE") != "0" ? 1 : 0;
            return lite == 1;
        }

        static void DropKeyboardAndMouse()
        {
            if (!Lite()) return;
            foreach (Player p in ReInput.players.AllPlayers)
            {
                if (p.controllers.hasKeyboard) p.controllers.hasKeyboard = false;
                if (p.controllers.hasMouse) p.controllers.hasMouse = false;
            }
            if (ReInput.controllers.Keyboard != null && ReInput.controllers.Keyboard.enabled) ReInput.controllers.Keyboard.enabled = false;
            if (ReInput.controllers.Mouse != null && ReInput.controllers.Mouse.enabled) ReInput.controllers.Mouse.enabled = false;
        }

        public static bool KeyboardOnly()
        {
            if (keyboardOnly < 0)
            {
                string v = Environment.GetEnvironmentVariable("CUPHEAD_INPUT");
                keyboardOnly = v != null && v.Trim() == "keyboard" ? 1 : 0;
            }
            return keyboardOnly == 1;
        }

        public static bool Usable(Joystick joystick)
        {
            return joystick != null && !KeyboardOnly() && joystick.hardwareTypeGuid != Guid.Empty;
        }

        // Replaces CupheadInput.CheckForUnconnectedControllerPress: the same, for usable joysticks only.
        public static Joystick CheckForUnconnectedControllerPress()
        {
            if (KeyboardOnly() || !ReInput.isReady) return null;
            foreach (Joystick joystick in ReInput.controllers.Joysticks)
            {
                if (!Usable(joystick) || ReInput.controllers.IsJoystickAssigned(joystick) || !joystick.GetAnyButtonDown()) continue;
                return joystick;
            }
            return null;
        }

        // Replaces CupheadInput.CheckForControllerPress.
        public static Joystick CheckForControllerPress(long systemID)
        {
            if (KeyboardOnly() || !ReInput.isReady) return null;
            foreach (Joystick joystick in ReInput.controllers.Joysticks)
            {
                if (!Usable(joystick)) continue;
                long? systemId = joystick.systemId;
                if (systemId.HasValue && systemId.Value == systemID && joystick.GetAnyButtonDown()) return joystick;
            }
            return null;
        }

        // The game's own rumble is off unless CUPHEAD_RUMBLE=game: on the RGSP the motor kept running
        // after the game's stop call (Rewired's SDL2 vibration), so taking damage left the device
        // buzzing; Rumble.cs plays a self-ending pulse instead. The game's Rumble setting is switched
        // off (checked every few seconds, so a settings reload or the Controls menu toggle does not
        // turn it back on) and any running vibration is stopped.
        static void EnforceRumble()
        {
            if (Rumble.GameRumble()) return;
            SettingsData data = SettingsData.Data;
            if (data == null || !data.canVibrate) return;
            data.canVibrate = false;
            foreach (Joystick j in ReInput.controllers.Joysticks)
                if (j.supportsVibration) j.StopVibration();
            UnityEngine.Debug.Log("[cupheadport] the game's rumble is off (the port's pulse rumble replaces it)");
        }

        // Called every frame (from Diagnostics.Tick): take unusable joysticks away from the players
        // (Rewired assigns new joysticks by itself).
        static int rwJoy = -1;

        public static void Tick()
        {
            if (!ReInput.isReady || frame++ % 15 != 0) return;
            if (frame % 150 == 1) { LogJoysticks(); EnforceRumble(); DropKeyboardAndMouse(); }
            if (KeyboardOnly() && ReInput.configuration.autoAssignJoysticks) ReInput.configuration.autoAssignJoysticks = false;
            // Joysticks the port never reads (the handheld's own pad, which Rewired cannot map) still had
            // every button and axis updated in each of Rewired's loops (Update and each FixedUpdate):
            // disabled, Rewired skips them. CUPHEAD_RWJOY=0 leaves them enabled.
            if (rwJoy < 0) rwJoy = Environment.GetEnvironmentVariable("CUPHEAD_RWJOY") == "0" ? 0 : 1;
            if (rwJoy == 1)
                foreach (Joystick j in ReInput.controllers.Joysticks)
                    if (j.enabled && !Usable(j)) j.enabled = false;
            foreach (Player p in ReInput.players.AllPlayers)
            {
                if (p.controllers.joystickCount == 0) continue;
                var remove = new List<Joystick>();
                foreach (Joystick j in p.controllers.Joysticks)
                    if (!Usable(j)) remove.Add(j);
                foreach (Joystick j in remove) p.controllers.RemoveController(j);
            }
        }
    }
}
