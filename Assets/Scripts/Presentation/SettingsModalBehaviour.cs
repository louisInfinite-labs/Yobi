using System.Collections.Generic;
using AOT;
using UnityEngine;
using UnityEngine.UI;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;
using Yobi.Infrastructure.FilePicker;
using Yobi.Infrastructure.Storage;

namespace Yobi.Presentation
{
    // The centered CONFIG-style modal (Display / Sound / Other tabs) - opened via the Room UI's
    // Settings button. Display's resolution/fullscreen controls and Other's notification toggle
    // are real; Language and Sound only persist a choice for now (no i18n system or audio
    // playback exists yet to apply them to - see roadmap discussion).
    public sealed class SettingsModalBehaviour : MonoBehaviour
    {
        // Rooted so the GC can't collect it - see TrayIconBehaviour for why.
        private static readonly MacFilePicker.FilePickedCallback WallpaperPickedDelegate = OnWallpaperPicked;
        private static RoomBackgroundBehaviour _roomBackground;

        [SerializeField]
        private GameObject modalRoot;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private Button displayTabButton;

        [SerializeField]
        private Button soundTabButton;

        [SerializeField]
        private Button otherTabButton;

        [SerializeField]
        private GameObject displayTabContent;

        [SerializeField]
        private GameObject soundTabContent;

        [SerializeField]
        private GameObject otherTabContent;

        [SerializeField]
        private Dropdown languageDropdown;

        [SerializeField]
        private Dropdown resolutionDropdown;

        [SerializeField]
        private Toggle fullscreenToggle;

        [SerializeField]
        private Button wallpaperButton;

        [SerializeField]
        private Toggle soundMuteToggle;

        [SerializeField]
        private Slider soundVolumeSlider;

        [SerializeField]
        private Toggle notificationsToggle;

        private static readonly string[] LanguageCodes = { "zh-TW", "en", "ja" };
        private static readonly string[] LanguageLabels = { "繁體中文", "English", "日本語" };

        private IAppSettingsRepository _repository;
        private AppSettings _settings;
        private List<(int width, int height, string label)> _availableResolutions;
        private bool _isApplyingLoadedState;

        private void Awake()
        {
            _repository = new LocalFileAppSettingsRepository();
            _roomBackground = FindFirstObjectByType<RoomBackgroundBehaviour>();

            var currentResolution = Screen.currentResolution;
            var defaultSettings = new AppSettings(
                languageCode: "zh-TW",
                resolutionWidth: currentResolution.width,
                resolutionHeight: currentResolution.height,
                fullscreen: Screen.fullScreen,
                soundMuted: false,
                soundVolume: 1f,
                notificationsEnabled: true);
            _settings = _repository.Load(defaultSettings);

            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }

            SetupResolutionOptions();
            SetupLanguageOptions();
            ApplyLoadedStateToControls();

            if (openButton != null)
            {
                openButton.onClick.AddListener(Open);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (displayTabButton != null)
            {
                displayTabButton.onClick.AddListener(() => ShowTab(displayTabContent));
            }

            if (soundTabButton != null)
            {
                soundTabButton.onClick.AddListener(() => ShowTab(soundTabContent));
            }

            if (otherTabButton != null)
            {
                otherTabButton.onClick.AddListener(() => ShowTab(otherTabContent));
            }

            if (wallpaperButton != null)
            {
                wallpaperButton.onClick.AddListener(OnWallpaperButtonClicked);
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
            }

            if (resolutionDropdown != null)
            {
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            }

            if (languageDropdown != null)
            {
                languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
            }

            if (soundMuteToggle != null)
            {
                soundMuteToggle.onValueChanged.AddListener(OnSoundMuteToggled);
            }

            if (soundVolumeSlider != null)
            {
                soundVolumeSlider.onValueChanged.AddListener(OnSoundVolumeChanged);
            }

            if (notificationsToggle != null)
            {
                notificationsToggle.onValueChanged.AddListener(OnNotificationsToggled);
            }
        }

        public void Open()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(true);
            }

