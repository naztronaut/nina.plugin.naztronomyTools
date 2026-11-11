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
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Naztronomy.NINA.Tools.Discord {

    /// <summary>
    /// Sends messages to a Discord channel through an incoming webhook using a plain HTTP POST.
    /// Images can be embedded from an http(s) URL or uploaded directly from a local file, avoiding a
    /// dependency on a full Discord client library.
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

            var embed = new Dictionary<string, object>();

            // Resolve the image: an http(s) URL is embedded directly, a local file is uploaded as an attachment.
            string attachmentFileName = null;
            byte[] attachmentBytes = null;
            var imageInput = message.ImageUrl?.Trim();

            if (!string.IsNullOrWhiteSpace(imageInput)) {
                if (IsHttpUrl(imageInput)) {
                    var url = NormalizeImageUrl(imageInput);
                    if (message.BustImageCache) {
                        url = AppendCacheBuster(url);
                    }
                    embed["image"] = new Dictionary<string, object> { ["url"] = url };
                } else if (File.Exists(imageInput)) {
                    attachmentBytes = File.ReadAllBytes(imageInput);
                    attachmentFileName = BuildAttachmentFileName(imageInput, message.BustImageCache);
                    embed["image"] = new Dictionary<string, object> { ["url"] = $"attachment://{attachmentFileName}" };
                } else {
                    throw new ArgumentException($"Image is neither an http(s) URL nor an existing file: {imageInput}");
                }
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
                throw new ArgumentException("Nothing to send: the message, image and embed fields are all empty");
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

            HttpResponseMessage response;

            if (attachmentBytes != null) {
                using var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(json, Encoding.UTF8, "application/json"), "payload_json");

                var fileContent = new ByteArrayContent(attachmentBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(GuessContentType(attachmentFileName));
                multipart.Add(fileContent, "files[0]", attachmentFileName);

                response = await httpClient.PostAsync(message.WebhookUrl, multipart, token);
            } else {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                response = await httpClient.PostAsync(message.WebhookUrl, content, token);
            }

            using (response) {
                if (!response.IsSuccessStatusCode) {
                    var body = await response.Content.ReadAsStringAsync(token);
                    throw new Exception($"Discord webhook returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
                }
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

        private static bool IsHttpUrl(string value) {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static string BuildAttachmentFileName(string path, bool bustCache) {
            var name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(name)) {
                name = "image";
            }

            if (bustCache) {
                var stem = Path.GetFileNameWithoutExtension(name);
                var ext = Path.GetExtension(name);
                name = $"{stem}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
            }

            return SanitizeFileName(name);
        }

        private static string SanitizeFileName(string name) {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (var c in name) {
                sb.Append(c == ' ' || Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }
            return sb.ToString();
        }

        private static string GuessContentType(string fileName) {
            switch (Path.GetExtension(fileName)?.ToLowerInvariant()) {
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".gif": return "image/gif";
                case ".webp": return "image/webp";
                case ".bmp": return "image/bmp";
                case ".tif":
                case ".tiff": return "image/tiff";
                default: return "application/octet-stream";
            }
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
