// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Text;
using XDM.Core.BrowserMonitoring;
using XDM.Core.UI;
using XDM.Core.Util;

namespace XDM.Core
{
    public static class ApplicationContext
    {
        // Registration-only container: every service field is written exactly once by
        // AppInstanceConfigurer before s_Init flips, and is immutable afterwards. The
        // volatile read of s_Init in each accessor is therefore the release/acquire
        // barrier that publishes those writes to download and IPC threads — without it
        // a reader can observe s_Init == true while still seeing a null service field.
        private static IApplicationCore? s_ApplicationCore;
        private static IApplication? s_IApplication;
        private static IApplicationWindow? s_ApplicationWindow;
        private static ILinkRefresher? s_LinkRefresher;
        private static IVideoTracker? s_VideoTracker;
        private static IClipboardMonitor? s_ClipboardMonitor;
        private static IPlatformUIService? s_PlatformUIService;
        private static volatile bool s_Init = false;

        public static event EventHandler? Initialized;
        public static event EventHandler<ApplicationEvent>? ApplicationEvent;
        public static event EventHandler? FirstRunCallback;

        public static IApplicationCore CoreService
        {
            get
            {
                if (!s_Init)
                {
                    throw new Exception("ApplicationContext is not initialized...");
                }
                return s_ApplicationCore!;
            }
        }

        public static IApplication Application
        {
            get
            {
                if (!s_Init)
                {
                    throw new Exception("ApplicationContext is not initialized...");
                }
                return s_IApplication!;
            }
        }

        public static IApplicationWindow MainWindow
        {
            get
            {
                if (!s_Init)
                {
                    throw new Exception("ApplicationContext is not initialized...");
                }
                return s_ApplicationWindow!;
            }
        }

        public static ILinkRefresher LinkRefresher
        {
            get
            {
                if (!s_Init)
                {
                    throw new Exception("ApplicationContext is not initialized...");
                }
                return s_LinkRefresher!;
            }
        }

        public static IVideoTracker VideoTracker
        {
            get
            {
                if (!s_Init)
                {
                    throw new Exception("ApplicationContext is not initialized...");
                }
                return s_VideoTracker!;
            }
        }

        public static IClipboardMonitor ClipboardMonitor
        {
            get
            {
                if (!s_Init)
                {
                    throw new Exception("ApplicationContext is not initialized...");
                }
                return s_ClipboardMonitor!;
            }
        }

        public static IPlatformUIService PlatformUIService
        {
            get
            {
                if (!s_Init)
                {
                    throw new Exception("ApplicationContext is not initialized...");
                }
                return s_PlatformUIService!;
            }
        }

        public static void BroadcastConfigChange()
        {
            ApplicationEvent?.Invoke(null, new ApplicationEvent("ConfigChanged"));
        }

        public static void ExtensionRegistered()
        {
            ApplicationEvent?.Invoke(null, new ApplicationEvent("ExtensionRegistered"));
        }

        // Broadcasts an application-wide event with optional payload data
        public static void RaiseApplicationEvent(string eventType, object? data = null)
        {
            ApplicationEvent?.Invoke(null, new ApplicationEvent(eventType, data));
        }

        public static AppInstanceConfigurer Configurer()
        {
            return new AppInstanceConfigurer();
        }

        public class AppInstanceConfigurer
        {
            public AppInstanceConfigurer RegisterApplicationCore(IApplicationCore service)
            {
                s_ApplicationCore = service;
                return this;
            }

            public AppInstanceConfigurer RegisterApplication(IApplication service)
            {
                s_IApplication = service;
                return this;
            }

            public AppInstanceConfigurer RegisterApplicationWindow(IApplicationWindow service)
            {
                s_ApplicationWindow = service;
                return this;
            }

            public AppInstanceConfigurer RegisterLinkRefresher(ILinkRefresher service)
            {
                s_LinkRefresher = service;
                return this;
            }

            public AppInstanceConfigurer RegisterCapturedVideoTracker(IVideoTracker service)
            {
                s_VideoTracker = service;
                return this;
            }

            public AppInstanceConfigurer RegisterClipboardMonitor(IClipboardMonitor service)
            {
                s_ClipboardMonitor = service;
                return this;
            }

            public AppInstanceConfigurer RegisterPlatformUIService(IPlatformUIService service)
            {
                s_PlatformUIService = service;
                return this;
            }

            public void Configure()
            {
                if (s_ApplicationCore == null || s_IApplication == null
                    || s_ApplicationWindow == null || s_LinkRefresher == null
                    || s_VideoTracker == null || s_ClipboardMonitor == null
                    || s_PlatformUIService == null)
                {
                    throw new Exception("Please configure all dependencies");
                }
                SingleInstance.Ensure();
                s_Init = true;
                Initialized?.Invoke(null, EventArgs.Empty);
                if (PlatformHelper.IsFirstRun())
                {
                    FirstRunCallback?.Invoke(null, EventArgs.Empty);
                }
            }
        }
    }

    public class ApplicationEvent : EventArgs
    {
        public ApplicationEvent(string eventType, object? data = null)
        {
            EventType = eventType;
            Data = data;
        }

        public string EventType { get; }
        public object? Data { get; set; }

    }
}
