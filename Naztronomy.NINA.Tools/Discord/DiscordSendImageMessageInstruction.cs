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
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Naztronomy.NINA.Tools.Discord {

    [ExportMetadata("Name", "Discord - Send Image Message")]
    [ExportMetadata("Description", "Posts the message and embedded image configured in the Naztronomy Tools plugin settings to a Discord webhook once.")]
    [ExportMetadata("Icon", "NaztronomyToolsSVG")]
    [ExportMetadata("Category", "Naztronomy Tools")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class DiscordSendImageMessageInstruction : SequenceItem, IValidatable {

        [ImportingConstructor]
        public DiscordSendImageMessageInstruction() {
            Validate();
        }

        private DiscordSendImageMessageInstruction(DiscordSendImageMessageInstruction copyMe) : this() {
            CopyMetaData(copyMe);
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (!Validate()) {
                throw new Exception(string.Join(", ", Issues));
            }

            var settings = NaztronomyToolsPlugin.Instance;

            Logger.Info($"{Name}: posting custom image message to Discord");
            progress?.Report(new ApplicationStatus { Status = "Posting message to Discord" });

            await DiscordWebhookSender.SendAsync(settings.BuildDiscordMessage(), token);
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
            return new DiscordSendImageMessageInstruction(this);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}";
        }
    }
}
