using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XDM.Core;

namespace XDM.Core.Clients.Http
{
    public static class HttpClientFactory
    {
        public static IHttpClient NewHttpClient(ProxyInfo? proxyInfo)
        {
            ProxyInfo? proxy = null;
            if (proxyInfo.HasValue)
            {
                if (proxyInfo.Value.ProxyType != ProxyType.Custom)
                {
                    proxy = proxyInfo;
                }
                else if (!string.IsNullOrEmpty(proxyInfo.Value.Host) && proxyInfo.Value.Port > 0)
                {
                    proxy = proxyInfo;
                }
            }

            // Phase3.4: on .NET 5+ the fully-managed DotNetHttpClient is used on every OS
            // (replaces the old WinHttp/CLR-version heuristic). The legacy WinHttpClient /
            // NetFxHttpClient paths are retained for the Windows net4.x builds only.
#if NET5_0_OR_GREATER
            return new DotNetHttpClient(proxy);
#else
            return new NetFxHttpClient(proxy);
#endif
        }
    }
}
