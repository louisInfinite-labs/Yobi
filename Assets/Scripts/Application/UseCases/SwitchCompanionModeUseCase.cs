using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Application.UseCases
{
    // Tracks and persists which of the two companion window modes (DesktopMate/Room) is
    // active. Deliberately doesn't touch the native window itself - that's a Presentation-layer
    // concern (DesktopCompanionWindowBehaviour), since applying DesktopMate style needs to fight
    // a Unity/macOS timing quirk (see its ApplyDesktopMateStyleOverFirstFrames) that has nothing
    // to do with which mode is persisted.
    public sealed class SwitchCompanionModeUseCase
    {
        private readonly ICompanionModeRepository _repository;
        private CompanionMode _currentMode;

        public SwitchCompanionModeUseCase(ICompanionModeRepository repository, CompanionMode defaultMode)
        {
            _repository = repository;
            _currentMode = repository.Load(defaultMode);
        }

        public CompanionMode CurrentMode => _currentMode;

        public CompanionMode Toggle()
        {
            _currentMode = _currentMode == CompanionMode.DesktopMate ? CompanionMode.Room : CompanionMode.DesktopMate;
            _repository.Save(_currentMode);
            return _currentMode;
        }
    }
}
