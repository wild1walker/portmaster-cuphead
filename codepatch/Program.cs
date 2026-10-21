// Code patches for Cuphead's managed assemblies (Mac build 1.3.4 running on the Linux player, or
// with --target=android on Unity's Android player through portmaster-unity).
// Usage: codepatch [--target=linux|android] [--test-hooks|--test=a,b] <Managed dir in> <out dir>
// Writes patched copies of the assemblies it changes into <out dir>; the originals are not touched.
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

static class Program
{
    static int Main(string[] args)
    {
        bool testHooks = args.Contains("--test-hooks");
        string target = args.FirstOrDefault(a => a.StartsWith("--target="))?.Substring(9) ?? "linux";
        if (target != "linux" && target != "android") { Console.Error.WriteLine("--target: linux or android"); return 2; }
        // --test=<name>[,<name>]: only these test patches (with the port's own)
        string[] only = args.FirstOrDefault(a => a.StartsWith("--test="))?.Substring(7).Split(',');
        if (only != null) testHooks = true;
        // --skip=<name>[,<name>]: leave these patches out (A/B tests of one patch)
        string[] skip = args.FirstOrDefault(a => a.StartsWith("--skip="))?.Substring(7).Split(',') ?? Array.Empty<string>();
        args = args.Where(a => !a.StartsWith("--")).ToArray();
        if (args.Length != 2) { Console.Error.WriteLine("usage: codepatch [--test-hooks] <Managed dir> <out dir>"); return 2; }
        string inDir = args[0], outDir = args[1];
        Directory.CreateDirectory(outDir);
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(inDir);
        var rp = new ReaderParameters { AssemblyResolver = resolver, ReadingMode = ReadingMode.Immediate };

        var asm = AssemblyDefinition.ReadAssembly(Path.Combine(inDir, "Assembly-CSharp.dll"), rp);
        var m = asm.MainModule;
        int failures = 0;
        var set = target == "android" ? Patches.Android : Patches.All;
        foreach (var (name, patch) in set.Concat(testHooks ? Patches.TestOnly.Where(p => only == null || only.Contains(p.Item1)) : Array.Empty<(string, Action<ModuleDefinition>)>()))
        {
            if (skip.Contains(name)) { Console.WriteLine($"skip {name}"); continue; }
            try { patch(m); Console.WriteLine($"ok   {name}"); }
            catch (Exception e) { failures++; Console.WriteLine($"FAIL {name}: {e.Message}"); }
        }
        // Never reference the patcher's own runtime (System.Private.CoreLib / System.Runtime): the game runs on Mono 2.0
        var bad = m.AssemblyReferences.Where(a => a.Name.StartsWith("System.Private") || a.Name == "System.Runtime" || a.Name == "netstandard").ToList();
        if (bad.Count > 0) { Console.WriteLine("FAIL foreign assembly references: " + string.Join(", ", bad.Select(a => a.FullName))); return 1; }
        asm.Write(Path.Combine(outDir, "Assembly-CSharp.dll"));
        return failures == 0 ? 0 : 1;
    }
}

static class Patches
{
    public static readonly (string, Action<ModuleDefinition>)[] All =
    {
        ("rewired-linux-input", RewiredLinux),
        ("gog-offline", GogOffline),
        ("dlc-from-gog-install", DlcFromGogInstall),
        ("fonts-lazy", FontsLazy),
        ("diagnostics", DiagnosticsTick),
        ("pad-keyboard-only", PadKeyboardOnly),
        ("aspect-crop", AspectCrop),
        ("pad-prompts", PadPrompts),
        ("bench-level-hook", TestLevelHook),
        ("glow-camera", GlowCamera),
        ("fx-blurgamma", FxBlurGamma),
    };

    // Unity's Android player: Rewired on its Android platform (Unity input; Android-layout gamepads
    // map as generic gamepads), the game's own BlurGamma (the port's replacement targets the Linux
    // player's GL path), no gptokeyb keyboard mode or SDL button-name prompts.
    public static readonly (string, Action<ModuleDefinition>)[] Android =
    {
        ("rewired-android-input", RewiredAndroid),
        ("pad-prompts-android", PadPromptsAndroid),
        ("gog-offline", GogOffline),
        ("dlc-from-gog-install", DlcFromGogInstall),
        ("fonts-lazy", FontsLazy),
        ("diagnostics", DiagnosticsTick),
        ("aspect-crop", AspectCrop),
        ("bench-level-hook", TestLevelHook),
        ("glow-camera", GlowCamera),
        ("bluroptimized-idle", BlurOptimizedIdle),
        ("geese-track", GeeseTrack),
        ("atlas-trim-reload", AtlasTrimReload),
    };

