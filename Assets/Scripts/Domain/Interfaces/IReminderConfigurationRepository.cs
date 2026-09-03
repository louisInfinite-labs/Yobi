using Yobi.Domain.Entities;

namespace Yobi.Domain.Interfaces
{
    public interface IReminderConfigurationRepository
    {
        // If nothing is persisted yet, implementations seed storage from defaultConfiguration
        // and return it, so first run behaves the same as the previous Inspector-only defaults.
        ReminderConfiguration Load(ReminderConfiguration defaultConfiguration);

        void Save(ReminderConfiguration configuration);
    }
}
