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

    [ExportMetadata("Name", "Discord - Custom Image Message")]
    [ExportMetadata("Description", "Posts the message and embedded image configured in the Naztronomy Tools plugin settings to a Discord webhook on a fixed time interval.")]
    [ExportMetadata("Icon", "NaztronomyToolsSVG")]
    [ExportMetadata("Category", "Naztronomy Tools")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class DiscordCustomImageMessageTrigger : SequenceTrigger, IValidatable {
        private int intervalSeconds = 3600;

        private DateTime lastTriggerTime = DateTime.MinValue;

        [ImportingConstructor]
        public DiscordCustomImageMessageTrigger() {
            Validate();
        }

        private DiscordCustomImageMessageTrigger(DiscordCustomImageMessageTrigger copyMe) : this() {
            CopyMetaData(copyMe);
        }

        [JsonProperty]
        public int IntervalSeconds {
            get => intervalSeconds;
            set {
                intervalSeconds = Math.Max(1, value);
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            lastTriggerTime = DateTime.Now;

            if (!Validate()) {
                Logger.Error($"{Name}: not sending Discord message because the configuration is invalid: {string.Join(", ", Issues)}");
                return;
            }

            var settings = NaztronomyToolsPlugin.Instance;

            Logger.Info($"{Name}: posting custom image message to Discord");
            progress?.Report(new ApplicationStatus { Status = "Posting message to Discord" });

            await DiscordWebhookSender.SendAsync(settings.DiscordWebhookUrl, settings.DiscordMessage, settings.DiscordImageUrl, token);
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            // Start the countdown the first time the trigger is evaluated.
            if (lastTriggerTime == DateTime.MinValue) {
                lastTriggerTime = DateTime.Now;
                return false;
            }

            return (DateTime.Now - lastTriggerTime).TotalSeconds >= IntervalSeconds;
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>();

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
                IntervalSeconds = IntervalSeconds,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}, Interval: {IntervalSeconds}s";
        }
    }
}
