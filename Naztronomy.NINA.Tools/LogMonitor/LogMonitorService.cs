#region "copyright"

/*
    Copyright (c) 2026 Naztronomy

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using Naztronomy.NINA.Tools.Discord;
using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Naztronomy.NINA.Tools.LogMonitor {

    /// <summary>
    /// Polls NINA's latest log file in the background and posts a Discord alert when it finds lines
    /// that match the rules configured on the Logs options tab. Runs independently of sequences and
    /// is started/stopped by the <c>LogMonitorEnabled</c> plugin setting.
    /// </summary>
    public class LogMonitorService : IDisposable {

        private const int PollIntervalMs = 5000;

        // Discord caps an embed description at 4096 characters; stay comfortably under it.
        private const int MaxDescriptionLength = 3900;
        private const int MaxIssuesPerAlert = 25;
        private const int MaxPendingIssues = 500;

        private readonly NaztronomyToolsPlugin settings;
        private readonly List<LogIssue> pending = new List<LogIssue>();

        private Timer timer;
        private string activeLogPath;
        private long readOffset;
        private int busy;

        public LogMonitorService(NaztronomyToolsPlugin settings) {
            this.settings = settings;
        }

        public bool IsRunning => timer != null;

        public void Start() {
            if (timer != null) {
                return;
            }

            // Begin at the end of the current log so only issues from now on are reported.
            activeLogPath = NinaLogReader.FindActiveLogFile();
            readOffset = 0;
            if (activeLogPath != null) {
                try {
                    readOffset = new FileInfo(activeLogPath).Length;
                } catch {
                    readOffset = 0;
                }
            }
            pending.Clear();

            timer = new Timer(PollIntervalMs) { AutoReset = true };
            timer.Elapsed += OnElapsed;
            timer.Start();

            Logger.Info("Naztronomy Tools: log monitor started");
        }

        public void Stop() {
            if (timer == null) {
                return;
            }

            timer.Stop();
            timer.Elapsed -= OnElapsed;
            timer.Dispose();
            timer = null;

            Logger.Info("Naztronomy Tools: log monitor stopped");
        }

        private async void OnElapsed(object sender, ElapsedEventArgs e) {
            // Skip this tick if the previous one is still running.
            if (Interlocked.Exchange(ref busy, 1) == 1) {
                return;
            }

            try {
                await PollAsync();
            } catch (Exception ex) {
                Logger.Warning($"Naztronomy Tools: log monitor poll failed: {ex.Message}");
            } finally {
                Interlocked.Exchange(ref busy, 0);
            }
        }

        private async Task PollAsync() {
            CollectIssues();

            if (pending.Count == 0) {
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.DiscordWebhookUrl)
                || !Uri.IsWellFormedUriString(settings.DiscordWebhookUrl, UriKind.Absolute)) {
                // Keep the issues queued (capped) until a valid webhook is configured.
                return;
            }

            var batch = pending.ToList();
            var message = BuildAlertMessage(batch);

            Logger.Info($"Naztronomy Tools: posting log alert to Discord ({batch.Count} issue(s))");

            await DiscordWebhookSender.SendAsync(message, CancellationToken.None);

            // Only clear after a successful send so transient failures don't drop alerts.
            pending.Clear();
        }

        private void CollectIssues() {
            var path = NinaLogReader.FindActiveLogFile();
            if (path == null) {
                return;
            }

            if (!string.Equals(path, activeLogPath, StringComparison.OrdinalIgnoreCase)) {
                activeLogPath = path;
                readOffset = 0;
            }

            var keywords = settings.GetLogMonitorKeywords();

            foreach (var line in NinaLogReader.ReadNewLines(path, ref readOffset)) {
                if (NinaLogReader.TryParse(line, out var issue) && ShouldReport(issue, keywords)) {
                    pending.Add(issue);
                }
            }

            if (pending.Count > MaxPendingIssues) {
                pending.RemoveRange(0, pending.Count - MaxPendingIssues);
            }
        }

        private bool ShouldReport(LogIssue issue, IReadOnlyList<string> keywords) {
            if (settings.LogMonitorNotifyOnErrors && (issue.Level == LogLevel.Error || issue.Level == LogLevel.Fatal)) {
                return true;
            }

            if (settings.LogMonitorNotifyOnWarnings && issue.Level == LogLevel.Warning) {
                return true;
            }

            if (keywords.Count > 0) {
                var haystack = $"{issue.Source} {issue.Member} {issue.Message}";
                foreach (var keyword in keywords) {
                    if (haystack.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) {
                        return true;
                    }
                }
            }

            return false;
        }

        private DiscordWebhookMessage BuildAlertMessage(IReadOnlyList<LogIssue> issues) {
            var hasFatalOrError = issues.Any(i => i.Level == LogLevel.Error || i.Level == LogLevel.Fatal);

            var sb = new StringBuilder();
            var shown = 0;
            foreach (var issue in issues) {
                var context = string.IsNullOrEmpty(issue.Member) ? issue.Source : $"{issue.Source}.{issue.Member}";
                var line = $"`{issue.Level.ToString().ToUpperInvariant()}` {context}: {issue.Message}";

                if (sb.Length + line.Length + 1 > MaxDescriptionLength || shown >= MaxIssuesPerAlert) {
                    sb.AppendLine($"…and {issues.Count - shown} more.");
                    break;
                }

                sb.AppendLine(line);
                shown++;
            }

            var defaultTitle = hasFatalOrError ? "\U0001f6a8 NINA Log Alert" : "\u26a0\ufe0f NINA Log Warning";
            var fileName = activeLogPath != null ? Path.GetFileName(activeLogPath) : string.Empty;

            return new DiscordWebhookMessage {
                WebhookUrl = settings.DiscordWebhookUrl,
                Username = settings.LogMonitorUsername,
                EmbedTitle = string.IsNullOrWhiteSpace(settings.LogMonitorEmbedTitle) ? defaultTitle : settings.LogMonitorEmbedTitle,
                EmbedDescription = sb.ToString().TrimEnd(),
                EmbedFooter = string.IsNullOrWhiteSpace(settings.DiscordEmbedFooter) ? fileName : settings.DiscordEmbedFooter,
                EmbedColor = hasFatalOrError ? 0xE74C3C : 0xF39C12,
                IncludeTimestamp = true,
            };
        }

        public void Dispose() {
            Stop();
        }
    }
}
