namespace Yobi.Domain.Entities
{
    public sealed class CreatorSearchResult
    {
        public string ChannelId { get; }
        public string DisplayName { get; }
        public string ChannelUrl { get; }

        public CreatorSearchResult(string channelId, string displayName, string channelUrl)
        {
            ChannelId = channelId;
            DisplayName = displayName;
            ChannelUrl = channelUrl;
        }
    }
}
