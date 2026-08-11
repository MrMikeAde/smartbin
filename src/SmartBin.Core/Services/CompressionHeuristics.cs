using System;
using System.Collections.Generic;
using System.IO;

namespace SmartBin.Core.Services
{
    public static class CompressionHeuristics
    {
        private static readonly HashSet<string> AlreadyCompressedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".mp3", ".jpg", ".jpeg", ".png",
            ".zip", ".rar", ".7z", ".gz", ".tar", ".tgz",
            ".aac", ".flac", ".ogg", ".webm", ".avi", ".mov",
            ".docx", ".xlsx", ".pptx", ".pdf"
        };

        /// <summary>
        /// A heuristic to evaluate whether a file of a given path/extension is typically already compressed.
        /// Note that this is a heuristic rather than a programmatic guarantee.
        /// </summary>
        public static bool IsTypicallyCompressed(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            var ext = Path.GetExtension(filePath);
            return AlreadyCompressedExtensions.Contains(ext);
        }
    }
}
