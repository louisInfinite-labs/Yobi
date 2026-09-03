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

        public bool Remove(string channelId)
        {
            if (!_channelIds.Remove(channelId))
            {
                return false;
            }

            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i].ChannelId == channelId)
                {
                    _items.RemoveAt(i);
                    break;
                }
            }

            return true;
        }

        public bool TrySetEnabled(string channelId, bool isEnabled)
        {
            foreach (var creator in _items)
            {
                if (creator.ChannelId == channelId)
                {
                    creator.SetEnabled(isEnabled);
                    return true;
                }
            }

            return false;
        }
    }
}
