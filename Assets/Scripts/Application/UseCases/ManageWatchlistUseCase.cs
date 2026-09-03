using System.Collections.Generic;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Application.UseCases
{
    public enum WatchlistAddResult
    {
        Added,
        AlreadyExists
    }

    public sealed class ManageWatchlistUseCase
    {
        private readonly IWatchlistRepository _repository;
        private readonly Watchlist _watchlist;

        public ManageWatchlistUseCase(IWatchlistRepository repository)
        {
            _repository = repository;
            _watchlist = _repository.Load();
        }

        public WatchlistAddResult Add(string channelId, string displayName, string channelUrl)
        {
            var added = _watchlist.TryAdd(new WatchedCreator(channelId, displayName, channelUrl));
            if (added)
            {
                _repository.Save(_watchlist);
            }

            return added ? WatchlistAddResult.Added : WatchlistAddResult.AlreadyExists;
        }

        public bool Remove(string channelId)
        {
            var removed = _watchlist.Remove(channelId);
            if (removed)
            {
                _repository.Save(_watchlist);
            }

            return removed;
        }

        public bool SetEnabled(string channelId, bool isEnabled)
        {
            var changed = _watchlist.TrySetEnabled(channelId, isEnabled);
            if (changed)
            {
                _repository.Save(_watchlist);
            }

            return changed;
        }

        public IReadOnlyList<WatchedCreator> GetAll()
        {
            return _watchlist.Items;
        }
    }
}
