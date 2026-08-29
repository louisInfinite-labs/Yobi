using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;
using Yobi.Infrastructure.Http;

namespace Yobi.Infrastructure.Api.Holodex
{
    public sealed class HolodexApiClient : ICreatorSearchProvider, ICreatorLivestreamStatusProvider
    {
        private const string BaseUrl = "https://holodex.net/api/v2";

        private readonly string _apiKey;

        public HolodexApiClient(string apiKey)
        {
            _apiKey = apiKey;
        }

        public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                var url = $"{BaseUrl}/videos?limit=1";
                using var request = UnityWebRequest.Get(url);
                request.SetRequestHeader("X-APIKEY", _apiKey);

                var json = await UnityWebRequestAsync.SendAsync(request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                var wrapped = JsonUtility.FromJson<VideoListWrapperDto>("{\"items\":" + json + "}");
                var itemCount = wrapped?.items?.Length ?? 0;
                return ConnectionTestResult.Success(itemCount);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ConnectionTestResult.Failure(ex.Message);
            }
        }

        public async Task<IReadOnlyList<CreatorSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
        {
            var url = $"{BaseUrl}/search/autocomplete?q={UnityWebRequest.EscapeURL(query)}";
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("X-APIKEY", _apiKey);

            var json = await UnityWebRequestAsync.SendAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var wrapped = JsonUtility.FromJson<AutocompleteWrapperDto>("{\"items\":" + json + "}");
            var results = new List<CreatorSearchResult>();
            if (wrapped?.items == null)
            {
                return results;
            }

            foreach (var item in wrapped.items)
            {
                if (item.type != "channel" || string.IsNullOrEmpty(item.value) || string.IsNullOrEmpty(item.text))
                {
                    continue;
                }

                var channelUrl = $"https://www.youtube.com/channel/{item.value}";
                results.Add(new CreatorSearchResult(item.value, item.text, channelUrl));
            }

            return results;
        }

        public async Task<CreatorLivestreamSnapshot> GetStatusAsync(string channelId, CancellationToken cancellationToken)
        {
            var url = $"{BaseUrl}/videos?channel_id={UnityWebRequest.EscapeURL(channelId)}&type=stream&status=live,upcoming&include=live_info&limit=10";
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("X-APIKEY", _apiKey);

            var json = await UnityWebRequestAsync.SendAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var wrapped = JsonUtility.FromJson<HolodexVideoListWrapperDto>("{\"items\":" + json + "}");

            LivestreamInfo currentLivestream = null;
            var upcoming = new List<LivestreamInfo>();
            string studio = null;
            var now = DateTime.UtcNow;
            var windowEnd = now.AddHours(24);

            if (wrapped?.items != null)
            {
                foreach (var item in wrapped.items)
                {
                    if (string.IsNullOrEmpty(item.id))
                    {
                        continue;
                    }

                    var scheduledStartUtc = default(DateTime);
                    if (!string.IsNullOrEmpty(item.start_scheduled))
                    {
                        DateTime.TryParse(
                            item.start_scheduled,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out scheduledStartUtc);
                    }

                    var channelName = item.channel?.name ?? string.Empty;
                    var livestream = new LivestreamInfo(channelName, item.title ?? string.Empty, scheduledStartUtc, item.id);

                    // This same call's nested "channel" object happens to carry "org" - reuse it
                    // rather than making a dedicated /channels/{id} request. If nothing here has
                    // it (e.g. no live/upcoming video at all), Studio just stays null.
                    if (string.IsNullOrEmpty(studio) && !string.IsNullOrEmpty(item.channel?.org))
                    {
                        studio = item.channel.org;
                    }

                    if (item.status == "live")
                    {
                        currentLivestream = livestream;
                    }
                    else if (item.status == "upcoming" && scheduledStartUtc > now && scheduledStartUtc <= windowEnd)
                    {
                        upcoming.Add(livestream);
                    }
                }
            }

            upcoming.Sort((a, b) => a.ScheduledStartUtc.CompareTo(b.ScheduledStartUtc));
            return new CreatorLivestreamSnapshot(currentLivestream, upcoming, studio);
        }

        [Serializable]
        private sealed class VideoListWrapperDto
        {
            public VideoSummaryDto[] items;
        }

        [Serializable]
        private sealed class VideoSummaryDto
        {
            public string id;
        }

        [Serializable]
        private sealed class AutocompleteWrapperDto
        {
            public AutocompleteItemDto[] items;
        }

        [Serializable]
        private sealed class AutocompleteItemDto
        {
            public string type;
            public string value;
            public string text;
        }

        [Serializable]
        private sealed class HolodexVideoListWrapperDto
        {
            public HolodexVideoDto[] items;
        }

        [Serializable]
        private sealed class HolodexVideoDto
        {
            public string id;
            public string title;
            public string status;

            // Field name intentionally mirrors Holodex's wire format (snake_case) so
            // JsonUtility, which matches JSON keys to field names verbatim, can bind it.
            public string start_scheduled;

            public HolodexVideoChannelDto channel;
        }

        [Serializable]
        private sealed class HolodexVideoChannelDto
        {
            public string id;
            public string name;
            public string org;
        }
    }
}
