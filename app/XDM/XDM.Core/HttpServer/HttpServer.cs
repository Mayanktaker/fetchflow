// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using TraceLog;

namespace XDM.Core.HttpServer
{
    public class NanoServer
    {
        private readonly TcpListener listener;
        private const int AcceptPollDelayMs = 200;
        private const int AcceptRetryDelayMs = 1000;
        public event EventHandler<RequestContextEventArgs>? RequestReceived;
        // Phase6: fired when a client requests a WebSocket upgrade (path, headers, session)
        public event Action<string, Dictionary<string, List<string>>, WebSocketSession>? WebSocketAccepted;

        public NanoServer(int port) : this(IPAddress.Any, port) { }

        public NanoServer(IPAddress host, int port)
        {
            this.listener = new TcpListener(host, port);
            try
            {
                this.listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }
            catch { }
        }

        // Actual bound endpoint (OS-assigned port when constructed with port 0)
        public IPEndPoint? LocalEndpoint => listener.LocalEndpoint as IPEndPoint;

        // Starts listening and serves the accept loop until Stop(); accept/dispatch
        // failures are contained so a hostile client can never silently kill the
        // listener and leave a live process without IPC. Uses a polling accept because
        // close() does not wake a blocked accept on Linux — a closed listener must be
        // detected so the relay supervisor can rebind.
        public void Start()
        {
            try
            {
                this.listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }
            catch { }
            listener.Start();
            Log.Debug($"NanoServer listening on {listener.LocalEndpoint}");
            while (true)
            {
                TcpClient tcp;
                try
                {
                    if (!listener.Pending())
                    {
                        Thread.Sleep(AcceptPollDelayMs);
                        continue;
                    }
                    tcp = listener.AcceptTcpClient();
                }
                catch (ObjectDisposedException)
                {
                    break; // listener stopped via Stop()
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "NanoServer accept error: " + ex.Message);
                    if (!IsListenerAlive()) break;
                    Thread.Sleep(AcceptRetryDelayMs);
                    continue;
                }
                try
                {
                    ProcessRequest(tcp);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "NanoServer dispatch error: " + ex.Message);
                    try { tcp.Close(); } catch { }
                }
            }
            Log.Debug("NanoServer accept loop exited.");
        }

        // True while the listening socket is still bound to its port
        private bool IsListenerAlive()
        {
            try { return listener.Server.IsBound; }
            catch { return false; }
        }

        public void Stop()
        {
            try
            {
                this.listener.Stop();
            }
            catch { }
        }

        private void ProcessRequest(TcpClient tcp)
        {
            new Thread(() =>
            {
                // Ownership transfer flag: once a WebSocket upgrade succeeds, the
                // session owns the TcpClient — the finally block must NOT close it.
                // (Closing it here killed every accepted WS session ~1ms after the
                // 101 handshake, which forced both extensions into HTTP polling.)
                var connectionHandedOff = false;
                try
                {
                    while (true)
                    {
                        var ctx = HttpParser.ParseContext(tcp);

                        // Phase6: detect WebSocket upgrade — if Upgrade: websocket, perform
                        // handshake and fire WebSocketAccepted instead of the normal HTTP path.
                        if (IsWebSocketUpgrade(ctx.RequestHeaders))
                        {
                            var session = WebSocketSession.Accept(tcp, ctx.RequestHeaders);
                            if (session != null)
                            {
                                connectionHandedOff = true;
                                Log.Debug("WebSocket upgrade accepted on " + ctx.RequestPath);
                                WebSocketAccepted?.Invoke(ctx.RequestPath, ctx.RequestHeaders, session);
                            }
                            return; // TcpClient lifetime now belongs to the WebSocketSession
                        }

                        this.RequestReceived?.Invoke(this, new RequestContextEventArgs(ctx));
                        if (!ctx.KeepAlive)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Non-FetchFlow clients occasionally probe the IPC port with binary
                    // garbage (TLS handshakes etc.) — log the first few, then only every
                    // 100th, so the diagnostic log is not dominated by connection noise.
                    var n = Interlocked.Increment(ref parseNoiseCount);
                    if (n <= 3 || n % 100 == 0)
                    {
                        Log.Debug(ex, $"request parse error #{n}: {ex.Message}");
                    }
                }
                finally
                {
                    if (!connectionHandedOff)
                    {
                        try { tcp.Close(); } catch { }
                    }
                }
            }).Start();
        }

        private static int parseNoiseCount;

        private static bool IsWebSocketUpgrade(Dictionary<string, List<string>> headers)
        {
            if (!headers.TryGetValue("Upgrade", out var values) || values.Count == 0)
                return false;
            return values[0].Equals("websocket", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
