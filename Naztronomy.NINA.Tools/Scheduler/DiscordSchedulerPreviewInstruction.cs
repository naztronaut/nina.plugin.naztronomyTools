#region "copyright"

/*
    Copyright (c) 2026 Naztronomy

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using Naztronomy.NINA.Tools.Discord;
using Naztronomy.NINA.Tools.TargetScheduler;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Naztronomy.NINA.Tools.Scheduler {

    [ExportMetadata("Name", "Discord - Scheduler Preview")]
    [ExportMetadata("Description", "Fetches the Target Scheduler plan preview via its API and posts it to the Discord webhook. Requires the Target Scheduler API to be enabled.")]
    [ExportMetadata("Icon", "NaztronomyToolsSVG")]
    [ExportMetadata("Category", "Naztronomy Tools")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class DiscordSchedulerPreviewInstruction : SequenceItem, IValidatable {

        [ImportingConstructor]
        public DiscordSchedulerPreviewInstruction() {
            Validate();
        }

        private DiscordSchedulerPreviewInstruction(DiscordSchedulerPreviewInstruction copyMe) : this() {
            CopyMetaData(copyMe);
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (!Validate()) {
                throw new Exception(string.Join(", ", Issues));
            }

            var settings = NaztronomyToolsPlugin.Instance;

            progress?.Report(new ApplicationStatus { Status = "Fetching Target Scheduler preview" });

            var items = await TargetSchedulerApiClient.GetPreviewAsync(
                settings.TargetSchedulerApiHost,
                settings.TargetSchedulerApiPort,
                settings.ActiveProfileId,
                startTime: null,
                token);

            var preview = TargetSchedulerApiClient.FormatPreview(items);

            var message = new DiscordWebhookMessage {
                WebhookUrl = settings.DiscordWebhookUrl,
                Username = settings.DiscordUsername,
                EmbedTitle = "\U0001f52d Scheduler Preview",
                EmbedDescription = preview,
                EmbedFooter = settings.DiscordEmbedFooter,
                EmbedColor = DiscordWebhookMessage.ParseColor(settings.DiscordEmbedColor),
                IncludeTimestamp = true,
            };

            Logger.Info($"{Name}: posting Target Scheduler preview to Discord ({items.Count} entries)");
            progress?.Report(new ApplicationStatus { Status = "Posting scheduler preview to Discord" });

            await DiscordWebhookSender.SendAsync(message, token);
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

                if (string.IsNullOrWhiteSpace(settings.TargetSchedulerApiHost)) {
                    i.Add("Target Scheduler API host is not set in the plugin settings");
                }

                if (settings.TargetSchedulerApiPort <= 0 || settings.TargetSchedulerApiPort > 65535) {
                    i.Add("Target Scheduler API port is invalid");
                }
            }

            Issues = i;
            RaisePropertyChanged(nameof(Issues));

            return i.Count == 0;
        }

        public override object Clone() {
            return new DiscordSchedulerPreviewInstruction(this);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}";
        }
    }
}
