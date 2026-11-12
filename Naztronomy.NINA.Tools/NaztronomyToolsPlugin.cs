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
using Microsoft.Win32;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
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
        private readonly LogMonitor.LogMonitorService logMonitor;

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
            BrowseImageFileCommand = new RelayCommand(_ => BrowseImageFile());

            Instance = this;

            logMonitor = new LogMonitor.LogMonitorService(this);
            if (LogMonitorEnabled) {
                logMonitor.Start();
            }
        }

        public override Task Teardown() {
            logMonitor?.Dispose();
            profileService.ProfileChanged -= ProfileService_ProfileChanged;
            return base.Teardown();
        }

        private void ProfileService_ProfileChanged(object sender, EventArgs e) {
            RaisePropertyChanged(nameof(DiscordWebhookUrl));
            RaisePropertyChanged(nameof(DiscordImageUrl));
            RaisePropertyChanged(nameof(DiscordMessage));
            RaisePropertyChanged(nameof(DiscordUsername));
            RaisePropertyChanged(nameof(DiscordEmbedTitle));
            RaisePropertyChanged(nameof(DiscordEmbedFooter));
            RaisePropertyChanged(nameof(DiscordEmbedColor));
            RaisePropertyChanged(nameof(DiscordBypassImageCache));
            RaisePropertyChanged(nameof(TargetSchedulerApiHost));
            RaisePropertyChanged(nameof(TargetSchedulerApiPort));
            RaisePropertyChanged(nameof(LogMonitorNotifyOnErrors));
            RaisePropertyChanged(nameof(LogMonitorNotifyOnWarnings));
            RaisePropertyChanged(nameof(LogMonitorKeywords));
            RaisePropertyChanged(nameof(LogMonitorUsername));
            RaisePropertyChanged(nameof(LogMonitorEmbedTitle));
            RaisePropertyChanged(nameof(LogMonitorEnabled));
            ApplyLogMonitorState();
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

        public string DiscordUsername {
            get => pluginSettings.GetValueString(nameof(DiscordUsername), string.Empty);
            set {
                pluginSettings.SetValueString(nameof(DiscordUsername), value?.Trim() ?? string.Empty);
                RaisePropertyChanged();
            }
        }

        public string DiscordEmbedTitle {
            get => pluginSettings.GetValueString(nameof(DiscordEmbedTitle), string.Empty);
            set {
                pluginSettings.SetValueString(nameof(DiscordEmbedTitle), value ?? string.Empty);
                RaisePropertyChanged();
            }
        }

        public string DiscordEmbedFooter {
            get => pluginSettings.GetValueString(nameof(DiscordEmbedFooter), string.Empty);
            set {
                pluginSettings.SetValueString(nameof(DiscordEmbedFooter), value ?? string.Empty);
                RaisePropertyChanged();
            }
        }

        public string DiscordEmbedColor {
            get => pluginSettings.GetValueString(nameof(DiscordEmbedColor), "#2E86DE");
            set {
                pluginSettings.SetValueString(nameof(DiscordEmbedColor), value?.Trim() ?? string.Empty);
                RaisePropertyChanged();
            }
        }

        public bool DiscordBypassImageCache {
            get => pluginSettings.GetValueBoolean(nameof(DiscordBypassImageCache), true);
            set {
                pluginSettings.SetValueBoolean(nameof(DiscordBypassImageCache), value);
                RaisePropertyChanged();
            }
        }

        public string TargetSchedulerApiHost {
            get => pluginSettings.GetValueString(nameof(TargetSchedulerApiHost), "localhost");
            set {
                pluginSettings.SetValueString(nameof(TargetSchedulerApiHost), value?.Trim() ?? string.Empty);
                RaisePropertyChanged();
            }
        }

        public int TargetSchedulerApiPort {
            get => pluginSettings.GetValueInt32(nameof(TargetSchedulerApiPort), 8188);
            set {
                pluginSettings.SetValueInt32(nameof(TargetSchedulerApiPort), value);
                RaisePropertyChanged();
            }
        }

        public bool LogMonitorNotifyOnErrors {
            get => pluginSettings.GetValueBoolean(nameof(LogMonitorNotifyOnErrors), true);
            set {
                pluginSettings.SetValueBoolean(nameof(LogMonitorNotifyOnErrors), value);
                RaisePropertyChanged();
            }
        }

        public bool LogMonitorNotifyOnWarnings {
            get => pluginSettings.GetValueBoolean(nameof(LogMonitorNotifyOnWarnings), false);
            set {
                pluginSettings.SetValueBoolean(nameof(LogMonitorNotifyOnWarnings), value);
                RaisePropertyChanged();
            }
        }

        public string LogMonitorKeywords {
            get => pluginSettings.GetValueString(nameof(LogMonitorKeywords), string.Empty);
            set {
                pluginSettings.SetValueString(nameof(LogMonitorKeywords), value ?? string.Empty);
                RaisePropertyChanged();
            }
        }

        public string LogMonitorUsername {
            get => pluginSettings.GetValueString(nameof(LogMonitorUsername), string.Empty);
            set {
                pluginSettings.SetValueString(nameof(LogMonitorUsername), value?.Trim() ?? string.Empty);
                RaisePropertyChanged();
            }
        }

        public string LogMonitorEmbedTitle {
            get => pluginSettings.GetValueString(nameof(LogMonitorEmbedTitle), string.Empty);
            set {
                pluginSettings.SetValueString(nameof(LogMonitorEmbedTitle), value ?? string.Empty);
                RaisePropertyChanged();
            }
        }

        public bool LogMonitorEnabled {
            get => pluginSettings.GetValueBoolean(nameof(LogMonitorEnabled), false);
            set {
                pluginSettings.SetValueBoolean(nameof(LogMonitorEnabled), value);
                RaisePropertyChanged();
                ApplyLogMonitorState();
            }
        }

        private void ApplyLogMonitorState() {
            if (LogMonitorEnabled) {
                logMonitor?.Start();
            } else {
                logMonitor?.Stop();
            }
        }

        /// <summary>
        /// The configured log-monitor phrases, one per line, with blanks removed.
        /// </summary>
        public IReadOnlyList<string> GetLogMonitorKeywords() {
            return (LogMonitorKeywords ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => k.Length > 0)
                .ToList();
        }

        /// <summary>
        /// The id of the currently active NINA profile, used to query the Target Scheduler API.
        /// </summary>
        public string ActiveProfileId => profileService.ActiveProfile.Id.ToString();

        /// <summary>
        /// Builds a Discord message from the current plugin settings.
        /// </summary>
        public DiscordWebhookMessage BuildDiscordMessage() {
            return new DiscordWebhookMessage {
                WebhookUrl = DiscordWebhookUrl,
                Username = DiscordUsername,
                Content = DiscordMessage,
                ImageUrl = DiscordImageUrl,
                EmbedTitle = DiscordEmbedTitle,
                EmbedFooter = DiscordEmbedFooter,
                EmbedColor = DiscordWebhookMessage.ParseColor(DiscordEmbedColor),
                IncludeTimestamp = true,
                BustImageCache = DiscordBypassImageCache,
            };
        }

        public ICommand SendTestDiscordMessageCommand { get; }

        public ICommand BrowseImageFileCommand { get; }

        private void BrowseImageFile() {
            var dialog = new OpenFileDialog {
                Title = "Select an image to send to Discord",
                Filter = "Images|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.bmp;*.tif;*.tiff|All files|*.*",
                CheckFileExists = true,
            };

            var current = DiscordImageUrl;
            if (!string.IsNullOrWhiteSpace(current) && System.IO.File.Exists(current)) {
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(current);
                dialog.FileName = System.IO.Path.GetFileName(current);
            }

            if (dialog.ShowDialog() == true) {
                DiscordImageUrl = dialog.FileName;
            }
        }

        private async Task SendTestDiscordMessageAsync() {
            try {
                await DiscordWebhookSender.SendAsync(BuildDiscordMessage(), CancellationToken.None);
                Notification.ShowSuccess("Naztronomy Tools: Discord test message sent");
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
