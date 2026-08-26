using System.Collections.Generic;
using Yobi.Domain.Entities;

namespace Yobi.Application.Models
{
    public sealed class ChannelLivestreamResult
    {
        public YouTubeChannel Channel { get; }
        public IReadOnlyList<LivestreamInfo> Livestreams { get; }

        public ChannelLivestreamResult(YouTubeChannel channel, IReadOnlyList<LivestreamInfo> livestreams)
        {
            Channel = channel;
            Livestreams = livestreams;
        }
    }
}
