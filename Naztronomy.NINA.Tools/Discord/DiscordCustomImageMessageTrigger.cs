#region "copyright"

/*
    Copyright (c) 2026 Naztronomy

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

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
using System.Threading;
using System.Threading.Tasks;

namespace Naztronomy.NINA.Tools.Discord {

    public enum IntervalUnit {
        Seconds,
        Minutes,
        Hours
    }

    [ExportMetadata("Name", "Discord - Custom Image Message")]
    [ExportMetadata("Description", "Posts the message and embedded image configured in the Naztronomy Tools plugin settings to a Discord webhook on a fixed time interval.")]
    [ExportMetadata("Icon", "NaztronomyToolsSVG")]
    [ExportMetadata("Category", "Naztronomy Tools")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class DiscordCustomImageMessageTrigger : SequenceTrigger, IValidatable, IDisposable {
        private double interval = 1;
        private IntervalUnit unit = IntervalUnit.Hours;

        private DateTime lastTriggerTime;
        private bool initialized;

        private double progressPercent;
        private string countdownText = string.Empty;
        private System.Timers.Timer countdownTimer;

        [ImportingConstructor]
        public DiscordCustomImageMessageTrigger() {
            Validate();
            UpdateCountdown();
        }

        private DiscordCustomImageMessageTrigger(DiscordCustomImageMessageTrigger copyMe) : this() {
            CopyMetaData(copyMe);
        }

        [JsonProperty]
        public double Interval {
            get => interval;
            set {
                interval = Math.Max(0, value);
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(IntervalSummary));
                UpdateCountdown();
                Validate();
            }
        }

        [JsonProperty]
        public IntervalUnit Unit {
            get => unit;
            set {
                unit = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(IntervalSummary));
                UpdateCountdown();
            }
        }

        public IntervalUnit[] IntervalUnits => (IntervalUnit[])Enum.GetValues(typeof(IntervalUnit));

        public string IntervalSummary => $"{Interval:0.##} {Unit.ToString().ToLowerInvariant()}";

        public double ProgressPercent {
            get => progressPercent;
            private set {
                progressPercent = value;
                RaisePropertyChanged();
            }
        }

        public string CountdownText {
            get => countdownText;
            private set {
                countdownText = value;
                RaisePropertyChanged();
            }
        }

        private double TotalSeconds => Unit switch {
            IntervalUnit.Seconds => Interval,
            IntervalUnit.Minutes => Interval * 60,
            IntervalUnit.Hours => Interval * 3600,
            _ => Interval
        };

        public override void SequenceBlockInitialize() {
            lastTriggerTime = DateTime.Now;
            initialized = true;
            UpdateCountdown();
        }

        public override void SequenceBlockStarted() {
            StartTimer();
        }

        public override void SequenceBlockTeardown() {
            StopTimer();
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (!Validate()) {
                Logger.Error($"{Name}: not sending Discord message because the configuration is invalid: {string.Join(", ", Issues)}");
                lastTriggerTime = DateTime.Now;
                UpdateCountdown();
                return;
            }

            var settings = NaztronomyToolsPlugin.Instance;

            Logger.Info($"{Name}: posting custom image message to Discord");
            progress?.Report(new ApplicationStatus { Status = "Posting message to Discord" });

            try {
                await DiscordWebhookSender.SendAsync(settings.BuildDiscordMessage(), token);
            } finally {
                lastTriggerTime = DateTime.Now;
                UpdateCountdown();
            }
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            // Start the countdown the first time the trigger is evaluated.
            if (!initialized) {
                lastTriggerTime = DateTime.Now;
                initialized = true;
            }

            UpdateCountdown();

            return (DateTime.Now - lastTriggerTime).TotalSeconds >= TotalSeconds;
        }

        private void StartTimer() {
            StopTimer();
            countdownTimer = new System.Timers.Timer(1000) { AutoReset = true };
            countdownTimer.Elapsed += (s, e) => UpdateCountdown();
            countdownTimer.Start();
        }

        private void StopTimer() {
            if (countdownTimer != null) {
                countdownTimer.Stop();
                countdownTimer.Dispose();
                countdownTimer = null;
            }
        }

        private void UpdateCountdown() {
            var total = TotalSeconds;
            double remaining;

            if (!initialized) {
                remaining = total;
            } else {
                var elapsed = (DateTime.Now - lastTriggerTime).TotalSeconds;
                remaining = Math.Max(0, total - elapsed);
            }

            ProgressPercent = total > 0 ? Math.Min(100, (total - remaining) / total * 100) : 0;
            CountdownText = remaining <= 0 ? "Due now" : $"Next in {TimeSpan.FromSeconds(remaining):hh\\:mm\\:ss}";
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>();

            if (TotalSeconds <= 0) {
                i.Add("Interval must be greater than zero");
            }

            var settings = NaztronomyToolsPlugin.Instance;

            if (settings == null) {
                i.Add("Naztronomy Tools plugin is not loaded");
            } else {
                if (string.IsNullOrWhiteSpace(settings.DiscordWebhookUrl)) {
                    i.Add("Discord webhook URL is not set in the plugin settings");
                } else if (!Uri.IsWellFormedUriString(settings.DiscordWebhookUrl, UriKind.Absolute)) {
                    i.Add("Discord webhook URL in the plugin settings is invalid");
                }

                if (string.IsNullOrWhiteSpace(settings.DiscordMessage) && string.IsNullOrWhiteSpace(settings.DiscordImageUrl)) {
                    i.Add("Set a message, an image URL, or both in the plugin settings");
                }
            }

            Issues = i;
            RaisePropertyChanged(nameof(Issues));

            return i.Count == 0;
        }

        public override object Clone() {
            return new DiscordCustomImageMessageTrigger(this) {
                Interval = Interval,
                Unit = Unit,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}, Interval: {IntervalSummary}";
        }

        public void Dispose() {
            StopTimer();
        }
    }
}
