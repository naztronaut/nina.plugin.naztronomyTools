#region "copyright"

/*
    Copyright (c) 2026 Naztronomy

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Naztronomy.NINA.Tools.LogMonitor {

    /// <summary>
    /// Severity of a parsed NINA log line, ordered from least to most severe.
    /// </summary>
    public enum LogLevel {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    /// <summary>
    /// A single parsed log line that a monitor considers worth reporting.
    /// </summary>
    public class LogIssue {
        public LogLevel Level { get; set; }
        public string Source { get; set; }
        public string Member { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Locates NINA's active log file and tails it, returning only complete, newly-appended lines.
    /// NINA writes pipe-delimited lines: <c>timestamp|LEVEL|source|member|line|message</c>.
    /// The reader keeps its own byte offset so it can be polled repeatedly to pick up new content.
    /// </summary>
    public static class NinaLogReader {

        private static readonly Dictionary<string, LogLevel> Levels = new Dictionary<string, LogLevel>(StringComparer.OrdinalIgnoreCase) {
            ["TRACE"] = LogLevel.Trace,
            ["DEBUG"] = LogLevel.Debug,
            ["INFO"] = LogLevel.Info,
            ["WARNING"] = LogLevel.Warning,
            ["ERROR"] = LogLevel.Error,
            ["FATAL"] = LogLevel.Fatal,
        };

        /// <summary>
        /// Returns the full path of the most recently written <c>*.log</c> file in NINA's Logs
        /// folder, or null when the folder or a log file cannot be found.
        /// </summary>
        public static string FindActiveLogFile() {
            var logsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NINA", "Logs");

            if (!Directory.Exists(logsDir)) {
                return null;
            }

            FileInfo latest = null;
            foreach (var file in new DirectoryInfo(logsDir).GetFiles("*.log")) {
                if (latest == null || file.LastWriteTimeUtc > latest.LastWriteTimeUtc) {
                    latest = file;
                }
            }

            return latest?.FullName;
        }

        /// <summary>
        /// Reads bytes appended to <paramref name="path"/> since <paramref name="offset"/> and returns
        /// the complete text lines found. A trailing partial line (one still being written) is left for
        /// the next call. <paramref name="offset"/> is advanced past the consumed, complete lines.
        /// </summary>
        public static IReadOnlyList<string> ReadNewLines(string path, ref long offset) {
            var result = new List<string>();

            if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
                return result;
            }

            long length;
            try {
                length = new FileInfo(path).Length;
            } catch {
                return result;
            }

            // The file was rotated or truncated: start over from the beginning.
            if (length < offset) {
                offset = 0;
            }

            if (length == offset) {
                return result;
            }

            var count = (int)Math.Min(length - offset, int.MaxValue);
            var buffer = new byte[count];
            int read;

            try {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fs.Seek(offset, SeekOrigin.Begin);
                int total = 0;
                int n;
                while (total < count && (n = fs.Read(buffer, total, count - total)) > 0) {
                    total += n;
                }
                read = total;
            } catch {
                return result;
            }

            var text = Encoding.UTF8.GetString(buffer, 0, read);

            var lastNewline = text.LastIndexOf('\n');
            if (lastNewline < 0) {
                // No complete line yet; keep the offset where it was so we re-read next time.
                return result;
            }

            var complete = text.Substring(0, lastNewline + 1);
            offset += Encoding.UTF8.GetByteCount(complete);

            foreach (var line in complete.Split('\n')) {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.Length > 0) {
                    result.Add(trimmed);
                }
            }

            return result;
        }

        /// <summary>
        /// Parses a single NINA log line. Returns false for lines that are not in the expected
        /// pipe-delimited format (for example continuation lines of a stack trace).
        /// </summary>
        public static bool TryParse(string line, out LogIssue issue) {
            issue = null;

            if (string.IsNullOrWhiteSpace(line)) {
                return false;
            }

            var parts = line.Split(new[] { '|' }, 6);
            if (parts.Length != 6) {
                return false;
            }

            if (!Levels.TryGetValue(parts[1].Trim(), out var level)) {
                return false;
            }

            issue = new LogIssue {
                Level = level,
                Source = parts[2].Trim(),
                Member = parts[3].Trim(),
                Message = parts[5].Trim(),
            };
            return true;
        }
    }
}
