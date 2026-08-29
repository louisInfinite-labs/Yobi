using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;
using Yobi.Infrastructure.Http;

namespace Yobi.Infrastructure.YouTube
{
    public sealed class YouTubeDataApiLivestreamRepository : IYouTubeLivestreamRepository
    {
        private const string BaseUrl = "https://www.googleapis.com/youtube/v3";

        private readonly string _apiKey;

        public YouTubeDataApiLivestreamRepository(string apiKey)
        {
            _apiKey = apiKey;
        }

        public async Task<IReadOnlyList<LivestreamInfo>> GetUpcomingLivestreamsAsync(YouTubeChannel channel, CancellationToken cancellationToken)
        {
            var channelId = !string.IsNullOrEmpty(channel.ChannelId)
                ? channel.ChannelId
                : await ResolveChannelIdAsync(channel.Handle, cancellationToken);

            if (string.IsNullOrEmpty(channelId))
            {
                return Array.Empty<LivestreamInfo>();
            }

            var videoIds = await SearchUpcomingVideoIdsAsync(channelId, cancellationToken);
            if (videoIds.Count == 0)
            {
                return Array.Empty<LivestreamInfo>();
            }

            return await GetLivestreamDetailsAsync(channel.Name, videoIds, cancellationToken);
        }

        // Cheapest authenticated call available (1 quota unit, no configured channel required) -
        // used purely to verify the API key is still valid, mirroring HolodexApiClient's
        // TestConnectionAsync.
        public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                var url = $"{BaseUrl}/videos?part=id&chart=mostPopular&maxResults=1&key={_apiKey}";
                using var request = UnityWebRequest.Get(url);
                var json = await UnityWebRequestAsync.SendAsync(request);
                cancellationToken.ThrowIfCancellationRequested();

                var response = JsonUtility.FromJson<VideoListResponseDto>(json);
                var itemCount = response?.items?.Length ?? 0;
                return ConnectionTestResult.Success(itemCount);
            }
            catch (Exception ex)
            {
                return ConnectionTestResult.Failure(ex.Message);
            }
        }

        private async Task<string> ResolveChannelIdAsync(string handle, CancellationToken cancellationToken)
        {
            var url = $"{BaseUrl}/channels?part=id&forHandle={UnityWebRequest.EscapeURL(handle)}&key={_apiKey}";
            using var request = UnityWebRequest.Get(url);
            var json = await UnityWebRequestAsync.SendAsync(request);
            cancellationToken.ThrowIfCancellationRequested();

            var response = JsonUtility.FromJson<ChannelListResponseDto>(json);
            return response?.items != null && response.items.Length > 0 ? response.items[0].id : null;
        }

        private async Task<List<string>> SearchUpcomingVideoIdsAsync(string channelId, CancellationToken cancellationToken)
        {
            var url = $"{BaseUrl}/search?part=snippet&channelId={channelId}&eventType=upcoming&type=video&order=date&key={_apiKey}";
            using var request = UnityWebRequest.Get(url);
            var json = await UnityWebRequestAsync.SendAsync(request);
            cancellationToken.ThrowIfCancellationRequested();

            var response = JsonUtility.FromJson<SearchListResponseDto>(json);
            if (response?.items == null)
            {
                return new List<string>();
            }

            return response.items
                .Where(item => item.id != null && !string.IsNullOrEmpty(item.id.videoId))
                .Select(item => item.id.videoId)
                .ToList();
        }

        private async Task<IReadOnlyList<LivestreamInfo>> GetLivestreamDetailsAsync(string channelName, List<string> videoIds, CancellationToken cancellationToken)
        {
            var url = $"{BaseUrl}/videos?part=snippet,liveStreamingDetails&id={string.Join(",", videoIds)}&key={_apiKey}";
            using var request = UnityWebRequest.Get(url);
            var json = await UnityWebRequestAsync.SendAsync(request);
            cancellationToken.ThrowIfCancellationRequested();

            var response = JsonUtility.FromJson<VideoListResponseDto>(json);
            var results = new List<LivestreamInfo>();
            if (response?.items == null)
            {
                return results;
            }

            foreach (var item in response.items)
            {
                var scheduledStart = item.liveStreamingDetails?.scheduledStartTime;
                if (string.IsNullOrEmpty(scheduledStart))
                {
                    continue;
                }

                if (!DateTime.TryParse(scheduledStart,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,out var startUtc))

                {

                    continue;

                }

                results.Add(new LivestreamInfo(channelName, item.snippet?.title ?? string.Empty, startUtc, item.id));
            }

            return results;
        }
    }
}
