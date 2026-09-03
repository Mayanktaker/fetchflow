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

#if NET5_0_OR_GREATER
            var sha256Hex = Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant();
            var md5Hex = Convert.ToHexString(md5.GetHashAndReset()).ToLowerInvariant();
            var sha1Hex = Convert.ToHexString(sha1.GetHashAndReset()).ToLowerInvariant();
            var sha512Hex = Convert.ToHexString(sha512.GetHashAndReset()).ToLowerInvariant();
#else
            var sha256Hex = BitConverter.ToString(sha256.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
            var md5Hex = BitConverter.ToString(md5.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
            var sha1Hex = BitConverter.ToString(sha1.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
            var sha512Hex = BitConverter.ToString(sha512.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
#endif

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

        // Extracts a cryptographic hash from a checksum manifest file (.sha256, .md5, SHA256SUMS, etc.)
        public static bool TryExtractHashFromChecksumFile(string checksumFilePath, string? targetFileName, out string extractedHash)
        {
            extractedHash = string.Empty;
            if (!File.Exists(checksumFilePath)) return false;

            try
            {
                var lines = File.ReadAllLines(checksumFilePath);
                string? firstCandidate = null;

                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    // BSD format: SHA256 (filename.iso) = <hash>
                    if (line.Contains("=") && line.Contains("(") && line.Contains(")"))
                    {
                        var eqIdx = line.LastIndexOf('=');
                        if (eqIdx >= 0 && eqIdx < line.Length - 1)
                        {
                            var hashPart = line.Substring(eqIdx + 1).Trim();
                            if (IsProbableHash(hashPart, out var h))
                            {
                                if (!string.IsNullOrEmpty(targetFileName) && line.IndexOf(targetFileName, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    extractedHash = h;
                                    return true;
                                }
                                firstCandidate ??= h;
                            }
                        }
                    }

                    // GNU format: <hash>  <filename> or bare hash
                    var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length > 0 && IsProbableHash(tokens[0], out var gnuHash))
                    {
                        if (tokens.Length > 1 && !string.IsNullOrEmpty(targetFileName))
                        {
                            var nameToken = tokens[tokens.Length - 1].TrimStart('*');
                            if (nameToken.Equals(targetFileName, StringComparison.OrdinalIgnoreCase) ||
                                nameToken.EndsWith("/" + targetFileName, StringComparison.OrdinalIgnoreCase) ||
                                nameToken.EndsWith("\\" + targetFileName, StringComparison.OrdinalIgnoreCase))
                            {
                                extractedHash = gnuHash;
                                return true;
                            }
                        }
                        firstCandidate ??= gnuHash;
                    }
                }

                if (!string.IsNullOrEmpty(firstCandidate))
                {
                    extractedHash = firstCandidate;
                    return true;
                }
            }
            catch
            {
                // Ignore file read exceptions
            }

            return false;
        }
    }
}
