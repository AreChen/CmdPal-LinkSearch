using System;

namespace LinkSearch.Helpers
{
    /// <summary>
    /// 简单的可控日志封装（用于替换大量 Debug.WriteLine 调用）。
    /// 默认根据环境变量 LINKSEARCH_DEBUG（"1" 或 "true"）开启 Debug 级别日志。
    /// 可以在运行时通过 Log.SetEnabled(true/false) 显式开启/关闭。
    /// </summary>
    internal static class Log
    {
        private static bool s_initialised;
        private static bool s_enabled;
        private static readonly object s_lock = new object();

        private static void EnsureInit()
        {
            if (s_initialised) return;
            lock (s_lock)
            {
                if (s_initialised) return;
                var env = Environment.GetEnvironmentVariable("LINKSEARCH_DEBUG");
                s_enabled = string.Equals(env, "1", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(env, "true", StringComparison.OrdinalIgnoreCase);
                s_initialised = true;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            lock (s_lock)
            {
                s_enabled = enabled;
                s_initialised = true;
            }
        }

        public static void Debug(string message)
        {
            EnsureInit();
            if (!s_enabled) return;
            try { System.Diagnostics.Debug.WriteLine(message); } catch { }
        }

        public static void Info(string message)
        {
            try { System.Diagnostics.Debug.WriteLine(message); } catch { }
        }

        public static void Warn(string message)
        {
            try { System.Diagnostics.Debug.WriteLine("WARN: " + message); } catch { }
        }

        public static void Error(string message)
        {
            try { System.Diagnostics.Debug.WriteLine("ERROR: " + message); } catch { }
        }
    }
}