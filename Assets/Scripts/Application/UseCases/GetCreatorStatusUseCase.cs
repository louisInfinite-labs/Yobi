using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Application.UseCases
{
    public sealed class GetCreatorStatusUseCase
    {
        private readonly ICreatorLivestreamStatusProvider _livestreamStatusProvider;

        public GetCreatorStatusUseCase(ICreatorLivestreamStatusProvider livestreamStatusProvider)
        {
            _livestreamStatusProvider = livestreamStatusProvider;
        }

        public async Task<CreatorStatus> GetStatusAsync(ChannelIdentity channel, bool isWatchlisted, CancellationToken cancellationToken)
        {
            var snapshot = await _livestreamStatusProvider.GetStatusAsync(channel.ChannelId, cancellationToken);
            var channelUrl = $"https://www.youtube.com/channel/{channel.ChannelId}";

            return new CreatorStatus(
                channel.ChannelId,
                channel.Name,
                channelUrl,
                snapshot.Studio,
                isWatchlisted,
                snapshot.LiveStatus,
                snapshot.CurrentLivestream,
                snapshot.UpcomingLivestreams);
        }

        public async Task<IReadOnlyList<CreatorStatus>> GetStatusesAsync(IReadOnlyList<ChannelIdentity> channels, bool isWatchlisted, CancellationToken cancellationToken)
        {
            var results = new List<CreatorStatus>(channels.Count);

            foreach (var channel in channels)
            {
                results.Add(await GetStatusAsync(channel, isWatchlisted, cancellationToken));
            }

            return results;
        }
    }
}
