using Yobi.Domain.Entities;

namespace Yobi.Domain.Interfaces
{
    public interface ICompanionModeRepository
    {
        // If nothing is persisted yet, implementations seed storage from defaultMode and
        // return it, so first run behaves the same as if it had always been persisted.
        CompanionMode Load(CompanionMode defaultMode);

        void Save(CompanionMode mode);
    }
}
