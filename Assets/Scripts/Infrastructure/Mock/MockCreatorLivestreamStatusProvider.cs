using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Infrastructure.Mock
{
    public sealed class MockCreatorLivestreamStatusProvider : ICreatorLivestreamStatusProvider
    {
        private readonly TimeSpan _startOffsetFromNow;

        public MockCreatorLivestreamStatusProvider(TimeSpan startOffsetFromNow)
        {
            _startOffsetFromNow = startOffsetFromNow;
        }

        public Task<CreatorLivestreamSnapshot> GetStatusAsync(string channelId, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var scheduledStartUtc = now + _startOffsetFromNow;

            LivestreamInfo currentLivestream = null;
            var upcoming = new List<LivestreamInfo>();

            if (scheduledStartUtc <= now)
            {
                currentLivestream = new LivestreamInfo("Mock Creator", $"Mock Livestream ({channelId})", scheduledStartUtc, $"mock-video-{channelId}");
            }
            else if (scheduledStartUtc <= now.AddHours(24))
            {
                upcoming.Add(new LivestreamInfo("Mock Creator", $"Mock Livestream ({channelId})", scheduledStartUtc, $"mock-video-{channelId}"));
            }

            return Task.FromResult(new CreatorLivestreamSnapshot(currentLivestream, upcoming, "MockStudio"));
        }
    }
}
