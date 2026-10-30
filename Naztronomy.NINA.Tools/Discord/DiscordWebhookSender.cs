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

        /// <summary>
        /// Posts a message and (optionally) an embedded image to the given Discord webhook URL.
        /// </summary>
        /// <param name="webhookUrl">The Discord incoming webhook URL.</param>
        /// <param name="message">The message content. May be empty if an image is provided.</param>
        /// <param name="imageUrl">A direct or Dropbox share URL to an image to embed. May be empty.</param>
        /// <param name="token">A cancellation token.</param>
        public static async Task SendAsync(string webhookUrl, string message, string imageUrl, CancellationToken token) {
            if (string.IsNullOrWhiteSpace(webhookUrl)) {
                throw new ArgumentException("Discord webhook URL is not set");
            }

            var normalizedImage = NormalizeImageUrl(imageUrl);

            if (string.IsNullOrWhiteSpace(message) && string.IsNullOrWhiteSpace(normalizedImage)) {
                throw new ArgumentException("Nothing to send: both the message and the image URL are empty");
            }

            var payload = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(message)) {
                payload["content"] = message;
            }

            if (!string.IsNullOrWhiteSpace(normalizedImage)) {
                payload["embeds"] = new[] {
                    new Dictionary<string, object> {
                        ["image"] = new Dictionary<string, object> {
                            ["url"] = normalizedImage
                        }
                    }
                };
            }

            var json = JsonConvert.SerializeObject(payload);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(webhookUrl, content, token);

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
    }
}
