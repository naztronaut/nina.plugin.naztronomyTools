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
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Naztronomy.NINA.Tools.TargetScheduler {

    /// <summary>
    /// A single entry in the Target Scheduler plan preview: either a scheduled target or a wait period.
    /// </summary>
    public class SchedulerPreviewItem {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool WaitPeriod { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<SchedulerPreviewExposure> ExposurePlan { get; set; } = new List<SchedulerPreviewExposure>();
    }

    public class SchedulerPreviewExposure {
        public string FilterName { get; set; }
        public double Exposure { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Minimal client for the Target Scheduler plugin's HTTP API. The API is optional and must be
    /// enabled by the user in the Target Scheduler profile preferences (default port 8188).
    /// </summary>
    public static class TargetSchedulerApiClient {
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        static TargetSchedulerApiClient() {
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Fetches the plan preview for the given profile. When <paramref name="startTime"/> is null,
        /// Target Scheduler uses its own default (1:00 PM of the current day).
        /// </summary>
        public static async Task<List<SchedulerPreviewItem>> GetPreviewAsync(string host, int port, string profileId, DateTime? startTime, CancellationToken token) {
            if (string.IsNullOrWhiteSpace(host)) {
                throw new ArgumentException("Target Scheduler API host is not set");
            }

            var url = $"http://{host}:{port}/ts/v0/profiles/{profileId}/preview";
            if (startTime.HasValue) {
                url += $"?startTime={Uri.EscapeDataString(startTime.Value.ToString("o"))}";
            }

            HttpResponseMessage response;
            try {
                response = await httpClient.GetAsync(url, token);
            } catch (Exception ex) {
                throw new Exception($"Could not reach the Target Scheduler API at {host}:{port}. Is the API enabled in Target Scheduler? ({ex.Message})");
            }

            using (response) {
                var body = await response.Content.ReadAsStringAsync(token);

                if (!response.IsSuccessStatusCode) {
                    throw new Exception($"Target Scheduler API returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
                }

                return JsonConvert.DeserializeObject<List<SchedulerPreviewItem>>(body) ?? new List<SchedulerPreviewItem>();
            }
        }

        /// <summary>
        /// Formats a plan preview into a human-readable message, truncated to fit a Discord embed description.
        /// </summary>
        public static string FormatPreview(IReadOnlyList<SchedulerPreviewItem> items, int maxLength = 3900) {
            if (items == null || items.Count == 0) {
                return "No active targets are scheduled.";
            }

            var sb = new StringBuilder();
            DateTime? lastEnd = null;

            foreach (var item in items) {
                if (item.WaitPeriod) {
                    sb.AppendLine($"\u23f3 Wait until {item.EndTime:yyyy-MM-dd HH:mm}");
                    sb.AppendLine();
                    continue;
                }

                sb.AppendLine($"**{item.Name}**");
                sb.AppendLine($"{item.StartTime:yyyy-MM-dd HH:mm} \u2192 {item.EndTime:HH:mm}");

                if (item.ExposurePlan != null && item.ExposurePlan.Count > 0) {
                    var parts = item.ExposurePlan
                        .Where(e => e.Count > 0)
                        .Select(e => $"{e.Count}\u00d7 {e.FilterName} {e.Exposure:0}s");
                    var line = string.Join(", ", parts);
                    if (!string.IsNullOrEmpty(line)) {
                        sb.AppendLine(line);
                    }
                }

                sb.AppendLine();
                lastEnd = item.EndTime;
            }

            if (lastEnd != null) {
                sb.AppendLine($"End at {lastEnd:yyyy-MM-dd HH:mm}");
            }

            var text = sb.ToString().TrimEnd();

            if (text.Length > maxLength) {
                text = text.Substring(0, maxLength) + "\u2026";
            }

            return text;
        }
    }
}
