using System.Collections.Generic;

namespace Yobi.Domain.Entities
{
    // A capped, most-recent-first list of past search-bar queries, shown back to the user the
    // way Google's search box shows recent searches. Unlike Watchlist there's no identity/dedup
    // machinery needed beyond "don't show the same query twice" - re-searching something already
    // in the list just moves it back to the front instead of duplicating it.
    public sealed class QueryHistory
    {
        private readonly List<string> _entries;

        public QueryHistory(IEnumerable<string> entries = null)
        {
            _entries = entries != null ? new List<string>(entries) : new List<string>();
        }

        public IReadOnlyList<string> Entries => _entries;

        public void Add(string query, int maxEntries)
        {
            _entries.Remove(query);
            _entries.Insert(0, query);

            if (_entries.Count > maxEntries)
            {
                _entries.RemoveRange(maxEntries, _entries.Count - maxEntries);
            }
        }
    }
}
