// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using TraceLog;
using XDM.Core;

namespace XDM.Core.BrowserMonitoring
{
    // Manages the lifecycle of the browser integration IPC server
    public static class BrowserMonitor
    {
        private static IpcHttpMessageProcessor? messageProcessor;

        // Starts the IPC message processor
        public static void Run()
        {
            try
            {
                messageProcessor = new IpcHttpMessageProcessor();
                messageProcessor.Run();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
        }

        // Restarts the IPC message processor listener
        public static void Restart()
        {
            try
            {
                if (messageProcessor == null)
                {
                    Run();
                }
                else
                {
                    messageProcessor.Restart();
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "BrowserMonitor.Restart error: " + ex.Message);
            }
        }
    }
}
