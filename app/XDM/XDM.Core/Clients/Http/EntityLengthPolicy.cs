// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;

namespace XDM.Core.Clients.Http
{
    // Decides which length on a response describes what, since a range carries two of them
    public static class EntityLengthPolicy
    {
        // Bytes this response carries: the slice for a ranged request, else the whole body
        public static long ResolveBodyLength(long contentLength, long contentRangeTotal, bool compressed)
        {
            if (compressed) return -1;
            if (contentLength > 0) return contentLength;
            return contentRangeTotal > 0 ? contentRangeTotal : -1;
        }

        // Size of the whole file, for plans and for the dialog. A compressed body length is the
        // encoded entity's, not the file's, so it is never reported as the size
        public static long ResolveFileSize(long contentRangeTotal, long contentLength, bool compressed)
        {
            if (compressed) return -1;
            if (contentRangeTotal > 0) return contentRangeTotal;
            return contentLength > 0 ? contentLength : -1;
        }
    }
}
