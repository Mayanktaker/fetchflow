// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.IO;

namespace TraceLog
{
    // Thread-safe application logging utility with console and file sink
    public static class Log
    {
        private static string? logFilePath;
        private static readonly object lockObj = new();

        // Initializes the file log path
        public static void InitFileBasedTrace(string logfile)
        {
            try
            {
                logFilePath = logfile;
                Debug("Log initialized at: " + logfile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Log init error: " + ex);
            }
        }

        // Writes formatted message with object context
        public static void Debug(object? obj, string message)
        {
            Debug($"{message} : {obj}");
        }

        // Writes a recovered-failure line. Used where an operation is deliberately
        // best-effort (aborting a request, closing a stream) so the swallowed error
        // is still greppable in the trace instead of vanishing.
        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        // Writes a formatted warn line with object context
        public static void Warn(object? obj, string message)
        {
            Warn($"{message} : {obj}");
        }

        // Writes timestamped line to stdout and log file
        public static void Debug(string message)
        {
            Write("DEBUG", message);
        }

        // Single sink for both levels so the file writer is never duplicated.
        private static void Write(string level, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            Console.WriteLine(line);
            if (!string.IsNullOrEmpty(logFilePath))
            {
                try
                {
                    lock (lockObj)
                    {
                        File.AppendAllText(logFilePath, line + Environment.NewLine);
                    }
                }
                catch (Exception ex)
                {
                    // Never let logging failure mask the original problem, but keep it
                    // visible on the console sink so a broken log file is diagnosable.
                    Console.WriteLine("Log write error: " + ex.Message);
                }
            }
        }
    }
}
