// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using TraceLog;
using XDM.Core.BrowserMonitoring;

namespace XDM.Core
{
    public static class SingleInstance
    {
        public static Mutex? GlobalMutex;
        private static bool ownsMutex = false;

        // Atomically acquires named mutex or forwards command args if another instance is active
        public static void Ensure()
        {
            try
            {
                GlobalMutex = new Mutex(false, @"Global\FetchFlow_Active_Instance");
                ownsMutex = GlobalMutex.WaitOne(TimeSpan.FromMilliseconds(500), false);
            }
            catch (AbandonedMutexException)
            {
                // Previous instance exited abnormally; this instance now owns the mutex
                ownsMutex = true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "SingleInstance mutex acquisition error: " + ex.Message);
                ownsMutex = true;
            }

            if (!ownsMutex)
            {
                Log.Debug("Another instance is actively running; forwarding arguments and exiting.");
                SendArgsToRunningInstance();
                Environment.Exit(0);
            }
        }

        private static void SendArgsToRunningInstance()
        {
            try
            {
                Log.Debug("Sending to running instance...");
                var args = Environment.GetCommandLineArgs().Skip(1);
                var postData = JsonConvert.SerializeObject(args.Count() == 0 ? new string[] { "--restore-window" } : args);
                Log.Debug("Sending...");
                var data = Encoding.UTF8.GetBytes(postData);
                // Phase2.3: probe the IPC port range; the running instance may have fallen back
                for (int p = Config.IpcPort; p < Config.IpcPort + 7; p++)
                {
                    try
                    {
                        var request = WebRequest.Create($"http://127.0.0.1:{p}/args");
                        request.Method = "POST";
                        request.ContentType = "application/json";
                        request.ContentLength = data.Length;
                        using (var stream = request.GetRequestStream())
                        {
                            stream.Write(data, 0, data.Length);
                        }
                        request.Timeout = 1500;
                        var response = request.GetResponse();
                        Log.Debug($"Sent args to running instance on port {p}...");
                        return; // success — stop probing
                    }
                    catch (Exception pex)
                    {
                        Log.Debug($"Port {p} did not respond: {pex.Message}");
                    }
                }
                Log.Debug("No running instance responded on the IPC port range.");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed sending args to running instance");
            }
        }
    }

    public class InstanceAlreadyRunningException : Exception
    {
        public InstanceAlreadyRunningException(string message) : base(message)
        {
        }
    }
}
