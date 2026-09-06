using Yobi.Domain.Entities;

namespace Yobi.Domain.Interfaces
{
    public interface IQueryHistoryRepository
    {
        QueryHistory Load();

        void Save(QueryHistory history);
    }
}
