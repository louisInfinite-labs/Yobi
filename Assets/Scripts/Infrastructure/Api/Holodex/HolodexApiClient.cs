using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;
using Yobi.Infrastructure.Http;

namespace Yobi.Infrastructure.Api.Holodex
{
    public sealed class HolodexApiClient : ICreatorSearchProvider
    {
        private const string BaseUrl = "https://holodex.net/api/v2";

        private readonly string _apiKey;

        public HolodexApiClient(string apiKey)
        {
            _apiKey = apiKey;
        }

        public async Task<HolodexConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                var url = $"{BaseUrl}/videos?limit=1";
                using var request = UnityWebRequest.Get(url);
                request.SetRequestHeader("X-APIKEY", _apiKey);

                var json = await UnityWebRequestAsync.SendAsync(request);
                cancellationToken.ThrowIfCancellationRequested();

                var wrapped = JsonUtility.FromJson<VideoListWrapperDto>("{\"items\":" + json + "}");
                var itemCount = wrapped?.items?.Length ?? 0;
                return HolodexConnectionTestResult.Success(itemCount);
            }
            catch (Exception ex)
            {
                return HolodexConnectionTestResult.Failure(ex.Message);
            }
        }

        public async Task<IReadOnlyList<CreatorSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
        {
            var url = $"{BaseUrl}/search/autocomplete?q={UnityWebRequest.EscapeURL(query)}";
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("X-APIKEY", _apiKey);

            var json = await UnityWebRequestAsync.SendAsync(request);
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

                results.Add(new CreatorSearchResult(item.value, item.text));
            }

            return results;
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
    }

    public readonly struct HolodexConnectionTestResult
    {
        public bool IsSuccess { get; }
        public int ItemCount { get; }
        public string ErrorMessage { get; }

        private HolodexConnectionTestResult(bool isSuccess, int itemCount, string errorMessage)
        {
            IsSuccess = isSuccess;
            ItemCount = itemCount;
            ErrorMessage = errorMessage;
        }

        public static HolodexConnectionTestResult Success(int itemCount) => new HolodexConnectionTestResult(true, itemCount, null);

        public static HolodexConnectionTestResult Failure(string errorMessage) => new HolodexConnectionTestResult(false, 0, errorMessage);
    }
}
