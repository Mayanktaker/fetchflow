// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Minimal RFC 6455 WebSocket implementation for the XDM IPC loopback server.
// Handles: handshake (SHA-1 accept-key), masked/unmasked text frames, ping/pong,
// close. No fragmentation (not needed for IPC messages < 64 KB).
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using TraceLog;

namespace XDM.Core.HttpServer
{
    public class WebSocketSession : IDisposable
    {
        private const string WsGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private readonly TcpClient tcp;
        private readonly NetworkStream stream;
        private bool disposed;

        public event Action<WebSocketSession, string>? OnMessage;
        public event Action<WebSocketSession>? OnClosed;
        public bool IsConnected => tcp.Connected && !disposed;

        private WebSocketSession(TcpClient tcp)
        {
            this.tcp = tcp;
            this.stream = tcp.GetStream();
        }

        /// <summary>Perform the RFC 6455 handshake on the given TcpClient. Returns null on failure.</summary>
        public static WebSocketSession? Accept(TcpClient tcp, Dictionary<string, List<string>> headers)
        {
            if (!headers.TryGetValue("Sec-WebSocket-Key", out var keys) || keys.Count == 0)
            {
                Log.Debug("WebSocket handshake: missing Sec-WebSocket-Key");
                return null;
            }
            var key = keys[0];
            using var sha1 = SHA1.Create();
            var acceptKey = Convert.ToBase64String(
                sha1.ComputeHash(Encoding.UTF8.GetBytes(key.Trim() + WsGuid)));

            var sb = new StringBuilder();
            sb.Append("HTTP/1.1 101 Switching Protocols\r\n");
            sb.Append("Upgrade: websocket\r\n");
            sb.Append("Connection: Upgrade\r\n");
            sb.Append("Sec-WebSocket-Accept: ").Append(acceptKey).Append("\r\n");
            sb.Append("\r\n");

            var respBytes = Encoding.UTF8.GetBytes(sb.ToString());
            tcp.GetStream().Write(respBytes, 0, respBytes.Length);
            tcp.GetStream().Flush();

            return new WebSocketSession(tcp);
        }

        /// <summary>Read the next text message from the WebSocket. Returns null on close/error.</summary>
        public string? ReadMessage()
        {
            try
            {
                while (true)
                {
                    var first = stream.ReadByte();
                    if (first < 0) return null;
                    int opcode = first & 0x0F;

                    // Close frame
                    if (opcode == 0x08) { SendClose(); return null; }
                    // Ping → auto pong
                    if (opcode == 0x09) { ReadFrame(); SendPong(); continue; }
                    // Pong → ignore
                    if (opcode == 0x0A) { ReadFrame(); continue; }
                    // Text frame (opcode 0x01)
                    if (opcode == 0x01) return ReadFrame();
                    // Unknown opcode → skip frame
                    ReadFrame();
                }
            }
            catch (Exception ex)
            {
                Log.Debug("WebSocket read error: " + ex.Message);
                return null;
            }
        }

        /// <summary>Send a text message (unmasked — server to client).</summary>
        public void Send(string message)
        {
            if (disposed) return;
            try
            {
                var payload = Encoding.UTF8.GetBytes(message);
                var header = BuildFrame(0x01, payload);
                stream.Write(header, 0, header.Length);
                stream.Write(payload, 0, payload.Length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                Log.Debug("WebSocket send error: " + ex.Message);
                Close();
            }
        }

        /// <summary>Send a close frame and dispose.</summary>
        public void Close()
        {
            if (disposed) return;
            try { SendClose(); } catch { }
            OnClosed?.Invoke(this);
            Dispose();
        }

        // Read a WebSocket frame, return the payload as a string. Handles masking.
        private string ReadFrame()
        {
            int b1 = stream.ReadByte(); // second byte: mask bit + payload length
            bool masked = (b1 & 0x80) != 0;
            long payloadLen = b1 & 0x7F;

            if (payloadLen == 126)
            {
                var lenBuf = new byte[2];
                ReadExact(stream, lenBuf, 2);
                payloadLen = (lenBuf[0] << 8) | lenBuf[1];
            }
            else if (payloadLen == 127)
            {
                var lenBuf = new byte[8];
                ReadExact(stream, lenBuf, 8);
                payloadLen = 0;
                for (int i = 0; i < 8; i++)
                    payloadLen = (payloadLen << 8) | lenBuf[i];
            }

            byte[]? maskKey = null;
            if (masked)
            {
                maskKey = new byte[4];
                ReadExact(stream, maskKey, 4);
            }

            var payload = new byte[payloadLen];
            ReadExact(stream, payload, (int)payloadLen);

            if (masked && maskKey != null)
            {
                for (int i = 0; i < payload.Length; i++)
                    payload[i] ^= maskKey[i % 4];
            }

            return Encoding.UTF8.GetString(payload);
        }

        private void SendClose()
        {
            var frame = BuildFrame(0x08, Array.Empty<byte>());
            stream.Write(frame, 0, frame.Length);
            stream.Flush();
        }

        private void SendPong()
        {
            var frame = BuildFrame(0x0A, Array.Empty<byte>());
            stream.Write(frame, 0, frame.Length);
            stream.Flush();
        }

        // Build a WebSocket frame (server → client = unmasked).
        private static byte[] BuildFrame(int opcode, byte[] payload)
        {
            using var ms = new MemoryStream();
            ms.WriteByte((byte)(0x80 | opcode)); // FIN + opcode
            if (payload.Length < 126)
            {
                ms.WriteByte((byte)payload.Length);
            }
            else if (payload.Length <= 0xFFFF)
            {
                ms.WriteByte(126);
                ms.WriteByte((byte)(payload.Length >> 8));
                ms.WriteByte((byte)(payload.Length & 0xFF));
            }
            else
            {
                ms.WriteByte(127);
                long len = payload.Length;
                for (int i = 7; i >= 0; i--)
                    ms.WriteByte((byte)((len >> (8 * i)) & 0xFF));
            }
            return ms.ToArray();
        }

        private static void ReadExact(Stream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read <= 0) throw new IOException("Connection closed during WebSocket read");
                offset += read;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { stream.Close(); } catch { }
            try { tcp.Close(); } catch { }
        }
    }
}
