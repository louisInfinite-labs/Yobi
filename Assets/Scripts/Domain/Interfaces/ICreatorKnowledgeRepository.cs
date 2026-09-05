using System.Threading;
using System.Threading.Tasks;
using Yobi.Domain.Entities;

namespace Yobi.Domain.Interfaces
{
    public interface ICreatorKnowledgeRepository
    {
        Task<CreatorKnowledgeBase> LoadAsync(CancellationToken cancellationToken);
    }
}
