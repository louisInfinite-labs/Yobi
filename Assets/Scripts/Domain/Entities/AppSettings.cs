namespace Yobi.Domain.Entities
{
    // A single persisted bundle for the Settings modal's three tabs (Display / Sound / Other)
    // rather than one entity per tab - they're all loaded, edited, and saved together as one
    // unit from the same modal, so splitting them wouldn't buy any independent lifecycle.
    public sealed class AppSettings
    {
        public string LanguageCode { get; }
        public int ResolutionWidth { get; }
        public int ResolutionHeight { get; }
        public bool Fullscreen { get; }
        public bool SoundMuted { get; }
        public float SoundVolume { get; }
        public bool NotificationsEnabled { get; }

        public AppSettings(
            string languageCode,
            int resolutionWidth,
            int resolutionHeight,
            bool fullscreen,
            bool soundMuted,
            float soundVolume,
            bool notificationsEnabled)
        {
            LanguageCode = languageCode;
            ResolutionWidth = resolutionWidth;
            ResolutionHeight = resolutionHeight;
            Fullscreen = fullscreen;
            SoundMuted = soundMuted;
            SoundVolume = soundVolume < 0f ? 0f : soundVolume > 1f ? 1f : soundVolume;
            NotificationsEnabled = notificationsEnabled;
        }
    }
}
