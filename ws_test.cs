using System;
using System.Security.Cryptography;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        string key = "dGhlIHNhbXBsZSBub25jZQ==";
        string WsGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        var acceptKey = Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(key.Trim() + WsGuid)));
        Console.WriteLine($"Expect: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=");
        Console.WriteLine($"Actual: {acceptKey}");
    }
}
