// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using XDM.Core.HttpServer;

namespace XDM.Tests
{
    // Regression tests: the IPC listener must survive hostile/garbage connections —
    // a dead listener inside a live process is what turns the single-instance mutex
    // into a permanent lockout (all ports refused while the mutex stays held)
    [TestClass]
    public class NanoServerResilienceTests
    {
        [TestMethod]
        public void AcceptLoopSurvivesGarbageConnections()
        {
            var server = new NanoServer(IPAddress.Loopback, 0);
            server.RequestReceived += (s, e) =>
            {
                e.RequestContext.ResponseBody = Encoding.UTF8.GetBytes("ok");
                e.RequestContext.SendResponse();
            };
            var serverThread = new Thread(server.Start) { IsBackground = true };
            serverThread.Start();

            var port = WaitForBoundPort(server);
            Assert.IsTrue(port > 0, "server never bound a port");

            try
            {
                // TLS ClientHello-style garbage (seen in production logs hitting the IPC port)
                SendGarbageAndClose(port, new byte[] { 0x16, 0x03, 0x01, 0x00, 0x7f, 0xab, 0xcd, 0xef });
                // Binary junk without any newline
                SendGarbageAndClose(port, new byte[] { 0xff, 0xfe, 0x00, 0x01, 0x02, 0x7f, 0x80 });
                // Garbage status line followed by abrupt disconnect
                SendGarbageAndClose(port, Encoding.ASCII.GetBytes("NOT-HTTP JUNK\r\n\r\n"));

                // The listener must still serve normal requests after the abuse
                var first = SendRawRequest(port, "POST /sync HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                StringAssert.Contains(first, "200");
                StringAssert.Contains(first, "ok");

                var second = SendRawRequest(port, "POST /args HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: 2\r\nConnection: close\r\n\r\n[]");
                StringAssert.Contains(second, "200");
                StringAssert.Contains(second, "ok");
            }
            finally
            {
                server.Stop();
            }
        }

        [TestMethod]
        public void StopEndsAcceptLoopAndRefusesFurtherConnections()
        {
            var server = new NanoServer(IPAddress.Loopback, 0);
            server.RequestReceived += (s, e) =>
            {
                e.RequestContext.ResponseBody = Encoding.UTF8.GetBytes("ok");
                e.RequestContext.SendResponse();
            };
            var serverThread = new Thread(server.Start) { IsBackground = true };
            serverThread.Start();

            var port = WaitForBoundPort(server);
            Assert.IsTrue(port > 0, "server never bound a port");

            try
            {
                server.Stop();
                var exited = serverThread.Join(TimeSpan.FromSeconds(5));
                Assert.IsTrue(exited, "accept loop did not exit after Stop()");

                try
                {
                    using var client = new TcpClient();
                    client.Connect(IPAddress.Loopback, port);
                    Assert.Fail("connection unexpectedly accepted after Stop()");
                }
                catch (SocketException)
                {
                    // expected: nothing listens anymore
                }
            }
            finally
            {
                server.Stop();
            }
        }

        // Polls until the OS-assigned port is visible (bind happens on the server thread)
        private static int WaitForBoundPort(NanoServer server)
        {
            for (int i = 0; i < 60; i++)
            {
                try
                {
                    var ep = server.LocalEndpoint;
                    if (ep != null && ep.Port > 0) return ep.Port;
                }
                catch (Exception)
                {
                    // LocalEndpoint is unavailable until the listener starts
                }
                Thread.Sleep(50);
            }
            return 0;
        }

        // Writes hostile bytes and closes the socket without reading any response
        private static void SendGarbageAndClose(int port, byte[] payload)
        {
            try
            {
                using var client = new TcpClient();
                client.Connect(IPAddress.Loopback, port);
                var stream = client.GetStream();
                stream.Write(payload, 0, payload.Length);
                stream.Flush();
                Thread.Sleep(50);
                client.Close();
            }
            catch (Exception)
            {
                // Garbage clients must never fail the test itself
            }
        }

        // Sends one raw HTTP request and reads whatever comes back
        private static string SendRawRequest(int port, string raw)
        {
            using var client = new TcpClient();
            client.Connect(IPAddress.Loopback, port);
            client.ReceiveTimeout = 5000;
            var stream = client.GetStream();
            var bytes = Encoding.UTF8.GetBytes(raw);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
            var sb = new StringBuilder();
            var buf = new byte[1024];
            int read;
            while ((read = stream.Read(buf, 0, buf.Length)) > 0)
            {
                sb.Append(Encoding.UTF8.GetString(buf, 0, read));
                if (sb.ToString().Contains("ok")) break; // full body received
            }
            return sb.ToString();
        }
    }
}
