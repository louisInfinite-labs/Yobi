namespace Yobi.Domain.Interfaces
{
    public interface IRoomWallpaperRepository
    {
        // Null if the user hasn't chosen a wallpaper yet - callers fall back to the Room mode
        // placeholder background.
        string Load();

        void Save(string imageFilePath);
    }
}
