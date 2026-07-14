using System;
using System.Security.Cryptography;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        string key = "dGhlIHNhbXBsZSBub25jZQ==";
        string WsGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        using var sha1 = SHA1.Create();
        var acceptKey = Convert.ToBase64String(sha1.ComputeHash(Encoding.UTF8.GetBytes(key.Trim() + WsGuid)));
        Console.WriteLine($"Expect: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=");
        Console.WriteLine($"Actual: {acceptKey}");
    }
}
