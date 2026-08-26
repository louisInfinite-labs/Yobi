using System;

namespace Yobi.Infrastructure.YouTube
{
    [Serializable]
    internal sealed class ChannelListResponseDto
    {
        public ChannelItemDto[] items;
    }

    [Serializable]
    internal sealed class ChannelItemDto
    {
        public string id;
    }

    [Serializable]
    internal sealed class SearchListResponseDto
    {
        public SearchItemDto[] items;
    }

    [Serializable]
    internal sealed class SearchItemDto
    {
        public SearchItemIdDto id;
    }

    [Serializable]
    internal sealed class SearchItemIdDto
    {
        public string videoId;
    }

    [Serializable]
    internal sealed class VideoListResponseDto
    {
        public VideoItemDto[] items;
    }

    [Serializable]
    internal sealed class VideoItemDto
    {
        public string id;
        public VideoSnippetDto snippet;
        public LiveStreamingDetailsDto liveStreamingDetails;
    }

    [Serializable]
    internal sealed class VideoSnippetDto
    {
        public string title;
    }

    [Serializable]
    internal sealed class LiveStreamingDetailsDto
    {
        public string scheduledStartTime;
    }
}
