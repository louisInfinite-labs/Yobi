using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Application.UseCases
{
    public sealed class SearchCreatorsUseCase
    {
        private readonly ICreatorSearchProvider _searchProvider;

        public SearchCreatorsUseCase(ICreatorSearchProvider searchProvider)
        {
            _searchProvider = searchProvider;
        }

        public async Task<IReadOnlyList<CreatorSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return System.Array.Empty<CreatorSearchResult>();
            }

            return await _searchProvider.SearchAsync(query.Trim(), cancellationToken);
        }
    }
}
