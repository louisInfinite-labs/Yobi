namespace Yobi.Domain.Entities
{
    public sealed class WatchedCreator
    {
        public string ChannelId { get; }
        public string DisplayName { get; }
        public string ChannelUrl { get; }
        public bool IsEnabled { get; private set; }

        public WatchedCreator(string channelId, string displayName, string channelUrl, bool isEnabled = true)
        {
            ChannelId = channelId;
            DisplayName = displayName;
            ChannelUrl = channelUrl;
            IsEnabled = isEnabled;
        }

        public void SetEnabled(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }
    }
}
