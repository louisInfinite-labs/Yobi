using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Yobi.Domain.Entities
{
    public sealed class Watchlist
    {
        private readonly List<WatchedCreator> _items = new List<WatchedCreator>();
        private readonly HashSet<string> _channelIds = new HashSet<string>();
        private readonly ReadOnlyCollection<WatchedCreator> _itemsReadOnly;

        public Watchlist()
        {
            _itemsReadOnly = _items.AsReadOnly();
        }

        public IReadOnlyList<WatchedCreator> Items => _itemsReadOnly;

        public bool TryAdd(WatchedCreator creator)
        {
            if (!_channelIds.Add(creator.ChannelId))
            {
                return false;
            }

            _items.Add(creator);
            return true;
        }
    }
}
