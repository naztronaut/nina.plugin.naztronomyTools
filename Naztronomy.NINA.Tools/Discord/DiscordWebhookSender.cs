#region "copyright"

/*
    Copyright (c) 2026 Naztronomy

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Naztronomy.NINA.Tools.Discord {

    /// <summary>
    /// Sends messages to a Discord channel through an incoming webhook using a plain JSON POST.
    /// This avoids a dependency on a full Discord client library for the simple case of posting
    /// a message with an embedded image referenced by URL.
    /// </summary>
    public static class DiscordWebhookSender {
        private static readonly HttpClient httpClient = new HttpClient();

        static DiscordWebhookSender() {
            httpClient.Timeout = TimeSpan.FromSeconds(100);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NaztronomyTools (+https://github.com/Naztronaut/NINA-Plugins)");
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Posts a message to the given Discord webhook. The message may contain plain content,
        /// an embedded image and additional embed styling (title, footer, color, timestamp).
        /// </summary>
        public static async Task SendAsync(DiscordWebhookMessage message, CancellationToken token) {
            if (message == null) {
                throw new ArgumentNullException(nameof(message));
            }

            if (string.IsNullOrWhiteSpace(message.WebhookUrl)) {
                throw new ArgumentException("Discord webhook URL is not set");
            }

            var normalizedImage = NormalizeImageUrl(message.ImageUrl);

            if (message.BustImageCache && !string.IsNullOrWhiteSpace(normalizedImage)) {
                normalizedImage = AppendCacheBuster(normalizedImage);
            }

            var embed = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(normalizedImage)) {
                embed["image"] = new Dictionary<string, object> { ["url"] = normalizedImage };
            }
            if (!string.IsNullOrWhiteSpace(message.EmbedTitle)) {
                embed["title"] = message.EmbedTitle;
            }
            if (!string.IsNullOrWhiteSpace(message.EmbedDescription)) {
                embed["description"] = message.EmbedDescription;
            }
            if (!string.IsNullOrWhiteSpace(message.EmbedFooter)) {
                embed["footer"] = new Dictionary<string, object> { ["text"] = message.EmbedFooter };
            }
            if (message.EmbedColor.HasValue) {
                embed["color"] = message.EmbedColor.Value;
            }
            if (embed.Count > 0 && message.IncludeTimestamp) {
                embed["timestamp"] = DateTimeOffset.UtcNow.ToString("o");
            }

            var hasContent = !string.IsNullOrWhiteSpace(message.Content);

            if (!hasContent && embed.Count == 0) {
                throw new ArgumentException("Nothing to send: the message, image URL and embed fields are all empty");
            }

            var payload = new Dictionary<string, object>();

            if (hasContent) {
                payload["content"] = message.Content;
            }
            if (!string.IsNullOrWhiteSpace(message.Username)) {
                payload["username"] = message.Username;
            }
            if (embed.Count > 0) {
                payload["embeds"] = new[] { embed };
            }

            var json = JsonConvert.SerializeObject(payload);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(message.WebhookUrl, content, token);

            if (!response.IsSuccessStatusCode) {
                var body = await response.Content.ReadAsStringAsync(token);
                throw new Exception($"Discord webhook returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
            }
        }

        /// <summary>
        /// Converts a Dropbox share link into a direct image link that Discord can embed.
        /// Non-Dropbox URLs are returned trimmed and unchanged.
        /// </summary>
        public static string NormalizeImageUrl(string url) {
            if (string.IsNullOrWhiteSpace(url)) {
                return url;
            }

            var trimmed = url.Trim();

            if (trimmed.IndexOf("dropbox.com", StringComparison.OrdinalIgnoreCase) < 0) {
                return trimmed;
            }

            // Dropbox share links use dl=0 (preview) or dl=1 (download). raw=1 serves the file
            // inline so Discord can render it as an image.
            trimmed = trimmed
                .Replace("dl=0", "raw=1")
                .Replace("dl=1", "raw=1");

            if (trimmed.IndexOf("raw=1", StringComparison.OrdinalIgnoreCase) < 0) {
                trimmed += (trimmed.Contains('?') ? "&" : "?") + "raw=1";
            }

            return trimmed;
        }

        /// <summary>
        /// Appends a unique query parameter so Discord's image proxy treats each post as a new image
        /// instead of serving a cached copy. Useful for frequently-updated images such as all-sky cameras.
        /// </summary>
        public static string AppendCacheBuster(string url) {
            if (string.IsNullOrWhiteSpace(url)) {
                return url;
            }

            var separator = url.Contains('?') ? "&" : "?";
            return url + separator + "naz_cb=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// Describes a single message to post to a Discord webhook.
    /// </summary>
    public class DiscordWebhookMessage {
        public string WebhookUrl { get; set; }
        public string Username { get; set; }
        public string Content { get; set; }
        public string ImageUrl { get; set; }
        public string EmbedTitle { get; set; }
        public string EmbedDescription { get; set; }
        public string EmbedFooter { get; set; }
        public int? EmbedColor { get; set; }
        public bool IncludeTimestamp { get; set; } = true;
        public bool BustImageCache { get; set; }

        /// <summary>
        /// Parses an "#RRGGBB" (or "RRGGBB") string into the integer color value Discord expects.
        /// Returns null when the input is empty or not a valid 6-digit hex color.
        /// </summary>
        public static int? ParseColor(string hex) {
            if (string.IsNullOrWhiteSpace(hex)) {
                return null;
            }

            var s = hex.Trim().TrimStart('#');

            if (s.Length == 6 && int.TryParse(s, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value)) {
                return value;
            }

            return null;
        }
    }
}
