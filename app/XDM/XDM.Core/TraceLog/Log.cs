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

        // Writes timestamped line to stdout and log file
        public static void Debug(string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
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
                catch { }
            }
        }
    }
}
