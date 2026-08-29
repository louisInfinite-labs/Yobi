using System.Collections.Generic;
using Yobi.Domain.Entities;

namespace Yobi.Application.UseCases
{
    public enum WatchlistAddResult
    {
        Added,
        AlreadyExists
    }

    public sealed class ManageWatchlistUseCase
    {
        private readonly Watchlist _watchlist = new Watchlist();

        public WatchlistAddResult Add(string channelId, string displayName, string channelUrl)
        {
            var added = _watchlist.TryAdd(new WatchedCreator(channelId, displayName, channelUrl));
            return added ? WatchlistAddResult.Added : WatchlistAddResult.AlreadyExists;
        }

        public IReadOnlyList<WatchedCreator> GetAll()
        {
            return _watchlist.Items;
        }
    }
}
