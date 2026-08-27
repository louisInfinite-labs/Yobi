using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Infrastructure.Mock
{
    public sealed class MockYouTubeLivestreamRepository : IYouTubeLivestreamRepository
    {
        private readonly TimeSpan _startOffsetFromNow;

        public MockYouTubeLivestreamRepository(TimeSpan startOffsetFromNow)
        {
            _startOffsetFromNow = startOffsetFromNow;
        }

        public Task<IReadOnlyList<LivestreamInfo>> GetUpcomingLivestreamsAsync(YouTubeChannel channel, CancellationToken cancellationToken)
        {
            var scheduledStartUtc = DateTime.UtcNow + _startOffsetFromNow;
            var videoId = $"mock-{channel.Handle ?? channel.Name}";

            IReadOnlyList<LivestreamInfo> result = new List<LivestreamInfo>
            {
                new LivestreamInfo(channel.Name, $"Mock Livestream ({channel.Name})", scheduledStartUtc, videoId)
            };

            return Task.FromResult(result);
        }
    }
}
