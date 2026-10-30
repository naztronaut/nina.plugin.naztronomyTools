#region "copyright"

/*
    Copyright (c) 2026 Naztronomy

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using Naztronomy.NINA.Tools.Discord;
using Naztronomy.NINA.Tools.Utility;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Naztronomy.NINA.Tools {

    /// <summary>
    /// Plugin manifest and global options for Naztronomy Tools.
    /// The options UI is defined by the DataTemplate with key "Naztronomy Tools_Options"
    /// (see Options.xaml), which matches the AssemblyTitle.
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class NaztronomyToolsPlugin : PluginBase, INotifyPropertyChanged {
        private readonly PluginOptionsAccessor pluginSettings;
        private readonly IProfileService profileService;

        /// <summary>
        /// A static handle to the loaded plugin so sequence items/triggers can read the
        /// configured defaults without being composed with the manifest instance.
        /// </summary>
        public static NaztronomyToolsPlugin Instance { get; private set; }

        [ImportingConstructor]
        public NaztronomyToolsPlugin(IProfileService profileService) {
            this.profileService = profileService;
            this.pluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(Identifier));

            this.profileService.ProfileChanged += ProfileService_ProfileChanged;

            SendTestDiscordMessageCommand = new RelayCommand(_ => _ = SendTestDiscordMessageAsync());

            Instance = this;
        }

        public override Task Teardown() {
            profileService.ProfileChanged -= ProfileService_ProfileChanged;
            return base.Teardown();
        }

        private void ProfileService_ProfileChanged(object sender, EventArgs e) {
            RaisePropertyChanged(nameof(DiscordWebhookUrl));
            RaisePropertyChanged(nameof(DiscordImageUrl));
            RaisePropertyChanged(nameof(DiscordMessage));
        }

        public string DiscordWebhookUrl {
            get => Security.Decrypt(pluginSettings.GetValueString(nameof(DiscordWebhookUrl), string.Empty));
            set {
                pluginSettings.SetValueString(nameof(DiscordWebhookUrl), Security.Encrypt(value?.Trim() ?? string.Empty));
                RaisePropertyChanged();
            }
        }

        public string DiscordImageUrl {
            get => Security.Decrypt(pluginSettings.GetValueString(nameof(DiscordImageUrl), string.Empty));
            set {
                pluginSettings.SetValueString(nameof(DiscordImageUrl), Security.Encrypt(value?.Trim() ?? string.Empty));
                RaisePropertyChanged();
            }
        }

        public string DiscordMessage {
            get => pluginSettings.GetValueString(nameof(DiscordMessage), string.Empty);
            set {
                pluginSettings.SetValueString(nameof(DiscordMessage), value ?? string.Empty);
                RaisePropertyChanged();
            }
        }

        public ICommand SendTestDiscordMessageCommand { get; }

        private async Task SendTestDiscordMessageAsync() {
            try {
                await DiscordWebhookSender.SendAsync(DiscordWebhookUrl, DiscordMessage, DiscordImageUrl, CancellationToken.None);
                // Notification.ShowSuccess("Naztronomy Tools: Discord test message sent");
            } catch (Exception ex) {
                Logger.Error($"Naztronomy Tools: failed to send Discord test message: {ex}");
                Notification.ShowExternalError($"Failed to send Discord message:{Environment.NewLine}{ex.Message}", "Naztronomy Tools");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
