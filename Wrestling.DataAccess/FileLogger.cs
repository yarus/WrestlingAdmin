using System;
using System.IO;
using System.Reflection;

namespace Wrestling.DataAccess
{
    public static class FileLogger
    {
        private static readonly object _lock = new object();
        private const string AppFolderFallback = "WrestlingAdmin";

        public static void Log(string source, string path, Exception ex)
        {
            var entry = string.Format(
                "[{0:yyyy-MM-dd HH:mm:ss.fff}] {1} | path: {2} | {3}: {4}{5}{6}{7}",
                DateTime.Now,
                source,
                path ?? "<null>",
                ex?.GetType().FullName ?? "<no-exception>",
                ex?.Message ?? string.Empty,
                ex?.InnerException != null ? " | inner: " + ex.InnerException.Message : string.Empty,
                Environment.NewLine,
                ex?.StackTrace != null ? ex.StackTrace + Environment.NewLine : string.Empty);

            Write(entry);
        }

        public static void Log(string source, string path, string message)
        {
            var entry = string.Format(
                "[{0:yyyy-MM-dd HH:mm:ss.fff}] {1} | path: {2} | {3}{4}",
                DateTime.Now,
                source,
                path ?? "<null>",
                message ?? string.Empty,
                Environment.NewLine);

            Write(entry);
        }

        private static void Write(string entry)
        {
            try
            {
                var logPath = GetLogFilePath();
                lock (_lock)
                {
                    File.AppendAllText(logPath, entry);
                }
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine(logEx);
            }
        }

        private static string GetLogFilePath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appName = Assembly.GetEntryAssembly()?.GetName().Name ?? AppFolderFallback;
            var logDirectory = Path.Combine(appDataPath, appName, "Logs");
            Directory.CreateDirectory(logDirectory);
            return Path.Combine(logDirectory, string.Format("data_log_{0:yyyyMMdd}.txt", DateTime.Now));
        }
    }
}
