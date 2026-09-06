using Yobi.Domain.Entities;

namespace Yobi.Domain.Interfaces
{
    public interface IAppSettingsRepository
    {
        // If nothing is persisted yet, implementations seed storage from defaultSettings and
        // return it, so first run behaves the same as if it had always been persisted.
        AppSettings Load(AppSettings defaultSettings);

        void Save(AppSettings settings);
    }
}
