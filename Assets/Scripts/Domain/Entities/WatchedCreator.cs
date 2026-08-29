namespace Yobi.Domain.Entities
{
    public sealed class WatchedCreator
    {
        public string ChannelId { get; }
        public string DisplayName { get; }
        public string ChannelUrl { get; }

        public WatchedCreator(string channelId, string displayName, string channelUrl)
        {
            ChannelId = channelId;
            DisplayName = displayName;
            ChannelUrl = channelUrl;
        }
    }
}
