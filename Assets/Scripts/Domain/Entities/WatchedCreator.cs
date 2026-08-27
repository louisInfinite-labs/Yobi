namespace Yobi.Domain.Entities
{
    public sealed class WatchedCreator
    {
        public string ChannelId { get; }
        public string DisplayName { get; }

        public WatchedCreator(string channelId, string displayName)
        {
            ChannelId = channelId;
            DisplayName = displayName;
        }
    }
}
