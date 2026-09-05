using Yobi.Domain.Entities;

namespace Yobi.Domain.Interfaces
{
    public interface IWindowPositionRepository
    {
        // Null if nothing has been saved yet (first run) - callers fall back to leaving the
        // window wherever it opened at by default.
        WindowPosition Load();

        void Save(WindowPosition position);
    }
}
