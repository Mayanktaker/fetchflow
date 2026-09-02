// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace XDM.Core.Util
{
    // Holds the computed cryptographic digests for a verified file
    public readonly struct ChecksumResult
    {
        public string Sha256 { get; }
        public string Md5 { get; }
        public string Sha1 { get; }
        public string Sha512 { get; }
        public long FileSizeBytes { get; }

        public ChecksumResult(string sha256, string md5, string sha1, string sha512, long fileSizeBytes)
        {
            Sha256 = sha256;
            Md5 = md5;
            Sha1 = sha1;
            Sha512 = sha512;
            FileSizeBytes = fileSizeBytes;
        }
    }

    // Result status of matching an expected hash string against computed digests
    public enum ChecksumMatchStatus
    {
        Empty,
        Match,
        Mismatch
    }

    // Detailed result of a hash verification comparison
    public readonly struct ChecksumMatchResult
    {
        public ChecksumMatchStatus Status { get; }
        public string MatchedAlgorithm { get; }

        public ChecksumMatchResult(ChecksumMatchStatus status, string matchedAlgorithm)
        {
            Status = status;
            MatchedAlgorithm = matchedAlgorithm;
        }
    }

    public static class ChecksumHelper
    {
        private const int BufferSize = 1024 * 1024; // 1 MiB chunk streaming buffer

        // Computes SHA-256, MD5, SHA-1, and SHA-512 concurrently in a single file-read pass
        public static async Task<ChecksumResult> ComputeHashesAsync(
            string filePath,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found for checksum verification", filePath);
            }

            var fileInfo = new FileInfo(filePath);
            long totalBytes = fileInfo.Length;
            long readBytes = 0;

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
            using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            using var sha512 = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);

            var buffer = new byte[BufferSize];
            int bytesRead;

            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                sha256.AppendData(buffer, 0, bytesRead);
                md5.AppendData(buffer, 0, bytesRead);
                sha1.AppendData(buffer, 0, bytesRead);
                sha512.AppendData(buffer, 0, bytesRead);

                readBytes += bytesRead;
                if (totalBytes > 0 && progress != null)
                {
                    progress.Report(Math.Min(1.0, (double)readBytes / totalBytes));
                }
            }

            progress?.Report(1.0);

            var sha256Hex = Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant();
            var md5Hex = Convert.ToHexString(md5.GetHashAndReset()).ToLowerInvariant();
            var sha1Hex = Convert.ToHexString(sha1.GetHashAndReset()).ToLowerInvariant();
            var sha512Hex = Convert.ToHexString(sha512.GetHashAndReset()).ToLowerInvariant();

            return new ChecksumResult(sha256Hex, md5Hex, sha1Hex, sha512Hex, totalBytes);
        }

        // Compares a user-provided expected hash against computed checksum results
        public static ChecksumMatchResult CompareHash(string? expectedHash, ChecksumResult computed)
        {
            if (string.IsNullOrWhiteSpace(expectedHash))
            {
                return new ChecksumMatchResult(ChecksumMatchStatus.Empty, string.Empty);
            }

            var clean = expectedHash.Trim().ToLowerInvariant();

            if (clean.Equals(computed.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return new ChecksumMatchResult(ChecksumMatchStatus.Match, "SHA-256");
            }
            if (clean.Equals(computed.Md5, StringComparison.OrdinalIgnoreCase))
            {
                return new ChecksumMatchResult(ChecksumMatchStatus.Match, "MD5");
            }
            if (clean.Equals(computed.Sha512, StringComparison.OrdinalIgnoreCase))
            {
                return new ChecksumMatchResult(ChecksumMatchStatus.Match, "SHA-512");
            }
            if (clean.Equals(computed.Sha1, StringComparison.OrdinalIgnoreCase))
            {
                return new ChecksumMatchResult(ChecksumMatchStatus.Match, "SHA-1");
            }

            return new ChecksumMatchResult(ChecksumMatchStatus.Mismatch, string.Empty);
        }

        // Checks if an input string is likely a hexadecimal hash (MD5, SHA-1, SHA-256, SHA-512)
        public static bool IsProbableHash(string? text, out string cleanHash)
        {
            cleanHash = string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var trimmed = text.Trim();
            if (trimmed.Length != 32 && trimmed.Length != 40 && trimmed.Length != 64 && trimmed.Length != 128)
            {
                return false;
            }

            foreach (var c in trimmed)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    return false;
                }
            }

            cleanHash = trimmed;
            return true;
        }
    }
}
