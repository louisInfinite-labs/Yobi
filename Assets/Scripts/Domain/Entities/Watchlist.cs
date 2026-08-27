using System.Collections.Generic;

namespace Yobi.Domain.Entities
{
    public sealed class Watchlist
    {
        private readonly List<WatchedCreator> _items = new List<WatchedCreator>();
        private readonly HashSet<string> _channelIds = new HashSet<string>();

        public IReadOnlyList<WatchedCreator> Items => _items;

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
