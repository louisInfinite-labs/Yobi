using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yobi.Domain.Entities;

namespace Yobi.Domain.Interfaces
{
    public interface ICreatorSearchProvider
    {
        Task<IReadOnlyList<CreatorSearchResult>> SearchAsync(string query, CancellationToken cancellationToken);
    }
}
