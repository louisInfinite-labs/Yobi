using Yobi.Domain.Entities;

namespace Yobi.Domain.Interfaces
{
    public interface IWatchlistRepository
    {
        Watchlist Load();

        void Save(Watchlist watchlist);
    }
}
