using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CupheadPort
{
    // Diagnostics only (CUPHEAD_JITMAP=<file>): names the methods behind code addresses. The profiler
    // (PROF_JITADDR=<file>) appends the addresses of the main thread's samples in code Mono generated
    // at run time; every 5 s their methods are looked up in Mono's JIT table and written to <file>.names.
    public static class JitMap
    {
        [DllImport("libmono.so.0")] static extern IntPtr mono_domain_get();
        [DllImport("libmono.so.0")] static extern IntPtr mono_jit_info_table_find(IntPtr domain, IntPtr addr);
        [DllImport("libmono.so.0")] static extern IntPtr mono_jit_info_get_method(IntPtr ji);
        [DllImport("libmono.so.0")] static extern IntPtr mono_jit_info_get_code_start(IntPtr ji);
        [DllImport("libmono.so.0")] static extern IntPtr mono_method_full_name(IntPtr method, int signature);

        static string path;
        static int state;                          // 0 not checked, 1 on, -1 off
        static float next;
        static long readPos;
        static readonly HashSet<long> done = new HashSet<long>();

        public static void Tick()
        {
            if (state == 0)
            {
                path = Environment.GetEnvironmentVariable("CUPHEAD_JITMAP");
                state = string.IsNullOrEmpty(path) ? -1 : 1;
            }
            if (state < 0 || UnityEngine.Time.realtimeSinceStartup < next) return;
            next = UnityEngine.Time.realtimeSinceStartup + 5f;
            try
            {
                if (!File.Exists(path)) return;
                var sb = new StringBuilder();
                using (var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    f.Seek(readPos, SeekOrigin.Begin);
                    var r = new StreamReader(f);
                    string line;
                    IntPtr domain = mono_domain_get();
                    while ((line = r.ReadLine()) != null)
                    {
                        long a;
                        if (!long.TryParse(line.Trim(), System.Globalization.NumberStyles.HexNumber, null, out a) || !done.Add(a)) continue;
                        string name = "?";
                        IntPtr ji = IntPtr.Zero;
                        for (int off = 0; off < 64 && ji == IntPtr.Zero; off += 8)   // the address is a 64-byte bucket
                            ji = mono_jit_info_table_find(domain, new IntPtr(a + off));
                        if (ji != IntPtr.Zero)
                        {
                            IntPtr m = mono_jit_info_get_method(ji);
                            if (m != IntPtr.Zero) name = Marshal.PtrToStringAnsi(mono_method_full_name(m, 1)) ?? "?";
                        }
                        sb.Append(a.ToString("x")).Append(' ').Append(name).Append('\n');
                    }
                    readPos = f.Position;
                }
                if (sb.Length > 0) File.AppendAllText(path + ".names", sb.ToString());
            }
            catch (Exception e)
            {
                state = -1;
                UnityEngine.Debug.Log("[cupheadport] jitmap off: " + e.GetType().Name + ": " + e.Message);
            }
        }
    }
}
