// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using TraceLog;

namespace XDM.Core
{
    public static class SingleInstance
    {
        public static Mutex? GlobalMutex;
        private static bool ownsMutex = false;
        private static Timer? mutexWatchTimer;

        private const string MutexName = @"Global\FetchFlow_Active_Instance";
        private const string TakeoverGateName = @"Global\FetchFlow_Takeover_Gate";
        private const string RelayAckMarker = "\"blockedHosts\"";
        private const int MutexWaitTimeoutMs = 500;
        private const int MutexRecoveryAttempts = 3;
        private const int MutexRecoveryDelayMs = 500;
        private const int TakeoverGateWaitMs = 5000;
        private const int RelayGraceMs = 1500;
        private const int RelayGracePollMs = 200;
        private const int MutexReacquirePollMs = 5000;
        private const int IpcSendTimeoutMs = 800;

        // Atomically acquires named mutex or forwards command args if another instance is active;
        // recovers by taking over as primary when the mutex holder exists but its IPC is dead
        public static void Ensure()
        {
            try
            {
                GlobalMutex = new Mutex(false, MutexName);
                ownsMutex = GlobalMutex.WaitOne(TimeSpan.FromMilliseconds(MutexWaitTimeoutMs), false);
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

            var argsDelivered = false;
            var mutexRecovered = false;

            if (!ownsMutex)
            {
                Log.Debug("Another instance appears active; forwarding arguments...");
                argsDelivered = SendArgsToRunningInstance();
                if (!argsDelivered)
                {
                    // Holder may be exiting right now — a brief retry avoids dual primaries
                    mutexRecovered = TryRecoverMutex();
                    if (!mutexRecovered)
                    {
                        // Slow-starting or just-took-over primary may bring its relay up
                        // within the grace window; the gate serializes concurrent launchers
                        argsDelivered = WaitForRelayInsideTakeoverGate();
                    }
                }
            }

            switch (SingleInstancePolicy.Decide(ownsMutex, argsDelivered, mutexRecovered))
            {
                case SingleInstanceAction.ForwardAndExit:
                    Log.Debug("Arguments delivered to running instance; exiting.");
                    Environment.Exit(0);
                    break;
                case SingleInstanceAction.TakeOverAsPrimary:
                    Log.Debug("SingleInstance: mutex held but IPC is unresponsive — previous instance is defunct; taking over as primary.");
                    ReacquireMutexInBackground();
                    break;
            }
        }

        // Short retry burst: grabs the mutex if the defunct holder exits during launch
        private static bool TryRecoverMutex()
        {
            for (int attempt = 0; attempt < MutexRecoveryAttempts; attempt++)
            {
                Thread.Sleep(MutexRecoveryDelayMs);
                try
                {
                    if (GlobalMutex!.WaitOne(0, false))
                    {
                        ownsMutex = true;
                        Log.Debug("SingleInstance: mutex recovered after defunct holder exited.");
                        return true;
                    }
                }
                catch (AbandonedMutexException)
                {
                    ownsMutex = true; // acquisition succeeds despite the exception
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        // Serializes takeover decisions: inside the gate, waits a grace period for a
        // fresh primary's relay to appear before this launch takes over itself
        private static bool WaitForRelayInsideTakeoverGate()
        {
            Mutex? gate = null;
            var acquired = false;
            try
            {
                gate = new Mutex(false, TakeoverGateName);
                acquired = gate.WaitOne(TimeSpan.FromMilliseconds(TakeoverGateWaitMs), false);
            }
            catch (AbandonedMutexException)
            {
                acquired = true; // previous gate holder died; gate is ours now
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Takeover gate unavailable: " + ex.Message);
            }

            try
            {
                var deadline = Environment.TickCount64 + RelayGraceMs;
                while (Environment.TickCount64 < deadline)
                {
                    if (SendArgsToRunningInstance())
                    {
                        return true; // a live primary answered inside the gate
                    }
                    Thread.Sleep(RelayGracePollMs);
                }
                return false;
            }
            finally
            {
                if (acquired)
                {
                    try { gate!.ReleaseMutex(); } catch { }
                }
                gate?.Dispose();
            }
        }

        // Becomes the canonical mutex owner in the background once the defunct holder exits
        private static void ReacquireMutexInBackground()
        {
            mutexWatchTimer = new Timer(_ =>
            {
                try
                {
                    if (GlobalMutex!.WaitOne(0, false))
                    {
                        ownsMutex = true;
                        StopMutexWatch();
                        Log.Debug("SingleInstance: mutex ownership recovered after takeover.");
                    }
                }
                catch (AbandonedMutexException)
                {
                    ownsMutex = true; // acquisition succeeds despite the exception
                    StopMutexWatch();
                }
                catch (Exception)
                {
                    // Keep polling on the next tick until the defunct holder exits
                }
            }, null, MutexReacquirePollMs, MutexReacquirePollMs);
        }

        // Stops the background ownership poll
        private static void StopMutexWatch()
        {
            mutexWatchTimer?.Dispose();
            mutexWatchTimer = null;
        }

        // Posts command args to the live instance; true only when it acknowledged delivery
        private static bool SendArgsToRunningInstance()
        {
            try
            {
                var args = Environment.GetCommandLineArgs().Skip(1);
                var postData = JsonConvert.SerializeObject(args.Count() == 0 ? new string[] { "--restore-window" } : args);
                var data = Encoding.UTF8.GetBytes(postData);
                // The running instance may have fallen back within the IPC port range
                for (int p = Config.IpcPort; p < Config.IpcPort + Config.IpcPortRangeSize; p++)
                {
                    if (TrySendArgsToPort(p, data))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed sending args to running instance");
                return false;
            }
        }

        // Delivers the args payload to one IPC port; true only when the FetchFlow relay
        // answers with its config JSON (guards against foreign servers on the port range)
        private static bool TrySendArgsToPort(int port, byte[] data)
        {
            try
            {
                var request = WebRequest.Create($"http://127.0.0.1:{port}/args");
                request.Method = "POST";
                request.ContentType = "application/json";
                request.ContentLength = data.Length;
                request.Timeout = IpcSendTimeoutMs;
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream() ?? Stream.Null))
                {
                    if (!reader.ReadToEnd().Contains(RelayAckMarker))
                    {
                        Log.Debug($"Port {port} answered but is not a FetchFlow relay.");
                        return false;
                    }
                }
                Log.Debug($"Sent args to running instance on port {port}.");
                return true;
            }
            catch (Exception pex)
            {
                Log.Debug($"Port {port} did not respond: {pex.Message}");
                return false;
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
