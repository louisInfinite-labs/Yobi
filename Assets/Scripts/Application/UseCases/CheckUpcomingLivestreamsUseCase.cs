using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Yobi.Application.Models;
using Yobi.Domain.Interfaces;

namespace Yobi.Application.UseCases
{
    public sealed class CheckUpcomingLivestreamsUseCase
    {
        private static readonly TimeSpan LookaheadWindow = TimeSpan.FromHours(24);

        private readonly IChannelConfigProvider _configProvider;
        private readonly IYouTubeLivestreamRepository _repository;

        public CheckUpcomingLivestreamsUseCase(IChannelConfigProvider configProvider, IYouTubeLivestreamRepository repository)
        {
            _configProvider = configProvider;
            _repository = repository;
        }

        public async Task<IReadOnlyList<ChannelLivestreamResult>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var channels = _configProvider.GetChannels();
            var results = new List<ChannelLivestreamResult>();
            var now = DateTime.UtcNow;
            var windowEnd = now + LookaheadWindow;

            foreach (var channel in channels)
            {
                var livestreams = await _repository.GetUpcomingLivestreamsAsync(channel, cancellationToken);
                var withinWindow = livestreams
                    .Where(l => l.ScheduledStartUtc >= now && l.ScheduledStartUtc <= windowEnd)
                    .OrderBy(l => l.ScheduledStartUtc)
                    .ToList();
                results.Add(new ChannelLivestreamResult(channel, withinWindow));
            }

            return results;
        }
    }
}
