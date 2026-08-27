using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yobi.Domain.Entities;

namespace Yobi.Domain.Interfaces
{
    public interface IYouTubeLivestreamRepository
    {
        Task<IReadOnlyList<LivestreamInfo>> GetUpcomingLivestreamsAsync(YouTubeChannel channel, CancellationToken cancellationToken);
    }
}
