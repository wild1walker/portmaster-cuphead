using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CupheadPort
{
    // Mono loads each precompiled (AOT) method the first time it is called, and compiles the ones
    // that are not precompiled (Unity's internal-call wrappers, some generic code) at that moment.
    // Under box64 on the H700 that costs a few milliseconds per method, so the first shot of a
    // session (a hundred or more new methods: weapon, bullet, effects, physics, UI) stalled for
    // 200-300 ms, and so did the first tutorial prompt.
    //
    // This loads or compiles them up front, while a loading screen is up:
    //  - at start-up, the internal-call wrappers of the Unity engine modules and .NET (Transform,
    //    Animator, physics, rendering...: compiled on first use otherwise) and the generic
    //    collections their classes keep in fields;
    //  - after every scene load, the methods of every script type the scene has loaded (placed
    //    objects and the prefabs they can spawn: bullets, effects, prompts), with the compiler-
    //    generated classes inside them (coroutines, lambdas) and the generic collections they
    //    keep in fields (List<Foo>, Dictionary<Foo, Bar>).
    // Each method is done once per session. CUPHEAD_CODEWARM=0 turns it off.
    public static class CodeWarmup
    {
        const BindingFlags All = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static |
                                 BindingFlags.Public | BindingFlags.NonPublic;
        static readonly HashSet<Type> done = new HashSet<Type>();
        static bool startupDone;
        static int off = -1;

        static bool Off()
        {
            if (off < 0) off = Environment.GetEnvironmentVariable("CUPHEAD_CODEWARM") == "0" ? 1 : 0;
            return off == 1;
        }

        static int Prepare(MethodBase m)
        {
            if (m.IsAbstract || m.ContainsGenericParameters) return 0;
            try
            {
                m.MethodHandle.GetFunctionPointer();   // loads the AOT code, or compiles the method
                return 1;
            }
            catch (Exception) { return 0; }
        }

        static int Methods(Type t)
        {
            int n = 0;
            try
            {
                foreach (MethodInfo m in t.GetMethods(All)) n += Prepare(m);
                foreach (ConstructorInfo c in t.GetConstructors(All)) n += Prepare(c);
            }
            catch (Exception) { }
            return n;
        }

        // a closed generic type (List<Foo>) and its nested types (enumerators)
        static int WarmGeneric(Type t)
        {
            if (!t.IsGenericType || t.ContainsGenericParameters || !done.Add(t)) return 0;
            int n = Methods(t);
            foreach (Type arg in t.GetGenericArguments()) n += WarmGeneric(arg);
            return n;
        }

        // a script type: its methods, its nested (compiler-generated) types, the generic collections
        // in its fields, then its base types
        static int WarmType(Type t)
        {
            int n = 0;
            for (; t != null && t != typeof(MonoBehaviour) && t != typeof(ScriptableObject) && done.Add(t); t = t.BaseType)
            {
                if (t.ContainsGenericParameters) continue;
                n += Methods(t);
                try
                {
                    foreach (Type nested in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                        if (!nested.ContainsGenericParameters) n += WarmType(nested);
                    foreach (FieldInfo f in t.GetFields(All)) n += WarmGeneric(f.FieldType);
                }
                catch (Exception) { }
            }
            return n;
        }

        // the internal calls of an engine assembly (their wrappers are compiled on first use; loading
        // all of an assembly's other methods would take far too long), and the generic collections
        // kept in its classes' fields (UI mesh building: List<Vector2>, List<UIVertex>...)
        static int WarmEngineAssembly(string name)
        {
            int n = 0;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.GetName().Name != name) continue;
                Type[] types;
                try { types = a.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types; }
                foreach (Type t in types)
                {
                    if (t == null || t.ContainsGenericParameters || !done.Add(t)) continue;
                    try
                    {
                        foreach (MethodInfo m in t.GetMethods(All))
                            if ((m.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) != 0) n += Prepare(m);
                        foreach (FieldInfo f in t.GetFields(All)) n += WarmGeneric(f.FieldType);
                    }
                    catch (Exception) { }
                }
            }
            return n;
        }

        // engine and library classes the game calls into all the time: all their methods (few hundred)
        static readonly string[] HotTypes = {
            "UnityEngine.Animator, UnityEngine.AnimationModule", "UnityEngine.Component, UnityEngine.CoreModule",
            "UnityEngine.GameObject, UnityEngine.CoreModule", "UnityEngine.Transform, UnityEngine.CoreModule",
            "UnityEngine.Material, UnityEngine.CoreModule", "UnityEngine.Renderer, UnityEngine.CoreModule",
            "UnityEngine.SpriteRenderer, UnityEngine.CoreModule", "UnityEngine.Object, UnityEngine.CoreModule",
            "UnityEngine.Vector2, UnityEngine.CoreModule", "UnityEngine.Vector3, UnityEngine.CoreModule",
            "UnityEngine.Vector4, UnityEngine.CoreModule", "UnityEngine.Rect, UnityEngine.CoreModule",
            "UnityEngine.Color, UnityEngine.CoreModule", "UnityEngine.Quaternion, UnityEngine.CoreModule",
            "UnityEngine.Mathf, UnityEngine.CoreModule", "UnityEngine.MonoBehaviour, UnityEngine.CoreModule",
            "UnityEngine.UI.Image, UnityEngine.UI", "UnityEngine.UI.Mask, UnityEngine.UI", "UnityEngine.UI.Graphic, UnityEngine.UI",
            "UnityEngine.UI.MaskableGraphic, UnityEngine.UI", "UnityEngine.UI.Text, UnityEngine.UI",
            "UnityEngine.UI.StencilMaterial, UnityEngine.UI", "UnityEngine.UI.VertexHelper, UnityEngine.UI",
            "UnityEngine.EventSystems.ExecuteEvents, UnityEngine.UI", "UnityEngine.EventSystems.BaseInputModule, UnityEngine.UI",
            "Rewired.ReInput, Rewired_Core", "Rewired.Player, Rewired_Core",
            "System.NumberFormatter, mscorlib", "System.Enum, mscorlib", "System.String, mscorlib",
            "System.Text.StringBuilder, mscorlib",
        };

        static int WarmStartup()
        {
            int n = 0;
            foreach (string name in HotTypes)
            {
                Type t = Type.GetType(name, false);
                if (t != null) n += Methods(t);
            }
            foreach (string asm in new[] { "UnityEngine.CoreModule", "UnityEngine.UI", "UnityEngine.AnimationModule",
                                           "UnityEngine.Physics2DModule", "UnityEngine.AudioModule", "UnityEngine.ParticleSystemModule",
                                           "UnityEngine.TextRenderingModule", "UnityEngine.InputModule", "UnityEngine.UIModule",
                                           "UnityEngine.SpriteMaskModule", "UnityEngine.IMGUIModule", "mscorlib" })
                n += WarmEngineAssembly(asm);
            return n;
        }

        // Called after every scene load (Warmup.OnSceneLoaded).
        public static void AfterSceneLoad(string scene)
        {
            if (Off()) return;
            float t0 = Time.realtimeSinceStartup;
            int n = 0, startup = 0;
            if (!startupDone)
            {
                startupDone = true;
                startup = WarmStartup();
            }
            var types = new HashSet<Type>();
            foreach (MonoBehaviour b in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                if (b != null) types.Add(b.GetType());
            foreach (ScriptableObject s in Resources.FindObjectsOfTypeAll<ScriptableObject>())
                if (s != null) types.Add(s.GetType());
            foreach (Type t in types) n += WarmType(t);
            UnityEngine.Debug.Log("[cupheadport] code warmup for " + scene + ": " + n + " methods of " + types.Count +
                " script types" + (startup > 0 ? ", " + startup + " engine and library methods" : "") + " in " +
                (int)((Time.realtimeSinceStartup - t0) * 1000f) + " ms");
        }
    }
}
