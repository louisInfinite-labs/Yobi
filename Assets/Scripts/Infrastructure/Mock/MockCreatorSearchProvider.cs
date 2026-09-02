using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Infrastructure.Mock
{
    public sealed class MockCreatorSearchProvider : ICreatorSearchProvider
    {
        private static readonly (string ChannelId, string Name)[] MockCreators =
        {
            ("mock-channel-1", "藍沢エマ（Mock）"),
            ("mock-channel-2", "常闇トワ（Mock）"),
        };

        public Task<IReadOnlyList<CreatorSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
        {
            var results = new List<CreatorSearchResult>();
            var normalizedQuery = query?.ToLowerInvariant() ?? string.Empty;

            foreach (var creator in MockCreators)
            {
                if (normalizedQuery.Length > 0 && !creator.Name.ToLowerInvariant().Contains(normalizedQuery))
                {
                    continue;
                }

                var channelUrl = $"https://www.youtube.com/channel/{creator.ChannelId}";
                results.Add(new CreatorSearchResult(creator.ChannelId, creator.Name, channelUrl));
            }

            return Task.FromResult<IReadOnlyList<CreatorSearchResult>>(results);
        }
    }
}
