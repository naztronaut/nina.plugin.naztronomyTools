#region "copyright"

/*
    Copyright (c) 2026 Naztronomy

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using Naztronomy.NINA.Tools.Discord;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Naztronomy.NINA.Tools.LogMonitor {

    [ExportMetadata("Name", "Discord - Log Monitor")]
    [ExportMetadata("Description", "Watches NINA's active log file while the sequence runs and posts a Discord alert when it detects error (and optionally warning) entries.")]
    [ExportMetadata("Icon", "NaztronomyToolsSVG")]
    [ExportMetadata("Category", "Naztronomy Tools")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class LogMonitorTrigger : SequenceTrigger, IValidatable {

        // Discord caps an embed description at 4096 characters; stay comfortably under it.
        private const int MaxDescriptionLength = 3900;
        private const int MaxIssuesPerAlert = 25;

        private bool includeWarnings;

        private string activeLogPath;
        private long readOffset;
        private readonly List<LogIssue> pending = new List<LogIssue>();

        private int alertsSent;
        private string watchedFileName = string.Empty;

        [ImportingConstructor]
        public LogMonitorTrigger() {
            Validate();
        }

        private LogMonitorTrigger(LogMonitorTrigger copyMe) : this() {
            CopyMetaData(copyMe);
        }

        [JsonProperty]
        public bool IncludeWarnings {
            get => includeWarnings;
            set {
                includeWarnings = value;
                RaisePropertyChanged();
            }
        }

        public int AlertsSent {
            get => alertsSent;
            private set {
                alertsSent = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(StatusText));
            }
        }

        public string WatchedFileName {
            get => watchedFileName;
            private set {
                watchedFileName = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(StatusText));
            }
        }

        public string StatusText =>
            string.IsNullOrEmpty(WatchedFileName)
                ? "Waiting for active log file…"
                : $"Watching {WatchedFileName} · {AlertsSent} alert(s) sent";

        public override void SequenceBlockInitialize() {
            // Start reading from the current end of the active log so only issues that occur from
            // now on are reported, not earlier entries from this NINA session.
            activeLogPath = NinaLogReader.FindActiveLogFile();
            readOffset = 0;
            if (activeLogPath != null) {
                try {
                    readOffset = new FileInfo(activeLogPath).Length;
                } catch {
                    readOffset = 0;
                }
                WatchedFileName = Path.GetFileName(activeLogPath);
            }
            pending.Clear();
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (NaztronomyToolsPlugin.Instance == null) {
                return false;
            }

            ScanForIssues();
            return pending.Count > 0;
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (pending.Count == 0) {
                return;
            }

            if (!Validate()) {
                Logger.Error($"{Name}: not sending Discord alert because the configuration is invalid: {string.Join(", ", Issues)}");
                pending.Clear();
                return;
            }

            var settings = NaztronomyToolsPlugin.Instance;
            var batch = pending.ToList();
            pending.Clear();

            var message = BuildAlertMessage(settings, batch);

            Logger.Info($"{Name}: posting log alert to Discord ({batch.Count} issue(s))");
            progress?.Report(new ApplicationStatus { Status = "Posting log alert to Discord" });

            await DiscordWebhookSender.SendAsync(message, token);
            AlertsSent++;
        }

        /// <summary>
        /// Reads newly-appended log lines and queues those whose severity meets the configured threshold.
        /// </summary>
        private void ScanForIssues() {
            try {
                var path = NinaLogReader.FindActiveLogFile();
                if (path == null) {
                    return;
                }

                if (!string.Equals(path, activeLogPath, StringComparison.OrdinalIgnoreCase)) {
                    activeLogPath = path;
                    readOffset = 0;
                    WatchedFileName = Path.GetFileName(path);
                }

                foreach (var line in NinaLogReader.ReadNewLines(path, ref readOffset)) {
                    if (NinaLogReader.TryParse(line, out var issue) && MeetsThreshold(issue.Level)) {
                        pending.Add(issue);
                    }
                }
            } catch (Exception ex) {
                // Never let a log read break the running sequence.
                Logger.Warning($"{Name}: failed to scan log file: {ex.Message}");
            }
        }

        private bool MeetsThreshold(LogLevel level) {
            if (level == LogLevel.Error || level == LogLevel.Fatal) {
                return true;
            }
            return IncludeWarnings && level == LogLevel.Warning;
        }

        private DiscordWebhookMessage BuildAlertMessage(NaztronomyToolsPlugin settings, IReadOnlyList<LogIssue> issues) {
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

            return new DiscordWebhookMessage {
                WebhookUrl = settings.DiscordWebhookUrl,
                Username = settings.DiscordUsername,
                EmbedTitle = hasFatalOrError ? "\U0001f6a8 NINA Log Alert" : "\u26a0\ufe0f NINA Log Warning",
                EmbedDescription = sb.ToString().TrimEnd(),
                EmbedFooter = string.IsNullOrWhiteSpace(settings.DiscordEmbedFooter) ? WatchedFileName : settings.DiscordEmbedFooter,
                EmbedColor = hasFatalOrError ? 0xE74C3C : 0xF39C12,
                IncludeTimestamp = true,
            };
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>();

            var settings = NaztronomyToolsPlugin.Instance;

            if (settings == null) {
                i.Add("Naztronomy Tools plugin is not loaded");
            } else {
                if (string.IsNullOrWhiteSpace(settings.DiscordWebhookUrl)) {
                    i.Add("Discord webhook URL is not set on the Config tab");
                } else if (!Uri.IsWellFormedUriString(settings.DiscordWebhookUrl, UriKind.Absolute)) {
                    i.Add("Discord webhook URL on the Config tab is invalid");
                }
            }

            Issues = i;
            RaisePropertyChanged(nameof(Issues));

            return i.Count == 0;
        }

        public override object Clone() {
            return new LogMonitorTrigger(this) {
                IncludeWarnings = IncludeWarnings,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}, IncludeWarnings: {IncludeWarnings}";
        }
    }
}