    // SceneLoader.ReloadLevel and LevelGameOverGUI.In (the death card) start with
    // CupheadPort.AtlasTrim.BeforeReload(): boss-phase atlases the helper unloaded during the fight are
    // loaded again before a retry (converter/helper AtlasTrim.cs).
    static void AtlasTrimReload(ModuleDefinition m)
    {
        foreach (var md in new[] { Method(Type(m, "SceneLoader"), "ReloadLevel"), Method(Type(m, "LevelGameOverGUI"), "In") })
        {
            var il = md.Body.GetILProcessor();
            il.InsertBefore(md.Body.Instructions[0], il.Create(OpCodes.Call, HelperMethod(m, "AtlasTrim", "BeforeReload", m.TypeSystem.Void)));
        }
    }

    // BlurOptimized.OnRenderImage starts with "if (CupheadPort.BlurIdle.Skip(this, source,
    // destination)) return;": Glumstone the Giant's geese camera copies its (empty) image instead of
    // blurring it while no goose is in view (converter/helper BlurIdle.cs).
    static void BlurOptimizedIdle(ModuleDefinition m)
    {
        var t = Type(m, "UnityStandardAssets.ImageEffects.BlurOptimized");
        var md = Method(t, "OnRenderImage");
        var rt = md.Parameters[0].ParameterType;
        var mono = Type(m, "UnityStandardAssets.ImageEffects.PostEffectsBase").BaseType;   // UnityEngine.MonoBehaviour
        var il = md.Body.GetILProcessor();
        var first = md.Body.Instructions[0];
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_2));
        il.InsertBefore(first, il.Create(OpCodes.Call, HelperMethod(m, "BlurIdle", "Skip", m.TypeSystem.Boolean, mono, rt, rt)));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse, first));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
    }

    // OldManLevelGoose.Init starts with CupheadPort.BlurIdle.Track(this): the geese BlurIdle watches.
    static void GeeseTrack(ModuleDefinition m)
    {
        var mono = Type(m, "UnityStandardAssets.ImageEffects.PostEffectsBase").BaseType;   // UnityEngine.MonoBehaviour
        var md = Method(Type(m, "OldManLevelGoose"), "Init");
        var il = md.Body.GetILProcessor();
        var first = md.Body.Instructions[0];
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Call, HelperMethod(m, "BlurIdle", "Track", m.TypeSystem.Void, mono)));
    }

    // The Mac build compiled Rewired's InputManager.DetectPlatform with platform = OSX; on the
    // Android player it must say Android (Rewired then reads pads through Unity input, with
    // android_supportUnknownGamepads on by default).
    static void RewiredAndroid(ModuleDefinition m)
    {
        var im = Type(m, "Rewired.InputManager");
        var detect = Method(im, "DetectPlatform");
        var baseT = im.BaseType.Resolve();
        var body = detect.Body;
        body.Instructions.Clear(); body.Variables.Clear(); body.ExceptionHandlers.Clear();
        var il = body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Stfld, m.ImportReference(Field(baseT, "editorPlatform")));
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_7); il.Emit(OpCodes.Stfld, m.ImportReference(Field(baseT, "platform")));   // Platform.Android
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Stfld, m.ImportReference(Field(baseT, "webplayerPlatform")));
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Stfld, m.ImportReference(Field(baseT, "isEditor")));
        il.Emit(OpCodes.Ret);
    }

    // BlurGamma.OnRenderImage is drawn by CupheadPort.Effects (same passes by default; CUPHEAD_FX
    // picks cheaper equivalents, CUPHEAD_FXTIME=1 times the steps).
    static void FxBlurGamma(ModuleDefinition m)
    {
        var t = Type(m, "BlurGamma");
        var md = Method(t, "OnRenderImage");
        var rt = md.Parameters[0].ParameterType;
        var b = md.Body; b.Instructions.Clear(); b.Variables.Clear(); b.ExceptionHandlers.Clear();
        var il = b.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, HelperMethod(m, "Effects", "RenderBlurGamma", m.TypeSystem.Void, t, rt, rt));
        il.Emit(OpCodes.Ret);
    }

    // GlowText's camera (CameraGlow) draws the UI layer into the glow's texture every frame, though
    // the texture is shown only between BeginGlow and StopGlow; CupheadPort.Glow switches the camera
    // on and off with the glow (and off after a scene load, until a glow begins).
    static void GlowCamera(ModuleDefinition m)
    {
        var t = Type(m, "GlowText");
        var cam = Field(t, "cameraGlow");
        foreach (var (method, helper) in new[] { ("BeginGlow", "Begin"), ("StopGlow", "Stop") })
        {
            var md = Method(t, method);
            var il = md.Body.GetILProcessor();
            var first = md.Body.Instructions[0];
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(first, il.Create(OpCodes.Ldfld, cam));
            il.InsertBefore(first, il.Create(OpCodes.Call, HelperMethod(m, "Glow", helper, m.TypeSystem.Void, cam.FieldType)));
        }
    }

    // Button prompts are Rewired element names; CupheadPort.Pad.FriendlyName turns the generic SDL
    // gamepad's "Button 1".."Button 4" into A/B/X/Y.
    static void PadPrompts(ModuleDefinition m)
    {
        var md = Method(Type(m, "CupheadInput"), "InputDisplayForButton");
        var get = md.Body.Instructions.First(i => i.Operand is MethodReference r && r.Name == "get_elementIdentifierName");
        var il = md.Body.GetILProcessor();
        il.InsertAfter(get, il.Create(OpCodes.Call, HelperMethod(m, "Pad", "FriendlyName", m.TypeSystem.String, m.TypeSystem.String)));
    }

    // On the Android player the prompts are the Xbox pad's letters by position; CupheadPort.Pad.Letters
    // shows the handheld's own letters (Nintendo layout unless CUPHEAD_PROMPTS=xbox).
    static void PadPromptsAndroid(ModuleDefinition m)
    {
        var md = Method(Type(m, "CupheadInput"), "InputDisplayForButton");
        var get = md.Body.Instructions.First(i => i.Operand is MethodReference r && r.Name == "get_elementIdentifierName");
        var il = md.Body.GetILProcessor();
        il.InsertAfter(get, il.Create(OpCodes.Call, HelperMethod(m, "Pad", "Letters", m.TypeSystem.String, m.TypeSystem.String)));
    }

    // AbstractCupheadCamera.UpdateRect letterboxes to 16:9; CupheadPort.Aspect fills a narrower
    // screen and crops the sides instead (default), unless CUPHEAD_ASPECT=16:9.
    static void AspectCrop(ModuleDefinition m)
    {
        var t = Type(m, "AbstractCupheadCamera");
        var md = Method(t, "UpdateRect");
        var ins = md.Body.Instructions;
        // how the method itself reads its camera: "ldarg.0; call get_camera" or "ldarg.0; ldfld camera"
        var getCam = ins.First(i => i.Previous != null && i.Previous.OpCode == OpCodes.Ldarg_0 &&
            ((i.Operand is MethodReference r && r.ReturnType.FullName == "UnityEngine.Camera") ||
             (i.Operand is FieldReference f && f.FieldType.FullName == "UnityEngine.Camera")));
        var cameraType = getCam.Operand is MethodReference mr ? mr.ReturnType : ((FieldReference)getCam.Operand).FieldType;
        var helper = HelperMethod(m, "Aspect", "UpdateRect", m.TypeSystem.Boolean, cameraType);
        var il = md.Body.GetILProcessor();
        var first = ins[0];
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(getCam.OpCode, (dynamic)getCam.Operand));
        il.InsertBefore(first, il.Create(OpCodes.Call, helper));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse, first));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
    }

    // Cuphead hands player one to whichever joystick was pressed (CupheadInput.CheckFor...Press).
    // On handhelds the built-in pad is a joystick Rewired cannot map; CupheadPort.Pad
    // (converter/helper) only considers joysticks Rewired recognises (gptokeyb's virtual Xbox 360
    // pad), or none with CUPHEAD_INPUT=keyboard. Both methods now just call the helper.
    static void PadKeyboardOnly(ModuleDefinition m)
    {
        var t = Type(m, "CupheadInput");
        foreach (var name in new[] { "CheckForUnconnectedControllerPress", "CheckForControllerPress" })
        {
            var md = Method(t, name);
            var helper = HelperMethod(m, "Pad", name, md.ReturnType, md.Parameters.Select(p => p.ParameterType).ToArray());
            var b = md.Body; b.Instructions.Clear(); b.Variables.Clear(); b.ExceptionHandlers.Clear();
            var il = b.GetILProcessor();
            foreach (var p in md.Parameters) il.Emit(OpCodes.Ldarg, p);
            il.Emit(OpCodes.Call, helper);
            il.Emit(OpCodes.Ret);
        }
    }

    // CupheadPort.Diagnostics.Tick() once per frame from Cuphead.Update (does nothing unless
    // CUPHEAD_MEMLOG=1): memory figures in the player log for tuning on the device.
    static void DiagnosticsTick(ModuleDefinition m)
    {
        var upd = Method(Type(m, "Cuphead"), "Update");
        var il = upd.Body.GetILProcessor();
        il.InsertBefore(upd.Body.Instructions[0], il.Create(OpCodes.Call, HelperMethod(m, "Diagnostics", "Tick", m.TypeSystem.Void)));
    }

    static AssemblyNameReference HelperAssembly(ModuleDefinition m)
    {
        var r = m.AssemblyReferences.FirstOrDefault(a => a.Name == "CupheadPort");
        if (r == null) { r = new AssemblyNameReference("CupheadPort", new Version(0, 0, 0, 0)); m.AssemblyReferences.Add(r); }
        return r;
    }

    static MethodReference HelperMethod(ModuleDefinition m, string type, string name, TypeReference ret, params TypeReference[] args)
    {
        var mr = new MethodReference(name, ret, new TypeReference("CupheadPort", type, m, HelperAssembly(m))) { HasThis = false };
        foreach (var a in args) mr.Parameters.Add(new ParameterDefinition(a));
        return m.ImportReference(mr);
    }

    // FontLoader loads all 50 font bundles at start-up (~130 MB of CJK fonts); CupheadPort.Fonts
    // (converter/helper) loads Cuphead's own fonts first and the rest on the first lookup miss.
    static void FontsLazy(ModuleDefinition m)
    {
        var fl = Type(m, "FontLoader");
        var fontType = fl.NestedTypes.Single(t => t.Name == "FontType");
        var tmpType = fl.NestedTypes.Single(t => t.Name == "TMPFontType");
        var init = Method(fl, "Initialize");
        var b = init.Body; b.Instructions.Clear(); b.Variables.Clear(); b.ExceptionHandlers.Clear();
        var il = b.GetILProcessor();
        il.Emit(OpCodes.Call, HelperMethod(m, "Fonts", "Initialize", init.ReturnType));
        il.Emit(OpCodes.Ret);
        void Before(string method, string helper, TypeReference arg)
        {
            var md = Method(fl, method);
            var p = md.Body.GetILProcessor();
            var first = md.Body.Instructions[0];
            p.InsertBefore(first, p.Create(OpCodes.Ldarg_0));
            p.InsertBefore(first, p.Create(OpCodes.Call, HelperMethod(m, "Fonts", helper, m.TypeSystem.Void, arg)));
        }
        Before("GetFont", "BeforeGetFont", fontType);
        Before("GetTMPFont", "BeforeGetTMPFont", tmpType);
        Before("GetTMPMaterial", "BeforeGetTMPMaterial", m.TypeSystem.String);
    }

    // Development only (codepatch --test-hooks), never part of the port.
    public static readonly (string, Action<ModuleDefinition>)[] TestOnly =
    {
        ("test-memlog", TestMemLog),
        ("test-trigger-pairs", TestTriggerPairs),
        ("test-find-sites", TestFindSites),
        ("test-time-updates", TestTimeUpdates),
    };

    // Times the game's per-frame work (CupheadPort.UpdTime, logged every 10 s): Update, FixedUpdate,
    // LateUpdate and the 2D physics callbacks of every MonoBehaviour, and every coroutine step
    // (MoveNext of the iterators those classes declare). Each method gets T0 at the start and T1
    // (this, "Type.Method", t0) before every return.
    static void TestTimeUpdates(ModuleDefinition m)
    {
        var long_ = m.TypeSystem.Int64;
        var t0 = HelperMethod(m, "UpdTime", "T0", long_);
        var t1 = HelperMethod(m, "UpdTime", "T1", m.TypeSystem.Void, m.TypeSystem.Object, m.TypeSystem.String, long_);
        string[] perFrame = { "Update", "FixedUpdate", "LateUpdate" };
        string[] physics = { "OnTriggerEnter2D", "OnTriggerStay2D", "OnTriggerExit2D", "OnCollisionEnter2D", "OnCollisionStay2D", "OnCollisionExit2D" };
        bool IsBehaviour(TypeDefinition t)
        {
            for (TypeReference b = t.BaseType; b != null; )
            {
                if (b.FullName == "UnityEngine.MonoBehaviour") return true;
                TypeDefinition d; try { d = b.Resolve(); } catch { return false; }
                if (d == null) return false;
                b = d.BaseType;
            }
            return false;
        }
        string[] inside = { "LevelPlayerMotor", "PlatformingLevelGroundMovementEnemy" };
        var sync = HelperMethod(m, "UpdTime", "Sync", m.TypeSystem.Void, m.TypeSystem.String);
        int n = 0, skipped = 0;
        foreach (var t in m.GetTypes().ToList())
        {
            if (t.HasGenericParameters || t.DeclaringType != null && t.DeclaringType.HasGenericParameters) continue;
            var targets = new List<(MethodDefinition, string)>();
            if (IsBehaviour(t))
                foreach (var md in t.Methods.Where(x => x.HasBody && !x.IsStatic &&
                         (perFrame.Contains(x.Name) && x.Parameters.Count == 0 || physics.Contains(x.Name) && x.Parameters.Count == 1)))
                    targets.Add((md, t.FullName + "." + md.Name));
            if (inside.Contains(t.FullName))                 // every method of the motors, to split their time
                foreach (var md in t.Methods.Where(x => x.HasBody && !x.IsConstructor && !targets.Any(y => y.Item1 == x)))
                    targets.Add((md, t.FullName + "." + md.Name));
            if (t.DeclaringType != null && IsBehaviour(t.DeclaringType) && t.Interfaces.Any(i => i.InterfaceType.FullName == "System.Collections.IEnumerator"))
            {
                var mn = t.Methods.FirstOrDefault(x => x.Name == "MoveNext" && x.HasBody && x.Parameters.Count == 0);
                if (mn != null) targets.Add((mn, t.DeclaringType.FullName + "/" + t.Name));
            }
            foreach (var (md, key) in targets)
            {
                var body = md.Body;
                if (body.ExceptionHandlers.Any(h => h.HandlerEnd == null || h.TryEnd == null)) { skipped++; continue; }
                body.SimplifyMacros();
                var il = body.GetILProcessor();
                var vt = new VariableDefinition(long_); body.Variables.Add(vt); body.InitLocals = true;
                VariableDefinition rv = null;
                if (md.ReturnType.MetadataType != MetadataType.Void) { rv = new VariableDefinition(md.ReturnType); body.Variables.Add(rv); }
                var epi = il.Create(md.IsStatic ? OpCodes.Ldnull : OpCodes.Ldarg_0);
                foreach (var ins in body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToList())
                {
                    if (rv == null) { ins.OpCode = OpCodes.Br; ins.Operand = epi; }
                    else { ins.OpCode = OpCodes.Stloc; ins.Operand = rv; il.InsertAfter(ins, il.Create(OpCodes.Br, epi)); }
                }
                il.Append(epi);
                il.Append(il.Create(OpCodes.Ldstr, key));
                il.Append(il.Create(OpCodes.Ldloc, vt));
                il.Append(il.Create(OpCodes.Call, t1));
                if (rv != null) il.Append(il.Create(OpCodes.Ldloc, rv));
                il.Append(il.Create(OpCodes.Ret));
                var first = body.Instructions[0];
                il.InsertBefore(first, il.Create(OpCodes.Call, t0));
                il.InsertBefore(first, il.Create(OpCodes.Stloc, vt));
                if (md.Name == "HandleRaycasts" && inside.Contains(t.FullName))
                {
                    il.InsertBefore(first, il.Create(OpCodes.Ldstr, t.FullName + ".HandleRaycasts: transform sync before the casts"));
                    il.InsertBefore(first, il.Create(OpCodes.Call, sync));
                }
                body.OptimizeMacros();
                n++;
            }
        }
        Console.WriteLine($"     test-time-updates: {n} methods timed, {skipped} skipped (handler to the end of the method)");
    }

    // Counts the calls of each UnityEngine.Object.FindObject(s)OfType call site (CupheadPort.Diag.Site:
    // the site's "Type.Method" in the player log every 20 s), to find what searches all objects every frame.
    static void TestFindSites(ModuleDefinition m)
    {
        var site = HelperMethod(m, "Diag", "Site", m.TypeSystem.Void, m.TypeSystem.String);
        foreach (var t in m.GetTypes())
            foreach (var md in t.Methods.Where(x => x.HasBody))
            {
                var calls = md.Body.Instructions.Where(i => (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) && i.Operand is MethodReference r &&
                    r.DeclaringType.FullName == "UnityEngine.Object" && r.Name.StartsWith("FindObject")).ToList();
                if (calls.Count == 0) continue;
                var il = md.Body.GetILProcessor();
                foreach (var c in calls)
                {
                    il.InsertBefore(c, il.Create(OpCodes.Ldstr, t.FullName + "." + md.Name));
                    il.InsertBefore(c, il.Create(OpCodes.Call, site));
                }
            }
    }

    // Counts AbstractCollidableObject's trigger/collision messages by (receiver, other collider)
    // (CupheadPort.Diag.Pair, logged every 20 s): which contacts a physics change keeps or loses.
    static void TestTriggerPairs(ModuleDefinition m)
    {
        var t = Type(m, "AbstractCollidableObject");
        var md = Method(t, "checkCollision");
        var il = md.Body.GetILProcessor();
        var first = md.Body.Instructions[0];
        var colT = md.Parameters[0].ParameterType;
        var phaseT = md.Parameters[1].ParameterType;
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_2));
        il.InsertBefore(first, il.Create(OpCodes.Box, phaseT));
        il.InsertBefore(first, il.Create(OpCodes.Call, HelperMethod(m, "Diag", "Pair", m.TypeSystem.Void, m.TypeSystem.Object, colT, m.TypeSystem.Object)));
    }

    // Calls CupheadPortDebug.MemLog.Tick() (test/memlog, copied next to Assembly-CSharp.dll) from
    // Cuphead.Update: Unity and Mono memory counters in the player log every 5 s.
    static void TestMemLog(ModuleDefinition m)
    {
        var asmRef = new AssemblyNameReference("CupheadPortDebug", new Version(0, 0, 0, 0));
        m.AssemblyReferences.Add(asmRef);
        var tick = new MethodReference("Tick", m.TypeSystem.Void, new TypeReference("CupheadPortDebug", "MemLog", m, asmRef)) { HasThis = false };
        var upd = Method(Type(m, "Cuphead"), "Update");
        var il = upd.Body.GetILProcessor();
        il.InsertBefore(upd.Body.Instructions[0], il.Create(OpCodes.Call, m.ImportReference(tick)));
    }

    // CUPHEAD_TEST_LEVEL=<Levels name>: choosing a save slot starts that level instead of the map,
    // so the heaviest boss fights can be measured without playing up to them (the device benchmark,
    // test/devbench). Part of every build: without the variable, the game's own choice is kept.
    static void TestLevelHook(ModuleDefinition m)
    {
        var t = Type(m, "SlotSelectScreen");
        var enter = Method(t, "EnterGame");
        var il = enter.Body.GetILProcessor();
        var ins = enter.Body.Instructions;
        var reset = ins.First(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference r && r.Name == "ResetPreviousLevelInfo");
        var inGame = ins.First(i => i.OpCode == OpCodes.Stsfld && i.Operand is FieldReference f && f.Name == "inGame");
        var inGameStart = inGame.Previous;                 // ldc.i4.1
        var tryEnter = HelperMethod(m, "TestHooks", "TryEnter", m.TypeSystem.Boolean);
        var after = reset.Next;
        var seq = new[] {
            il.Create(OpCodes.Call, tryEnter), il.Create(OpCodes.Brfalse, after), il.Create(OpCodes.Br, inGameStart),
        };
        foreach (var x in seq.Reverse()) il.InsertAfter(reset, x);
    }

    internal static TypeDefinition Type(ModuleDefinition m, string fullName) =>
        m.GetType(fullName) ?? throw new Exception($"type {fullName} not found");

    internal static MethodDefinition Method(TypeDefinition t, string name) =>
        t.Methods.SingleOrDefault(x => x.Name == name) ?? throw new Exception($"method {t.FullName}::{name} not found");

    internal static FieldDefinition Field(TypeDefinition t, string name) =>
        t.Fields.SingleOrDefault(x => x.Name == name) ?? throw new Exception($"field {t.FullName}::{name} not found");

    static bool TouchesGalaxy(MethodDefinition md) =>
        md.HasBody && md.Body.Instructions.Any(i => i.Operand is MemberReference r &&
            ((r.DeclaringType?.Namespace ?? "").StartsWith("Galaxy") || r.DeclaringType?.Name == "GogGalaxyManager" ||
             (r is TypeReference tr && tr.Namespace.StartsWith("Galaxy"))));

    // Replaces a method body with "return default" (0 / null / false / nothing).
    internal static void StubOut(MethodDefinition md)
    {
        var body = md.Body;
        body.Instructions.Clear(); body.Variables.Clear(); body.ExceptionHandlers.Clear();
        var il = body.GetILProcessor();
        var rt = md.ReturnType;
        if (rt.MetadataType == MetadataType.Void) { }
        else if (rt.IsValueType)
        {
            var v = new VariableDefinition(rt); body.Variables.Add(v); body.InitLocals = true;
            il.Emit(OpCodes.Ldloca_S, v); il.Emit(OpCodes.Initobj, rt); il.Emit(OpCodes.Ldloc, v);
        }
        else il.Emit(OpCodes.Ldnull);
        foreach (var p in md.Parameters.Where(p => p.IsOut))
        { il.Emit(OpCodes.Ldarg, p); il.Emit(OpCodes.Initobj, ((ByReferenceType)p.ParameterType).ElementType); }
        il.Emit(OpCodes.Ret);
    }

    // OnlineInterfaceGog derives from Galaxy's SWIG IAuthListener, whose constructor needs the native
    // GalaxyCSharpGlue (macOS/Windows only). Rebase it on System.Object, drop the Galaxy start-up and
    // every achievement/stat call, and keep the local save files (SavePath is empty on Linux: use
    // Application.persistentDataPath, which the launcher points into the port folder).
    static void GogOffline(ModuleDefinition m)
    {
        var t = Type(m, "OnlineInterfaceGog");
        var objCtor = m.ImportReference(new MethodReference(".ctor", m.TypeSystem.Void, m.TypeSystem.Object) { HasThis = true });
        t.BaseType = m.TypeSystem.Object;
        foreach (var ctor in t.Methods.Where(x => x.IsConstructor && !x.IsStatic))
            foreach (var ins in ctor.Body.Instructions)
                if (ins.OpCode == OpCodes.Call && ins.Operand is MethodReference mr && mr.Name == ".ctor" &&
                    mr.DeclaringType.Namespace.StartsWith("Galaxy"))
                    ins.Operand = objCtor;
        foreach (var md in t.Methods)
        {
            if (md.IsVirtual && !md.IsNewSlot && md.Name.StartsWith("OnAuth")) md.IsNewSlot = true;
            if (md.Name == "Init" || (!md.IsConstructor && TouchesGalaxy(md))) StubOut(md);
        }
        var savePath = t.Properties.Single(p => p.Name == "SavePath").GetMethod;
        var getPdp = m.ImportReference(
            new MethodReference("get_persistentDataPath", m.TypeSystem.String,
                new TypeReference("UnityEngine", "Application", m, UnityCore(m))) { HasThis = false });
        var combine = m.ImportReference(new MethodReference("Combine", m.TypeSystem.String,
            new TypeReference("System.IO", "Path", m, m.TypeSystem.CoreLibrary)) { HasThis = false,
            Parameters = { new ParameterDefinition(m.TypeSystem.String), new ParameterDefinition(m.TypeSystem.String) } });
        var b = savePath.Body; b.Instructions.Clear(); b.Variables.Clear(); b.ExceptionHandlers.Clear();
        var il = b.GetILProcessor();
        il.Emit(OpCodes.Call, getPdp); il.Emit(OpCodes.Ldstr, "saves/"); il.Emit(OpCodes.Call, combine); il.Emit(OpCodes.Ret);
        // fields of Galaxy types (none expected besides gogManager, which stays null)
    }

    internal static IMetadataScope UnityCore(ModuleDefinition m) =>
        m.AssemblyReferences.First(a => a.Name == "UnityEngine.CoreModule");

    // DLCManager asks Galaxy whether The Delicious Last Course is installed. GOG's offline installers
    // leave goggame-<DLC product id>.info next to the game when the DLC is installed; the port's setup
    // copies the .info files beside the player (beside the APK/OBB on Android), so check for that.
    static void DlcFromGogInstall(ModuleDefinition m)
    {
        var t = Type(m, "DLCManager");
        var refresh = Method(t, "refreshDLC");
        var fAvail = Field(t, "dlcAvailable");
        var getDataPath = m.ImportReference(new MethodReference("get_dataPath", m.TypeSystem.String,
            new TypeReference("UnityEngine", "Application", m, UnityCore(m))) { HasThis = false });
        var combine = m.ImportReference(new MethodReference("Combine", m.TypeSystem.String,
            new TypeReference("System.IO", "Path", m, m.TypeSystem.CoreLibrary)) { HasThis = false,
            Parameters = { new ParameterDefinition(m.TypeSystem.String), new ParameterDefinition(m.TypeSystem.String) } });
        var dirName = m.ImportReference(new MethodReference("GetDirectoryName", m.TypeSystem.String,
            new TypeReference("System.IO", "Path", m, m.TypeSystem.CoreLibrary)) { HasThis = false,
            Parameters = { new ParameterDefinition(m.TypeSystem.String) } });
        var exists = m.ImportReference(new MethodReference("Exists", m.TypeSystem.Boolean,
            new TypeReference("System.IO", "File", m, m.TypeSystem.CoreLibrary)) { HasThis = false,
            Parameters = { new ParameterDefinition(m.TypeSystem.String) } });
        var b = refresh.Body; b.Instructions.Clear(); b.Variables.Clear(); b.ExceptionHandlers.Clear();
        var il = b.GetILProcessor();
        // GetDirectoryName(dataPath): the folder holding the data folder (desktop), or holding the
        // APK/OBB that dataPath names on Android ("dataPath/.." only resolves for a directory)
        il.Emit(OpCodes.Call, getDataPath); il.Emit(OpCodes.Call, dirName); il.Emit(OpCodes.Ldstr, "goggame-2048763853.info");
        il.Emit(OpCodes.Call, combine); il.Emit(OpCodes.Call, exists);
        il.Emit(OpCodes.Stsfld, fAvail); il.Emit(OpCodes.Ret);
        StubOut(Method(t, "checkInstallationStatusChanged"));
    }

    // The Mac build compiled Rewired's InputManager.DetectPlatform with platform = OSX, so Rewired
    // loads Rewired_OSX_Lib (IOKit) and fails on Linux. Report Linux and let CupheadPort.Pad choose
    // the input source (SDL2 by default, Unity input in keyboard mode); neither needs Rewired's
    // Linux platform library, which the Mac build does not have.
    static void RewiredLinux(ModuleDefinition m)
    {
        var im = Type(m, "Rewired.InputManager");
        var detect = Method(im, "DetectPlatform");
        var baseT = im.BaseType.Resolve();
        var fEditorPlatform = m.ImportReference(Field(baseT, "editorPlatform"));
        var fPlatform = m.ImportReference(Field(baseT, "platform"));
        var fWebplayer = m.ImportReference(Field(baseT, "webplayerPlatform"));
        var fIsEditor = m.ImportReference(Field(baseT, "isEditor"));
        var getUserData = m.ImportReference(baseT.Properties.Single(p => p.Name == "userData").GetMethod);
        var userDataT = baseT.Properties.Single(p => p.Name == "userData").PropertyType.Resolve();
        var getConfigVars = m.ImportReference(userDataT.Properties.Single(p => p.Name == "ConfigVars").GetMethod);
        var configVarsT = m.ImportReference(userDataT.Properties.Single(p => p.Name == "ConfigVars").PropertyType);
        var configure = HelperMethod(m, "Pad", "ConfigureRewired", m.TypeSystem.Void, configVarsT);

        var body = detect.Body;
        body.Instructions.Clear();
        body.Variables.Clear();
        body.ExceptionHandlers.Clear();
        var il = body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Stfld, fEditorPlatform);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_6); il.Emit(OpCodes.Stfld, fPlatform);   // Platform.Linux
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Stfld, fWebplayer);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Stfld, fIsEditor);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, getUserData);
        il.Emit(OpCodes.Callvirt, getConfigVars);
        il.Emit(OpCodes.Call, configure);
        il.Emit(OpCodes.Ret);
    }}
