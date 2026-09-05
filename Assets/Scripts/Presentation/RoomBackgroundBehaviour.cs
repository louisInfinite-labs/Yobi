using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Yobi.Domain.Interfaces;
using Yobi.Infrastructure.Storage;

namespace Yobi.Presentation
{
    // Displays the Room mode's user-chosen wallpaper behind everything else. Hidden while in
    // DesktopMate mode - that mode wants full transparency instead (see
    // DesktopCompanionWindowBehaviour, which toggles this via SetVisible on mode switch).
    public sealed class RoomBackgroundBehaviour : MonoBehaviour
    {
        [SerializeField]
        private Image backgroundImage;

        // A UI Image with no sprite still renders as a solid white rect (Unity's built-in
        // white texture, tinted by Image.color) - it's not "nothing". So the image can only be
        // enabled once a wallpaper has actually loaded; otherwise Room mode's plain background
        // color (set on the camera) would be hidden behind an opaque white rectangle instead of
        // showing through as the intended placeholder.
        private bool _hasWallpaper;
        private bool _visibilityRequested;

        private IRoomWallpaperRepository _repository;

        private IRoomWallpaperRepository Repository =>
            _repository ??= new LocalFileRoomWallpaperRepository();

        private void Awake()
        {
            var savedPath = Repository.Load();
            if (!string.IsNullOrEmpty(savedPath))
            {
                LoadAndApply(savedPath);
            }
        }

        // Toggles the Image's own enabled state rather than this GameObject's active state:
        // this component must stay discoverable via FindFirstObjectByType (which excludes
        // inactive objects by default) so TrayIconBehaviour can still reach it to apply a newly
        // chosen wallpaper while hidden in DesktopMate mode.
        public void SetVisible(bool visible)
        {
            _visibilityRequested = visible;
            UpdateImageEnabled();
        }

        // Called after the native file picker returns a chosen image path. Persists it only if
        // it actually loads as a valid image, so a bad/deleted-mid-pick file can't leave the
        // saved wallpaper pointing at something unreadable.
        public void SetWallpaper(string imageFilePath)
        {
            if (string.IsNullOrEmpty(imageFilePath))
            {
                return;
            }

            if (!LoadAndApply(imageFilePath))
            {
                return;
            }

            Repository.Save(imageFilePath);
        }

        private bool LoadAndApply(string imageFilePath)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(imageFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoomBackgroundBehaviour] Failed to read wallpaper file {imageFilePath}: {ex.Message}");
                return false;
            }

            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(bytes))
            {
                Debug.LogError($"[RoomBackgroundBehaviour] {imageFilePath} is not a readable image.");
                return false;
            }

            if (backgroundImage != null)
            {
                backgroundImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }

            _hasWallpaper = true;
            UpdateImageEnabled();
            return true;
        }

        private void UpdateImageEnabled()
        {
            if (backgroundImage != null)
            {
                backgroundImage.enabled = _visibilityRequested && _hasWallpaper;
            }
        }
    }
}
