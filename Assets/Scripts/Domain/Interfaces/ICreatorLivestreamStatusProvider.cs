using System.Threading;
using System.Threading.Tasks;
using Yobi.Domain.Entities;

namespace Yobi.Domain.Interfaces
{
    public interface ICreatorLivestreamStatusProvider
    {
        Task<CreatorLivestreamSnapshot> GetStatusAsync(string channelId, CancellationToken cancellationToken);
    }
}
