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

        // Starts listening for incoming HTTP and WebSocket connections
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
                var tcp = listener.AcceptTcpClient();
                ProcessRequest(tcp);
            }
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
                                Log.Debug("WebSocket upgrade accepted on " + ctx.RequestPath);
                                WebSocketAccepted?.Invoke(ctx.RequestPath, ctx.RequestHeaders, session);
                            }
                            return; // hand off TcpClient lifetime to the WebSocketSession
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
                    Log.Debug(ex, ex.Message);
                }
                finally
                {
                    try { tcp.Close(); } catch { }
                }
            }).Start();
        }

        private static bool IsWebSocketUpgrade(Dictionary<string, List<string>> headers)
        {
            if (!headers.TryGetValue("Upgrade", out var values) || values.Count == 0)
                return false;
            return values[0].Equals("websocket", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
