// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using Newtonsoft.Json;
using XDM.Core.Util;
using XDM.Core.HttpServer;
using System.Threading;
using TraceLog;
using Translations;
using System.IO;
using System.Collections.Generic;

namespace XDM.Core.BrowserMonitoring
{
    public class IpcHttpMessageProcessor
    {
        private NanoServer server;
        private static string[] blockedHeaders = { "accept", "if", "authorization", "proxy", "connection", "expect", "TE",
            "upgrade", "range", "cookie", "transfer-encoding", "content-type", "content-length","content-encoding" };

        public static IpcHttpMessageProcessor? Instance { get; private set; }

        // Phase2.3: the port actually bound (the browser extension probes the same range)
        public static int EffectivePort { get; private set; } = 8597;

        // Active connection metrics
        public static DateTime LastActivityTime { get; private set; } = DateTime.MinValue;
        public static int ActiveWebSocketSessionsCount => Instance?.wsSessions.Count ?? 0;
        public static bool IsConnected => ActiveWebSocketSessionsCount > 0 || (DateTime.UtcNow - LastActivityTime).TotalSeconds < 60;

        // Phase6: all active WebSocket sessions (for push/broadcast to extensions)
        private readonly List<WebSocketSession> wsSessions = new();
        private readonly object wsLock = new();

        // Blob upload receiver for handling chunked binary blob downloads from extensions
        private readonly BlobUploadReceiver blobReceiver = new();

        // Periodic cleanup timer for stale blob uploads (every 5 minutes)
        private readonly Timer blobPurgeTimer;

        // Relay supervision: a live process must always answer on the IPC range —
        // single-instance arbitration treats "all ports dead" as "instance defunct"
        private volatile bool stopped = false;
        private int runGeneration = 0;
        private const int RelayRestartDelayMs = 3000;
        private const int BindRetryDelayMs = 10000;