            ShowTab(displayTabContent);
        }

        public void Close()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        private void ShowTab(GameObject tabToShow)
        {
            if (displayTabContent != null)
            {
                displayTabContent.SetActive(displayTabContent == tabToShow);
            }

            if (soundTabContent != null)
            {
                soundTabContent.SetActive(soundTabContent == tabToShow);
            }

            if (otherTabContent != null)
            {
                otherTabContent.SetActive(otherTabContent == tabToShow);
            }
        }

        // A curated, human-picked set of common resolutions rather than every entry
        // Screen.resolutions reports (which repeats each pixel size at every supported refresh
        // rate and can run to dozens of entries) - the dropdown only has room for a short list.
        private static readonly (int width, int height, string label)[] ResolutionPresets =
        {
            (1024, 768, "1024 x 768"),
            (1280, 1024, "1280 x 1024"),
            (1920, 1080, "1920 x 1080 (Full HD)"),
            (2560, 1440, "2560 x 1440 (2K)"),
            (3840, 2160, "3840 x 2160 (4K)"),
        };

        private void SetupResolutionOptions()
        {
            if (resolutionDropdown == null)
            {
                return;
            }

            var currentResolution = Screen.currentResolution;

            // Only offer presets the current display can actually show - no point letting
            // someone pick 4K on a 1920x1080 monitor.
            _availableResolutions = new List<(int width, int height, string label)>();
            foreach (var preset in ResolutionPresets)
            {
                if (preset.width <= currentResolution.width && preset.height <= currentResolution.height)
                {
                    _availableResolutions.Add(preset);
                }
            }

            if (_availableResolutions.Count == 0)
            {
                _availableResolutions.Add((currentResolution.width, currentResolution.height, $"{currentResolution.width} x {currentResolution.height}"));
            }

            var options = new List<string>();
            foreach (var resolution in _availableResolutions)
            {
                options.Add(resolution.label);
            }

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
        }

        private void SetupLanguageOptions()
        {
            if (languageDropdown == null)
            {
                return;
            }

            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(new List<string>(LanguageLabels));
        }

        private void ApplyLoadedStateToControls()
        {
            _isApplyingLoadedState = true;

            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = _settings.Fullscreen;
            }

            if (resolutionDropdown != null && _availableResolutions != null)
            {
                var index = _availableResolutions.FindIndex(r => r.width == _settings.ResolutionWidth && r.height == _settings.ResolutionHeight);
                resolutionDropdown.value = index >= 0 ? index : 0;
            }

            if (languageDropdown != null)
            {
                var index = System.Array.IndexOf(LanguageCodes, _settings.LanguageCode);
                languageDropdown.value = index >= 0 ? index : 0;
            }

            if (soundMuteToggle != null)
            {
                soundMuteToggle.isOn = _settings.SoundMuted;
            }

            if (soundVolumeSlider != null)
            {
                soundVolumeSlider.value = _settings.SoundVolume;
            }

            if (notificationsToggle != null)
            {
                notificationsToggle.isOn = _settings.NotificationsEnabled;
            }

            _isApplyingLoadedState = false;
        }

        private void OnFullscreenToggled(bool isFullscreen)
        {
            if (_isApplyingLoadedState)
            {
                return;
            }

            Screen.fullScreen = isFullscreen;
            Persist(fullscreen: isFullscreen);
        }

        private void OnResolutionChanged(int index)
        {
            if (_isApplyingLoadedState || _availableResolutions == null || index < 0 || index >= _availableResolutions.Count)
            {
                return;
            }

            var resolution = _availableResolutions[index];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode);
            Persist(resolutionWidth: resolution.width, resolutionHeight: resolution.height);
        }

        private void OnLanguageChanged(int index)
        {
            if (_isApplyingLoadedState || index < 0 || index >= LanguageCodes.Length)
            {
                return;
            }

            // Persisted only - no i18n system exists yet to actually retranslate the UI text.
            Persist(languageCode: LanguageCodes[index]);
        }

        private void OnSoundMuteToggled(bool isMuted)
        {
            if (_isApplyingLoadedState)
            {
                return;
            }

            // Persisted only - there is no audio playback in the app yet for this to control.
            Persist(soundMuted: isMuted);
        }

        private void OnSoundVolumeChanged(float volume)
        {
            if (_isApplyingLoadedState)
            {
                return;
            }

            Persist(soundVolume: volume);
        }

        private void OnNotificationsToggled(bool isEnabled)
        {
            if (_isApplyingLoadedState)
            {
                return;
            }

            // Takes effect on next launch: CreatorSearchPanelBehaviour reads this once at its
            // own Awake() to decide whether to request authorization and schedule OS
            // notifications at all - it doesn't cancel already-scheduled ones live.
            Persist(notificationsEnabled: isEnabled);
        }

        private void OnWallpaperButtonClicked()
        {
            // MacFilePicker is a macOS-only native plugin - see TrayIconBehaviour's identical
            // gate for the same reason.
            if (UnityEngine.Application.platform == RuntimePlatform.OSXPlayer)
            {
                MacFilePicker.ShowImageOpenPanel(WallpaperPickedDelegate);
            }
        }

        [MonoPInvokeCallback(typeof(MacFilePicker.FilePickedCallback))]
        private static void OnWallpaperPicked(string path)
        {
            if (_roomBackground != null)
            {
                _roomBackground.SetWallpaper(path);
            }
        }

        private void Persist(
            string languageCode = null,
            int? resolutionWidth = null,
            int? resolutionHeight = null,
            bool? fullscreen = null,
            bool? soundMuted = null,
            float? soundVolume = null,
            bool? notificationsEnabled = null)
        {
            _settings = new AppSettings(
                languageCode ?? _settings.LanguageCode,
                resolutionWidth ?? _settings.ResolutionWidth,
                resolutionHeight ?? _settings.ResolutionHeight,
                fullscreen ?? _settings.Fullscreen,
                soundMuted ?? _settings.SoundMuted,
                soundVolume ?? _settings.SoundVolume,
                notificationsEnabled ?? _settings.NotificationsEnabled);

            _repository.Save(_settings);
        }
    }
}