        public IpcHttpMessageProcessor()
        {
            Instance = this;
            EffectivePort = Config.IpcPort;

            // Start periodic cleanup of abandoned blob transfers (every 5 min)
            blobPurgeTimer = new Timer(_ => blobReceiver.PurgeStaleTransfers(),
                null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            // Push config & video list updates to all connected WebSockets immediately on change
            ApplicationContext.ApplicationEvent += OnApplicationEvent;
        }

        private void OnApplicationEvent(object? sender, ApplicationEvent e)
        {
            if (e.EventType == "ConfigChanged")
            {
                BroadcastConfig();
            }
        }

        // Starts the supervised IPC HTTP/WebSocket listener on the first available port in the range
        public void Run()
        {
            stopped = false;
            var gen = Interlocked.Increment(ref runGeneration);
            new Thread(() =>
            {
                // Supervision loop: if the relay ever ends unexpectedly, re-bind so this
                // process keeps answering IPC probes (prevents defunct-instance lockouts).
                // The generation counter invalidates stale loops after Stop()/Restart().
                while (!stopped && gen == runGeneration)
                {
                    if (!TryBindAndServe(gen))
                    {
                        // Ports may be foreign-occupied for a while — retry instead of giving up
                        Log.Debug("All IPC ports busy; retrying in " + (BindRetryDelayMs / 1000) + "s...");
                        Thread.Sleep(BindRetryDelayMs);
                        continue;
                    }
                    if (stopped || gen != runGeneration)
                    {
                        return;
                    }
                    Log.Debug("IPC relay ended unexpectedly; restarting listener in " + (RelayRestartDelayMs / 1000) + "s...");
                    Thread.Sleep(RelayRestartDelayMs);
                }
            })
            {
                IsBackground = true
            }.Start();
        }

        // Binds the relay on the first free port and serves until the accept loop stops
        private bool TryBindAndServe(int gen)
        {
            for (int p = Config.IpcPort; p < Config.IpcPort + Config.IpcPortRangeSize; p++)
            {
                if (stopped || gen != runGeneration)
                {
                    return true; // superseded by a newer Run() — let the loop condition exit it
                }
                try
                {
                    EffectivePort = p;
                    Log.Debug("IPC HTTP relay starting on 127.0.0.1:" + EffectivePort);
                    server = new NanoServer(IPAddress.Loopback, EffectivePort);
                    server.RequestReceived += (sender, args) => HandleRequest(args.RequestContext);
                    server.WebSocketAccepted += OnWebSocketAccepted;
                    server.Start();
                    return true; // accept loop ended — Stop() or a dead listener
                }
                catch (Exception ex)
                {
                    Log.Debug($"IPC port {p} start error: {ex.Message}");
                }
            }
            return false;
        }

        // Stops the active server and terminates active sessions
        public void Stop()
        {
            stopped = true;
            ApplicationContext.ApplicationEvent -= OnApplicationEvent;
            try
            {
                server?.Stop();
            }
            catch (Exception ex)
            {
                Log.Debug("Error stopping IPC server: " + ex.Message);
            }

            lock (wsLock)
            {
                foreach (var session in wsSessions)
                {
                    try { session.Close(); } catch { }
                }
                wsSessions.Clear();
            }
        }

        // Restarts the IPC server listener and re-probes available ports
        public void Restart()
        {
            Log.Debug("Restarting IPC HTTP relay...");
            Stop();
            Run();
            ApplicationContext.RaiseApplicationEvent("ExtensionConnectionChanged", 0);
        }

        public void HandleRequest(RequestContext context)
        {
            LastActivityTime = DateTime.UtcNow;
            try
            {
                // Blob upload: binary POST with custom headers — handle before OnSyncMessage
                if (context.RequestPath == "/blob-upload")
                {
                    blobReceiver.HandleUpload(context);
                    return;
                }

                switch (context.RequestPath)
                {
                    case "/sync":
                        break;
                    case "/download":
                        OnDownloadMessage(context);
                        break;
                    case "/media":
                        OnMediaMessage(context);
                        break;
                    case "/tab-update":
                        OnTabUpdateMessage(context);
                        break;
                    case "/vid":
                        OnVideoDownloadMessage(context);
                        break;
                    case "/clear":
                        ApplicationContext.VideoTracker.ClearVideoList();
                        break;
                    case "/link":
                        OnBatchMessage(context);
                        break;
                    case "/args":
                        OnArgsMessage(context);
                        break;
                    default:
                        throw new ArgumentException("Unsupported request: " + context.RequestPath);
                }
                OnSyncMessage(context);
            }
            catch (Exception ex)
            {
                Log.Debug(ex.ToString());
                throw;
            }
        }

        private void OnArgsMessage(RequestContext context)
        {
            var args = JsonConvert.DeserializeObject<List<string>>(Encoding.UTF8.GetString(context.RequestBody!));
            if (args == null || args.Count == 0)
            {
                return;
            }
            ArgsProcessor.Process(args);
        }

        private void OnVideoDownloadMessage(RequestContext context)
        {
            var msg = JsonConvert.DeserializeObject<ExtensionData>(Encoding.UTF8.GetString(context.RequestBody!));
            if (msg == null)
            {
                return;
            }
            ApplicationContext.VideoTracker.AddVideoDownload(msg.Vid);
        }

        private void OnTabUpdateMessage(RequestContext context)
        {
            var msg = JsonConvert.DeserializeObject<ExtensionData>(Encoding.UTF8.GetString(context.RequestBody!));
            if (msg == null)
            {
                return;
            }
            ApplicationContext.VideoTracker.UpdateMediaTitle(msg.TabUrl, msg.TabTitle);
            if (msg.TabUrl != null && msg.TabId != null)
            {
                VideoUrlHelper.ProcessMediaTab(msg.TabUrl, msg.TabId);
            }
        }

        private void OnDownloadMessage(RequestContext context)
        {
            var msg = JsonConvert.DeserializeObject<ExtensionData>(Encoding.UTF8.GetString(context.RequestBody!));
            if (msg == null)
            {
                return;
            }
            var dmsg = new Message();
            dmsg.Url = msg.Url;
            dmsg.RequestMethod = msg.Method;
            dmsg.RequestHeaders = msg.RequestHeaders ?? new Dictionary<string, List<string>>();
            dmsg.ResponseHeaders = msg.ResponseHeaders ?? new Dictionary<string, List<string>>();
            dmsg.Cookies = msg.Cookie;
            dmsg.File = FileHelper.SanitizeFileName(msg.File)!;
            dmsg.TabUrl = msg.TabUrl;
            dmsg.TabId = msg.TabId;
            RemoveBlockedHeaders(dmsg);
            ApplicationContext.CoreService.AddDownload(dmsg);
        }

        private void OnMediaMessage(RequestContext context)
        {
            var msg = JsonConvert.DeserializeObject<ExtensionData>(Encoding.UTF8.GetString(context.RequestBody!));
            if (msg == null)
            {
                return;
            }
            var dmsg = new Message();
            dmsg.Url = msg.Url;
            dmsg.RequestMethod = msg.Method;
            dmsg.RequestHeaders = msg.RequestHeaders ?? new Dictionary<string, List<string>>();
            dmsg.ResponseHeaders = msg.ResponseHeaders ?? new Dictionary<string, List<string>>();
            dmsg.Cookies = msg.Cookie;
            dmsg.File = FileHelper.SanitizeFileName(msg.File)!;
            dmsg.TabUrl = msg.TabUrl;
            dmsg.TabId = msg.TabId;
            RemoveBlockedHeaders(dmsg);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    VideoUrlHelper.ProcessMediaMessage(dmsg);
                }
                catch (Exception ex)
                {
                    Log.Debug("ProcessMediaMessage background error: " + ex.Message);
                }
            });
        }

        private void OnBatchMessage(RequestContext context)
        {
            var msgArr = JsonConvert.DeserializeObject<ExtensionData[]>(Encoding.UTF8.GetString(context.RequestBody!));
            if (msgArr == null)
            {
                return;
            }
            ApplicationContext.CoreService.AddBatchLinks(msgArr.Select(msg =>
            {
                var dmsg = new Message();
                dmsg.Url = msg.Url;
                dmsg.RequestMethod = msg.Method;
                dmsg.RequestHeaders = msg.RequestHeaders;
                dmsg.ResponseHeaders = msg.ResponseHeaders;
                dmsg.Cookies = msg.Cookie;
                dmsg.File = FileHelper.SanitizeFileName(msg.File)!;
                dmsg.TabUrl = msg.TabUrl;
                dmsg.TabId = msg.TabId;
                RemoveBlockedHeaders(dmsg);
                return dmsg;
            }).ToList());
        }

        //public void HandleRequest2(RequestContext context)
        //{
        //    if (context.RequestPath == "/204")
        //    {
        //        context.ResponseStatus = new ResponseStatus
        //        {
        //            StatusCode = 204,
        //            StatusMessage = "No Content"
        //        };
        //        context.AddResponseHeader("Cache-Control", "max-age=0, no-cache, must-revalidate");
        //        context.SendResponse();
        //        return;
        //    }

        //    try
        //    {
        //        switch (context.RequestPath)
        //        {
        //            case "/download":
        //                {
        //                    var text = Encoding.UTF8.GetString(context.RequestBody!);
        //                    Log.Debug(text);
        //                    var message = Message.ParseMessage(text);
        //                    if (!(Helpers.IsBlockedHost(message.Url) || Helpers.IsCompressedJSorCSS(message.Url)))
        //                    {
        //                        ApplicationContext.CoreService.AddDownload(message);
        //                    }
        //                    break;
        //                }
        //            case "/video":
        //                {
        //                    var text = Encoding.UTF8.GetString(context.RequestBody!);
        //                    Log.Debug(text);
        //                    var message2 = Message.ParseMessage(Encoding.UTF8.GetString(context.RequestBody!));
        //                    var contentType = message2.GetResponseHeaderFirstValue("Content-Type")?.ToLowerInvariant() ?? string.Empty;
        //                    if (VideoUrlHelper.IsHLS(contentType))
        //                    {
        //                        VideoUrlHelper.ProcessHLSVideo(message2);
        //                    }
        //                    if (VideoUrlHelper.IsDASH(contentType))
        //                    {
        //                        VideoUrlHelper.ProcessDashVideo(message2);
        //                    }
        //                    if (!VideoUrlHelper.ProcessYtDashSegment(message2))
        //                    {
        //                        if (contentType != null && !(contentType.Contains("f4f") ||
        //                            contentType.Contains("m4s") ||
        //                            contentType.Contains("mp2t") || message2.Url.Contains("abst") ||
        //                            message2.Url.Contains("f4x") || message2.Url.Contains(".fbcdn")
        //                            || message2.Url.Contains("http://127.0.0.1:9614")))
        //                        {
        //                            VideoUrlHelper.ProcessNormalVideo(message2);
        //                        }
        //                    }
        //                    break;
        //                }
        //            case "/links":
        //                {
        //                    var text = Encoding.UTF8.GetString(context.RequestBody!);
        //                    Log.Debug(text);
        //                    var arr = text.Split(new string[] { "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        //                    ApplicationContext.CoreService.AddBatchLinks(arr.Select(str => Message.ParseMessage(str.Trim())).ToList());
        //                    break;
        //                }
        //            case "/item":
        //                {
        //                    foreach (var item in Encoding.UTF8.GetString(context.RequestBody!).Split(new char[] { '\r', '\n' }))
        //                    {
        //                        ApplicationContext.VideoTracker.AddVideoDownload(item);
        //                    }
        //                    break;
        //                }
        //            case "/clear":
        //                ApplicationContext.VideoTracker.ClearVideoList();
        //                break;
        //        }
        //    }
        //    finally
        //    {
        //        SendSyncResponse(context);
        //    }
        //}

        private void OnSyncMessage(RequestContext context)
        {
            var json = CreateConfigJson();
            context.ResponseStatus = new ResponseStatus
            {
                StatusCode = 200,
                StatusMessage = "OK"
            };
            context.AddResponseHeader("Content-Type", "application/json");
            context.AddResponseHeader("Cache-Control", "max-age=0, no-cache, must-revalidate");
            context.ResponseBody = Encoding.UTF8.GetBytes(json);
            context.SendResponse();
        }

        private string? CreateConfigJson()
        {
            try
            {
                var w = new StringWriter();
                using var writer = new JsonTextWriter(w);
                writer.CloseOutput = false;
                writer.Formatting = Formatting.None;

                writer.WriteStartObject();

                writer.WritePropertyName("enabled");
                writer.WriteValue(Config.Instance.IsBrowserMonitoringEnabled);

                writer.WritePropertyName("fileExts");
                writer.WriteStartArray();
                foreach (var ext in Config.Instance.FileExtensions)
                {
                    writer.WriteValue(ext);
                }
                writer.WriteEndArray();

                writer.WritePropertyName("blockedHosts");
                writer.WriteStartArray();
                foreach (var host in Config.Instance.BlockedHosts)
                {
                    writer.WriteValue(host);
                }
                writer.WriteEndArray();

                writer.WritePropertyName("requestFileExts");
                writer.WriteStartArray();
                foreach (var ext in Config.Instance.VideoExtensions)
                {
                    writer.WriteValue(ext);
                }
                writer.WriteEndArray();

                writer.WritePropertyName("mediaTypes");
                writer.WriteStartArray();
                foreach (var ext in new string[] { "audio/", "video/" })
                {
                    writer.WriteValue(ext);
                }
                writer.WriteEndArray();

                writer.WritePropertyName("tabsWatcher");
                writer.WriteStartArray();
                // All yt-dlp supported domains — must match SupportedYdlDomains in VideoUrlHelper
                foreach (var ext in new string[] {
                    "youtube.com", "youtu.be", "/watch?v=",
                    "vimeo.com", "dailymotion.com",
                    "facebook.com", "fb.watch",
                    "instagram.com",
                    "twitter.com", "x.com",
                    "twitch.tv",
                    "bilibili.com", "tiktok.com", "reddit.com" })
                {
                    writer.WriteValue(ext);
                }
                writer.WriteEndArray();

                var videoList = ApplicationContext.VideoTracker.GetVideoList();

                writer.WritePropertyName("videoList");
                writer.WriteStartArray();
                foreach (var video in videoList)
                {
                    writer.WriteStartObject();

                    writer.WritePropertyName("id");
                    writer.WriteValue(video.ID);

                    writer.WritePropertyName("text");
                    writer.WriteValue(video.Name);

                    writer.WritePropertyName("info");
                    writer.WriteValue(video.Description);

                    writer.WritePropertyName("tabId");
                    writer.WriteValue(video.TabId);

                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                writer.WritePropertyName("matchingHosts");
                writer.WriteStartArray();
                foreach (var ext in new string[] { "googlevideo", "youtube" })
                {
                    writer.WriteValue(ext);
                }
                writer.WriteEndArray();

                writer.WritePropertyName("blobMaxBytes");
                writer.WriteValue(Config.Instance.BlobMaxBytes);

                writer.WriteEndObject();
                writer.Close();
                var str = w.ToString();
                return str;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error sending config");
                return null;
            }
        }

        private void RemoveBlockedHeaders(Message message)
        {
            foreach (var header in blockedHeaders)
            {
                string? keyName = null;
                foreach (var key in message.RequestHeaders.Keys)
                {
                    if (key.Equals(header, StringComparison.InvariantCultureIgnoreCase))
                    {
                        keyName = key;
                        break;
                    }
                }
                if (!String.IsNullOrEmpty(keyName))
                {
                    message.RequestHeaders.Remove(keyName!);
                }
            }
        }

        // Phase6: WebSocket support — bidirectional, real-time, no polling.
        // Extension sends JSON: { "path": "/download", "body": {...} }
        // XDM responds with the same config JSON as the HTTP /sync response.
        // This enables push: XDM can push video list updates to the extension instantly.

        private void OnWebSocketAccepted(string path, Dictionary<string, List<string>> headers, WebSocketSession session)
        {
            LastActivityTime = DateTime.UtcNow;
            lock (wsLock) { wsSessions.Add(session); }
            Log.Debug("WebSocket connected: " + path + " (total: " + wsSessions.Count + ")");
            ApplicationContext.RaiseApplicationEvent("ExtensionConnectionChanged", wsSessions.Count);

            session.OnClosed += s =>
            {
                lock (wsLock) { wsSessions.Remove(s); }
                Log.Debug("WebSocket disconnected (total: " + wsSessions.Count + ")");
                ApplicationContext.RaiseApplicationEvent("ExtensionConnectionChanged", wsSessions.Count);
            };

            // Read loop on a background thread
            new Thread(() =>
            {
                try
                {
                    string? msg;
                    while (session.IsConnected && (msg = session.ReadMessage()) != null)
                    {
                        HandleWsMessage(session, msg);
                    }
                }
                catch (Exception ex) { Log.Debug("WebSocket read loop error: " + ex.Message); }
                finally { session.Close(); }
            }) { IsBackground = true }.Start();
        }

        // Parse the WebSocket message envelope: { "path": "/download", "body": {...} }
        // Route to the same handlers as the HTTP path.
        private void HandleWsMessage(WebSocketSession session, string json)
        {
            try
            {
                var envelope = JsonConvert.DeserializeObject<WsEnvelope>(json);
                if (envelope?.Path == null) return;

                var bodyBytes = envelope.Body != null ? Encoding.UTF8.GetBytes(envelope.Body) : null;

                switch (envelope.Path)
                {
                    case "/sync":
                        // Extension requesting config → send config JSON back
                        session.Send(CreateConfigJson() ?? "{}");
                        break;
                    case "/ping":
                        session.Send(CreateConfigJson() ?? "{}");
                        break;
                    case "/download":
                        if (bodyBytes != null) OnDownloadMessage(bodyBytes);
                        session.Send(CreateConfigJson() ?? "{}");
                        break;
                    case "/media":
                        if (bodyBytes != null) OnMediaMessage(bodyBytes);
                        session.Send(CreateConfigJson() ?? "{}");
                        break;
                    case "/tab-update":
                        if (bodyBytes != null) OnTabUpdateMessage(bodyBytes);
                        session.Send(CreateConfigJson() ?? "{}");
                        break;
                    case "/vid":
                        if (bodyBytes != null) OnVideoDownloadMessage(bodyBytes);
                        session.Send(CreateConfigJson() ?? "{}");
                        break;
                    case "/clear":
                        ApplicationContext.VideoTracker.ClearVideoList();
                        session.Send(CreateConfigJson() ?? "{}");
                        break;
                    case "/link":
                        if (bodyBytes != null) OnBatchMessage(bodyBytes);
                        session.Send(CreateConfigJson() ?? "{}");
                        break;
                    case "/blob-upload":
                        Log.Debug("WebSocket: blob-upload not supported over WS; use HTTP POST");
                        session.Send("{\"error\":\"use HTTP POST for blob-upload\"}");
                        break;
                    default:
                        Log.Debug("WebSocket: unknown path " + envelope.Path);
                        session.Send(CreateConfigJson() ?? "{}");
                        break;
                }
            }
            catch (Exception ex) { Log.Debug("WebSocket message error: " + ex.Message); }
        }

        // Overload handlers that accept raw bytes (for WebSocket path reuse)
        private void OnDownloadMessage(byte[] body)
        {
            var msg = JsonConvert.DeserializeObject<ExtensionData>(Encoding.UTF8.GetString(body));
            if (msg == null) return;
            var dmsg = new Message();
            dmsg.Url = msg.Url;
            dmsg.RequestMethod = msg.Method;
            dmsg.RequestHeaders = msg.RequestHeaders ?? new Dictionary<string, List<string>>();
            dmsg.ResponseHeaders = msg.ResponseHeaders ?? new Dictionary<string, List<string>>();
            dmsg.Cookies = msg.Cookie;
            dmsg.File = FileHelper.SanitizeFileName(msg.File)!;
            dmsg.TabUrl = msg.TabUrl;
            dmsg.TabId = msg.TabId;
            RemoveBlockedHeaders(dmsg);
            ApplicationContext.CoreService.AddDownload(dmsg);
        }

        private void OnMediaMessage(byte[] body)
        {
            var msg = JsonConvert.DeserializeObject<ExtensionData>(Encoding.UTF8.GetString(body));
            if (msg == null) return;
            var dmsg = new Message();
            dmsg.Url = msg.Url;
            dmsg.RequestMethod = msg.Method;
            dmsg.RequestHeaders = msg.RequestHeaders ?? new Dictionary<string, List<string>>();
            dmsg.ResponseHeaders = msg.ResponseHeaders ?? new Dictionary<string, List<string>>();
            dmsg.Cookies = msg.Cookie;
            dmsg.File = FileHelper.SanitizeFileName(msg.File)!;
            dmsg.TabUrl = msg.TabUrl;
            dmsg.TabId = msg.TabId;
            RemoveBlockedHeaders(dmsg);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    VideoUrlHelper.ProcessMediaMessage(dmsg);
                }
                catch (Exception ex)
                {
                    Log.Debug("ProcessMediaMessage background error: " + ex.Message);
                }
            });
        }

        private void OnTabUpdateMessage(byte[] body)
        {
            var msg = JsonConvert.DeserializeObject<ExtensionData>(Encoding.UTF8.GetString(body));
            if (msg == null) return;
            if (msg.TabId != null)
            {
                VideoUrlHelper.ClearTabState(msg.TabId);
            }
            ApplicationContext.VideoTracker.UpdateMediaTitle(msg.TabUrl, msg.TabTitle);
            if (msg.TabUrl != null && msg.TabId != null)
            {
                VideoUrlHelper.ProcessMediaTab(msg.TabUrl, msg.TabId);
            }
        }

        private void OnVideoDownloadMessage(byte[] body)
        {
            var msg = JsonConvert.DeserializeObject<ExtensionData>(Encoding.UTF8.GetString(body));
            if (msg == null) return;
            ApplicationContext.VideoTracker.AddVideoDownload(msg.Vid);
        }

        private void OnBatchMessage(byte[] body)
        {
            var msgArr = JsonConvert.DeserializeObject<ExtensionData[]>(Encoding.UTF8.GetString(body));
            if (msgArr == null) return;
            ApplicationContext.CoreService.AddBatchLinks(msgArr.Select(msg =>
            {
                var dmsg = new Message();
                dmsg.Url = msg.Url;
                dmsg.RequestMethod = msg.Method;
                dmsg.RequestHeaders = msg.RequestHeaders;
                dmsg.ResponseHeaders = msg.ResponseHeaders;
                dmsg.Cookies = msg.Cookie;
                dmsg.File = FileHelper.SanitizeFileName(msg.File)!;
                dmsg.TabUrl = msg.TabUrl;
                dmsg.TabId = msg.TabId;
                RemoveBlockedHeaders(dmsg);
                return dmsg;
            }).ToList());
        }

        // Phase6: push the current config to all connected WebSocket clients (call after
        // video list changes, config changes, etc.).
        public void BroadcastConfig()
        {
            var json = CreateConfigJson();
            if (json == null) return;
            lock (wsLock)
            {
                for (int i = wsSessions.Count - 1; i >= 0; i--)
                {
                    var s = wsSessions[i];
                    if (s.IsConnected) s.Send(json);
                    else { wsSessions.RemoveAt(i); s.Dispose(); }
                }
            }
        }

        // Envelope for WebSocket messages: { "path": "/download", "body": "..." }
        private class WsEnvelope
        {
            [JsonProperty("path")]
            public string? Path { get; set; }
            [JsonProperty("body")]
            public string? Body { get; set; }
        }
    }
}
